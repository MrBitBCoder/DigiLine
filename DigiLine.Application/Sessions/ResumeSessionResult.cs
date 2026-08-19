using DigiLine.Domain.Common;

namespace DigiLine.Application.Sessions;

public sealed record ResumeSessionResult(
    Guid SessionId,
    SessionStatus Status);
