using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Roles;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/roles")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _service;

        public RoleController(IRoleService service)
        {
            _service = service;
        }

        [Authorize]
        [HttpPost]
        [HasPermissionAny("read-admin-roles")]
        public async Task<IActionResult> GetRoles([FromBody] RolePermissionFilterRequest request)
        {
            var result = await _service.GetRolesAsync(request);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{id}")]
        [HasPermissionAny("read-admin-roles")]
        public async Task<IActionResult> GetRole(Guid id)
        {
            var role = await _service.GetRoleAsync(id);
            if (role == null) return NotFound();
            return Ok(role);
        }

        [Authorize]
        [HttpGet("{id}/edit")]
        [HasPermissionAny("update-admin-roles")]
        public async Task<IActionResult> GetRoleForEdit(Guid id)
        {
            var role = await _service.GetRoleForEditAsync(id);
            if (role == null) return NotFound();
            return Ok(role);
        }

        [Authorize]
        [HttpPost("create")]
        [HasPermissionAny("create-admin-roles")]
        public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var result = await _service.CreateRoleAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [Authorize]
        [HttpPut("{id}")]
        [HasPermissionAny("update-admin-roles")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var result = await _service.UpdateRoleAsync(id, request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [Authorize]
        [HttpDelete("{id}")]
        [HasPermissionAny("delete-admin-roles")]
        public async Task<IActionResult> DeleteRole(Guid id, [FromQuery] bool permanent = false)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.DeleteRoleAsync(id, permanent, currentUserId);
            
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
        [HasPermissionAny("restore-admin-roles")]
        public async Task<IActionResult> RestoreRole(Guid id)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.RestoreRoleAsync(id, currentUserId);
            
            return result.Success 
                ? Ok(new { message = result.Message }) 
                : BadRequest(new { message = result.Message });
        }

        [Authorize]
        [HttpGet("{id}/delete-info")]
        [HasPermissionAny("restore-admin-roles")]
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