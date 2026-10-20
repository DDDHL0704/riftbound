using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialInsightAndSpellLockTests;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
    [Theory]
    [InlineData("Alice", "alice")]
    [InlineData(" ALICE ", "aLiCe")]
    [InlineData("aLiCe", " ALICE ")]
    public async Task AuthenticatedRoomAliasesCreateOneCanonicalSeatAndCanonicalPrivateGroups(string firstAlias, string secondAlias)
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        var hub = CreateHub(clients, groups, "canonical-first", registry, playerIdentity: identity);
        Assert.True((await hub.Authenticate(firstAlias, "canonical-owner-key-1234")).Authenticated);
        await hub.JoinRoom("canonical-room", firstAlias);
        await hub.JoinRoom("canonical-room", secondAlias);
        await hub.RequestSnapshot("canonical-room", secondAlias);
        Assert.Equal(2, clients.CallerClient.JoinedMessages.Count);
        Assert.All(clients.CallerClient.JoinedMessages, message =>
        {
            Assert.Equal("alice", message.PlayerId);
            Assert.Equal("alice", Assert.IsType<PlayerSessionDto>(message.Payload).PlayerId);
        });
        Assert.Empty(clients.CallerClient.Errors);
        var session = Assert.IsAssignableFrom<IMatchSession>(await registry.FindAsync("canonical-room", default));
        var snapshot = JsonSerializer.SerializeToElement(session.SnapshotFor("alice"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("alice", Assert.Single(snapshot.GetProperty("players").EnumerateObject()).Name);
        Assert.All(clients.CallerClient.Snapshots, message => Assert.Equal("alice", message.PlayerId));
        Assert.All(clients.CallerClient.Prompts, message => Assert.Equal("alice", message.PlayerId));
        Assert.Contains(("canonical-first", "room:canonical-room:player:alice"), groups.Added);
        Assert.All(groups.Added.Where(group => group.GroupName.Contains(":player:", StringComparison.Ordinal)),
            group => Assert.Equal("room:canonical-room:player:alice", group.GroupName));
    }

    [Theory]
    [InlineData("ALICE", " alice ")]
    [InlineData(" Alice ", "aLiCe")]
    public async Task CanonicalRoomAliasRestoresPrivateOrderAndDeduplicatesTheSameActorIntent(string joinedAlias, string restoredAlias)
    {
        var played = await Act(CanonicalIdentityChoicePosition("alice", "bob"), "alice", new PlayCardCommand("SPELL", "VEN·056", []));
        var opened = await ResolveTop(played.State);
        var choice = opened.State.PendingCardChoice!;
        var ordered = await Act(opened.State, "alice", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal("INSIGHT_ORDER", ordered.State.PendingCardChoice!.ChoiceWindow);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(ordered.State, new CoreRuleEngine(), journal);
        session.EnsurePlayer("alice");
        var registry = new FixedMatchSessionRegistry(session);
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var firstClients = new RecordingHubClients();
        var first = CreateHub(firstClients, new RecordingGroupManager(), "canonical-owner", registry, playerIdentity: identity);
        Assert.True((await first.Authenticate(joinedAlias, "canonical-owner-key-1234")).Authenticated);
        await first.JoinRoom(session.RoomId, joinedAlias);
        var token = Assert.IsType<PlayerSessionDto>(Assert.Single(firstClients.CallerClient.JoinedMessages).Payload).ReconnectToken;
        var clients = new RecordingHubClients();
        var second = CreateHub(clients, new RecordingGroupManager(), "canonical-restored", registry, playerIdentity: identity);
        Assert.True((await second.Authenticate(restoredAlias, "canonical-owner-key-1234")).Authenticated);
        await second.Reconnect(session.RoomId, restoredAlias, token);
        await second.RequestSnapshot(session.RoomId, joinedAlias);
        Assert.Equal("alice", Assert.Single(clients.CallerClient.JoinedMessages).PlayerId);
        Assert.Equal(2, clients.CallerClient.Snapshots.Count);
        var pending = ordered.State.PendingCardChoice!;
        var prompt = session.PromptFor("alice");
        var raw = JsonSerializer.SerializeToElement(new { cmdType = "CHOOSE_CARDS", choiceId = pending.ChoiceId,
            choiceWindow = pending.ChoiceWindow, chosenObjectIds = new[] { "D5", "D4", "D3", "D2", "D1" },
            promptId = prompt.PromptId, snapshotTick = prompt.SnapshotTick });
        var receipt = await second.SubmitIntent(session.RoomId, restoredAlias, "canonical-order", raw);
        Assert.True(receipt.Accepted, receipt.Message);
        var duplicate = await second.SubmitIntent(session.RoomId, joinedAlias, "canonical-order", raw);
        Assert.True(duplicate.Accepted, duplicate.Message);
        Assert.Empty(clients.CallerClient.Errors);
        Assert.Single(journal.Entries);
        Assert.Equal("alice", Assert.Single(journal.Entries).PlayerId);
        Assert.Equal(new[] { "D5", "D4" }, StringList(ZoneView(PlayerView(session.SnapshotFor("alice"), "alice"))["hand"]));
    }

    // A new controlled fixture built with canonical account IDs before any action;
    // historical accepted matches and their actor IDs are never translated.
    private static MatchState CanonicalIdentityChoicePosition(string first = "p1", string second = "p2")
    {
        var cards = new Dictionary<string, CardObjectState>
        {
            ["SPELL"] = new("SPELL", cardNo: "VEN·056", manaCost: 7, ownerId: first, controllerId: first, tags: [CardObjectTags.SpellCard]),
            ["COUNTER"] = new("COUNTER", cardNo: "OGN·064/298", ownerId: second, controllerId: second, tags: [CardObjectTags.SpellCard])
        };
        foreach (var i in Enumerable.Range(1, 7)) cards["D" + i] = new("D" + i, cardNo: "OGN·048/298", ownerId: first, controllerId: first, tags: [CardObjectTags.SpellCard]);
        foreach (var i in Enumerable.Range(1, 2)) cards["E" + i] = new("E" + i, cardNo: "OGN·048/298", ownerId: second, controllerId: second, tags: [CardObjectTags.SpellCard]);
        return new("canonical-identity-choice", 0, 3, first, new Dictionary<string, string> { [first] = "P1", [second] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: [first, second], turnPlayerId: first, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            cardObjects: cards, playerZones: new Dictionary<string, PlayerZones> {
                [first] = PlayerZones.Empty with { Hand = ["SPELL"], MainDeck = Enumerable.Range(1, 7).Select(i => "D" + i).ToArray() },
                [second] = PlayerZones.Empty with { Hand = ["COUNTER"], MainDeck = ["E1", "E2"] } },
            playerScores: new Dictionary<string, int> { [first] = 0, [second] = 0 },
            runePools: new Dictionary<string, RunePool> { [first] = new(30, 30), [second] = new(30, 30) });
    }
}
