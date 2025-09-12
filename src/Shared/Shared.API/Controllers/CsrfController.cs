using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;


namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CsrfController : ControllerBase
    {
        private readonly IAntiforgery _antiforgery;

        public CsrfController(IAntiforgery antiforgery)
        {
            _antiforgery = antiforgery;
        }

        [HttpGet("token")]
        [AllowAnonymous]
        public IActionResult GetCsrfToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            return Ok(new { csrfToken = tokens.RequestToken });
        }
    }
}
