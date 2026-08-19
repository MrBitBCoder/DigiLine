using DigiLine.Application.Queue;
using DigiLine.Domain.Common;

namespace DigiLine.Application.Sessions;

public sealed record DoctorSessionRecord(
    Guid Id,
    Guid ClinicId,
    string ClinicName,
    Guid DoctorId,
    string DoctorName,
    string Name,
    DateTime BookingOpenTime,
    DateTime StartTime,
    DateTime EndTime,
    DateTime LastBookingTime,
    int AverageConsultationMinutes,
    SessionStatus Status,
    IReadOnlyList<QueueTokenListItem> Queue);

public sealed record DoctorQueueSummary(
    QueueTokenListItem? CurrentToken,
    int WaitingCount,
    int LateCount,
    int TotalCount);

public sealed record DoctorSessionSummary(
    Guid Id,
    Guid ClinicId,
    string ClinicName,
    Guid DoctorId,
    string DoctorName,
    string Name,
    DateTime BookingOpenTime,
    DateTime StartTime,
    DateTime EndTime,
    DateTime LastBookingTime,
    int AverageConsultationMinutes,
    SessionStatus Status,
    bool IsBookingOpen,
    DoctorQueueSummary QueueSummary);

public sealed record DoctorSessionDetails(
    DoctorSessionSummary Session,
    IReadOnlyList<QueueTokenListItem> Queue);
