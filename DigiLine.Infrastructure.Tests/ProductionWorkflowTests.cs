using DigiLine.Application.Administration;
using DigiLine.Application.Patients;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;
using DigiLine.Infrastructure.Authentication;
using DigiLine.Infrastructure.Persistence;
using DigiLine.Infrastructure.Persistence.Repositories;
using DigiLine.Infrastructure.Persistence.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigiLine.Infrastructure.Tests;

public sealed class ProductionWorkflowTests : IClassFixture<ProductionWorkflowTests.Database>
{
    private readonly Database database;
    private const string Password="VerificationOnly123!";
    public ProductionWorkflowTests(Database database) => this.database=database;
    private static AdministrationService Admin(DigiLineDbContext db, Mail? mail=null) => new(db,new PasswordHasher(),mail ?? new Mail(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["PasswordReset:PublicBaseUrl"]="https://localhost" }).Build(),NullLogger<AdministrationService>.Instance);
    private static SessionLifecycleService Lifecycle(DigiLineDbContext db, IQueueEventPublisher? publisher=null) => new(new SessionRepository(db),publisher,new EfTransactionManager(db));
    private async Task<(Guid Clinic,Guid Owner,Guid Session)> Fixture(bool stopped=false, bool skipped=false)
    {
        await using var db=database.Open(); var suffix=Guid.NewGuid().ToString("N");
        await Admin(db).RegisterAsync(new("Test clinic",suffix,"Asia/Kolkata","Owner",$"{suffix}@example.test",Password,true),default);
        var owner=await db.Staff.SingleAsync(x=>x.NormalizedEmail==$"{suffix}@example.test".ToUpperInvariant());
        var now=DateTime.UtcNow;var session=new Session(owner.ClinicId,owner.Id,"Concurrency",now.AddHours(-1),now.AddMinutes(-30),now.AddHours(1),now.AddHours(1),5);
        session.OpenBooking();session.Start();if(stopped)session.StopBooking(now);db.Sessions.Add(session);
        if(skipped){var token=new QueueToken(session.Id,1,"Absent",null);token.AssignQueueOrder(1);token.Call(now);token.Skip();db.QueueTokens.Add(token);}
        await db.SaveChangesAsync();return(owner.ClinicId,owner.Id,session.Id);
    }
    [Fact]
    public async Task DirectStaffCreationUsesExistingHasherAndSkipsInvitations()
    {
        var f=await Fixture();await using var db=database.Open();var mail=new Mail {Fail=true};
        var staff=await Admin(db,mail).CreateAsync(f.Clinic,f.Owner,new("New staff",$"{Guid.NewGuid()}@example.test",[StaffRole.Admin,StaffRole.Doctor],Password),default);
        var saved=await db.Staff.Include(x=>x.Roles).SingleAsync(x=>x.Id==staff.Id);
        Assert.True(saved.IsActive);Assert.False(saved.InvitationPending);
        Assert.NotEqual(Password,saved.PasswordHash);Assert.True(new PasswordHasher().Verify(Password,saved.PasswordHash));
        Assert.Equal(new[]{StaffRole.Doctor,StaffRole.Admin},saved.Roles.Select(x=>x.Role).OrderBy(x=>x));
        Assert.False(await db.StaffInvitations.AnyAsync(x=>x.StaffId==staff.Id));Assert.Equal("",mail.Token);
        Assert.True(await db.AdministrationAudits.AnyAsync(x=>x.StaffId==staff.Id && x.Action=="Staff created"));
        await Assert.ThrowsAsync<DigiLine.Application.Common.ApplicationConflictException>(()=>Admin(db).CreateAsync(f.Clinic,f.Owner,new("Duplicate",saved.Email,[StaffRole.Receptionist],Password),default));
    }
    [Fact]
    public async Task InvitationExpiryResendRevocationAndDeliveryFailure()
    {
        var f=await Fixture();await using var db=database.Open();var mail=new Mail {Fail=true};var admin=Admin(db,mail);
        var staff=await admin.InviteAsync(f.Clinic,f.Owner,new("Reception",$"{Guid.NewGuid()}@example.test",[StaffRole.Receptionist]),default);
        Assert.True(staff.DeliveryFailed);Assert.False(staff.IsActive);
        mail.Fail=false;Assert.True(await admin.ResendAsync(f.Clinic,f.Owner,staff.Id,default));var first=mail.Token;
        Assert.True(await admin.ResendAsync(f.Clinic,f.Owner,staff.Id,default));var second=mail.Token;
        await Assert.ThrowsAsync<ArgumentException>(()=>admin.AcceptAsync(new(first,Password),default));
        await admin.RevokeAsync(f.Clinic,f.Owner,staff.Id,default);
        await Assert.ThrowsAsync<ArgumentException>(()=>admin.AcceptAsync(new(second,Password),default));
        await admin.ResendAsync(f.Clinic,f.Owner,staff.Id,default);
        var pending=await db.StaffInvitations.SingleAsync(x=>x.StaffId==staff.Id && x.UsedAt==null);pending.ExpiresAt=DateTime.UtcNow.AddSeconds(-1);await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(()=>admin.AcceptAsync(new(mail.Token,Password),default));
        db.ChangeTracker.Clear();await admin.ResendAsync(f.Clinic,f.Owner,staff.Id,default);await admin.AcceptAsync(new(mail.Token,Password),default);
        Assert.True((await db.Staff.FindAsync(staff.Id))!.IsActive);
        Assert.True(await db.AdministrationAudits.AnyAsync(x=>x.StaffId==staff.Id && x.Action=="Invitation accepted"));
    }
    [Fact]
    public async Task StaffMutationRejectsForeignClinicAndUnfinishedDoctorRemoval()
    {
        var f=await Fixture();await using var db=database.Open();var admin=Admin(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>admin.UpdateAsync(Guid.NewGuid(),f.Owner,f.Owner,new("Owner",[StaffRole.Admin],true),default));
        await Assert.ThrowsAsync<ArgumentException>(()=>admin.UpdateAsync(f.Clinic,f.Owner,f.Owner,new("Owner",[StaffRole.Admin],true),default));
        await Assert.ThrowsAsync<ArgumentException>(()=>admin.UpdateAsync(f.Clinic,f.Owner,f.Owner,new("Owner",[StaffRole.Doctor],true),default));
        var queries=new StaffSessionQueries(db);Assert.Null(await queries.DetailsAsync(Guid.NewGuid(),f.Owner,f.Session,1,10,"Active",default));
        Assert.False(await queries.OwnsAsync(f.Clinic,Guid.NewGuid(),f.Session,default));
    }
    [Fact]
    public async Task RoleChangesRevokePasswordVersionAndPreserveHistory()
    {
        var f=await Fixture();await using var db=database.Open();var admin=Admin(db);var mail=new Mail();admin=Admin(db,mail);
        var invited=await admin.InviteAsync(f.Clinic,f.Owner,new("Staff",$"{Guid.NewGuid()}@example.test",[StaffRole.Receptionist]),default);
        await admin.AcceptAsync(new(mail.Token,Password),default);var staff=await db.Staff.FindAsync(invited.Id);var version=staff!.PasswordVersion;
        await admin.UpdateAsync(f.Clinic,f.Owner,staff.Id,new("Renamed",[StaffRole.Admin,StaffRole.Receptionist],false),default);
        Assert.NotEqual(version,staff.PasswordVersion);Assert.False(staff.IsActive);Assert.Equal(2,staff.Roles.Count);
        Assert.True(await db.Staff.AnyAsync(x=>x.Id==staff.Id));
    }
    [Fact]
    public async Task ConcurrentDuplicateRegistrationHasOneWinner()
    {
        var suffix=Guid.NewGuid().ToString("N");
        async Task<bool> Register(){await using var db=database.Open();try{await Admin(db).RegisterAsync(new("Duplicate",suffix,"Asia/Kolkata","Owner",$"{suffix}@example.test",Password,false),default);return true;}catch(DigiLine.Application.Common.ApplicationConflictException){return false;}}
        var results=await Task.WhenAll(Register(),Register());Assert.Single(results.Where(x=>x));
    }
    [Fact]
    public async Task SimultaneousAdminRemovalCannotRemoveLastAdministrator()
    {
        var f=await Fixture();Guid second;
        await using(var db=database.Open()){var staff=new Staff(f.Clinic,"Other",$"{Guid.NewGuid()}@example.test","hash",StaffRole.Admin);db.Staff.Add(staff);await db.SaveChangesAsync();second=staff.Id;}
        async Task<bool> Remove(Guid id,StaffRole[] roles){await using var db=database.Open();try{await Admin(db).UpdateAsync(f.Clinic,f.Owner,id,new("Changed",roles,true),default);return true;}catch(ArgumentException){return false;}}
        var result=await Task.WhenAll(Remove(f.Owner,[StaffRole.Doctor]),Remove(second,[StaffRole.Receptionist]));Assert.Single(result.Where(x=>x));
        await using var check=database.Open();Assert.Equal(1,await check.Staff.CountAsync(x=>x.ClinicId==f.Clinic && x.IsActive && x.Roles.Any(r=>r.Role==StaffRole.Admin)));
    }
    [Fact]
    public async Task BookingRacingStopNeverBooksAfterStoppedStateIsObserved()
    {
        for(var iteration=0;iteration<10;iteration++){
            var f=await Fixture();
            async Task Book(){await using var db=database.Open();var tokens=new QueueTokenRepository(db);var service=new CreateWalkInTokenService(new TokenNumberService(tokens),tokens,new SessionRepository(db),new EfTransactionManager(db));try{await service.CreateAsync(f.Session,"Race",null);}catch(InvalidOperationException){}}
            async Task Stop(){await using var db=database.Open();await Lifecycle(db).StopBookingAsync(f.Session);}
            await Task.WhenAll(Book(),Stop());
            await using var check=database.Open();Assert.NotNull((await check.Sessions.FindAsync(f.Session))!.BookingStoppedAt);
            var tokens=new QueueTokenRepository(check);await Assert.ThrowsAsync<InvalidOperationException>(()=>new CreateWalkInTokenService(new TokenNumberService(tokens),tokens,new SessionRepository(check),new EfTransactionManager(check)).CreateAsync(f.Session,"After stop",null));
            Assert.InRange(await check.QueueTokens.CountAsync(x=>x.SessionId==f.Session),0,1);
        }
    }
    [Fact]
    public async Task LateArrivalRacingCompletionCannotLeaveLateTokenInClosedSession()
    {
        for(var iteration=0;iteration<10;iteration++){
            var f=await Fixture(true,true);await using var lookup=database.Open();var id=await lookup.QueueTokens.Where(x=>x.SessionId==f.Session).Select(x=>x.Id).SingleAsync();
            async Task Arrive(){await using var db=database.Open();try{await new MarkTokenLateService(new SessionRepository(db),new QueueTokenRepository(db),transactionManager:new EfTransactionManager(db)).MarkLateAsync(f.Session,id);}catch(InvalidOperationException){}}
            async Task Complete(){await using var db=database.Open();try{await Lifecycle(db).CompleteAsync(f.Session,true);}catch(SessionCompletionException){}}
            await Task.WhenAll(Arrive(),Complete());await using var check=database.Open();var session=await check.Sessions.FindAsync(f.Session);var token=await check.QueueTokens.FindAsync(id);
            Assert.True(session!.Status==SessionStatus.Closed ? token!.Status==QueueTokenStatus.NoShow : token!.Status==QueueTokenStatus.Late);
        }
    }
    [Fact]
    public async Task PatientCancellationRacingCallNextHasOneValidWinner()
    {
        for(var iteration=0;iteration<10;iteration++){
            var f=await Fixture();var phone=$"9{Random.Shared.NextInt64(100000000,999999999)}";Guid tokenId;
            await using(var setup=database.Open()){
                var account=new PhoneAccount(phone);var profile=new PatientProfile(account.Id,"Patient",30);
                var setupToken=new QueueToken(f.Session,1,profile.Id);setup.AddRange(account,profile,setupToken);await setup.SaveChangesAsync();tokenId=setupToken.Id;
            }
            async Task Cancel(){await using var db=database.Open();try{await new CancelPatientTokenService(new Access(phone),new PublicPatientRepository(db),new SessionRepository(db),new QueueTokenRepository(db),new EfTransactionManager(db)).CancelAsync("access",f.Session,tokenId);}catch(InvalidOperationException){}}
            async Task Call(){await using var db=database.Open();try{await new CallNextTokenService(new SessionRepository(db),new QueueTokenRepository(db),new EfTransactionManager(db)).CallAsync(f.Session);}catch(InvalidOperationException){}}
            await Task.WhenAll(Cancel(),Call());await using var check=database.Open();var savedToken=await check.QueueTokens.SingleAsync(x=>x.Id==tokenId);
            Assert.True(savedToken.Status is QueueTokenStatus.Cancelled or QueueTokenStatus.Called);Assert.Equal(1,savedToken.TokenNumber);Assert.Equal(1,await check.QueueTokens.CountAsync(x=>x.SessionId==f.Session));
        }
    }
    [Fact]
    public async Task SimultaneousCompletionIsIdempotentAndPublishesAfterConnectionRelease()
    {
        var f=await Fixture(true,true);
        async Task Complete(){await using var db=database.Open();await Lifecycle(db,new ReleasedConnectionPublisher(db)).CompleteAsync(f.Session,true);}
        await Task.WhenAll(Complete(),Complete());await using var check=database.Open();var token=await check.QueueTokens.SingleAsync(x=>x.SessionId==f.Session);
        Assert.Equal(QueueTokenStatus.NoShow,token.Status);Assert.NotNull(token.FinalizedAt);Assert.Null(token.CompletedAt);
    }
    private sealed class ReleasedConnectionPublisher(DigiLineDbContext db):IQueueEventPublisher
    {
        public Task PublishAsync(QueueChangedEvent e,CancellationToken ct=default){Assert.Null(db.Database.CurrentTransaction);return Task.CompletedTask;}
    }
    private sealed class Mail:IStaffEmailSender
    {
        public bool Fail;public string Token="";
        public Task SendResetLinkAsync(string email,string link,CancellationToken ct)=>Task.CompletedTask;
        public Task SendInvitationLinkAsync(string email,string link,CancellationToken ct){if(Fail)throw new InvalidOperationException("Test mail failure");Token=link.Split("#token=")[1];return Task.CompletedTask;}
    }
    private sealed class Access(string phone):IPatientAccessService
    {
        public PhoneVerificationChallenge CreateChallenge(string phoneNumber)=>throw new NotSupportedException();
        public string? Verify(Guid challengeId,string code)=>throw new NotSupportedException();
        public string IssueAccess(string phoneNumber)=>throw new NotSupportedException();
        public string? GetPhoneNumber(string accessToken)=>accessToken=="access"?phone:null;
    }
    public sealed class Database:IAsyncLifetime
    {
        private readonly string connection=CreateConnection();
        private static string CreateConnection()
        {
            var database = "DigiLine_Verification_Integration_"+Guid.NewGuid().ToString("N");
            var configured = Environment.GetEnvironmentVariable("DIGILINE_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(configured))
                return $"Server=localhost;Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";

            var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = database };
            return builder.ConnectionString;
        }
        public DigiLineDbContext Open()=>new(new DbContextOptionsBuilder<DigiLineDbContext>().UseSqlServer(connection).Options);
        public async Task InitializeAsync(){await using var db=Open();await db.Database.MigrateAsync();}
        public Task DisposeAsync()=>Task.CompletedTask;
    }
}
