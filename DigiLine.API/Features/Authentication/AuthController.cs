using DigiLine.Application.Common.Authentication;
using DigiLine.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using DigiLine.Infrastructure.Authentication;
using System.Security.Claims;

namespace DigiLine.API.Features.Authentication
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly IStaffPasswordService? _passwords;

        public AuthController(
            IAuthenticationService authenticationService, IStaffPasswordService? passwords = null)
        {
            _authenticationService = authenticationService;
            _passwords = passwords;
        }

        public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
        public sealed record ForgotPasswordRequest(string Email);
        public sealed record ResetPasswordRequest(string Token, string NewPassword, string ConfirmPassword);

        [Authorize(Roles = "Doctor,Receptionist,Admin")]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (!Guid.TryParse(subject, out var id)) return Unauthorized();
            if (request.NewPassword != request.ConfirmPassword) return BadRequest("Passwords do not match.");
            try { await _passwords!.ChangeAsync(id, request.CurrentPassword, request.NewPassword, cancellationToken); return NoContent(); }
            catch (ArgumentException error) { return BadRequest(error.Message); }
        }

        [EnableRateLimiting("password-reset")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.Email))
                await _passwords!.RequestResetAsync(request.Email, cancellationToken);
            return Ok(new { message = "If that email is registered, a reset link has been sent." });
        }

        [EnableRateLimiting("password-reset")]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token)) return BadRequest("This reset link is invalid or expired.");
            if (request.NewPassword != request.ConfirmPassword) return BadRequest("Passwords do not match.");
            try { await _passwords!.ResetAsync(request.Token, request.NewPassword, cancellationToken); return NoContent(); }
            catch (ArgumentException error) { return BadRequest(error.Message); }
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
