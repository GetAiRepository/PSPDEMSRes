using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebApp.Controllers
{
    [Route("session")]
    public class SessionController : Controller
    {
        [Authorize]
        [HttpGet("check")]
        public IActionResult Check()
        {
            return Ok(); // If authenticated and valid token, returns 200
        }
    }
}