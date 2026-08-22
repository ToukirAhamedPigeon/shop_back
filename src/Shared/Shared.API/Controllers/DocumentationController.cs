// src/Shared/Shared.API/Controllers/DocumentationController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Helpers;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using System.Security.Claims;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentationController : ControllerBase
    {
        private readonly IDocumentationService _documentationService;

        public DocumentationController(IDocumentationService documentationService)
        {
            _documentationService = documentationService;
        }

        private string? GetCurrentUserRole()
        {
            return User?.FindFirst(ClaimTypes.Role)?.Value
                   ?? User?.FindFirst("role")?.Value;
        }

        [HttpGet("developer/tree")]
        [HasPermissionAny("read-admin-doc-developer")]
        public async Task<IActionResult> GetDeveloperTree()
        {
            var tree = await _documentationService.GetDeveloperTreeAsync();
            return Ok(tree);
        }

        [HttpGet("developer/page")]
        [HasPermissionAny("read-admin-doc-developer")]
        public async Task<IActionResult> GetDeveloperPage([FromQuery] string? slug)
        {
            if (!DocPathResolver.IsValidSlug(slug))
                return BadRequest(new { message = "Invalid or missing slug." });

            var page = await _documentationService.GetDeveloperPageAsync(slug);
            if (page == null)
                return NotFound();

            return Ok(page);
        }

        [HttpGet("user-guide")]
        [HasPermissionAny("read-admin-doc-user-guide")]
        public async Task<IActionResult> GetUserGuide()
        {
            var role = GetCurrentUserRole();
            var guide = await _documentationService.GetUserGuideAsync(role);
            return Ok(guide);
        }

        [HttpGet("changelog")]
        [HasPermissionAny("read-admin-doc-developer")]
        public async Task<IActionResult> GetChangelog([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? repo = null)
        {
            var result = await _documentationService.GetChangelogAsync(page, pageSize, repo);
            return Ok(result);
        }
    }
}
