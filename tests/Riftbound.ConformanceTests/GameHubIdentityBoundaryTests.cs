using System.Text.Json;
using Riftbound.Api.Hubs;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialInsightAndSpellLockTests;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
    public static IEnumerable<object[]> RoomIdentityBoundaryCases()
    {
        foreach (var identity in new[] { "ANONYMOUS", "FAILED_KEY", "OTHER_HANDLE" })
        foreach (var action in new[] { "JOIN", "RECONNECT", "SNAPSHOT", "SEED", "READY", "PASS", "END_TURN", "INTENT" })
            yield return [identity, action];
    }

    [Theory]
    [MemberData(nameof(RoomIdentityBoundaryCases))]
    public async Task RoomIdentityBoundaryProtectsPrivateChoiceAndHasNoSideEffects(string identityKind, string action)
    {
        var played = await Act(ClairvoyanceTests.Position(), "P1", new PlayCardCommand("SPELL", "VEN·056", [], OptionalCosts: []));
        var opened = await ResolveTop(played.State);
        Assert.Equal("INSIGHT", opened.State.PendingCardChoice!.ChoiceWindow);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(opened.State, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1"); // Restored fixture: create the original live seat token before the authenticated join.
        var registry = new FixedMatchSessionRegistry(session);
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var ownerClients = new RecordingHubClients();
        var owner = CreateHub(ownerClients, new RecordingGroupManager(), "owner", registry, playerIdentity: identity);
        Assert.True((await owner.Authenticate("P1", "owner-secret-key-1234")).Authenticated);
        await owner.JoinRoom(session.RoomId, "P1");
        var token = Assert.IsType<PlayerSessionDto>(Assert.Single(ownerClients.CallerClient.JoinedMessages).Payload).ReconnectToken;
        Assert.NotEmpty(ownerClients.CallerClient.Prompts);
        var before = MatchStateHasher.HashValue(new[] { session.SnapshotFor("P1"), session.SnapshotFor("P2") });
        var beforePrompts = MatchStateHasher.HashValue(new[] { session.PromptFor("P1"), session.PromptFor("P2") });
        var journalCount = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        var untrusted = CreateHub(clients, groups, "untrusted", registry, playerIdentity: identity);
        if (identityKind == "FAILED_KEY")
            Assert.False((await untrusted.Authenticate("P1", "impostor-secret-key-1234")).Authenticated);
        if (identityKind == "OTHER_HANDLE")
            Assert.True((await untrusted.Authenticate("P2", "other-secret-key-1234")).Authenticated);

        // Even possession of the real reconnect token cannot substitute for the seat's identity.
        var receipt = await InvokeRoomIdentityAction(untrusted, action, session.RoomId, "P1", token);
        var expected = identityKind == "OTHER_HANDLE" ? ErrorCodes.IdentityMismatch : ErrorCodes.AuthenticationRequired;
        Assert.Equal(expected, Assert.IsType<ErrorDto>(Assert.Single(clients.CallerClient.Errors).Payload).Code);
        if (receipt is not null)
        {
            Assert.False(receipt.Accepted);
            Assert.Equal(expected, receipt.ErrorCode);
            Assert.Equal("REJECTED", receipt.State);
        }
        Assert.Empty(groups.Added);
        Assert.Empty(groups.Removed);
        AssertIdentityBoundarySendsNoPrivateOrBroadcastMessages(clients);
        Assert.Equal(before, MatchStateHasher.HashValue(new[] { session.SnapshotFor("P1"), session.SnapshotFor("P2") }));
        Assert.Equal(beforePrompts, MatchStateHasher.HashValue(new[] { session.PromptFor("P1"), session.PromptFor("P2") }));
        Assert.Equal(journalCount, journal.Entries.Count);
    }

    [Theory]
    [InlineData("JOIN")]
    [InlineData("RECONNECT")]
    [InlineData("SNAPSHOT")]
    [InlineData("SEED")]
    [InlineData("READY")]
    [InlineData("PASS")]
    [InlineData("END_TURN")]
    [InlineData("INTENT")]
    public async Task RoomIdentityBoundaryRejectsBeforeCreatingAnUnknownRoom(string action)
    {
        var journal = new RecordingMatchJournal();
        var playerStore = new RecordingMatchPlayerStore();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, NoopMatchRecoveryStore.Instance, playerStore);
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        var hub = CreateHub(clients, groups, "anonymous", registry);
        await InvokeRoomIdentityAction(hub, action, "identity-unknown", "Alice", "invalid-token");
        Assert.Equal(ErrorCodes.AuthenticationRequired, Assert.IsType<ErrorDto>(Assert.Single(clients.CallerClient.Errors).Payload).Code);
        Assert.Null(await registry.FindAsync("identity-unknown", default));
        Assert.Empty(journal.Entries);
        Assert.Empty(playerStore.Saved);
        Assert.Empty(groups.Added);
        AssertIdentityBoundarySendsNoPrivateOrBroadcastMessages(clients);
    }

    [Fact]
    public async Task RoomIdentityBoundaryMatchingAuthenticationRestoresPrivateOrderAndContinues()
    {
        var played = await Act(ClairvoyanceTests.Position(), "P1", new PlayCardCommand("SPELL", "VEN·056", [], OptionalCosts: []));
        var opened = await ResolveTop(played.State);
        var choice = opened.State.PendingCardChoice!;
        var ordered = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal("INSIGHT_ORDER", ordered.State.PendingCardChoice!.ChoiceWindow);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(ordered.State, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1"); // Restored fixture: create the original live seat token before the authenticated join.
        var registry = new FixedMatchSessionRegistry(session);
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var firstClients = new RecordingHubClients();
        var first = CreateHub(firstClients, new RecordingGroupManager(), "first", registry, playerIdentity: identity);
        Assert.True((await first.Authenticate("P1", "owner-secret-key-1234")).Authenticated);
        await first.JoinRoom(session.RoomId, "P1");
        var token = Assert.IsType<PlayerSessionDto>(Assert.Single(firstClients.CallerClient.JoinedMessages).Payload).ReconnectToken;
        var clients = new RecordingHubClients();
        var second = CreateHub(clients, new RecordingGroupManager(), "second", registry, playerIdentity: identity);
        Assert.True((await second.Authenticate(" p1 ", "owner-secret-key-1234")).Authenticated);
        await second.Reconnect(session.RoomId, "P1", token);
        await second.RequestSnapshot(session.RoomId, "P1");
        Assert.Single(clients.CallerClient.JoinedMessages);
        Assert.Equal(2, clients.CallerClient.Snapshots.Count);
        var pending = ordered.State.PendingCardChoice;
        var command = new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["D5", "D4", "D3", "D2", "D1"]);
        var raw = JsonSerializer.SerializeToElement(new { cmdType = "CHOOSE_CARDS", choiceId = pending.ChoiceId,
            choiceWindow = pending.ChoiceWindow, chosenObjectIds = command.ChosenObjectIds });
        var receipt = await second.SubmitIntent(session.RoomId, "P1", "authenticated-order", raw);
        Assert.True(receipt.Accepted, receipt.Message);
        Assert.Empty(clients.CallerClient.Errors);
        Assert.Single(journal.Entries);
        var final = await session.SubmitAsync("P1", "authenticated-order", command, raw, default);
        Assert.Equal(new[] { "D5", "D4" }, final.State.PlayerZones["P1"].Hand);
        Assert.Null(final.State.PendingCardChoice);
        Assert.Single(journal.Entries);
    }

    private static async Task<CommandReceiptDto?> InvokeRoomIdentityAction(GameHub hub, string action, string room, string actor, string token)
    {
        switch (action)
        {
            case "JOIN": await hub.JoinRoom(room, actor); break;
            case "RECONNECT": await hub.Reconnect(room, actor, token); break;
            case "SNAPSHOT": await hub.RequestSnapshot(room, actor); break;
            case "SEED": await hub.SeedScenario(room, actor, "STANDBY", "untrusted-seed"); break;
            case "READY": return await hub.Ready(room, actor, "untrusted-ready");
            case "PASS": return await hub.Pass(room, actor, "untrusted-pass");
            case "END_TURN": return await hub.EndTurn(room, actor, "untrusted-end");
            case "INTENT": return await hub.SubmitIntent(room, actor, "untrusted-intent", JsonSerializer.SerializeToElement(new { cmdType = "SURRENDER" }));
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
        return null;
    }

    private static void AssertIdentityBoundarySendsNoPrivateOrBroadcastMessages(RecordingHubClients clients)
    {
        Assert.Empty(clients.CallerClient.JoinedMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.GroupClient.JoinedMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Errors);
    }
}
