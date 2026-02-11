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

        [HttpPost("check-unique")]
        [HasPermissionAny("read-admin-dashboard")]
        public async Task<IActionResult> CheckUnique([FromBody] CheckUniqueRequest request)
        {
            var exists = await _service.ExistsAsync(request);
            return Ok(new { exists });
        }
    }
}
