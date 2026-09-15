using DigiLine.Domain.Entities;
using DigiLine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigiLine.Infrastructure.Tests;

public sealed class SessionUtcMappingTests
{
    [Theory]
    [InlineData(nameof(Session.BookingOpenTime))]
    [InlineData(nameof(Session.StartTime))]
    [InlineData(nameof(Session.EndTime))]
    [InlineData(nameof(Session.LastBookingTime))]
    [InlineData(nameof(Session.CreatedAt))]
    [InlineData(nameof(Session.UpdatedAt))]
    public void SessionTimeReadFromDatabase_IsMarkedUtc(string propertyName)
    {
        using var db = new DigiLineDbContext(new DbContextOptionsBuilder<DigiLineDbContext>()
            .UseSqlServer("Server=localhost;Database=DigiLineDb;TrustServerCertificate=True")
            .Options);
        var converter = db.Model.FindEntityType(typeof(Session))!
            .FindProperty(propertyName)!.GetTypeMapping().Converter!;
        var stored = new DateTime(2026, 9, 13, 10, 30, 0, DateTimeKind.Unspecified);

        var restored = (DateTime)converter.ConvertFromProvider(stored)!;

        Assert.Equal(DateTimeKind.Utc, restored.Kind);
        Assert.Equal("2026-09-13T10:30:00.0000000Z", restored.ToString("O"));
    }

    [Theory]
    [InlineData(nameof(QueueToken.CreatedAt))]
    [InlineData(nameof(QueueToken.CalledAt))]
    [InlineData(nameof(QueueToken.LateArrivedAt))]
    [InlineData(nameof(QueueToken.StartedConsultationAt))]
    [InlineData(nameof(QueueToken.CompletedAt))]
    public void QueueTimeReadFromDatabase_IsMarkedUtc(string propertyName)
    {
        using var db = new DigiLineDbContext(new DbContextOptionsBuilder<DigiLineDbContext>()
            .UseSqlServer("Server=localhost;Database=DigiLineDb;TrustServerCertificate=True").Options);
        var converter = db.Model.FindEntityType(typeof(QueueToken))!
            .FindProperty(propertyName)!.GetTypeMapping().Converter!;
        var stored = new DateTime(2026, 9, 13, 10, 30, 0, DateTimeKind.Unspecified);
        var restored = converter.ConvertFromProvider(stored);
        Assert.Equal(DateTimeKind.Utc, ((DateTime)restored!).Kind);
    }
}
