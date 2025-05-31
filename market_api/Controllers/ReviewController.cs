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
    public class ReviewController : ControllerBase
    {
        private readonly AppDbContext _context;
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

        public ReviewController(AppDbContext context, ILogger<ReviewController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Review/Product/{productId}
        [HttpGet("Product/{productId}")]
        public async Task<ActionResult<IEnumerable<ReviewResponse>>> GetProductReviews(int productId)
        {
            try
            {
                var reviews = await _context.Reviews
                    .Include(r => r.User)
                    .Where(r => r.ProductId == productId)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                return reviews.Select(review => new ReviewResponse
                {
                    ReviewId = review.ReviewId,
                    Username = review.User.Username,
                    Rating = review.Rating,
                    Text = review.Text,
                    CreatedAt = review.CreatedAt
                }).ToList();
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
            if (userId == 0)
                return Unauthorized();

            try
            {
                var reviews = await _context.Reviews
                    .Include(r => r.Product)
                    .Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                return reviews.Select(review => new ReviewDetailResponse
                {
                    ReviewId = review.ReviewId,
                    ProductId = review.ProductId,
                    ProductName = review.Product.Name,
                    ProductImageUrl = review.Product.ImageUrl,
                    Rating = review.Rating,
                    Text = review.Text,
                    CreatedAt = review.CreatedAt
                }).ToList();
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
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Validate request
                if (request.Rating < 1 || request.Rating > 5)
                    return BadRequest("Рейтинг должен быть от 1 до 5");

                if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length < 10)
                    return BadRequest("Текст отзыва должен содержать не менее 10 символов");

                // Check if product exists
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                    return NotFound("Продукт не найден");

                // Check if the user has purchased this product
                // Move the status check to client evaluation to avoid EF Core translation issues
                var orderItems = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi =>
                        oi.ProductId == request.ProductId &&
                        oi.Order.UserId == userId)
                    .ToListAsync(); // Execute query first

                // Now filter in memory using the IsOrderCompleted method
                var completedOrder = orderItems.FirstOrDefault(oi => IsOrderCompleted(oi.Order.Status));

                if (completedOrder == null)
                {
                    _logger.LogWarning("User {UserId} attempted to review product {ProductId} without purchasing", userId, request.ProductId);
                    return BadRequest("Вы можете оставить отзыв только на купленный товар");
                }

                // Check if user already reviewed this product
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == request.ProductId);

                if (existingReview != null)
                {
                    // Update existing review
                    existingReview.Rating = request.Rating;
                    existingReview.Text = request.Text.Trim();
                    existingReview.CreatedAt = DateTime.UtcNow;

                    _context.Entry(existingReview).State = EntityState.Modified;
                    _logger.LogInformation("Updated review {ReviewId} for user {UserId} and product {ProductId}", existingReview.ReviewId, userId, request.ProductId);
                }
                else
                {
                    // Create new review
                    var review = new Review
                    {
                        UserId = userId,
                        ProductId = request.ProductId,
                        OrderId = completedOrder.Order.OrderId,
                        Rating = request.Rating,
                        Text = request.Text.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Reviews.Add(review);
                    _logger.LogInformation("Created new review for user {UserId} and product {ProductId}", userId, request.ProductId);
                }

                await _context.SaveChangesAsync();
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
        public async Task<IActionResult> DeleteReview(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                var review = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.ReviewId == id && r.UserId == userId);

                if (review == null)
                    return NotFound("Отзыв не найден");

                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();

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
        public async Task<ActionResult<CanReviewResponse>> CanUserReviewProduct(int productId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Check if user has purchased this product
                var orderItems = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi =>
                        oi.ProductId == productId &&
                        oi.Order.UserId == userId)
                    .ToListAsync(); // Execute query first

                // Filter in memory
                var hasPurchased = orderItems.Any(oi => IsOrderCompleted(oi.Order.Status));

                // Check if user already reviewed this product
                var hasReviewed = await _context.Reviews
                    .AnyAsync(r => r.UserId == userId && r.ProductId == productId);

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
        public async Task<ActionResult<ReviewCheckResponse>> CheckUserReviewForProduct(int productId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == productId);

                if (existingReview != null)
                {
                    return Ok(new ReviewCheckResponse
                    {
                        HasReview = true,
                        Review = new ReviewResponse
                        {
                            ReviewId = existingReview.ReviewId,
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

        private bool IsOrderCompleted(string status)
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

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return 0;

            return userId;
        }
    }

    public class ReviewResponse
    {
        public int ReviewId { get; set; }
        public string Username { get; set; }
        public int Rating { get; set; }
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ReviewDetailResponse
    {
        public int ReviewId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductImageUrl { get; set; }
        public int Rating { get; set; }
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateReviewRequest
    {
        public int ProductId { get; set; }
        public int Rating { get; set; }
        public string Text { get; set; }
    }

    public class CanReviewResponse
    {
        public bool CanReview { get; set; }
        public bool HasPurchased { get; set; }
        public bool HasReviewed { get; set; }
        public string Message { get; set; }
    }

    public class ReviewCheckResponse
    {
        public bool HasReview { get; set; }
        public ReviewResponse Review { get; set; }
    }
}