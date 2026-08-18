using DigiLine.Application.Common.Authentication;
using DigiLine.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationService _authenticationService;

        public AuthController(
            IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(
            LoginRequest request,
            CancellationToken cancellationToken)
        {
            var token = await _authenticationService.LoginAsync(
                request.Email,
                request.Password,
                cancellationToken);

            if (token is null)
            {
                return Unauthorized();
            }

            return Ok(new LoginResponse
            {
                Token = token
            });
        }
    }
}
