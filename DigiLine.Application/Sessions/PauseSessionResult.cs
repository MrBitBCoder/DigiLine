using DigiLine.Domain.Common;

namespace DigiLine.Application.Sessions;

public sealed record PauseSessionResult(
    Guid SessionId,
    SessionStatus Status);
