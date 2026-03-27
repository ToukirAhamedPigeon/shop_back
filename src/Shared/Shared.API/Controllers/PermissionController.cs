using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Permissions;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/permissions")]
    public class PermissionController : ControllerBase
    {
        private readonly IPermissionService _service;

        public PermissionController(IPermissionService service)
        {
            _service = service;
        }

        [Authorize]
        [HttpPost]
        [HasPermissionAny("read-admin-permissions")]
        public async Task<IActionResult> GetPermissions([FromBody] RolePermissionFilterRequest request)
        {
            var result = await _service.GetPermissionsAsync(request);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{id}")]
        [HasPermissionAny("read-admin-permissions")]
        public async Task<IActionResult> GetPermission(Guid id)
        {
            var permission = await _service.GetPermissionAsync(id);
            if (permission == null) return NotFound();
            return Ok(permission);
        }

        [Authorize]
        [HttpGet("{id}/edit")]
        [HasPermissionAny("update-admin-permissions")]
        public async Task<IActionResult> GetPermissionForEdit(Guid id)
        {
            var permission = await _service.GetPermissionForEditAsync(id);
            if (permission == null) return NotFound();
            return Ok(permission);
        }

        [Authorize]
        [HttpPost("create")]
        [HasPermissionAny("create-admin-permissions")]
        public async Task<IActionResult> Create([FromBody] CreatePermissionRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var result = await _service.CreatePermissionAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [Authorize]
        [HttpPut("{id}")]
        [HasPermissionAny("update-admin-permissions")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePermissionRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var result = await _service.UpdatePermissionAsync(id, request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [Authorize]
        [HttpDelete("{id}")]
        [HasPermissionAny("delete-admin-permissions")]
        public async Task<IActionResult> DeletePermission(Guid id, [FromQuery] bool permanent = false)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.DeletePermissionAsync(id, permanent, currentUserId);
            
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            
            return Ok(new
            {
                message = result.Message,
                deleteType = result.DeleteType
            });
        }

        [Authorize]
        [HttpPost("{id}/restore")]
        [HasPermissionAny("restore-admin-permissions")]
        public async Task<IActionResult> RestorePermission(Guid id)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.RestorePermissionAsync(id, currentUserId);
            
            return result.Success 
                ? Ok(new { message = result.Message }) 
                : BadRequest(new { message = result.Message });
        }

        [Authorize]
        [HttpGet("{id}/delete-info")]
        [HasPermissionAny("restore-admin-permissions")]
        public async Task<IActionResult> GetDeleteInfo(Guid id)
        {
            var result = await _service.CheckDeleteEligibilityAsync(id);
            
            if (!result.Success)
                return NotFound(new { message = result.Message });
            
            return Ok(new
            {
                canBePermanent = result.CanBePermanent,
                message = result.Message
            });
        }
    }
}