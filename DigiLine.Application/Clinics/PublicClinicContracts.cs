using DigiLine.Domain.Common;

namespace DigiLine.Application.Clinics;

public sealed record PublicQueueTokenRecord(
    int TokenNumber,
    QueueTokenStatus Status,
    DateTime CreatedAt,
    DateTime? CalledAt);

public sealed record PublicSessionRecord(
    Guid Id,
    string Name,
    DateTime BookingOpenTime,
    DateTime StartTime,
    DateTime EndTime,
    DateTime LastBookingTime,
    SessionStatus Status,
    IReadOnlyList<PublicQueueTokenRecord> Queue);

public sealed record PublicClinicRecord(
    Guid Id,
    string Name,
    string Slug,
    IReadOnlyList<PublicSessionRecord> Sessions);

public sealed record PublicSessionSummary(
    Guid Id,
    string Name,
    DateTime BookingOpenTime,
    DateTime StartTime,
    DateTime EndTime,
    DateTime LastBookingTime,
    SessionStatus Status,
    bool IsBookingOpen,
    int? CurrentTokenNumber,
    int ApproximateQueueSize);

public sealed record PublicClinicDetails(
    string Name,
    string Slug,
    IReadOnlyList<PublicSessionSummary> Sessions);
