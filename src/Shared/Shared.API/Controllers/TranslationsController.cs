using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.Interfaces;
using shop_back.src.Shared.Application.Services;

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
    }
}
