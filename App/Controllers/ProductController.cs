using Microsoft.AspNetCore.Mvc;
using server.App.Models;
using server.App.DTOs;
using server.App.Services;

namespace server.App.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductController(IProductService _productService) : ControllerBase
    {
        // POST: /api/products/get-all
        [HttpPost("get-all")]
        public async Task<ActionResult<IEnumerable<Product>>> GetAllProducts([FromBody] ProductFilterDto filter)
        {
            var products = await _productService.GetFilteredAsync(filter);
            return Ok(products);
        }
        // public async Task<ActionResult<IEnumerable<Product>>> GetAllProducts()
        // {
        //     var products = await _productService.GetAllAsync();
        //     return Ok(products);
        // }
    }
}
