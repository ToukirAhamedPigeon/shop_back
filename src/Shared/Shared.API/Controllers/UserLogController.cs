using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.UserLogs;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.Common;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserLogController : ControllerBase
    {
        private readonly IUserLogService _service;

        public UserLogController(IUserLogService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> GetFiltered([FromBody] UserLogFilterRequest request)
        {
            var result = await _service.GetFilteredLogsAsync(request);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var log = await _service.GetLogAsync(id);
            if (log == null) return NotFound();
            
            var dto = new UserLogDto
            {
                Id = log.Id,
                Detail = log.Detail,
                Changes = log.Changes,
                ActionType = log.ActionType,
                ModelName = log.ModelName,
                ModelId = log.ModelId,
                CreatedBy = log.CreatedBy,
                CreatedAt = log.CreatedAt,
                IpAddress = log.IpAddress,
                Browser = log.Browser,
                Device = log.Device,
                OperatingSystem = log.OperatingSystem,
                UserAgent = log.UserAgent
            };

            return Ok(dto);
        }

        // 🔹 Collection Name Select
        [HttpPost("collections")]
        public async Task<IActionResult> GetCollections([FromBody] SelectRequestDto? req= null)
        {
            req ??= new SelectRequestDto();
            var result = await _service.GetCollectionsAsync(req);
            return Ok(result);
        }

        [HttpPost("action-types")]
        public async Task<IActionResult> GetActionTypes([FromBody] SelectRequestDto? req = null)
        {
            req ??= new SelectRequestDto();
            var result = await _service.GetActionTypesAsync(req);
            return Ok(result);
        }

        [HttpPost("creators")]
        public async Task<IActionResult> GetCreators([FromBody] SelectRequestDto? req = null)
        {
            req ??= new SelectRequestDto();
            var result = await _service.GetCreatorsAsync(req);
            return Ok(result);
        }
    }
}
