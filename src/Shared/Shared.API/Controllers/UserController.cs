using System.IdentityModel.Tokens.Jwt;
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
        private readonly IMailVerificationService _mailVerificationService;

        public UserController(IUserService service, IMailVerificationService mailVerificationService)
        {
            _service = service;
            _mailVerificationService = mailVerificationService;
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

        [HttpPost("create")]
        [HasPermissionAny("read-admin-dashboard")]
        public async Task<IActionResult> Create([FromForm] CreateUserRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var result = await _service.CreateUserAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            var result = await _mailVerificationService.VerifyTokenAsync(token);
            if (!result.Success) return BadRequest(result.Message);

            return Ok(result.Message);
        }
    }
}
