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

        var prompt = Assert.IsType<ActionPromptDto>(promptMessage.Payload);
        Assert.Equal("alice", prompt.PlayerId);
        Assert.Equal(snapshot.Tick, prompt.SnapshotTick);
        Assert.False(string.IsNullOrWhiteSpace(prompt.PromptId));
        var candidate = Assert.Single(prompt.Candidates ?? []);
        Assert.Equal("READY", candidate.Action);
        Assert.Equal("准备", candidate.Label);
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
