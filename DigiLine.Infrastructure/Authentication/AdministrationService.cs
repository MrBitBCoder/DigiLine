using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DigiLine.Application.Administration;
using DigiLine.Application.Common.Interfaces;
using DigiLine.Application.Common;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;
using DigiLine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DigiLine.Infrastructure.Authentication;

public sealed class AdministrationService(DigiLineDbContext db, IPasswordHasher hasher,
    IStaffEmailSender sender, IConfiguration configuration, ILogger<AdministrationService> logger) : IAdministrationService
{
    private static string Name(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 200
        ? value.Trim() : throw new ArgumentException("A name of at most 200 characters is required.");
    private static string Email(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 320 && MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim()
        ? value.Trim() : throw new ArgumentException("Enter a valid email address.");
    private static StaffRole[] Roles(StaffRole[] roles) => roles is { Length: > 0 } && roles.All(x => Enum.IsDefined(x))
        ? roles.Distinct().ToArray() : throw new ArgumentException("Choose at least one valid role.");
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private void Audit(Guid clinic, Guid actor, Guid? staff, string action) => db.AdministrationAudits.Add(new() { ClinicId = clinic, ActorId = actor, StaffId = staff, Action = action });
    private async Task<Clinic> LockClinic(Guid id, CancellationToken ct) => await db.Clinics
        .FromSqlInterpolated($"SELECT * FROM Clinics WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
        .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Clinic was not found.");
    private async Task<Staff> Staff(Guid clinic, Guid id, CancellationToken ct) => await db.Staff.Include(x => x.Roles)
        .SingleOrDefaultAsync(x => x.Id == id && x.ClinicId == clinic, ct) ?? throw new KeyNotFoundException("Staff member was not found.");
    public async Task RegisterAsync(RegisterClinicRequest request, CancellationToken ct)
    {
        StaffPasswordService.ValidatePassword(request.Password);
        var email = Email(request.Email);
        var slug = request.Slug?.Trim().ToLowerInvariant() ?? "";
        if (!Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant) || slug.Length > 100)
            throw new ArgumentException("Use a clinic address of at most 100 lowercase letters, numbers, and hyphens.");
        var clinic = new Clinic(Name(request.ClinicName), slug, request.TimeZoneId);
        var owner = new Staff(clinic.Id, Name(request.Name), email, hasher.Hash(request.Password), StaffRole.Admin);
        owner.UpdateAccess(owner.Name, request.AlsoDoctor ? [StaffRole.Admin, StaffRole.Doctor] : [StaffRole.Admin], true);
        db.Clinics.Add(clinic); db.Staff.Add(owner); Audit(clinic.Id, owner.Id, owner.Id, "Clinic registered");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new ApplicationConflictException("That clinic address or staff email is already registered."); }
    }
    public async Task<object> GetClinicAsync(Guid clinicId, CancellationToken ct) => await db.Clinics.AsNoTracking()
        .Where(x => x.Id == clinicId).Select(x => new { x.Id, x.Name, x.Slug, x.TimeZoneId }).SingleAsync(ct);
    public async Task UpdateClinicAsync(Guid clinicId, Guid actorId, ClinicUpdateRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var clinic = await LockClinic(clinicId, ct);
        clinic.Rename(Name(request.Name)); clinic.SetTimeZone(request.TimeZoneId);
        Audit(clinicId, actorId, null, "Clinic details updated"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<PageResult<StaffView>> ListAsync(Guid clinicId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Staff.AsNoTracking().Where(x => x.ClinicId == clinicId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new StaffView(x.Id, x.Name, x.Email, x.Roles.Select(r => r.Role).ToArray(), x.IsActive, x.InvitationPending,
                db.StaffInvitations.Any(i => i.StaffId == x.Id && i.UsedAt == null && i.DeliveryFailed))).ToListAsync(ct);
        return new(items, page, pageSize, total);
    }
    private async Task<(StaffInvitation Invitation, string Raw)> NewInvitation(Guid staffId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var old = await db.StaffInvitations.Where(x => x.StaffId == staffId && x.UsedAt == null).ToListAsync(ct);
        foreach (var invitation in old) invitation.UsedAt = now;
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var next = new StaffInvitation { StaffId = staffId, TokenHash = Hash(raw), ExpiresAt = now.AddHours(48) };
        db.StaffInvitations.Add(next); return (next, raw);
    }
    private async Task<bool> Deliver(Staff staff, StaffInvitation invitation, string raw, CancellationToken ct)
    {
        try {
            var url = configuration["PasswordReset:PublicBaseUrl"];
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Host != "localhost"))
                throw new InvalidOperationException("Configure an HTTPS public base URL.");
            await sender.SendInvitationLinkAsync(staff.Email, $"{url!.TrimEnd('/')}/accept-invitation#token={raw}", ct);
            return true;
        } catch (Exception e) when (e is not OperationCanceledException) {
            logger.LogWarning(e, "Invitation delivery failed for staff {StaffId}", staff.Id);
            invitation.DeliveryFailed = true; await db.SaveChangesAsync(ct); return false;
        }
    }
    public async Task<StaffView> CreateAsync(Guid clinicId, Guid actorId, CreateStaffRequest request, CancellationToken ct)
    {
        StaffPasswordService.ValidatePassword(request.Password);
        var roles = Roles(request.Roles);
        var staff = new Staff(clinicId, Name(request.Name), Email(request.Email), hasher.Hash(request.Password), roles[0]);
        staff.UpdateAccess(staff.Name, roles, true);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockClinic(clinicId, ct);
        if (await db.Staff.AnyAsync(x => x.NormalizedEmail == staff.NormalizedEmail, ct))
            throw new ApplicationConflictException("That email is already registered.");
        db.Staff.Add(staff);
        Audit(clinicId, actorId, staff.Id, "Staff created");
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new ApplicationConflictException("That email is already registered."); }
        return new(staff.Id, staff.Name, staff.Email, staff.Roles.Select(x => x.Role).ToArray(), staff.IsActive, staff.InvitationPending, false);
    }
    public async Task<StaffView> InviteAsync(Guid clinicId, Guid actorId, InviteStaffRequest request, CancellationToken ct)
    {
        var staff = new Staff(clinicId, Name(request.Name), Email(request.Email), "", StaffRole.Receptionist);
        staff.UpdateAccess(staff.Name, Roles(request.Roles), false); staff.AwaitInvitation();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockClinic(clinicId, ct);
        if (await db.Staff.AnyAsync(x => x.NormalizedEmail == staff.NormalizedEmail, ct)) throw new ApplicationConflictException("That email is already registered.");
        db.Staff.Add(staff); var (invitation, raw) = await NewInvitation(staff.Id, ct);
        Audit(clinicId, actorId, staff.Id, "Staff invited");
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new ApplicationConflictException("That email is already registered."); }
        var delivered = await Deliver(staff, invitation, raw, ct);
        return new(staff.Id, staff.Name, staff.Email, staff.Roles.Select(x => x.Role).ToArray(), false, true, !delivered);
    }
    public async Task UpdateAsync(Guid clinicId, Guid actorId, Guid staffId, StaffUpdateRequest request, CancellationToken ct)
    {
        var roles = Roles(request.Roles); var name = Name(request.Name);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockClinic(clinicId, ct); var staff = await Staff(clinicId, staffId, ct);
        if (staff.InvitationPending && request.IsActive) throw new ArgumentException("The invitation must be accepted before activation.");
        if (staff.IsActive && staff.Roles.Any(x => x.Role == StaffRole.Admin) && (!request.IsActive || !roles.Contains(StaffRole.Admin)) &&
            !await db.Staff.AnyAsync(x => x.ClinicId == clinicId && x.Id != staffId && x.IsActive && x.Roles.Any(r => r.Role == StaffRole.Admin), ct))
            throw new ArgumentException("Keep at least one active administrator.");
        if ((!request.IsActive || !roles.Contains(StaffRole.Doctor)) && staff.Roles.Any(x => x.Role == StaffRole.Doctor) &&
            await db.Sessions.AnyAsync(x => x.DoctorId == staffId && x.Status != SessionStatus.Closed, ct))
            throw new ArgumentException("Complete this doctor's unfinished sessions before removing access.");
        staff.UpdateAccess(name, roles, request.IsActive); Audit(clinicId, actorId, staffId, "Staff name, roles or activation updated");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<bool> ResendAsync(Guid clinicId, Guid actorId, Guid staffId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockClinic(clinicId, ct); var staff = await Staff(clinicId, staffId, ct);
        if (!staff.InvitationPending) throw new ArgumentException("This account has already accepted its invitation.");
        var (invitation, raw) = await NewInvitation(staffId, ct);
        Audit(clinicId, actorId, staffId, "Invitation resent"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await Deliver(staff, invitation, raw, ct);
    }
    public async Task RevokeAsync(Guid clinicId, Guid actorId, Guid staffId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockClinic(clinicId, ct); var staff = await Staff(clinicId, staffId, ct);
        if (!staff.InvitationPending) throw new ArgumentException("Only pending invitations can be revoked.");
        foreach (var invitation in await db.StaffInvitations.Where(x => x.StaffId == staffId && x.UsedAt == null).ToListAsync(ct)) invitation.UsedAt = DateTime.UtcNow;
        Audit(clinicId, actorId, staffId, "Invitation revoked"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task AcceptAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        StaffPasswordService.ValidatePassword(request.Password);
        if (string.IsNullOrWhiteSpace(request.Token)) throw new ArgumentException("Invalid or expired invitation.");
        var hash = Hash(request.Token);
        var clinicId = await db.StaffInvitations.Where(x => x.TokenHash == hash)
            .Join(db.Staff, i => i.StaffId, s => s.Id, (i, s) => (Guid?)s.ClinicId).SingleOrDefaultAsync(ct)
            ?? throw new ArgumentException("Invalid or expired invitation.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockClinic(clinicId, ct);
        var invitation = await db.StaffInvitations.SingleAsync(x => x.TokenHash == hash, ct);
        if (invitation.UsedAt != null || invitation.ExpiresAt <= DateTime.UtcNow) throw new ArgumentException("Invalid or expired invitation.");
        var staff = await Staff(clinicId, invitation.StaffId, ct);
        if (!staff.InvitationPending) throw new ArgumentException("This invitation has already been accepted.");
        invitation.UsedAt = DateTime.UtcNow; staff.AcceptInvitation(hasher.Hash(request.Password));
        Audit(clinicId, staff.Id, staff.Id, "Invitation accepted"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
