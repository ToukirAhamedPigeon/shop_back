using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Users;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _service;

        public UserController(IUserService service)
        {
            _service = service;
        }

        [HttpPost]
        [HasPermissionAny("read-admin-dashboard")]
        public async Task<IActionResult> GetUsers([FromBody] UserFilterRequest request)
        {
            var result = await _service.GetUsersAsync(request);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [HasPermissionAny("read-admin-dashboard")]
        public async Task<IActionResult> GetUser(Guid id)
        {
            var user = await _service.GetUserAsync(id);
            if (user == null) return NotFound();

            return Ok(user);
        }
    }
}
