using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.PermissionGroups;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;

namespace shop_back.src.Shared.API.Controllers
{
    /// <summary>
    /// Permission groups: named bundles of permissions given to roles or users.
    /// Managing them uses the permission permissions; reading the list is also
    /// open to anyone who edits roles or users, since their forms pick groups.
    /// </summary>
    [ApiController]
    [Route("api/permission-groups")]
    public class PermissionGroupController : ControllerBase
    {
        private readonly IPermissionGroupService _service;

        public PermissionGroupController(IPermissionGroupService service)
        {
            _service = service;
        }

        private string? CurrentUserId =>
            User?.FindFirst("UserId")?.Value ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        [Authorize]
        [HttpGet]
        [HasPermissionAny("read-admin-permissions", "create-admin-roles", "update-admin-roles", "create-admin-users", "update-admin-users")]
        public async Task<IActionResult> GetGroups([FromQuery] string? search = null, [FromQuery] bool activeOnly = false)
        {
            return Ok(new { groups = await _service.GetGroupsAsync(search, activeOnly) });
        }

        [Authorize]
        [HttpGet("{id}")]
        [HasPermissionAny("read-admin-permissions", "update-admin-permissions")]
        public async Task<IActionResult> GetGroup(Guid id)
        {
            var group = await _service.GetGroupAsync(id);
            return group == null ? NotFound() : Ok(group);
        }

        [Authorize]
        [HttpPost("create")]
        [HasPermissionAny("create-admin-permissions")]
        public async Task<IActionResult> Create([FromBody] SavePermissionGroupRequest request)
        {
            var (success, message, id) = await _service.CreateGroupAsync(request, CurrentUserId);
            return success ? Ok(new { success, message, id }) : BadRequest(new { success, message });
        }

        [Authorize]
        [HttpPut("{id}")]
        [HasPermissionAny("update-admin-permissions")]
        public async Task<IActionResult> Update(Guid id, [FromBody] SavePermissionGroupRequest request)
        {
            var (success, message) = await _service.UpdateGroupAsync(id, request, CurrentUserId);
            return success ? Ok(new { success, message }) : BadRequest(new { success, message });
        }

        [Authorize]
        [HttpDelete("{id}")]
        [HasPermissionAny("delete-admin-permissions")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var (success, message) = await _service.DeleteGroupAsync(id, CurrentUserId);
            return success ? Ok(new { success, message }) : BadRequest(new { success, message });
        }
    }
}
