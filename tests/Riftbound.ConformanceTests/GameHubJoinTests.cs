using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Riftbound.Api.Hubs;
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
        Assert.False(playP2Prompt.Actionable);
        Assert.Contains("WAIT", playP2Prompt.Actions);

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
