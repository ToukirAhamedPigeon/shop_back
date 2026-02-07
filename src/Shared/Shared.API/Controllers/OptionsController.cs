using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OptionsController : ControllerBase
    {
        private readonly IOptionsService _service;

        public OptionsController(IOptionsService service)
        {
            _service = service;
        }

        /// <summary>
        /// Generic select options endpoint.
        /// Example: type = "collections" | "actionTypes" | "creators"
        /// </summary>
        [HttpPost("{type}")]
        [HasPermissionAny("read-admin-dashboard")]
        public async Task<IActionResult> GetOptions(string type, [FromBody] SelectRequestDto? req = null)
        {
            req ??= new SelectRequestDto();
            var options = await _service.GetOptionsAsync(type, req);
            return Ok(options);
        }
    }
}
