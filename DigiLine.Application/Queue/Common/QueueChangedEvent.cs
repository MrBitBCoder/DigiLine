using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed record QueueChangedEvent(
    QueueEventType Type,
    Guid SessionId,
    Guid? TokenId,
    int? TokenNumber,
    QueueTokenStatus? TokenStatus,
    SessionStatus SessionStatus,
    DateTime OccurredAt);
