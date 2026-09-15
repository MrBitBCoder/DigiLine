using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed record PublicBookingResult(
    Guid SessionId,
    Guid TokenId,
    int TokenNumber,
    bool WasAlreadyBooked);

public sealed record PublicTokenRecoveryResult(
    Guid SessionId,
    Guid TokenId,
    int TokenNumber);

public sealed record PublicTokenState(
    Guid SessionId,
    Guid TokenId,
    int TokenNumber,
    QueueTokenStatus Status,
    int? CurrentTokenNumber,
    int? Position,
    int? PatientsAhead,
    int? EstimatedWaitMinutes,
    bool IsPaused,
    SessionStatus SessionStatus,
    DateTime UpdatedAt);
