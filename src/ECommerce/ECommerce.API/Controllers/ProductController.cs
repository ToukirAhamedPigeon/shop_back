using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.ECommerce.Domain.Entities;
using shop_back.src.ECommerce.Application.DTOs;
using shop_back.src.ECommerce.Application.Services;
using shop_back.src.ECommerce.Application.Interfaces;

namespace shop_back.src.ECommerce.API.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductController(IProductService _productService) : ControllerBase
    {
        // POST: /api/products/get-all
        [HttpPost("get-all")]
        //[HasPermissionAny("admin:create-product", "admin:edit-product")]
        // [HasPermissionAll("admin:delete-product", "admin:hard-delete")]
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
