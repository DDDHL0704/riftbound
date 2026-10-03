using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LobbyPresenceTests
{
    [Fact]
    public async Task DisconnectedWaiterIsNeverPairedWithNextPlayer()
    {
        var presence = new PlayerConnections();
        var sessions = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var queue = new InMemoryMatchmakingQueue(sessions, () => "RB-PRESENCE", presence);
        presence.Register("alice-old", "alice");
        Assert.Equal(MatchmakingStates.Queued, (await queue.EnqueueAsync("alice", default)).State);
        presence.Disconnect("alice-old");
        presence.Register("bob", "bob");
        // This must work even before the hub's asynchronous cleanup runs.
        Assert.Equal(MatchmakingStates.Queued, (await queue.EnqueueAsync("bob", default)).State);
    }

    [Fact]
    public async Task OldDisconnectCannotCancelAReconnectedPlayersQueueEntry()
    {
        var presence = new PlayerConnections();
        var sessions = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var queue = new InMemoryMatchmakingQueue(sessions, () => "RB-PRESENCE", presence);
        presence.Register("alice-old", "alice");
        await queue.EnqueueAsync("alice", default);
        presence.Disconnect("alice-old");
        presence.Register("alice-new", "alice");
        await queue.RemoveDisconnectedAsync("alice", default);
        presence.Register("bob", "bob");
        var result = await queue.EnqueueAsync("bob", default);
        Assert.Equal(MatchmakingStates.Matched, result.State);
        Assert.Equal("alice", result.OpponentPlayerId);
    }

    [Fact]
    public async Task PublicRoomRequiresHostPresenceInThatSpecificRoom()
    {
        var presence = new PlayerConnections();
        var directory = new InMemoryPublicMatchDirectory(() => DateTimeOffset.UtcNow, presence);
        presence.EnterRoom("host", "alice", "RB-PUBLIC");
        await directory.CreateAsync("RB-PUBLIC", "alice", default);
        Assert.Single(await directory.ListOpenAsync(default));
        presence.Disconnect("host");
        Assert.Empty(await directory.ListOpenAsync(default));
        presence.Register("new-host", "alice");
        Assert.Empty(await directory.ListOpenAsync(default));
        presence.EnterRoom("new-host", "alice", "RB-OTHER");
        Assert.Empty(await directory.ListOpenAsync(default));
        presence.EnterRoom("new-host", "alice", "RB-PUBLIC");
        Assert.Single(await directory.ListOpenAsync(default));
        await directory.NotifyPlayerJoinedAsync("RB-PUBLIC", "bob", default);
        Assert.Empty(await directory.ListOpenAsync(default));
    }

    [Fact]
    public void ClosingOneWindowDoesNotMarkOtherWindowOffline()
    {
        var presence = new PlayerConnections();
        presence.EnterRoom("a", "alice", "RB-PUBLIC");
        presence.EnterRoom("b", "alice", "RB-PUBLIC");
        presence.Disconnect("a");
        Assert.True(presence.IsConnected("alice"));
        Assert.True(presence.IsInRoom("alice", "RB-PUBLIC"));
        presence.Disconnect("b");
        Assert.False(presence.IsConnected("alice"));
    }
}
