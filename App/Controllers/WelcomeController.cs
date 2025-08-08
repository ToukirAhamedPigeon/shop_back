using Microsoft.AspNetCore.Mvc;

namespace shop_back.App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WelcomeController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetWelcomeMessage()
        {
            return Ok("Welcome to Smart Shop Management in aws server!");
        }
    }
}
