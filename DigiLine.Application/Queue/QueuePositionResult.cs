using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed record QueuePositionResult(
    Guid TokenId,
    int TokenNumber,
    QueueTokenStatus Status,
    int Position,
    int PatientsAhead);
