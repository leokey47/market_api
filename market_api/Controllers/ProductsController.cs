using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using market_api.Data;
using market_api.Models;
using Microsoft.AspNetCore.Authorization;

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
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<object>> PostProduct([FromBody] ProductCreateDto productDto)
        {
            if (string.IsNullOrEmpty(productDto.ImageUrl))
            {
                return BadRequest(new { message = "Ошибка: изображение не загружено." });
            }

            Console.WriteLine($"Получен продукт: {productDto.Name}, {productDto.ImageUrl}");

            // Create the product
            var product = new Product
            {
                Name = productDto.Name,
                Description = productDto.Description,
                Price = productDto.Price,
                ImageUrl = productDto.ImageUrl, // Store main image for backward compatibility
                Category = productDto.Category
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Now add photos (including the main image as the first photo)
            var photos = new List<ProductPhoto> {
                new ProductPhoto {
                    ImageUrl = productDto.ImageUrl,
                    ProductId = product.Id,
                    DisplayOrder = 1
                }
            };

            // Add additional photos if provided
            if (productDto.AdditionalPhotos != null && productDto.AdditionalPhotos.Any())
            {
                int displayOrder = 2;
                foreach (var photoUrl in productDto.AdditionalPhotos.Take(4)) // Limit to 4 additional photos (5 total)
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

            // Add specifications if provided
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

            // Return the created product with all relations
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
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
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