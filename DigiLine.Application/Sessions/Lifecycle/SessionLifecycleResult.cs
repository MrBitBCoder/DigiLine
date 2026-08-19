using DigiLine.Domain.Common;

namespace DigiLine.Application.Sessions;

public sealed record SessionLifecycleResult(Guid SessionId, SessionStatus Status);
