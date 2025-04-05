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
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CartController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Cart
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CartItemResponse>>> GetCartItems()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            return cartItems.Select(item => new CartItemResponse
            {
                CartItemId = item.CartItemId,
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                ProductDescription = item.Product.Description,
                ProductImageUrl = item.Product.ImageUrl,
                Price = item.Product.Price,
                Quantity = item.Quantity,
                TotalPrice = item.Product.Price * item.Quantity
            }).ToList();
        }

        // POST: api/Cart
        [HttpPost]
        public async Task<ActionResult<CartItem>> AddToCart(AddToCartRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var product = await _context.Products.FindAsync(request.ProductId);
            if (product == null)
                return NotFound("Продукт не найден");

            var existingItem = await _context.CartItems
                .Where(c => c.UserId == userId && c.ProductId == request.ProductId)
                .FirstOrDefaultAsync();

            if (existingItem != null)
            {
                existingItem.Quantity += request.Quantity;
                _context.Entry(existingItem).State = EntityState.Modified;
            }
            else
            {
                var cartItem = new CartItem
                {
                    UserId = userId,
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    AddedAt = DateTime.UtcNow
                };
                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Товар добавлен в корзину" });
        }

        // PUT: api/Cart/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCartItem(int id, UpdateCartItemRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var cartItem = await _context.CartItems
                .Where(c => c.CartItemId == id && c.UserId == userId)
                .FirstOrDefaultAsync();

            if (cartItem == null)
                return NotFound("Товар в корзине не найден");

            cartItem.Quantity = request.Quantity;
            _context.Entry(cartItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CartItemExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Cart/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCartItem(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var cartItem = await _context.CartItems
                .Where(c => c.CartItemId == id && c.UserId == userId)
                .FirstOrDefaultAsync();

            if (cartItem == null)
                return NotFound("Товар в корзине не найден");

            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Товар удален из корзины" });
        }

        // DELETE: api/Cart
        [HttpDelete]
        public async Task<IActionResult> ClearCart()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var cartItems = await _context.CartItems
                .Where(c => c.UserId == userId)
                .ToListAsync();

            _context.CartItems.RemoveRange(cartItems);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Корзина очищена" });
        }

        private bool CartItemExists(int id)
        {
            return _context.CartItems.Any(e => e.CartItemId == id);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return 0;

            return userId;
        }
    }

    public class CartItemResponse
    {
        public int CartItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductDescription { get; set; }
        public string ProductImageUrl { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class AddToCartRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class UpdateCartItemRequest
    {
        public int Quantity { get; set; }
    }
}