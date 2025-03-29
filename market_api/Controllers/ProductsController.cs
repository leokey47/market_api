using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using market_api.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using market_api.Data;

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
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            var products = await _context.Products.ToListAsync();
            return Ok(products);
        }
    }
}
