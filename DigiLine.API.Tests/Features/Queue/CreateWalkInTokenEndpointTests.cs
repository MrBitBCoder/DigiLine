using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DigiLine.Infrastructure.Authentication;
using DigiLine.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DigiLine.API.Tests;

// Exercises the real Production pipeline and SQL-backed services, replacing tests for retired unscoped routes.
public sealed class CreateWalkInTokenEndpointTests : IClassFixture<CreateWalkInTokenEndpointTests.ApiFactory>
{
    private readonly ApiFactory factory;
    public CreateWalkInTokenEndpointTests(ApiFactory factory) => this.factory = factory;
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private async Task<Guid> Session()
    {
        var start = DateTime.UtcNow.AddMinutes(5);
        var result = await Json(await factory.Doctor.PostAsJsonAsync("/api/doctor/sessions", new { name="Endpoint test", startTime=start, endTime=start.AddHours(2), averageConsultationMinutes=5 }));
        var id = result.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/start", null)).StatusCode); return id;
    }
    private Task<HttpResponseMessage> WalkIn(Guid id, string name="Patient") => factory.Reception.PostAsJsonAsync("/api/reception/queue/walk-in", new { sessionId=id,walkInName=name,age=30 });
    [Fact]
    public async Task ProductionRegistrationAndDirectStaffCreationWorkWithoutSeeds()
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DigiLineDbContext>();
        Assert.False(await db.Clinics.AnyAsync(x => x.Slug == "dev-clinic"));
        Assert.True(await db.Staff.CountAsync() >= 2);
        var staff = await db.Staff.Include(x => x.Roles).SingleAsync(x => x.Email == "reception@verification.local");
        Assert.True(staff.IsActive);
        Assert.False(staff.InvitationPending);
        Assert.NotEqual(ApiFactory.Password, staff.PasswordHash);
        Assert.True(new PasswordHasher().Verify(ApiFactory.Password, staff.PasswordHash));
        Assert.Empty(await db.StaffInvitations.ToListAsync());
        Assert.Equal("", factory.Mail.Token);
        Assert.Equal(HttpStatusCode.OK,(await factory.Doctor.GetAsync("/api/admin/clinic")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await factory.Reception.GetAsync("/api/admin/clinic")).StatusCode);
    }
    [Theory]
    [InlineData("short")]
    [InlineData("NoNumbersHere!")]
    [InlineData("NoSymbols12345")]
    public async Task AdminCreationRejectsInvalidPassword(string password)
    {
        var response = await factory.Doctor.PostAsJsonAsync("/api/admin/staff", new { name="Invalid",email=$"{Guid.NewGuid()}@example.test",roles=new[]{2},password });
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
    }
    [Fact]
    public async Task AdminCreationResponseDoesNotExposePasswordAndRejectsNonAdmin()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var body = new { name="New doctor",email=$"{Guid.NewGuid()}@example.test",roles=new[]{1,2},password=ApiFactory.Password };
        Assert.Equal(HttpStatusCode.Forbidden,(await isolated.Reception.PostAsJsonAsync("/api/admin/staff",body)).StatusCode);
        var result = await Json(await isolated.Doctor.PostAsJsonAsync("/api/admin/staff",body));
        Assert.True(result.GetProperty("isActive").GetBoolean());
        Assert.False(result.GetProperty("invitationPending").GetBoolean());
        Assert.False(result.TryGetProperty("password",out _));
        Assert.False(result.TryGetProperty("passwordHash",out _));
        Assert.Equal(new[]{1,2},result.GetProperty("roles").EnumerateArray().Select(x=>x.GetInt32()).OrderBy(x=>x));
        Assert.Equal(HttpStatusCode.OK,(await isolated.Public.PostAsJsonAsync("/api/auth/login",new {body.email,body.password})).StatusCode);
    }
    [Theory]
    [InlineData("/api/queue/walk-in", "POST")]
    [InlineData("/api/queue/book", "POST")]
    [InlineData("/api/queue/session/11111111-1111-1111-1111-111111111111", "GET")]
    public async Task LegacyUnscopedRoutesAreRetired(string route,string method)
    { Assert.Equal(HttpStatusCode.NotFound,(await factory.Public.SendAsync(new HttpRequestMessage(new HttpMethod(method),route))).StatusCode); }
    [Fact]
    public async Task WalkInValidatesAndEnforcesSessionScope()
    {
        var id=await Session(); var result=await Json(await WalkIn(id)); Assert.Equal(1,result.GetProperty("tokenNumber").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest,(await WalkIn(id,"")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await WalkIn(Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await factory.Public.PostAsJsonAsync("/api/reception/queue/walk-in",new {sessionId=id,walkInName="No auth",age=30})).StatusCode);
    }
    [Fact]
    public async Task StopBookingRequiresStartingSessionAndThenAllowsCompletion()
    {
        var start = DateTime.UtcNow.AddMinutes(5);
        var created = await Json(await factory.Doctor.PostAsJsonAsync("/api/doctor/sessions", new { name="Start before stop",startTime=start,endTime=start.AddHours(1),averageConsultationMinutes=5 }));
        var id = created.GetProperty("id").GetGuid();
        var rejected = await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/stop-booking",null);
        Assert.Equal(HttpStatusCode.BadRequest,rejected.StatusCode);
        Assert.Contains("running or paused",await rejected.Content.ReadAsStringAsync());
        await Json(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/start",null));
        await Json(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/stop-booking",null));
        await Json(await factory.Doctor.PostAsJsonAsync($"/api/doctor/sessions/{id}/complete",new {confirmSkipped=true}));
        var details = await Json(await factory.Doctor.GetAsync($"/api/doctor/sessions/{id}"));
        Assert.Equal(5,details.GetProperty("session").GetProperty("status").GetInt32());
        Assert.False(details.GetProperty("session").GetProperty("isBookingOpen").GetBoolean());
    }
    [Fact]
    public async Task StopBookingThenCompleteRequiresResolvingQueueAndSkippedConfirmation()
    {
        var id=await Session(); await Json(await WalkIn(id));
        Assert.Equal(HttpStatusCode.OK,(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/stop-booking",null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await WalkIn(id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await factory.Doctor.PostAsJsonAsync($"/api/doctor/sessions/{id}/complete",new {confirmSkipped=true})).StatusCode);
        var called=await Json(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/queue/call-next",null)); var token=called.GetProperty("id").GetGuid();
        await Json(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/tokens/{token}/skip",null));
        Assert.Equal(HttpStatusCode.Conflict,(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/close",null)).StatusCode);
        await Json(await factory.Doctor.PostAsJsonAsync($"/api/doctor/sessions/{id}/complete",new {confirmSkipped=true}));
        var details=await Json(await factory.Doctor.GetAsync($"/api/doctor/sessions/{id}?filter=Other%20finished"));
        Assert.Equal(5, details.GetProperty("session").GetProperty("status").GetInt32());
        Assert.False(details.GetProperty("session").GetProperty("isBookingOpen").GetBoolean());
        var reception = await Json(await factory.Reception.GetAsync($"/api/reception/sessions/{id}"));
        Assert.Equal(5, reception.GetProperty("session").GetProperty("status").GetInt32());
        var finished=details.GetProperty("queue")[0]; Assert.Equal(8,finished.GetProperty("status").GetInt32()); Assert.Equal(JsonValueKind.Null,finished.GetProperty("completedAt").ValueKind); Assert.NotEqual(JsonValueKind.Null,finished.GetProperty("finalizedAt").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest,(await factory.Reception.PostAsync($"/api/reception/sessions/{id}/tokens/{token}/late",null)).StatusCode);
        await Json(await factory.Doctor.PostAsJsonAsync($"/api/doctor/sessions/{id}/complete",new {confirmSkipped=true}));
    }
    [Fact]
    public async Task LatePatientsRemainCallableAfterBookingStops()
    {
        var id=await Session(); await Json(await WalkIn(id));
        var called=await Json(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/queue/call-next",null)); var token=called.GetProperty("id").GetGuid();
        await Json(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/tokens/{token}/skip",null));
        await Json(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/stop-booking",null));
        await Json(await factory.Reception.PostAsync($"/api/reception/sessions/{id}/tokens/{token}/late",null));
        Assert.Equal(HttpStatusCode.Conflict,(await factory.Doctor.PostAsJsonAsync($"/api/doctor/sessions/{id}/complete",new {confirmSkipped=true})).StatusCode);
        await Json(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/queue/call-next",null));
        await Json(await factory.Reception.PostAsync($"/api/reception/sessions/{id}/tokens/{token}/recall",null));
        await Json(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/tokens/{token}/complete",null));
        Assert.Equal(HttpStatusCode.BadRequest,(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/tokens/{token}/complete",null)).StatusCode);
        await Json(await factory.Doctor.PostAsync($"/api/doctor/sessions/{id}/pause",null));
        await Json(await factory.Doctor.PostAsJsonAsync($"/api/doctor/sessions/{id}/complete",new {confirmSkipped=true}));
    }
    [Fact]
    public async Task QueuePagesHaveStableTotalsAndServerFilters()
    {
        var id=await Session(); for(var i=0;i<12;i++) await Json(await WalkIn(id));
        var details=await Json(await factory.Doctor.GetAsync($"/api/doctor/sessions/{id}?page=2"));
        Assert.Equal(12,details.GetProperty("totalCount").GetInt32()); Assert.Equal(2,details.GetProperty("queue").GetArrayLength());
        Assert.Equal(11,details.GetProperty("queue")[0].GetProperty("tokenNumber").GetInt32());
        var token=details.GetProperty("queue")[0].GetProperty("id").GetGuid();
        await Json(await factory.Reception.PostAsync($"/api/reception/sessions/{id}/tokens/{token}/cancel",null));
        var finished=await Json(await factory.Doctor.GetAsync($"/api/doctor/sessions/{id}?filter=Other%20finished"));
        Assert.Equal(1,finished.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest,(await factory.Doctor.GetAsync($"/api/doctor/sessions/{id}?filter=invalid")).StatusCode);
    }
    [Fact]
    public async Task HubAndDoctorEndpointsEnforceAuthenticationAndRoles()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,(await factory.Public.PostAsync("/hubs/queue/negotiate?negotiateVersion=1",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await factory.Reception.GetAsync("/api/doctor/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await factory.Doctor.PostAsync($"/api/doctor/sessions/{Guid.NewGuid()}/stop-booking",null)).StatusCode);
    }
    public sealed class CaptureMail : IStaffEmailSender
    {
        public string Token { get; private set; }="";
        public Task SendResetLinkAsync(string email,string link,CancellationToken ct)=>Task.CompletedTask;
        public Task SendInvitationLinkAsync(string email,string link,CancellationToken ct) { Token=link.Split("#token=")[1];return Task.CompletedTask; }
    }
    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public const string Password="VerificationOnly123!";
        public CaptureMail Mail { get; }=new();
        public HttpClient Public { get; private set; }=null!;
        public HttpClient Doctor { get; private set; }=null!;
        public HttpClient Reception { get; private set; }=null!;
        private readonly string database="DigiLine_Verification_Api_"+Guid.NewGuid().ToString("N");
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:DigiLine", $"Server=localhost;Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5");
            builder.ConfigureAppConfiguration((_, configuration)=>configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                ["ConnectionStrings:DigiLine"]=$"Server=localhost;Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5",
                ["JwtSettings:Issuer"]="DigiLine.Tests",["JwtSettings:Audience"]="DigiLine.Tests",["JwtSettings:SecretKey"]="DigiLine.Tests.SecretKey.AtLeast32Characters",["PasswordReset:PublicBaseUrl"]="https://localhost",["Logging:LogLevel:Default"]="Warning"
            }));
            builder.ConfigureServices(services=>{ services.RemoveAll<IStaffEmailSender>();services.AddSingleton<IStaffEmailSender>(Mail); });
        }
        public async Task InitializeAsync()
        {
            Public=CreateClient(new(){BaseAddress=new Uri("https://localhost")});Doctor=CreateClient(new(){BaseAddress=new Uri("https://localhost")});Reception=CreateClient(new(){BaseAddress=new Uri("https://localhost")});
            var register=await Json(await Public.PostAsJsonAsync("/api/auth/register",new {clinicName="Production journey",slug="production-journey",timeZoneId="Asia/Kolkata",name="Owner",email="owner@verification.local",password=Password,alsoDoctor=true}));
            Doctor.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",register.GetProperty("token").GetString());
            await Json(await Doctor.PostAsJsonAsync("/api/admin/staff",new {name="Reception",email="reception@verification.local",roles=new[]{2},password=Password}));
            var login=await Json(await Public.PostAsJsonAsync("/api/auth/login",new {email="reception@verification.local",password=Password}));
            Reception.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",login.GetProperty("token").GetString());
        }
        Task IAsyncLifetime.DisposeAsync() { Public?.Dispose();Doctor?.Dispose();Reception?.Dispose();return Task.CompletedTask; }
    }
}
