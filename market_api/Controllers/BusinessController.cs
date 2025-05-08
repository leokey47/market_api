using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using market_api.Data;
using market_api.Models;

namespace market_api.Controllers
{
    [Route("api/User")]
    [ApiController]
    [Authorize]
    public class BusinessController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BusinessController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/User/{userId}/business
        [HttpPost("{userId}/business")]
        public async Task<IActionResult> CreateBusinessAccount(int userId, [FromBody] CreateBusinessAccountRequest request)
        {
            // Проверяем, что пользователь может изменять этот аккаунт
            var currentUserId = GetCurrentUserId();
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserId != userId && userRole != "admin")
            {
                return Forbid();
            }

            var user = await _context.Users.FindAsync(userId);
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
                await _context.SaveChangesAsync();
                return Ok(new { message = "Business account created successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating business account" });
            }
        }

        // PUT: api/User/{userId}/business
        [HttpPut("{userId}/business")]
        public async Task<IActionResult> UpdateBusinessAccount(int userId, [FromBody] UpdateBusinessAccountRequest request)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserId != userId && userRole != "admin")
            {
                return Forbid();
            }

            var user = await _context.Users.FindAsync(userId);
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
                await _context.SaveChangesAsync();
                return Ok(new { message = "Business account updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating business account" });
            }
        }

        // GET: api/User/{userId}/products
        [HttpGet("{userId}/products")]
        public async Task<ActionResult<IEnumerable<object>>> GetBusinessProducts(int userId)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Проверяем права доступа
            if (currentUserId != userId && userRole != "admin")
            {
                return Forbid();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            if (!user.IsBusiness)
            {
                return BadRequest(new { message = "User is not a business account" });
            }

            var products = await _context.Products
                .Where(p => p.BusinessOwnerId == userId)
                .Include(p => p.Photos)
                .Include(p => p.Specifications)
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.ImageUrl,
                    p.Category,
                    Photos = p.Photos.OrderBy(ph => ph.DisplayOrder).Select(ph => new {
                        ph.Id,
                        ph.ImageUrl,
                        ph.DisplayOrder
                    }),
                    Specifications = p.Specifications.Select(s => new {
                        s.Id,
                        s.Name,
                        s.Value
                    })
                })
                .ToListAsync();

            return Ok(products);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return 0;

            return userId;
        }
    }

    public class CreateBusinessAccountRequest
    {
        public string CompanyName { get; set; }
        public string CompanyAvatar { get; set; }
        public string CompanyDescription { get; set; }
    }

    public class UpdateBusinessAccountRequest
    {
        public string CompanyName { get; set; }
        public string CompanyAvatar { get; set; }
        public string CompanyDescription { get; set; }
    }
}