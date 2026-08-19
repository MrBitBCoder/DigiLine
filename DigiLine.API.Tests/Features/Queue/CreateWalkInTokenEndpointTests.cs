using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DigiLine.Application.Queue;
using DigiLine.Domain.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DigiLine.API.Tests;

public class CreateWalkInTokenEndpointTests
    : IClassFixture<CreateWalkInTokenEndpointTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public CreateWalkInTokenEndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostWalkIn_WithValidRequest_ReturnsAllocatedTokenNumber()
    {
        var sessionId = Guid.NewGuid();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/queue/walk-in",
            new
            {
                sessionId,
                walkInName = "Jane Doe",
                phoneNumber = "9876543210"
            });

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(result);
        Assert.Equal(42, result.TokenNumber);
        Assert.Equal(sessionId, _factory.Service.SessionId);
        Assert.Equal("Jane Doe", _factory.Service.WalkInName);
        Assert.Equal("9876543210", _factory.Service.PhoneNumber);
    }

    [Fact]
    public async Task PostWalkIn_WithBlankName_ReturnsBadRequestWithoutCallingService()
    {
        _factory.Service.Reset();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/queue/walk-in",
            new
            {
                sessionId = Guid.NewGuid(),
                walkInName = "",
                phoneNumber = (string?)null
            });

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            await response.Content.ReadAsStringAsync());
        Assert.False(_factory.Service.WasCalled);
    }

    [Fact]
    public async Task GetSessionQueue_ReturnsBackendOrderedTokens()
    {
        var sessionId = Guid.NewGuid();
        _factory.OrderedQueueService.Result =
        [
            new QueueTokenListItem(
                Guid.NewGuid(), 1, Guid.NewGuid(), null, null,
                QueueTokenStatus.Waiting, DateTime.UtcNow),
            new QueueTokenListItem(
                Guid.NewGuid(), 2, null, "Walk-in", "12345",
                QueueTokenStatus.Waiting, DateTime.UtcNow)
        ];
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/queue/session/{sessionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<List<QueueTokenListItem>>();
        Assert.NotNull(result);
        Assert.Equal(new[] { 1, 2 }, result.Select(item => item.TokenNumber));
        Assert.Equal(sessionId, _factory.OrderedQueueService.SessionId);
    }

    [Fact]
    public async Task GetSessionQueue_WhenSessionDoesNotExist_ReturnsBadRequest()
    {
        _factory.OrderedQueueService.Exception =
            new InvalidOperationException("Session was not found.");
        var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/queue/session/{Guid.NewGuid()}");

        _factory.OrderedQueueService.Exception = null;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCallNext_ReturnsCalledToken()
    {
        var sessionId = Guid.NewGuid();
        _factory.CallNextService.Result = new QueueTokenListItem(
            Guid.NewGuid(),
            5,
            null,
            "Next Walk-in",
            null,
            QueueTokenStatus.Called,
            DateTime.UtcNow);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{sessionId}/call-next",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<QueueTokenListItem>();
        Assert.NotNull(result);
        Assert.Equal(5, result.TokenNumber);
        Assert.Equal(QueueTokenStatus.Called, result.Status);
        Assert.Equal(sessionId, _factory.CallNextService.SessionId);
    }

    [Fact]
    public async Task PostCallNext_WhenQueueIsEmpty_ReturnsBadRequest()
    {
        _factory.CallNextService.Exception = new InvalidOperationException(
            "There are no callable tokens in this session.");
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{Guid.NewGuid()}/call-next",
            null);

        _factory.CallNextService.Exception = null;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRecall_ReturnsRecalledToken()
    {
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var recalledAt = DateTime.UtcNow;
        _factory.RecallService.Result = new QueueTokenListItem(
            tokenId, 5, null, "Recalled Walk-in", null,
            QueueTokenStatus.Called, recalledAt.AddMinutes(-10), recalledAt);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{sessionId}/token/{tokenId}/recall",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<QueueTokenListItem>();
        Assert.NotNull(result);
        Assert.Equal(tokenId, result.Id);
        Assert.Equal(recalledAt, result.CalledAt);
        Assert.Equal(sessionId, _factory.RecallService.SessionId);
        Assert.Equal(tokenId, _factory.RecallService.TokenId);
    }

    [Fact]
    public async Task PostRecall_WhenTokenCannotBeRecalled_ReturnsBadRequest()
    {
        _factory.RecallService.Exception = new InvalidOperationException(
            "Only a called token can be recalled.");
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{Guid.NewGuid()}/token/{Guid.NewGuid()}/recall",
            null);

        _factory.RecallService.Exception = null;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSkip_ReturnsSkippedToken()
    {
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        _factory.SkipService.Result = new QueueTokenListItem(
            tokenId, 5, null, "Skipped Walk-in", null,
            QueueTokenStatus.Skipped, DateTime.UtcNow, DateTime.UtcNow);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{sessionId}/token/{tokenId}/skip",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<QueueTokenListItem>();
        Assert.NotNull(result);
        Assert.Equal(tokenId, result.Id);
        Assert.Equal(QueueTokenStatus.Skipped, result.Status);
        Assert.Equal(sessionId, _factory.SkipService.SessionId);
        Assert.Equal(tokenId, _factory.SkipService.TokenId);
    }

    [Fact]
    public async Task PostSkip_WhenTokenCannotBeSkipped_ReturnsBadRequest()
    {
        _factory.SkipService.Exception = new InvalidOperationException(
            "Only a called token can be skipped.");
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{Guid.NewGuid()}/token/{Guid.NewGuid()}/skip",
            null);

        _factory.SkipService.Exception = null;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostComplete_ReturnsCompletedToken()
    {
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;
        _factory.CompleteService.Result = new QueueTokenListItem(
            tokenId, 5, null, "Completed Walk-in", null,
            QueueTokenStatus.Completed, completedAt.AddMinutes(-10),
            completedAt.AddMinutes(-5), completedAt);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{sessionId}/token/{tokenId}/complete",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<QueueTokenListItem>();
        Assert.NotNull(result);
        Assert.Equal(tokenId, result.Id);
        Assert.Equal(QueueTokenStatus.Completed, result.Status);
        Assert.Equal(completedAt, result.CompletedAt);
        Assert.Equal(sessionId, _factory.CompleteService.SessionId);
        Assert.Equal(tokenId, _factory.CompleteService.TokenId);
    }

    [Fact]
    public async Task PostComplete_WhenTransitionIsInvalid_ReturnsBadRequest()
    {
        _factory.CompleteService.Exception = new InvalidOperationException(
            "Only a called or in-consultation token can be completed.");
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{Guid.NewGuid()}/token/{Guid.NewGuid()}/complete",
            null);

        _factory.CompleteService.Exception = null;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCancel_ReturnsCancelledToken()
    {
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        _factory.CancelService.Result = new QueueTokenListItem(
            tokenId, 5, null, "Cancelled Walk-in", null,
            QueueTokenStatus.Cancelled, DateTime.UtcNow);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{sessionId}/token/{tokenId}/cancel",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<QueueTokenListItem>();
        Assert.NotNull(result);
        Assert.Equal(tokenId, result.Id);
        Assert.Equal(QueueTokenStatus.Cancelled, result.Status);
        Assert.Equal(sessionId, _factory.CancelService.SessionId);
        Assert.Equal(tokenId, _factory.CancelService.TokenId);
    }

    [Fact]
    public async Task PostCancel_WhenTransitionIsInvalid_ReturnsBadRequest()
    {
        _factory.CancelService.Exception = new InvalidOperationException(
            "Only a waiting token can be cancelled.");
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{Guid.NewGuid()}/token/{Guid.NewGuid()}/cancel",
            null);

        _factory.CancelService.Exception = null;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostLate_ReturnsLateToken()
    {
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        _factory.MarkLateService.Result = new QueueTokenListItem(
            tokenId, 5, null, "Late Walk-in", null,
            QueueTokenStatus.Late, DateTime.UtcNow, DateTime.UtcNow);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{sessionId}/token/{tokenId}/late",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<QueueTokenListItem>();
        Assert.NotNull(result);
        Assert.Equal(tokenId, result.Id);
        Assert.Equal(QueueTokenStatus.Late, result.Status);
        Assert.Equal(sessionId, _factory.MarkLateService.SessionId);
        Assert.Equal(tokenId, _factory.MarkLateService.TokenId);
    }

    [Fact]
    public async Task PostLate_WhenTransitionIsInvalid_ReturnsBadRequest()
    {
        _factory.MarkLateService.Exception = new InvalidOperationException(
            "Only a skipped token can be marked late.");
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/queue/session/{Guid.NewGuid()}/token/{Guid.NewGuid()}/late",
            null);

        _factory.MarkLateService.Exception = null;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPosition_ReturnsPositionAndPatientsAhead()
    {
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        _factory.PositionService.Result = new QueuePositionResult(
            tokenId, 8, QueueTokenStatus.Waiting, 4, 3);
        var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/queue/session/{sessionId}/token/{tokenId}/position");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<QueuePositionResult>();
        Assert.NotNull(result);
        Assert.Equal(4, result.Position);
        Assert.Equal(3, result.PatientsAhead);
        Assert.Equal(sessionId, _factory.PositionService.SessionId);
        Assert.Equal(tokenId, _factory.PositionService.TokenId);
    }

    [Fact]
    public async Task GetPosition_WhenTokenIsInactive_ReturnsBadRequest()
    {
        _factory.PositionService.Exception = new InvalidOperationException(
            "Queue position is only available for waiting or late tokens.");
        var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/queue/session/{Guid.NewGuid()}/token/{Guid.NewGuid()}/position");

        _factory.PositionService.Exception = null;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEstimatedWait_ReturnsBackendCalculation()
    {
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        _factory.EstimatedWaitService.Result = new EstimatedWaitResult(
            tokenId, 18, QueueTokenStatus.Waiting, 6, 5, 8, false, 40);
        var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/queue/session/{sessionId}/token/{tokenId}/estimated-wait");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<EstimatedWaitResult>();
        Assert.NotNull(result);
        Assert.Equal(40, result.EstimatedWaitMinutes);
        Assert.Equal(8, result.AverageConsultationMinutes);
        Assert.False(result.IsPaused);
        Assert.Equal(sessionId, _factory.EstimatedWaitService.SessionId);
        Assert.Equal(tokenId, _factory.EstimatedWaitService.TokenId);
    }

    [Fact]
    public async Task GetEstimatedWait_WhenPositionIsUnavailable_ReturnsBadRequest()
    {
        _factory.EstimatedWaitService.Exception = new InvalidOperationException(
            "Queue position is only available for waiting or late tokens.");
        var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/queue/session/{Guid.NewGuid()}/token/{Guid.NewGuid()}/estimated-wait");

        _factory.EstimatedWaitService.Exception = null;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QueueHub_Negotiate_ReturnsConnectionAndTransports()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/hubs/queue/negotiate?negotiateVersion=1",
            new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(
            root.GetProperty("connectionId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            root.GetProperty("connectionToken").GetString()));
        Assert.Contains(
            root.GetProperty("availableTransports").EnumerateArray(),
            transport => transport.GetProperty("transport").GetString()
                == "WebSockets");
    }

    [Fact]
    public void QueueHub_SessionGroup_IsStableAndSessionScoped()
    {
        var sessionId = Guid.Parse("f91d07ef-bfd7-4f61-b6bb-318d45e02f75");

        var group = DigiLine.API.Realtime.QueueHubContract
            .SessionGroup(sessionId);

        Assert.Equal(
            "session:f91d07efbfd74f61b6bb318d45e02f75",
            group);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public StubCreateWalkInTokenService Service { get; } = new();
        public StubOrderedQueueService OrderedQueueService { get; } = new();
        public StubCallNextTokenService CallNextService { get; } = new();
        public StubRecallTokenService RecallService { get; } = new();
        public StubSkipTokenService SkipService { get; } = new();
        public StubCompleteTokenService CompleteService { get; } = new();
        public StubCancelTokenService CancelService { get; } = new();
        public StubMarkTokenLateService MarkLateService { get; } = new();
        public StubGetQueuePositionService PositionService { get; } = new();
        public StubGetEstimatedWaitService EstimatedWaitService { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["JwtSettings:Issuer"] = "DigiLine.Tests",
                        ["JwtSettings:Audience"] = "DigiLine.Tests",
                        ["JwtSettings:SecretKey"] =
                            "DigiLine.Tests.SecretKey.AtLeast32Characters"
                    });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICreateWalkInTokenService>();
                services.AddSingleton<ICreateWalkInTokenService>(Service);
                services.RemoveAll<IGetOrderedQueueService>();
                services.AddSingleton<IGetOrderedQueueService>(OrderedQueueService);
                services.RemoveAll<ICallNextTokenService>();
                services.AddSingleton<ICallNextTokenService>(CallNextService);
                services.RemoveAll<IRecallTokenService>();
                services.AddSingleton<IRecallTokenService>(RecallService);
                services.RemoveAll<ISkipTokenService>();
                services.AddSingleton<ISkipTokenService>(SkipService);
                services.RemoveAll<ICompleteTokenService>();
                services.AddSingleton<ICompleteTokenService>(CompleteService);
                services.RemoveAll<ICancelTokenService>();
                services.AddSingleton<ICancelTokenService>(CancelService);
                services.RemoveAll<IMarkTokenLateService>();
                services.AddSingleton<IMarkTokenLateService>(MarkLateService);
                services.RemoveAll<IGetQueuePositionService>();
                services.AddSingleton<IGetQueuePositionService>(PositionService);
                services.RemoveAll<IGetEstimatedWaitService>();
                services.AddSingleton<IGetEstimatedWaitService>(EstimatedWaitService);
            });
        }
    }

    public sealed class StubGetEstimatedWaitService : IGetEstimatedWaitService
    {
        public Guid SessionId { get; private set; }
        public Guid TokenId { get; private set; }
        public EstimatedWaitResult Result { get; set; } = null!;
        public Exception? Exception { get; set; }

        public Task<EstimatedWaitResult> GetAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            TokenId = tokenId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    public sealed class StubGetQueuePositionService : IGetQueuePositionService
    {
        public Guid SessionId { get; private set; }
        public Guid TokenId { get; private set; }
        public QueuePositionResult Result { get; set; } = null!;
        public Exception? Exception { get; set; }

        public Task<QueuePositionResult> GetAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            TokenId = tokenId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    public sealed class StubMarkTokenLateService : IMarkTokenLateService
    {
        public Guid SessionId { get; private set; }
        public Guid TokenId { get; private set; }
        public QueueTokenListItem Result { get; set; } = null!;
        public Exception? Exception { get; set; }

        public Task<QueueTokenListItem> MarkLateAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            TokenId = tokenId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    public sealed class StubCancelTokenService : ICancelTokenService
    {
        public Guid SessionId { get; private set; }
        public Guid TokenId { get; private set; }
        public QueueTokenListItem Result { get; set; } = null!;
        public Exception? Exception { get; set; }

        public Task<QueueTokenListItem> CancelAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            TokenId = tokenId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    public sealed class StubCompleteTokenService : ICompleteTokenService
    {
        public Guid SessionId { get; private set; }
        public Guid TokenId { get; private set; }
        public QueueTokenListItem Result { get; set; } = null!;
        public Exception? Exception { get; set; }

        public Task<QueueTokenListItem> CompleteAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            TokenId = tokenId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    public sealed class StubSkipTokenService : ISkipTokenService
    {
        public Guid SessionId { get; private set; }
        public Guid TokenId { get; private set; }
        public QueueTokenListItem Result { get; set; } = null!;
        public Exception? Exception { get; set; }

        public Task<QueueTokenListItem> SkipAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            TokenId = tokenId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    public sealed class StubRecallTokenService : IRecallTokenService
    {
        public Guid SessionId { get; private set; }
        public Guid TokenId { get; private set; }
        public QueueTokenListItem Result { get; set; } = null!;
        public Exception? Exception { get; set; }

        public Task<QueueTokenListItem> RecallAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            TokenId = tokenId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    public sealed class StubCallNextTokenService : ICallNextTokenService
    {
        public Guid SessionId { get; private set; }
        public QueueTokenListItem Result { get; set; } = null!;
        public Exception? Exception { get; set; }

        public Task<QueueTokenListItem> CallAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    public sealed class StubOrderedQueueService : IGetOrderedQueueService
    {
        public Guid SessionId { get; private set; }
        public IReadOnlyList<QueueTokenListItem> Result { get; set; } = [];
        public Exception? Exception { get; set; }

        public Task<IReadOnlyList<QueueTokenListItem>> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    public sealed class StubCreateWalkInTokenService
        : ICreateWalkInTokenService
    {
        public bool WasCalled { get; private set; }
        public Guid SessionId { get; private set; }
        public string? WalkInName { get; private set; }
        public string? PhoneNumber { get; private set; }

        public Task<int> CreateAsync(
            Guid sessionId,
            string walkInName,
            string? phoneNumber,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            SessionId = sessionId;
            WalkInName = walkInName;
            PhoneNumber = phoneNumber;
            return Task.FromResult(42);
        }

        public void Reset()
        {
            WasCalled = false;
            SessionId = Guid.Empty;
            WalkInName = null;
            PhoneNumber = null;
        }
    }

    private sealed record TokenResponse(int TokenNumber);
}
