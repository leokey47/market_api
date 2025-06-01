using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using market_api.Data;
using market_api.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace market_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly MongoDbContext _context;

        public ProductController(MongoDbContext context)
        {
            _context = context;
        }

        // GET: api/Product
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProducts()
        {
            var products = await _context.Products.Find(_ => true).ToListAsync();
            var response = new List<object>();

            foreach (var product in products)
            {
                var photos = await _context.ProductPhotos
                    .Find(pp => pp.ProductId == product.Id)
                    .SortBy(pp => pp.DisplayOrder)
                    .ToListAsync();

                var specifications = await _context.ProductSpecifications
                    .Find(ps => ps.ProductId == product.Id)
                    .ToListAsync();

                response.Add(new
                {
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Price,
                    product.ImageUrl,
                    product.Category,
                    Photos = photos.Select(ph => new {
                        ph.Id,
                        ph.ImageUrl,
                        ph.DisplayOrder
                    }),
                    Specifications = specifications.Select(s => new {
                        s.Id,
                        s.Name,
                        s.Value
                    })
                });
            }

            return response;
        }

        // GET: api/Product/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetProduct(string id)
        {
            var product = await _context.Products.Find(p => p.Id == id).FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound();
            }

            var photos = await _context.ProductPhotos
                .Find(pp => pp.ProductId == product.Id)
                .SortBy(pp => pp.DisplayOrder)
                .ToListAsync();

            var specifications = await _context.ProductSpecifications
                .Find(ps => ps.ProductId == product.Id)
                .ToListAsync();

            var response = new
            {
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.ImageUrl,
                product.Category,
                Photos = photos.Select(ph => new {
                    ph.Id,
                    ph.ImageUrl,
                    ph.DisplayOrder
                }),
                Specifications = specifications.Select(s => new {
                    s.Id,
                    s.Name,
                    s.Value
                })
            };

            return response;
        }

        // POST: api/Product
        [HttpPost]
        [Authorize] // Требует аутентификации (администраторы и бизнес-аккаунты)
        public async Task<ActionResult<object>> PostProduct([FromBody] ProductCreateDto productDto)
        {
            if (string.IsNullOrEmpty(productDto.ImageUrl))
            {
                return BadRequest(new { message = "Ошибка: изображение не загружено." });
            }

            // Получаем ID текущего пользователя
            var userIdClaim = User.FindFirst("userId");
            if (userIdClaim == null || string.IsNullOrEmpty(userIdClaim.Value))
            {
                return Unauthorized(new { message = "Не удалось определить пользователя" });
            }

            var userId = userIdClaim.Value;

            // Проверяем пользователя
            var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { message = "Пользователь не найден" });
            }

            // Проверяем права: админ или владелец бизнес-аккаунта
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userRole != "admin" && !user.IsBusiness)
            {
                return Forbid();
            }

            // Создаем продукт
            var product = new Product
            {
                Name = productDto.Name,
                Description = productDto.Description,
                Price = productDto.Price,
                ImageUrl = productDto.ImageUrl, // Основное изображение для обратной совместимости
                Category = productDto.Category,
                BusinessOwnerId = user.IsBusiness ? userId : null, // Устанавливаем владельца только для бизнес-аккаунтов
                CreatedAt = DateTime.UtcNow
            };

            await _context.Products.InsertOneAsync(product);

            // Добавляем фотографии (включая основное изображение как первое фото)
            var photos = new List<ProductPhoto> {
                new ProductPhoto {
                    ImageUrl = productDto.ImageUrl,
                    ProductId = product.Id!,
                    DisplayOrder = 1
                }
            };

            // Добавляем дополнительные фотографии если они есть
            if (productDto.AdditionalPhotos != null && productDto.AdditionalPhotos.Any())
            {
                int displayOrder = 2;
                foreach (var photoUrl in productDto.AdditionalPhotos.Take(4)) // Ограничиваем 4 дополнительными фото (5 всего)
                {
                    if (!string.IsNullOrEmpty(photoUrl))
                    {
                        photos.Add(new ProductPhoto
                        {
                            ImageUrl = photoUrl,
                            ProductId = product.Id!,
                            DisplayOrder = displayOrder++
                        });
                    }
                }
            }

            if (photos.Count > 0)
            {
                await _context.ProductPhotos.InsertManyAsync(photos);
            }

            // Добавляем спецификации если есть
            if (productDto.Specifications != null && productDto.Specifications.Any())
            {
                var specs = productDto.Specifications
                    .Where(s => !string.IsNullOrEmpty(s.Name) && !string.IsNullOrEmpty(s.Value))
                    .Select(s => new ProductSpecification
                    {
                        Name = s.Name,
                        Value = s.Value,
                        ProductId = product.Id!
                    });

                if (specs.Any())
                {
                    await _context.ProductSpecifications.InsertManyAsync(specs);
                }
            }

            // Возвращаем созданный продукт со всеми связями
            var createdPhotos = await _context.ProductPhotos
                .Find(pp => pp.ProductId == product.Id)
                .SortBy(pp => pp.DisplayOrder)
                .ToListAsync();

            var createdSpecs = await _context.ProductSpecifications
                .Find(ps => ps.ProductId == product.Id)
                .ToListAsync();

            var createdProduct = new
            {
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.ImageUrl,
                product.Category,
                product.BusinessOwnerId,
                BusinessOwner = product.BusinessOwnerId != null ? new
                {
                    user.Username,
                    user.CompanyName
                } : null,
                Photos = createdPhotos.Select(ph => new {
                    ph.Id,
                    ph.ImageUrl,
                    ph.DisplayOrder
                }),
                Specifications = createdSpecs.Select(s => new {
                    s.Id,
                    s.Name,
                    s.Value
                })
            };

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, createdProduct);
        }

        // PUT: api/Product/5
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> PutProduct(string id, [FromBody] ProductUpdateDto productDto)
        {
            if (id != productDto.Id)
            {
                return BadRequest();
            }

            // Get existing product
            var product = await _context.Products.Find(p => p.Id == id).FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound();
            }

            // Update product properties
            product.Name = productDto.Name;
            product.Description = productDto.Description;
            product.Price = productDto.Price;
            product.Category = productDto.Category;
            product.ImageUrl = productDto.ImageUrl; // Main image
            product.UpdatedAt = DateTime.UtcNow;

            // Handle photos
            // First, remove all existing photos
            await _context.ProductPhotos.DeleteManyAsync(pp => pp.ProductId == id);

            // Add main photo as first
            var newPhotos = new List<ProductPhoto> {
                new ProductPhoto {
                    ImageUrl = productDto.ImageUrl,
                    ProductId = id,
                    DisplayOrder = 1
                }
            };

            // Add additional photos
            if (productDto.AdditionalPhotos != null && productDto.AdditionalPhotos.Any())
            {
                int displayOrder = 2;
                foreach (var photoUrl in productDto.AdditionalPhotos.Take(4)) // Limit to 4 additional photos (5 total)
                {
                    if (!string.IsNullOrEmpty(photoUrl))
                    {
                        newPhotos.Add(new ProductPhoto
                        {
                            ImageUrl = photoUrl,
                            ProductId = id,
                            DisplayOrder = displayOrder++
                        });
                    }
                }
            }

            if (newPhotos.Count > 0)
            {
                await _context.ProductPhotos.InsertManyAsync(newPhotos);
            }

            // Handle specifications
            // First, remove all existing specifications
            await _context.ProductSpecifications.DeleteManyAsync(ps => ps.ProductId == id);

            // Add new specifications
            if (productDto.Specifications != null && productDto.Specifications.Any())
            {
                var newSpecs = productDto.Specifications
                    .Where(s => !string.IsNullOrEmpty(s.Name) && !string.IsNullOrEmpty(s.Value))
                    .Select(s => new ProductSpecification
                    {
                        Name = s.Name,
                        Value = s.Value,
                        ProductId = id
                    });

                if (newSpecs.Any())
                {
                    await _context.ProductSpecifications.InsertManyAsync(newSpecs);
                }
            }

            // Update the product
            var result = await _context.Products.ReplaceOneAsync(p => p.Id == id, product);

            if (result.ModifiedCount == 0)
            {
                return NotFound();
            }

            return NoContent();
        }

        // DELETE: api/Product/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteProduct(string id)
        {
            try
            {
                var product = await _context.Products.Find(p => p.Id == id).FirstOrDefaultAsync();

                if (product == null)
                {
                    return NotFound();
                }

                // Find any order items that reference this product
                var orderItems = await _context.OrderItems
                    .Find(oi => oi.ProductId == id)
                    .ToListAsync();

                if (orderItems.Any())
                {
                    // Get all affected orders
                    var orderIds = orderItems.Select(oi => oi.OrderId).Distinct().ToList();
                    var orders = await _context.Orders
                        .Find(o => orderIds.Contains(o.Id!))
                        .ToListAsync();

                    // Remove order items first
                    await _context.OrderItems.DeleteManyAsync(oi => oi.ProductId == id);

                    // For each order, check if it has any remaining items
                    foreach (var orderId in orderIds)
                    {
                        var remainingItems = await _context.OrderItems
                            .Find(oi => oi.OrderId == orderId)
                            .FirstOrDefaultAsync();

                        // If order has no items left, delete the order
                        if (remainingItems == null)
                        {
                            await _context.Orders.DeleteOneAsync(o => o.Id == orderId);
                        }
                    }
                }

                // Delete cart items that reference this product
                await _context.CartItems.DeleteManyAsync(ci => ci.ProductId == id);

                // Delete wishlist items that reference this product
                await _context.WishlistItems.DeleteManyAsync(wi => wi.ProductId == id);

                // Delete reviews for this product
                await _context.Reviews.DeleteManyAsync(r => r.ProductId == id);

                // Delete product photos and specifications
                await _context.ProductPhotos.DeleteManyAsync(pp => pp.ProductId == id);
                await _context.ProductSpecifications.DeleteManyAsync(ps => ps.ProductId == id);

                // Now delete the product
                await _context.Products.DeleteOneAsync(p => p.Id == id);

                return NoContent();
            }
            catch (Exception ex)
            {
                // Log the exception
                return StatusCode(500, new { message = $"Internal error: {ex.Message}" });
            }
        }

        private async Task<bool> ProductExistsAsync(string id)
        {
            var product = await _context.Products.Find(p => p.Id == id).FirstOrDefaultAsync();
            return product != null;
        }
    }

    // DTOs for API operations
    public class ProductCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> AdditionalPhotos { get; set; } = new List<string>();
        public List<SpecificationDto> Specifications { get; set; } = new List<SpecificationDto>();
    }

    public class ProductUpdateDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> AdditionalPhotos { get; set; } = new List<string>();
        public List<SpecificationDto> Specifications { get; set; } = new List<SpecificationDto>();
    }

    public class SpecificationDto
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}