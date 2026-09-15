using DigiLine.Infrastructure.Persistence;
using DigiLine.Infrastructure.Authentication;
using DigiLine.Infrastructure.Persistence.Repositories;
using DigiLine.Application.Administration;
using DigiLine.Domain.Entities;
using DigiLine.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

var connection = Environment.GetEnvironmentVariable("DIGILINE_TEST_CONNECTION") ?? throw new Exception("Set DIGILINE_TEST_CONNECTION to an isolated verification database.");
if (!new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith("DigiLine_Verification_")) throw new Exception("Only DigiLine_Verification_* databases are permitted.");
var options = new DbContextOptionsBuilder<DigiLineDbContext>().UseSqlServer(connection, o => o.CommandTimeout(600)).Options;
await using var db = new DigiLineDbContext(options);
await db.Database.MigrateAsync();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["PasswordReset:PublicBaseUrl"] = "https://localhost" }).Build();
var hasher = new PasswordHasher();
const string password = "VerificationOnly123!";
var watch = Stopwatch.StartNew();
if (args.FirstOrDefault() == "query-sql") {
  var queryService = new StaffSessionQueries(db);
  var method = typeof(StaffSessionQueries).GetMethod("Summaries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
  var clinic = await db.Clinics.Where(x => x.Slug == "load-clinic-0").Select(x => x.Id).SingleAsync();
  var sessions = db.Sessions.Where(x => x.ClinicId == clinic).OrderByDescending(x => x.StartTime).Take(10);
  var query = (IQueryable<DigiLine.Application.Sessions.DoctorSessionSummary>)method.Invoke(queryService, [sessions])!;
  Console.WriteLine(query.ToQueryString());
} else if (args.FirstOrDefault() == "prepare-load") {
  await db.Database.ExecuteSqlRawAsync("UPDATE Sessions SET BookingOpenTime=DATEADD(hour,-2,SYSUTCDATETIME()),StartTime=DATEADD(hour,-1,SYSUTCDATETIME()),EndTime=DATEADD(hour,8,SYSUTCDATETIME()),LastBookingTime=DATEADD(hour,8,SYSUTCDATETIME()) WHERE Name='Load live session' AND ClinicId IN (SELECT Id FROM Clinics WHERE Slug LIKE 'load-clinic-%');");
  Console.WriteLine($"Load clinics: {await db.Clinics.CountAsync(x => x.Slug.StartsWith("load-clinic-"))}; historical tokens: {await db.QueueTokens.CountAsync(x => x.Session.Name.StartsWith("Historical ") && x.Session.Clinic.Slug.StartsWith("load-clinic-"))}");
} else if (args.FirstOrDefault() == "seed") {
  var count = args.Length > 1 ? int.Parse(args[1]) : 100;
  for (var i = 0; i < count; i++) {
    var slug = $"load-clinic-{i}";
    if (await db.Clinics.AnyAsync(x => x.Slug == slug)) continue;
    var clinic = new Clinic($"Load Clinic {i}", slug);
    var staff = new Staff(clinic.Id, "Load Doctor", $"load-{i}@verification.local", hasher.Hash(password), StaffRole.Doctor);
    staff.UpdateAccess(staff.Name, [StaffRole.Admin, StaffRole.Doctor, StaffRole.Receptionist], true);
    db.Clinics.Add(clinic); db.Staff.Add(staff);
    var now = DateTime.UtcNow;
    var live = new Session(clinic.Id, staff.Id, "Load live session", now.AddHours(-2), now.AddHours(-1), now.AddHours(8), now.AddHours(8), 5); live.OpenBooking(); live.Start();
    db.Sessions.Add(live); await db.SaveChangesAsync();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      ;WITH n AS (SELECT TOP (100) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) n FROM sys.all_objects)
      INSERT INTO Sessions (Id,ClinicId,DoctorId,Name,BookingOpenTime,StartTime,EndTime,LastBookingTime,NextTokenNumber,NextQueueOrder,AverageConsultationMinutes,Status,CreatedAt,UpdatedAt)
      SELECT NEWID(),{clinic.Id},{staff.Id},CONCAT('Historical ',n),DATEADD(day,-n-1,SYSUTCDATETIME()),DATEADD(day,-n,SYSUTCDATETIME()),DATEADD(hour,2,DATEADD(day,-n,SYSUTCDATETIME())),DATEADD(hour,2,DATEADD(day,-n,SYSUTCDATETIME())),1001,1001,5,5,SYSUTCDATETIME(),SYSUTCDATETIME() FROM n;
      ;WITH n AS (SELECT TOP (1000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) n FROM sys.all_objects a CROSS JOIN sys.all_objects b)
      INSERT INTO QueueTokens (Id,SessionId,TokenNumber,QueueOrder,WalkInName,Status,CreatedAt,CompletedAt)
      SELECT NEWID(),s.Id,n.n,n.n,'Synthetic patient',4,s.StartTime,s.EndTime FROM Sessions s CROSS JOIN n WHERE s.ClinicId={clinic.Id} AND s.Status=5;
      """);
    db.ChangeTracker.Clear(); Console.WriteLine($"Seeded {i+1}/{count} clinics ({watch.Elapsed}).");
  }
} else {
  var suffix = Guid.NewGuid().ToString("N"); var mail = new CaptureMail();
  var service = new AdministrationService(db, hasher, mail, config, NullLogger<AdministrationService>.Instance);
  await service.RegisterAsync(new("Verification Clinic", $"verify-{suffix}", "Asia/Kolkata", "Owner", $"owner-{suffix}@verification.local", password, true), default);
  var owner = await db.Staff.Include(x => x.Roles).SingleAsync(x => x.Email == $"owner-{suffix}@verification.local");
  if (owner.Roles.Count != 2 || !owner.IsActive) throw new Exception("Owner roles/activation failed.");
  var invited = await service.InviteAsync(owner.ClinicId, owner.Id, new("Reception", $"staff-{suffix}@verification.local", [StaffRole.Receptionist]), default);
  await service.AcceptAsync(new(mail.Raw!, password), default);
  try { await service.AcceptAsync(new(mail.Raw!, password), default); throw new Exception("Invitation replay accepted."); } catch (ArgumentException) { }
  try { await service.UpdateAsync(owner.ClinicId, owner.Id, owner.Id, new("Owner", [StaffRole.Doctor], true), default); throw new Exception("Last admin removed."); } catch (ArgumentException) { }
  var now = DateTime.UtcNow; var session = new Session(owner.ClinicId, owner.Id, "Verify queue", now.AddHours(-2),now.AddHours(-1),now.AddHours(1),now.AddHours(1),5); session.OpenBooking();session.Start();db.Sessions.Add(session);
  var token = new QueueToken(session.Id, 1, "Skipped patient", null);token.AssignQueueOrder(1);token.Call(now);token.Skip();db.QueueTokens.Add(token);await db.SaveChangesAsync();
  var queries = new StaffSessionQueries(db);
  var list = await queries.ListAsync(owner.ClinicId, owner.Id, new(), default);
  var details = await queries.DetailsAsync(owner.ClinicId, owner.Id, session.Id,1,10,"Active",default);
  if (list.TotalCount != 1 || details?.Session.QueueSummary.SkippedCount != 1 || details.Queue.Count != 1) throw new Exception("Paged query failed.");
  var lifecycle = new DigiLine.Application.Sessions.SessionLifecycleService(new SessionRepository(db), transactions: new DigiLine.Infrastructure.Persistence.Transactions.EfTransactionManager(db));
  await lifecycle.StopBookingAsync(session.Id); await lifecycle.CompleteAsync(session.Id,true);
  if (token.Status != QueueTokenStatus.NoShow || token.CompletedAt != null) throw new Exception("Finalization failed.");
  Console.WriteLine("PASS: registration, roles, invitation acceptance/replay, last-admin protection, SQL summaries/pages, stop/completion.");
}
Console.WriteLine($"Finished in {watch.Elapsed}.");
sealed class CaptureMail : IStaffEmailSender {
  public string? Raw;
  public Task SendResetLinkAsync(string email,string link,CancellationToken ct) => Task.CompletedTask;
  public Task SendInvitationLinkAsync(string email,string link,CancellationToken ct) { Raw = link.Split("#token=")[1]; return Task.CompletedTask; }
}
