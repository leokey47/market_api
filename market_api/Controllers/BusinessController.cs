using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using System.Security.Claims;
using market_api.Data;
using market_api.Models;

namespace market_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BusinessController : ControllerBase
    {
        private readonly MongoDbContext _context;

        public BusinessController(MongoDbContext context)
        {
            _context = context;
        }

        // POST: api/Business/create/{userId}
        [HttpPost("create/{userId}")]
        public async Task<IActionResult> CreateBusinessAccount(string userId, [FromBody] CreateBusinessAccountRequest request)
        {
            // Проверяем, что пользователь может изменять этот аккаунт
            var currentUserId = GetCurrentUserId();
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserId != userId && userRole != "admin")
            {
                return Forbid();
            }

            var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Проверяем, не является ли пользователь уже бизнес-аккаунтом
            if (user.IsBusiness)
            {
                return BadRequest(new { message = "User is already a business account" });
            }

            // Обновляем пользователя
            user.IsBusiness = true;
            user.CompanyName = request.CompanyName;
            user.CompanyAvatar = request.CompanyAvatar;
            user.CompanyDescription = request.CompanyDescription;

            try
            {
                await _context.Users.ReplaceOneAsync(u => u.Id == userId, user);
                return Ok(new { message = "Business account created successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error creating business account" });
            }
        }

        // DELETE: api/Business/remove/{userId} - ОБНОВЛЕННЫЙ ЭНДПОИНТ
        [HttpDelete("remove/{userId}")]
        public async Task<IActionResult> RemoveBusinessAccount(string userId)
        {
            // Проверяем, что пользователь может изменять этот аккаунт
            var currentUserId = GetCurrentUserId();
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserId != userId && userRole != "admin")
            {
                return Forbid();
            }

            var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Проверяем, является ли пользователь бизнес-аккаунтом
            if (!user.IsBusiness)
            {
                return BadRequest(new { message = "User is not a business account" });
            }

            try
            {
                // Получаем все товары этого бизнес-аккаунта
                var businessProducts = await _context.Products
                    .Find(p => p.BusinessOwnerId == userId)
                    .ToListAsync();

                // Удаляем все связанные данные для каждого товара
                foreach (var product in businessProducts)
                {
                    var productId = product.Id!;

                    // Находим все заказы, содержащие этот товар
                    var orderItems = await _context.OrderItems
                        .Find(oi => oi.ProductId == productId)
                        .ToListAsync();

                    if (orderItems.Any())
                    {
                        // Получаем все затронутые заказы
                        var orderIds = orderItems.Select(oi => oi.OrderId).Distinct().ToList();

                        // Удаляем элементы заказов для этого товара
                        await _context.OrderItems.DeleteManyAsync(oi => oi.ProductId == productId);

                        // Проверяем каждый заказ на наличие оставшихся элементов
                        foreach (var orderId in orderIds)
                        {
                            var remainingItems = await _context.OrderItems
                                .Find(oi => oi.OrderId == orderId)
                                .FirstOrDefaultAsync();

                            // Если в заказе не осталось товаров, удаляем заказ
                            if (remainingItems == null)
                            {
                                // Удаляем связанные доставки
                                await _context.Deliveries.DeleteManyAsync(d => d.OrderId == orderId);

                                // Удаляем сам заказ
                                await _context.Orders.DeleteOneAsync(o => o.Id == orderId);
                            }
                        }
                    }

                    // Удаляем товары из корзин пользователей
                    await _context.CartItems.DeleteManyAsync(ci => ci.ProductId == productId);

                    // Удаляем товары из списков желаемого
                    await _context.WishlistItems.DeleteManyAsync(wi => wi.ProductId == productId);

                    // Удаляем отзывы на этот товар
                    await _context.Reviews.DeleteManyAsync(r => r.ProductId == productId);

                    // Удаляем фотографии товара
                    await _context.ProductPhotos.DeleteManyAsync(pp => pp.ProductId == productId);

                    // Удаляем спецификации товара
                    await _context.ProductSpecifications.DeleteManyAsync(ps => ps.ProductId == productId);

                    // Удаляем сам товар
                    await _context.Products.DeleteOneAsync(p => p.Id == productId);
                }

                // Удаляем бизнес-статус пользователя
                user.IsBusiness = false;
                user.CompanyName = null;
                user.CompanyAvatar = null;
                user.CompanyDescription = null;

                await _context.Users.ReplaceOneAsync(u => u.Id == userId, user);

                return Ok(new
                {
                    message = "Business status removed successfully",
                    deletedProducts = businessProducts.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error removing business status",
                    error = ex.Message
                });
            }
        }

        // PUT: api/Business/update/{userId}
        [HttpPut("update/{userId}")]
        public async Task<IActionResult> UpdateBusinessAccount(string userId, [FromBody] UpdateBusinessAccountRequest request)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserId != userId && userRole != "admin")
            {
                return Forbid();
            }

            var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            if (!user.IsBusiness)
            {
                return BadRequest(new { message = "User is not a business account" });
            }

            user.CompanyName = request.CompanyName;
            user.CompanyAvatar = request.CompanyAvatar;
            user.CompanyDescription = request.CompanyDescription;

            try
            {
                await _context.Users.ReplaceOneAsync(u => u.Id == userId, user);
                return Ok(new { message = "Business account updated successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error updating business account" });
            }
        }

        // GET: api/Business/products/{userId}
        [HttpGet("products/{userId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetBusinessProducts(string userId)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Проверяем права доступа
            if (currentUserId != userId && userRole != "admin")
            {
                return Forbid();
            }

            var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            if (!user.IsBusiness)
            {
                return BadRequest(new { message = "User is not a business account" });
            }

            var products = await _context.Products
                .Find(p => p.BusinessOwnerId == userId)
                .ToListAsync();

            var response = new List<object>();

            foreach (var product in products)
            {
                var photos = await _context.ProductPhotos
                    .Find(pp => pp.ProductId == product.Id)
                    .SortBy(pp => pp.DisplayOrder)
                    .ToListAsync();

                var specifications = await _context.ProductSpecifications
                    .Find(ps => ps.ProductId == product.Id)
                    .ToListAsync();

                response.Add(new
                {
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Price,
                    product.ImageUrl,
                    product.Category,
                    Photos = photos.Select(ph => new {
                        ph.Id,
                        ph.ImageUrl,
                        ph.DisplayOrder
                    }),
                    Specifications = specifications.Select(s => new {
                        s.Id,
                        s.Name,
                        s.Value
                    })
                });
            }

            return Ok(response);
        }

        // GET: api/Business/info/{userId}
        [HttpGet("info/{userId}")]
        public async Task<IActionResult> GetBusinessInfo(string userId)
        {
            var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            if (!user.IsBusiness)
            {
                return BadRequest(new { message = "User is not a business account" });
            }

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.CompanyName,
                user.CompanyAvatar,
                user.CompanyDescription,
                user.IsBusiness
            });
        }

        private string GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            return userIdClaim?.Value ?? string.Empty;
        }
    }

    public class CreateBusinessAccountRequest
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAvatar { get; set; } = string.Empty;
        public string CompanyDescription { get; set; } = string.Empty;
    }

    public class UpdateBusinessAccountRequest
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAvatar { get; set; } = string.Empty;
        public string CompanyDescription { get; set; } = string.Empty;
    }
}