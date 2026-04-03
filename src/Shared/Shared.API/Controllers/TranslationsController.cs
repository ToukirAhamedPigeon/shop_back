using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Translations;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;

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
            
            // Check if user is developer (you may need to implement this check)
            // For now, we'll pass false; you can add a claim check for developer role
            bool isDeveloper = User?.IsInRole("Developer") ?? false;
            
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
    }
}
