namespace DigiLine.Domain.Common;

public enum QueueTokenStatus
{
    Waiting = 1,
    Called = 2,
    InConsultation = 3,
    Completed = 4,
    Skipped = 5,
    Late = 6,
    Cancelled = 7,
    NoShow = 8
}
