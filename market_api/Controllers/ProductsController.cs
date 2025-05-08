using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly AppDbContext _context;

        public ProductController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Product
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProducts()
        {
            var products = await _context.Products
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

            return products;
        }

        // GET: api/Product/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetProduct(int id)
        {
            var product = await _context.Products
                .Include(p => p.Photos)
                .Include(p => p.Specifications)
                .Where(p => p.Id == id)
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
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound();
            }

            return product;
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

            Console.WriteLine($"Получен продукт: {productDto.Name}, {productDto.ImageUrl}");

            // Получаем ID текущего пользователя
            var userIdClaim = User.FindFirst("userId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Не удалось определить пользователя" });
            }

            // Проверяем пользователя
            var user = await _context.Users.FindAsync(userId);
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
                BusinessOwnerId = user.IsBusiness ? userId : null // Устанавливаем владельца только для бизнес-аккаунтов
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Добавляем фотографии (включая основное изображение как первое фото)
            var photos = new List<ProductPhoto> {
        new ProductPhoto {
            ImageUrl = productDto.ImageUrl,
            ProductId = product.Id,
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
                            ProductId = product.Id,
                            DisplayOrder = displayOrder++
                        });
                    }
                }
            }

            await _context.ProductPhotos.AddRangeAsync(photos);

            // Добавляем спецификации если есть
            if (productDto.Specifications != null && productDto.Specifications.Any())
            {
                var specs = productDto.Specifications
                    .Where(s => !string.IsNullOrEmpty(s.Name) && !string.IsNullOrEmpty(s.Value))
                    .Select(s => new ProductSpecification
                    {
                        Name = s.Name,
                        Value = s.Value,
                        ProductId = product.Id
                    });

                await _context.ProductSpecifications.AddRangeAsync(specs);
            }

            await _context.SaveChangesAsync();

            // Возвращаем созданный продукт со всеми связями
            var createdProduct = await _context.Products
                .Include(p => p.Photos)
                .Include(p => p.Specifications)
                .Where(p => p.Id == product.Id)
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.ImageUrl,
                    p.Category,
                    p.BusinessOwnerId,
                    BusinessOwner = p.BusinessOwner != null ? new
                    {
                        p.BusinessOwner.Username,
                        p.BusinessOwner.CompanyName
                    } : null,
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
                .FirstOrDefaultAsync();

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, createdProduct);
        }

        // PUT: api/Product/5
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> PutProduct(int id, [FromBody] ProductUpdateDto productDto)
        {
            if (id != productDto.Id)
            {
                return BadRequest();
            }

            // Get existing product with related data
            var product = await _context.Products
                .Include(p => p.Photos)
                .Include(p => p.Specifications)
                .FirstOrDefaultAsync(p => p.Id == id);

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

            // Handle photos
            // First, remove all existing photos
            _context.ProductPhotos.RemoveRange(product.Photos);

            // Add main photo as first
            var newPhotos = new List<ProductPhoto> {
                new ProductPhoto {
                    ImageUrl = productDto.ImageUrl,
                    ProductId = product.Id,
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
                            ProductId = product.Id,
                            DisplayOrder = displayOrder++
                        });
                    }
                }
            }

            await _context.ProductPhotos.AddRangeAsync(newPhotos);

            // Handle specifications
            // First, remove all existing specifications
            _context.ProductSpecifications.RemoveRange(product.Specifications);

            // Add new specifications
            if (productDto.Specifications != null && productDto.Specifications.Any())
            {
                var newSpecs = productDto.Specifications
                    .Where(s => !string.IsNullOrEmpty(s.Name) && !string.IsNullOrEmpty(s.Value))
                    .Select(s => new ProductSpecification
                    {
                        Name = s.Name,
                        Value = s.Value,
                        ProductId = product.Id
                    });

                await _context.ProductSpecifications.AddRangeAsync(newSpecs);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/Product/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Photos)
                    .Include(p => p.Specifications)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    return NotFound();
                }

                // Find any order items that reference this product
                var orderItems = await _context.OrderItems
                    .Where(oi => oi.ProductId == id)
                    .ToListAsync();

                if (orderItems.Any())
                {
                    // Get all affected orders
                    var orderIds = orderItems.Select(oi => oi.OrderId).Distinct().ToList();
                    var orders = await _context.Orders
                        .Where(o => orderIds.Contains(o.OrderId))
                        .ToListAsync();

                    // Remove order items first
                    _context.OrderItems.RemoveRange(orderItems);
                    await _context.SaveChangesAsync();

                    // For each order, check if it has any remaining items
                    foreach (var orderId in orderIds)
                    {
                        var remainingItems = await _context.OrderItems
                            .Where(oi => oi.OrderId == orderId)
                            .AnyAsync();

                        // If order has no items left, delete the order
                        if (!remainingItems)
                        {
                            var order = orders.FirstOrDefault(o => o.OrderId == orderId);
                            if (order != null)
                            {
                                _context.Orders.Remove(order);
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                // Now delete the product - photos and specifications will be deleted automatically
                // due to cascade delete configuration
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                // Log the exception
                return StatusCode(500, new { message = $"Internal error: {ex.Message}" });
            }
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }

    // DTOs for API operations
    public class ProductCreateDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public List<string> AdditionalPhotos { get; set; } = new List<string>();
        public List<SpecificationDto> Specifications { get; set; } = new List<SpecificationDto>();
    }

    public class ProductUpdateDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public List<string> AdditionalPhotos { get; set; } = new List<string>();
        public List<SpecificationDto> Specifications { get; set; } = new List<SpecificationDto>();
    }

    public class SpecificationDto
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}