using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using MongoDB.Bson;
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
        private readonly MongoDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DeliveryController> _logger;

        public DeliveryController(
            MongoDbContext context,
            IConfiguration configuration,
            ILogger<DeliveryController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // GET: api/Delivery/order/{orderId}
        [HttpGet("order/{orderId}")]
        public async Task<ActionResult<Delivery>> GetDeliveryByOrder(string orderId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Проверяем, принадлежит ли заказ текущему пользователю
            var order = await _context.Orders
                .Find(o => o.Id == orderId && o.UserId == userId)
                .FirstOrDefaultAsync();

            if (order == null)
                return NotFound("Заказ не найден");

            // Получаем данные о доставке
            var delivery = await _context.Deliveries
                .Find(d => d.OrderId == orderId)
                .FirstOrDefaultAsync();

            if (delivery == null)
                return NotFound("Информация о доставке не найдена");

            return delivery;
        }

        // POST: api/Delivery
        [HttpPost]
        public async Task<ActionResult<Delivery>> CreateDelivery([FromBody] DeliveryCreateDto deliveryDto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Проверяем существование заказа и его принадлежность пользователю
            var order = await _context.Orders
                .Find(o => o.Id == deliveryDto.OrderId && o.UserId == userId)
                .FirstOrDefaultAsync();

            if (order == null)
                return NotFound("Заказ не найден");

            // Проверяем, не создана ли уже доставка для этого заказа
            var existingDelivery = await _context.Deliveries
                .Find(d => d.OrderId == deliveryDto.OrderId)
                .FirstOrDefaultAsync();

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

            await _context.Deliveries.InsertOneAsync(delivery);

            return CreatedAtAction(nameof(GetDeliveryByOrder), new { orderId = delivery.OrderId }, delivery);
        }

        // PUT: api/Delivery/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDelivery(string id, [FromBody] DeliveryUpdateDto deliveryDto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var delivery = await _context.Deliveries
                .Find(d => d.Id == id)
                .FirstOrDefaultAsync();

            if (delivery == null)
                return NotFound("Информация о доставке не найдена");

            // Проверяем, что заказ принадлежит пользователю
            var order = await _context.Orders
                .Find(o => o.Id == delivery.OrderId && o.UserId == userId)
                .FirstOrDefaultAsync();

            if (order == null)
                return NotFound("Заказ не найден или не принадлежит пользователю");

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

            var result = await _context.Deliveries.ReplaceOneAsync(d => d.Id == id, delivery);

            if (result.ModifiedCount == 0)
                return NotFound();

            return NoContent();
        }

        // PATCH: api/Delivery/{id}/status
        [HttpPatch("{id}/status")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateDeliveryStatus(string id, [FromBody] DeliveryStatusUpdateDto statusDto)
        {
            var delivery = await _context.Deliveries.Find(d => d.Id == id).FirstOrDefaultAsync();
            if (delivery == null)
                return NotFound("Информация о доставке не найдена");

            delivery.DeliveryStatus = statusDto.DeliveryStatus;

            if (!string.IsNullOrEmpty(statusDto.TrackingNumber))
                delivery.TrackingNumber = statusDto.TrackingNumber;

            if (statusDto.EstimatedDeliveryDate.HasValue)
                delivery.EstimatedDeliveryDate = statusDto.EstimatedDeliveryDate;

            delivery.UpdatedAt = DateTime.UtcNow;

            var result = await _context.Deliveries.ReplaceOneAsync(d => d.Id == id, delivery);

            if (result.ModifiedCount == 0)
                return NotFound();

            return NoContent();
        }

        // GET: api/Delivery
        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<IEnumerable<DeliveryListItemDto>>> GetAllDeliveries([FromQuery] string status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            // Базовый фильтр
            var filterBuilder = Builders<Delivery>.Filter.Empty;

            // Применяем фильтр по статусу, если указан
            if (!string.IsNullOrEmpty(status))
            {
                filterBuilder = Builders<Delivery>.Filter.Eq(d => d.DeliveryStatus, status);
            }

            // Считаем общее количество записей для пагинации
            var totalCount = await _context.Deliveries.CountDocumentsAsync(filterBuilder);

            // Применяем пагинацию и сортировку
            var deliveries = await _context.Deliveries
                .Find(filterBuilder)
                .SortByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var response = deliveries.Select(d => new DeliveryListItemDto
            {
                DeliveryId = d.Id!,
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
            }).ToList();

            // Устанавливаем заголовки пагинации
            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());
            Response.Headers.Add("X-Total-Pages", Math.Ceiling((double)totalCount / pageSize).ToString());

            return response;
        }

        // GET: api/Delivery/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<Delivery>> GetDeliveryById(string id)
        {
            var delivery = await _context.Deliveries
                .Find(d => d.Id == id)
                .FirstOrDefaultAsync();

            if (delivery == null)
                return NotFound("Информация о доставке не найдена");

            return delivery;
        }

        // POST: api/Delivery/{id}/tracking
        [HttpPost("{id}/tracking")]
        public async Task<IActionResult> AddTrackingNumber(string id, [FromBody] TrackingNumberDto trackingDto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var delivery = await _context.Deliveries
                .Find(d => d.Id == id)
                .FirstOrDefaultAsync();

            if (delivery == null)
                return NotFound("Информация о доставке не найдена");

            // Проверяем, что заказ принадлежит пользователю
            var order = await _context.Orders
                .Find(o => o.Id == delivery.OrderId && o.UserId == userId)
                .FirstOrDefaultAsync();

            if (order == null)
                return NotFound("Заказ не найден или не принадлежит пользователю");

            // Проверяем, что трекинг номер указан
            if (string.IsNullOrEmpty(trackingDto.TrackingNumber))
                return BadRequest("Необходимо указать номер ТТН");

            // Обновляем данные
            delivery.TrackingNumber = trackingDto.TrackingNumber;
            delivery.DeliveryStatus = "InTransit"; // Меняем статус на "В пути"
            delivery.UpdatedAt = DateTime.UtcNow;

            await _context.Deliveries.ReplaceOneAsync(d => d.Id == id, delivery);

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
            var totalDeliveries = await _context.Deliveries.CountDocumentsAsync(Builders<Delivery>.Filter.Empty);
            var pendingDeliveries = await _context.Deliveries.CountDocumentsAsync(Builders<Delivery>.Filter.Eq(d => d.DeliveryStatus, "Pending"));
            var inTransitDeliveries = await _context.Deliveries.CountDocumentsAsync(Builders<Delivery>.Filter.Eq(d => d.DeliveryStatus, "InTransit"));
            var deliveredDeliveries = await _context.Deliveries.CountDocumentsAsync(Builders<Delivery>.Filter.Eq(d => d.DeliveryStatus, "Delivered"));
            var failedDeliveries = await _context.Deliveries.CountDocumentsAsync(Builders<Delivery>.Filter.Eq(d => d.DeliveryStatus, "Failed"));

            // Группировка по методам доставки
            var deliveryMethodsPipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$deliveryMethod" },
                    { "count", new BsonDocument("$sum", 1) }
                })
            };
            var deliveryMethodsAggregate = await _context.Deliveries
                .Aggregate<BsonDocument>(deliveryMethodsPipeline)
                .ToListAsync();

            // Группировка по типам доставки
            var deliveryTypesPipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$deliveryType" },
                    { "count", new BsonDocument("$sum", 1) }
                })
            };
            var deliveryTypesAggregate = await _context.Deliveries
                .Aggregate<BsonDocument>(deliveryTypesPipeline)
                .ToListAsync();

            // Средняя стоимость доставки
            var averageCostPipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", BsonNull.Value },
                    { "averageCost", new BsonDocument("$avg", "$deliveryCost") }
                })
            };
            var averageCostAggregate = await _context.Deliveries
                .Aggregate<BsonDocument>(averageCostPipeline)
                .FirstOrDefaultAsync();

            var statistics = new
            {
                TotalDeliveries = totalDeliveries,
                PendingDeliveries = pendingDeliveries,
                InTransitDeliveries = inTransitDeliveries,
                DeliveredDeliveries = deliveredDeliveries,
                FailedDeliveries = failedDeliveries,
                DeliveryMethods = deliveryMethodsAggregate.Select(d => new {
                    Method = d.GetValue("_id", "").AsString,
                    Count = d.GetValue("count", 0).AsInt32
                }).ToList(),
                DeliveryTypes = deliveryTypesAggregate.Select(d => new {
                    Type = d.GetValue("_id", "").AsString,
                    Count = d.GetValue("count", 0).AsInt32
                }).ToList(),
                AverageDeliveryCost = averageCostAggregate?.GetValue("averageCost", 0.0).AsDouble ?? 0.0
            };

            return Ok(statistics);
        }

        private string GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            return userIdClaim?.Value ?? string.Empty;
        }

        private async Task<bool> DeliveryExistsAsync(string id)
        {
            var delivery = await _context.Deliveries.Find(d => d.Id == id).FirstOrDefaultAsync();
            return delivery != null;
        }
    }

    // DTO для создания доставки
    public class DeliveryCreateDto
    {
        public string OrderId { get; set; } = string.Empty;
        public string DeliveryMethod { get; set; } = string.Empty; // например, "NovaPoshta"
        public string DeliveryType { get; set; } = string.Empty; // например, "Warehouse" или "Courier"
        public string RecipientFullName { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public string CityRef { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public string WarehouseRef { get; set; } = string.Empty;
        public string WarehouseAddress { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public decimal DeliveryCost { get; set; }
        public object? AdditionalData { get; set; } // Дополнительные данные в формате JSON
    }

    // DTO для обновления доставки
    public class DeliveryUpdateDto
    {
        public string RecipientFullName { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public string CityRef { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public string WarehouseRef { get; set; } = string.Empty;
        public string WarehouseAddress { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public object? AdditionalData { get; set; }
    }

    // DTO для обновления статуса доставки
    public class DeliveryStatusUpdateDto
    {
        public string DeliveryStatus { get; set; } = string.Empty;
        public string TrackingNumber { get; set; } = string.Empty;
        public DateTime? EstimatedDeliveryDate { get; set; }
    }

    // DTO для списка доставок
    public class DeliveryListItemDto
    {
        public string DeliveryId { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string DeliveryMethod { get; set; } = string.Empty;
        public string DeliveryType { get; set; } = string.Empty;
        public string RecipientFullName { get; set; } = string.Empty;
        public string? CityName { get; set; }
        public string DeliveryStatus { get; set; } = string.Empty;
        public string? TrackingNumber { get; set; }
        public decimal DeliveryCost { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // DTO для добавления номера ТТН
    public class TrackingNumberDto
    {
        public string TrackingNumber { get; set; } = string.Empty;
    }
}