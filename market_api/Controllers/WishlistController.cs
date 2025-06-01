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
    public class WishlistController : ControllerBase
    {
        private readonly MongoDbContext _context;

        public WishlistController(MongoDbContext context)
        {
            _context = context;
        }

        // GET: api/Wishlist
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WishlistItemResponse>>> GetWishlistItems()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var wishlistItems = await _context.WishlistItems
                .Find(w => w.UserId == userId)
                .ToListAsync();

            var response = new List<WishlistItemResponse>();

            foreach (var item in wishlistItems)
            {
                var product = await _context.Products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
                if (product != null)
                {
                    response.Add(new WishlistItemResponse
                    {
                        WishlistItemId = item.Id ?? string.Empty,
                        ProductId = item.ProductId,
                        ProductName = product.Name,
                        ProductDescription = product.Description,
                        Price = product.Price,
                        ProductImageUrl = product.ImageUrl,
                        AddedAt = item.AddedAt
                    });
                }
            }

            return response;
        }

        // POST: api/Wishlist
        [HttpPost]
        public async Task<ActionResult<WishlistItem>> AddToWishlist(AddToWishlistRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var product = await _context.Products.Find(p => p.Id == request.ProductId).FirstOrDefaultAsync();
            if (product == null)
                return NotFound("Продукт не найден");

            // Check if product is already in wishlist
            var existingItem = await _context.WishlistItems
                .Find(w => w.UserId == userId && w.ProductId == request.ProductId)
                .FirstOrDefaultAsync();

            if (existingItem != null)
                return Ok(new { message = "Товар уже в списке желаемого" });

            var wishlistItem = new WishlistItem
            {
                UserId = userId,
                ProductId = request.ProductId,
                AddedAt = DateTime.UtcNow
            };

            await _context.WishlistItems.InsertOneAsync(wishlistItem);

            return Ok(new { message = "Товар добавлен в список желаемого" });
        }

        // DELETE: api/Wishlist/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveFromWishlist(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var wishlistItem = await _context.WishlistItems
                .Find(w => w.Id == id && w.UserId == userId)
                .FirstOrDefaultAsync();

            if (wishlistItem == null)
                return NotFound("Товар не найден в списке желаемого");

            await _context.WishlistItems.DeleteOneAsync(w => w.Id == id);

            return Ok(new { message = "Товар удален из списка желаемого" });
        }

        // POST: api/Wishlist/MoveToCart/5
        [HttpPost("MoveToCart/{id}")]
        public async Task<IActionResult> MoveToCart(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var wishlistItem = await _context.WishlistItems
                .Find(w => w.Id == id && w.UserId == userId)
                .FirstOrDefaultAsync();

            if (wishlistItem == null)
                return NotFound("Товар не найден в списке желаемого");

            // Check if product is already in cart
            var existingCartItem = await _context.CartItems
                .Find(c => c.UserId == userId && c.ProductId == wishlistItem.ProductId)
                .FirstOrDefaultAsync();

            if (existingCartItem != null)
            {
                existingCartItem.Quantity += 1;
                await _context.CartItems.ReplaceOneAsync(c => c.Id == existingCartItem.Id, existingCartItem);
            }
            else
            {
                var cartItem = new CartItem
                {
                    UserId = userId,
                    ProductId = wishlistItem.ProductId,
                    Quantity = 1,
                    AddedAt = DateTime.UtcNow
                };
                await _context.CartItems.InsertOneAsync(cartItem);
            }

            // Remove from wishlist
            await _context.WishlistItems.DeleteOneAsync(w => w.Id == id);

            return Ok(new { message = "Товар перемещен в корзину" });
        }

        // DELETE: api/Wishlist
        [HttpDelete]
        public async Task<IActionResult> ClearWishlist()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _context.WishlistItems.DeleteManyAsync(w => w.UserId == userId);

            return Ok(new { message = "Список желаемого очищен" });
        }

        private string GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            return userIdClaim?.Value ?? string.Empty;
        }
    }

    public class WishlistItemResponse
    {
        public string WishlistItemId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductDescription { get; set; } = string.Empty;
        public string ProductImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime AddedAt { get; set; }
    }

    public class AddToWishlistRequest
    {
        public string ProductId { get; set; } = string.Empty;
    }
}