using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Riftbound.Api.Hubs;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GameHubJoinTests
{
    [Fact]
    public async Task JoinRoomSendsSnapshotPromptAndAddsRoomGroups()
    {
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        var hub = CreateHub(clients, groups, "connection-1");

        await hub.JoinRoom("room-a", " alice ");

        Assert.Contains(("connection-1", "room:room-a"), groups.Added);
        Assert.Contains(("connection-1", "room:room-a:player:alice"), groups.Added);
        var joinMessage = Assert.Single(clients.CallerClient.JoinedMessages);
        var snapshotMessage = Assert.Single(clients.CallerClient.Snapshots);
        var promptMessage = Assert.Single(clients.CallerClient.Prompts);
        Assert.Equal(MessageType.JOIN, joinMessage.Type);
        Assert.Equal("alice", snapshotMessage.PlayerId);
        Assert.Equal("alice", promptMessage.PlayerId);

        var join = Assert.IsType<PlayerSessionDto>(joinMessage.Payload);
        Assert.Equal("alice", join.PlayerId);
        Assert.Equal("P1", join.Seat);
        Assert.StartsWith("rt_", join.ReconnectToken, StringComparison.Ordinal);

        var snapshot = Assert.IsType<SnapshotDto>(snapshotMessage.Payload);
        var player = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["alice"]);
        Assert.Equal("P1", player["seat"]);

        var prompt = Assert.IsType<ActionPromptDto>(promptMessage.Payload);
        Assert.Equal("alice", prompt.PlayerId);
        Assert.Equal(snapshot.Tick, prompt.SnapshotTick);
        Assert.False(string.IsNullOrWhiteSpace(prompt.PromptId));
        var candidate = Assert.Single(prompt.Candidates ?? []);
        Assert.Equal("SUBMIT_DECK", candidate.Action);
        Assert.Equal("提交卡组", candidate.Label);
        Assert.True(candidate.Enabled);
        Assert.Equal(prompt.Reason, candidate.Reason);
    }

    [Fact]
    public async Task JoinRoomRejectsThirdPlayerWithError()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");

        var clients = new RecordingHubClients();
        await CreateHub(clients, new RecordingGroupManager(), "connection-3", registry)
            .JoinRoom("room-a", "charlie");

        var error = Assert.Single(clients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.RoomFull, payload.Code);
        Assert.Equal("room already has two players", payload.Message);
    }

    [Fact]
    public async Task ReconnectWithValidTokenRejoinsGroupsAndSendsSnapshotPrompt()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var joinClients = new RecordingHubClients();
        await CreateHub(joinClients, new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        var join = Assert.IsType<PlayerSessionDto>(Assert.Single(joinClients.CallerClient.JoinedMessages).Payload);

        var reconnectClients = new RecordingHubClients();
        var reconnectGroups = new RecordingGroupManager();
        await CreateHub(reconnectClients, reconnectGroups, "connection-2", registry)
            .Reconnect("room-a", "alice", join.ReconnectToken);

        Assert.Contains(("connection-2", "room:room-a"), reconnectGroups.Added);
        Assert.Contains(("connection-2", "room:room-a:player:alice"), reconnectGroups.Added);
        var reconnectMessage = Assert.Single(reconnectClients.CallerClient.JoinedMessages);
        Assert.Equal(MessageType.RECONNECT, reconnectMessage.Type);
        var reconnect = Assert.IsType<PlayerSessionDto>(reconnectMessage.Payload);
        Assert.Equal(join.PlayerId, reconnect.PlayerId);
        Assert.Equal(join.Seat, reconnect.Seat);
        Assert.StartsWith("rt_", reconnect.ReconnectToken, StringComparison.Ordinal);
        Assert.NotEqual(join.ReconnectToken, reconnect.ReconnectToken);
        Assert.Single(reconnectClients.CallerClient.Snapshots);
        Assert.Single(reconnectClients.CallerClient.Prompts);
    }

    [Fact]
    public async Task JoinRoomPersistsReconnectTokenHashWithoutPlaintext()
    {
        var playerStore = new RecordingMatchPlayerStore();
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            NoopMatchRecoveryStore.Instance,
            playerStore);
        var clients = new RecordingHubClients();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");

        var join = Assert.IsType<PlayerSessionDto>(Assert.Single(clients.CallerClient.JoinedMessages).Payload);
        var saved = Assert.Single(playerStore.Saved);
        Assert.Equal("room-a", saved.RoomId);
        Assert.Equal("alice", saved.PlayerId);
        Assert.Equal("P1", saved.Seat);
        Assert.Equal(ReconnectTokenHasher.Hash(join.ReconnectToken), saved.ReconnectTokenHash);
        Assert.DoesNotContain(join.ReconnectToken, saved.ReconnectTokenHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReconnectWithInvalidTokenReturnsStableErrorCode()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");

        var clients = new RecordingHubClients();
        await CreateHub(clients, new RecordingGroupManager(), "connection-2", registry)
            .Reconnect("room-a", "alice", "wrong-token");

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.InvalidReconnectToken, payload.Code);
        Assert.Equal("invalid reconnect token", payload.Message);
    }

    [Fact]
    public async Task RequestSnapshotSendsCurrentSnapshotAndPrompt()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");

        var clients = new RecordingHubClients();
        await CreateHub(clients, new RecordingGroupManager(), "connection-2", registry)
            .RequestSnapshot("room-a", "alice");

        var snapshot = Assert.Single(clients.CallerClient.Snapshots);
        var prompt = Assert.Single(clients.CallerClient.Prompts);
        Assert.Equal("alice", snapshot.PlayerId);
        Assert.Equal("alice", prompt.PlayerId);
    }

    [Fact]
    public async Task RequestSnapshotForUnknownPlayerReturnsStableErrorCode()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");

        var clients = new RecordingHubClients();
        await CreateHub(clients, new RecordingGroupManager(), "connection-2", registry)
            .RequestSnapshot("room-a", "charlie");

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.PlayerNotInRoom, payload.Code);
        Assert.Equal("player is not in room", payload.Message);
    }

    [Fact]
    public async Task SubmitIntentForUnknownPlayerReturnsStableErrorCode()
    {
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1")
            .SubmitIntent("room-a", "alice", "intent-pass", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.PlayerNotInRoom, payload.Code);
        Assert.Equal("player is not in room", payload.Message);
    }

    [Fact]
    public async Task SubmitIntentBeforeReadyReturnsStableErrorCode()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-before-ready", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchNotStarted, payload.Code);
        Assert.Equal("match has not started", payload.Message);
    }

    [Fact]
    public async Task SubmitIntentWithoutClientIntentIdReturnsStableErrorCode()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", " ", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentIdRequired, payload.Code);
        Assert.Equal("clientIntentId is required", payload.Message);
        Assert.Empty(clients.GroupClient.EventMessages);
    }

    [Fact]
    public async Task ReadyStartsMatchAfterBothPlayersAreReady()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");

        var aliceReadyClients = new RecordingHubClients();
        await CreateHub(aliceReadyClients, new RecordingGroupManager(), "connection-1", registry)
            .Ready("room-a", "alice", "ready-alice");

        var readyMessage = Assert.Single(aliceReadyClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.READY, readyMessage.Type);
        var readyEvents = Assert.IsAssignableFrom<IReadOnlyList<GameEvent>>(readyMessage.Payload);
        Assert.Contains(readyEvents, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.DoesNotContain(readyEvents, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var bobReadyClients = new RecordingHubClients();
        await CreateHub(bobReadyClients, new RecordingGroupManager(), "connection-2", registry)
            .Ready("room-a", "bob", "ready-bob");

        var startMessage = Assert.Single(bobReadyClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.START, startMessage.Type);
        var startEvents = Assert.IsAssignableFrom<IReadOnlyList<GameEvent>>(startMessage.Payload);
        Assert.Contains(startEvents, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(startEvents, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var passClients = new RecordingHubClients();
        var pass = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-after-start", pass);

        Assert.Empty(passClients.CallerClient.Errors);
        Assert.Equal(MessageType.EVENTS, Assert.Single(passClients.GroupClient.EventMessages).Type);
    }

    [Fact]
    public async Task OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub()
    {
        const string roomId = "official-hub-opening-room";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");

        var p1SubmitClients = new RecordingHubClients();
        await CreateHub(p1SubmitClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "submit-deck-p1", SubmitDeckJson(p1Deck));
        Assert.Empty(p1SubmitClients.CallerClient.Errors);
        Assert.Contains(EventsFor(p1SubmitClients), gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal));
        var p1SubmittedPrompt = PromptFor(p1SubmitClients, "P1");
        var p2MissingDeckPrompt = PromptFor(p1SubmitClients, "P2");
        Assert.Equal(["READY"], p1SubmittedPrompt.Actions);
        Assert.Equal(["SUBMIT_DECK"], p2MissingDeckPrompt.Actions);

        var p2SubmitClients = new RecordingHubClients();
        await CreateHub(p2SubmitClients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "submit-deck-p2", SubmitDeckJson(p2Deck));
        Assert.Empty(p2SubmitClients.CallerClient.Errors);
        Assert.Contains(EventsFor(p2SubmitClients), gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal));
        Assert.Equal(["READY"], PromptFor(p2SubmitClients, "P1").Actions);
        Assert.Equal(["READY"], PromptFor(p2SubmitClients, "P2").Actions);

        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .Ready(roomId, "P1", "ready-official-p1");
        var readyClients = new RecordingHubClients();
        await CreateHub(readyClients, new RecordingGroupManager(), "connection-2", registry)
            .Ready(roomId, "P2", "ready-official-p2");

        var startMessage = Assert.Single(readyClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.START, startMessage.Type);
        var startEvents = Assert.IsAssignableFrom<IReadOnlyList<GameEvent>>(startMessage.Payload);
        Assert.Contains(startEvents, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(startEvents, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        var startSnapshot = SnapshotFor(readyClients, "P1");
        Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(startSnapshot.Timing["phase"]));
        var activePlayerId = startSnapshot.ActivePlayerId;
        var secondPlayerId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "P2" : "P1";
        var activePrompt = PromptFor(readyClients, activePlayerId);
        var secondPrompt = PromptFor(readyClients, secondPlayerId);
        Assert.True(activePrompt.Actionable);
        Assert.Contains("MULLIGAN", activePrompt.Actions);
        Assert.False(secondPrompt.Actionable);

        var activeSnapshot = SnapshotFor(readyClients, activePlayerId);
        var activeHand = StringList(ZoneView(PlayerView(activeSnapshot, activePlayerId))["hand"]);
        Assert.Equal(4, activeHand.Count);
        var activeMulliganClients = new RecordingHubClients();
        await CreateHub(activeMulliganClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, activePlayerId, "mulligan-active", MulliganJson(activeHand.Take(1).ToArray()));
        Assert.Empty(activeMulliganClients.CallerClient.Errors);
        Assert.False(PromptFor(activeMulliganClients, activePlayerId).Actionable);
        Assert.True(PromptFor(activeMulliganClients, secondPlayerId).Actionable);

        var secondSnapshot = SnapshotFor(activeMulliganClients, secondPlayerId);
        var secondHand = StringList(ZoneView(PlayerView(secondSnapshot, secondPlayerId))["hand"]);
        Assert.Equal(4, secondHand.Count);
        var secondMulliganClients = new RecordingHubClients();
        await CreateHub(secondMulliganClients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, secondPlayerId, "mulligan-second", MulliganJson([]));
        Assert.Empty(secondMulliganClients.CallerClient.Errors);
        var completeEvents = EventsFor(secondMulliganClients);
        Assert.Contains(completeEvents, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        Assert.Contains(completeEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        var finalSnapshot = SnapshotFor(secondMulliganClients, activePlayerId);
        Assert.Equal(MatchPhases.Main, Assert.IsType<string>(finalSnapshot.Timing["phase"]));
        Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(finalSnapshot.Timing["timingState"]));
        var finalPrompt = PromptFor(secondMulliganClients, activePlayerId);
        Assert.True(finalPrompt.Actionable);
        var tapRuneCandidate = Assert.Single(
            finalPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "TAP_RUNE", StringComparison.Ordinal));
        Assert.True(tapRuneCandidate.Enabled);
        Assert.NotNull(tapRuneCandidate.Sources);
        var runeSourceId = tapRuneCandidate.Sources.First().Id;

        var tapRuneClients = new RecordingHubClients();
        await CreateHub(
                tapRuneClients,
                new RecordingGroupManager(),
                string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "connection-1" : "connection-2",
                registry)
            .SubmitIntent(roomId, activePlayerId, "tap-rune-active", JsonSerializer.SerializeToElement(new
            {
                cmdType = "TAP_RUNE",
                sourceObjectId = runeSourceId
            }));

        Assert.Empty(tapRuneClients.CallerClient.Errors);
        var tapRuneEvents = EventsFor(tapRuneClients);
        Assert.Contains(tapRuneEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNE_TAPPED", StringComparison.Ordinal));
        Assert.Contains(tapRuneEvents, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        var tapRuneSnapshot = SnapshotFor(tapRuneClients, activePlayerId);
        var activePlayer = PlayerView(tapRuneSnapshot, activePlayerId);
        var activeRunePool = Assert.IsType<Dictionary<string, object?>>(activePlayer["runePool"]);
        Assert.Equal(1, Assert.IsType<int>(activeRunePool["mana"]));
        var activeObjects = Assert.IsType<Dictionary<string, object?>>(activePlayer["objects"]);
        var tappedRune = Assert.IsType<Dictionary<string, object?>>(activeObjects[runeSourceId]);
        Assert.True(Assert.IsType<bool>(tappedRune["isExhausted"]));
        var postTapPrompt = PromptFor(tapRuneClients, activePlayerId);
        var postTapRuneCandidate = Assert.Single(
            postTapPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "TAP_RUNE", StringComparison.Ordinal));
        Assert.DoesNotContain(postTapRuneCandidate.Sources ?? [], source => string.Equals(source.Id, runeSourceId, StringComparison.Ordinal));

        var recycleRuneCandidate = Assert.Single(
            postTapPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "RECYCLE_RUNE", StringComparison.Ordinal));
        Assert.True(recycleRuneCandidate.Enabled);
        Assert.Contains(recycleRuneCandidate.Sources ?? [], source => string.Equals(source.Id, runeSourceId, StringComparison.Ordinal));

        var recycleRuneClients = new RecordingHubClients();
        await CreateHub(
                recycleRuneClients,
                new RecordingGroupManager(),
                string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "connection-1" : "connection-2",
                registry)
            .SubmitIntent(roomId, activePlayerId, "recycle-rune-active", JsonSerializer.SerializeToElement(new
            {
                cmdType = "RECYCLE_RUNE",
                sourceObjectId = runeSourceId
            }));

        Assert.Empty(recycleRuneClients.CallerClient.Errors);
        var recycleEvents = EventsFor(recycleRuneClients);
        Assert.Contains(recycleEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Contains(recycleEvents, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        var recycleRuneSnapshot = SnapshotFor(recycleRuneClients, activePlayerId);
        var recyclePlayer = PlayerView(recycleRuneSnapshot, activePlayerId);
        var recycleRunePool = Assert.IsType<Dictionary<string, object?>>(recyclePlayer["runePool"]);
        Assert.Equal(1, Assert.IsType<int>(recycleRunePool["mana"]));
        Assert.Equal(1, Assert.IsType<int>(recycleRunePool["power"]));
        var recyclePowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(recycleRunePool["powerByTrait"]);
        Assert.Single(recyclePowerByTrait);
        var recycleZones = ZoneView(recyclePlayer);
        Assert.DoesNotContain(runeSourceId, StringList(recycleZones["base"]));
        Assert.True(Assert.IsType<int>(recycleZones["runeDeckCount"]) > 0);
    }

    [Fact]
    public async Task SubmitIntentUnsupportedCommandReturnsStableErrorCode()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse("""{"cmdType":"FLIP_TABLE"}""").RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-unsupported", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("Unsupported command: FLIP_TABLE", payload.Message);
    }

    [Fact]
    public async Task SubmitIntentDuplicateConflictReturnsStableErrorCode()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var pass = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-same", pass);
        var clients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-same", endTurn);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("clientIntentId already belongs to another command", payload.Message);
    }

    [Fact]
    public async Task SubmitIntentPreservesOriginalCommandPayloadInJournal()
    {
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var cmd = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY","clientNote":"keep-me"}""").RootElement.Clone();

        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-raw-payload", cmd);

        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.CommandType, "PASS_PRIORITY", StringComparison.Ordinal));
        Assert.Equal("PASS_PRIORITY", entry.CommandType);
        Assert.NotNull(entry.RawCommand);
        Assert.Equal("keep-me", entry.RawCommand.Value.GetProperty("clientNote").GetString());
    }

    [Fact]
    public async Task SeedScenarioBroadcastsDevSnapshotsAndPromptsInDevelopment()
    {
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "P2");
        var clients = new RecordingHubClients();

        await CreateHub(
                clients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario("room-a", "P1", "basic-play", "seed-basic-play");

        Assert.Empty(clients.CallerClient.Errors);
        var eventsMessage = Assert.Single(clients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, eventsMessage.Type);
        var events = Assert.IsAssignableFrom<IReadOnlyList<GameEvent>>(eventsMessage.Payload);
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "DEV_SCENARIO_SEEDED", StringComparison.Ordinal));
        Assert.Equal(2, clients.GroupClient.Snapshots.Count);
        Assert.Equal(2, clients.GroupClient.Prompts.Count);

        var p1Snapshot = Assert.IsType<SnapshotDto>(
            Assert.Single(clients.GroupClient.Snapshots, message => string.Equals(message.PlayerId, "P1", StringComparison.Ordinal)).Payload);
        var p1View = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1View["zones"]);
        Assert.Contains("P1-UNIT-MIGHTY-FAERIE", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));

        var p1Prompt = PromptFor(clients, "P1");
        var playCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-UNIT-MIGHTY-FAERIE", StringComparison.Ordinal));
        Assert.Contains(playCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "BASE", StringComparison.Ordinal));
        Assert.NotNull(playCandidate.Metadata);
        Assert.Contains(playCandidate.Metadata, entry => string.Equals(entry.Key, "sourcePolicy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SeedScenarioBroadcastsIllegalStandbyCleanupTask()
    {
        const string roomId = "illegal-standby-cleanup-task-room";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();

        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-illegal-standby", "seed-illegal-standby-cleanup-task");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var battlefieldStates = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(p1Snapshot.Lanes["battlefields"]);
        var battlefield = Assert.Single(battlefieldStates);
        Assert.Equal("P2", Assert.IsType<string>(battlefield["controllerId"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(battlefield["occupantObjectIds"]));
        Assert.Equal(
            ["P1-STANDBY-ILLEGAL-001"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(battlefield["standbyObjectIds"]));
        Assert.Equal(1, Assert.IsType<int>(battlefield["faceDownStandbyCount"]));
        Assert.Equal(
            ["REMOVE_ILLEGAL_STANDBY"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(battlefield["pendingTaskKinds"]));

        var taskQueue = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Timing["pendingTaskQueue"]);
        Assert.True(Assert.IsType<bool>(taskQueue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(taskQueue["isBlocking"]));
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(taskQueue["phase"]));
        Assert.Equal(
            "cleanup:illegal-standby:P1-BATTLEFIELD-ILLEGAL-STANDBY-001:P1-STANDBY-ILLEGAL-001",
            Assert.IsType<string>(taskQueue["activeTaskId"]));
        var tasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]);
        var task = Assert.Single(tasks);
        Assert.Equal("REMOVE_ILLEGAL_STANDBY", Assert.IsType<string>(task["kind"]));
        Assert.Equal("BATTLEFIELD_CONTROL_CLEANUP", Assert.IsType<string>(task["reason"]));

        var p1Prompt = PromptFor(seedClients, "P1");
        var p2Prompt = PromptFor(seedClients, "P2");
        Assert.Equal(["WAIT"], p1Prompt.Actions);
        Assert.Equal(["WAIT"], p2Prompt.Actions);
        Assert.Contains("REMOVE_ILLEGAL_STANDBY", p1Prompt.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task P79TypedPowerPaymentSeedOffersAmountChoicesAndPlaysThroughHub()
    {
        const string roomId = "p7-9-typed-power-payment-core";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");

        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "typed-power-payment", "seed-p7-9-typed-power-payment");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, "SPEND_POWER:2", StringComparison.Ordinal));
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, "SPEND_POWER:red:2", StringComparison.Ordinal));

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-typed-power-bullet-time", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-SPELL-BULLET-TIME",
                cardNo = "OGN·268/298",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { "SPEND_POWER:red:2" }
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        var costEvent = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(2, costEvent.Payload["power"]);
        var snapshot = SnapshotFor(playClients, "P1");
        var stackItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
        Assert.Equal(2, Assert.IsType<int>(stackItem["damageAmount"]));
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var runePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(runePool["power"]));
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(runePool["powerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Red, powerByTrait.Keys);
    }

    [Fact]
    public async Task P79TypedPowerPaymentRecycleSeedOffersPartialAmountAndPlaysThroughHub()
    {
        const string roomId = "p7-9-typed-power-payment-recycle-core";
        const string paymentRuneObjectId = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        var paymentResourceAction = $"RECYCLE_RUNE:{paymentRuneObjectId}";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");

        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "typed-power-payment-recycle", "seed-p7-9-typed-power-payment-recycle");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, "SPEND_POWER:red:2", StringComparison.Ordinal));
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .ToArray();
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["availablePower"]));
        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["availablePowerWithPaymentResources"]));

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-typed-power-recycle-bullet-time", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-SPELL-BULLET-TIME",
                cardNo = "OGN·268/298",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { paymentResourceAction, "SPEND_POWER:red:2" }
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        var costEvent = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(2, costEvent.Payload["power"]);
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        var snapshot = SnapshotFor(playClients, "P1");
        var stackItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
        Assert.Equal(2, Assert.IsType<int>(stackItem["damageAmount"]));
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.DoesNotContain(paymentRuneObjectId, Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        Assert.Equal(2, Assert.IsType<int>(p1Zones["runeDeckCount"]));
        var runePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(runePool["powerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Red, powerByTrait.Keys);
    }

    [Fact]
    public async Task P79TypedPowerPaymentDoubleRecycleSeedRequiresBothResourcesAndPlaysThroughHub()
    {
        const string roomId = "p7-9-typed-power-payment-double-recycle-core";
        const string firstPaymentRuneObjectId = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        const string secondPaymentRuneObjectId = "P1-RUNE-RED-EXTRA-PAYMENT-001";
        var firstPaymentResourceAction = $"RECYCLE_RUNE:{firstPaymentRuneObjectId}";
        var secondPaymentResourceAction = $"RECYCLE_RUNE:{secondPaymentRuneObjectId}";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");

        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "typed-power-payment-double-recycle", "seed-p7-9-typed-power-payment-double-recycle");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, "SPEND_POWER:red:2", StringComparison.Ordinal));
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, firstPaymentResourceAction, StringComparison.Ordinal));
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, secondPaymentResourceAction, StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Contains(firstPaymentResourceAction, paymentResourceChoices);
        Assert.Contains(secondPaymentResourceAction, paymentResourceChoices);
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["availablePower"]));
        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["availablePowerWithPaymentResources"]));
        var availablePowerByTraitWithPaymentResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            sourceRequirement["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(2, availablePowerByTraitWithPaymentResources[RuneTrait.Red]);

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-typed-power-double-recycle-bullet-time", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-SPELL-BULLET-TIME",
                cardNo = "OGN·268/298",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { firstPaymentResourceAction, secondPaymentResourceAction, "SPEND_POWER:red:2" }
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        Assert.Equal(2, playEvents.Count(gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal)));
        var costEvent = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(2, costEvent.Payload["power"]);
        Assert.Equal(
            [firstPaymentResourceAction, secondPaymentResourceAction],
            Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        var snapshot = SnapshotFor(playClients, "P1");
        var stackItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
        Assert.Equal(2, Assert.IsType<int>(stackItem["damageAmount"]));
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        Assert.Equal(3, Assert.IsType<int>(p1Zones["runeDeckCount"]));
        var runePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(runePool["powerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Red, powerByTrait.Keys);
    }

    [Fact]
    public async Task P6SpellDuelSeedTransfersOnlinePriorityAfterSpellIsPlayed()
    {
        const string roomId = "p6-3a-response-window";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "spell-duel", "seed-p6-spell-duel");

        var playClients = new RecordingHubClients();
        var play = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-HEXTECH-RAY",
              "cardNo": "OGN·009/298",
              "targetObjectIds": ["P2-UNIT-001"]
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-play-hextech-ray", play);

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = Assert.IsAssignableFrom<IReadOnlyList<GameEvent>>(
            Assert.Single(playClients.GroupClient.EventMessages).Payload);
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Equal(2, playClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, playClients.GroupClient.Prompts.Count);
        var playP1Prompt = PromptFor(playClients, "P1");
        var playP2Prompt = PromptFor(playClients, "P2");
        Assert.True(playP1Prompt.Actionable);
        Assert.Contains("PASS_PRIORITY", playP1Prompt.Actions);
        Assert.Contains(playP1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "PASS_PRIORITY", StringComparison.Ordinal) && candidate.Enabled);
        Assert.False(playP2Prompt.Actionable);
        Assert.Contains("WAIT", playP2Prompt.Actions);
        Assert.Contains(playP2Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "WAIT", StringComparison.Ordinal) && !candidate.Enabled);

        var passClients = new RecordingHubClients();
        var pass = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-pass-priority", pass);

        Assert.Empty(passClients.CallerClient.Errors);
        var passEvents = Assert.IsAssignableFrom<IReadOnlyList<GameEvent>>(
            Assert.Single(passClients.GroupClient.EventMessages).Payload);
        Assert.Contains(passEvents, gameEvent => string.Equals(gameEvent.Kind, "PRIORITY_PASSED", StringComparison.Ordinal));
        Assert.Equal(2, passClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, passClients.GroupClient.Prompts.Count);
        var passP1Prompt = PromptFor(passClients, "P1");
        var passP2Prompt = PromptFor(passClients, "P2");
        Assert.False(passP1Prompt.Actionable);
        Assert.Contains("WAIT", passP1Prompt.Actions);
        Assert.True(passP2Prompt.Actionable);
        Assert.Contains("PASS_PRIORITY", passP2Prompt.Actions);

        var p2Snapshot = Assert.IsType<SnapshotDto>(
            Assert.Single(passClients.GroupClient.Snapshots, message => string.Equals(message.PlayerId, "P2", StringComparison.Ordinal)).Payload);
        Assert.Single(p2Snapshot.Stack);
        Assert.Equal("P2", p2Snapshot.Timing["priorityPlayerId"]);
        Assert.Equal("NEUTRAL_CLOSED", p2Snapshot.Timing["timingState"]);
    }

    [Fact]
    public async Task P6SpellDuelFocusSeedExposesPlayableSwiftCardPrompt()
    {
        const string roomId = "p6-3b-response-window-focus-prompt";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");

        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "spell-duel-focus", "seed-p6-spell-duel-focus");

        Assert.Empty(seedClients.CallerClient.Errors);
        var seedEvents = EventsFor(seedClients);
        Assert.Contains(seedEvents, gameEvent => string.Equals(gameEvent.Kind, "DEV_SCENARIO_SEEDED", StringComparison.Ordinal));
        Assert.Equal(2, seedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, seedClients.GroupClient.Prompts.Count);

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        Assert.Equal("SPELL_DUEL_OPEN", p1Snapshot.Timing["timingState"]);
        Assert.Equal("P1", p1Snapshot.Timing["focusPlayerId"]);
        var p1View = PlayerView(p1Snapshot, "P1");
        var p1Zones = ZoneView(p1View);
        Assert.Contains("P1-SPELL-HEXTECH-RAY", StringList(p1Zones["hand"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1View["objects"]);
        var p1Spell = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-SPELL-HEXTECH-RAY"]);
        Assert.Equal("OGN·009/298", Assert.IsType<string>(p1Spell["cardNo"]));
        Assert.Contains(
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Spell["tags"]),
            tag => string.Equals(tag, CardObjectTags.SpellCard, StringComparison.Ordinal));

        var p2ViewFromP1 = PlayerView(p1Snapshot, "P2");
        var p2ObjectsFromP1 = Assert.IsType<Dictionary<string, object?>>(p2ViewFromP1["objects"]);
        var p2Target = Assert.IsType<Dictionary<string, object?>>(p2ObjectsFromP1["P2-UNIT-HEXTECH-RAY-001"]);
        Assert.Equal("SFD·125/221", Assert.IsType<string>(p2Target["cardNo"]));

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.True(p1Prompt.Actionable);
        Assert.Equal(["PLAY_CARD", "PASS_FOCUS"], p1Prompt.Actions);
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-SPELL-HEXTECH-RAY", StringComparison.Ordinal));
        Assert.Contains(
            playCandidate.Targets ?? [],
            target => string.Equals(target.Id, "P2-UNIT-HEXTECH-RAY-001", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var sourceRequirement = Assert.Single(sourceRequirements);
        Assert.Equal("P1-SPELL-HEXTECH-RAY", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("OGN·009/298", Assert.IsType<string>(sourceRequirement["cardNo"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["manaCost"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstTargetChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            targetChoicesByIndex["0"]);
        Assert.Contains(
            firstTargetChoices,
            target => string.Equals(target.Id, "P2-UNIT-HEXTECH-RAY-001", StringComparison.Ordinal));

        var p2Prompt = PromptFor(seedClients, "P2");
        Assert.False(p2Prompt.Actionable);
        Assert.Contains("WAIT", p2Prompt.Actions);

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-play-hextech-ray-focus", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-SPELL-HEXTECH-RAY",
                cardNo = "OGN·009/298",
                targetObjectIds = new[] { "P2-UNIT-HEXTECH-RAY-001" }
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        var playP1Prompt = PromptFor(playClients, "P1");
        Assert.True(playP1Prompt.Actionable);
        Assert.Contains("PASS_PRIORITY", playP1Prompt.Actions);
    }

    [Fact]
    public async Task P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass()
    {
        const string roomId = "p6-3c-battlefield-contest-task-advance";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");

        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-contest-stack", "seed-p6-battlefield-contest-stack");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p2Prompt = PromptFor(seedClients, "P2");
        Assert.True(p2Prompt.Actionable);
        Assert.Equal(["PASS_PRIORITY"], p2Prompt.Actions);
        var seededP1Snapshot = SnapshotFor(seedClients, "P1");
        Assert.Equal("NEUTRAL_CLOSED", seededP1Snapshot.Timing["timingState"]);
        Assert.Equal("P2", seededP1Snapshot.Timing["priorityPlayerId"]);
        var seededQueue = Assert.IsType<Dictionary<string, object?>>(seededP1Snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("BATTLEFIELD_TASKS", Assert.IsType<string>(seededQueue["phase"]));

        var passClients = new RecordingHubClients();
        await CreateHub(passClients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p6-battlefield-contest-stack-pass", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PASS_PRIORITY"
            }));

        Assert.Empty(passClients.CallerClient.Errors);
        var events = EventsFor(passClients);
        Assert.Equal(
            ["PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
            events.Select(gameEvent => gameEvent.Kind).ToArray());
        var p1Snapshot = SnapshotFor(passClients, "P1");
        Assert.Equal("SPELL_DUEL_OPEN", p1Snapshot.Timing["timingState"]);
        Assert.Equal("P1", p1Snapshot.Timing["focusPlayerId"]);
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(taskQueue["phase"]));
        Assert.Equal("task:start-spell-duel:P1-BATTLEFIELD-CONTEST-001", Assert.IsType<string>(taskQueue["activeTaskId"]));
        var p1Prompt = PromptFor(passClients, "P1");
        Assert.True(p1Prompt.Actionable);
        Assert.Equal(["PASS_FOCUS"], p1Prompt.Actions);

        var p1FocusPassClients = new RecordingHubClients();
        await CreateHub(p1FocusPassClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-battlefield-contest-p1-focus-pass", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PASS_FOCUS"
            }));

        Assert.Empty(p1FocusPassClients.CallerClient.Errors);
        var p2FocusPrompt = PromptFor(p1FocusPassClients, "P2");
        Assert.True(p2FocusPrompt.Actionable);
        Assert.Equal(["PASS_FOCUS"], p2FocusPrompt.Actions);

        var p2FocusPassClients = new RecordingHubClients();
        await CreateHub(p2FocusPassClients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p6-battlefield-contest-p2-focus-pass", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PASS_FOCUS"
            }));

        Assert.Empty(p2FocusPassClients.CallerClient.Errors);
        var focusPassEvents = EventsFor(p2FocusPassClients);
        Assert.Equal(
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED"],
            focusPassEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var finalP1Snapshot = SnapshotFor(p2FocusPassClients, "P1");
        Assert.Equal("NEUTRAL_OPEN", finalP1Snapshot.Timing["timingState"]);
        var finalTaskQueue = Assert.IsType<Dictionary<string, object?>>(finalP1Snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("BATTLE_TASKS", Assert.IsType<string>(finalTaskQueue["phase"]));
        Assert.Equal("task:start-battle:P1-BATTLEFIELD-CONTEST-001", Assert.IsType<string>(finalTaskQueue["activeTaskId"]));
        var finalP1Prompt = PromptFor(p2FocusPassClients, "P1");
        Assert.True(finalP1Prompt.Actionable);
        Assert.Equal(["DECLARE_BATTLE"], finalP1Prompt.Actions);
        var declareBattleCandidate = Assert.Single(
            finalP1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.True(declareBattleCandidate.Enabled);
        Assert.Equal(["P1-UNIT-CONTEST-001"], (declareBattleCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["P2-UNIT-CONTEST-001"], (declareBattleCandidate.Targets ?? []).Select(target => target.Id).ToArray());
        Assert.Equal(["P1-BATTLEFIELD-CONTEST-001"], (declareBattleCandidate.Destinations ?? []).Select(destination => destination.Id).ToArray());

        var declareBattleClients = new RecordingHubClients();
        await CreateHub(declareBattleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-battlefield-contest-declare-battle", JsonSerializer.SerializeToElement(new
            {
                cmdType = "DECLARE_BATTLE",
                battlefieldId = "P1-BATTLEFIELD-CONTEST-001",
                attackerObjectIds = new[] { "P1-UNIT-CONTEST-001" },
                defenderObjectIds = new[] { "P2-UNIT-CONTEST-001" },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }));

        Assert.Empty(declareBattleClients.CallerClient.Errors);
        var declareBattleEvents = EventsFor(declareBattleClients);
        Assert.Contains(declareBattleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Contains(declareBattleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(declareBattleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["resolution"] as string, "CONTROL_CHANGED", StringComparison.Ordinal));
        Assert.Contains(declareBattleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_STANDBY_REMOVED", StringComparison.Ordinal)
            && Assert.IsType<object[]>(gameEvent.Payload["removedObjectIds"]).Contains("P1-STANDBY-CONTEST-001"));
        Assert.Contains(declareBattleEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        var battleResolvedP1Snapshot = SnapshotFor(declareBattleClients, "P1");
        var battleResolvedP1 = Assert.IsType<Dictionary<string, object?>>(battleResolvedP1Snapshot.Players["P1"]);
        var battleResolvedP1Zones = Assert.IsType<Dictionary<string, object?>>(battleResolvedP1["zones"]);
        Assert.DoesNotContain(
            "P1-STANDBY-CONTEST-001",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(battleResolvedP1Zones["battlefields"]));
        Assert.Contains(
            "P1-STANDBY-CONTEST-001",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(battleResolvedP1Zones["graveyard"]));
        var battleResolvedP1Objects = Assert.IsType<Dictionary<string, object?>>(battleResolvedP1["objects"]);
        var clearedStandbyObject = Assert.IsType<Dictionary<string, object?>>(battleResolvedP1Objects["P1-STANDBY-CONTEST-001"]);
        Assert.Equal(false, clearedStandbyObject["isFaceDown"]);
        var battleResolvedTaskQueue = Assert.IsType<Dictionary<string, object?>>(battleResolvedP1Snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("IDLE", Assert.IsType<string>(battleResolvedTaskQueue["phase"]));
        var battleResolvedBattle = Assert.IsType<Dictionary<string, object?>>(battleResolvedP1Snapshot.Timing["battle"]);
        Assert.False(Assert.IsType<bool>(battleResolvedBattle["isActive"]));
        var battlefields = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(battleResolvedP1Snapshot.Lanes["battlefields"]);
        var contestBattlefield = Assert.Single(
            battlefields,
            item => string.Equals(item["battlefieldObjectId"] as string, "P1-BATTLEFIELD-CONTEST-001", StringComparison.Ordinal));
        Assert.Equal("P2", contestBattlefield["controllerId"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(contestBattlefield["standbyObjectIds"]));
    }

    [Fact]
    public async Task P6MovementAndScoreSeedsBroadcastCoreSnapshotsInDevelopment()
    {
        var movementRegistry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        const string movementRoomId = "p6-4a-movement-core";
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", movementRegistry)
            .JoinRoom(movementRoomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", movementRegistry)
            .JoinRoom(movementRoomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                movementRegistry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(movementRoomId, "P1", "movement", "seed-p6-movement");

        var playMovementClients = new RecordingHubClients();
        var rideTheWind = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-RIDE-THE-WIND",
              "cardNo": "OGN·173/298",
              "targetObjectIds": ["P1-BATTLEFIELD-UNIT-001"]
            }
            """).RootElement.Clone();
        await CreateHub(playMovementClients, new RecordingGroupManager(), "connection-1", movementRegistry)
            .SubmitIntent(movementRoomId, "P1", "intent-p6-ride-the-wind", rideTheWind);
        Assert.Empty(playMovementClients.CallerClient.Errors);
        Assert.Contains(EventsFor(playMovementClients), gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var passMovementP1Clients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passMovementP1Clients, new RecordingGroupManager(), "connection-1", movementRegistry)
            .SubmitIntent(movementRoomId, "P1", "intent-p6-movement-p1-pass", passPriority);
        Assert.Empty(passMovementP1Clients.CallerClient.Errors);
        Assert.Contains(EventsFor(passMovementP1Clients), gameEvent => string.Equals(gameEvent.Kind, "PRIORITY_PASSED", StringComparison.Ordinal));

        var passMovementP2Clients = new RecordingHubClients();
        var passPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passMovementP2Clients, new RecordingGroupManager(), "connection-2", movementRegistry)
            .SubmitIntent(movementRoomId, "P2", "intent-p6-movement-p2-pass", passPriorityAgain);
        Assert.Empty(passMovementP2Clients.CallerClient.Errors);
        var movementEvents = EventsFor(passMovementP2Clients);
        Assert.Contains(movementEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(movementEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
        var movementSnapshot = SnapshotFor(passMovementP2Clients, "P1");
        var movementP1 = Assert.IsType<Dictionary<string, object?>>(movementSnapshot.Players["P1"]);
        var movementP1Zones = Assert.IsType<Dictionary<string, object?>>(movementP1["zones"]);
        Assert.Contains("P1-BATTLEFIELD-UNIT-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(movementP1Zones["base"]));

        var scoreRegistry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        const string scoreRoomId = "p6-4a-score-core";
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", scoreRegistry)
            .JoinRoom(scoreRoomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", scoreRegistry)
            .JoinRoom(scoreRoomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                scoreRegistry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(scoreRoomId, "P1", "battle-score", "seed-p6-battle-score");

        var scoreClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();
        await CreateHub(scoreClients, new RecordingGroupManager(), "connection-1", scoreRegistry)
            .SubmitIntent(scoreRoomId, "P1", "intent-p6-score-end-turn", endTurn);

        Assert.Empty(scoreClients.CallerClient.Errors);
        var scoreEvents = EventsFor(scoreClients);
        Assert.Contains(scoreEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_END_DECLARED", StringComparison.Ordinal));
        Assert.Contains(scoreEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_PLAYER_ADVANCED", StringComparison.Ordinal));
        Assert.Contains(scoreEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_START_BEGAN", StringComparison.Ordinal));
        Assert.Contains(scoreEvents, gameEvent => string.Equals(gameEvent.Kind, "BURNOUT_APPLIED", StringComparison.Ordinal));
        Assert.Contains(scoreEvents, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        var scoreSnapshot = SnapshotFor(scoreClients, "P1");
        Assert.Equal(76, scoreSnapshot.TurnNumber);
        Assert.Equal("P2", scoreSnapshot.ActivePlayerId);
        Assert.Equal("P1", scoreSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal("FINISHED", scoreSnapshot.Timing["roomStatus"]);
        var scoreP1 = Assert.IsType<Dictionary<string, object?>>(scoreSnapshot.Players["P1"]);
        Assert.Equal(8, Assert.IsType<int>(scoreP1["score"]));
        var scoreP2Prompt = PromptFor(scoreClients, "P2");
        Assert.False(scoreP2Prompt.Actionable);
        Assert.Contains("WAIT", scoreP2Prompt.Actions);
    }

    [Fact]
    public async Task P6BattleDeclareSeedBroadcastsCombatDamageInDevelopment()
    {
        const string roomId = "p6-4b-battle-declare-core";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battle-declare", "seed-p6-battle-declare");

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLE-ATTACKER-001"],
              "defenderObjectIds": ["P2-BATTLE-DEFENDER-001"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-declare-battle", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Equal(2, battleEvents.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        var battleSnapshot = SnapshotFor(battleClients, "P1");
        Assert.Empty(battleSnapshot.Stack);
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains("P1-BATTLE-ATTACKER-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.DoesNotContain("P2-BATTLE-DEFENDER-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Contains("P2-BATTLE-DEFENDER-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
    }

    [Fact]
    public async Task P79CombatPromptFiltersDeclareBattleCandidatesToLegalBattlefieldUnits()
    {
        const string roomId = "p7-9-combat-prompt-filter";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battle-prompt-filter", "seed-p7-9-combat-prompt-filter");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.True(battleCandidate.Enabled);
        Assert.Equal(
            ["P1-BATTLE-PROMPT-ATTACKER"],
            (battleCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        Assert.Equal(
            ["P2-BATTLE-PROMPT-DEFENDER"],
            (battleCandidate.Targets ?? []).Select(choice => choice.Id).ToArray());
        Assert.DoesNotContain(battleCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLE-PROMPT-BASE-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(battleCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLE-PROMPT-FACEDOWN", StringComparison.Ordinal));
        Assert.DoesNotContain(battleCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLE-PROMPT-ALREADY-ATTACKING", StringComparison.Ordinal));
        Assert.DoesNotContain(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-PROMPT-BASE-DEFENDER", StringComparison.Ordinal));
        Assert.DoesNotContain(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-PROMPT-FACEDOWN", StringComparison.Ordinal));
        Assert.DoesNotContain(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-PROMPT-ALREADY-DEFENDING", StringComparison.Ordinal));
        Assert.DoesNotContain(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-PROMPT-EQUIPMENT", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(battleCandidate.Metadata);
        Assert.Equal(1, Assert.IsType<int>(metadata["attackerCount"]));
        Assert.Equal(2, Assert.IsType<int>(metadata["defenderCountMax"]));
        Assert.Equal("battlefield-zone-face-up-units-not-already-in-combat", metadata["candidateFiltering"]);

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLE-PROMPT-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLE-PROMPT-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-combat-prompt-filter", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79CombatMultiDefenderSeedAssignsBulwarkBeforeBackRow()
    {
        const string roomId = "p7-9-combat-multi-defender";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battle-multi-defender", "seed-p7-9-combat-multi-defender");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLE-MULTI-VOLIBEAR", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-MULTI-LEBLANC", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-MULTI-KITTEN", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(battleCandidate.Metadata);
        Assert.Equal(2, Assert.IsType<int>(metadata["defenderCountMax"]));
        Assert.Equal("up-to-two-defenders-requires-assignment-keyword-representative-path", metadata["multiDefenderPolicy"]);

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLE-MULTI-VOLIBEAR"],
              "defenderObjectIds": ["P2-BATTLE-MULTI-LEBLANC", "P2-BATTLE-MULTI-KITTEN"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-combat-multi-defender", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLE-MULTI-KITTEN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["assignmentRole"] as string, "BULWARK_FIRST", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["assignmentIndex"], 1));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLE-MULTI-LEBLANC", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["assignmentRole"] as string, "BACK_ROW_LAST", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["assignmentIndex"], 2));
        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Equal(
            ["P2-BATTLE-MULTI-KITTEN", "P2-BATTLE-MULTI-LEBLANC"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
    }

    [Fact]
    public async Task P79CombatSamePriorityBulwarkSeedPreservesSubmittedDefenderOrder()
    {
        const string roomId = "p7-9-combat-same-priority-bulwark";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battle-same-priority-bulwark", "seed-p7-9-combat-same-priority-bulwark");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLE-SAME-VOLIBEAR", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-SAME-BULWARK-A", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-SAME-BULWARK-B", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(battleCandidate.Metadata);
        Assert.Equal(2, Assert.IsType<int>(metadata["defenderCountMax"]));
        Assert.Equal(
            "preserve-player-submitted-object-order-within-same-priority",
            Assert.IsType<string>(metadata["samePriorityAssignmentPolicy"]));
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-BATTLE-SAME-VOLIBEAR", StringComparison.Ordinal));
        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["maxDefenderCount"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Contains(targetChoicesByIndex["0"], choice => string.Equals(choice.Id, "P2-BATTLE-SAME-BULWARK-A", StringComparison.Ordinal));
        Assert.Contains(targetChoicesByIndex["0"], choice => string.Equals(choice.Id, "P2-BATTLE-SAME-BULWARK-B", StringComparison.Ordinal));
        Assert.Contains(targetChoicesByIndex["1"], choice => string.Equals(choice.Id, "P2-BATTLE-SAME-BULWARK-A", StringComparison.Ordinal));
        Assert.Contains(targetChoicesByIndex["1"], choice => string.Equals(choice.Id, "P2-BATTLE-SAME-BULWARK-B", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLE-SAME-VOLIBEAR"],
              "defenderObjectIds": ["P2-BATTLE-SAME-BULWARK-B", "P2-BATTLE-SAME-BULWARK-A"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-combat-same-priority-bulwark", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        var attackerDamageEvents = battleEvents
            .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLE-SAME-VOLIBEAR", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            ["P2-BATTLE-SAME-BULWARK-B", "P2-BATTLE-SAME-BULWARK-A"],
            attackerDamageEvents.Select(gameEvent => (string)gameEvent.Payload["targetObjectId"]!).ToArray());
        Assert.Collection(
            attackerDamageEvents,
            firstBulwarkDamageEvent =>
            {
                Assert.Equal("P2-BATTLE-SAME-BULWARK-B", firstBulwarkDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("BULWARK_FIRST", firstBulwarkDamageEvent.Payload["assignmentRole"]);
                Assert.Equal(1, firstBulwarkDamageEvent.Payload["assignmentIndex"]);
                Assert.Equal(4, firstBulwarkDamageEvent.Payload["damage"]);
            },
            secondBulwarkDamageEvent =>
            {
                Assert.Equal("P2-BATTLE-SAME-BULWARK-A", secondBulwarkDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("BULWARK_FIRST", secondBulwarkDamageEvent.Payload["assignmentRole"]);
                Assert.Equal(2, secondBulwarkDamageEvent.Payload["assignmentIndex"]);
                Assert.Equal(6, secondBulwarkDamageEvent.Payload["damage"]);
            });

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-BATTLE-SAME-VOLIBEAR"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        var p2Graveyard = Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]);
        Assert.Contains("P2-BATTLE-SAME-BULWARK-A", p2Graveyard);
        Assert.Contains("P2-BATTLE-SAME-BULWARK-B", p2Graveyard);
    }

    [Fact]
    public async Task P79CombatMultiAttackerSeedOffersSecondAttackerAndAssignsDamage()
    {
        const string roomId = "p7-9-combat-multi-attacker";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battle-multi-attacker", "seed-p7-9-combat-multi-attacker");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLE-MULTI-GAREN", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLE-MULTI-YI", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-MULTI-DEFENDER", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(battleCandidate.Metadata);
        Assert.Equal(2, Assert.IsType<int>(metadata["attackerCountMax"]));
        Assert.Equal("up-to-two-attackers-representative-path", metadata["multiAttackerPolicy"]);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        var garenRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-BATTLE-MULTI-GAREN", StringComparison.Ordinal));
        Assert.Equal(2, Assert.IsType<int>(garenRequirement["maxAttackerCount"]));
        Assert.Equal(1, Assert.IsType<int>(garenRequirement["maxDefenderCount"]));
        var attackerChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            garenRequirement["attackerChoicesByIndex"]);
        var secondAttackerChoices = attackerChoicesByIndex["1"];
        Assert.Contains(secondAttackerChoices, choice => string.Equals(choice.Id, "P1-BATTLE-MULTI-YI", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLE-MULTI-GAREN", "P1-BATTLE-MULTI-YI"],
              "defenderObjectIds": ["P2-BATTLE-MULTI-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-combat-multi-attacker", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLE-MULTI-GAREN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLE-MULTI-DEFENDER", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 5));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLE-MULTI-YI", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLE-MULTI-DEFENDER", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLE-MULTI-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLE-MULTI-GAREN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 5));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLE-MULTI-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLE-MULTI-YI", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 1));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-BATTLE-MULTI-YI"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        Assert.Equal(["P1-BATTLE-MULTI-GAREN"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Equal(["P2-BATTLE-MULTI-DEFENDER"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
    }

    [Fact]
    public async Task P79CombatMultiParticipantSeedOffersSecondAttackerAndSecondDefender()
    {
        const string roomId = "p7-9-combat-multi-participant";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battle-multi-participant", "seed-p7-9-combat-multi-participant");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLE-MULTI-PARTICIPANT-GAREN", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLE-MULTI-PARTICIPANT-YI", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-MULTI-PARTICIPANT-BULWARK", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(battleCandidate.Metadata);
        Assert.Equal(2, Assert.IsType<int>(metadata["attackerCountMax"]));
        Assert.Equal(2, Assert.IsType<int>(metadata["defenderCountMax"]));
        Assert.Equal("up-to-two-attackers-representative-path", metadata["multiAttackerPolicy"]);
        Assert.Equal("up-to-two-defenders-requires-assignment-keyword-representative-path", metadata["multiDefenderPolicy"]);
        Assert.Equal(
            "up-to-two-attackers-and-defenders-without-independent-assignment-prompt",
            metadata["multiParticipantBattlePolicy"]);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        var garenRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-BATTLE-MULTI-PARTICIPANT-GAREN", StringComparison.Ordinal));
        Assert.Equal(2, Assert.IsType<int>(garenRequirement["maxAttackerCount"]));
        Assert.Equal(2, Assert.IsType<int>(garenRequirement["maxDefenderCount"]));
        var attackerChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            garenRequirement["attackerChoicesByIndex"]);
        Assert.Contains(attackerChoicesByIndex["1"], choice => string.Equals(choice.Id, "P1-BATTLE-MULTI-PARTICIPANT-YI", StringComparison.Ordinal));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            garenRequirement["targetChoicesByIndex"]);
        Assert.Contains(targetChoicesByIndex["0"], choice => string.Equals(choice.Id, "P2-BATTLE-MULTI-PARTICIPANT-BULWARK", StringComparison.Ordinal));
        Assert.Contains(targetChoicesByIndex["0"], choice => string.Equals(choice.Id, "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER", StringComparison.Ordinal));
        Assert.Contains(targetChoicesByIndex["1"], choice => string.Equals(choice.Id, "P2-BATTLE-MULTI-PARTICIPANT-BULWARK", StringComparison.Ordinal));
        Assert.Contains(targetChoicesByIndex["1"], choice => string.Equals(choice.Id, "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLE-MULTI-PARTICIPANT-GAREN", "P1-BATTLE-MULTI-PARTICIPANT-YI"],
              "defenderObjectIds": ["P2-BATTLE-MULTI-PARTICIPANT-BULWARK", "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-combat-multi-participant", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLE-MULTI-PARTICIPANT-GAREN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLE-MULTI-PARTICIPANT-BULWARK", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 4));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLE-MULTI-PARTICIPANT-YI", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 3));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLE-MULTI-PARTICIPANT-YI", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 1));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-BATTLE-MULTI-PARTICIPANT-YI"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        Assert.Equal(["P1-BATTLE-MULTI-PARTICIPANT-GAREN"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Equal(
            ["P2-BATTLE-MULTI-PARTICIPANT-BULWARK", "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
        var battleResolutions = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            battleSnapshot.Timing["battleResolutions"]);
        var battleResolution = Assert.Single(battleResolutions);
        Assert.Equal("CLOSED", battleResolution["kind"]);
        Assert.Equal("BATTLEFIELD:P1-MAIN", battleResolution["battlefieldId"]);
        Assert.Equal("P1", battleResolution["attackingPlayerId"]);
        Assert.Equal("P2", battleResolution["defendingPlayerId"]);
        Assert.Equal("P1", battleResolution["winnerPlayerId"]);
        Assert.Equal(["P1-BATTLE-MULTI-PARTICIPANT-YI"], Assert.IsAssignableFrom<IReadOnlyList<string>>(battleResolution["survivingAttackerObjectIds"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(battleResolution["survivingDefenderObjectIds"]));
    }

    [Fact]
    public async Task P79CombatNoResultSeedEmitsNoResultAndMovesBothParticipantsToGraveyard()
    {
        const string roomId = "p7-9-combat-no-result";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battle-no-result", "seed-p7-9-combat-no-result");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLE-NO-RESULT-GAREN", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLE-NO-RESULT-DEFENDER", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLE-NO-RESULT-GAREN"],
              "defenderObjectIds": ["P2-BATTLE-NO-RESULT-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-combat-no-result", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLE-NO-RESULT-GAREN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLE-NO-RESULT-DEFENDER", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 4));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLE-NO-RESULT-DEFENDER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLE-NO-RESULT-GAREN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["damage"], 4));
        var noResultEvent = Assert.Single(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_NO_RESULT", StringComparison.Ordinal));
        Assert.Equal("ALL_PARTICIPANTS_DESTROYED", noResultEvent.Payload["reason"]);
        Assert.Empty(Assert.IsType<string[]>(noResultEvent.Payload["survivingAttackerObjectIds"]));
        Assert.Empty(Assert.IsType<string[]>(noResultEvent.Payload["survivingDefenderObjectIds"]));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        Assert.Equal(["P1-BATTLE-NO-RESULT-GAREN"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Equal(["P2-BATTLE-NO-RESULT-DEFENDER"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
        var timing = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Timing);
        var battle = Assert.IsType<Dictionary<string, object?>>(timing["battle"]);
        Assert.False(Assert.IsType<bool>(battle["isActive"]));
    }

    [Fact]
    public async Task P79BattlefieldHeldDrawSeedOffersBattlefieldDestinationAndDraws()
    {
        const string roomId = "p7-9-battlefield-held-draw";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-draw", "seed-p7-9-battlefield-held-draw");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-DREAM-TREE", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-DREAM-TREE",
              "attackerObjectIds": ["P1-BATTLEFIELD-HELD-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-HELD-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-draw", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_DRAW_ONE", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P2", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var battlefieldResolutions = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            battleSnapshot.Timing["battlefieldResolutions"]);
        var heldResolution = Assert.Single(
            battlefieldResolutions,
            resolution => string.Equals(resolution["kind"] as string, "HELD", StringComparison.Ordinal));
        Assert.Equal("P2", heldResolution["playerId"]);
        Assert.Equal("P2-BATTLEFIELD-DREAM-TREE", heldResolution["battlefieldObjectId"]);
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Contains("P2-BATTLEFIELD-HELD-DRAW-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["hand"]));
    }

    [Fact]
    public async Task P79BattlefieldHeldBoonSeedOffersBattlefieldDestinationAndGrantsBoon()
    {
        const string roomId = "p7-9-battlefield-held-boon";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-boon", "seed-p7-9-battlefield-held-boon");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-NAVORI-ARENA", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-NAVORI-ARENA",
              "attackerObjectIds": ["P1-BATTLEFIELD-BOON-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-BOON-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-boon", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_GRANT_BOON", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-BOON-DEFENDER", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Objects = Assert.IsType<Dictionary<string, object?>>(p2["objects"]);
        var defender = Assert.IsType<Dictionary<string, object?>>(p2Objects["P2-BATTLEFIELD-BOON-DEFENDER"]);
        Assert.Equal(4, Assert.IsType<int>(defender["power"]));
        Assert.Contains(CardObjectTags.Boon, Assert.IsAssignableFrom<IReadOnlyList<string>>(defender["tags"]));
    }

    [Fact]
    public async Task P79BattlefieldHeldMoveToBaseSeedOffersBattlefieldDestinationAndMovesDefender()
    {
        const string roomId = "p7-9-battlefield-held-move-to-base";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-move-to-base", "seed-p7-9-battlefield-held-move-to-base");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-REHEARSAL-HALL", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-REHEARSAL-HALL",
              "attackerObjectIds": ["P1-BATTLEFIELD-REHEARSAL-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-REHEARSAL-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-move-to-base", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-REHEARSAL-DEFENDER", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(["P2-BATTLEFIELD-REHEARSAL-DEFENDER"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["base"]));
        Assert.Equal(["P2-BATTLEFIELD-REHEARSAL-HALL"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerBoonDrawSeedOffersBattlefieldDestinationAndConsumesBoon()
    {
        const string roomId = "p7-9-battlefield-conquer-boon-draw";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-boon-draw", "seed-p7-9-battlefield-conquer-boon-draw");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-SHIRANA-MONASTERY", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-SHIRANA-MONASTERY",
              "attackerObjectIds": ["P1-BATTLEFIELD-SHIRANA-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-SHIRANA-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-conquer-boon-draw", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_CONSUMED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-SHIRANA-ATTACKER", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        Assert.Equal(["P1-BATTLEFIELD-BOON-DRAW-CARD"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        var battlefieldResolutions = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            battleSnapshot.Timing["battlefieldResolutions"]);
        var conqueredResolution = Assert.Single(
            battlefieldResolutions,
            resolution => string.Equals(resolution["kind"] as string, "CONQUERED", StringComparison.Ordinal));
        Assert.Equal("P1", conqueredResolution["playerId"]);
        Assert.Equal("P2-BATTLEFIELD-SHIRANA-MONASTERY", conqueredResolution["battlefieldObjectId"]);
        var attacker = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-BATTLEFIELD-SHIRANA-ATTACKER"]);
        Assert.Equal(3, Assert.IsType<int>(attacker["power"]));
        Assert.DoesNotContain(CardObjectTags.Boon, Assert.IsAssignableFrom<IReadOnlyList<string>>(attacker["tags"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerWarhawkSeedOffersBattlefieldDestinationAndCreatesWarhawk()
    {
        const string roomId = "p7-9-battlefield-conquer-warhawk";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-warhawk", "seed-p7-9-battlefield-conquer-warhawk");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-HUNTING-GROUNDS", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-HUNTING-GROUNDS",
              "attackerObjectIds": ["P1-BATTLEFIELD-HUNTING-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-HUNTING-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-conquer-warhawk", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_OVERKILL_CREATE_WARHAWK", StringComparison.Ordinal));
        var tokenEvent = Assert.Single(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["tokenCardNo"] as string, "UNL·T02", StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        Assert.Contains(tokenObjectId, Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        var token = Assert.IsType<Dictionary<string, object?>>(p1Objects[tokenObjectId]);
        Assert.Equal(1, Assert.IsType<int>(token["power"]));
        Assert.Contains(CardObjectTags.Spellshield, Assert.IsAssignableFrom<IReadOnlyList<string>>(token["tags"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerMillSeedOffersBattlefieldDestinationAndMills()
    {
        const string roomId = "p7-9-battlefield-conquer-mill";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-mill", "seed-p7-9-battlefield-conquer-mill");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-SCRAPYARD", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-SCRAPYARD",
              "attackerObjectIds": ["P1-BATTLEFIELD-CONQUER-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-CONQUER-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-conquer-mill", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_MILL_TOP_TWO", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "CARDS_MILLED", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(1, Assert.IsType<int>(p1Zones["mainDeckCount"]));
        Assert.Equal(["P1-BATTLEFIELD-MILL-001", "P1-BATTLEFIELD-MILL-002"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerDiscardDrawSeedOffersBattlefieldDestinationAndCyclesHand()
    {
        const string roomId = "p7-9-battlefield-conquer-discard-draw";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-discard-draw", "seed-p7-9-battlefield-conquer-discard-draw");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-ZAUN-SUMP", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-ZAUN-SUMP",
              "attackerObjectIds": ["P1-BATTLEFIELD-DISCARD-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-DISCARD-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-conquer-discard-draw", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_DISCARD_DRAW", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-BATTLEFIELD-DRAW-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        Assert.Contains("P1-BATTLEFIELD-DISCARD-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerRecycleRuneSeedOffersBattlefieldDestinationAndRecyclesRune()
    {
        const string roomId = "p7-9-battlefield-conquer-recycle-rune";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-recycle-rune", "seed-p7-9-battlefield-conquer-recycle-rune");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-THUNDER-RUNE", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-THUNDER-RUNE",
              "attackerObjectIds": ["P1-BATTLEFIELD-RECYCLE-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-RECYCLE-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-conquer-recycle-rune", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_RECYCLE_RUNE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-RECYCLE-RUNE-001", StringComparison.Ordinal));
        var recycleEvent = Assert.Single(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-THUNDER-RUNE", StringComparison.Ordinal));
        Assert.Equal(["P1-BATTLEFIELD-RECYCLE-RUNE-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(recycleEvent.Payload["cardIds"]));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        Assert.Equal(2, Assert.IsType<int>(p1Zones["mainDeckCount"]));
    }

    [Fact]
    public async Task P79BattlefieldDefendRevealSpellSeedOffersBattlefieldDestinationAndDrawsSpell()
    {
        const string roomId = "p7-9-battlefield-defend-reveal-spell";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-defend-reveal-spell", "seed-p7-9-battlefield-defend-reveal-spell");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-RAVENBLOOM", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-RAVENBLOOM",
              "attackerObjectIds": ["P1-BATTLEFIELD-REVEAL-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-REVEAL-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-defend-reveal-spell", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["revealedIsSpell"], true));
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-RAVENBLOOM", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(["P2-BATTLEFIELD-REVEAL-SPELL"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["hand"]));
        Assert.Equal(1, Assert.IsType<int>(p2Zones["mainDeckCount"]));
    }

    [Fact]
    public async Task P79BattlefieldEphemeralSteadfastSeedOffersBattlefieldDestinationAndAppliesBonus()
    {
        const string roomId = "p7-9-battlefield-ephemeral-steadfast";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-ephemeral-steadfast", "seed-p7-9-battlefield-ephemeral-steadfast");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-BLACK-FLAME", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-BLACK-FLAME",
              "attackerObjectIds": ["P1-BATTLEFIELD-EPHEMERAL-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-EPHEMERAL-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-ephemeral-steadfast", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        var defenderDamageEvent = Assert.Single(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-EPHEMERAL-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(1, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(3, defenderDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public async Task P79BattlefieldDefenderSteadfastSeedOffersBattlefieldDestinationAndChoice()
    {
        const string roomId = "p7-9-battlefield-defender-steadfast";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-defender-steadfast", "seed-p7-9-battlefield-defender-steadfast");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-FORTIFIED-POSITION", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-FORTIFIED-DEFENDER", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-FORTIFIED-POSITION",
              "attackerObjectIds": ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-FORTIFIED-DEFENDER"],
              "battlefieldTargetObjectIds": ["P2-BATTLEFIELD-FORTIFIED-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-defender-steadfast", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DEFENSE_GRANT_STEADFAST_TWO", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-FORTIFIED-DEFENDER", StringComparison.Ordinal));
        var defenderDamageEvent = Assert.Single(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal(2, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(4, defenderDamageEvent.Payload["combatPower"]);

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-BATTLEFIELD-FORTIFIED-ATTACKER"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
    }

    [Fact]
    public async Task P79BattlefieldDefendMoveToBaseSeedOffersBattlefieldDestinationAndChoice()
    {
        const string roomId = "p7-9-battlefield-defend-move-to-base";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-defend-move-to-base", "seed-p7-9-battlefield-defend-move-to-base");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-PLUNDER-ALLEY", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-PLUNDER-DEFENDER", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-PLUNDER-ALLEY",
              "attackerObjectIds": ["P1-BATTLEFIELD-PLUNDER-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-PLUNDER-DEFENDER"],
              "battlefieldTargetObjectIds": ["P2-BATTLEFIELD-PLUNDER-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-defend-move-to-base", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-PLUNDER-DEFENDER", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(["P2-BATTLEFIELD-PLUNDER-DEFENDER"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["base"]));
        Assert.Equal(["P2-BATTLEFIELD-PLUNDER-ALLEY"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
    }

    [Fact]
    public async Task P79BattlefieldIsolatedDefenderSeedOffersBattlefieldDestinationAndPenalty()
    {
        const string roomId = "p7-9-battlefield-isolated-defender";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-isolated-defender", "seed-p7-9-battlefield-isolated-defender");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-FORBIDDEN-WASTELAND", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-FORBIDDEN-WASTELAND",
              "attackerObjectIds": ["P1-BATTLEFIELD-ISOLATED-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-ISOLATED-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-isolated-defender", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var defenderDamageEvent = Assert.Single(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal(-2, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(2, defenderDamageEvent.Payload["combatPower"]);

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var attacker = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-BATTLEFIELD-ISOLATED-ATTACKER"]);
        Assert.Equal(2, Assert.IsType<int>(attacker["damage"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerReadyLegendSeedOffersBattlefieldDestinationAndReadiesLegend()
    {
        const string roomId = "p7-9-battlefield-ready-legend";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-ready-legend", "seed-p7-9-battlefield-ready-legend");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-LEGEND-HALL", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-LEGEND-HALL",
              "attackerObjectIds": ["P1-BATTLEFIELD-READY-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-READY-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-ready-legend", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        Assert.Contains(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND", StringComparison.Ordinal));
        Assert.Contains(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-LEGEND-READY-TARGET", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1RunePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(p1RunePool["mana"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var legend = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-LEGEND-READY-TARGET"]);
        Assert.False(Assert.IsType<bool>(legend["isExhausted"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerReadyRunesEndSeedSchedulesAndReadiesRunes()
    {
        const string roomId = "p7-9-battlefield-ready-runes-end";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-ready-runes-end", "seed-p7-9-battlefield-ready-runes-end");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-MOUNT-TARGON", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-MOUNT-TARGON",
              "attackerObjectIds": ["P1-BATTLEFIELD-RUNE-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-RUNE-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-ready-runes", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        Assert.Contains(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_READY_TWO_RUNES_AT_END", StringComparison.Ordinal));
        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var battleP1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var battleObjects = Assert.IsType<Dictionary<string, object?>>(battleP1["objects"]);
        Assert.True(Assert.IsType<bool>(Assert.IsType<Dictionary<string, object?>>(battleObjects["P1-BATTLEFIELD-READY-RUNE-001"])["isExhausted"]));
        Assert.True(Assert.IsType<bool>(Assert.IsType<Dictionary<string, object?>>(battleObjects["P1-BATTLEFIELD-READY-RUNE-002"])["isExhausted"]));

        var endClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();
        await CreateHub(endClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-ready-runes-end", endTurn);

        Assert.Empty(endClients.CallerClient.Errors);
        Assert.Contains(EventsFor(endClients), gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_END_TURN_READY_RUNES", StringComparison.Ordinal));
        var readyEvent = Assert.Single(EventsFor(endClients), gameEvent =>
            string.Equals(gameEvent.Kind, "RUNE_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_END_TURN_READY_RUNES", StringComparison.Ordinal));
        Assert.Equal(2, readyEvent.Payload["count"]);

        var endSnapshot = SnapshotFor(endClients, "P1");
        var endP1 = Assert.IsType<Dictionary<string, object?>>(endSnapshot.Players["P1"]);
        var endObjects = Assert.IsType<Dictionary<string, object?>>(endP1["objects"]);
        Assert.False(Assert.IsType<bool>(Assert.IsType<Dictionary<string, object?>>(endObjects["P1-BATTLEFIELD-READY-RUNE-001"])["isExhausted"]));
        Assert.False(Assert.IsType<bool>(Assert.IsType<Dictionary<string, object?>>(endObjects["P1-BATTLEFIELD-READY-RUNE-002"])["isExhausted"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerDrawOtherSeedOffersBattlefieldDestinationAndDraws()
    {
        const string roomId = "p7-9-battlefield-draw-other";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-draw-other", "seed-p7-9-battlefield-draw-other");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-THRONE-OF-POWER", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-THRONE-OF-POWER",
              "attackerObjectIds": ["P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-draw-other", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var triggerEvent = Assert.Single(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS", StringComparison.Ordinal));
        Assert.Equal(2, triggerEvent.Payload["drawCount"]);
        Assert.Contains(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 2));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(
            ["P1-BATTLEFIELD-DRAW-OTHER-001", "P1-BATTLEFIELD-DRAW-OTHER-002"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        Assert.Equal(1, Assert.IsType<int>(p1Zones["mainDeckCount"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerPowerfulDrawSeedOffersBattlefieldDestinationAndDraws()
    {
        const string roomId = "p7-9-battlefield-powerful-draw";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-powerful-draw", "seed-p7-9-battlefield-powerful-draw");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-SUNKEN-TEMPLE", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-SUNKEN-TEMPLE",
              "attackerObjectIds": ["P1-BATTLEFIELD-POWERFUL-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-POWERFUL-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-powerful-draw", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var triggerEvent = Assert.Single(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-POWERFUL-ATTACKER", triggerEvent.Payload["powerfulObjectId"]);
        Assert.Contains(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1RunePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(p1RunePool["mana"]));
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-BATTLEFIELD-POWERFUL-DRAW-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerGoldSeedOffersBattlefieldDestinationAndCreatesGold()
    {
        const string roomId = "p7-9-battlefield-gold";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-gold", "seed-p7-9-battlefield-gold");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-TREASURE-PILE", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-TREASURE-PILE",
              "attackerObjectIds": ["P1-BATTLEFIELD-GOLD-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-GOLD-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-gold", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        Assert.Contains(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD", StringComparison.Ordinal));
        var tokenEvent = Assert.Single(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD", StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1RunePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(p1RunePool["mana"]));
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains(tokenObjectId, Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerSandSoldierSeedReturnsUnitAndCreatesToken()
    {
        const string roomId = "p7-9-battlefield-sand-soldier";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-sand-soldier", "seed-p7-9-battlefield-sand-soldier");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-EMPEROR-SHRINE", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-EMPEROR-SHRINE",
              "attackerObjectIds": ["P1-BATTLEFIELD-SAND-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-SAND-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-sand-soldier", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var triggerEvent = Assert.Single(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-SAND-ATTACKER", triggerEvent.Payload["returnedObjectId"]);
        var tokenEvent = Assert.Single(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER", StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1RunePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(p1RunePool["mana"]));
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains("P1-BATTLEFIELD-SAND-ATTACKER", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        Assert.Contains(tokenObjectId, Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
    }

    [Fact]
    public async Task P79BattlefieldConquerReadyEquipmentSeedOffersBattlefieldDestinationAndDetachesArmament()
    {
        const string roomId = "p7-9-battlefield-ready-equipment";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-ready-equipment", "seed-p7-9-battlefield-ready-equipment");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-MOONVEIL-ALTAR", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-MOONVEIL-ALTAR",
              "attackerObjectIds": ["P1-BATTLEFIELD-EQUIPMENT-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-ready-equipment", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_READY_EQUIPMENT", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-ARMAMENT", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_DETACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, "P1-BATTLEFIELD-ARMAMENT", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var equipment = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-BATTLEFIELD-ARMAMENT"]);
        Assert.False(Assert.IsType<bool>(equipment["isExhausted"]));
        Assert.Null(equipment["attachedToObjectId"]);
    }

    [Fact]
    public async Task P79BattlefieldHeldMinionSeedOffersBattlefieldDestinationAndCreatesToken()
    {
        const string roomId = "p7-9-battlefield-held-minion";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-minion", "seed-p7-9-battlefield-held-minion");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-UNITY-SANCTUM", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-UNITY-SANCTUM",
              "attackerObjectIds": ["P1-BATTLEFIELD-MINION-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-MINION-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-minion", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_CREATE_MINION", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["tokenCardNo"] as string, "OGN·271/298", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Contains("P2-BATTLEFIELD-UNITY-SANCTUM-TOKEN-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["base"]));
    }

    [Fact]
    public async Task P79BattlefieldHeldRunesSeedOffersBattlefieldDestinationAndCallsRunes()
    {
        const string roomId = "p7-9-battlefield-held-runes";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-runes", "seed-p7-9-battlefield-held-runes");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-CONFETTI-TREE", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-CONFETTI-TREE",
              "attackerObjectIds": ["P1-BATTLEFIELD-RUNES-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-RUNES-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-runes", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_EACH_PLAYER_CALL_RUNE", StringComparison.Ordinal));
        Assert.Equal(2, battleEvents.Count(gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)));

        var p1Snapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains("P1-BATTLEFIELD-RUNE-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        var p2Snapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Contains("P2-BATTLEFIELD-RUNE-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["base"]));
    }

    [Fact]
    public async Task P79BattlefieldHeldRuneSeedOffersBattlefieldDestinationAndCallsRuneForHolder()
    {
        const string roomId = "p7-9-battlefield-held-rune";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-rune", "seed-p7-9-battlefield-held-rune");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-STAR-PEAK", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-STAR-PEAK",
              "attackerObjectIds": ["P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-rune", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_CALL_RUNE", StringComparison.Ordinal));
        var runeEvent = Assert.Single(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P2", StringComparison.Ordinal));
        Assert.Equal(["P2-BATTLEFIELD-SINGLE-RUNE-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(runeEvent.Payload["runeObjectIds"]));

        var p1Snapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.DoesNotContain("P1-RUNE-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        var p2Snapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Contains("P2-BATTLEFIELD-SINGLE-RUNE-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["base"]));
    }

    [Fact]
    public async Task P79BattlefieldStaticPowerSeedOffersBattlefieldDestinationAndAppliesBonus()
    {
        const string roomId = "p7-9-battlefield-static-power";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-static-power", "seed-p7-9-battlefield-static-power");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-POWER-PLUS", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-POWER-PLUS",
              "attackerObjectIds": ["P1-BATTLEFIELD-STATIC-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-STATIC-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-static-power", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        var attackerDamageEvent = Assert.Single(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-STATIC-DEFENDER", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(["P2-BATTLEFIELD-STATIC-DEFENDER"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
    }

    [Fact]
    public async Task P79BattlefieldStaticRoamSeedAllowsPreciseBattlefieldMove()
    {
        const string roomId = "p7-9-battlefield-static-roam";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-static-roam", "seed-p7-9-battlefield-static-roam");

        var p1Prompt = PromptFor(seedClients, "P1");
        var moveCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.Contains(moveCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-WIND-RUNNER", StringComparison.Ordinal));
        Assert.Contains(moveCandidate.OptionalCosts ?? [], choice => string.Equals(choice.Id, "ROAM", StringComparison.Ordinal));

        var moveClients = new RecordingHubClients();
        var move = JsonDocument.Parse("""
            {
              "cmdType": "MOVE_UNIT",
              "sourceObjectId": "P1-BATTLEFIELD-WIND-RUNNER",
              "origin": "BATTLEFIELD:P1-WIND-HILL",
              "destination": "BATTLEFIELD:P1-FAR-FIELD",
              "optionalCosts": ["ROAM"]
            }
            """).RootElement.Clone();
        await CreateHub(moveClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-static-roam", move);

        Assert.Empty(moveClients.CallerClient.Errors);
        var moveEvent = Assert.Single(EventsFor(moveClients), gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Equal("游走", moveEvent.Payload["movementKeyword"]);
        Assert.Equal("BATTLEFIELD:P1-WIND-HILL", moveEvent.Payload["origin"]);
        Assert.Equal("BATTLEFIELD:P1-FAR-FIELD", moveEvent.Payload["destination"]);

        var p1Snapshot = SnapshotFor(moveClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(
            ["P1-BATTLEFIELD-WIND-HILL", "P1-BATTLEFIELD-WIND-RUNNER"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
    }

    [Fact]
    public async Task P79BattlefieldStaticPreventMoveBaseSeedRejectsMoveToBase()
    {
        const string roomId = "p7-9-battlefield-static-prevent-move-base";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-static-prevent-move-base", "seed-p7-9-battlefield-static-prevent-move-base");

        var moveClients = new RecordingHubClients();
        var move = JsonDocument.Parse("""
            {
              "cmdType": "MOVE_UNIT",
              "sourceObjectId": "P1-BATTLEFIELD-TRAPPED-UNIT",
              "origin": "BATTLEFIELD",
              "destination": "BASE",
              "optionalCosts": []
            }
            """).RootElement.Clone();
        await CreateHub(moveClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-static-prevent-move-base", move);

        var error = Assert.Single(moveClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.InvalidTarget, payload.Code);
        Assert.Equal(
            "MOVE_UNIT blocked by battlefield static: units cannot move from this battlefield to base.",
            payload.Message);
    }

    [Fact]
    public async Task P79BattlefieldStaticPreventPlayUnitsSeedRejectsAmbushToBattlefield()
    {
        const string roomId = "p7-9-battlefield-static-prevent-play-units";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-static-prevent-play-units", "seed-p7-9-battlefield-static-prevent-play-units");

        var playClients = new RecordingHubClients();
        var ambushPlay = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-HAND-UNL-GLOOMY-APOTHECARY",
              "cardNo": "UNL-021/219",
              "targetObjectIds": [],
              "mode": "AMBUSH",
              "destination": "BATTLEFIELD:P1-MAIN"
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-static-prevent-play-units", ambushPlay);

        var error = Assert.Single(playClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.InvalidTarget, payload.Code);
        Assert.Equal(
            "PLAY_CARD blocked by battlefield static: units cannot be played to this battlefield.",
            payload.Message);
    }

    [Fact]
    public async Task P79BattlefieldStaticEchoCostReductionSeedPaysReducedEchoCost()
    {
        const string roomId = "p7-9-battlefield-static-echo-cost-reduction";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-static-echo-cost-reduction", "seed-p7-9-battlefield-static-echo-cost-reduction");

        var playClients = new RecordingHubClients();
        var centerStage = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-CENTER-STAGE",
              "cardNo": "UNL-061/219",
              "targetObjectIds": [],
              "optionalCosts": ["ECHO"]
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-echo-cost-reduction", centerStage);

        Assert.Empty(playClients.CallerClient.Errors);
        var events = EventsFor(playClients);
        var costPaid = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(3, costPaid.Payload["mana"]);
        Assert.Equal(1, costPaid.Payload["battlefieldEchoCostReductionMana"]);
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["effectRepeatCount"], 2));
        var p1Snapshot = SnapshotFor(playClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
    }

    [Fact]
    public async Task P79BattlefieldStaticEquipmentCostReductionSeedPaysReducedEquipmentCost()
    {
        const string roomId = "p7-9-battlefield-static-equipment-cost-reduction";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(
                roomId,
                "P1",
                "battlefield-static-equipment-cost-reduction",
                "seed-p7-9-battlefield-static-equipment-cost-reduction");

        var playClients = new RecordingHubClients();
        var longSword = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-EQUIPMENT-LONG-SWORD",
              "cardNo": "SFD·022/221",
              "targetObjectIds": []
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-equipment-cost-reduction", longSword);

        Assert.Empty(playClients.CallerClient.Errors);
        var events = EventsFor(playClients);
        var costPaid = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costPaid.Payload["mana"]);
        Assert.Equal(1, costPaid.Payload["battlefieldEquipmentCostReductionMana"]);
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["cardNo"] as string, "SFD·022/221", StringComparison.Ordinal));
        var p1Snapshot = SnapshotFor(playClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
    }

    [Fact]
    public async Task P79BattlefieldFriendlySpellDrawSeedDrawsWhenTargetingFriendlyUnit()
    {
        const string roomId = "p7-9-battlefield-friendly-spell-draw";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-friendly-spell-draw", "seed-p7-9-battlefield-friendly-spell-draw");

        var playClients = new RecordingHubClients();
        var savageStrength = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-SAVAGE-STRENGTH",
              "cardNo": "SFD·034/221",
              "targetObjectIds": ["P1-BATTLEFIELD-ALLY"]
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-friendly-spell-draw", savageStrength);

        Assert.Empty(playClients.CallerClient.Errors);
        var events = EventsFor(playClients);
        var trigger = Assert.Single(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-DREAMTREE", trigger.Payload["battlefieldObjectId"]);
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 1));
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["cardNo"] as string, "SFD·034/221", StringComparison.Ordinal));
        var p1Snapshot = SnapshotFor(playClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-MAIN-DRAWN"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
    }

    [Fact]
    public async Task P79BattlefieldSpellPowerBonusSeedBuffsControlledUnitOnSpellPlay()
    {
        const string roomId = "p7-9-battlefield-spell-power-bonus";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-spell-power-bonus", "seed-p7-9-battlefield-spell-power-bonus");

        var playClients = new RecordingHubClients();
        var savageStrength = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-SAVAGE-STRENGTH",
              "cardNo": "SFD·034/221",
              "targetObjectIds": ["P1-BATTLEFIELD-ALLY"]
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-spell-power-bonus", savageStrength);

        Assert.Empty(playClients.CallerClient.Errors);
        var events = EventsFor(playClients);
        var trigger = Assert.Single(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_SPELL_POWER_PLUS_1", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-WASTE-HALL", trigger.Payload["battlefieldObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-ALLY", trigger.Payload["targetObjectId"]);
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["appliedPowerDelta"], 1)
            && Equals(gameEvent.Payload["resultingPower"], 3));
        var p1Snapshot = SnapshotFor(playClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var target = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-BATTLEFIELD-ALLY"]);
        Assert.Equal(3, target["power"]);
        Assert.Equal(1, target["untilEndOfTurnPowerModifier"]);
    }

    [Fact]
    public async Task P79BattlefieldHighCostSpellInsightSeedRecyclesTopCardOnSpellPlay()
    {
        const string roomId = "p7-9-battlefield-high-cost-spell-insight";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-high-cost-spell-insight", "seed-p7-9-battlefield-high-cost-spell-insight");

        var playClients = new RecordingHubClients();
        var moonfall = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-MOONFALL",
              "cardNo": "UNL-066/219",
              "targetObjectIds": ["P2-BATTLEFIELD-ENEMY"]
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-high-cost-spell-insight", moonfall);

        Assert.Empty(playClients.CallerClient.Errors);
        var events = EventsFor(playClients);
        var trigger = Assert.Single(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-LOST-LIBRARY", trigger.Payload["battlefieldObjectId"]);
        Assert.Equal("UNL-066/219", trigger.Payload["playedCardNo"]);
        Assert.Equal(7, trigger.Payload["paidMana"]);
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 1));
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["cardNo"] as string, "UNL-066/219", StringComparison.Ordinal));

        var p1Snapshot = SnapshotFor(playClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(2, Assert.IsType<int>(p1Zones["mainDeckCount"]));
    }

    [Fact]
    public async Task P79BattlefieldUnitExperienceAbilitySeedOffersActivateAbilityAndGainsExperience()
    {
        const string roomId = "p7-9-battlefield-unit-experience-ability";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-unit-experience-ability", "seed-p7-9-battlefield-unit-experience-ability");

        var p1Prompt = PromptFor(seedClients, "P1");
        var abilityCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        Assert.True(abilityCandidate.Enabled);
        Assert.Contains(abilityCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-EXPERIENCE-UNIT", StringComparison.Ordinal));
        Assert.Contains(abilityCandidate.Modes ?? [], choice => string.Equals(choice.Id, "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE", StringComparison.Ordinal));

        var activateClients = new RecordingHubClients();
        var command = JsonDocument.Parse("""
            {
              "cmdType": "ACTIVATE_ABILITY",
              "sourceObjectId": "P1-BATTLEFIELD-EXPERIENCE-UNIT",
              "abilityId": "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
              "targetObjectIds": []
            }
            """).RootElement.Clone();
        await CreateHub(activateClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-unit-experience-ability", command);

        Assert.Empty(activateClients.CallerClient.Errors);
        var events = EventsFor(activateClients);
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE", StringComparison.Ordinal));
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        var snapshot = SnapshotFor(activateClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        Assert.Equal(1, Assert.IsType<int>(p1["experience"]));
        var objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unit = Assert.IsType<Dictionary<string, object?>>(objects["P1-BATTLEFIELD-EXPERIENCE-UNIT"]);
        Assert.True(Assert.IsType<bool>(unit["isExhausted"]));
    }

    [Fact]
    public async Task P79BattlefieldReturnCallRuneSeedPaysOneAndCallsExtraRune()
    {
        const string roomId = "p7-9-battlefield-return-call-rune";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-return-call-rune", "seed-p7-9-battlefield-return-call-rune");

        var playClients = new RecordingHubClients();
        var reconsider = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-RECONSIDER",
              "cardNo": "OGN·104/298",
              "targetObjectIds": ["P1-BATTLEFIELD-RETURN-UNIT"]
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-return-call-rune-play", reconsider);
        Assert.Empty(playClients.CallerClient.Errors);

        var passP1Clients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-return-call-rune-p1-pass", passPriority);
        Assert.Empty(passP1Clients.CallerClient.Errors);

        var passP2Clients = new RecordingHubClients();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-battlefield-return-call-rune-p2-pass", passPriority);
        Assert.Empty(passP2Clients.CallerClient.Errors);

        var events = EventsFor(passP2Clients);
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE", StringComparison.Ordinal));
        Assert.Equal(2, events.Count(gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)));
        var ghostBayRuneEvent = Assert.Single(events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("reason", out var reason)
            && string.Equals(reason as string, "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE", StringComparison.Ordinal));
        Assert.Equal(1, ghostBayRuneEvent.Payload["count"]);

        var snapshot = SnapshotFor(passP2Clients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(0, Assert.IsType<int>(zones["runeDeckCount"]));
        var baseZone = Assert.IsType<string[]>(zones["base"]);
        Assert.Contains("P1-GHOST-BAY-RUNE-001", baseZone);
        Assert.Contains("P1-GHOST-BAY-RUNE-002", baseZone);
    }

    [Fact]
    public async Task P79BattlefieldTargetDamageBonusSeedAddsOneDamageOnResolution()
    {
        const string roomId = "p7-9-battlefield-target-damage-bonus";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-target-damage-bonus", "seed-p7-9-battlefield-target-damage-bonus");

        var playClients = new RecordingHubClients();
        var punishment = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-PUNISHMENT",
              "cardNo": "UNL-007/219",
              "targetObjectIds": ["P2-BATTLEFIELD-TARGET"]
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-target-damage-bonus-play", punishment);
        Assert.Empty(playClients.CallerClient.Errors);

        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-target-damage-bonus-p1-pass", passPriority);
        var resolveClients = new RecordingHubClients();
        var passPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(resolveClients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-battlefield-target-damage-bonus-p2-pass", passPriorityAgain);

        Assert.Empty(resolveClients.CallerClient.Errors);
        var events = EventsFor(resolveClients);
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        var damageEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-TARGET", damageEvent.Payload["targetObjectId"]);
        Assert.Equal(4, damageEvent.Payload["damage"]);

        var p2Snapshot = SnapshotFor(resolveClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        var p2Objects = Assert.IsType<Dictionary<string, object?>>(p2["objects"]);
        var target = Assert.IsType<Dictionary<string, object?>>(p2Objects["P2-BATTLEFIELD-TARGET"]);
        Assert.Equal(4, target["damage"]);
    }

    [Fact]
    public async Task P79BattlefieldPlayUnitBoonSeedPaysOneAndGrantsBoonOnResolution()
    {
        const string roomId = "p7-9-battlefield-play-unit-boon";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-play-unit-boon", "seed-p7-9-battlefield-play-unit-boon");

        var playClients = new RecordingHubClients();
        var unitPlay = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-UNIT-CRAFTSMAN",
              "cardNo": "OGN·211/298",
              "targetObjectIds": [],
              "destination": "BATTLEFIELD:P1-MAIN"
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-play-unit-boon-play", unitPlay);
        Assert.Empty(playClients.CallerClient.Errors);

        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-play-unit-boon-p1-pass", passPriority);
        var resolveClients = new RecordingHubClients();
        var passPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(resolveClients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-battlefield-play-unit-boon-p2-pass", passPriorityAgain);

        Assert.Empty(resolveClients.CallerClient.Errors);
        var events = EventsFor(resolveClients);
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON", StringComparison.Ordinal));
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["mana"], 1));
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-CRAFTSMAN", StringComparison.Ordinal));

        var p1Snapshot = SnapshotFor(resolveClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unit = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-UNIT-CRAFTSMAN"]);
        Assert.Equal(3, Assert.IsType<int>(unit["power"]));
        Assert.Contains(CardObjectTags.Boon, Assert.IsAssignableFrom<IReadOnlyList<string>>(unit["tags"]));
    }

    [Fact]
    public async Task P79BattlefieldFirstUnitMoveOtherSeedMovesOtherUnitOnResolution()
    {
        const string roomId = "p7-9-battlefield-first-unit-move-other";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-first-unit-move-other", "seed-p7-9-battlefield-first-unit-move-other");

        var playClients = new RecordingHubClients();
        var unitPlay = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-UNIT-CRAFTSMAN",
              "cardNo": "OGN·211/298",
              "targetObjectIds": [],
              "destination": "BATTLEFIELD:P1-MAIN"
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-first-unit-move-other-play", unitPlay);
        Assert.Empty(playClients.CallerClient.Errors);

        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-first-unit-move-other-p1-pass", passPriority);
        var resolveClients = new RecordingHubClients();
        var passPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(resolveClients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-battlefield-first-unit-move-other-p2-pass", passPriorityAgain);

        Assert.Empty(resolveClients.CallerClient.Errors);
        var events = EventsFor(resolveClients);
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE", StringComparison.Ordinal));
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-ALLY", StringComparison.Ordinal));

        var p1Snapshot = SnapshotFor(resolveClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains("P1-BATTLEFIELD-ALLY", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        Assert.Contains("P1-UNIT-CRAFTSMAN", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
    }

    [Fact]
    public async Task P79BattlefieldHeldUnitCostIncreaseSeedAddsOneToUnitPlayCost()
    {
        const string roomId = "p7-9-battlefield-held-unit-cost-increase";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-unit-cost-increase", "seed-p7-9-battlefield-held-unit-cost-increase");

        var playClients = new RecordingHubClients();
        var unitPlay = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-UNIT-CRAFTSMAN",
              "cardNo": "OGN·211/298",
              "targetObjectIds": []
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-unit-cost-increase-play", unitPlay);

        Assert.Empty(playClients.CallerClient.Errors);
        var events = EventsFor(playClients);
        var costEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(4, costEvent.Payload["mana"]);
        Assert.Equal(3, costEvent.Payload["baseMana"]);
        Assert.Equal(1, costEvent.Payload["battlefieldHeldUnitCostIncreaseMana"]);
        Assert.Contains(events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["cardNo"] as string, "OGN·211/298", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldMovePowerSeedMovesUnitAndAppliesBonus()
    {
        const string roomId = "p7-9-battlefield-move-power";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-move-power", "seed-p7-9-battlefield-move-power");

        var moveClients = new RecordingHubClients();
        var move = JsonDocument.Parse("""
            {
              "cmdType": "MOVE_UNIT",
              "sourceObjectId": "P1-BATTLEFIELD-BAR-REGULAR",
              "origin": "BATTLEFIELD",
              "destination": "BASE",
              "optionalCosts": []
            }
            """).RootElement.Clone();
        await CreateHub(moveClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-move-power", move);

        Assert.Empty(moveClients.CallerClient.Errors);
        var moveEvents = EventsFor(moveClients);
        Assert.Contains(moveEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
        Assert.Contains(moveEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_UNIT_MOVED_POWER_PLUS_1", StringComparison.Ordinal));
        Assert.Contains(moveEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["resultingPower"], 3));

        var p1Snapshot = SnapshotFor(moveClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-BATTLEFIELD-BACK-ALLEY-BAR"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        Assert.Equal(["P1-BATTLEFIELD-BAR-REGULAR"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
    }

    [Fact]
    public async Task P79BattlefieldWinningScoreSeedRaisesThresholdAndDelaysBurnoutWin()
    {
        const string roomId = "p7-9-battlefield-winning-score";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-winning-score", "seed-p7-9-battlefield-winning-score");

        var seedSnapshot = SnapshotFor(seedClients, "P1");
        Assert.Equal(9, seedSnapshot.Timing["winningScore"]);

        var endTurnClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();
        await CreateHub(endTurnClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-winning-score", endTurn);

        Assert.Empty(endTurnClients.CallerClient.Errors);
        var endTurnEvents = EventsFor(endTurnClients);
        Assert.Contains(endTurnEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BURNOUT_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["scoredPlayerId"] as string, "P1", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["scoredPlayerScore"], 8));
        Assert.DoesNotContain(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));

        var p1Snapshot = SnapshotFor(endTurnClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        Assert.Equal(8, Assert.IsType<int>(p1["score"]));
        Assert.Null(p1Snapshot.Timing["winnerPlayerId"]);
        Assert.Equal(9, p1Snapshot.Timing["winningScore"]);
        Assert.Equal(MatchStatuses.InProgress, p1Snapshot.Timing["roomStatus"]);
        var p2Prompt = PromptFor(endTurnClients, "P2");
        Assert.True(p2Prompt.Actionable);
        Assert.Contains("END_TURN", p2Prompt.Actions);
    }

    [Fact]
    public async Task P79BattlefieldFirstTurnRuneSeedCallsFourthRune()
    {
        const string roomId = "p7-9-battlefield-first-turn-rune";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-first-turn-rune", "seed-p7-9-battlefield-first-turn-rune");

        var endTurnClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();
        await CreateHub(endTurnClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-first-turn-rune", endTurn);

        Assert.Empty(endTurnClients.CallerClient.Errors);
        var endTurnEvents = EventsFor(endTurnClients);
        var runeEvent = Assert.Single(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal("P2", runeEvent.Payload["playerId"]);
        Assert.Equal(4, runeEvent.Payload["count"]);

        var p2Snapshot = SnapshotFor(endTurnClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(
            ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003", "P2-RUNE-004"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["base"]));
        Assert.Equal(0, Assert.IsType<int>(p2Zones["runeDeckCount"]));
    }

    [Fact]
    public async Task P79BattlefieldFirstTurnScoreSeedGainsScore()
    {
        const string roomId = "p7-9-battlefield-first-turn-score";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-first-turn-score", "seed-p7-9-battlefield-first-turn-score");

        var endTurnClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();
        await CreateHub(endTurnClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-first-turn-score", endTurn);

        Assert.Empty(endTurnClients.CallerClient.Errors);
        var endTurnEvents = EventsFor(endTurnClients);
        Assert.Contains(endTurnEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FIRST_TURN_GAIN_SCORE", StringComparison.Ordinal));
        var scoreEvent = Assert.Single(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.Equal("P2", scoreEvent.Payload["playerId"]);
        Assert.Equal(1, scoreEvent.Payload["score"]);

        var p2Snapshot = SnapshotFor(endTurnClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        Assert.Equal(1, Assert.IsType<int>(p2["score"]));
        Assert.Null(p2Snapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.InProgress, p2Snapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldScoreDelaySeedPreventsFirstTurnScore()
    {
        const string roomId = "p7-9-battlefield-score-delay";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-score-delay", "seed-p7-9-battlefield-score-delay");

        var endTurnClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();
        await CreateHub(endTurnClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-score-delay", endTurn);

        Assert.Empty(endTurnClients.CallerClient.Errors);
        var endTurnEvents = EventsFor(endTurnClients);
        var preventedEvent = Assert.Single(endTurnEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_SCORE_PREVENTED", StringComparison.Ordinal));
        Assert.Equal("P2", preventedEvent.Payload["playerId"]);
        Assert.Equal("BATTLEFIELD_FIRST_TURN_GAIN_SCORE", preventedEvent.Payload["preventedReason"]);
        Assert.DoesNotContain(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));

        var p2Snapshot = SnapshotFor(endTurnClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        Assert.Equal(0, Assert.IsType<int>(p2["score"]));
        Assert.Null(p2Snapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.InProgress, p2Snapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldTurnStartDamageSeedDamagesAndDestroysBeforeRuneCall()
    {
        const string roomId = "p7-9-battlefield-turn-start-damage";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-turn-start-damage", "seed-p7-9-battlefield-turn-start-damage");

        var endTurnClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();
        await CreateHub(endTurnClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-turn-start-damage", endTurn);

        Assert.Empty(endTurnClients.CallerClient.Errors);
        var endTurnEvents = EventsFor(endTurnClients);
        Assert.Contains(endTurnEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS", StringComparison.Ordinal));
        Assert.Equal(
            2,
            endTurnEvents.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS", StringComparison.Ordinal)));
        Assert.Contains(endTurnEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-FROST-FALLING", StringComparison.Ordinal));
        var indexedEvents = endTurnEvents.Select((gameEvent, index) => (gameEvent, index)).ToArray();
        var triggerIndex = indexedEvents.First(entry =>
            string.Equals(entry.gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(entry.gameEvent.Payload["trigger"] as string, "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS", StringComparison.Ordinal)).index;
        var runeIndex = indexedEvents.First(entry => string.Equals(entry.gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)).index;
        Assert.True(triggerIndex >= 0);
        Assert.True(runeIndex > triggerIndex);

        var p2Snapshot = SnapshotFor(endTurnClients, "P2");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-BATTLEFIELD-FROST-HOLD"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        Assert.Equal(["P1-BATTLEFIELD-FROST-FALLING"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Contains("P2-BATTLEFIELD-FROST-SURVIVOR", Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Equal(MatchStatuses.InProgress, p2Snapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldTurnStartDestroyDrawSeedDestroysAndDrawsBeforeRuneCall()
    {
        const string roomId = "p7-9-battlefield-turn-start-destroy-draw";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-turn-start-destroy-draw", "seed-p7-9-battlefield-turn-start-destroy-draw");

        var endTurnClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();
        await CreateHub(endTurnClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-turn-start-destroy-draw", endTurn);

        Assert.Empty(endTurnClients.CallerClient.Errors);
        var endTurnEvents = EventsFor(endTurnClients);
        Assert.Contains(endTurnEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-ROSE-SACRIFICE", StringComparison.Ordinal));
        Assert.Contains(endTurnEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-ROSE-SACRIFICE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW", StringComparison.Ordinal));
        Assert.Equal(2, endTurnEvents.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)));
        var indexedEvents = endTurnEvents.Select((gameEvent, index) => (gameEvent, index)).ToArray();
        var triggerIndex = indexedEvents.First(entry =>
            string.Equals(entry.gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(entry.gameEvent.Payload["trigger"] as string, "BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW", StringComparison.Ordinal)).index;
        var firstDrawIndex = indexedEvents.First(entry => string.Equals(entry.gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)).index;
        var runeIndex = indexedEvents.First(entry => string.Equals(entry.gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)).index;
        Assert.True(firstDrawIndex > triggerIndex);
        Assert.True(runeIndex > firstDrawIndex);

        var p2Snapshot = SnapshotFor(endTurnClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(["P2-BATTLEFIELD-ROSE-LAB"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Equal(["P2-BATTLEFIELD-ROSE-SACRIFICE"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
        Assert.Equal(["P2-ROSE-DRAW-001", "P2-NORMAL-DRAW-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["hand"]));
        Assert.Equal(MatchStatuses.InProgress, p2Snapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldHeldScoreSeedOffersBattlefieldDestinationAndGainsScore()
    {
        const string roomId = "p7-9-battlefield-held-score";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-score", "seed-p7-9-battlefield-held-score");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-ENERGY-HUB", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-ENERGY-HUB",
              "attackerObjectIds": ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-ENERGY-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-score", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["power"], 4));
        var scoreEvent = Assert.Single(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.Equal("P2", scoreEvent.Payload["playerId"]);
        Assert.Equal(1, scoreEvent.Payload["score"]);

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        Assert.Equal(1, Assert.IsType<int>(p2["score"]));
        var p2RunePool = Assert.IsType<Dictionary<string, object?>>(p2["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(p2RunePool["power"]));
        Assert.Null(battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.InProgress, battleSnapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldHeldReturnHeroSeedOffersBattlefieldDestinationAndReturnsHero()
    {
        const string roomId = "p7-9-battlefield-held-return-hero";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-return-hero", "seed-p7-9-battlefield-held-return-hero");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-HALLOWED-TOMB", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-HALLOWED-TOMB",
              "attackerObjectIds": ["P1-BATTLEFIELD-TOMB-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-TOMB-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-return-hero", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_RETURN_HERO_FROM_GRAVEYARD", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_RETURNED_TO_CHAMPION_ZONE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-HERO-TOMB-RETURN", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
        Assert.Equal(["P2-HERO-TOMB-RETURN"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["championZone"]));
        Assert.Null(battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.InProgress, battleSnapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldHeldNextSpellEchoSeedOffersBattlefieldDestinationAndGrantsEcho()
    {
        const string roomId = "p7-9-battlefield-held-next-spell-echo";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-next-spell-echo", "seed-p7-9-battlefield-held-next-spell-echo");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-PILTOVER-ACADEMY", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-PILTOVER-ACADEMY",
              "attackerObjectIds": ["P1-BATTLEFIELD-PILTOVER-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-PILTOVER-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-next-spell-echo", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        var triggerEvent = Assert.Single(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO", StringComparison.Ordinal));
        Assert.Equal("P2", triggerEvent.Payload["playerId"]);
        Assert.Equal("P2-BATTLEFIELD-PILTOVER-ACADEMY", triggerEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:P2", triggerEvent.Payload["effectId"]);

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        Assert.Null(battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.InProgress, battleSnapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldBattleDestroyedRecallSeedOffersBattlefieldDestinationAndRecalls()
    {
        const string roomId = "p7-9-battlefield-battle-destroyed-recall";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-battle-destroyed-recall", "seed-p7-9-battlefield-battle-destroyed-recall");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-BLOOD-ALTAR", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-BLOOD-ALTAR",
              "attackerObjectIds": ["P1-BATTLEFIELD-BLOOD-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-BLOOD-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-battle-destroyed-recall", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "P2-BATTLEFIELD-BLOOD-ALTAR", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["mana"], 3));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_RECALLED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-BLOOD-DEFENDER", StringComparison.Ordinal));
        Assert.DoesNotContain(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-BLOOD-DEFENDER", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(["P2-BATTLEFIELD-BLOOD-DEFENDER"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["base"]));
        Assert.Equal(["P2-BATTLEFIELD-BLOOD-ALTAR"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
        var p2RunePool = Assert.IsType<Dictionary<string, object?>>(p2["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(p2RunePool["mana"]));
        Assert.Null(battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.InProgress, battleSnapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldLegendAttachArmamentSeedOffersLegendActionAndAttaches()
    {
        const string roomId = "p7-9-battlefield-legend-attach-armament";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-legend-attach-armament", "seed-p7-9-battlefield-legend-attach-armament");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var legendCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal));
        Assert.True(legendCandidate.Enabled);
        Assert.Contains(legendCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-LEGEND-PORO-FORGE", StringComparison.Ordinal));
        Assert.Contains(
            legendCandidate.Modes ?? [],
            choice => string.Equals(choice.Id, "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD", StringComparison.Ordinal));

        var actClients = new RecordingHubClients();
        var legendAct = JsonDocument.Parse("""
            {
              "cmdType": "LEGEND_ACT",
              "sourceObjectId": "P1-LEGEND-PORO-FORGE",
              "abilityId": "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD",
              "targetObjectIds": ["P1-UNIT-PORO-FORGE-TARGET", "P1-EQUIPMENT-PORO-FORGE-ARMAMENT"],
              "optionalCosts": []
            }
            """).RootElement.Clone();
        await CreateHub(actClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-legend-attach-armament", legendAct);

        Assert.Empty(actClients.CallerClient.Errors);
        var actEvents = EventsFor(actClients);
        Assert.Contains(actEvents, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Contains(actEvents, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal));
        Assert.Contains(actEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONTROLLED_LEGEND_ATTACH_ARMAMENT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "P1-BATTLEFIELD-PORO-FORGE", StringComparison.Ordinal));
        Assert.Contains(actEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, "P1-EQUIPMENT-PORO-FORGE-ARMAMENT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["attachedToObjectId"] as string, "P1-UNIT-PORO-FORGE-TARGET", StringComparison.Ordinal));

        var actSnapshot = SnapshotFor(actClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(actSnapshot.Players["P1"]);
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var legendObject = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-LEGEND-PORO-FORGE"]);
        Assert.Equal(true, legendObject["isExhausted"]);
        var armamentObject = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-EQUIPMENT-PORO-FORGE-ARMAMENT"]);
        Assert.Equal("P1-UNIT-PORO-FORGE-TARGET", armamentObject["attachedToObjectId"]);
        Assert.Null(actSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.InProgress, actSnapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldExtraStandbySeedOffersBandleDestinationAndHides()
    {
        const string roomId = "p7-9-battlefield-extra-standby";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-extra-standby", "seed-p7-9-battlefield-extra-standby");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var hideCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "HIDE_CARD", StringComparison.Ordinal));
        Assert.True(hideCandidate.Enabled);
        Assert.Contains(hideCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-STANDBY-BANDLE-TEEMO", StringComparison.Ordinal));
        Assert.Contains(hideCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE", StringComparison.Ordinal));
        Assert.Contains(hideCandidate.OptionalCosts ?? [], choice => string.Equals(choice.Id, "STANDBY_A", StringComparison.Ordinal));

        var hideClients = new RecordingHubClients();
        var hideCard = JsonDocument.Parse("""
            {
              "cmdType": "HIDE_CARD",
              "sourceObjectId": "P1-STANDBY-BANDLE-TEEMO",
              "cardNo": "OGN·121/298",
              "destination": "BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE",
              "optionalCosts": ["STANDBY_A"]
            }
            """).RootElement.Clone();
        await CreateHub(hideClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-extra-standby", hideCard);

        Assert.Empty(hideClients.CallerClient.Errors);
        var hideEvents = EventsFor(hideClients);
        Assert.Contains(hideEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_EXTRA_STANDBY_ARRANGED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "P1-BATTLEFIELD-BANDLE-TREE", StringComparison.Ordinal));
        Assert.Contains(hideEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "P1-BATTLEFIELD-BANDLE-TREE", StringComparison.Ordinal));

        var hideSnapshot = SnapshotFor(hideClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(hideSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        Assert.Equal(
            ["P1-BATTLEFIELD-BANDLE-TREE", "P1-STANDBY-BANDLE-TEEMO"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var hiddenObject = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-STANDBY-BANDLE-TEEMO"]);
        Assert.Equal(true, hiddenObject["isFaceDown"]);
        var p1RunePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(p1RunePool["mana"]));
        Assert.Null(hideSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.InProgress, hideSnapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestSeedOffersBattlefieldDestinationAndActivatesUnits()
    {
        const string roomId = "p7-9-battlefield-held-activate-conquest";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-activate-conquest", "seed-p7-9-battlefield-held-activate-conquest");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-RECKONER-ARENA", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-RECKONER-ARENA",
              "attackerObjectIds": ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-BAD-PORO"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-activate-conquest", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_ACTIVATE_UNIT_CONQUEST_EFFECTS", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "P2-BATTLEFIELD-RECKONER-ARENA", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "UNIT_CONQUEST_CREATE_DORMANT_GOLD", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P2", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(["P2-BATTLEFIELD-RECKONER-DRAW-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["hand"]));
        Assert.Contains(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["base"]), objectId =>
            objectId.StartsWith("P2-BATTLEFIELD-BAD-PORO-TOKEN-", StringComparison.Ordinal));
        Assert.Null(battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.InProgress, battleSnapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldHeldSevenUnitsSeedOffersBattlefieldDestinationAndWins()
    {
        const string roomId = "p7-9-battlefield-held-seven-units-win";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-battlefield-held-seven-units-win");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-GRAND-PLAZA", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-GRAND-PLAZA",
              "attackerObjectIds": ["P1-BATTLEFIELD-GRAND-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-GRAND-UNIT-001"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-seven-units-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        var triggerEvent = Assert.Single(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_SEVEN_UNITS_WIN", StringComparison.Ordinal));
        Assert.Equal(7, triggerEvent.Payload["controlledBattlefieldUnitCount"]);
        var winEvent = Assert.Single(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        Assert.Equal("P2", winEvent.Payload["winnerPlayerId"]);
        Assert.Equal("BATTLEFIELD_HELD_SEVEN_UNITS_WIN", winEvent.Payload["reason"]);

        var battleSnapshot = SnapshotFor(battleClients, "P2");
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
    }

    [Fact]
    public async Task P79BattlefieldConquerRevealRecycleSeedOffersBattlefieldDestinationAndRecycles()
    {
        const string roomId = "p7-9-battlefield-conquer-reveal-recycle";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-conquer-reveal-recycle", "seed-p7-9-battlefield-conquer-reveal-recycle");

        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-CANDLELIT-SANCTUM", StringComparison.Ordinal));

        var battleClients = new RecordingHubClients();
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P1-BATTLEFIELD-CANDLELIT-SANCTUM",
              "attackerObjectIds": ["P1-BATTLEFIELD-CANDLE-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-CANDLE-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(battleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-conquer-reveal-recycle", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE", StringComparison.Ordinal));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 2));
        Assert.Contains(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 2));

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(3, Assert.IsType<int>(p1Zones["mainDeckCount"]));
        Assert.Null(battleSnapshot.Timing["winnerPlayerId"]);
    }

    [Fact]
    public async Task P6EchoStackSeedBroadcastsRepeatedDrawInDevelopment()
    {
        const string roomId = "p6-5a-echo-stack-core";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "echo-stack", "seed-p6-echo-stack");

        var playClients = new RecordingHubClients();
        var centerStage = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-CENTER-STAGE",
              "cardNo": "UNL-061/219",
              "targetObjectIds": [],
              "optionalCosts": ["ECHO"]
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-echo-center-stage", centerStage);
        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var passP1Clients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-echo-p1-pass", passPriority);
        Assert.Empty(passP1Clients.CallerClient.Errors);

        var passP2Clients = new RecordingHubClients();
        var passPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p6-echo-p2-pass", passPriorityAgain);
        Assert.Empty(passP2Clients.CallerClient.Errors);
        var resolveEvents = EventsFor(passP2Clients);
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        var drawEvent = Assert.Single(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(2, drawEvent.Payload["count"]);
        var snapshot = SnapshotFor(passP2Clients, "P1");
        Assert.Empty(snapshot.Stack);
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(0, Assert.IsType<int>(p1Zones["mainDeckCount"]));
        Assert.Equal(
            ["P1-DRAW-001", "P1-DRAW-002"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        Assert.Contains("P1-SPELL-CENTER-STAGE", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
    }

    [Fact]
    public async Task P6StandbyReactionSeedBroadcastsRevealStackAndResolutionInDevelopment()
    {
        const string roomId = "p6-5a-standby-reaction-core";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "standby-reaction", "seed-p6-standby-reaction");

        var revealClients = new RecordingHubClients();
        var teemoReveal = JsonDocument.Parse("""
            {
              "cmdType": "REVEAL_CARD",
              "sourceObjectId": "P1-FACEDOWN-OGN-TEEMO-PURPLE",
              "cardNo": "OGN·197/298",
              "targetObjectIds": [],
              "mode": "STANDBY_REACTION",
              "optionalCosts": ["STANDBY_REVEAL_0"],
              "destination": "STACK"
            }
            """).RootElement.Clone();
        await CreateHub(revealClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-standby-teemo-reveal", teemoReveal);
        Assert.Empty(revealClients.CallerClient.Errors);
        var revealEvents = EventsFor(revealClients);
        Assert.Contains(revealEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_REVEALED", StringComparison.Ordinal));
        Assert.Contains(revealEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(revealEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var passP1Clients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-standby-p1-pass", passPriority);
        Assert.Empty(passP1Clients.CallerClient.Errors);

        var passP2Clients = new RecordingHubClients();
        var passPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p6-standby-p2-pass", passPriorityAgain);
        Assert.Empty(passP2Clients.CallerClient.Errors);
        var resolveEvents = EventsFor(passP2Clients);
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        var snapshot = SnapshotFor(passP2Clients, "P1");
        Assert.Single(snapshot.Stack);
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains("P1-FACEDOWN-OGN-TEEMO-PURPLE", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
    }

    [Fact]
    public async Task P6AmbushReactionSeedBroadcastsBattlefieldPlayInDevelopment()
    {
        const string roomId = "p6-5a-ambush-reaction-core";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "ambush-reaction", "seed-p6-ambush-reaction");

        var playClients = new RecordingHubClients();
        var gloomyApothecary = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-HAND-UNL-GLOOMY-APOTHECARY",
              "cardNo": "UNL-021/219",
              "targetObjectIds": [],
              "mode": "AMBUSH",
              "destination": "BATTLEFIELD:P1-MAIN"
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-ambush-gloomy-apothecary", gloomyApothecary);
        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var passP1Clients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-ambush-p1-pass", passPriority);
        Assert.Empty(passP1Clients.CallerClient.Errors);

        var passP2Clients = new RecordingHubClients();
        var passPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p6-ambush-p2-pass", passPriorityAgain);
        Assert.Empty(passP2Clients.CallerClient.Errors);
        var resolveEvents = EventsFor(passP2Clients);
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BATTLEFIELD", StringComparison.Ordinal));
        var snapshot = SnapshotFor(passP2Clients, "P1");
        Assert.Single(snapshot.Stack);
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        Assert.Equal(
            ["P1-BATTLEFIELD-FRIENDLY-001", "P1-HAND-UNL-GLOOMY-APOTHECARY"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
    }

    [Fact]
    public async Task P6EquipmentSeedBroadcastsPlayAndAssembleInDevelopment()
    {
        const string roomId = "p6-6b-equipment-assemble-core";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "equipment", "seed-p6-equipment");

        var playClients = new RecordingHubClients();
        var longSword = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-EQUIPMENT-LONG-SWORD",
              "cardNo": "SFD·022/221",
              "targetObjectIds": []
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-play-long-sword", longSword);
        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var passP1Clients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-equipment-p1-pass", passPriority);
        Assert.Empty(passP1Clients.CallerClient.Errors);

        var passP2Clients = new RecordingHubClients();
        var passPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p6-equipment-p2-pass", passPriorityAgain);
        Assert.Empty(passP2Clients.CallerClient.Errors);
        var resolveEvents = EventsFor(passP2Clients);
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal));
        var playSnapshot = SnapshotFor(passP2Clients, "P1");
        var playP1 = Assert.IsType<Dictionary<string, object?>>(playSnapshot.Players["P1"]);
        var playP1Zones = Assert.IsType<Dictionary<string, object?>>(playP1["zones"]);
        Assert.Contains("P1-EQUIPMENT-LONG-SWORD", Assert.IsAssignableFrom<IReadOnlyList<string>>(playP1Zones["base"]));

        var assembleClients = new RecordingHubClients();
        var assemble = JsonDocument.Parse("""
            {
              "cmdType": "ASSEMBLE_EQUIPMENT",
              "sourceObjectId": "P1-EQUIPMENT-LONG-SWORD",
              "targetObjectId": "P1-UNIT-ASSEMBLE-TARGET",
              "optionalCosts": ["ASSEMBLE_RED"]
            }
            """).RootElement.Clone();
        await CreateHub(assembleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-assemble-long-sword", assemble);
        Assert.Empty(assembleClients.CallerClient.Errors);
        var assembleEvents = EventsFor(assembleClients);
        Assert.Contains(assembleEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(assembleEvents, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        var assembleSnapshot = SnapshotFor(assembleClients, "P1");
        var assembleP1 = Assert.IsType<Dictionary<string, object?>>(assembleSnapshot.Players["P1"]);
        var assembleObjects = Assert.IsType<Dictionary<string, object?>>(assembleP1["objects"]);
        var equipment = Assert.IsType<Dictionary<string, object?>>(assembleObjects["P1-EQUIPMENT-LONG-SWORD"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", equipment["attachedToObjectId"]);
        Assert.Equal("P1", equipment["ownerId"]);
        Assert.Equal("P1", equipment["controllerId"]);
    }

    [Fact]
    public async Task P7StatusShowcaseSeedBroadcastsAttachedEquipmentAndStatusMarkersInDevelopment()
    {
        const string roomId = "p7-5-status-showcase";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");

        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "status-showcase", "seed-p7-status-showcase");

        Assert.Empty(seedClients.CallerClient.Errors);
        Assert.Contains(EventsFor(seedClients), gameEvent => string.Equals(gameEvent.Kind, "DEV_SCENARIO_SEEDED", StringComparison.Ordinal));
        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var anchor = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-UNIT-STATUS-ANCHOR"]);
        Assert.Equal(2, anchor["untilEndOfTurnPowerModifier"]);
        Assert.Contains(CardObjectTags.Spellshield, Assert.IsAssignableFrom<IReadOnlyList<string>>(anchor["tags"]));
        Assert.Contains(CardCombatKeywordNames.Roam, Assert.IsAssignableFrom<IReadOnlyList<string>>(anchor["tags"]));

        var equipment = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-EQUIPMENT-LONG-SWORD"]);
        Assert.Equal("P1-UNIT-STATUS-ANCHOR", equipment["attachedToObjectId"]);
        Assert.Equal("P1", equipment["ownerId"]);
        Assert.Equal("P1", equipment["controllerId"]);

        var p2Snapshot = SnapshotFor(seedClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        var p2Objects = Assert.IsType<Dictionary<string, object?>>(p2["objects"]);
        var controlled = Assert.IsType<Dictionary<string, object?>>(p2Objects["P2-CONTROLLED-UNIT"]);
        Assert.Equal("P2", controlled["ownerId"]);
        Assert.Equal("P1", controlled["controllerId"]);
        Assert.True(Assert.IsType<bool>(controlled["isDefending"]));
    }

    [Fact]
    public async Task P6ResourceExperienceSeedBroadcastsExperienceAndLevelInDevelopment()
    {
        const string roomId = "p6-7b-resource-experience-core";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "resource-experience", "seed-p6-resource-experience");

        var playExperienceClients = new RecordingHubClients();
        var demaciaEnvoy = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-UNIT-DEMACIA-ENVOY",
              "cardNo": "UNL-092/219",
              "targetObjectIds": []
            }
            """).RootElement.Clone();
        await CreateHub(playExperienceClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-play-demacia-envoy", demaciaEnvoy);
        Assert.Empty(playExperienceClients.CallerClient.Errors);
        var playExperienceEvents = EventsFor(playExperienceClients);
        Assert.Contains(playExperienceEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(playExperienceEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(playExperienceEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var passExperienceP1Clients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passExperienceP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-resource-p1-pass", passPriority);
        Assert.Empty(passExperienceP1Clients.CallerClient.Errors);

        var passExperienceP2Clients = new RecordingHubClients();
        var passPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passExperienceP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p6-resource-p2-pass", passPriorityAgain);
        Assert.Empty(passExperienceP2Clients.CallerClient.Errors);
        var experienceEvents = EventsFor(passExperienceP2Clients);
        Assert.Contains(experienceEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(experienceEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        Assert.Contains(experienceEvents, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        var experienceSnapshot = SnapshotFor(passExperienceP2Clients, "P1");
        var experienceP1 = Assert.IsType<Dictionary<string, object?>>(experienceSnapshot.Players["P1"]);
        Assert.Equal(3, Assert.IsType<int>(experienceP1["experience"]));

        var playLevelClients = new RecordingHubClients();
        var mossStepper = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-UNIT-MOSS-STEPPER",
              "cardNo": "UNL-047/219",
              "targetObjectIds": []
            }
            """).RootElement.Clone();
        await CreateHub(playLevelClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-play-moss-stepper", mossStepper);
        Assert.Empty(playLevelClients.CallerClient.Errors);

        var passLevelP1Clients = new RecordingHubClients();
        var passLevelPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passLevelP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-level-p1-pass", passLevelPriority);
        Assert.Empty(passLevelP1Clients.CallerClient.Errors);

        var passLevelP2Clients = new RecordingHubClients();
        var passLevelPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passLevelP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p6-level-p2-pass", passLevelPriorityAgain);
        Assert.Empty(passLevelP2Clients.CallerClient.Errors);
        var levelEvents = EventsFor(passLevelP2Clients);
        Assert.Contains(levelEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(levelEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        var levelSnapshot = SnapshotFor(passLevelP2Clients, "P1");
        var levelP1 = Assert.IsType<Dictionary<string, object?>>(levelSnapshot.Players["P1"]);
        Assert.Equal(3, Assert.IsType<int>(levelP1["experience"]));
        var levelP1Zones = Assert.IsType<Dictionary<string, object?>>(levelP1["zones"]);
        Assert.Equal(
            ["P1-UNIT-DEMACIA-ENVOY", "P1-UNIT-MOSS-STEPPER"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(levelP1Zones["base"]));
        var levelObjects = Assert.IsType<Dictionary<string, object?>>(levelP1["objects"]);
        var leveledMossStepper = Assert.IsType<Dictionary<string, object?>>(levelObjects["P1-UNIT-MOSS-STEPPER"]);
        Assert.Equal(4, Assert.IsType<int>(leveledMossStepper["power"]));
        var tags = Assert.IsAssignableFrom<IReadOnlyList<string>>(leveledMossStepper["tags"]);
        Assert.Contains(CardObjectTags.Spellshield, tags);
        Assert.Contains("狩猎2", tags);
    }

    [Fact]
    public async Task P79LegendActSeedBroadcastsPromptAndDrawsFromLegendActionInDevelopment()
    {
        const string roomId = "p7-9-legend-act-core";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "legend-act", "seed-p7-9-legend-act");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var legendCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal));
        Assert.True(legendCandidate.Enabled);
        Assert.Contains(legendCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-LEGEND-POPPY", StringComparison.Ordinal));
        Assert.Contains(legendCandidate.Modes ?? [], choice => string.Equals(choice.Id, "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW", StringComparison.Ordinal));
        Assert.Contains(legendCandidate.OptionalCosts ?? [], choice => string.Equals(choice.Id, "SPEND_EXPERIENCE:3", StringComparison.Ordinal));

        var actClients = new RecordingHubClients();
        var legendAct = JsonDocument.Parse("""
            {
              "cmdType": "LEGEND_ACT",
              "sourceObjectId": "P1-LEGEND-POPPY",
              "abilityId": "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
              "targetObjectIds": [],
              "optionalCosts": ["SPEND_EXPERIENCE:3"]
            }
            """).RootElement.Clone();
        await CreateHub(actClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-legend-act", legendAct);

        Assert.Empty(actClients.CallerClient.Errors);
        var actEvents = EventsFor(actClients);
        Assert.Contains(actEvents, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Contains(actEvents, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_SPENT", StringComparison.Ordinal));
        Assert.Contains(actEvents, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal));
        Assert.Contains(actEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        var actSnapshot = SnapshotFor(actClients, "P1");
        var actP1 = Assert.IsType<Dictionary<string, object?>>(actSnapshot.Players["P1"]);
        Assert.Equal(0, Assert.IsType<int>(actP1["experience"]));
        var actZones = Assert.IsType<Dictionary<string, object?>>(actP1["zones"]);
        Assert.Equal(0, Assert.IsType<int>(actZones["mainDeckCount"]));
        Assert.Equal(["P1-LEGEND-DRAW-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(actZones["hand"]));
        var actObjects = Assert.IsType<Dictionary<string, object?>>(actP1["objects"]);
        var legendObject = Assert.IsType<Dictionary<string, object?>>(actObjects["P1-LEGEND-POPPY"]);
        Assert.True(Assert.IsType<bool>(legendObject["isExhausted"]));
    }

    [Fact]
    public async Task P6LifecycleEphemeralSeedBroadcastsTurnStartCleanupInDevelopment()
    {
        const string roomId = "p6-8b-lifecycle-ephemeral-core";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "lifecycle-ephemeral", "seed-p6-lifecycle-ephemeral");

        var endTurnClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();
        await CreateHub(endTurnClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-ephemeral-end-turn", endTurn);
        Assert.Empty(endTurnClients.CallerClient.Errors);
        var events = EventsFor(endTurnClients);
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "TURN_END_DECLARED", StringComparison.Ordinal));
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "TURN_PLAYER_ADVANCED", StringComparison.Ordinal));
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "TURN_START_BEGAN", StringComparison.Ordinal));
        Assert.Equal(2, events.Count(gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)));
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "MAIN_PHASE_BEGAN", StringComparison.Ordinal));

        var snapshot = SnapshotFor(endTurnClients, "P2");
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains("P1-EPHEMERAL-OTHER", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Equal(
            ["P2-KEEP-BASE", "P2-RUNE-001", "P2-RUNE-002"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["base"]));
        Assert.Equal(
            ["P2-MAIN-001"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["hand"]));
        Assert.Equal(
            ["P2-EPHEMERAL-BASE", "P2-EPHEMERAL-BATTLEFIELD"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
    }

    [Fact]
    public async Task P6LifecycleLastBreathSeedBroadcastsTriggerQueueInDevelopment()
    {
        const string roomId = "p6-8b-lifecycle-last-breath-core";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(
                new RecordingHubClients(),
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "lifecycle-last-breath", "seed-p6-lifecycle-last-breath");

        var playClients = new RecordingHubClients();
        var vengeance = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-VENGEANCE",
              "cardNo": "OGN·229/298",
              "targetObjectIds": ["P2-WATCHFUL-SENTINEL-001"]
            }
            """).RootElement.Clone();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-last-breath-vengeance", vengeance);
        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var passP1Clients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-last-breath-p1-pass", passPriority);
        Assert.Empty(passP1Clients.CallerClient.Errors);

        var passP2Clients = new RecordingHubClients();
        var passPriorityAgain = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p6-last-breath-p2-pass", passPriorityAgain);
        Assert.Empty(passP2Clients.CallerClient.Errors);
        var resolveEvents = EventsFor(passP2Clients);
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));

        var snapshot = SnapshotFor(passP2Clients, "P2");
        Assert.Empty(snapshot.Stack);
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-SPELL-VENGEANCE"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Equal(["P2-LAST-BREATH-DRAW-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["hand"]));
        Assert.Equal(["P2-WATCHFUL-SENTINEL-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
    }

    [Fact]
    public async Task SeedScenarioIsRejectedOutsideDevelopment()
    {
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "P2");
        var clients = new RecordingHubClients();

        await CreateHub(
                clients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Production))
            .SeedScenario("room-a", "P1", "basic-play", "seed-basic-play");

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("SeedScenario is only available in Development.", payload.Message);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
    }

    private static JsonElement SubmitDeckJson(OfficialDecklist decklist)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "SUBMIT_DECK",
            legendCardNo = decklist.LegendCardNo,
            championCardNo = decklist.ChampionCardNo,
            mainDeck = decklist.MainDeck,
            runeDeck = decklist.RuneDeck,
            battlefields = decklist.Battlefields
        });
    }

    private static JsonElement MulliganJson(IReadOnlyList<string> handObjectIds)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "MULLIGAN",
            handObjectIds
        });
    }

    private static OfficialDecklist BuildValidDeck(OfficialCardCatalog catalog)
    {
        const string legendCardNo = "UNL-181/219";
        const string championCardNo = "UNL-022/219";
        var legend = catalog.Cards.Single(card => string.Equals(card.CardNo, legendCardNo, StringComparison.Ordinal));
        var allowedColors = legend.CardColorList.ToHashSet(StringComparer.Ordinal);
        var mainDeck = new List<string> { championCardNo };
        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [catalog.Cards.Single(card => string.Equals(card.CardNo, championCardNo, StringComparison.Ordinal)).CardName] = 1
        };
        var candidates = catalog.Cards
            .Where(card => IsMainDeckCandidate(card, allowedColors))
            .Where(card => !string.Equals(card.CardNo, championCardNo, StringComparison.Ordinal))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .ToArray();

        foreach (var card in candidates)
        {
            while (mainDeck.Count < OfficialDeckValidator.MinimumMainDeckCount
                && (!nameCounts.TryGetValue(card.CardName, out var count) || count < OfficialDeckValidator.DefaultMaxCopiesByName))
            {
                mainDeck.Add(card.CardNo);
                nameCounts[card.CardName] = nameCounts.TryGetValue(card.CardName, out var current) ? current + 1 : 1;
            }

            if (mainDeck.Count >= OfficialDeckValidator.MinimumMainDeckCount)
            {
                break;
            }
        }

        Assert.Equal(OfficialDeckValidator.MinimumMainDeckCount, mainDeck.Count);
        var allowedRunes = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
            .Where(card => TraitsAllowed(card, allowedColors))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Select(card => card.CardNo)
            .ToArray();
        Assert.NotEmpty(allowedRunes);
        var runeDeck = Enumerable.Range(0, OfficialDeckValidator.RuneDeckCount)
            .Select(index => allowedRunes[index % allowedRunes.Length])
            .ToArray();
        var battlefields = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "战场", StringComparison.Ordinal))
            .GroupBy(card => card.CardName, StringComparer.Ordinal)
            .Select(group => group.OrderBy(card => card.CardNo, StringComparer.Ordinal).First())
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Take(OfficialDeckValidator.BattlefieldCount)
            .Select(card => card.CardNo)
            .ToArray();

        return new OfficialDecklist(legendCardNo, championCardNo, mainDeck, runeDeck, battlefields);
    }

    private static bool IsMainDeckCandidate(OfficialCard card, HashSet<string> allowedColors)
    {
        if (card.CardCategoryName.StartsWith("专属", StringComparison.Ordinal)
            || card.CardGroupLimit == 1
            || card.CardEffect.Contains("{{唯我}}", StringComparison.Ordinal))
        {
            return false;
        }

        return card.CardCategoryName is "单位" or "英雄单位" or "装备" or "法术"
            && TraitsAllowed(card, allowedColors);
    }

    private static bool TraitsAllowed(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardColorList.All(color => string.Equals(color, "colorless", StringComparison.Ordinal)
            || allowedColors.Contains(color));
    }

    private static GameHub CreateHub(
        RecordingHubClients clients,
        RecordingGroupManager groups,
        string connectionId,
        IMatchSessionRegistry? registry = null,
        IHostEnvironment? hostEnvironment = null)
    {
        return new GameHub(registry ?? new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance),
            hostEnvironment)
        {
            Clients = clients,
            Groups = groups,
            Context = new TestHubCallerContext(connectionId)
        };
    }

    private static async Task ReadyBothAsync(IMatchSessionRegistry registry)
    {
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .Ready("room-a", "alice", "ready-alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .Ready("room-a", "bob", "ready-bob");
    }

    private static ActionPromptDto PromptFor(RecordingHubClients clients, string playerId)
    {
        return Assert.IsType<ActionPromptDto>(
            Assert.Single(clients.GroupClient.Prompts, message => string.Equals(message.PlayerId, playerId, StringComparison.Ordinal)).Payload);
    }

    private static IReadOnlyList<GameEvent> EventsFor(RecordingHubClients clients)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<GameEvent>>(
            Assert.Single(clients.GroupClient.EventMessages).Payload);
    }

    private static SnapshotDto SnapshotFor(RecordingHubClients clients, string playerId)
    {
        return Assert.IsType<SnapshotDto>(
            Assert.Single(clients.GroupClient.Snapshots, message => string.Equals(message.PlayerId, playerId, StringComparison.Ordinal)).Payload);
    }

    private static Dictionary<string, object?> PlayerView(SnapshotDto snapshot, string playerId)
    {
        return Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
    }

    private static Dictionary<string, object?> ZoneView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["zones"]);
    }

    private static IReadOnlyList<string> StringList(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(value);
    }

    private sealed class RecordingGameClient : IGameClient
    {
        public List<WsServerMessage> JoinedMessages { get; } = [];

        public List<WsServerMessage> Snapshots { get; } = [];

        public List<WsServerMessage> Prompts { get; } = [];

        public List<WsServerMessage> EventMessages { get; } = [];

        public List<WsServerMessage> Errors { get; } = [];

        public Task Joined(WsServerMessage message)
        {
            JoinedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task Snapshot(WsServerMessage message)
        {
            Snapshots.Add(message);
            return Task.CompletedTask;
        }

        public Task Prompt(WsServerMessage message)
        {
            Prompts.Add(message);
            return Task.CompletedTask;
        }

        public Task Events(WsServerMessage message)
        {
            EventMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task Error(WsServerMessage message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHubClients : IHubCallerClients<IGameClient>
    {
        public RecordingGameClient CallerClient { get; } = new();

        public RecordingGameClient GroupClient { get; } = new();

        public IGameClient All => GroupClient;

        public IGameClient Caller => CallerClient;

        public IGameClient Others => GroupClient;

        public IGameClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => GroupClient;

        public IGameClient Client(string connectionId) => GroupClient;

        public IGameClient Clients(IReadOnlyList<string> connectionIds) => GroupClient;

        public IGameClient Group(string groupName) => GroupClient;

        public IGameClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => GroupClient;

        public IGameClient Groups(IReadOnlyList<string> groupNames) => GroupClient;

        public IGameClient OthersInGroup(string groupName) => GroupClient;

        public IGameClient User(string userId) => GroupClient;

        public IGameClient Users(IReadOnlyList<string> userIds) => GroupClient;
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> Added { get; } = [];

        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestHubCallerContext(string connectionId) : HubCallerContext
    {
        private readonly Dictionary<object, object?> items = new();

        public override string ConnectionId => connectionId;

        public override string? UserIdentifier => null;

        public override ClaimsPrincipal? User => null;

        public override IDictionary<object, object?> Items => items;

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
        }
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingMatchPlayerStore : IMatchPlayerStore
    {
        public List<SavedPlayer> Saved { get; } = [];

        public ValueTask SavePlayerSessionAsync(
            string roomId,
            string playerId,
            string seat,
            string reconnectTokenHash,
            CancellationToken cancellationToken)
        {
            Saved.Add(new SavedPlayer(roomId, playerId, seat, reconnectTokenHash));
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> HasReconnectTokenHashAsync(
            string roomId,
            string playerId,
            string reconnectTokenHash,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Saved.Any(saved =>
                string.Equals(saved.RoomId, roomId, StringComparison.Ordinal)
                && string.Equals(saved.PlayerId, playerId, StringComparison.Ordinal)
                && string.Equals(saved.ReconnectTokenHash, reconnectTokenHash, StringComparison.Ordinal)));
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Riftbound.ConformanceTests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed record SavedPlayer(
        string RoomId,
        string PlayerId,
        string Seat,
        string ReconnectTokenHash);
}
