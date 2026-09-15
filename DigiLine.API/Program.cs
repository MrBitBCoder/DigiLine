using DigiLine.API.Development;
using DigiLine.API.Middleware;
using DigiLine.API.Realtime;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Application.Clinics;
using DigiLine.Application.Patients;
using DigiLine.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using DigiLine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration
            .GetSection("JwtSettings");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    jwtSettings["SecretKey"]!))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Principal?.FindFirstValue("sub");
                var version = context.Principal?.FindFirstValue("password_version");
                if (!Guid.TryParse(subject, out var staffId) || !Guid.TryParse(version, out var passwordVersion))
                { context.Fail("Invalid staff session."); return; }
                var db = context.HttpContext.RequestServices.GetRequiredService<DigiLineDbContext>();
                if (!await db.Staff.AnyAsync(staff => staff.Id == staffId && staff.IsActive && !staff.InvitationPending && staff.PasswordVersion == passwordVersion,
                        context.HttpContext.RequestAborted))
                    context.Fail("Staff session has been revoked.");
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/queue"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("password-reset", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    options.AddPolicy("patient-recovery", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));


builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<StaffSubscriptions>();
builder.Services.AddSingleton<IQueueEventPublisher, SignalRQueueEventPublisher>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


builder.Services.AddScoped<ISessionLifecycleService, SessionLifecycleService>();
builder.Services.AddScoped<ICreateDoctorSessionService, CreateDoctorSessionService>();
builder.Services.AddScoped<IManageDoctorSessionService, ManageDoctorSessionService>();
builder.Services.AddScoped<IGetDoctorSessionsService, GetDoctorSessionsService>();
builder.Services.AddScoped<IGetReceptionDashboardService, GetReceptionDashboardService>();
builder.Services.AddScoped<IGetPublicClinicService, GetPublicClinicService>();
builder.Services.AddScoped<IPublicPatientService, PublicPatientService>();
builder.Services.AddScoped<IPublicQueueService, PublicQueueService>();
builder.Services.AddScoped<IBookTokenService, BookTokenService>();
builder.Services.AddScoped<ICreateWalkInTokenService, CreateWalkInTokenService>();
builder.Services.AddScoped<ICallNextTokenService, CallNextTokenService>();
builder.Services.AddScoped<IRecallTokenService, RecallTokenService>();
builder.Services.AddScoped<ISkipTokenService, SkipTokenService>();
builder.Services.AddScoped<ICompleteTokenService, CompleteTokenService>();
builder.Services.AddScoped<ICancelTokenService, CancelTokenService>();
builder.Services.AddScoped<IMarkTokenLateService, MarkTokenLateService>();
builder.Services.AddScoped<IGetQueuePositionService, GetQueuePositionService>();
builder.Services.AddScoped<IGetEstimatedWaitService, GetEstimatedWaitService>();


builder.Services.AddExceptionHandler<BookingExceptionHandler>();
builder.Services.AddProblemDetails();


var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<DigiLineDbContext>();

    await dbContext.Database.MigrateAsync();
    var defaultZone = builder.Configuration["ClinicTime:DefaultTimeZoneId"] ?? "Asia/Kolkata";
    _ = TimeZoneInfo.FindSystemTimeZoneById(defaultZone);
    await dbContext.Clinics.Where(clinic => clinic.TimeZoneId == "")
        .ExecuteUpdateAsync(setters => setters.SetProperty(clinic => clinic.TimeZoneId, defaultZone));
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{

    await DevelopmentDataSeeder.SeedAsync(app.Services);
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();


app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseCors("Frontend");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();
app.MapHub<QueueHub>("/hubs/queue", options => options.CloseOnAuthenticationExpiration = true);
app.MapHub<PublicQueueHub>("/hubs/public-queue");

app.Run();

public partial class Program;
