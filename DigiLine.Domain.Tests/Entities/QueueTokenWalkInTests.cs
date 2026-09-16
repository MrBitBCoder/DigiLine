using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Domain.Tests;

public class QueueTokenWalkInTests
{
    [Fact]
    public void Call_WhenWaiting_TransitionsToCalledAndSetsTimestamp()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);
        var calledAt = DateTime.UtcNow;

        token.Call(calledAt);

        Assert.Equal(QueueTokenStatus.Called, token.Status);
        Assert.Equal(calledAt, token.CalledAt);
    }

    [Fact]
    public void Call_WhenAlreadyCalled_ThrowsWithoutChangingTimestamp()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);
        var originalCalledAt = DateTime.UtcNow;
        token.Call(originalCalledAt);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            token.Call(originalCalledAt.AddMinutes(1)));

        Assert.Equal(
            "Only a waiting or late token can be called.",
            exception.Message);
        Assert.Equal(originalCalledAt, token.CalledAt);
    }

    [Fact]
    public void Skip_WhenCalled_TransitionsToSkippedAndPreservesCalledAt()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);
        var calledAt = DateTime.UtcNow;
        token.Call(calledAt);

        token.Skip();

        Assert.Equal(QueueTokenStatus.Skipped, token.Status);
        Assert.Equal(calledAt, token.CalledAt);
    }

    [Fact]
    public void Skip_WhenWaiting_Throws()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);

        var exception = Assert.Throws<InvalidOperationException>(token.Skip);

        Assert.Equal("Only a called token can be skipped.", exception.Message);
        Assert.Equal(QueueTokenStatus.Waiting, token.Status);
    }

    [Fact]
    public void Complete_WhenCalled_TransitionsToCompletedAndSetsTimestamp()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);
        var calledAt = DateTime.UtcNow.AddMinutes(-5);
        var completedAt = DateTime.UtcNow;
        token.Call(calledAt);

        token.Complete(completedAt);

        Assert.Equal(QueueTokenStatus.Completed, token.Status);
        Assert.Equal(calledAt, token.CalledAt);
        Assert.Equal(completedAt, token.CompletedAt);
    }

    [Fact]
    public void Complete_WhenWaiting_ThrowsWithoutSettingTimestamp()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            token.Complete(DateTime.UtcNow));

        Assert.Equal(
            "Only a called or in-consultation token can be completed.",
            exception.Message);
        Assert.Null(token.CompletedAt);
    }

    [Fact]
    public void Cancel_WhenWaiting_TransitionsToCancelled()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);

        token.Cancel();

        Assert.Equal(QueueTokenStatus.Cancelled, token.Status);
        Assert.Null(token.CalledAt);
        Assert.Null(token.CompletedAt);
    }

    [Fact]
    public void Cancel_WhenAlreadyCalled_Throws()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);
        token.Call(DateTime.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(token.Cancel);

        Assert.Equal("Only a waiting token can be cancelled.", exception.Message);
        Assert.Equal(QueueTokenStatus.Called, token.Status);
    }

    [Theory]
    [InlineData(QueueTokenStatus.Waiting)]
    [InlineData(QueueTokenStatus.Skipped)]
    [InlineData(QueueTokenStatus.Late)]
    public void CancelByPatient_WhenStillAwaitingService_FinalizesExistingToken(
        QueueTokenStatus status)
    {
        var token = TokenInStatus(status);
        var tokenId = token.Id;
        var tokenNumber = token.TokenNumber;
        var queueOrder = token.QueueOrder;
        var cancelledAt = DateTime.UtcNow;

        token.CancelByPatient(cancelledAt);

        Assert.Equal(QueueTokenStatus.Cancelled, token.Status);
        Assert.Equal(cancelledAt, token.FinalizedAt);
        Assert.Equal(tokenId, token.Id);
        Assert.Equal(tokenNumber, token.TokenNumber);
        Assert.Equal(queueOrder, token.QueueOrder);
    }

    [Theory]
    [InlineData(QueueTokenStatus.Called)]
    [InlineData(QueueTokenStatus.Completed)]
    [InlineData(QueueTokenStatus.NoShow)]
    public void CancelByPatient_WhenServiceStartedOrFinalized_Rejects(
        QueueTokenStatus status)
    {
        var token = TokenInStatus(status);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            token.CancelByPatient(DateTime.UtcNow));

        Assert.Equal("This token can no longer be cancelled.", exception.Message);
        Assert.Equal(status, token.Status);
        if (status != QueueTokenStatus.NoShow)
            Assert.Null(token.FinalizedAt);
    }

    [Fact]
    public void CancelByPatient_WhenAlreadyCancelled_ReturnsSpecificConflict()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, Guid.NewGuid());
        token.CancelByPatient(DateTime.UtcNow.AddMinutes(-1));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            token.CancelByPatient(DateTime.UtcNow));

        Assert.Equal("This token has already been cancelled.", exception.Message);
    }

    private static QueueToken TokenInStatus(QueueTokenStatus status)
    {
        var token = new QueueToken(Guid.NewGuid(), 8, Guid.NewGuid());
        if (status == QueueTokenStatus.Waiting) return token;
        token.Call(DateTime.UtcNow.AddMinutes(-5));
        if (status == QueueTokenStatus.Called) return token;
        if (status == QueueTokenStatus.Completed)
        {
            token.Complete(DateTime.UtcNow);
            return token;
        }
        token.Skip();
        if (status == QueueTokenStatus.Skipped) return token;
        if (status == QueueTokenStatus.NoShow)
        {
            token.FinalizeNoShow(DateTime.UtcNow);
            return token;
        }
        token.MarkLate();
        return token;
    }

    [Fact]
    public void MarkLate_WhenSkipped_TransitionsToLate()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);
        token.Call(DateTime.UtcNow);
        token.Skip();

        token.MarkLate();

        Assert.Equal(QueueTokenStatus.Late, token.Status);
        Assert.NotNull(token.LateArrivedAt);
        Assert.Equal(DateTimeKind.Utc, token.LateArrivedAt!.Value.Kind);
        Assert.Throws<InvalidOperationException>(() => token.MarkLate());
    }

    [Fact]
    public void MarkLate_WhenTokenWasNotSkipped_Throws()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);

        var exception = Assert.Throws<InvalidOperationException>(() => token.MarkLate());

        Assert.Equal("Only a skipped token can be marked late.", exception.Message);
        Assert.Equal(QueueTokenStatus.Waiting, token.Status);
    }

    [Fact]
    public void Call_WhenLate_TransitionsBackToCalled()
    {
        var token = new QueueToken(Guid.NewGuid(), 1, "Jane Doe", null);
        token.Call(DateTime.UtcNow.AddMinutes(-10));
        token.Skip();
        token.MarkLate();
        var arrivedAt = token.LateArrivedAt;
        var calledAgainAt = DateTime.UtcNow;

        token.Call(calledAgainAt);
        Assert.Equal(arrivedAt, token.LateArrivedAt);

        Assert.Equal(QueueTokenStatus.Called, token.Status);
        Assert.Equal(calledAgainAt, token.CalledAt);
    }

    [Fact]
    public void Constructor_WithValidDetails_CreatesWaitingWalkInToken()
    {
        var sessionId = Guid.NewGuid();

        var token = new QueueToken(
            sessionId,
            7,
            "  Jane Doe  ",
            "  9876543210  ");

        Assert.Equal(sessionId, token.SessionId);
        Assert.Equal(7, token.TokenNumber);
        Assert.Null(token.PatientProfileId);
        Assert.Equal("Jane Doe", token.WalkInName);
        Assert.Equal("9876543210", token.PhoneNumber);
        Assert.Equal(QueueTokenStatus.Waiting, token.Status);
    }

    [Fact]
    public void Constructor_WithBlankOptionalPhone_StoresNull()
    {
        var token = new QueueToken(
            Guid.NewGuid(),
            1,
            "Jane Doe",
            "   ");

        Assert.Null(token.PhoneNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string walkInName)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new QueueToken(Guid.NewGuid(), 1, walkInName, null));

        Assert.Equal("Walk-in name is required.", exception.Message);
    }

    [Fact]
    public void Constructor_WithNameOverLimit_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new QueueToken(Guid.NewGuid(), 1, new string('a', 201), null));

        Assert.Equal(
            "Walk-in name cannot exceed 200 characters.",
            exception.Message);
    }

    [Fact]
    public void Constructor_WithPhoneOverLimit_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new QueueToken(Guid.NewGuid(), 1, "Jane Doe", new string('1', 21)));

        Assert.Equal(
            "Phone number cannot exceed 20 characters.",
            exception.Message);
    }
}
