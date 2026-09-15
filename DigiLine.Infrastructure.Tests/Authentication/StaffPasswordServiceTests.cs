using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;
using DigiLine.Infrastructure.Authentication;
using DigiLine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace DigiLine.Infrastructure.Tests;

public sealed class StaffPasswordServiceTests
{
    private static (DigiLineDbContext Db, Staff Staff, StaffPasswordService Service, CaptureSender Sender, PasswordHasher Hasher) Create()
    {
        var db = new DigiLineDbContext(new DbContextOptionsBuilder<DigiLineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var hasher = new PasswordHasher();
        var clinic = new Clinic("Clinic", "clinic");
        var staff = new Staff(clinic.Id, "Doctor", "Doctor@Example.test", hasher.Hash("OldPassword123!"), StaffRole.Doctor);
        db.AddRange(clinic, staff); db.SaveChanges();
        var sender = new CaptureSender();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["PasswordReset:PublicBaseUrl"] = "https://example.test" }).Build();
        return (db, staff, new StaffPasswordService(db, hasher, sender, settings), sender, hasher);
    }

    [Fact]
    public void StaffEmail_IsNormalizedForEmailOnlyLogin()
    {
        var staff = new Staff(Guid.NewGuid(), "Doctor", " Doctor@Example.test ", "hash", StaffRole.Doctor);
        Assert.Equal("Doctor@Example.test", staff.Email);
        Assert.Equal("DOCTOR@EXAMPLE.TEST", staff.NormalizedEmail);
    }

    [Fact]
    public async Task Change_RequiresCurrentPasswordAndRevokesOldVersion()
    {
        var fixture = Create();
        await using var db = fixture.Db;
        var oldVersion = fixture.Staff.PasswordVersion;
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.ChangeAsync(fixture.Staff.Id,
            "wrong", "NewPassword123!", default));
        Assert.Equal(oldVersion, fixture.Staff.PasswordVersion);
        await fixture.Service.ChangeAsync(fixture.Staff.Id, "OldPassword123!", "NewPassword123!", default);
        Assert.NotEqual(oldVersion, fixture.Staff.PasswordVersion);
        Assert.True(fixture.Hasher.Verify("NewPassword123!", fixture.Staff.PasswordHash));
    }

    [Fact]
    public async Task Reset_IsHashedExpiringSingleUseAndNormalizesEmail()
    {
        var fixture = Create();
        await using var db = fixture.Db;
        await fixture.Service.RequestResetAsync(" doctor@example.TEST ", default);
        var link = Assert.Single(fixture.Sender.Links);
        var raw = new Uri(link).Fragment[7..];
        var token = Assert.Single(db.PasswordResetTokens);
        Assert.DoesNotContain(raw, token.TokenHash);
        var oldVersion = fixture.Staff.PasswordVersion;
        await fixture.Service.ResetAsync(raw, "Replacement123!", default);
        Assert.NotEqual(oldVersion, fixture.Staff.PasswordVersion);
        Assert.True(fixture.Hasher.Verify("Replacement123!", fixture.Staff.PasswordHash));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.ResetAsync(raw, "AnotherPass123!", default));
        await fixture.Service.RequestResetAsync("absent@example.test", default);
        Assert.Single(fixture.Sender.Links);
    }

    [Fact]
    public void WeakPassword_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => StaffPasswordService.ValidatePassword("short"));
    }

    [Fact]
    public async Task ExpiredResetToken_IsRejected()
    {
        var fixture = Create();
        await using var db = fixture.Db;
        const string raw = "expired-token";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        db.PasswordResetTokens.Add(new PasswordResetToken(fixture.Staff.Id, hash, DateTime.UtcNow.AddHours(-1)));
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.ResetAsync(raw, "Replacement123!", default));
    }

    private sealed class CaptureSender : IStaffEmailSender
    {
        public List<string> Links { get; } = [];
        public Task SendResetLinkAsync(string email, string link, CancellationToken cancellationToken)
        { Links.Add(link); return Task.CompletedTask; }
    }
}
