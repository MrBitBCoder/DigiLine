using DigiLine.Application.Administration;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace DigiLine.Infrastructure.Persistence.Repositories;

public sealed class StaffSessionQueries(DigiLineDbContext db) : IStaffSessionQueries
{
    private IQueryable<Session> Scoped(Guid clinic, Guid? doctor) => db.Sessions.AsNoTracking().Where(x => x.ClinicId == clinic && (!doctor.HasValue || x.DoctorId == doctor));
    public Task<bool> OwnsAsync(Guid clinic, Guid? doctor, Guid id, CancellationToken ct) => Scoped(clinic, doctor).AnyAsync(x => x.Id == id, ct);
    private IQueryable<DoctorSessionSummary> Summaries(IQueryable<Session> query)
    {
        var now = DateTime.UtcNow;
        return query.Select(s => new DoctorSessionSummary(s.Id, s.ClinicId, s.Clinic.Name, s.DoctorId, s.Doctor.Name, s.Name,
            s.BookingOpenTime, s.StartTime, s.EndTime, s.LastBookingTime, s.AverageConsultationMinutes, s.Status,
            s.BookingStoppedAt == null && (s.Status == SessionStatus.BookingOpen || s.Status == SessionStatus.Running || s.Status == SessionStatus.Paused) && now >= s.BookingOpenTime && now < s.LastBookingTime && now < s.EndTime,
            new DoctorQueueSummary(
                null,
                s.QueueTokens.Count(t => t.Status == QueueTokenStatus.Waiting), s.QueueTokens.Count(t => t.Status == QueueTokenStatus.Late),
                s.QueueTokens.Count(), s.QueueTokens.Count(t => t.Status == QueueTokenStatus.Completed),
                s.QueueTokens.Count(t => t.Status == QueueTokenStatus.NoShow || t.Status == QueueTokenStatus.Skipped),
                s.QueueTokens.Count(t => t.Status == QueueTokenStatus.Skipped),
                s.QueueTokens.Count(t => t.Status == QueueTokenStatus.Waiting || t.Status == QueueTokenStatus.Called || t.Status == QueueTokenStatus.InConsultation || t.Status == QueueTokenStatus.Late)),
            s.Clinic.TimeZoneId, s.BookingStoppedAt, s.CompletedAt, s.BookingStoppedAt != null || now >= s.LastBookingTime || now >= s.EndTime));
    }
    private async Task<List<DoctorSessionSummary>> WithCurrentTokens(IQueryable<DoctorSessionSummary> query, CancellationToken ct)
    {
        var summaries = await query.ToListAsync(ct);
        var ids = summaries.Where(x => x.Status != SessionStatus.Closed).Select(x => x.Id).ToArray();
        if (ids.Length == 0)
            return summaries;
        // Scope before ranking: EF's navigation FirstOrDefault otherwise ranks calls across the entire token table.
        var calls = await db.QueueTokens.AsNoTracking().Where(t => ids.Contains(t.SessionId) &&
                (t.Status == QueueTokenStatus.Called || t.Status == QueueTokenStatus.InConsultation))
            .OrderByDescending(t => t.CalledAt ?? t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => new
            {
                t.SessionId,
                Token = new QueueTokenListItem(t.Id, t.TokenNumber, t.PatientProfileId, t.WalkInName,
                t.PatientProfile != null ? t.PatientProfile.PhoneAccount.PhoneNumber : t.PhoneNumber, t.Status, t.CreatedAt, t.CalledAt, t.CompletedAt,
                t.PatientProfile != null ? t.PatientProfile.Name : t.WalkInName, t.PatientProfile != null ? t.PatientProfile.Age : t.WalkInAge, t.LateArrivedAt, t.QueueOrder, t.FinalizedAt)
            }).ToListAsync(ct);
        return summaries.Select(s => s with { QueueSummary = s.QueueSummary with { CurrentToken = calls.FirstOrDefault(t => t.SessionId == s.Id)?.Token } }).ToList();
    }

