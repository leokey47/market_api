using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using market_api.Data;
using market_api.Models;

namespace market_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OAuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OAuthController> _logger;

        public OAuthController(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<OAuthController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // Эндпоинт для начала аутентификации Google
        [HttpGet("google")]
        public IActionResult GoogleLogin()
        {
            _logger.LogInformation("Начинаем процесс аутентификации Google");

            // Не устанавливаем RedirectUri для обработки обратного вызова
            // ASP.NET Core автоматически использует значение CallbackPath из конфигурации
            var properties = new AuthenticationProperties
            {
                // Добавляем корреляционный идентификатор для связывания запросов
                // ASP.NET Core автоматически обрабатывает state в .xsrf
                Items = { { "scheme", "Google" } },

                // Используем абсолютный URL для перенаправления после аутентификации
                RedirectUri = "/api/OAuth/google-callback"
            };

            foreach (var item in properties.Items)
            {
                _logger.LogInformation("Auth property: {Key} = {Value}", item.Key, item.Value);
            }

            // Выполняем перенаправление на Google OAuth
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // Эндпоинт обратного вызова Google
        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            _logger.LogInformation("Получен обратный вызов от Google");

            try
            {
                // Аутентификация результата
                var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Ошибка аутентификации Google: {Error}",
                        result.Failure?.Message);
                    _logger.LogWarning("Полная информация об ошибке: {Exception}",
                        result.Failure?.ToString());

                    string errorDetails = result.Failure?.Message ?? "Неизвестная ошибка";

                    // Проверяем статус cookie
                    var cookies = HttpContext.Request.Cookies;
                    _logger.LogInformation("Текущие cookies: {CookieCount}", cookies.Count);
                    foreach (var cookie in cookies)
                    {
                        _logger.LogInformation("Cookie: {Name} = {Value}", cookie.Key, "Значение скрыто");
                    }

                    return RedirectToFrontendWithError($"Ошибка аутентификации: {errorDetails}");
                }

                // Извлекаем данные пользователя
                var claims = result.Principal.Identities.FirstOrDefault()?.Claims;
                if (claims == null)
                {
                    _logger.LogWarning("Не найдены утверждения (claims) в результате аутентификации Google");
                    return RedirectToFrontendWithError("Информация о пользователе не найдена");
                }

                // Логируем полученные утверждения для диагностики
                foreach (var claim in claims)
                {
                    _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
                }

                var emailClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
                var nameClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                var idClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

                if (emailClaim == null || nameClaim == null || idClaim == null)
                {
                    _logger.LogWarning("Отсутствуют необходимые утверждения от Google");
                    return RedirectToFrontendWithError("Отсутствует необходимая информация о пользователе");
                }

                _logger.LogInformation("Успешная аутентификация Google для пользователя: {Email}", emailClaim.Value);

                // Обрабатываем вход и получаем JWT токен
                var token = await ProcessExternalLogin(
                    idClaim.Value,
                    "google",
                    emailClaim.Value,
                    nameClaim.Value);

                // Перенаправляем на фронтенд с токеном
                return RedirectToFrontendWithToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в обработке обратного вызова Google");
                return RedirectToFrontendWithError($"Внутренняя ошибка сервера: {ex.Message}");
            }
        }

        private IActionResult RedirectToFrontendWithToken(string token)
        {
            // Получаем базовый URL для фронтенда
            string baseUrl = _configuration["OAuth:FrontendRedirectUrl"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = "http://localhost:3000";
            }
            else
            {
                // Убедимся, что мы не добавляем '/oauth-callback' дважды
                if (baseUrl.EndsWith("/oauth-callback"))
                {
                    baseUrl = baseUrl.Substring(0, baseUrl.Length - "/oauth-callback".Length);
                }
            }

            // Формируем полный URL для перенаправления
            string redirectUrl = baseUrl.TrimEnd('/') + "/oauth-callback";

            _logger.LogInformation("Перенаправление на фронтенд: {Url} с токеном", redirectUrl);
            return Redirect($"{redirectUrl}?token={Uri.EscapeDataString(token)}");
        }

        private IActionResult RedirectToFrontendWithError(string errorMessage)
        {
            // Предотвращаем дублирование путей, проверяя значение из конфигурации
            string baseUrl = _configuration["OAuth:FrontendRedirectUrl"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = "http://localhost:3000";
            }
            else
            {
                // Убедимся, что мы не добавляем '/oauth-callback' дважды
                if (baseUrl.EndsWith("/oauth-callback"))
                {
                    baseUrl = baseUrl.Substring(0, baseUrl.Length - "/oauth-callback".Length);
                }
            }

            // Формируем полный URL для перенаправления
            string redirectUrl = baseUrl.TrimEnd('/') + "/oauth-callback";

            _logger.LogInformation("Перенаправление на фронтенд с ошибкой: {Url}, Сообщение: {ErrorMessage}",
                redirectUrl, errorMessage);

            return Redirect($"{redirectUrl}?error={Uri.EscapeDataString(errorMessage)}");
        }

        private async Task<string> ProcessExternalLogin(string externalId, string provider, string email, string displayName)
        {
            _logger.LogInformation("Обработка внешнего входа для пользователя {Provider}: {Id}", provider, externalId);

            // Проверяем существует ли пользователь с этим email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                // Создаем нового пользователя
                string username = await GenerateUniqueUsername(displayName);

                user = new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = HashRandomPassword(),
                    Role = "user",
                    CreatedAt = DateTime.UtcNow,
                    ProfileImageUrl = ""
                };

                _logger.LogInformation("Создание нового пользователя {Username} из {Provider}", username, provider);
                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();
            }

            // Проверяем существование таблицы ExternalLogin
            var externalLoginExists = _context.Model.FindEntityType(typeof(ExternalLogin)) != null;

            if (externalLoginExists)
            {
                // Проверяем существует ли уже связь с внешним провайдером
                var externalLogin = await _context.ExternalLogins
                    .FirstOrDefaultAsync(el => el.UserId == user.UserId && el.Provider == provider);

                if (externalLogin == null)
                {
                    // Добавляем новую связь с внешним провайдером
                    _logger.LogInformation("Добавление новой записи внешнего входа для пользователя {UserId}", user.UserId);
                    await _context.ExternalLogins.AddAsync(new ExternalLogin
                    {
                        Provider = provider,
                        ProviderKey = externalId,
                        UserId = user.UserId
                    });

                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                _logger.LogWarning("Таблица ExternalLogin не найдена в модели. Убедитесь, что таблица добавлена в DbContext и миграции выполнены");
            }

            // Генерируем JWT токен
            return GenerateJwtToken(user);
        }

        private async Task<string> GenerateUniqueUsername(string baseName)
        {
            // Удаляем пробелы и специальные символы
            string sanitizedName = new string(baseName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

            // Убеждаемся, что имя не пустое
            if (string.IsNullOrEmpty(sanitizedName))
                sanitizedName = "user";

            string username = sanitizedName.ToLower();
            int counter = 1;

            // Проверяем существует ли уже пользователь с таким именем
            while (await _context.Users.AnyAsync(u => u.Username == username))
            {
                // Добавляем числовой суффикс и пробуем снова
                username = $"{sanitizedName.ToLower()}{counter}";
                counter++;
            }

            return username;
        }

        private string HashRandomPassword()
        {
            var random = new Random();
            var password = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 16);

            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"] ?? "thisisaverysecureandlongenoughkey123456");
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("userId", user.UserId.ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(24),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}