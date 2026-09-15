namespace DigiLine.Domain.Entities;

public sealed class PasswordResetToken
{
    public Guid Id { get; private set; }
    public Guid StaffId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    private PasswordResetToken() { }
    public PasswordResetToken(Guid staffId, string tokenHash, DateTime now)
    {
        Id = Guid.NewGuid(); StaffId = staffId; TokenHash = tokenHash;
        CreatedAt = now; ExpiresAt = now.AddMinutes(30);
    }
    public bool IsValid(DateTime now) => UsedAt is null && now < ExpiresAt;
    public void Consume(DateTime now)
    {
        if (!IsValid(now)) throw new InvalidOperationException("This reset link has expired or was already used.");
        UsedAt = now;
    }
    public void Invalidate(DateTime now) => UsedAt ??= now;
}
