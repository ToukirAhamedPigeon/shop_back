using Microsoft.AspNetCore.Mvc;
using shop_back.App.Models;
using shop_back.App.DTOs;
using shop_back.App.Services;

namespace shop_back.App.Controllers
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
