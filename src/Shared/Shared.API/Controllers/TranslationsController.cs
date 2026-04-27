using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Translations;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using System.Security.Claims;
using shop_back.src.Shared.Application.DTOs.Common;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranslationsController : ControllerBase
    {
        private readonly ITranslationService _service;
        public TranslationsController(ITranslationService service) => _service = service;

        /// <summary>
        /// Get translations for a given language and module.
        /// Pass forceFetch=true to bypass cache (e.g., on frontend language switch).
        /// </summary>
        /// <param name="lang">Language code, default 'en'</param>
        /// <param name="module">Optional module name, default 'common'</param>
        /// <param name="forceFetch">If true, fetch from DB even if cache exists</param>
        /// <param name="ct">Cancellation token</param>
        [HttpGet("get")]
        public async Task<IActionResult> Get(
            [FromQuery] string lang = "en",
            [FromQuery] string? module = null,
            [FromQuery] bool forceFetch = false,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(lang))
                lang = "en";

            var translations = await _service.GetTranslationsAsync(lang, module, forceFetch, ct);

            return Ok(new
            {
                lang,
                module,
                translations
            });
        }
        /// <summary>
        /// Get paginated list of translations with filtering
        /// </summary>
        [HttpPost("list")]
        [HasPermissionAny("read-admin-translations")]
        public async Task<IActionResult> GetTranslations([FromBody] TranslationFilterRequest request)
        {
            var result = await _service.GetTranslationsAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Get a single translation by ID
        /// </summary>
        [HttpGet("{id}")]
        [HasPermissionAny("read-admin-translations")]
        public async Task<IActionResult> GetTranslation(long id)
        {
            var translation = await _service.GetTranslationByIdAsync(id);
            if (translation == null)
                return NotFound(new { message = "Translation not found" });
            
            return Ok(translation);
        }

        /// <summary>
        /// Get translation for editing
        /// </summary>
        [HttpGet("{id}/edit")]
        [HasPermissionAny("update-admin-translations")]
        public async Task<IActionResult> GetTranslationForEdit(long id)
        {
            var translation = await _service.GetTranslationForEditAsync(id);
            if (translation == null)
                return NotFound(new { message = "Translation not found" });
            
            return Ok(translation);
        }

        /// <summary>
        /// Create a new translation
        /// </summary>
        [HttpPost("create")]
        [HasPermissionAny("create-admin-translations")]
        public async Task<IActionResult> CreateTranslation([FromBody] CreateTranslationRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.CreateTranslationAsync(request, currentUserId);
            
            return result.Success 
                ? Ok(new { message = result.Message })
                : BadRequest(new { message = result.Message });
        }

        /// <summary>
        /// Update an existing translation
        /// </summary>
        [HttpPut("{id}")]
        [HasPermissionAny("update-admin-translations")]
        public async Task<IActionResult> UpdateTranslation(long id, [FromBody] UpdateTranslationRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            // Check if user has Developer role - now roles are in the JWT
            bool isDeveloper = User?.HasClaim(ClaimTypes.Role, "Developer") ?? false;
            
            // Also check if not found
            if (!isDeveloper)
            {
                isDeveloper = User?.HasClaim("role", "Developer") ?? false;
            }
            
            // Also check case-insensitive
            if (!isDeveloper)
            {
                isDeveloper = User?.Claims
                    .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                    .Any(c => c.Value.Equals("Developer", StringComparison.OrdinalIgnoreCase)) ?? false;
            }
            
            var result = await _service.UpdateTranslationAsync(id, request, currentUserId, isDeveloper);
            
            return result.Success 
                ? Ok(new { message = result.Message })
                : BadRequest(new { message = result.Message });
        }

        /// <summary>
        /// Delete a translation
        /// </summary>
        [HttpDelete("{id}")]
        [HasPermissionAny("delete-admin-translations")]
        public async Task<IActionResult> DeleteTranslation(long id)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.DeleteTranslationAsync(id, currentUserId);
            
            return result.Success 
                ? Ok(new { message = result.Message })
                : BadRequest(new { message = result.Message });
        }

        /// <summary>
        /// Get distinct modules for filtering options
        /// </summary>
        [HttpGet("modules")]
        [HasPermissionAny("read-admin-translations")]
        public async Task<IActionResult> GetModules()
        {
            var modules = await _service.GetModulesForOptionsAsync();
            return Ok(modules);
        }
        /// <summary>
        /// Bulk delete translations
        /// </summary>
        [Authorize]
        [HttpPost("bulk-delete")]
        [HasPermissionAny("delete-admin-translations")]
        public async Task<IActionResult> BulkDelete([FromBody] BulkOperationRequest request)
        {
            // Convert string IDs to long for translations
            var ids = new List<long>();
            var invalidIds = new List<string>();
            
            foreach (var id in request.Ids)
            {
                if (long.TryParse(id, out var longId))
                {
                    ids.Add(longId);
                }
                else
                {
                    invalidIds.Add(id);
                }
            }
            
            if (invalidIds.Any())
            {
                return BadRequest(new 
                { 
                    success = false, 
                    message = $"Invalid ID format for IDs: {string.Join(", ", invalidIds)}" 
                });
            }
            
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.BulkDeleteTranslationsAsync(ids, currentUserId);
            
            if (!result.Success)
                return BadRequest(result);
            
            return Ok(result);
        }
    }
}
