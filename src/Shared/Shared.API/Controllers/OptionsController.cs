using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using System.IdentityModel.Tokens.Jwt;
using shop_back.src.Shared.Application.DTOs.Options;

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
        [Authorize]
        [HttpPost("{type}")]
        [HasPermissionAny("read-admin-options")]
        public async Task<IActionResult> GetOptions(string type, [FromBody] SelectRequestDto? req = null)
        {
            req ??= new SelectRequestDto();
            var options = await _service.GetOptionsAsync(type, req);
            return Ok(options);
        }

        [Authorize]
        [HttpPost("list")]
        [HasPermissionAny("read-admin-options")]
        public async Task<IActionResult> GetOptions([FromBody] OptionFilterRequest request)
        {
            var result = await _service.GetOptionsAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Get single option by ID
        /// </summary>
        [Authorize]
        [HttpGet("{id}")]
        [HasPermissionAny("read-admin-options")]
        public async Task<IActionResult> GetOption(Guid id)
        {
            var option = await _service.GetOptionAsync(id);
            if (option == null) return NotFound();
            return Ok(option);
        }

        /// <summary>
        /// Get option for editing
        /// </summary>
        [Authorize]
        [HttpGet("{id}/edit")]
        [HasPermissionAny("update-admin-options")]
        public async Task<IActionResult> GetOptionForEdit(Guid id)
        {
            var option = await _service.GetOptionForEditAsync(id);
            if (option == null) return NotFound();
            return Ok(option);
        }

        /// <summary>
        /// Create new option(s)
        /// </summary>
        [Authorize]
        [HttpPost("create")]
        [HasPermissionAny("create-admin-options")]
        public async Task<IActionResult> Create([FromBody] CreateOptionRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var result = await _service.CreateOptionAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Update an option
        /// </summary>
        [Authorize]
        [HttpPut("{id}")]
        [HasPermissionAny("update-admin-options")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOptionRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var result = await _service.UpdateOptionAsync(id, request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Delete an option (soft or permanent)
        /// </summary>
        [Authorize]
        [HttpDelete("{id}")]
        [HasPermissionAny("delete-admin-options")]
        public async Task<IActionResult> DeleteOption(Guid id, [FromQuery] bool permanent = false)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.DeleteOptionAsync(id, permanent, currentUserId);
            
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            
            return Ok(new
            {
                message = result.Message,
                deleteType = result.DeleteType
            });
        }

        /// <summary>
        /// Restore a soft-deleted option
        /// </summary>
        [Authorize]
        [HttpPost("{id}/restore")]
        [HasPermissionAny("update-admin-options")]
        public async Task<IActionResult> RestoreOption(Guid id)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.RestoreOptionAsync(id, currentUserId);
            
            return result.Success 
                ? Ok(new { message = result.Message }) 
                : BadRequest(new { message = result.Message });
        }

        /// <summary>
        /// Check if an option can be permanently deleted
        /// </summary>
        [Authorize]
        [HttpGet("{id}/delete-info")]
        [HasPermissionAny("delete-admin-options")]
        public async Task<IActionResult> GetDeleteInfo(Guid id)
        {
            var result = await _service.CheckDeleteEligibilityAsync(id);
            return Ok(result);
        }

        /// <summary>
        /// Get parent options for dropdown (only those with has_child = true)
        /// </summary>
        [Authorize]
        [HttpPost("parents")]
        [HasPermissionAny("read-admin-options")]
        public async Task<IActionResult> GetParentOptions([FromBody] SelectRequestDto? req = null)
        {
            var options = await _service.GetParentOptionsAsync(req);
            return Ok(options);
        }
    }
}
