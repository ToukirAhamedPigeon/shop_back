using Microsoft.AspNetCore.Mvc;

namespace server.App.Controllers
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
