// src/Shared/API/Controllers/MailTemplateController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Mails;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using System.IdentityModel.Tokens.Jwt;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MailTemplateController : ControllerBase
    {
        private readonly IMailService _mailService;

        public MailTemplateController(IMailService mailService)
        {
            _mailService = mailService;
        }

        private Guid GetCurrentUserId()
        {
            var userId = User?.FindFirst("UserId")?.Value 
                         ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.Parse(userId!);
        }

        [HttpGet]
        [HasPermissionAny("read-admin-mail-templates")]
        public async Task<IActionResult> GetTemplates(
            [FromQuery] string? q,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10,
            [FromQuery] bool includeGlobal = true)
        {
            var userId = GetCurrentUserId();
            var (items, totalCount) = await _mailService.GetTemplatesAsync(q, page, limit, includeGlobal, userId);
            return Ok(new { templates = items, totalCount });
        }

        [HttpGet("{id}")]
        [HasPermissionAny("read-admin-mail-templates")]
        public async Task<IActionResult> GetTemplate(long id)
        {
            var template = await _mailService.GetTemplateAsync(id);
            if (template == null)
                return NotFound();

            return Ok(template);
        }

        [HttpPost]
        [HasPermissionAny("create-admin-mail-templates")]
        public async Task<IActionResult> CreateTemplate([FromBody] MailTemplateRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var template = await _mailService.CreateTemplateAsync(request, userId);
                return Ok(new { success = true, template });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [HasPermissionAny("update-admin-mail-templates")]
        public async Task<IActionResult> UpdateTemplate(long id, [FromBody] MailTemplateRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var template = await _mailService.UpdateTemplateAsync(id, request, userId);
                return Ok(new { success = true, template });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Template not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [HasPermissionAny("delete-admin-mail-templates")]
        public async Task<IActionResult> DeleteTemplate(long id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _mailService.DeleteTemplateAsync(id, userId);
                return Ok(new { success = true });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Template not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}