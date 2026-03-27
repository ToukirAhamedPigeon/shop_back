using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/common")]
    public class CommonController : ControllerBase
    {
        private readonly IUniqueCheckService _service;

        public CommonController(IUniqueCheckService service)
        {
            _service = service;
        }

        /// <summary>
        /// Check if a value is unique in a specified field.
        /// </summary>
        [Authorize]
        [HttpPost("check-unique")]
        [HasPermissionAny("check-admin-unique")]
        public async Task<IActionResult> CheckUnique([FromBody] CheckUniqueRequest request)
        {
            var exists = await _service.ExistsAsync(request);
            return Ok(new { exists });
        }
    }
}
