using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using MongoDB.Driver;
using market_api.Data;
using market_api.Models;

[ApiController]
[Route("api/[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly MongoDbContext _context;

    public AuthenticationController(MongoDbContext context)
    {
        _context = context;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
        {
            return BadRequest(new { message = "Имя пользователя и пароль обязательны" });
        }

        var user = await _context.Users.Find(u => u.Username == model.Username).FirstOrDefaultAsync();

        if (user == null)
        {
            return Unauthorized(new { message = "Пользователь не найден" });
        }

        if (user.PasswordHash != HashPassword(model.Password))
        {
            return Unauthorized(new { message = "Неправильный пароль" });
        }

        var token = GenerateJwtToken(user);
        return Ok(new { token });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Email)
            || string.IsNullOrEmpty(model.Password) || string.IsNullOrEmpty(model.Reason))
        {
            return BadRequest(new { message = "Все поля обязательны для заполнения" });
        }

        // Проверка на существующего пользователя
        var existingUserFilter = Builders<User>.Filter.Or(
            Builders<User>.Filter.Eq(u => u.Username, model.Username),
            Builders<User>.Filter.Eq(u => u.Email, model.Email)
        );
        var existingUser = await _context.Users.Find(existingUserFilter).FirstOrDefaultAsync();

        if (existingUser != null)
        {
            return BadRequest(new { message = "Пользователь с таким именем или email уже существует" });
        }

        var user = new User
        {
            Username = model.Username,
            Email = model.Email,
            PasswordHash = HashPassword(model.Password),
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            ProfileImageUrl = "" // Пустая строка по умолчанию
        };

        await _context.Users.InsertOneAsync(user);

        return Ok(new { message = "Регистрация прошла успешно" });
    }

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes("thisisaverysecureandlongenoughkey123456");
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("userId", user.Id ?? string.Empty)
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
        }
    }
}

public class LoginModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// Модель для регистрации
public class RegisterModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty; // Добавлено поле
}