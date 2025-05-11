using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using market_api.Data;
using market_api.Models;
using System.Text.Json;

namespace market_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DeliveryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DeliveryController> _logger;

        public DeliveryController(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<DeliveryController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // GET: api/Delivery/order/{orderId}
        [HttpGet("order/{orderId}")]
        public async Task<ActionResult<Delivery>> GetDeliveryByOrder(int orderId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            // Проверяем, принадлежит ли заказ текущему пользователю
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

            if (order == null)
                return NotFound("Заказ не найден");

            // Получаем данные о доставке
            var delivery = await _context.Deliveries
                .FirstOrDefaultAsync(d => d.OrderId == orderId);

            if (delivery == null)
                return NotFound("Информация о доставке не найдена");

            return delivery;
        }

        // POST: api/Delivery
        [HttpPost]
        public async Task<ActionResult<Delivery>> CreateDelivery([FromBody] DeliveryCreateDto deliveryDto)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            // Проверяем существование заказа и его принадлежность пользователю
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == deliveryDto.OrderId && o.UserId == userId);

            if (order == null)
                return NotFound("Заказ не найден");

            // Проверяем, не создана ли уже доставка для этого заказа
            var existingDelivery = await _context.Deliveries
                .FirstOrDefaultAsync(d => d.OrderId == deliveryDto.OrderId);

            if (existingDelivery != null)
                return BadRequest("Доставка для данного заказа уже создана");

            // Создаем новую доставку
            var delivery = new Delivery
            {
                OrderId = deliveryDto.OrderId,
                DeliveryMethod = deliveryDto.DeliveryMethod,
                DeliveryType = deliveryDto.DeliveryType,
                RecipientFullName = deliveryDto.RecipientFullName,
                RecipientPhone = deliveryDto.RecipientPhone,
                CityRef = deliveryDto.CityRef,
                CityName = deliveryDto.CityName,
                WarehouseRef = deliveryDto.WarehouseRef,
                WarehouseAddress = deliveryDto.WarehouseAddress,
                DeliveryAddress = deliveryDto.DeliveryAddress,
                DeliveryCost = deliveryDto.DeliveryCost,
                DeliveryStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            // Сохраняем дополнительные данные о доставке в JSON
            if (deliveryDto.AdditionalData != null)
            {
                delivery.DeliveryData = JsonSerializer.Serialize(deliveryDto.AdditionalData);
            }

            _context.Deliveries.Add(delivery);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDeliveryByOrder), new { orderId = delivery.OrderId }, delivery);
        }

        // PUT: api/Delivery/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDelivery(int id, [FromBody] DeliveryUpdateDto deliveryDto)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var delivery = await _context.Deliveries
                .Include(d => d.Order)
                .FirstOrDefaultAsync(d => d.DeliveryId == id && d.Order.UserId == userId);

            if (delivery == null)
                return NotFound("Информация о доставке не найдена");

            // Обновляем данные доставки
            if (!string.IsNullOrEmpty(deliveryDto.RecipientFullName))
                delivery.RecipientFullName = deliveryDto.RecipientFullName;

            if (!string.IsNullOrEmpty(deliveryDto.RecipientPhone))
                delivery.RecipientPhone = deliveryDto.RecipientPhone;

            if (!string.IsNullOrEmpty(deliveryDto.CityRef))
                delivery.CityRef = deliveryDto.CityRef;

            if (!string.IsNullOrEmpty(deliveryDto.CityName))
                delivery.CityName = deliveryDto.CityName;

            if (!string.IsNullOrEmpty(deliveryDto.WarehouseRef))
                delivery.WarehouseRef = deliveryDto.WarehouseRef;

            if (!string.IsNullOrEmpty(deliveryDto.WarehouseAddress))
                delivery.WarehouseAddress = deliveryDto.WarehouseAddress;

            if (!string.IsNullOrEmpty(deliveryDto.DeliveryAddress))
                delivery.DeliveryAddress = deliveryDto.DeliveryAddress;

            delivery.UpdatedAt = DateTime.UtcNow;

            // Обновляем дополнительные данные, если они предоставлены
            if (deliveryDto.AdditionalData != null)
            {
                delivery.DeliveryData = JsonSerializer.Serialize(deliveryDto.AdditionalData);
            }

            _context.Entry(delivery).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DeliveryExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // PATCH: api/Delivery/{id}/status
        [HttpPatch("{id}/status")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateDeliveryStatus(int id, [FromBody] DeliveryStatusUpdateDto statusDto)
        {
            var delivery = await _context.Deliveries.FindAsync(id);
            if (delivery == null)
                return NotFound("Информация о доставке не найдена");

            delivery.DeliveryStatus = statusDto.DeliveryStatus;

            if (!string.IsNullOrEmpty(statusDto.TrackingNumber))
                delivery.TrackingNumber = statusDto.TrackingNumber;

            if (statusDto.EstimatedDeliveryDate.HasValue)
                delivery.EstimatedDeliveryDate = statusDto.EstimatedDeliveryDate;

            delivery.UpdatedAt = DateTime.UtcNow;

            _context.Entry(delivery).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DeliveryExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // GET: api/Delivery
        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<IEnumerable<DeliveryListItemDto>>> GetAllDeliveries([FromQuery] string status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            // Базовый запрос
            IQueryable<Delivery> query = _context.Deliveries
                .Include(d => d.Order)
                .OrderByDescending(d => d.CreatedAt);

            // Применяем фильтр по статусу, если указан
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(d => d.DeliveryStatus == status);
            }

            // Считаем общее количество записей для пагинации
            var totalCount = await query.CountAsync();

            // Применяем пагинацию
            var deliveries = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new DeliveryListItemDto
                {
                    DeliveryId = d.DeliveryId,
                    OrderId = d.OrderId,
                    DeliveryMethod = d.DeliveryMethod,
                    DeliveryType = d.DeliveryType,
                    RecipientFullName = d.RecipientFullName,
                    CityName = d.CityName,
                    DeliveryStatus = d.DeliveryStatus,
                    TrackingNumber = d.TrackingNumber,
                    DeliveryCost = d.DeliveryCost,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                })
                .ToListAsync();

            // Устанавливаем заголовки пагинации
            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());
            Response.Headers.Add("X-Total-Pages", Math.Ceiling((double)totalCount / pageSize).ToString());

            return deliveries;
        }

        // GET: api/Delivery/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<Delivery>> GetDeliveryById(int id)
        {
            var delivery = await _context.Deliveries
                .Include(d => d.Order)
                .FirstOrDefaultAsync(d => d.DeliveryId == id);

            if (delivery == null)
                return NotFound("Информация о доставке не найдена");

            return delivery;
        }

        // POST: api/Delivery/{id}/tracking
        [HttpPost("{id}/tracking")]
        public async Task<IActionResult> AddTrackingNumber(int id, [FromBody] TrackingNumberDto trackingDto)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var delivery = await _context.Deliveries
                .Include(d => d.Order)
                .FirstOrDefaultAsync(d => d.DeliveryId == id && d.Order.UserId == userId);

            if (delivery == null)
                return NotFound("Информация о доставке не найдена");

            // Проверяем, что трекинг номер указан
            if (string.IsNullOrEmpty(trackingDto.TrackingNumber))
                return BadRequest("Необходимо указать номер ТТН");

            // Обновляем данные
            delivery.TrackingNumber = trackingDto.TrackingNumber;
            delivery.DeliveryStatus = "InTransit"; // Меняем статус на "В пути"
            delivery.UpdatedAt = DateTime.UtcNow;

            _context.Entry(delivery).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Номер ТТН успешно добавлен", trackingNumber = delivery.TrackingNumber });
        }

        // GET: api/Delivery/tracking/{trackingNumber}
        [HttpGet("tracking/{trackingNumber}")]
        public async Task<ActionResult<object>> TrackDelivery(string trackingNumber)
        {
            // В реальном приложении здесь будет запрос к API Новой почты для получения статуса
            // Для примера возвращаем заглушку

            // Проверяем формат трекинг-номера (для Новой почты обычно 14 цифр)
            if (string.IsNullOrEmpty(trackingNumber) || !System.Text.RegularExpressions.Regex.IsMatch(trackingNumber, @"^\d{14}$"))
                return BadRequest("Неверный формат номера ТТН");

            // Пример данных трекинга (в реальности получаемых от API Новой почты)
            var trackingData = new
            {
                Status = "InTransit",
                StatusDescription = "В пути",
                ReceivedAt = DateTime.UtcNow.AddDays(-2),
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(1),
                CurrentCity = "Киев",
                CurrentWarehouse = "Отделение №1",
                StatusHistory = new[]
                {
                    new
                    {
                        Date = DateTime.UtcNow.AddHours(-12),
                        Status = "InTransit",
                        Description = "Отправление в пути",
                        City = "Киев"
                    },
                    new
                    {
                        Date = DateTime.UtcNow.AddDays(-1),
                        Status = "Received",
                        Description = "Отправление принято",
                        City = "Днепр"
                    }
                }
            };

            return Ok(trackingData);
        }

        // GET: api/Delivery/statistics
        [HttpGet("statistics")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<object>> GetStatistics()
        {
            // Получаем статистику по доставкам
            var statistics = new
            {
                TotalDeliveries = await _context.Deliveries.CountAsync(),
                PendingDeliveries = await _context.Deliveries.CountAsync(d => d.DeliveryStatus == "Pending"),
                InTransitDeliveries = await _context.Deliveries.CountAsync(d => d.DeliveryStatus == "InTransit"),
                DeliveredDeliveries = await _context.Deliveries.CountAsync(d => d.DeliveryStatus == "Delivered"),
                FailedDeliveries = await _context.Deliveries.CountAsync(d => d.DeliveryStatus == "Failed"),

                DeliveryMethods = await _context.Deliveries
                    .GroupBy(d => d.DeliveryMethod)
                    .Select(g => new { Method = g.Key, Count = g.Count() })
                    .ToListAsync(),

                DeliveryTypes = await _context.Deliveries
                    .GroupBy(d => d.DeliveryType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync(),

                AverageDeliveryCost = await _context.Deliveries.AverageAsync(d => d.DeliveryCost)
            };

            return Ok(statistics);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return 0;

            return userId;
        }

        private bool DeliveryExists(int id)
        {
            return _context.Deliveries.Any(e => e.DeliveryId == id);
        }
    }

    // DTO для создания доставки
    public class DeliveryCreateDto
    {
        public int OrderId { get; set; }
        public string DeliveryMethod { get; set; } // например, "NovaPoshta"
        public string DeliveryType { get; set; } // например, "Warehouse" или "Courier"
        public string RecipientFullName { get; set; }
        public string RecipientPhone { get; set; }
        public string CityRef { get; set; }
        public string CityName { get; set; }
        public string WarehouseRef { get; set; }
        public string WarehouseAddress { get; set; }
        public string DeliveryAddress { get; set; }
        public decimal DeliveryCost { get; set; }
        public object AdditionalData { get; set; } // Дополнительные данные в формате JSON
    }

    // DTO для обновления доставки
    public class DeliveryUpdateDto
    {
        public string RecipientFullName { get; set; }
        public string RecipientPhone { get; set; }
        public string CityRef { get; set; }
        public string CityName { get; set; }
        public string WarehouseRef { get; set; }
        public string WarehouseAddress { get; set; }
        public string DeliveryAddress { get; set; }
        public object AdditionalData { get; set; }
    }

    // DTO для обновления статуса доставки
    public class DeliveryStatusUpdateDto
    {
        public string DeliveryStatus { get; set; }
        public string TrackingNumber { get; set; }
        public DateTime? EstimatedDeliveryDate { get; set; }
    }

    // DTO для списка доставок
    public class DeliveryListItemDto
    {
        public int DeliveryId { get; set; }
        public int OrderId { get; set; }
        public string DeliveryMethod { get; set; }
        public string DeliveryType { get; set; }
        public string RecipientFullName { get; set; }
        public string CityName { get; set; }
        public string DeliveryStatus { get; set; }
        public string TrackingNumber { get; set; }
        public decimal DeliveryCost { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // DTO для добавления номера ТТН
    public class TrackingNumberDto
    {
        public string TrackingNumber { get; set; }
    }
}