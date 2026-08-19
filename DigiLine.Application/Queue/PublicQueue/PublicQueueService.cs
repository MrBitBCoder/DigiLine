using DigiLine.Application.Patients;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed class PublicQueueService : IPublicQueueService
{
    private readonly IPatientAccessService _access;
    private readonly IPublicPatientRepository _patients;
    private readonly IBookTokenService _booking;
    private readonly IQueueTokenRepository _tokens;
    private readonly ISessionRepository _sessions;
    private readonly IGetQueuePositionService _positions;
    private readonly IGetEstimatedWaitService _waits;

    public PublicQueueService(IPatientAccessService access, IPublicPatientRepository patients, IBookTokenService booking, IQueueTokenRepository tokens, ISessionRepository sessions, IGetQueuePositionService positions, IGetEstimatedWaitService waits)
    { _access = access; _patients = patients; _booking = booking; _tokens = tokens; _sessions = sessions; _positions = positions; _waits = waits; }

    public async Task<PublicBookingResult> BookAsync(string accessToken, Guid sessionId, Guid patientProfileId, CancellationToken cancellationToken = default)
    {
        var phone = _access.GetPhoneNumber(accessToken) ?? throw new UnauthorizedAccessException("Patient access has expired.");
        if (!await _patients.IsOwnedByPhoneAsync(patientProfileId, phone, cancellationToken)) throw new UnauthorizedAccessException("Patient profile is not available.");
        var existing = await _tokens.GetActiveByPatientAsync(sessionId, patientProfileId, cancellationToken);
        if (existing is not null) return new PublicBookingResult(sessionId, existing.Id, existing.TokenNumber, true);
        var number = await _booking.BookAsync(sessionId, patientProfileId, cancellationToken);
        var created = await _tokens.GetActiveByPatientAsync(sessionId, patientProfileId, cancellationToken)
            ?? throw new InvalidOperationException("The booked token could not be loaded.");
        return new PublicBookingResult(sessionId, created.Id, number, false);
    }

    public async Task<PublicTokenState?> GetTokenAsync(string accessToken, Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var phone = _access.GetPhoneNumber(accessToken) ?? throw new UnauthorizedAccessException("Patient access has expired.");
        var token = await _tokens.GetByIdAsync(sessionId, tokenId, cancellationToken);
        if (token?.PatientProfileId is null || !await _patients.IsOwnedByPhoneAsync(token.PatientProfileId.Value, phone, cancellationToken)) return null;
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null) return null;
        var queue = await _tokens.GetBySessionOrderedAsync(sessionId, cancellationToken);
        var current = queue.Where(item => item.Status is QueueTokenStatus.Called or QueueTokenStatus.InConsultation)
            .OrderByDescending(item => item.CalledAt ?? item.CreatedAt).FirstOrDefault();
        QueuePositionResult? position = null; EstimatedWaitResult? wait = null;
        if (token.Status is QueueTokenStatus.Waiting or QueueTokenStatus.Late)
        { position = await _positions.GetAsync(sessionId, tokenId, cancellationToken); wait = await _waits.GetAsync(sessionId, tokenId, cancellationToken); }
        return new PublicTokenState(sessionId, token.Id, token.TokenNumber, token.Status, current?.TokenNumber,
            position?.Position, position?.PatientsAhead, wait?.EstimatedWaitMinutes,
            session.Status == SessionStatus.Paused, session.Status, DateTime.UtcNow);
    }
}
