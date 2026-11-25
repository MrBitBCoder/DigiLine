using System.Collections.Concurrent;
using System.Security.Cryptography;
using DigiLine.Application.Patients;

namespace DigiLine.Infrastructure.Patients;

public sealed class InMemoryPatientAccessService : IPatientAccessService
{
    private readonly ConcurrentDictionary<Guid, Challenge> _challenges = new();
    private readonly ConcurrentDictionary<string, Ticket> _tickets = new();

    public PhoneVerificationChallenge CreateChallenge(string phoneNumber)
    {
        var id = Guid.NewGuid();
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expires = DateTime.UtcNow.AddMinutes(10);
        _challenges[id] = new Challenge(phoneNumber, code, expires);
        return new PhoneVerificationChallenge(id, code, expires);
    }

    public string? Verify(Guid challengeId, string code)
    {
        if (!_challenges.TryRemove(challengeId, out var challenge)
            || challenge.ExpiresAt < DateTime.UtcNow
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(challenge.Code),
                System.Text.Encoding.UTF8.GetBytes(code ?? string.Empty))) return null;
        return challenge.PhoneNumber;
    }

    public string IssueAccess(string phoneNumber)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _tickets[token] = new Ticket(phoneNumber, DateTime.UtcNow.AddHours(12));
        return token;
    }

    public string? GetPhoneNumber(string accessToken) =>
        _tickets.TryGetValue(accessToken ?? string.Empty, out var ticket) && ticket.ExpiresAt >= DateTime.UtcNow 
        ? ticket.PhoneNumber : null;

    private sealed record Challenge(string PhoneNumber, string Code, DateTime ExpiresAt);
    private sealed record Ticket(string PhoneNumber, DateTime ExpiresAt);
}
