using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using market_api.Models;
using market_api.Data;
using market_api.Services;
using System.Security.Claims;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly AppDbContext _context;

    public UserController(IUserService userService, AppDbContext context)
    {
        _userService = userService;
        _context = context;
    }

    [HttpGet("{id}")]
    [Authorize]
    public IActionResult GetUserById(int id)
    {
        // Check if the user is requesting their own data or is an admin
        var currentUserId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        if (currentUserId != id && userRole != "admin")
        {
            return Forbid();
        }

        var user = _userService.GetUserById(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }
        return Ok(user);
    }

    [HttpPut("{id}")]
    [Authorize]
    public IActionResult UpdateUser(int id, [FromBody] UpdateUserModel model)
    {
        // Check if the user is updating their own data or is an admin
        var currentUserId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        if (currentUserId != id && userRole != "admin")
        {
            return Forbid();
        }

        var user = _context.Users.Find(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Update only the allowed fields
        if (!string.IsNullOrEmpty(model.Username))
        {
            // Check if the username is already taken by another user
            var existingUser = _context.Users.FirstOrDefault(u => u.Username == model.Username && u.UserId != id);
            if (existingUser != null)
            {
                return BadRequest(new { message = "Username is already taken" });
            }

            user.Username = model.Username;
        }

        if (!string.IsNullOrEmpty(model.Email))
        {
            // Check if the email is already taken by another user
            var existingUser = _context.Users.FirstOrDefault(u => u.Email == model.Email && u.UserId != id);
            if (existingUser != null)
            {
                return BadRequest(new { message = "Email is already taken" });
            }

            user.Email = model.Email;
        }

        _context.SaveChanges();

        return Ok(new { message = "User updated successfully" });
    }

    [HttpPut("{id}/avatar")]
    [Authorize]
    public IActionResult UpdateAvatar(int id, [FromBody] UpdateAvatarModel model)
    {
        // Check if the user is updating their own avatar or is an admin
        var currentUserId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        if (currentUserId != id && userRole != "admin")
        {
            return Forbid();
        }

        var user = _context.Users.Find(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        if (string.IsNullOrEmpty(model.ProfileImageUrl))
        {
            return BadRequest(new { message = "Profile image URL is required" });
        }

        user.ProfileImageUrl = model.ProfileImageUrl;
        _context.SaveChanges();

        return Ok(new { message = "Avatar updated successfully" });
    }
}

public class UpdateUserModel
{
    public string Username { get; set; }
    public string Email { get; set; }
}

public class UpdateAvatarModel
{
    public string ProfileImageUrl { get; set; }
}