namespace DigiLine.Application.Queue;

public enum QueueEventType
{
    TokenCreated = 1,
    TokenCalled = 2,
    TokenRecalled = 3,
    TokenSkipped = 4,
    TokenCompleted = 5,
    TokenCancelled = 6,
    TokenMarkedLate = 7,
    SessionPaused = 8,
    SessionResumed = 9
}
