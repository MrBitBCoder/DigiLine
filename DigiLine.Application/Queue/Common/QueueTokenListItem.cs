using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed record QueueTokenListItem(
    Guid Id,
    int TokenNumber,
    Guid? PatientProfileId,
    string? WalkInName,
    string? PhoneNumber,
    QueueTokenStatus Status,
    DateTime CreatedAt,
    DateTime? CalledAt = null,
    DateTime? CompletedAt = null);
