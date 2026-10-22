using Microsoft.AspNetCore.SignalR;
using Riftbound.GodotClient;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RoomSnapshotRequestTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConnectionLostBeforeOrDuringJoinDoesNotRequestAnObsoleteSnapshot(bool duringJoin)
    {
        var ready = duringJoin;
        var joined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var joins = 0;
        var snapshots = 0;
        var pending = RoomSnapshotRequest.RunAsync(async () =>
        {
            joins++;
            joined.SetResult();
            await release.Task;
        }, () => { snapshots++; return Task.CompletedTask; }, () => ready);
        if (duringJoin)
        {
            await joined.Task.WaitAsync(TimeSpan.FromSeconds(10));
            ready = false;
        }
        release.SetResult();
        Assert.False(await pending);
        Assert.Equal(duringJoin ? 1 : 0, joins);
        Assert.Equal(0, snapshots);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("client")]
    [InlineData("room")]
    [InlineData("player")]
    public async Task CompletedJoinCannotRefreshAReplacedConnectionOrTarget(string changedPart)
    {
        var client = new object();
        var currentClient = client;
        const string connection = "connection-one", room = "room-one", player = "player-one";
        var currentConnection = connection;
        var currentRoom = room;
        var currentPlayer = player;
        var joined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var joins = 0;
        var snapshots = 0;
        var pending = RoomSnapshotRequest.RunAsync(async () =>
        {
            joins++;
            joined.SetResult();
            await release.Task;
        }, () => { snapshots++; return Task.CompletedTask; }, () =>
            ReferenceEquals(client, currentClient) && connection == currentConnection
            && room == currentRoom && player == currentPlayer);
        await joined.Task.WaitAsync(TimeSpan.FromSeconds(10));
        switch (changedPart)
        {
            case "connection": currentConnection = "connection-two"; break;
            case "client": currentClient = new object(); break;
            case "room": currentRoom = "room-two"; break;
            case "player": currentPlayer = "player-two"; break;
            default: throw new InvalidOperationException("Unknown context change.");
        }
        release.SetResult();
        Assert.False(await pending);
        Assert.Equal(1, joins);
        Assert.Equal(0, snapshots);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InFlightSnapshotInterruptedByLossOrFastReconnectDoesNotRetry(bool alreadyRestored)
    {
        var ready = true;
        const string connection = "original-connection";
        var currentConnection = connection;
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var joins = 0;
        var snapshots = 0;
        var pending = RoomSnapshotRequest.RunAsync(() => { joins++; return Task.CompletedTask; }, async () =>
        {
            snapshots++;
            requested.SetResult();
            await reply.Task;
        }, () => ready && currentConnection == connection);
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (alreadyRestored) currentConnection = "restored-connection";
        else ready = false;
        reply.SetException(new InvalidOperationException("The connection is not active."));
        Assert.False(await pending);
        Assert.Equal(1, joins);
        Assert.Equal(1, snapshots);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ServerRejectionStillPropagatesWhenConnectionChanges(bool connected)
    {
        var ready = true;
        var rejected = new HubException("INVALID_RECONNECT_TOKEN");
        var error = await Assert.ThrowsAsync<HubException>(() => RoomSnapshotRequest.RunAsync(
            () => Task.CompletedTask,
            () => { ready = connected; return Task.FromException(rejected); },
            () => ready));
        Assert.Same(rejected, error);
    }

    [Fact]
    public async Task InvalidOperationOnCurrentActiveConnectionStillPropagates()
    {
        var failure = new InvalidOperationException("An active request failed.");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => RoomSnapshotRequest.RunAsync(
            () => Task.CompletedTask, () => Task.FromException(failure), () => true));
        Assert.Same(failure, error);
    }

    [Fact]
    public async Task SnapshotCancellationKeepsTheOriginalCancellationToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RoomSnapshotRequest.RunAsync(
            () => Task.CompletedTask, () => Task.FromCanceled(cancelled.Token), () => true));
        Assert.Equal(cancelled.Token, error.CancellationToken);
    }

    [Fact]
    public async Task CurrentJoinRequestsExactlyOneSnapshotAfterJoining()
    {
        var joins = 0;
        var snapshots = 0;
        Assert.True(await RoomSnapshotRequest.RunAsync(
            () => { joins++; return Task.CompletedTask; },
            () => { Assert.Equal(1, joins); snapshots++; return Task.CompletedTask; },
            () => true));
        Assert.Equal(1, joins);
        Assert.Equal(1, snapshots);
    }
}
