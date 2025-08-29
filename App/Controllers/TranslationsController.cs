using Microsoft.AspNetCore.Mvc;
using shop_back.App.Services;

namespace shop_back.App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranslationsController : ControllerBase
    {
        private readonly ITranslationService _service;
        public TranslationsController(ITranslationService service) => _service = service;

        // GET api/translations/get?lang=en&module=common
        [HttpGet("get")]
        public async Task<IActionResult> Get(
            [FromQuery] string lang = "en",
            [FromQuery] string? module = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(lang)) lang = "en";

            var translations = await _service.GetTranslationsAsync(lang, module, ct);
            return Ok(new { lang, module, translations });
        }
    }
}
