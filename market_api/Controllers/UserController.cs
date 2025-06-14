using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using market_api.Models;
using market_api.Data;
using market_api.Services;
using System.Security.Claims;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly MongoDbContext _context;

    public UserController(IUserService userService, MongoDbContext context)
    {
        _userService = userService;
        _context = context;
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetUserById(string id)
    {
        // Check if the user is requesting their own data or is an admin
        var currentUserId = User.FindFirst("userId")?.Value ?? string.Empty;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        if (currentUserId != id && userRole != "admin")
        {
            return Forbid();
        }

        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }
        return Ok(user);
    }

    // GET: api/User/all
    [HttpGet("all")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<object>>> GetAllUsers()
    {
        try
        {
            var users = await _context.Users.Find(_ => true).ToListAsync();

            var userResponses = users.Select(user => new
            {
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.CreatedAt,
                user.ProfileImageUrl,
                user.IsBusiness,
                user.CompanyName,
                user.CompanyAvatar,
                user.CompanyDescription
            }).ToList();

            return Ok(userResponses);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving users", error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserModel model)
    {
        // Check if the user is updating their own data or is an admin
        var currentUserId = User.FindFirst("userId")?.Value ?? string.Empty;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        if (currentUserId != id && userRole != "admin")
        {
            return Forbid();
        }

        var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Update only the allowed fields
        if (!string.IsNullOrEmpty(model.Username))
        {
            // Check if the username is already taken by another user
            var existingUser = await _context.Users
                .Find(u => u.Username == model.Username && u.Id != id)
                .FirstOrDefaultAsync();
            if (existingUser != null)
            {
                return BadRequest(new { message = "Username is already taken" });
            }

            user.Username = model.Username;
        }

        if (!string.IsNullOrEmpty(model.Email))
        {
            // Check if the email is already taken by another user
            var existingUser = await _context.Users
                .Find(u => u.Email == model.Email && u.Id != id)
                .FirstOrDefaultAsync();
            if (existingUser != null)
            {
                return BadRequest(new { message = "Email is already taken" });
            }

            user.Email = model.Email;
        }

        // Admins can update roles
        if (userRole == "admin" && !string.IsNullOrEmpty(model.Role))
        {
            user.Role = model.Role;
        }

        await _context.Users.ReplaceOneAsync(u => u.Id == id, user);

        return Ok(new { message = "User updated successfully" });
    }

    // DELETE: api/User/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Prevent deletion of admin users
            if (user.Role == "admin")
            {
                return BadRequest(new { message = "Cannot delete admin users" });
            }

            // Clean up related data
            await _context.CartItems.DeleteManyAsync(c => c.UserId == id);
            await _context.WishlistItems.DeleteManyAsync(w => w.UserId == id);
            await _context.Reviews.DeleteManyAsync(r => r.UserId == id);
            await _context.ExternalLogins.DeleteManyAsync(el => el.UserId == id);

            // Handle orders - don't delete them, just mark as orphaned
            var userOrders = await _context.Orders.Find(o => o.UserId == id).ToListAsync();
            foreach (var order in userOrders)
            {
                order.UserId = "deleted_user"; // Mark as deleted user
                await _context.Orders.ReplaceOneAsync(o => o.Id == order.Id, order);
            }

            // Delete the user
            await _context.Users.DeleteOneAsync(u => u.Id == id);

            return Ok(new { message = "User deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error deleting user", error = ex.Message });
        }
    }

    [HttpPut("{id}/avatar")]
    [Authorize]
    public async Task<IActionResult> UpdateAvatar(string id, [FromBody] UpdateAvatarModel model)
    {
        // Check if the user is updating their own avatar or is an admin
        var currentUserId = User.FindFirst("userId")?.Value ?? string.Empty;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        if (currentUserId != id && userRole != "admin")
        {
            return Forbid();
        }

        var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        if (string.IsNullOrEmpty(model.ProfileImageUrl))
        {
            return BadRequest(new { message = "Profile image URL is required" });
        }

        user.ProfileImageUrl = model.ProfileImageUrl;
        await _context.Users.ReplaceOneAsync(u => u.Id == id, user);

        return Ok(new { message = "Avatar updated successfully" });
    }

    // PUT: api/User/{id}/business-info
    [HttpPut("{id}/business-info")]
    [Authorize]
    public async Task<IActionResult> UpdateBusinessInfo(string id, [FromBody] UpdateBusinessInfoModel model)
    {
        var currentUserId = User.FindFirst("userId")?.Value ?? string.Empty;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        if (currentUserId != id && userRole != "admin")
        {
            return Forbid();
        }

        var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        if (!user.IsBusiness)
        {
            return BadRequest(new { message = "User is not a business account" });
        }

        user.CompanyName = model.CompanyName;
        user.CompanyAvatar = model.CompanyAvatar;
        user.CompanyDescription = model.CompanyDescription;

        await _context.Users.ReplaceOneAsync(u => u.Id == id, user);

        return Ok(new { message = "Business info updated successfully" });
    }

    private string GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("userId");
        return userIdClaim?.Value ?? string.Empty;
    }
}

public class UpdateBusinessInfoModel
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyAvatar { get; set; } = string.Empty;
    public string CompanyDescription { get; set; } = string.Empty;
}

public class UpdateUserModel
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UpdateAvatarModel
{
    public string ProfileImageUrl { get; set; } = string.Empty;
}