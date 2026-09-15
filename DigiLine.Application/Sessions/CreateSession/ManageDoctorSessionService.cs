using DigiLine.Domain.Entities;

namespace DigiLine.Application.Sessions;

public interface IManageDoctorSessionService
{
    Task UpdateAsync(Guid clinicId, Guid doctorId, Guid sessionId, CreateDoctorSessionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid clinicId, Guid doctorId, Guid sessionId, CancellationToken cancellationToken = default);
}

public sealed class ManageDoctorSessionService(ISessionRepository sessions, DigiLine.Application.Transactions.ITransactionManager? transactions = null) : IManageDoctorSessionService
{
    public async Task UpdateAsync(Guid clinicId, Guid doctorId, Guid sessionId, CreateDoctorSessionRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = transactions is null ? null : await transactions.BeginAsync(cancellationToken);
        var session = await Owned(sessionId, clinicId, doctorId, cancellationToken);
        if (request.StartTime.Kind != DateTimeKind.Utc || request.EndTime.Kind != DateTimeKind.Utc ||
            (request.BookingOpenTime.HasValue && request.BookingOpenTime.Value.Kind != DateTimeKind.Utc))
            throw new InvalidOperationException("Session times must include an explicit UTC offset.");
        session.Reschedule(request.Name, request.BookingOpenTime ?? session.BookingOpenTime,
            request.StartTime, request.EndTime, request.AverageConsultationMinutes);
        await sessions.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid clinicId, Guid doctorId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = transactions is null ? null : await transactions.BeginAsync(cancellationToken);
        var session = await Owned(sessionId, clinicId, doctorId, cancellationToken);
        session.EnsureCanDelete();
        await sessions.DeleteAsync(session, cancellationToken);
        await sessions.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private async Task<Session> Owned(Guid sessionId, Guid clinicId, Guid doctorId, CancellationToken cancellationToken) =>
        await sessions.GetForDoctorForUpdateAsync(sessionId, clinicId, doctorId, cancellationToken)
        ?? throw new KeyNotFoundException("Session was not found.");
}