    public async Task<PageResult<DoctorSessionSummary>> ListAsync(Guid clinic, Guid? doctor, SessionPageQuery request, CancellationToken ct)
    {
        var query = Scoped(clinic, doctor);
        if (!string.IsNullOrWhiteSpace(request.Name))
            query = query.Where(x => x.Name.Contains(request.Name.Trim()));
        if (request.Status.HasValue)
        { if (!Enum.IsDefined(request.Status.Value)) throw new ArgumentException("Invalid session status."); query = query.Where(x => x.Status == request.Status); }
        if (request.FromDate > request.ToDate)
            throw new ArgumentException("End date must be on or after start date.");
        if (request.FromDate.HasValue || request.ToDate.HasValue)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(await db.Clinics.Where(x => x.Id == clinic).Select(x => x.TimeZoneId).SingleAsync(ct));
            if (request.FromDate.HasValue)
            { var from = TimeZoneInfo.ConvertTimeToUtc(request.FromDate.Value.ToDateTime(TimeOnly.MinValue), zone); query = query.Where(x => x.StartTime >= from); }
            if (request.ToDate.HasValue)
            { var to = TimeZoneInfo.ConvertTimeToUtc(request.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), zone); query = query.Where(x => x.StartTime < to); }
        }
        var total = await query.CountAsync(ct);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Clamp(request.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)size)));
        var items = await WithCurrentTokens(Summaries(query.OrderByDescending(x => x.StartTime).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)), ct);
        return new(items, page, size, total);
    }
    public async Task<DoctorSessionDetails?> DetailsAsync(Guid clinic, Guid? doctor, Guid id, int page, int pageSize, string filter, CancellationToken ct)
    {
        var summary = (await WithCurrentTokens(Summaries(Scoped(clinic, doctor).Where(x => x.Id == id)), ct)).SingleOrDefault();
        if (summary is null)
            return null;
        var query = db.QueueTokens.AsNoTracking().Where(x => x.SessionId == id);
        query = filter switch
        {
            "Active" => query.Where(x => x.Status == QueueTokenStatus.Called || x.Status == QueueTokenStatus.InConsultation || x.Status == QueueTokenStatus.Waiting || x.Status == QueueTokenStatus.Late || x.Status == QueueTokenStatus.Skipped),
            "Completed" => query.Where(x => x.Status == QueueTokenStatus.Completed),
            "Other finished" => query.Where(x => x.Status == QueueTokenStatus.Cancelled || x.Status == QueueTokenStatus.NoShow),
            _ => throw new ArgumentException("Invalid queue filter.")
        };
        var total = await query.CountAsync(ct);
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var ordered = filter == "Completed"
            ? query.OrderByDescending(x => x.CompletedAt ?? x.CreatedAt).ThenByDescending(x => x.TokenNumber).ThenBy(x => x.Id)
            : query.OrderBy(x => x.Status == QueueTokenStatus.Called || x.Status == QueueTokenStatus.InConsultation ? 0 : x.Status == QueueTokenStatus.Waiting || x.Status == QueueTokenStatus.Late ? 1 : x.Status == QueueTokenStatus.Skipped ? 2 : 3)
                .ThenByDescending(x => x.Status == QueueTokenStatus.Called || x.Status == QueueTokenStatus.InConsultation ? x.CalledAt ?? x.CreatedAt : new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                .ThenBy(x => x.QueueOrder).ThenBy(x => x.TokenNumber).ThenBy(x => x.Id);
        var tokens = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(t => new QueueTokenListItem(t.Id, t.TokenNumber, t.PatientProfileId, t.WalkInName,
            t.PatientProfile != null ? t.PatientProfile.PhoneAccount.PhoneNumber : t.PhoneNumber, t.Status, t.CreatedAt, t.CalledAt, t.CompletedAt,
            t.PatientProfile != null ? t.PatientProfile.Name : t.WalkInName, t.PatientProfile != null ? t.PatientProfile.Age : t.WalkInAge, t.LateArrivedAt, t.QueueOrder, t.FinalizedAt)).ToListAsync(ct);
        return new(summary, tokens, page, pageSize, total);
    }
    public async Task<DoctorSessionDetails?> ActiveAsync(Guid clinic, Guid doctor, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var id = await Scoped(clinic, doctor).Where(x => x.Status == SessionStatus.Running || x.Status == SessionStatus.Paused || (x.Status != SessionStatus.Closed && x.EndTime >= now))
            .OrderBy(x => x.Status == SessionStatus.Running || x.Status == SessionStatus.Paused ? 0 : 1).ThenBy(x => x.StartTime).ThenBy(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        return id.HasValue ? await DetailsAsync(clinic, doctor, id.Value, 1, 10, "Active", ct) : null;
    }
}
