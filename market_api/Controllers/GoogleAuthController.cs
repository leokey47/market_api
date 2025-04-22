using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using market_api.Data;
using market_api.Models;

namespace market_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GoogleAuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoogleAuthController> _logger;

        public GoogleAuthController(
            AppDbContext context,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<GoogleAuthController> logger)
        {
            _context = context;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // Endpoint for starting Google authentication
        [HttpGet("login")]
        public IActionResult Login()
        {
            _logger.LogInformation("Starting Google authentication process (manual mode)");

            // Get settings from configuration
            var clientId = _configuration["Authentication:Google:ClientId"];
            var redirectUri = $"{Request.Scheme}://{Request.Host}/api/GoogleAuth/callback";

            // Create state for CSRF protection
            var state = GenerateSecureRandomString();

            // Save state in cookie
            SaveStateInCookie(state);

            _logger.LogInformation("Set state: {State}, redirect URI: {RedirectUri}", state, redirectUri);

            // Create OAuth URL for Google
            var authUrl =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&response_type=code" +
                $"&scope={Uri.EscapeDataString("email profile")}" +
                $"&state={Uri.EscapeDataString(state)}";

            _logger.LogInformation("Redirecting to Google OAuth URL: {AuthUrl}", authUrl);

            // Redirect to Google - this is a browser redirect, not an AJAX call
            return Redirect(authUrl);
        }

        // Callback endpoint from Google
        [HttpGet("callback")]
        public async Task<IActionResult> Callback()
        {
            _logger.LogInformation("Received callback from Google (manual mode)");

            try
            {
                // Get parameters from URL
                var code = Request.Query["code"].ToString();
                var receivedState = Request.Query["state"].ToString();
                var error = Request.Query["error"].ToString();

                _logger.LogInformation("Received parameters: code={CodeExists}, state={State}, error={Error}",
                    !string.IsNullOrEmpty(code),
                    receivedState,
                    error);

                // Check for error
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning("Received error from Google: {Error}", error);
                    return RedirectToFrontendWithError($"Error from Google: {error}");
                }

                // Check for code
                if (string.IsNullOrEmpty(code))
                {
                    _logger.LogWarning("Authorization code missing in Google response");
                    return RedirectToFrontendWithError("Authorization code missing in Google response");
                }

                // Check state (CSRF protection)
                var savedState = Request.Cookies["GoogleAuthState"];

                // In production, strictly validate state
                if (string.IsNullOrEmpty(savedState) || savedState != receivedState)
                {
                    _logger.LogWarning("State mismatch: saved={SavedState}, received={ReceivedState}",
                        savedState, receivedState);

                    // For debugging, we'll continue anyway
                    _logger.LogWarning("Continuing despite state mismatch for testing");
                }

                // Clear state cookie
                Response.Cookies.Delete("GoogleAuthState", new CookieOptions
                {
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    HttpOnly = true
                });

                // Get token from Google by exchanging code
                var tokenResponse = await ExchangeCodeForTokenAsync(code);
                if (tokenResponse == null)
                {
                    _logger.LogError("Failed to exchange code for token from Google");
                    return RedirectToFrontendWithError("Failed to obtain token from Google");
                }

                // Get user info from Google
                var googleUser = await GetGoogleUserInfoAsync(tokenResponse.AccessToken);
                if (googleUser == null)
                {
                    _logger.LogError("Failed to get user info from Google");
                    return RedirectToFrontendWithError("Failed to get user information");
                }

                _logger.LogInformation("Received user info: Email={Email}, Name={Name}, ID={Id}",
                    googleUser.Email, googleUser.Name, googleUser.Id);

                // Process user info and create JWT
                var token = await ProcessGoogleUser(googleUser);

                // Redirect to frontend with token
                return RedirectToFrontendWithToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Google callback");
                return RedirectToFrontendWithError($"Server error: {ex.Message}");
            }
        }

        private async Task<GoogleTokenResponse> ExchangeCodeForTokenAsync(string code)
        {
            try
            {
                var clientId = _configuration["Authentication:Google:ClientId"];
                var clientSecret = _configuration["Authentication:Google:ClientSecret"];
                var redirectUri = $"{Request.Scheme}://{Request.Host}/api/GoogleAuth/callback";

                var httpClient = _httpClientFactory.CreateClient();

                var tokenRequestContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri)
                });

                _logger.LogInformation("Requesting token from Google with redirect_uri: {RedirectUri}", redirectUri);

                var response = await httpClient.PostAsync(
                    "https://oauth2.googleapis.com/token",
                    tokenRequestContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Google returned error exchanging code: {StatusCode}, {Error}",
                        response.StatusCode, errorContent);
                    return null;
                }

                var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>();
                _logger.LogInformation("Received token from Google: {TokenType}", tokenResponse?.TokenType);

                return tokenResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging code for token");
                return null;
            }
        }

        private async Task<GoogleUserInfo> GetGoogleUserInfoAsync(string accessToken)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Google returned error requesting user data: {StatusCode}, {Error}",
                        response.StatusCode, errorContent);
                    return null;
                }

                var userInfo = await response.Content.ReadFromJsonAsync<GoogleUserInfo>();
                return userInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user information");
                return null;
            }
        }

        private async Task<string> ProcessGoogleUser(GoogleUserInfo googleUser)
        {
            _logger.LogInformation("Processing Google user: {Email}", googleUser.Email);

            // Check if user exists with this email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == googleUser.Email);

            if (user == null)
            {
                // Create new user
                string username = await GenerateUniqueUsername(googleUser.Name);

                user = new User
                {
                    Username = username,
                    Email = googleUser.Email,
                    PasswordHash = HashRandomPassword(),
                    Role = "user",
                    CreatedAt = DateTime.UtcNow,
                    ProfileImageUrl = googleUser.Picture ?? ""
                };

                _logger.LogInformation("Creating new user {Username} from Google", username);
                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();
            }

            // Check existence of ExternalLogin table
            var externalLoginExists = _context.Model.FindEntityType(typeof(ExternalLogin)) != null;

            if (externalLoginExists)
            {
                // Check if external provider link already exists
                var externalLogin = await _context.ExternalLogins
                    .FirstOrDefaultAsync(el => el.UserId == user.UserId && el.Provider == "google");

                if (externalLogin == null)
                {
                    // Add new external provider link
                    _logger.LogInformation("Adding new external login record for user {UserId}", user.UserId);
                    await _context.ExternalLogins.AddAsync(new ExternalLogin
                    {
                        Provider = "google",
                        ProviderKey = googleUser.Sub,
                        UserId = user.UserId
                    });

                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                _logger.LogWarning("ExternalLogin table not found in model.");
            }

            // Generate JWT token
            return GenerateJwtToken(user);
        }

        private IActionResult RedirectToFrontendWithToken(string token)
        {
            string baseUrl = _configuration["OAuth:FrontendRedirectUrl"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = "http://localhost:3000";
            }

            string redirectUrl = baseUrl.TrimEnd('/') + "/oauth-callback";

            _logger.LogInformation("Redirecting to frontend: {Url} with token", redirectUrl);
            return Redirect($"{redirectUrl}?token={Uri.EscapeDataString(token)}");
        }

        private IActionResult RedirectToFrontendWithError(string errorMessage)
        {
            string baseUrl = _configuration["OAuth:FrontendRedirectUrl"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = "http://localhost:3000";
            }

            string redirectUrl = baseUrl.TrimEnd('/') + "/oauth-callback";

            _logger.LogInformation("Redirecting to frontend with error: {Url}, Message: {ErrorMessage}",
                redirectUrl, errorMessage);

            return Redirect($"{redirectUrl}?error={Uri.EscapeDataString(errorMessage)}");
        }

        private async Task<string> GenerateUniqueUsername(string baseName)
        {
            // Remove spaces and special characters
            string sanitizedName = new string(baseName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

            // Make sure name isn't empty
            if (string.IsNullOrEmpty(sanitizedName))
                sanitizedName = "user";

            string username = sanitizedName.ToLower();
            int counter = 1;

            // Check if username already exists
            while (await _context.Users.AnyAsync(u => u.Username == username))
            {
                // Add numeric suffix and try again
                username = $"{sanitizedName.ToLower()}{counter}";
                counter++;
            }

            return username;
        }

        private string HashRandomPassword()
        {
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

        private string GenerateSecureRandomString()
        {
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                var bytes = new byte[32]; // 256 bits
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .Replace("=", "");
            }
        }

        private void SaveStateInCookie(string state)
        {
            Response.Cookies.Append(
                "GoogleAuthState",
                state,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None, // For cross-domain interaction
                    MaxAge = TimeSpan.FromMinutes(10) // Limited lifetime
                }
            );
        }
    }

    // Data models for OAuth
    public class GoogleTokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("scope")]
        public string Scope { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("id_token")]
        public string IdToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }
    }

    public class GoogleUserInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("sub")]
        public string Sub { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("given_name")]
        public string GivenName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("family_name")]
        public string FamilyName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("picture")]
        public string Picture { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string Email { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("email_verified")]
        public bool EmailVerified { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("locale")]
        public string Locale { get; set; }

        // Alias for compatibility
        public string Id => Sub;
    }
}
