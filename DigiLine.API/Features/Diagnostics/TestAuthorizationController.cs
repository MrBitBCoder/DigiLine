using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Features.Diagnostics
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestAuthorizationController : ControllerBase
    {
        [HttpGet("authenticated")]
        [Authorize]
        public IActionResult Authenticated()
        {
            return Ok(new
            {
                Message = "Authenticated successfully."
            });
        }

        [HttpGet("doctor")]
        [Authorize(Roles = "Doctor")]
        public IActionResult Doctor()
        {
            return Ok(new
            {
                Message = "Doctor authorization successful."
            });
        }

        [HttpGet("receptionist")]
        [Authorize(Roles = "Receptionist")]
        public IActionResult Receptionist()
        {
            return Ok(new
            {
                Message = "Receptionist authorization successful."
            });
        }
    }
}
