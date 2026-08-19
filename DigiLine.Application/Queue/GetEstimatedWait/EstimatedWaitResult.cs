using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed record EstimatedWaitResult(
    Guid TokenId,
    int TokenNumber,
    QueueTokenStatus Status,
    int Position,
    int PatientsAhead,
    int AverageConsultationMinutes,
    bool IsPaused,
    int? EstimatedWaitMinutes);
