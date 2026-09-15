using DigiLine.Domain.Entities;

namespace DigiLine.Application.Sessions;

public sealed record CreateDoctorSessionRequest(
    string Name,
    DateTime StartTime,
    DateTime EndTime,
    int AverageConsultationMinutes,
    DateTime? BookingOpenTime = null);

public interface ICreateDoctorSessionService
{
    Task<Guid> CreateAsync(Guid clinicId, Guid doctorId, CreateDoctorSessionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CreateDoctorSessionService(ISessionRepository sessions, DigiLine.Application.Transactions.ITransactionManager? transactions = null) : ICreateDoctorSessionService
{
    public async Task<Guid> CreateAsync(Guid clinicId, Guid doctorId, CreateDoctorSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartTime.Kind != DateTimeKind.Utc || request.EndTime.Kind != DateTimeKind.Utc ||
            (request.BookingOpenTime.HasValue && request.BookingOpenTime.Value.Kind != DateTimeKind.Utc))
            throw new InvalidOperationException("Session times must include an explicit UTC offset.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Session name is required.");

        var now = DateTime.UtcNow;
        if (request.StartTime <= now)
            throw new InvalidOperationException("Start time must be in the future.");

        var bookingOpenTime = request.BookingOpenTime ?? now;
        var session = new Session(clinicId, doctorId, request.Name.Trim(), bookingOpenTime,
            request.StartTime, request.EndTime, request.EndTime,
            request.AverageConsultationMinutes);
        session.OpenBooking();
        await using var transaction = transactions is null ? null : await transactions.BeginAsync(cancellationToken);
        await sessions.AddAsync(session, cancellationToken);
        await sessions.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return session.Id;
    }
}
