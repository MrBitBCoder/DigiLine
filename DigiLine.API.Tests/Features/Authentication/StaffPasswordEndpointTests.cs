using System.Security.Claims;
using DigiLine.API.Features.Authentication;
using DigiLine.Application.Common.Interfaces;
using DigiLine.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Tests;

public sealed class StaffPasswordEndpointTests
{
    [Fact]
    public async Task ChangePassword_UsesAuthenticatedStaffId()
    {
        var service = new StubPasswords();
        var id = Guid.NewGuid();
        var controller = new AuthController(new StubAuth(), service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, "Doctor")], "test"));

        var result = await controller.ChangePassword(new AuthController.ChangePasswordRequest(
            "old", "NewPassword123!", "NewPassword123!"), default);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(id, service.ChangedId);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsSameMessageForUnknownEmail()
    {
        var service = new StubPasswords();
        var controller = new AuthController(new StubAuth(), service);
        var result = await controller.ForgotPassword(new AuthController.ForgotPasswordRequest("unknown@example.test"), default);
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("unknown@example.test", service.RequestedEmail);
    }

    private sealed class StubAuth : IAuthenticationService
    {
        public Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
    private sealed class StubPasswords : IStaffPasswordService
    {
        public Guid ChangedId { get; private set; }
        public string? RequestedEmail { get; private set; }
        public Task ChangeAsync(Guid staffId, string currentPassword, string newPassword, CancellationToken cancellationToken)
        { ChangedId = staffId; return Task.CompletedTask; }
        public Task RequestResetAsync(string email, CancellationToken cancellationToken)
        { RequestedEmail = email; return Task.CompletedTask; }
        public Task ResetAsync(string token, string newPassword, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
