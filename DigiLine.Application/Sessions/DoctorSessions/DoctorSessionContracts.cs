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
    IReadOnlyList<QueueTokenListItem> Queue,
    string TimeZoneId = "Asia/Kolkata");

public sealed record DoctorQueueSummary(
    QueueTokenListItem? CurrentToken,
    int WaitingCount,
    int LateCount,
    int TotalCount,
    int CompletedCount,
    int NotAttendedCount, int SkippedCount = 0, int UnfinishedCount = 0);

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
    DoctorQueueSummary QueueSummary,
    string TimeZoneId = "Asia/Kolkata", DateTime? BookingStoppedAt = null, DateTime? CompletedAt = null, bool BookingHasStopped = false);

public sealed record DoctorSessionDetails(
    DoctorSessionSummary Session,
    IReadOnlyList<QueueTokenListItem> Queue, int Page = 1, int PageSize = 10, int TotalCount = 0);
