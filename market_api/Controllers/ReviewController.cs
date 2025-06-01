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
    public class ReviewController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<ReviewController> _logger;

        // Define completed statuses as a static list for reuse
        private static readonly string[] CompletedStatuses = new[]
        {
            "completed",
            "оплачен",
            "завершен",
            "доставлен",
            "получен"
        };

        public ReviewController(MongoDbContext context, ILogger<ReviewController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Review/Product/{productId}
        [HttpGet("Product/{productId}")]
        public async Task<ActionResult<IEnumerable<ReviewResponse>>> GetProductReviews(string productId)
        {
            try
            {
                var reviews = await _context.Reviews
                    .Find(r => r.ProductId == productId)
                    .SortByDescending(r => r.CreatedAt)
                    .ToListAsync();

                var response = new List<ReviewResponse>();

                foreach (var review in reviews)
                {
                    var user = await _context.Users.Find(u => u.Id == review.UserId).FirstOrDefaultAsync();
                    if (user != null)
                    {
                        response.Add(new ReviewResponse
                        {
                            ReviewId = review.Id!,
                            Username = user.Username,
                            Rating = review.Rating,
                            Text = review.Text,
                            CreatedAt = review.CreatedAt
                        });
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product reviews for product {ProductId}", productId);
                return StatusCode(500, "Error retrieving reviews");
            }
        }

        // GET: api/Review/User
        [HttpGet("User")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<ReviewDetailResponse>>> GetUserReviews()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var reviews = await _context.Reviews
                    .Find(r => r.UserId == userId)
                    .SortByDescending(r => r.CreatedAt)
                    .ToListAsync();

                var response = new List<ReviewDetailResponse>();

                foreach (var review in reviews)
                {
                    var product = await _context.Products.Find(p => p.Id == review.ProductId).FirstOrDefaultAsync();
                    if (product != null)
                    {
                        response.Add(new ReviewDetailResponse
                        {
                            ReviewId = review.Id!,
                            ProductId = review.ProductId,
                            ProductName = product.Name,
                            ProductImageUrl = product.ImageUrl,
                            Rating = review.Rating,
                            Text = review.Text,
                            CreatedAt = review.CreatedAt
                        });
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user reviews for user {UserId}", userId);
                return StatusCode(500, "Error retrieving user reviews");
            }
        }

        // POST: api/Review
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Review>> CreateReview(CreateReviewRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                // Validate request
                if (request.Rating < 1 || request.Rating > 5)
                    return BadRequest("Рейтинг должен быть от 1 до 5");

                if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length < 10)
                    return BadRequest("Текст отзыва должен содержать не менее 10 символов");

                // Check if product exists
                var product = await _context.Products.Find(p => p.Id == request.ProductId).FirstOrDefaultAsync();
                if (product == null)
                    return NotFound("Продукт не найден");

                // Check if the user has purchased this product
                var orderItems = await _context.OrderItems
                    .Find(oi => oi.ProductId == request.ProductId)
                    .ToListAsync();

                // Get orders for this user and check if any contain the product with completed status
                var userOrders = await _context.Orders
                    .Find(o => o.UserId == userId)
                    .ToListAsync();

                var completedOrder = userOrders.FirstOrDefault(order =>
                    IsOrderCompleted(order.Status) &&
                    orderItems.Any(oi => oi.OrderId == order.Id));

                if (completedOrder == null)
                {
                    _logger.LogWarning("User {UserId} attempted to review product {ProductId} without purchasing", userId, request.ProductId);
                    return BadRequest("Вы можете оставить отзыв только на купленный товар");
                }

                // Check if user already reviewed this product
                var existingReview = await _context.Reviews
                    .Find(r => r.UserId == userId && r.ProductId == request.ProductId)
                    .FirstOrDefaultAsync();

                if (existingReview != null)
                {
                    // Update existing review
                    existingReview.Rating = request.Rating;
                    existingReview.Text = request.Text.Trim();
                    existingReview.CreatedAt = DateTime.UtcNow;

                    await _context.Reviews.ReplaceOneAsync(r => r.Id == existingReview.Id, existingReview);
                    _logger.LogInformation("Updated review {ReviewId} for user {UserId} and product {ProductId}", existingReview.Id, userId, request.ProductId);
                }
                else
                {
                    // Create new review
                    var review = new Review
                    {
                        UserId = userId,
                        ProductId = request.ProductId,
                        OrderId = completedOrder.Id!,
                        Rating = request.Rating,
                        Text = request.Text.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Reviews.InsertOneAsync(review);
                    _logger.LogInformation("Created new review for user {UserId} and product {ProductId}", userId, request.ProductId);
                }

                return Ok(new { message = "Отзыв сохранен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating review for user {UserId} and product {ProductId}", userId, request.ProductId);
                return StatusCode(500, "Ошибка при сохранении отзыва");
            }
        }

        // DELETE: api/Review/{id}
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var review = await _context.Reviews
                    .Find(r => r.Id == id && r.UserId == userId)
                    .FirstOrDefaultAsync();

                if (review == null)
                    return NotFound("Отзыв не найден");

                await _context.Reviews.DeleteOneAsync(r => r.Id == id);

                _logger.LogInformation("Deleted review {ReviewId} for user {UserId}", id, userId);
                return Ok(new { message = "Отзыв удален" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting review {ReviewId} for user {UserId}", id, userId);
                return StatusCode(500, "Ошибка при удалении отзыва");
            }
        }

        // GET: api/Review/CanReview/{productId}
        [HttpGet("CanReview/{productId}")]
        [Authorize]
        public async Task<ActionResult<CanReviewResponse>> CanUserReviewProduct(string productId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                // Check if user has purchased this product
                var orderItems = await _context.OrderItems
                    .Find(oi => oi.ProductId == productId)
                    .ToListAsync();

                // Get orders for this user and check if any contain the product with completed status
                var userOrders = await _context.Orders
                    .Find(o => o.UserId == userId)
                    .ToListAsync();

                var hasPurchased = userOrders.Any(order =>
                    IsOrderCompleted(order.Status) &&
                    orderItems.Any(oi => oi.OrderId == order.Id));

                // Check if user already reviewed this product
                var hasReviewed = await _context.Reviews
                    .Find(r => r.UserId == userId && r.ProductId == productId)
                    .FirstOrDefaultAsync() != null;

                var canReview = hasPurchased && !hasReviewed;

                return Ok(new CanReviewResponse
                {
                    CanReview = canReview,
                    HasPurchased = hasPurchased,
                    HasReviewed = hasReviewed,
                    Message = GetReviewStatusMessage(hasPurchased, hasReviewed)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking review status for user {UserId} and product {ProductId}", userId, productId);
                return StatusCode(500, "Ошибка при проверке статуса отзыва");
            }
        }

        // GET: api/Review/Check/{productId}
        [HttpGet("Check/{productId}")]
        [Authorize]
        public async Task<ActionResult<ReviewCheckResponse>> CheckUserReviewForProduct(string productId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var existingReview = await _context.Reviews
                    .Find(r => r.UserId == userId && r.ProductId == productId)
                    .FirstOrDefaultAsync();

                if (existingReview != null)
                {
                    return Ok(new ReviewCheckResponse
                    {
                        HasReview = true,
                        Review = new ReviewResponse
                        {
                            ReviewId = existingReview.Id!,
                            Username = "", // Не нужно для собственного отзыва
                            Rating = existingReview.Rating,
                            Text = existingReview.Text,
                            CreatedAt = existingReview.CreatedAt
                        }
                    });
                }

                return Ok(new ReviewCheckResponse { HasReview = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user review for product {ProductId}", productId);
                return StatusCode(500, "Ошибка при проверке отзыва");
            }
        }

        private bool IsOrderCompleted(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            // Use case-insensitive comparison
            return CompletedStatuses.Any(completedStatus =>
                status.ToLower().Contains(completedStatus.ToLower()));
        }

        private string GetReviewStatusMessage(bool hasPurchased, bool hasReviewed)
        {
            if (!hasPurchased)
                return "Вы должны купить этот товар, чтобы оставить отзыв";
            if (hasReviewed)
                return "Вы уже оставили отзыв на этот товар";
            return "Вы можете оставить отзыв на этот товар";
        }

        private string GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            return userIdClaim?.Value ?? string.Empty;
        }
    }

    public class ReviewResponse
    {
        public string ReviewId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ReviewDetailResponse
    {
        public string ReviewId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductImageUrl { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateReviewRequest
    {
        public string ProductId { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class CanReviewResponse
    {
        public bool CanReview { get; set; }
        public bool HasPurchased { get; set; }
        public bool HasReviewed { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ReviewCheckResponse
    {
        public bool HasReview { get; set; }
        public ReviewResponse? Review { get; set; }
    }
}