using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using DigiLine.Application.Common.Interfaces;
using DigiLine.Domain.Entities;
using DigiLine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DigiLine.Infrastructure.Authentication;

public interface IStaffEmailSender
{
    Task SendResetLinkAsync(string email, string link, CancellationToken cancellationToken);
    Task SendInvitationLinkAsync(string email, string link, CancellationToken cancellationToken) => throw new NotSupportedException();
}

public sealed class SmtpStaffEmailSender(IConfiguration configuration) : IStaffEmailSender
{
    public Task SendInvitationLinkAsync(string email, string link, CancellationToken cancellationToken) =>
        SendAsync(email, "Join your DigiLine clinic", $"Set your password within 48 hours: {link}", cancellationToken);
    public Task SendResetLinkAsync(string email, string link, CancellationToken cancellationToken) =>
        SendAsync(email, "Reset your DigiLine password", $"Use this link within 30 minutes to reset your password: {link}\nIf you did not request this, ignore this email.", cancellationToken);
    private async Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"];
        var from = configuration["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("SMTP host and sender must be configured.");
        using var client = new SmtpClient(host, configuration.GetValue("Smtp:Port", 587))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(configuration["Smtp:Username"], configuration["Smtp:Password"])
        };
        using var message = new MailMessage(from, email, subject, body);
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}

public interface IStaffPasswordService
{
    Task ChangeAsync(Guid staffId, string currentPassword, string newPassword, CancellationToken cancellationToken);
    Task RequestResetAsync(string email, CancellationToken cancellationToken);
    Task ResetAsync(string token, string newPassword, CancellationToken cancellationToken);
}

public sealed class StaffPasswordService(DigiLineDbContext db, IPasswordHasher hasher,
    IStaffEmailSender sender, IConfiguration configuration, ILogger<StaffPasswordService>? logger = null) : IStaffPasswordService
{
    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 12 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(c => !char.IsLetterOrDigit(c)))
            throw new ArgumentException("Use at least 12 characters with uppercase, lowercase, number, and symbol.");
    }

    public async Task ChangeAsync(Guid staffId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        ValidatePassword(newPassword);
        var staff = await db.Staff.SingleOrDefaultAsync(x => x.Id == staffId, cancellationToken)
            ?? throw new UnauthorizedAccessException();
        if (!hasher.Verify(currentPassword, staff.PasswordHash))
            throw new ArgumentException("Current password is incorrect.");
        staff.ChangePassword(hasher.Hash(newPassword));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestResetAsync(string email, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["PasswordReset:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Host != "localhost"))
            throw new InvalidOperationException("Configure a valid HTTPS password-reset public base URL.");
        var normalized = email.Trim().ToUpperInvariant();
        var staff = await db.Staff.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, cancellationToken);
        if (staff is null)
            return;
        var now = DateTime.UtcNow;
        if (await db.PasswordResetTokens.AnyAsync(x => x.StaffId == staff.Id && x.CreatedAt > now.AddMinutes(-5), cancellationToken))
            return;
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var token = new PasswordResetToken(staff.Id, Hash(raw), now);
        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
        try
        { await sender.SendResetLinkAsync(staff.Email, $"{baseUrl.TrimEnd('/')}/reset-password#token={Uri.EscapeDataString(raw)}", cancellationToken); }
        catch (Exception error)
        {
            logger?.LogError(error, "Password reset delivery failed for staff {StaffId}", staff.Id);
            db.PasswordResetTokens.Remove(token);
            await db.SaveChangesAsync(cancellationToken);
            // Deliberately preserve the same outward response for known and unknown emails.
        }
    }

    public async Task ResetAsync(string raw, string newPassword, CancellationToken cancellationToken)
    {
        ValidatePassword(newPassword);
        var now = DateTime.UtcNow;
        var token = await db.PasswordResetTokens.SingleOrDefaultAsync(x => x.TokenHash == Hash(raw), cancellationToken);
        if (token is null || !token.IsValid(now))
            throw new ArgumentException("This reset link is invalid or expired.");
        token.Consume(now);
        var staff = await db.Staff.SingleAsync(x => x.Id == token.StaffId, cancellationToken);
        var otherTokens = await db.PasswordResetTokens.Where(x => x.StaffId == staff.Id && x.Id != token.Id && x.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var other in otherTokens)
            other.Invalidate(now);
        staff.ChangePassword(hasher.Hash(newPassword));
        try
        { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ArgumentException("This reset link is invalid or expired."); }
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
