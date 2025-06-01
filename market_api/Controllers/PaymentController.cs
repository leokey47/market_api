using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
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
        private readonly MongoDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            MongoDbContext context,
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
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("NOWPayments API key is missing, returning fallback currencies");
                    // Возвращаем fallback валюты если API ключ отсутствует
                    var fallbackCurrencies = new List<string>
                    {
                        "BTC", "ETH", "LTC", "USDT", "USDC", "XRP", "DOGE", "ADA", "DOT", "MATIC",
                        "BNB", "SOL", "AVAX", "LINK", "UNI", "TRX", "XLM", "VET", "FIL", "THETA"
                    };
                    return Ok(fallbackCurrencies);
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

                var response = await _httpClient.GetAsync("https://api.nowpayments.io/v1/currencies");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var currenciesResponse = JsonSerializer.Deserialize<NowPaymentsCurrenciesResponse>(content);

                    if (currenciesResponse?.Currencies != null && currenciesResponse.Currencies.Any())
                    {
                        return Ok(currenciesResponse.Currencies);
                    }
                }

                _logger.LogWarning("Failed to fetch currencies from NOWPayments API, returning fallback");
                // Fallback валюты если API не работает
                var defaultCurrencies = new List<string>
                {
                    "BTC", "ETH", "TON", "USDT", "USDC", "XRP", "DOGE", "ADA", "DOT", "MATIC",
                    
                };
                return Ok(defaultCurrencies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting available currencies");

                // Возвращаем fallback валюты при любой ошибке
                var fallbackCurrencies = new List<string>
                {
                    "BTC", "ETH", "TON", "USDT", "USDC", "XRP", "DOGE", "ADA", "DOT", "MATIC",
                    
                };
                return Ok(fallbackCurrencies);
            }
        }

        // POST: api/Payment/create
        [HttpPost("create")]
        public async Task<ActionResult<PaymentResponse>> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                _logger.LogInformation("Starting payment creation process for user {UserId} with currency {Currency}", userId, request.Currency);

                // Validate cart has items
                var cartItems = await _context.CartItems
                    .Find(c => c.UserId == userId)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    _logger.LogWarning("Cart is empty for user {UserId}", userId);
                    return BadRequest("Cart is empty");
                }

                // Calculate total by getting product prices
                decimal total = 0;
                var cartProductDetails = new List<(CartItem cartItem, Product product)>();

                foreach (var cartItem in cartItems)
                {
                    var product = await _context.Products.Find(p => p.Id == cartItem.ProductId).FirstOrDefaultAsync();
                    if (product != null)
                    {
                        total += product.Price * cartItem.Quantity;
                        cartProductDetails.Add((cartItem, product));
                    }
                }

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

                await _context.Orders.InsertOneAsync(order);
                _logger.LogInformation("Created order {OrderId} in database", order.Id);

                // Create order items
                var orderItems = cartProductDetails.Select(detail => new OrderItem
                {
                    OrderId = order.Id!,
                    ProductId = detail.cartItem.ProductId,
                    Quantity = detail.cartItem.Quantity,
                    Price = detail.product.Price
                }).ToList();

                await _context.OrderItems.InsertManyAsync(orderItems);
                _logger.LogInformation("Added {Count} items to order {OrderId}", orderItems.Count, order.Id);

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
                var successUrl = $"{_configuration["NOWPayments:SuccessUrl"]}?orderId={order.Id}";
                var cancelUrl = $"{_configuration["NOWPayments:CancelUrl"]}?orderId={order.Id}";
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
                    ""order_id"": ""{order.Id}"",
                    ""order_description"": ""Order #{order.Id}"",
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
                    await _context.Orders.ReplaceOneAsync(o => o.Id == order.Id, order);
                    _logger.LogInformation("Updated order with payment details: PaymentId={PaymentId}", paymentResponse.Id);

                    // Clear cart after successful order creation
                    await _context.CartItems.DeleteManyAsync(c => c.UserId == userId);
                    _logger.LogInformation("Cleared cart for user {UserId}", userId);

                    return Ok(new PaymentResponse
                    {
                        OrderId = order.Id!,
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

        // GET: api/Payment/check/{orderId}
        [HttpGet("check/{orderId}")]
        public async Task<ActionResult<OrderStatusResponse>> CheckOrderStatus(string orderId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var order = await _context.Orders
                    .Find(o => o.Id == orderId && o.UserId == userId)
                    .FirstOrDefaultAsync();

                if (order == null)
                    return NotFound("Order not found");

                return Ok(new OrderStatusResponse
                {
                    OrderId = order.Id!,
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

        // GET: api/Payment/orders
        [HttpGet("orders")]
        public async Task<ActionResult<List<OrderStatusResponse>>> GetUserOrders()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var orders = await _context.Orders
                    .Find(o => o.UserId == userId)
                    .SortByDescending(o => o.CreatedAt)
                    .ToListAsync();

                var response = orders.Select(order => new OrderStatusResponse
                {
                    OrderId = order.Id!,
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

        // GET: api/Payment/orders/{orderId}/items
        [HttpGet("orders/{orderId}/items")]
        public async Task<ActionResult<List<OrderItemResponse>>> GetOrderItems(string orderId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                // Verify the order belongs to the current user
                var order = await _context.Orders
                    .Find(o => o.Id == orderId && o.UserId == userId)
                    .FirstOrDefaultAsync();

                if (order == null)
                    return NotFound("Order not found");

                var orderItems = await _context.OrderItems
                    .Find(oi => oi.OrderId == orderId)
                    .ToListAsync();

                var response = new List<OrderItemResponse>();

                foreach (var item in orderItems)
                {
                    var product = await _context.Products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
                    if (product != null)
                    {
                        response.Add(new OrderItemResponse
                        {
                            OrderItemId = item.Id!,
                            ProductId = item.ProductId,
                            ProductName = product.Name,
                            ProductDescription = product.Description,
                            ProductImageUrl = product.ImageUrl,
                            Price = item.Price,
                            Quantity = item.Quantity
                        });
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order items for order {OrderId}", orderId);
                return StatusCode(500, "Error retrieving order items");
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
                    if (webhookEvent.EventType == "payment" && !string.IsNullOrEmpty(webhookEvent.OrderId))
                    {
                        var order = await _context.Orders.Find(o => o.Id == webhookEvent.OrderId).FirstOrDefaultAsync();
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

                            await _context.Orders.ReplaceOneAsync(o => o.Id == order.Id, order);
                            _logger.LogInformation("Updated order {OrderId} status to {Status}", webhookEvent.OrderId, order.Status);
                        }
                        else
                        {
                            _logger.LogWarning("Order {OrderId} not found for webhook", webhookEvent.OrderId);
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

        private string GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            return userIdClaim?.Value ?? string.Empty;
        }
    }

    // Request & Response DTOs
    public class CreatePaymentRequest
    {
        public string Currency { get; set; } = string.Empty; // Cryptocurrency code (BTC, ETH, etc.)
    }

    public class PaymentResponse
    {
        public string OrderId { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public string PaymentUrl { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    public class OrderItemResponse
    {
        public string OrderItemId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductDescription { get; set; } = string.Empty;
        public string ProductImageUrl { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class OrderStatusResponse
    {
        public string OrderId { get; set; } = string.Empty;
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
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("invoice_url")]
        public string InvoiceUrl { get; set; } = string.Empty;

        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("payment_status")]
        public string PaymentStatus { get; set; } = string.Empty;

        [JsonPropertyName("price_amount")]
        public string PriceAmount { get; set; } = string.Empty;

        [JsonPropertyName("price_currency")]
        public string PriceCurrency { get; set; } = string.Empty;

        [JsonPropertyName("pay_currency")]
        public string PayCurrency { get; set; } = string.Empty;
    }

    public class NowPaymentsCurrenciesResponse
    {
        [JsonPropertyName("currencies")]
        public List<string> Currencies { get; set; } = new List<string>();
    }

    public class NowPaymentsWebhookEvent
    {
        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("payment_id")]
        public string PaymentId { get; set; } = string.Empty;

        [JsonPropertyName("payment_status")]
        public string PaymentStatus { get; set; } = string.Empty;

        [JsonPropertyName("pay_amount")]
        public decimal PayAmount { get; set; }

        [JsonPropertyName("pay_currency")]
        public string PayCurrency { get; set; } = string.Empty;
    }
}