using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Globalization;
using market_api.Data;
using market_api.Models;

namespace market_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _httpClient = httpClientFactory.CreateClient("NOWPayments");
            _configuration = configuration;
            _logger = logger;
        }

        // GET: api/Payment/currencies
        [HttpGet("currencies")]
        public async Task<ActionResult<List<string>>> GetAvailableCurrencies()
        {
            try
            {
                var apiKey = _configuration["NOWPayments:ApiKey"];
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

                var response = await _httpClient.GetAsync("https://api.nowpayments.io/v1/currencies");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var currenciesResponse = JsonSerializer.Deserialize<NowPaymentsCurrenciesResponse>(content);
                    return Ok(currenciesResponse.Currencies);
                }

                return StatusCode((int)response.StatusCode, "Error retrieving available currencies");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting available currencies");
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Payment/admin/fake-payment/{orderId}
        [HttpPost("admin/fake-payment/{orderId}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AdminFakePayment(int orderId)
        {
            try
            {
                _logger.LogInformation("Admin fake payment initiated for order {OrderId}", orderId);

                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return NotFound(new { message = "Order not found" });
                }

                // Проверяем, что заказ еще не оплачен
                if (order.Status == "Completed" || order.Status == "Confirmed")
                {
                    return BadRequest(new { message = "Order is already paid" });
                }

                // Создаем фейковый ID платежа
                var fakePaymentId = $"FAKE_{Guid.NewGuid().ToString("N").Substring(0, 12)}";

                // Обновляем заказ как полностью оплаченный
                order.Status = "Completed";
                order.CompletedAt = DateTime.UtcNow;
                order.PaymentId = fakePaymentId;
                order.PaymentUrl = null; // Очищаем URL оплаты

                // Убедимся, что PaymentCurrency установлен
                if (string.IsNullOrEmpty(order.PaymentCurrency))
                {
                    order.PaymentCurrency = "USD"; // Устанавливаем валюту по умолчанию
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Admin fake payment completed for order {OrderId}. Payment ID: {PaymentId}",
                    orderId, fakePaymentId);

                return Ok(new
                {
                    message = "Fake payment successfully processed",
                    orderId = order.OrderId,
                    status = order.Status,
                    paymentId = fakePaymentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing admin fake payment for order {OrderId}", orderId);
                return StatusCode(500, new { message = "Error processing fake payment", error = ex.Message });
            }
        }

        // GET: api/Payment/admin/all-orders
        [HttpGet("admin/all-orders")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> GetAllOrders(
            [FromQuery] string status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Orders.AsQueryable();

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(o => o.Status == status);
                }

                var totalCount = await query.CountAsync();

                var orders = await query
                    .OrderByDescending(o => o.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var response = orders.Select(order => new OrderStatusResponse
                {
                    OrderId = order.OrderId,
                    Status = order.Status ?? "Pending",
                    Total = order.Total,
                    Currency = order.PaymentCurrency ?? "",
                    CreatedAt = order.CreatedAt,
                    CompletedAt = order.CompletedAt,
                    PaymentId = order.PaymentId ?? "",
                    PaymentUrl = order.PaymentUrl ?? ""
                }).ToList();

                return Ok(new
                {
                    orders = response,
                    totalCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all orders");
                return StatusCode(500, new { message = "Error retrieving orders", error = ex.Message });
            }
        }

        // POST: api/Payment/create
        [HttpPost("create")]
        public async Task<ActionResult<PaymentResponse>> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                _logger.LogInformation("Starting payment creation process for user {UserId} with currency {Currency}", userId, request.Currency);

                // Validate cart has items
                var cartItems = await _context.CartItems
                    .Include(c => c.Product)
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    _logger.LogWarning("Cart is empty for user {UserId}", userId);
                    return BadRequest("Cart is empty");
                }

                // Calculate total
                decimal total = cartItems.Sum(item => item.Product.Price * item.Quantity);
                _logger.LogInformation("Calculated total: {Total}", total);

                // Create order in database
                var order = new Order
                {
                    UserId = userId,
                    Total = total,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    PaymentCurrency = request.Currency
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created order {OrderId} in database", order.OrderId);

                // Create order items
                var orderItems = cartItems.Select(cartItem => new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = cartItem.ProductId,
                    Quantity = cartItem.Quantity,
                    Price = cartItem.Product.Price
                }).ToList();

                _context.OrderItems.AddRange(orderItems);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Added {Count} items to order {OrderId}", orderItems.Count, order.OrderId);

                // Create payment with NOWPayments API
                var apiKey = _configuration["NOWPayments:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("NOWPayments API key is missing in configuration");
                    return StatusCode(500, "Payment configuration error");
                }

                _logger.LogInformation("Using API key: {ApiKeyFirstChars}...", apiKey.Substring(0, Math.Min(5, apiKey.Length)));

                // Настройка заголовков HTTP
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Добавляем orderId в URL успешной оплаты и отмены
                var successUrl = $"{_configuration["NOWPayments:SuccessUrl"]}?orderId={order.OrderId}";
                var cancelUrl = $"{_configuration["NOWPayments:CancelUrl"]}?orderId={order.OrderId}";
                var ipnCallbackUrl = _configuration["NOWPayments:IpnCallbackUrl"];

                _logger.LogInformation("Using callback URLs: Success={SuccessUrl}, Cancel={CancelUrl}, IPN={IpnUrl}",
                    successUrl, cancelUrl, ipnCallbackUrl);

                // Форматируем число с точностью до 2 знаков и с точкой в качестве разделителя
                string priceAmountStr = total.ToString("F2", CultureInfo.InvariantCulture);
                _logger.LogInformation("PriceAmount string representation: {PriceAmount}", priceAmountStr);

                // Создаем JSON строку напрямую для обхода проблем с сериализацией
                string jsonContent = $@"{{
                    ""price_amount"": ""{priceAmountStr}"",
                    ""price_currency"": ""usd"",
                    ""pay_currency"": ""{request.Currency}"",
                    ""order_id"": ""{order.OrderId}"",
                    ""order_description"": ""Order #{order.OrderId}"",
                    ""ipn_callback_url"": ""{ipnCallbackUrl}"",
                    ""success_url"": ""{successUrl}"",
                    ""cancel_url"": ""{cancelUrl}""
                }}";

                _logger.LogInformation("Payment request payload: {Payload}", jsonContent);

                var content = new StringContent(
                    jsonContent,
                    Encoding.UTF8,
                    "application/json");

                _logger.LogInformation("Sending request to NOWPayments API");
                var response = await _httpClient.PostAsync("https://api.nowpayments.io/v1/invoice", content);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("NOWPayments API response: Status={StatusCode}, Content={Content}",
                    (int)response.StatusCode, responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var paymentResponse = JsonSerializer.Deserialize<NowPaymentsInvoiceResponse>(responseContent);

                    // Update order with payment details
                    order.PaymentId = paymentResponse.Id;
                    order.PaymentUrl = paymentResponse.InvoiceUrl;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated order with payment details: PaymentId={PaymentId}", paymentResponse.Id);

                    // Clear cart after successful order creation
                    _context.CartItems.RemoveRange(cartItems);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Cleared cart for user {UserId}", userId);

                    return Ok(new PaymentResponse
                    {
                        OrderId = order.OrderId,
                        PaymentId = paymentResponse.Id,
                        PaymentUrl = paymentResponse.InvoiceUrl,
                        Total = total,
                        Currency = request.Currency
                    });
                }

                _logger.LogError("NOWPayments API error: Status={StatusCode}, Content={ErrorContent}",
                    (int)response.StatusCode, responseContent);
                return StatusCode((int)response.StatusCode, $"Payment provider error: {responseContent}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment: {Message}", ex.Message);
                return StatusCode(500, "Error creating payment: " + ex.Message);
            }
        }

        // GET: api/Payment/orders/{orderId}/items
        [HttpGet("orders/{orderId}/items")]
        public async Task<ActionResult<List<OrderItemResponse>>> GetOrderItems(int orderId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Verify the order belongs to the current user
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

                if (order == null)
                    return NotFound("Order not found");

                var orderItems = await _context.OrderItems
                    .Include(oi => oi.Product)
                    .Where(oi => oi.OrderId == orderId)
                    .ToListAsync();

                var response = orderItems.Select(item => new OrderItemResponse
                {
                    OrderItemId = item.OrderItemId,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    ProductDescription = item.Product.Description,
                    ProductImageUrl = item.Product.ImageUrl,
                    Price = item.Price,
                    Quantity = item.Quantity
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order items for order {OrderId}", orderId);
                return StatusCode(500, "Error retrieving order items");
            }
        }

        // POST: api/Payment/test-complete/{orderId}
        [HttpPost("test-complete/{orderId}")]
        public async Task<ActionResult> TestCompleteOrder(int orderId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

                if (order == null)
                    return NotFound("Order not found");

                // Update order status to completed for testing
                order.Status = "Completed";
                order.CompletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Test completed order {OrderId} for user {UserId}", orderId, userId);

                return Ok(new { message = "Order marked as completed for testing" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error test completing order {OrderId}", orderId);
                return StatusCode(500, "Error completing order");
            }
        }

        // GET: api/Payment/check/{orderId}
        [HttpGet("check/{orderId}")]
        public async Task<ActionResult<OrderStatusResponse>> CheckOrderStatus(int orderId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

                if (order == null)
                    return NotFound("Order not found");

                return Ok(new OrderStatusResponse
                {
                    OrderId = order.OrderId,
                    Status = order.Status ?? "Pending",
                    Total = order.Total,
                    Currency = order.PaymentCurrency ?? "",
                    CreatedAt = order.CreatedAt,
                    CompletedAt = order.CompletedAt,
                    PaymentId = order.PaymentId ?? "",
                    PaymentUrl = order.PaymentUrl ?? ""
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking order status for order {OrderId}", orderId);
                return StatusCode(500, "Error checking order status");
            }
        }

        // POST: api/Payment/webhook
        [HttpPost("webhook")]
        [AllowAnonymous] // Webhook needs to be accessible without auth
        public async Task<IActionResult> WebhookHandler()
        {
            try
            {
                // Enable buffering so we can read the request body multiple times
                HttpContext.Request.EnableBuffering();

                // Read the request body
                using var reader = new StreamReader(HttpContext.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();

                // Log the full webhook payload for debugging
                _logger.LogInformation("Received webhook: {Body}", body);

                // Important: Reset the position of the request body stream
                HttpContext.Request.Body.Position = 0;

                // Try to deserialize as a NowPaymentsWebhookEvent
                try
                {
                    var webhookEvent = JsonSerializer.Deserialize<NowPaymentsWebhookEvent>(body);

                    if (webhookEvent == null)
                    {
                        _logger.LogWarning("Failed to deserialize webhook payload");
                        return BadRequest("Invalid webhook payload");
                    }

                    // Process based on event type
                    if (webhookEvent.EventType == "payment" && !string.IsNullOrEmpty(webhookEvent.OrderId) && int.TryParse(webhookEvent.OrderId, out int orderId))
                    {
                        var order = await _context.Orders.FindAsync(orderId);
                        if (order != null)
                        {
                            // Update order status based on payment status
                            switch (webhookEvent.PaymentStatus?.ToLower())
                            {
                                case "finished":
                                case "confirmed":
                                    order.Status = "Completed";
                                    order.CompletedAt = DateTime.UtcNow;
                                    break;
                                case "partially_paid":
                                    order.Status = "Partially Paid";
                                    break;
                                case "confirming":
                                    order.Status = "Confirming";
                                    break;
                                case "waiting":
                                    order.Status = "Waiting";
                                    break;
                                case "expired":
                                    order.Status = "Expired";
                                    break;
                                case "failed":
                                    order.Status = "Failed";
                                    break;
                                case "refunded":
                                    order.Status = "Refunded";
                                    break;
                                default:
                                    order.Status = webhookEvent.PaymentStatus ?? "Unknown";
                                    break;
                            }

                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Updated order {OrderId} status to {Status}", orderId, order.Status);
                        }
                        else
                        {
                            _logger.LogWarning("Order {OrderId} not found for webhook", orderId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Invalid event type or order ID: {EventType}, {OrderId}",
                            webhookEvent.EventType, webhookEvent.OrderId);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing webhook payload");

                    // Try alternate format - NOWPayments may send different formats
                    try
                    {
                        // Try to parse as dynamic JSON in case the format is different
                        using (JsonDocument document = JsonDocument.Parse(body))
                        {
                            var root = document.RootElement;

                            // Try to extract fields regardless of structure
                            if (root.TryGetProperty("order_id", out var orderIdElement) &&
                                orderIdElement.ValueKind == JsonValueKind.String &&
                                int.TryParse(orderIdElement.GetString(), out int orderId))
                            {
                                var order = await _context.Orders.FindAsync(orderId);
                                if (order != null)
                                {
                                    // Try to extract payment status
                                    string status = "Unknown";
                                    if (root.TryGetProperty("payment_status", out var statusElement) &&
                                        statusElement.ValueKind == JsonValueKind.String)
                                    {
                                        status = statusElement.GetString() ?? "Unknown";
                                    }

                                    // Update order status
                                    switch (status.ToLower())
                                    {
                                        case "finished":
                                        case "confirmed":
                                            order.Status = "Completed";
                                            order.CompletedAt = DateTime.UtcNow;
                                            break;
                                        default:
                                            order.Status = status;
                                            break;
                                    }

                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation("Updated order {OrderId} status to {Status} (alternate format)", orderId, order.Status);
                                }
                            }
                        }
                    }
                    catch (Exception alternateEx)
                    {
                        _logger.LogError(alternateEx, "Error processing webhook with alternate parsing");
                    }
                }

                // Always return OK to NOWPayments even if we couldn't process it
                // This prevents them from retrying webhooks that might fail due to our parsing
                return Ok(new { status = "success" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing webhook");
                return StatusCode(500, "Error processing webhook");
            }
        }

        // GET: api/Payment/orders
        [HttpGet("orders")]
        public async Task<ActionResult<List<OrderStatusResponse>>> GetUserOrders()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                var orders = await _context.Orders
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                var response = orders.Select(order => new OrderStatusResponse
                {
                    OrderId = order.OrderId,
                    Status = order.Status ?? "Pending",
                    Total = order.Total,
                    Currency = order.PaymentCurrency ?? "",
                    CreatedAt = order.CreatedAt,
                    CompletedAt = order.CompletedAt,
                    PaymentId = order.PaymentId ?? "",
                    PaymentUrl = order.PaymentUrl ?? ""
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user orders for user {UserId}", userId);
                return StatusCode(500, new { message = "Error loading orders", error = ex.Message });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return 0;

            return userId;
        }
    }

    // Request & Response DTOs
    public class CreatePaymentRequest
    {
        public string Currency { get; set; } // Cryptocurrency code (BTC, ETH, etc.)
    }

    public class PaymentResponse
    {
        public int OrderId { get; set; }
        public string PaymentId { get; set; }
        public string PaymentUrl { get; set; }
        public decimal Total { get; set; }
        public string Currency { get; set; }
    }

    public class OrderItemResponse
    {
        public int OrderItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductDescription { get; set; }
        public string ProductImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class OrderStatusResponse
    {
        public int OrderId { get; set; }
        public string Status { get; set; } = "Pending";
        public decimal Total { get; set; }
        public string Currency { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string PaymentId { get; set; } = "";
        public string PaymentUrl { get; set; } = "";
    }

    // NOWPayments API Models
    public class NowPaymentsInvoiceResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("invoice_url")]
        public string InvoiceUrl { get; set; }

        [JsonPropertyName("order_id")]
        public string OrderId { get; set; }

        [JsonPropertyName("payment_status")]
        public string PaymentStatus { get; set; }

        [JsonPropertyName("price_amount")]
        public string PriceAmount { get; set; }  // Изменено с decimal на string

        [JsonPropertyName("price_currency")]
        public string PriceCurrency { get; set; }

        [JsonPropertyName("pay_currency")]
        public string PayCurrency { get; set; }
    }

    public class NowPaymentsCurrenciesResponse
    {
        [JsonPropertyName("currencies")]
        public List<string> Currencies { get; set; }
    }

    public class NowPaymentsWebhookEvent
    {
        [JsonPropertyName("event_type")]
        public string EventType { get; set; }

        [JsonPropertyName("order_id")]
        public string OrderId { get; set; }

        [JsonPropertyName("payment_id")]
        public string PaymentId { get; set; }

        [JsonPropertyName("payment_status")]
        public string PaymentStatus { get; set; }

        [JsonPropertyName("pay_amount")]
        public decimal PayAmount { get; set; }

        [JsonPropertyName("pay_currency")]
        public string PayCurrency { get; set; }
    }
}