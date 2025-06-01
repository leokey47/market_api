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
    public class CartController : ControllerBase
    {
        private readonly MongoDbContext _context;

        public CartController(MongoDbContext context)
        {
            _context = context;
        }

        // GET: api/Cart
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CartItemResponse>>> GetCartItems()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var cartItems = await _context.CartItems
                .Find(c => c.UserId == userId)
                .ToListAsync();

            var response = new List<CartItemResponse>();

            foreach (var item in cartItems)
            {
                var product = await _context.Products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
                if (product != null)
                {
                    response.Add(new CartItemResponse
                    {
                        CartItemId = item.Id ?? string.Empty,
                        ProductId = item.ProductId,
                        ProductName = product.Name,
                        ProductDescription = product.Description,
                        ProductImageUrl = product.ImageUrl,
                        Price = product.Price,
                        Quantity = item.Quantity,
                        TotalPrice = product.Price * item.Quantity
                    });
                }
            }

            return response;
        }

        // POST: api/Cart
        [HttpPost]
        public async Task<ActionResult<CartItem>> AddToCart(AddToCartRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var product = await _context.Products.Find(p => p.Id == request.ProductId).FirstOrDefaultAsync();
            if (product == null)
                return NotFound("Продукт не найден");

            var existingItem = await _context.CartItems
                .Find(c => c.UserId == userId && c.ProductId == request.ProductId)
                .FirstOrDefaultAsync();

            if (existingItem != null)
            {
                existingItem.Quantity += request.Quantity;
                await _context.CartItems.ReplaceOneAsync(c => c.Id == existingItem.Id, existingItem);
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
                await _context.CartItems.InsertOneAsync(cartItem);
            }

            return Ok(new { message = "Товар добавлен в корзину" });
        }

        // PUT: api/Cart/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCartItem(string id, UpdateCartItemRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var cartItem = await _context.CartItems
                .Find(c => c.Id == id && c.UserId == userId)
                .FirstOrDefaultAsync();

            if (cartItem == null)
                return NotFound("Товар в корзине не найден");

            cartItem.Quantity = request.Quantity;
            var result = await _context.CartItems.ReplaceOneAsync(c => c.Id == id, cartItem);

            if (result.ModifiedCount == 0)
                return NotFound();

            return NoContent();
        }

        // DELETE: api/Cart/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCartItem(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var cartItem = await _context.CartItems
                .Find(c => c.Id == id && c.UserId == userId)
                .FirstOrDefaultAsync();

            if (cartItem == null)
                return NotFound("Товар в корзине не найден");

            await _context.CartItems.DeleteOneAsync(c => c.Id == id);

            return Ok(new { message = "Товар удален из корзины" });
        }

        // DELETE: api/Cart
        [HttpDelete]
        public async Task<IActionResult> ClearCart()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _context.CartItems.DeleteManyAsync(c => c.UserId == userId);

            return Ok(new { message = "Корзина очищена" });
        }

        private string GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            return userIdClaim?.Value ?? string.Empty;
        }
    }

    public class CartItemResponse
    {
        public string CartItemId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductDescription { get; set; } = string.Empty;
        public string ProductImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class AddToCartRequest
    {
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
    }

    public class UpdateCartItemRequest
    {
        public int Quantity { get; set; }
    }
}