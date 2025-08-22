using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace shop_back.App.Controllers
{
    /// <summary>
    /// Issues CSRF tokens for the SPA frontend.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CsrfController : ControllerBase
    {
        private readonly IAntiforgery _antiforgery;

        public CsrfController(IAntiforgery antiforgery)
        {
            _antiforgery = antiforgery;
        }

        /// <summary>
        /// Generates a CSRF token and stores it in a cookie (HttpOnly + SameSite).
        /// The SPA should also receive the request token in response body to send in headers.
        /// </summary>
        [HttpGet("token")]
        public IActionResult GetCsrfToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

            return Ok(new { csrfToken = tokens.RequestToken });
        }
    }
}
