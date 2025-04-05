using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
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
        private readonly AppDbContext _context;

        public WishlistController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Wishlist
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WishlistItemResponse>>> GetWishlistItems()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var wishlistItems = await _context.WishlistItems
                .Include(w => w.Product)
                .Where(w => w.UserId == userId)
                .ToListAsync();

            return wishlistItems.Select(item => new WishlistItemResponse
            {
                WishlistItemId = item.WishlistItemId,
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                ProductDescription = item.Product.Description,
                Price = item.Product.Price,
                ProductImageUrl = item.Product.ImageUrl,
                AddedAt = item.AddedAt
            }).ToList();
        }

        // POST: api/Wishlist
        [HttpPost]
        public async Task<ActionResult<WishlistItem>> AddToWishlist(AddToWishlistRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var product = await _context.Products.FindAsync(request.ProductId);
            if (product == null)
                return NotFound("Продукт не найден");

            // Check if product is already in wishlist
            var existingItem = await _context.WishlistItems
                .Where(w => w.UserId == userId && w.ProductId == request.ProductId)
                .FirstOrDefaultAsync();

            if (existingItem != null)
                return Ok(new { message = "Товар уже в списке желаемого" });

            var wishlistItem = new WishlistItem
            {
                UserId = userId,
                ProductId = request.ProductId,
                AddedAt = DateTime.UtcNow
            };

            _context.WishlistItems.Add(wishlistItem);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Товар добавлен в список желаемого" });
        }

        // DELETE: api/Wishlist/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveFromWishlist(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var wishlistItem = await _context.WishlistItems
                .Where(w => w.WishlistItemId == id && w.UserId == userId)
                .FirstOrDefaultAsync();

            if (wishlistItem == null)
                return NotFound("Товар не найден в списке желаемого");

            _context.WishlistItems.Remove(wishlistItem);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Товар удален из списка желаемого" });
        }

        // POST: api/Wishlist/MoveToCart/5
        [HttpPost("MoveToCart/{id}")]
        public async Task<IActionResult> MoveToCart(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var wishlistItem = await _context.WishlistItems
                .Where(w => w.WishlistItemId == id && w.UserId == userId)
                .FirstOrDefaultAsync();

            if (wishlistItem == null)
                return NotFound("Товар не найден в списке желаемого");

            // Check if product is already in cart
            var existingCartItem = await _context.CartItems
                .Where(c => c.UserId == userId && c.ProductId == wishlistItem.ProductId)
                .FirstOrDefaultAsync();

            if (existingCartItem != null)
            {
                existingCartItem.Quantity += 1;
                _context.Entry(existingCartItem).State = EntityState.Modified;
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
                _context.CartItems.Add(cartItem);
            }

            // Remove from wishlist
            _context.WishlistItems.Remove(wishlistItem);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Товар перемещен в корзину" });
        }

        // DELETE: api/Wishlist
        [HttpDelete]
        public async Task<IActionResult> ClearWishlist()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var wishlistItems = await _context.WishlistItems
                .Where(w => w.UserId == userId)
                .ToListAsync();

            _context.WishlistItems.RemoveRange(wishlistItems);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Список желаемого очищен" });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return 0;

            return userId;
        }
    }

    public class WishlistItemResponse
    {
        public int WishlistItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductDescription { get; set; }
        public string ProductImageUrl { get; set; }
        public decimal Price { get; set; }
        public DateTime AddedAt { get; set; }
    }

    public class AddToWishlistRequest
    {
        public int ProductId { get; set; }
    }
}