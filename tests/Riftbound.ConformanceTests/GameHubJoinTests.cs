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
    public async Task HubMessagesCarryProtocolVersionsOnJoinSnapshotPromptAndError()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var joinClients = new RecordingHubClients();
        await CreateHub(joinClients, new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");

        var joinMessage = Assert.Single(joinClients.CallerClient.JoinedMessages);
        var snapshotMessage = Assert.Single(joinClients.CallerClient.Snapshots);
        var promptMessage = Assert.Single(joinClients.CallerClient.Prompts);
        Assert.Equal(MessageType.JOIN, joinMessage.Type);
        Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
        Assert.Equal(MessageType.PROMPT, promptMessage.Type);
        AssertProtocolDefaults(joinMessage);
        AssertProtocolDefaults(snapshotMessage);
        AssertProtocolDefaults(promptMessage);

        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");

        var errorClients = new RecordingHubClients();
        await CreateHub(errorClients, new RecordingGroupManager(), "connection-3", registry)
            .JoinRoom("room-a", "charlie");

        var errorMessage = Assert.Single(errorClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, errorMessage.Type);
        AssertProtocolDefaults(errorMessage);
        var error = Assert.IsType<ErrorDto>(errorMessage.Payload);
        Assert.Equal(ErrorCodes.RoomFull, error.Code);
    }

    [Fact]
    public async Task ReconnectMessagesCarryProtocolVersionsOnReconnectSnapshotAndPrompt()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var joinClients = new RecordingHubClients();
        await CreateHub(joinClients, new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        var join = Assert.IsType<PlayerSessionDto>(Assert.Single(joinClients.CallerClient.JoinedMessages).Payload);

        var reconnectClients = new RecordingHubClients();
        await CreateHub(reconnectClients, new RecordingGroupManager(), "connection-2", registry)
            .Reconnect("room-a", " alice ", join.ReconnectToken);

        var reconnectMessage = Assert.Single(reconnectClients.CallerClient.JoinedMessages);
        var snapshotMessage = Assert.Single(reconnectClients.CallerClient.Snapshots);
        var promptMessage = Assert.Single(reconnectClients.CallerClient.Prompts);
        Assert.Equal(MessageType.RECONNECT, reconnectMessage.Type);
        Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
        Assert.Equal(MessageType.PROMPT, promptMessage.Type);
        AssertProtocolDefaults(reconnectMessage);
        AssertProtocolDefaults(snapshotMessage);
        AssertProtocolDefaults(promptMessage);
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
        Assert.Equal("房间已有两名玩家。", payload.Message);
        Assert.DoesNotContain("room already has two players", payload.Message, StringComparison.Ordinal);
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

        var reconnectSnapshotMessage = Assert.Single(reconnectClients.CallerClient.Snapshots);
        var reconnectPromptMessage = Assert.Single(reconnectClients.CallerClient.Prompts);
        Assert.Equal(MessageType.SNAPSHOT, reconnectSnapshotMessage.Type);
        Assert.Equal(MessageType.PROMPT, reconnectPromptMessage.Type);
        Assert.Equal("alice", reconnectSnapshotMessage.PlayerId);
        Assert.Equal("alice", reconnectPromptMessage.PlayerId);
        Assert.Empty(reconnectClients.GroupClient.Snapshots);
        Assert.Empty(reconnectClients.GroupClient.Prompts);

        var reconnectSnapshotJson = JsonSerializer.Serialize(reconnectSnapshotMessage);
        var reconnectPromptJson = JsonSerializer.Serialize(reconnectPromptMessage);
        Assert.DoesNotContain(join.ReconnectToken, reconnectSnapshotJson, StringComparison.Ordinal);
        Assert.DoesNotContain(reconnect.ReconnectToken, reconnectSnapshotJson, StringComparison.Ordinal);
        Assert.DoesNotContain(join.ReconnectToken, reconnectPromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain(reconnect.ReconnectToken, reconnectPromptJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReconnectWithRotatedOldTokenDoesNotJoinGroupsOrLeakSessionData()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var joinClients = new RecordingHubClients();
        await CreateHub(joinClients, new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        var join = Assert.IsType<PlayerSessionDto>(Assert.Single(joinClients.CallerClient.JoinedMessages).Payload);

        var reconnectClients = new RecordingHubClients();
        await CreateHub(reconnectClients, new RecordingGroupManager(), "connection-2", registry)
            .Reconnect("room-a", "alice", join.ReconnectToken);
        var reconnect = Assert.IsType<PlayerSessionDto>(
            Assert.Single(reconnectClients.CallerClient.JoinedMessages).Payload);
        Assert.NotEqual(join.ReconnectToken, reconnect.ReconnectToken);

        var staleClients = new RecordingHubClients();
        var staleGroups = new RecordingGroupManager();
        await CreateHub(staleClients, staleGroups, "connection-3", registry)
            .Reconnect("room-a", " alice ", join.ReconnectToken);

        Assert.Empty(staleGroups.Added);
        Assert.Empty(staleClients.CallerClient.JoinedMessages);
        Assert.Empty(staleClients.CallerClient.Snapshots);
        Assert.Empty(staleClients.CallerClient.Prompts);
        Assert.Empty(staleClients.GroupClient.JoinedMessages);
        Assert.Empty(staleClients.GroupClient.Snapshots);
        Assert.Empty(staleClients.GroupClient.Prompts);

        var error = Assert.Single(staleClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal("room-a", error.RoomId);
        Assert.Equal("alice", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.InvalidReconnectToken, payload.Code);
        Assert.Equal("重连令牌无效。", payload.Message);

        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain(join.ReconnectToken, errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(reconnect.ReconnectToken, errorJson, StringComparison.Ordinal);
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
    public async Task ReconnectPersistsRotatedReconnectTokenHashWithoutPlaintext()
    {
        var playerStore = new RecordingMatchPlayerStore();
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            NoopMatchRecoveryStore.Instance,
            playerStore);
        var joinClients = new RecordingHubClients();
        await CreateHub(joinClients, new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        var join = Assert.IsType<PlayerSessionDto>(Assert.Single(joinClients.CallerClient.JoinedMessages).Payload);

        var reconnectClients = new RecordingHubClients();
        await CreateHub(reconnectClients, new RecordingGroupManager(), "connection-2", registry)
            .Reconnect("room-a", " alice ", join.ReconnectToken);
        var reconnect = Assert.IsType<PlayerSessionDto>(
            Assert.Single(reconnectClients.CallerClient.JoinedMessages).Payload);

        Assert.NotEqual(join.ReconnectToken, reconnect.ReconnectToken);
        Assert.Equal(2, playerStore.Saved.Count);
        var initialSave = playerStore.Saved[0];
        var rotatedSave = playerStore.Saved[1];
        Assert.Equal("room-a", rotatedSave.RoomId);
        Assert.Equal("alice", rotatedSave.PlayerId);
        Assert.Equal("P1", rotatedSave.Seat);
        Assert.Equal(ReconnectTokenHasher.Hash(join.ReconnectToken), initialSave.ReconnectTokenHash);
        Assert.Equal(ReconnectTokenHasher.Hash(reconnect.ReconnectToken), rotatedSave.ReconnectTokenHash);
        Assert.NotEqual(initialSave.ReconnectTokenHash, rotatedSave.ReconnectTokenHash);
        Assert.DoesNotContain(join.ReconnectToken, rotatedSave.ReconnectTokenHash, StringComparison.Ordinal);
        Assert.DoesNotContain(reconnect.ReconnectToken, rotatedSave.ReconnectTokenHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReconnectWithWhitespaceWrappedTokenRejoinsGroupsAndPersistsRotatedHash()
    {
        var playerStore = new RecordingMatchPlayerStore();
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            NoopMatchRecoveryStore.Instance,
            playerStore);
        var joinClients = new RecordingHubClients();
        await CreateHub(joinClients, new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        var join = Assert.IsType<PlayerSessionDto>(Assert.Single(joinClients.CallerClient.JoinedMessages).Payload);

        var reconnectClients = new RecordingHubClients();
        var reconnectGroups = new RecordingGroupManager();
        await CreateHub(reconnectClients, reconnectGroups, "connection-2", registry)
            .Reconnect("room-a", " alice ", $" \t{join.ReconnectToken}\n ");

        Assert.Contains(("connection-2", "room:room-a"), reconnectGroups.Added);
        Assert.Contains(("connection-2", "room:room-a:player:alice"), reconnectGroups.Added);
        var reconnectMessage = Assert.Single(reconnectClients.CallerClient.JoinedMessages);
        Assert.Equal(MessageType.RECONNECT, reconnectMessage.Type);
        Assert.Equal("room-a", reconnectMessage.RoomId);
        Assert.Equal("alice", reconnectMessage.PlayerId);
        var reconnect = Assert.IsType<PlayerSessionDto>(reconnectMessage.Payload);
        Assert.Equal("alice", reconnect.PlayerId);
        Assert.Equal("P1", reconnect.Seat);
        Assert.NotEqual(join.ReconnectToken, reconnect.ReconnectToken);

        var reconnectSnapshotMessage = Assert.Single(reconnectClients.CallerClient.Snapshots);
        var reconnectPromptMessage = Assert.Single(reconnectClients.CallerClient.Prompts);
        Assert.Equal("alice", reconnectSnapshotMessage.PlayerId);
        Assert.Equal("alice", reconnectPromptMessage.PlayerId);

        Assert.Equal(2, playerStore.Saved.Count);
        Assert.Equal(ReconnectTokenHasher.Hash(join.ReconnectToken), playerStore.Saved[0].ReconnectTokenHash);
        Assert.Equal(ReconnectTokenHasher.Hash(reconnect.ReconnectToken), playerStore.Saved[1].ReconnectTokenHash);
    }

    [Fact]
    public async Task ReconnectWithRotatedOldPersistedTokenDoesNotJoinGroupsOrLeakSessionData()
    {
        var playerStore = new RecordingMatchPlayerStore();
        var registry = new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            NoopMatchRecoveryStore.Instance,
            playerStore);
        var joinClients = new RecordingHubClients();
        await CreateHub(joinClients, new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        var join = Assert.IsType<PlayerSessionDto>(Assert.Single(joinClients.CallerClient.JoinedMessages).Payload);

        var reconnectClients = new RecordingHubClients();
        await CreateHub(reconnectClients, new RecordingGroupManager(), "connection-2", registry)
            .Reconnect("room-a", "alice", join.ReconnectToken);
        var reconnect = Assert.IsType<PlayerSessionDto>(
            Assert.Single(reconnectClients.CallerClient.JoinedMessages).Payload);
        Assert.NotEqual(join.ReconnectToken, reconnect.ReconnectToken);
        Assert.Equal(2, playerStore.Saved.Count);
        Assert.Equal(ReconnectTokenHasher.Hash(join.ReconnectToken), playerStore.Saved[0].ReconnectTokenHash);
        Assert.Equal(ReconnectTokenHasher.Hash(reconnect.ReconnectToken), playerStore.Saved[1].ReconnectTokenHash);

        var staleClients = new RecordingHubClients();
        var staleGroups = new RecordingGroupManager();
        await CreateHub(staleClients, staleGroups, "connection-3", registry)
            .Reconnect("room-a", " alice ", join.ReconnectToken);

        Assert.Equal(2, playerStore.Saved.Count);
        Assert.Empty(staleGroups.Added);
        Assert.Empty(staleClients.CallerClient.JoinedMessages);
        Assert.Empty(staleClients.CallerClient.Snapshots);
        Assert.Empty(staleClients.CallerClient.Prompts);
        Assert.Empty(staleClients.GroupClient.JoinedMessages);
        Assert.Empty(staleClients.GroupClient.Snapshots);
        Assert.Empty(staleClients.GroupClient.Prompts);

        var error = Assert.Single(staleClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal("room-a", error.RoomId);
        Assert.Equal("alice", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.InvalidReconnectToken, payload.Code);
        Assert.Equal("重连令牌无效。", payload.Message);

        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain(join.ReconnectToken, errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(reconnect.ReconnectToken, errorJson, StringComparison.Ordinal);
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
        Assert.Equal("重连令牌无效。", payload.Message);
        Assert.DoesNotContain("invalid reconnect token", payload.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReconnectWithInvalidTokenDoesNotJoinGroupsOrLeakSessionData()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var joinClients = new RecordingHubClients();
        await CreateHub(joinClients, new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        var join = Assert.IsType<PlayerSessionDto>(Assert.Single(joinClients.CallerClient.JoinedMessages).Payload);

        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        await CreateHub(clients, groups, "connection-2", registry)
            .Reconnect("room-a", " alice ", "wrong-token");

        Assert.Empty(groups.Added);
        Assert.Empty(clients.CallerClient.JoinedMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.JoinedMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        var error = Assert.Single(clients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal("room-a", error.RoomId);
        Assert.Equal("alice", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.InvalidReconnectToken, payload.Code);
        Assert.Equal("重连令牌无效。", payload.Message);

        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain(join.ReconnectToken, errorJson, StringComparison.Ordinal);
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
    public async Task RequestSnapshotMessagesCarryProtocolVersionsOnSnapshotAndPrompt()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");

        var clients = new RecordingHubClients();
        await CreateHub(clients, new RecordingGroupManager(), "connection-2", registry)
            .RequestSnapshot("room-a", " alice ");

        var snapshotMessage = Assert.Single(clients.CallerClient.Snapshots);
        var promptMessage = Assert.Single(clients.CallerClient.Prompts);
        Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
        Assert.Equal(MessageType.PROMPT, promptMessage.Type);
        Assert.Equal("alice", snapshotMessage.PlayerId);
        Assert.Equal("alice", promptMessage.PlayerId);
        AssertProtocolDefaults(snapshotMessage);
        AssertProtocolDefaults(promptMessage);
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
        Assert.Equal("玩家不在房间中。", payload.Message);
        Assert.DoesNotContain("player is not in room", payload.Message, StringComparison.Ordinal);
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
        Assert.Equal("玩家不在房间中。", payload.Message);
        Assert.DoesNotContain("player is not in room", payload.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitIntentUnknownPlayerMessagesCarryProtocolVersionsOnPlayerNotInRoomError()
    {
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1")
            .SubmitIntent("room-a", " alice ", "intent-unknown-player-protocol-envelope", cmd);

        var errorMessage = Assert.Single(clients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, errorMessage.Type);
        Assert.Equal("room-a", errorMessage.RoomId);
        Assert.Equal("alice", errorMessage.PlayerId);
        AssertProtocolDefaults(errorMessage);
        var payload = Assert.IsType<ErrorDto>(errorMessage.Payload);
        Assert.Equal(ErrorCodes.PlayerNotInRoom, payload.Code);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
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
        Assert.Equal("对局尚未开始。", payload.Message);
        Assert.DoesNotContain("match has not started", payload.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitIntentPreStartMessagesCarryProtocolVersionsOnMatchNotStartedError()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", " alice ", "intent-before-ready-protocol-envelope", cmd);

        var errorMessage = Assert.Single(clients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, errorMessage.Type);
        Assert.Equal("room-a", errorMessage.RoomId);
        Assert.Equal("alice", errorMessage.PlayerId);
        AssertProtocolDefaults(errorMessage);
        var payload = Assert.IsType<ErrorDto>(errorMessage.Payload);
        Assert.Equal(ErrorCodes.MatchNotStarted, payload.Code);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
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
        Assert.Equal("客户端行动编号不能为空。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(clients.GroupClient.EventMessages);
    }

    [Fact]
    public async Task SubmitIntentPreflightMessagesCarryProtocolVersionsOnClientIntentIdError()
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
            .SubmitIntent("room-a", " alice ", " ", cmd);

        var errorMessage = Assert.Single(clients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, errorMessage.Type);
        Assert.Equal("room-a", errorMessage.RoomId);
        Assert.Equal("alice", errorMessage.PlayerId);
        AssertProtocolDefaults(errorMessage);
        var payload = Assert.IsType<ErrorDto>(errorMessage.Payload);
        Assert.Equal(ErrorCodes.ClientIntentIdRequired, payload.Code);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
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
    public async Task ReadyMessagesCarryProtocolVersionsOnReadyStartSnapshotsAndPrompts()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");

        var aliceReadyClients = new RecordingHubClients();
        await CreateHub(aliceReadyClients, new RecordingGroupManager(), "connection-1", registry)
            .Ready("room-a", " alice ", "ready-alice-protocol-envelope");

        Assert.Empty(aliceReadyClients.CallerClient.Errors);
        var readyMessage = Assert.Single(aliceReadyClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.READY, readyMessage.Type);
        Assert.Equal("alice", readyMessage.PlayerId);
        AssertProtocolDefaults(readyMessage);

        Assert.NotEmpty(aliceReadyClients.GroupClient.Snapshots);
        foreach (var snapshotMessage in aliceReadyClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.NotEmpty(aliceReadyClients.GroupClient.Prompts);
        foreach (var promptMessage in aliceReadyClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        var bobReadyClients = new RecordingHubClients();
        await CreateHub(bobReadyClients, new RecordingGroupManager(), "connection-2", registry)
            .Ready("room-a", " bob ", "ready-bob-protocol-envelope");

        Assert.Empty(bobReadyClients.CallerClient.Errors);
        var startMessage = Assert.Single(bobReadyClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.START, startMessage.Type);
        Assert.Equal("bob", startMessage.PlayerId);
        AssertProtocolDefaults(startMessage);

        Assert.NotEmpty(bobReadyClients.GroupClient.Snapshots);
        foreach (var snapshotMessage in bobReadyClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.NotEmpty(bobReadyClients.GroupClient.Prompts);
        foreach (var promptMessage in bobReadyClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }
    }

    [Fact]
    public async Task ReadyReplayMessagesCarryProtocolVersionsOnReadySnapshotsAndPrompts()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .Ready("room-a", "alice", "ready-replay-protocol-envelope");

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedReadyMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .Ready("room-a", " alice ", "ready-replay-protocol-envelope");

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayReadyMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.READY, replayReadyMessage.Type);
        Assert.Equal("alice", replayReadyMessage.PlayerId);
        Assert.Equal(acceptedReadyMessage.ServerTick, replayReadyMessage.ServerTick);
        AssertProtocolDefaults(replayReadyMessage);

        Assert.NotEmpty(replayClients.GroupClient.Snapshots);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.NotEmpty(replayClients.GroupClient.Prompts);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }
    }

    [Fact]
    public async Task SubmitIntentMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var clients = new RecordingHubClients();
        var pass = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", " alice ", "intent-pass-protocol-envelope", pass);

        Assert.Empty(clients.CallerClient.Errors);
        var eventsMessage = Assert.Single(clients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, eventsMessage.Type);
        Assert.Equal("alice", eventsMessage.PlayerId);
        AssertProtocolDefaults(eventsMessage);

        Assert.NotEmpty(clients.GroupClient.Snapshots);
        foreach (var snapshotMessage in clients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.NotEmpty(clients.GroupClient.Prompts);
        foreach (var promptMessage in clients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }
    }

    [Fact]
    public async Task SubmitIntentReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var pass = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-replay-protocol-envelope", pass);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", " alice ", "intent-replay-protocol-envelope", pass);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal("alice", replayEventsMessage.PlayerId);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);

        Assert.NotEmpty(replayClients.GroupClient.Snapshots);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.NotEmpty(replayClients.GroupClient.Prompts);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }
    }

    [Fact]
    public async Task SubmitDeckReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        const string roomId = "official-hub-submit-deck-replay-protocol-envelope";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var submitDeck = SubmitDeckJson(p1Deck);
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "submit-deck-replay-protocol-envelope", submitDeck);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        var acceptedEventKinds = EventsFor(acceptedClients)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "submit-deck-replay-protocol-envelope", submitDeck);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal("P1", replayEventsMessage.PlayerId);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);
        Assert.Equal(
            acceptedEventKinds,
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());

        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());

        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task MulliganReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        const string roomId = "official-hub-mulligan-replay-protocol-envelope";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "submit-deck-p1-protocol-envelope", SubmitDeckJson(p1Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "submit-deck-p2-protocol-envelope", SubmitDeckJson(p2Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .Ready(roomId, "P1", "ready-p1-protocol-envelope");
        var readyClients = new RecordingHubClients();
        await CreateHub(readyClients, new RecordingGroupManager(), "connection-2", registry)
            .Ready(roomId, "P2", "ready-p2-protocol-envelope");

        var startSnapshot = SnapshotFor(readyClients, "P1");
        Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(startSnapshot.Timing["phase"]));
        var activePlayerId = startSnapshot.ActivePlayerId;
        var activeConnectionId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";
        var activeHand = StringList(ZoneView(PlayerView(SnapshotFor(readyClients, activePlayerId), activePlayerId))["hand"]);
        var mulligan = MulliganJson(activeHand.Take(1).ToArray());
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "mulligan-replay-protocol-envelope", mulligan);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        var acceptedEventKinds = EventsFor(acceptedClients)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, $" {activePlayerId} ", "mulligan-replay-protocol-envelope", mulligan);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal(activePlayerId, replayEventsMessage.PlayerId);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);
        Assert.Equal(
            acceptedEventKinds,
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());

        Assert.Equal(2, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());

        Assert.Equal(2, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task TapRuneReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        const string roomId = "official-hub-tap-rune-replay-protocol-envelope";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "submit-deck-p1-tap-rune-protocol-envelope", SubmitDeckJson(p1Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "submit-deck-p2-tap-rune-protocol-envelope", SubmitDeckJson(p2Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .Ready(roomId, "P1", "ready-p1-tap-rune-protocol-envelope");
        var readyClients = new RecordingHubClients();
        await CreateHub(readyClients, new RecordingGroupManager(), "connection-2", registry)
            .Ready(roomId, "P2", "ready-p2-tap-rune-protocol-envelope");

        var startSnapshot = SnapshotFor(readyClients, "P1");
        Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(startSnapshot.Timing["phase"]));
        var activePlayerId = startSnapshot.ActivePlayerId;
        var secondPlayerId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "P2" : "P1";
        var activeConnectionId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";
        var secondConnectionId = string.Equals(secondPlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";

        var activeHand = StringList(ZoneView(PlayerView(SnapshotFor(readyClients, activePlayerId), activePlayerId))["hand"]);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(
                roomId,
                activePlayerId,
                "mulligan-active-tap-rune-protocol-envelope",
                MulliganJson(activeHand.Take(1).ToArray()));
        var secondMulliganClients = new RecordingHubClients();
        await CreateHub(secondMulliganClients, new RecordingGroupManager(), secondConnectionId, registry)
            .SubmitIntent(roomId, secondPlayerId, "mulligan-second-tap-rune-protocol-envelope", MulliganJson([]));

        var mainSnapshot = SnapshotFor(secondMulliganClients, activePlayerId);
        Assert.Equal(MatchPhases.Main, Assert.IsType<string>(mainSnapshot.Timing["phase"]));
        Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(mainSnapshot.Timing["timingState"]));
        var mainPrompt = PromptFor(secondMulliganClients, activePlayerId);
        Assert.True(mainPrompt.Actionable);
        var tapRuneCandidate = Assert.Single(
            mainPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "TAP_RUNE", StringComparison.Ordinal));
        Assert.True(tapRuneCandidate.Enabled);
        Assert.NotNull(tapRuneCandidate.Sources);
        var runeSourceId = tapRuneCandidate.Sources.First().Id;
        var tapRune = JsonSerializer.SerializeToElement(new
        {
            cmdType = "TAP_RUNE",
            sourceObjectId = runeSourceId
        });
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "tap-rune-replay-protocol-envelope", tapRune);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        var acceptedEventKinds = EventsFor(acceptedClients)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, $" {activePlayerId} ", "tap-rune-replay-protocol-envelope", tapRune);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal(activePlayerId, replayEventsMessage.PlayerId);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);
        Assert.Equal(
            acceptedEventKinds,
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());

        Assert.Equal(2, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());

        Assert.Equal(2, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task RecycleRuneReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        const string roomId = "official-hub-recycle-rune-replay-protocol-envelope";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "submit-deck-p1-recycle-rune-protocol-envelope", SubmitDeckJson(p1Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "submit-deck-p2-recycle-rune-protocol-envelope", SubmitDeckJson(p2Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .Ready(roomId, "P1", "ready-p1-recycle-rune-protocol-envelope");
        var readyClients = new RecordingHubClients();
        await CreateHub(readyClients, new RecordingGroupManager(), "connection-2", registry)
            .Ready(roomId, "P2", "ready-p2-recycle-rune-protocol-envelope");

        var startSnapshot = SnapshotFor(readyClients, "P1");
        Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(startSnapshot.Timing["phase"]));
        var activePlayerId = startSnapshot.ActivePlayerId;
        var secondPlayerId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "P2" : "P1";
        var activeConnectionId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";
        var secondConnectionId = string.Equals(secondPlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";

        var activeHand = StringList(ZoneView(PlayerView(SnapshotFor(readyClients, activePlayerId), activePlayerId))["hand"]);
        var activeMulliganClients = new RecordingHubClients();
        await CreateHub(activeMulliganClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(
                roomId,
                activePlayerId,
                "mulligan-active-recycle-rune-protocol-envelope",
                MulliganJson(activeHand.Take(1).ToArray()));
        Assert.Empty(activeMulliganClients.CallerClient.Errors);

        var secondMulliganClients = new RecordingHubClients();
        await CreateHub(secondMulliganClients, new RecordingGroupManager(), secondConnectionId, registry)
            .SubmitIntent(roomId, secondPlayerId, "mulligan-second-recycle-rune-protocol-envelope", MulliganJson([]));
        Assert.Empty(secondMulliganClients.CallerClient.Errors);

        var mainSnapshot = SnapshotFor(secondMulliganClients, activePlayerId);
        Assert.Equal(MatchPhases.Main, Assert.IsType<string>(mainSnapshot.Timing["phase"]));
        Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(mainSnapshot.Timing["timingState"]));
        var mainPrompt = PromptFor(secondMulliganClients, activePlayerId);
        Assert.True(mainPrompt.Actionable);
        var tapRuneCandidate = Assert.Single(
            mainPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "TAP_RUNE", StringComparison.Ordinal));
        Assert.True(tapRuneCandidate.Enabled);
        Assert.NotNull(tapRuneCandidate.Sources);
        var runeSourceId = tapRuneCandidate.Sources.First().Id;
        var tapRuneClients = new RecordingHubClients();

        await CreateHub(tapRuneClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "tap-rune-before-recycle-protocol-envelope", JsonSerializer.SerializeToElement(new
            {
                cmdType = "TAP_RUNE",
                sourceObjectId = runeSourceId
            }));

        Assert.Empty(tapRuneClients.CallerClient.Errors);
        var postTapPrompt = PromptFor(tapRuneClients, activePlayerId);
        var recycleRuneCandidate = Assert.Single(
            postTapPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "RECYCLE_RUNE", StringComparison.Ordinal));
        Assert.True(recycleRuneCandidate.Enabled);
        Assert.Contains(recycleRuneCandidate.Sources ?? [], source => string.Equals(source.Id, runeSourceId, StringComparison.Ordinal));
        var recycleRune = JsonSerializer.SerializeToElement(new
        {
            cmdType = "RECYCLE_RUNE",
            sourceObjectId = runeSourceId
        });
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "recycle-rune-replay-protocol-envelope", recycleRune);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        var acceptedEventKinds = EventsFor(acceptedClients)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, $" {activePlayerId} ", "recycle-rune-replay-protocol-envelope", recycleRune);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal(activePlayerId, replayEventsMessage.PlayerId);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);
        Assert.Equal(
            acceptedEventKinds,
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());

        Assert.Equal(2, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());

        Assert.Equal(2, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task PassReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .Pass("room-a", "alice", "pass-replay-protocol-envelope");

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        var acceptedEventKinds = EventsFor(acceptedClients)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .Pass("room-a", " alice ", "pass-replay-protocol-envelope");

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal("alice", replayEventsMessage.PlayerId);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);
        Assert.Equal(
            acceptedEventKinds,
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());

        Assert.NotEmpty(replayClients.GroupClient.Snapshots);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.NotEmpty(replayClients.GroupClient.Prompts);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }
    }

    [Fact]
    public async Task EndTurnReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .EndTurn("room-a", "alice", "end-turn-replay-protocol-envelope");

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        var acceptedEventKinds = EventsFor(acceptedClients)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .EndTurn("room-a", " alice ", "end-turn-replay-protocol-envelope");

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal("alice", replayEventsMessage.PlayerId);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);
        Assert.Equal(
            acceptedEventKinds,
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());

        Assert.NotEmpty(replayClients.GroupClient.Snapshots);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.NotEmpty(replayClients.GroupClient.Prompts);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }
    }

    [Fact]
    public async Task ReadyWrapperDuplicateClientIntentRawPayloadReplaysButSubmitIntentChangedRawConflictsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");

        var acceptedClients = new RecordingHubClients();
        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .Ready("room-a", "alice", "ready-same");

        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.READY, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.DoesNotContain(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedJournalCount = journal.Entries.Count;
        var readyEntry = Assert.Single(journal.Entries);
        Assert.Equal("alice", readyEntry.PlayerId);
        Assert.Equal("ready-same", readyEntry.ClientIntentId);
        Assert.Equal("READY", readyEntry.CommandType);
        Assert.NotNull(readyEntry.RawCommand);
        Assert.Equal("READY", readyEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.False(readyEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .Ready("room-a", "alice", "ready-same");

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.READY, replayMessage.Type);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(2, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(2, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedReady = JsonDocument.Parse("""{"cmdType":"READY","clientNote":"changed"}""").RootElement.Clone();

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "ready-same", changedReady);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } rawCommand
            && rawCommand.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));
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
        var mulliganCandidate = Assert.Single(
            activePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "MULLIGAN", StringComparison.Ordinal));
        Assert.Equal(activeHand, (mulliganCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.All(
            mulliganCandidate.Sources ?? [],
            source =>
            {
                Assert.DoesNotContain(source.Id, source.Label, StringComparison.Ordinal);
                Assert.DoesNotContain("-MAIN-", source.Label, StringComparison.Ordinal);
                Assert.Equal("起手调整候选", source.Reason);
                Assert.DoesNotContain("opening hand mulligan candidate", source.Reason, StringComparison.Ordinal);
            });
        Assert.NotNull(mulliganCandidate.Metadata);
        Assert.Equal(2, mulliganCandidate.Metadata["maxSelectionCount"]);
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

        var nextPlayerId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "P2" : "P1";
        var endTurnClients = new RecordingHubClients();
        await CreateHub(
                endTurnClients,
                new RecordingGroupManager(),
                string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "connection-1" : "connection-2",
                registry)
            .SubmitIntent(roomId, activePlayerId, "end-turn-active", JsonSerializer.SerializeToElement(new
            {
                cmdType = "END_TURN"
            }));

        Assert.Empty(endTurnClients.CallerClient.Errors);
        var endTurnEvents = EventsFor(endTurnClients);
        Assert.Contains(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_END_DECLARED", StringComparison.Ordinal));
        Assert.Contains(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_PLAYER_ADVANCED", StringComparison.Ordinal));
        Assert.Contains(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_START_BEGAN", StringComparison.Ordinal));
        Assert.Contains(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Contains(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Contains(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "MAIN_PHASE_BEGAN", StringComparison.Ordinal));
        var nextSnapshot = SnapshotFor(endTurnClients, nextPlayerId);
        Assert.Equal(nextPlayerId, nextSnapshot.ActivePlayerId);
        Assert.Equal(nextPlayerId, Assert.IsType<string>(nextSnapshot.Timing["turnPlayerId"]));
        Assert.Equal(MatchPhases.Main, Assert.IsType<string>(nextSnapshot.Timing["phase"]));
        Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(nextSnapshot.Timing["timingState"]));
        var nextPrompt = PromptFor(endTurnClients, nextPlayerId);
        Assert.True(nextPrompt.Actionable);
        Assert.Contains("TAP_RUNE", nextPrompt.Actions);
        Assert.Contains("SURRENDER", nextPrompt.Actions);

        var surrenderClients = new RecordingHubClients();
        await CreateHub(
                surrenderClients,
                new RecordingGroupManager(),
                string.Equals(nextPlayerId, "P1", StringComparison.Ordinal) ? "connection-1" : "connection-2",
                registry)
            .SubmitIntent(roomId, nextPlayerId, "surrender-next-turn-player", JsonSerializer.SerializeToElement(new
            {
                cmdType = "SURRENDER"
            }));

        Assert.Empty(surrenderClients.CallerClient.Errors);
        var surrenderEvent = Assert.Single(
            EventsFor(surrenderClients),
            gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        Assert.Equal(activePlayerId, Assert.IsType<string>(surrenderEvent.Payload["winnerPlayerId"]));
        Assert.Equal(nextPlayerId, Assert.IsType<string>(surrenderEvent.Payload["surrenderedPlayerId"]));
        Assert.Equal("SURRENDER", Assert.IsType<string>(surrenderEvent.Payload["reason"]));
        var surrenderSnapshot = SnapshotFor(surrenderClients, activePlayerId);
        Assert.Equal(MatchStatuses.Finished, Assert.IsType<string>(surrenderSnapshot.Timing["roomStatus"]));
        Assert.Equal(activePlayerId, Assert.IsType<string>(surrenderSnapshot.Timing["winnerPlayerId"]));
    }

    [Fact]
    public async Task SubmitDeckDuplicateClientIntentReorderedRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "official-hub-submit-deck-reordered-raw-idempotency";
        const string clientIntentId = "submit-deck-same";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");

        var raw = SubmitDeckJson(p1Deck);
        var reordered = JsonSerializer.SerializeToElement(new
        {
            battlefields = p1Deck.Battlefields,
            runeDeck = p1Deck.RuneDeck,
            mainDeck = p1Deck.MainDeck,
            championCardNo = p1Deck.ChampionCardNo,
            legendCardNo = p1Deck.LegendCardNo,
            cmdType = "SUBMIT_DECK"
        });
        var changed = JsonSerializer.SerializeToElement(new
        {
            cmdType = "SUBMIT_DECK",
            legendCardNo = p1Deck.LegendCardNo,
            championCardNo = p1Deck.ChampionCardNo,
            mainDeck = p1Deck.MainDeck,
            runeDeck = p1Deck.RuneDeck,
            battlefields = p1Deck.Battlefields,
            clientNote = "changed"
        });

        Assert.NotEqual(raw.GetRawText(), reordered.GetRawText());
        Assert.Equal(MatchStateHasher.HashValue(raw), MatchStateHasher.HashValue(reordered));
        Assert.NotEqual(MatchStateHasher.HashValue(raw), MatchStateHasher.HashValue(changed));

        var acceptedClients = new RecordingHubClients();
        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, raw);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal));
        var acceptedMessageType = acceptedMessage.Type;
        var acceptedServerTick = acceptedMessage.ServerTick;
        var acceptedEventKinds = acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray();
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptActions = acceptedClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(1, acceptedJournalCount);
        var submitEntry = Assert.Single(journal.Entries);
        Assert.Equal(roomId, submitEntry.RoomId);
        Assert.Equal("P1", submitEntry.PlayerId);
        Assert.Equal("SUBMIT_DECK", submitEntry.CommandType);
        Assert.Equal(clientIntentId, submitEntry.ClientIntentId);
        Assert.NotNull(submitEntry.RawCommand);
        Assert.Equal("SUBMIT_DECK", submitEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.False(submitEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, reordered);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(acceptedMessageType, replayMessage.Type);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal(acceptedServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEventKinds,
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        var replayPromptActions = replayClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        Assert.Equal(acceptedPromptActions, replayPromptActions);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, changed);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } rawCommand
            && rawCommand.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SurrenderDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "official-hub-surrender-raw-idempotency";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "submit-deck-p1", SubmitDeckJson(p1Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "submit-deck-p2", SubmitDeckJson(p2Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .Ready(roomId, "P1", "ready-official-p1");
        var readyClients = new RecordingHubClients();
        await CreateHub(readyClients, new RecordingGroupManager(), "connection-2", registry)
            .Ready(roomId, "P2", "ready-official-p2");

        var startSnapshot = SnapshotFor(readyClients, "P1");
        Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(startSnapshot.Timing["phase"]));
        var activePlayerId = startSnapshot.ActivePlayerId;
        var secondPlayerId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "P2" : "P1";
        var activeConnectionId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";
        var secondConnectionId = string.Equals(secondPlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";

        var activeSnapshot = SnapshotFor(readyClients, activePlayerId);
        var activeHand = StringList(ZoneView(PlayerView(activeSnapshot, activePlayerId))["hand"]);
        var activeMulliganClients = new RecordingHubClients();
        await CreateHub(activeMulliganClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "mulligan-active", MulliganJson(activeHand.Take(1).ToArray()));
        Assert.Empty(activeMulliganClients.CallerClient.Errors);

        var secondMulliganClients = new RecordingHubClients();
        await CreateHub(secondMulliganClients, new RecordingGroupManager(), secondConnectionId, registry)
            .SubmitIntent(roomId, secondPlayerId, "mulligan-second", MulliganJson([]));
        Assert.Empty(secondMulliganClients.CallerClient.Errors);
        var completeEvents = EventsFor(secondMulliganClients);
        Assert.Contains(completeEvents, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        Assert.Contains(completeEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        var mainPrompt = PromptFor(secondMulliganClients, activePlayerId);
        Assert.True(mainPrompt.Actionable);
        Assert.Contains(CommandTypes.Surrender, mainPrompt.Actions);
        var tapRuneCandidate = Assert.Single(
            mainPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal));
        var runeSourceId = (tapRuneCandidate.Sources ?? []).First().Id;

        var tapRuneClients = new RecordingHubClients();
        await CreateHub(tapRuneClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "tap-rune-active", JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.TapRune,
                sourceObjectId = runeSourceId
            }));
        Assert.Empty(tapRuneClients.CallerClient.Errors);

        var endTurnClients = new RecordingHubClients();
        await CreateHub(endTurnClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "end-turn-active", JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.EndTurn
            }));
        Assert.Empty(endTurnClients.CallerClient.Errors);
        var endTurnEvents = EventsFor(endTurnClients);
        Assert.Contains(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_END_DECLARED", StringComparison.Ordinal));
        Assert.Contains(endTurnEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_PLAYER_ADVANCED", StringComparison.Ordinal));
        var nextPlayerId = secondPlayerId;
        var nextConnectionId = secondConnectionId;
        var nextSnapshot = SnapshotFor(endTurnClients, nextPlayerId);
        Assert.Equal(nextPlayerId, nextSnapshot.ActivePlayerId);
        Assert.Equal(nextPlayerId, Assert.IsType<string>(nextSnapshot.Timing["turnPlayerId"]));
        Assert.Equal(MatchPhases.Main, Assert.IsType<string>(nextSnapshot.Timing["phase"]));
        Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(nextSnapshot.Timing["timingState"]));
        var nextPrompt = PromptFor(endTurnClients, nextPlayerId);
        Assert.True(nextPrompt.Actionable);
        Assert.Contains(CommandTypes.Surrender, nextPrompt.Actions);

        var surrender = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.Surrender
        });
        var readyJournalCount = journal.Entries.Count;
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), nextConnectionId, registry)
            .SubmitIntent(roomId, nextPlayerId, "surrender-same", surrender);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        var acceptedSurrenderEvent = Assert.Single(
            acceptedEvents,
            gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        Assert.Equal(activePlayerId, Assert.IsType<string>(acceptedSurrenderEvent.Payload["winnerPlayerId"]));
        Assert.Equal(nextPlayerId, Assert.IsType<string>(acceptedSurrenderEvent.Payload["surrenderedPlayerId"]));
        Assert.Equal(CommandTypes.Surrender, Assert.IsType<string>(acceptedSurrenderEvent.Payload["reason"]));
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedSnapshot = SnapshotFor(acceptedClients, activePlayerId);
        Assert.Equal(MatchStatuses.Finished, Assert.IsType<string>(acceptedSnapshot.Timing["roomStatus"]));
        Assert.Equal(activePlayerId, Assert.IsType<string>(acceptedSnapshot.Timing["winnerPlayerId"]));
        var acceptedPrompt = PromptFor(acceptedClients, nextPlayerId);
        Assert.False(acceptedPrompt.Actionable);
        Assert.DoesNotContain(CommandTypes.Surrender, acceptedPrompt.Actions);
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptActions = acceptedClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(readyJournalCount + 1, acceptedJournalCount);
        var surrenderEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "surrender-same", StringComparison.Ordinal));
        Assert.Equal(roomId, surrenderEntry.RoomId);
        Assert.Equal(nextPlayerId, surrenderEntry.PlayerId);
        Assert.Equal(CommandTypes.Surrender, surrenderEntry.CommandType);
        Assert.NotNull(surrenderEntry.RawCommand);
        Assert.Equal(CommandTypes.Surrender, surrenderEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.False(surrenderEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), nextConnectionId, registry)
            .SubmitIntent(roomId, $" {nextPlayerId} ", "surrender-same", surrender);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal(nextPlayerId, replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var replaySurrenderEvent = Assert.Single(
            replayEvents,
            gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        Assert.Equal(acceptedSurrenderEvent.Description, replaySurrenderEvent.Description);
        Assert.Equal(acceptedSurrenderEvent.Payload["winnerPlayerId"], replaySurrenderEvent.Payload["winnerPlayerId"]);
        Assert.Equal(acceptedSurrenderEvent.Payload["surrenderedPlayerId"], replaySurrenderEvent.Payload["surrenderedPlayerId"]);
        Assert.Equal(acceptedSurrenderEvent.Payload["reason"], replaySurrenderEvent.Payload["reason"]);
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        var replaySnapshot = SnapshotFor(replayClients, activePlayerId);
        Assert.Equal(acceptedSnapshot.Tick, replaySnapshot.Tick);
        Assert.Equal(MatchStatuses.Finished, Assert.IsType<string>(replaySnapshot.Timing["roomStatus"]));
        Assert.Equal(activePlayerId, Assert.IsType<string>(replaySnapshot.Timing["winnerPlayerId"]));
        var replayPromptActions = replayClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        Assert.Equal(acceptedPromptActions, replayPromptActions);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedSurrender = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.Surrender,
            clientNote = "changed"
        });

        await CreateHub(conflictClients, new RecordingGroupManager(), nextConnectionId, registry)
            .SubmitIntent(roomId, nextPlayerId, "surrender-same", changedSurrender);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), nextConnectionId, registry)
            .RequestSnapshot(roomId, nextPlayerId);

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Equal(MatchStatuses.Finished, Assert.IsType<string>(currentSnapshot.Timing["roomStatus"]));
        Assert.Equal(activePlayerId, Assert.IsType<string>(currentSnapshot.Timing["winnerPlayerId"]));
        var currentPrompt = Assert.IsType<ActionPromptDto>(Assert.Single(stateClients.CallerClient.Prompts).Payload);
        Assert.False(currentPrompt.Actionable);
        Assert.DoesNotContain(CommandTypes.Surrender, currentPrompt.Actions);
    }

    [Fact]
    public async Task MulliganDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "official-hub-mulligan-raw-idempotency";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "submit-deck-p1", SubmitDeckJson(p1Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "submit-deck-p2", SubmitDeckJson(p2Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .Ready(roomId, "P1", "ready-official-p1");
        var readyClients = new RecordingHubClients();
        await CreateHub(readyClients, new RecordingGroupManager(), "connection-2", registry)
            .Ready(roomId, "P2", "ready-official-p2");

        var startSnapshot = SnapshotFor(readyClients, "P1");
        Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(startSnapshot.Timing["phase"]));
        var activePlayerId = startSnapshot.ActivePlayerId;
        var secondPlayerId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "P2" : "P1";
        var activeConnectionId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";
        var activePrompt = PromptFor(readyClients, activePlayerId);
        Assert.True(activePrompt.Actionable);
        Assert.Contains("MULLIGAN", activePrompt.Actions);

        var activeSnapshot = SnapshotFor(readyClients, activePlayerId);
        var activeHand = StringList(ZoneView(PlayerView(activeSnapshot, activePlayerId))["hand"]);
        var selectedObjectIds = activeHand.Take(1).ToArray();
        Assert.Single(selectedObjectIds);
        var mulligan = MulliganJson(selectedObjectIds);
        var acceptedClients = new RecordingHubClients();
        var readyJournalCount = journal.Entries.Count;

        await CreateHub(acceptedClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "mulligan-same", mulligan);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal));
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(readyJournalCount + 1, acceptedJournalCount);
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        Assert.False(PromptFor(acceptedClients, activePlayerId).Actionable);
        Assert.True(PromptFor(acceptedClients, secondPlayerId).Actionable);
        var mulliganEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "mulligan-same", StringComparison.Ordinal));
        Assert.Equal(activePlayerId, mulliganEntry.PlayerId);
        Assert.Equal("MULLIGAN", mulliganEntry.CommandType);
        Assert.NotNull(mulliganEntry.RawCommand);
        Assert.Equal("MULLIGAN", mulliganEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(
            selectedObjectIds,
            mulliganEntry.RawCommand.Value.GetProperty("handObjectIds")
                .EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray());

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, $" {activePlayerId} ", "mulligan-same", mulligan);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal(activePlayerId, replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(
            acceptedClients.GroupClient.Snapshots.Select(message => message.PlayerId).OrderBy(playerId => playerId, StringComparer.Ordinal).ToArray(),
            replayClients.GroupClient.Snapshots.Select(message => message.PlayerId).OrderBy(playerId => playerId, StringComparer.Ordinal).ToArray());
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedClients.GroupClient.Prompts.Select(message => message.PlayerId).OrderBy(playerId => playerId, StringComparer.Ordinal).ToArray(),
            replayClients.GroupClient.Prompts.Select(message => message.PlayerId).OrderBy(playerId => playerId, StringComparer.Ordinal).ToArray());
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedMulligan = JsonSerializer.SerializeToElement(new
        {
            cmdType = "MULLIGAN",
            handObjectIds = Array.Empty<string>(),
            clientNote = "changed"
        });

        await CreateHub(conflictClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "mulligan-same", changedMulligan);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } rawCommand
            && rawCommand.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), activeConnectionId, registry)
            .RequestSnapshot(roomId, activePlayerId);
        var activeStateSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(activeStateSnapshot.Timing["phase"]));
        var activeStatePrompt = Assert.IsType<ActionPromptDto>(Assert.Single(stateClients.CallerClient.Prompts).Payload);
        Assert.False(activeStatePrompt.Actionable);
    }

    [Fact]
    public async Task TapRuneDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "official-hub-tap-rune-raw-idempotency";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "submit-deck-p1", SubmitDeckJson(p1Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "submit-deck-p2", SubmitDeckJson(p2Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .Ready(roomId, "P1", "ready-official-p1");
        var readyClients = new RecordingHubClients();
        await CreateHub(readyClients, new RecordingGroupManager(), "connection-2", registry)
            .Ready(roomId, "P2", "ready-official-p2");

        var startSnapshot = SnapshotFor(readyClients, "P1");
        Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(startSnapshot.Timing["phase"]));
        var activePlayerId = startSnapshot.ActivePlayerId;
        var secondPlayerId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "P2" : "P1";
        var activeConnectionId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";
        var secondConnectionId = string.Equals(secondPlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";

        var activeSnapshot = SnapshotFor(readyClients, activePlayerId);
        var activeHand = StringList(ZoneView(PlayerView(activeSnapshot, activePlayerId))["hand"]);
        var activeMulliganClients = new RecordingHubClients();
        await CreateHub(activeMulliganClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "mulligan-active", MulliganJson(activeHand.Take(1).ToArray()));
        Assert.Empty(activeMulliganClients.CallerClient.Errors);

        var secondMulliganClients = new RecordingHubClients();
        await CreateHub(secondMulliganClients, new RecordingGroupManager(), secondConnectionId, registry)
            .SubmitIntent(roomId, secondPlayerId, "mulligan-second", MulliganJson([]));
        Assert.Empty(secondMulliganClients.CallerClient.Errors);
        var completeEvents = EventsFor(secondMulliganClients);
        Assert.Contains(completeEvents, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        Assert.Contains(completeEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        var mainSnapshot = SnapshotFor(secondMulliganClients, activePlayerId);
        Assert.Equal(MatchPhases.Main, Assert.IsType<string>(mainSnapshot.Timing["phase"]));
        Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(mainSnapshot.Timing["timingState"]));
        var mainPrompt = PromptFor(secondMulliganClients, activePlayerId);
        Assert.True(mainPrompt.Actionable);
        var tapRuneCandidate = Assert.Single(
            mainPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "TAP_RUNE", StringComparison.Ordinal));
        Assert.True(tapRuneCandidate.Enabled);
        var runeSourceIds = (tapRuneCandidate.Sources ?? []).Select(source => source.Id).ToArray();
        Assert.True(runeSourceIds.Length > 1);
        var runeSourceId = runeSourceIds[0];
        var changedRuneSourceId = runeSourceIds[1];
        var tapRune = JsonSerializer.SerializeToElement(new
        {
            cmdType = "TAP_RUNE",
            sourceObjectId = runeSourceId
        });
        var mainJournalCount = journal.Entries.Count;

        var acceptedClients = new RecordingHubClients();
        await CreateHub(acceptedClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "tap-rune-same", tapRune);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNE_TAPPED", StringComparison.Ordinal));
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(mainJournalCount + 1, acceptedJournalCount);
        var tapRuneEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "tap-rune-same", StringComparison.Ordinal));
        Assert.Equal(activePlayerId, tapRuneEntry.PlayerId);
        Assert.Equal("TAP_RUNE", tapRuneEntry.CommandType);
        Assert.NotNull(tapRuneEntry.RawCommand);
        Assert.Equal("TAP_RUNE", tapRuneEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(runeSourceId, tapRuneEntry.RawCommand.Value.GetProperty("sourceObjectId").GetString());
        Assert.False(tapRuneEntry.RawCommand.Value.TryGetProperty("clientNote", out _));
        var acceptedSnapshot = SnapshotFor(acceptedClients, activePlayerId);
        var acceptedPlayer = PlayerView(acceptedSnapshot, activePlayerId);
        var acceptedRunePool = Assert.IsType<Dictionary<string, object?>>(acceptedPlayer["runePool"]);
        Assert.Equal(1, Assert.IsType<int>(acceptedRunePool["mana"]));
        var acceptedObjects = Assert.IsType<Dictionary<string, object?>>(acceptedPlayer["objects"]);
        var tappedRune = Assert.IsType<Dictionary<string, object?>>(acceptedObjects[runeSourceId]);
        Assert.True(Assert.IsType<bool>(tappedRune["isExhausted"]));
        var changedRune = Assert.IsType<Dictionary<string, object?>>(acceptedObjects[changedRuneSourceId]);
        Assert.False(Assert.IsType<bool>(changedRune["isExhausted"]));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, $" {activePlayerId} ", "tap-rune-same", tapRune);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal(activePlayerId, replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(
            acceptedClients.GroupClient.Snapshots.Select(message => message.PlayerId).OrderBy(playerId => playerId, StringComparer.Ordinal).ToArray(),
            replayClients.GroupClient.Snapshots.Select(message => message.PlayerId).OrderBy(playerId => playerId, StringComparer.Ordinal).ToArray());
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedClients.GroupClient.Prompts.Select(message => message.PlayerId).OrderBy(playerId => playerId, StringComparer.Ordinal).ToArray(),
            replayClients.GroupClient.Prompts.Select(message => message.PlayerId).OrderBy(playerId => playerId, StringComparer.Ordinal).ToArray());
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedTapRune = JsonSerializer.SerializeToElement(new
        {
            cmdType = "TAP_RUNE",
            sourceObjectId = changedRuneSourceId,
            clientNote = "changed"
        });

        await CreateHub(conflictClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "tap-rune-same", changedTapRune);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } rawCommand
            && rawCommand.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), activeConnectionId, registry)
            .RequestSnapshot(roomId, activePlayerId);
        var activeStateSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        var activeStatePlayer = PlayerView(activeStateSnapshot, activePlayerId);
        var activeStateRunePool = Assert.IsType<Dictionary<string, object?>>(activeStatePlayer["runePool"]);
        Assert.Equal(1, Assert.IsType<int>(activeStateRunePool["mana"]));
        var activeStateObjects = Assert.IsType<Dictionary<string, object?>>(activeStatePlayer["objects"]);
        var activeStateTappedRune = Assert.IsType<Dictionary<string, object?>>(activeStateObjects[runeSourceId]);
        Assert.True(Assert.IsType<bool>(activeStateTappedRune["isExhausted"]));
        var activeStateChangedRune = Assert.IsType<Dictionary<string, object?>>(activeStateObjects[changedRuneSourceId]);
        Assert.False(Assert.IsType<bool>(activeStateChangedRune["isExhausted"]));
        var activeStatePrompt = Assert.IsType<ActionPromptDto>(Assert.Single(stateClients.CallerClient.Prompts).Payload);
        var activeStateTapRuneCandidate = Assert.Single(
            activeStatePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "TAP_RUNE", StringComparison.Ordinal));
        Assert.DoesNotContain(activeStateTapRuneCandidate.Sources ?? [], source => string.Equals(source.Id, runeSourceId, StringComparison.Ordinal));
        Assert.Contains(activeStateTapRuneCandidate.Sources ?? [], source => string.Equals(source.Id, changedRuneSourceId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecycleRuneDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "official-hub-recycle-rune-raw-idempotency";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "submit-deck-p1", SubmitDeckJson(p1Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "submit-deck-p2", SubmitDeckJson(p2Deck));
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .Ready(roomId, "P1", "ready-official-p1");
        var readyClients = new RecordingHubClients();
        await CreateHub(readyClients, new RecordingGroupManager(), "connection-2", registry)
            .Ready(roomId, "P2", "ready-official-p2");

        var startSnapshot = SnapshotFor(readyClients, "P1");
        Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(startSnapshot.Timing["phase"]));
        var activePlayerId = startSnapshot.ActivePlayerId;
        var secondPlayerId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal) ? "P2" : "P1";
        var activeConnectionId = string.Equals(activePlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";
        var secondConnectionId = string.Equals(secondPlayerId, "P1", StringComparison.Ordinal)
            ? "connection-1"
            : "connection-2";

        var activeSnapshot = SnapshotFor(readyClients, activePlayerId);
        var activeHand = StringList(ZoneView(PlayerView(activeSnapshot, activePlayerId))["hand"]);
        var activeMulliganClients = new RecordingHubClients();
        await CreateHub(activeMulliganClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "mulligan-active", MulliganJson(activeHand.Take(1).ToArray()));
        Assert.Empty(activeMulliganClients.CallerClient.Errors);

        var secondMulliganClients = new RecordingHubClients();
        await CreateHub(secondMulliganClients, new RecordingGroupManager(), secondConnectionId, registry)
            .SubmitIntent(roomId, secondPlayerId, "mulligan-second", MulliganJson([]));
        Assert.Empty(secondMulliganClients.CallerClient.Errors);
        var completeEvents = EventsFor(secondMulliganClients);
        Assert.Contains(completeEvents, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        Assert.Contains(completeEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        var mainSnapshot = SnapshotFor(secondMulliganClients, activePlayerId);
        Assert.Equal(MatchPhases.Main, Assert.IsType<string>(mainSnapshot.Timing["phase"]));
        Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(mainSnapshot.Timing["timingState"]));
        var mainPrompt = PromptFor(secondMulliganClients, activePlayerId);
        Assert.True(mainPrompt.Actionable);
        var tapRuneCandidate = Assert.Single(
            mainPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "TAP_RUNE", StringComparison.Ordinal));
        Assert.True(tapRuneCandidate.Enabled);
        Assert.NotNull(tapRuneCandidate.Sources);
        var runeSourceId = tapRuneCandidate.Sources.First().Id;

        var tapRuneClients = new RecordingHubClients();
        await CreateHub(tapRuneClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "tap-rune-before-recycle", JsonSerializer.SerializeToElement(new
            {
                cmdType = "TAP_RUNE",
                sourceObjectId = runeSourceId
            }));

        Assert.Empty(tapRuneClients.CallerClient.Errors);
        var tapRuneEvents = EventsFor(tapRuneClients);
        Assert.Contains(tapRuneEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNE_TAPPED", StringComparison.Ordinal));
        Assert.Contains(tapRuneEvents, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        var postTapPrompt = PromptFor(tapRuneClients, activePlayerId);
        var recycleRuneCandidate = Assert.Single(
            postTapPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "RECYCLE_RUNE", StringComparison.Ordinal));
        Assert.True(recycleRuneCandidate.Enabled);
        Assert.Contains(recycleRuneCandidate.Sources ?? [], source => string.Equals(source.Id, runeSourceId, StringComparison.Ordinal));

        var recycleRune = JsonSerializer.SerializeToElement(new
        {
            cmdType = "RECYCLE_RUNE",
            sourceObjectId = runeSourceId
        });
        var postTapJournalCount = journal.Entries.Count;
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "recycle-rune-same", recycleRune);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        var acceptedRecycleEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        var acceptedPowerEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(runeSourceId, acceptedRecycleEvent.Payload["sourceObjectId"]);
        Assert.Equal(activePlayerId, acceptedPowerEvent.Payload["playerId"]);
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedSnapshot = SnapshotFor(acceptedClients, activePlayerId);
        Assert.Equal(activePlayerId, acceptedSnapshot.ActivePlayerId);
        var acceptedPlayer = PlayerView(acceptedSnapshot, activePlayerId);
        var acceptedRunePool = Assert.IsType<Dictionary<string, object?>>(acceptedPlayer["runePool"]);
        Assert.Equal(1, Assert.IsType<int>(acceptedRunePool["mana"]));
        Assert.Equal(1, Assert.IsType<int>(acceptedRunePool["power"]));
        var acceptedZones = ZoneView(acceptedPlayer);
        Assert.DoesNotContain(runeSourceId, StringList(acceptedZones["base"]));
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptActions = acceptedClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(postTapJournalCount + 1, acceptedJournalCount);
        var recycleEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "recycle-rune-same", StringComparison.Ordinal));
        Assert.Equal(roomId, recycleEntry.RoomId);
        Assert.Equal(activePlayerId, recycleEntry.PlayerId);
        Assert.Equal("RECYCLE_RUNE", recycleEntry.CommandType);
        Assert.NotNull(recycleEntry.RawCommand);
        var rawCommand = recycleEntry.RawCommand.Value;
        Assert.Equal("RECYCLE_RUNE", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(runeSourceId, rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, $" {activePlayerId} ", "recycle-rune-same", recycleRune);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal(activePlayerId, replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var replayRecycleEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        var replayPowerEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(acceptedRecycleEvent.Payload["sourceObjectId"], replayRecycleEvent.Payload["sourceObjectId"]);
        Assert.Equal(acceptedPowerEvent.Payload["playerId"], replayPowerEvent.Payload["playerId"]);
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        var replaySnapshot = SnapshotFor(replayClients, activePlayerId);
        Assert.Equal(acceptedSnapshot.Tick, replaySnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, replaySnapshot.ActivePlayerId);
        var replayPlayer = PlayerView(replaySnapshot, activePlayerId);
        var replayRunePool = Assert.IsType<Dictionary<string, object?>>(replayPlayer["runePool"]);
        Assert.Equal(acceptedRunePool["mana"], replayRunePool["mana"]);
        Assert.Equal(acceptedRunePool["power"], replayRunePool["power"]);
        Assert.DoesNotContain(runeSourceId, StringList(ZoneView(replayPlayer)["base"]));
        var replayPromptActions = replayClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        Assert.Equal(acceptedPromptActions, replayPromptActions);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedRecycleRune = JsonSerializer.SerializeToElement(new
        {
            cmdType = "RECYCLE_RUNE",
            sourceObjectId = runeSourceId,
            clientNote = "changed"
        });

        await CreateHub(conflictClients, new RecordingGroupManager(), activeConnectionId, registry)
            .SubmitIntent(roomId, activePlayerId, "recycle-rune-same", changedRecycleRune);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), activeConnectionId, registry)
            .RequestSnapshot(roomId, activePlayerId);

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, currentSnapshot.ActivePlayerId);
        var currentPlayer = PlayerView(currentSnapshot, activePlayerId);
        var currentRunePool = Assert.IsType<Dictionary<string, object?>>(currentPlayer["runePool"]);
        Assert.Equal(acceptedRunePool["mana"], currentRunePool["mana"]);
        Assert.Equal(acceptedRunePool["power"], currentRunePool["power"]);
        Assert.DoesNotContain(runeSourceId, StringList(ZoneView(currentPlayer)["base"]));
        var currentPrompt = Assert.IsType<ActionPromptDto>(Assert.Single(stateClients.CallerClient.Prompts).Payload);
        Assert.Equal(activePlayerId, currentPrompt.PlayerId);
        Assert.Equal(acceptedSnapshot.Tick, currentPrompt.SnapshotTick);
    }

    [Fact]
    public async Task OrderTriggersDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "gamehub-order-triggers-raw-idempotency";
        const string clientIntentId = "order-triggers-same";
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(BuildGameHubOrderTriggersState(roomId), new CoreRuleEngine(), journal);
        var registry = new FixedMatchSessionRegistry(session);

        var promptClients = new RecordingHubClients();
        await CreateHub(promptClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");

        Assert.Empty(promptClients.CallerClient.Errors);
        var prompt = Assert.IsType<ActionPromptDto>(Assert.Single(promptClients.CallerClient.Prompts).Payload);
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.OrderTriggers, prompt.View?.Type);
        Assert.Contains(CommandTypes.OrderTriggers, prompt.Actions);
        Assert.NotNull(prompt.PromptId);
        Assert.True(prompt.SnapshotTick.HasValue);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.OrderTriggers, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var orderedTriggerIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["orderedTriggerIds"]);
        Assert.Equal(["TRIGGER-BATTLE-DEFENDER", "TRIGGER-BATTLE-ATTACKER"], orderedTriggerIds);

        var orderTriggers = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.OrderTriggers,
            orderedTriggerIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
        var seededJournalCount = journal.Entries.Count;
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, orderTriggers);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        Assert.Equal(
            ["TRIGGERS_ORDERED", "TRIGGERS_MOVED_TO_STACK"],
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedOrderedEvent = Assert.Single(
            acceptedEvents,
            gameEvent => string.Equals(gameEvent.Kind, "TRIGGERS_ORDERED", StringComparison.Ordinal));
        Assert.Equal(orderedTriggerIds.ToArray(), Assert.IsType<string[]>(acceptedOrderedEvent.Payload["orderedTriggerIds"]));
        var acceptedMovedEvent = Assert.Single(
            acceptedEvents,
            gameEvent => string.Equals(gameEvent.Kind, "TRIGGERS_MOVED_TO_STACK", StringComparison.Ordinal));
        Assert.Equal("ordered-TRIGGER-BATTLE-DEFENDER", Assert.IsType<string>(acceptedMovedEvent.Payload["topStackItemId"]));
        Assert.Equal("P2", Assert.IsType<string>(acceptedMovedEvent.Payload["nextPriorityPlayerId"]));
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedP1Snapshot = SnapshotFor(acceptedClients, "P1");
        var acceptedP2Snapshot = SnapshotFor(acceptedClients, "P2");
        var acceptedP1SnapshotHash = MatchStateHasher.HashValue(acceptedP1Snapshot);
        var acceptedP2SnapshotHash = MatchStateHasher.HashValue(acceptedP2Snapshot);
        Assert.Equal(acceptedP1Snapshot.Tick, acceptedP2Snapshot.Tick);
        var acceptedP1Prompt = PromptFor(acceptedClients, "P1");
        var acceptedP2Prompt = PromptFor(acceptedClients, "P2");
        Assert.DoesNotContain(CommandTypes.OrderTriggers, acceptedP1Prompt.Actions);
        Assert.DoesNotContain(CommandTypes.OrderTriggers, acceptedP2Prompt.Actions);
        Assert.Equal(PromptTypes.StackPriority, acceptedP2Prompt.View?.Type);
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptActions = acceptedClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(acceptedPrompt => acceptedPrompt.PlayerId, StringComparer.Ordinal)
            .Select(acceptedPrompt => string.Join("|", acceptedPrompt.Actions))
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(seededJournalCount + 1, acceptedJournalCount);
        var orderTriggerEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, clientIntentId, StringComparison.Ordinal));
        Assert.Equal(roomId, orderTriggerEntry.RoomId);
        Assert.Equal("P1", orderTriggerEntry.PlayerId);
        Assert.Equal(CommandTypes.OrderTriggers, orderTriggerEntry.CommandType);
        Assert.NotNull(orderTriggerEntry.RawCommand);
        Assert.Equal(CommandTypes.OrderTriggers, orderTriggerEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(
            orderedTriggerIds,
            orderTriggerEntry.RawCommand.Value.GetProperty("orderedTriggerIds")
                .EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray());
        Assert.False(orderTriggerEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", clientIntentId, orderTriggers);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("P1", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var replayOrderedEvent = Assert.Single(
            replayEvents,
            gameEvent => string.Equals(gameEvent.Kind, "TRIGGERS_ORDERED", StringComparison.Ordinal));
        Assert.Equal(
            Assert.IsType<string[]>(acceptedOrderedEvent.Payload["orderedTriggerIds"]),
            Assert.IsType<string[]>(replayOrderedEvent.Payload["orderedTriggerIds"]));
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(acceptedP1SnapshotHash, MatchStateHasher.HashValue(SnapshotFor(replayClients, "P1")));
        Assert.Equal(acceptedP2SnapshotHash, MatchStateHasher.HashValue(SnapshotFor(replayClients, "P2")));
        var replayPromptActions = replayClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(replayPrompt => replayPrompt.PlayerId, StringComparer.Ordinal)
            .Select(replayPrompt => string.Join("|", replayPrompt.Actions))
            .ToArray();
        Assert.Equal(acceptedPromptActions, replayPromptActions);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedOrderTriggers = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.OrderTriggers,
            orderedTriggerIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed"
        });

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, changedOrderTriggers);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } rawCommand
            && rawCommand.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));
        Assert.Equal(acceptedP1SnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(acceptedP2SnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
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
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        Assert.DoesNotContain("FLIP_TABLE", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported command", payload.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitIntentRejectedMessagesCarryProtocolVersionsOnError()
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
            .SubmitIntent("room-a", " alice ", "intent-unsupported-protocol-envelope", cmd);

        var errorMessage = Assert.Single(clients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, errorMessage.Type);
        Assert.Equal("room-a", errorMessage.RoomId);
        Assert.Equal("alice", errorMessage.PlayerId);
        AssertProtocolDefaults(errorMessage);
        var payload = Assert.IsType<ErrorDto>(errorMessage.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
    }

    [Fact]
    public async Task SubmitIntentUnsupportedCommandPreservesRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-unsupported-payload";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "cmdType": "FLIP_TABLE",
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-unsupported-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(error), StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-unsupported-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("FLIP_TABLE", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        Assert.Equal("FLIP_TABLE", entry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(sentinel, entry.RawCommand.Value.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, entry.RawCommand.Value.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentMalformedCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-malformed-cmdtype";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "cmdType": ["FLIP_TABLE"],
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-malformed-cmdtype-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("FLIP_TABLE", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-malformed-cmdtype-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("UNKNOWN", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Array, rawCommand.GetProperty("cmdType").ValueKind);
        Assert.Equal("FLIP_TABLE", Assert.Single(rawCommand.GetProperty("cmdType").EnumerateArray()).GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentBooleanCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-boolean-cmdtype";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "cmdType": true,
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-boolean-cmdtype-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("cmdType", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-boolean-cmdtype-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("UNKNOWN", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Object, rawCommand.ValueKind);
        Assert.Equal(JsonValueKind.True, rawCommand.GetProperty("cmdType").ValueKind);
        Assert.True(rawCommand.GetProperty("cmdType").GetBoolean());
        Assert.Equal(sentinel, rawCommand.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentNumericCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-numeric-cmdtype";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "cmdType": 42,
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-numeric-cmdtype-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("cmdType", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-numeric-cmdtype-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("UNKNOWN", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Object, rawCommand.ValueKind);
        Assert.Equal(JsonValueKind.Number, rawCommand.GetProperty("cmdType").ValueKind);
        Assert.Equal(42, rawCommand.GetProperty("cmdType").GetInt32());
        Assert.Equal(sentinel, rawCommand.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentObjectCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-object-cmdtype";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "cmdType": {
                    "name": "FLIP_TABLE",
                    "nested": {
                        "audit": "{{sentinel}}"
                    }
                },
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-object-cmdtype-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("cmdType", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain("FLIP_TABLE", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-object-cmdtype-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("UNKNOWN", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Object, rawCommand.ValueKind);
        var rawCommandType = rawCommand.GetProperty("cmdType");
        Assert.Equal(JsonValueKind.Object, rawCommandType.ValueKind);
        Assert.Equal("FLIP_TABLE", rawCommandType.GetProperty("name").GetString());
        Assert.Equal(sentinel, rawCommandType.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentDuplicateCommandTypeUsesLastMalformedValueAndPreservesRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-duplicate-cmdtype";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "cmdType": "PASS_PRIORITY",
                "cmdType": ["FLIP_TABLE"],
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-duplicate-cmdtype-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("cmdType", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PASS_PRIORITY", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain("FLIP_TABLE", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-duplicate-cmdtype-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("UNKNOWN", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Object, rawCommand.ValueKind);
        Assert.Equal(JsonValueKind.Array, rawCommand.GetProperty("cmdType").ValueKind);
        var commandTypeProperties = rawCommand
            .EnumerateObject()
            .Where(property => string.Equals(property.Name, "cmdType", StringComparison.Ordinal))
            .Select(property => property.Value.Clone())
            .ToArray();
        Assert.Equal(2, commandTypeProperties.Length);
        Assert.Equal("PASS_PRIORITY", commandTypeProperties[0].GetString());
        Assert.Equal(JsonValueKind.Array, commandTypeProperties[1].ValueKind);
        Assert.Equal("FLIP_TABLE", Assert.Single(commandTypeProperties[1].EnumerateArray()).GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentWhitespaceWrappedKnownCommandTypeDoesNotExecuteAndPreservesRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-wrapped-known-cmdtype";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "cmdType": " PASS_PRIORITY ",
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-wrapped-known-cmdtype-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("cmdType", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PASS_PRIORITY", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-wrapped-known-cmdtype-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal(" PASS_PRIORITY ", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Object, rawCommand.ValueKind);
        Assert.Equal(" PASS_PRIORITY ", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentLowercaseKnownCommandTypeDoesNotExecuteAndPreservesRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-lowercase-known-cmdtype";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "cmdType": "pass_priority",
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-lowercase-known-cmdtype-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("cmdType", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain("pass_priority", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PASS_PRIORITY", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-lowercase-known-cmdtype-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("pass_priority", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Object, rawCommand.ValueKind);
        Assert.Equal("pass_priority", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentNullCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-null-cmdtype";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "cmdType": null,
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-null-cmdtype-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("cmdType", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-null-cmdtype-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("UNKNOWN", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Object, rawCommand.ValueKind);
        Assert.Equal(JsonValueKind.Null, rawCommand.GetProperty("cmdType").ValueKind);
        Assert.Equal(sentinel, rawCommand.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentWhitespaceCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-whitespace-cmdtype";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "cmdType": "   ",
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-whitespace-cmdtype-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("cmdType", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-whitespace-cmdtype-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("UNKNOWN", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Object, rawCommand.ValueKind);
        Assert.Equal("   ", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentMissingCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-missing-cmdtype";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            {
                "clientNote": "{{sentinel}}",
                "nested": {
                    "audit": "{{sentinel}}"
                }
            }
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-missing-cmdtype-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("clientNote", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-missing-cmdtype-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("UNKNOWN", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Object, rawCommand.ValueKind);
        Assert.False(rawCommand.TryGetProperty("cmdType", out _));
        Assert.Equal(sentinel, rawCommand.GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawCommand.GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentNonObjectCommandPreservesUnknownRawPayloadInJournalWithoutBroadcast()
    {
        const string sentinel = "SECRET-RAW-nonobject-command";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var journalCountBeforeUnsupported = journal.Entries.Count;
        var clients = new RecordingHubClients();
        var cmd = JsonDocument.Parse($$"""
            [
                "FLIP_TABLE",
                {
                    "clientNote": "{{sentinel}}",
                    "nested": {
                        "audit": "{{sentinel}}"
                    }
                }
            ]
            """).RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-nonobject-command-raw", cmd);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("当前命令不受服务端支持。", payload.Message);
        var errorJson = JsonSerializer.Serialize(error);
        Assert.DoesNotContain("FLIP_TABLE", errorJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, errorJson, StringComparison.Ordinal);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);

        Assert.Equal(journalCountBeforeUnsupported + 1, journal.Entries.Count);
        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "intent-nonobject-command-raw", StringComparison.Ordinal));
        Assert.False(entry.Accepted);
        Assert.Equal("UNKNOWN", entry.CommandType);
        Assert.Equal("当前命令不受服务端支持。", entry.ErrorMessage);
        Assert.NotNull(entry.RawCommand);
        var rawCommand = entry.RawCommand.Value;
        Assert.Equal(JsonValueKind.Array, rawCommand.ValueKind);
        var rawItems = rawCommand.EnumerateArray().ToArray();
        Assert.Equal(2, rawItems.Length);
        Assert.Equal("FLIP_TABLE", rawItems[0].GetString());
        Assert.Equal(sentinel, rawItems[1].GetProperty("clientNote").GetString());
        Assert.Equal(sentinel, rawItems[1].GetProperty("nested").GetProperty("audit").GetString());
        Assert.Empty(entry.Events);
    }

    [Fact]
    public async Task SubmitIntentKnownP0ContractCommandsUseCoreValidationShell()
    {
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);

        var cases = new[]
        {
            ("intent-known-pay-cost", """{"cmdType":"PAY_COST"}"""),
            ("intent-known-assign-damage", """{"cmdType":"ASSIGN_COMBAT_DAMAGE"}"""),
            ("intent-known-order-triggers", """{"cmdType":"ORDER_TRIGGERS"}""")
        };

        foreach (var (intentId, commandJson) in cases)
        {
            var clients = new RecordingHubClients();
            var cmd = JsonDocument.Parse(commandJson).RootElement.Clone();

            await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
                .SubmitIntent("room-a", "alice", intentId, cmd);

            var error = Assert.Single(clients.CallerClient.Errors);
            var payload = Assert.IsType<ErrorDto>(error.Payload);
            Assert.Equal(ErrorCodes.InvalidPayload, payload.Code);
            Assert.Empty(clients.GroupClient.EventMessages);
            Assert.Empty(clients.GroupClient.Snapshots);
        }
    }

    [Fact]
    public async Task SubmitIntentKnownP0ContractCommandsRedactValidationErrorDetails()
    {
        const string sentinel = "secret-raw-validation-leak";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);

        var cases = new[]
        {
            ("intent-redact-pay-cost", """{"cmdType":"PAY_COST","clientNote":"secret-raw-validation-leak"}"""),
            ("intent-redact-assign-damage", """{"cmdType":"ASSIGN_COMBAT_DAMAGE","clientNote":"secret-raw-validation-leak"}"""),
            ("intent-redact-order-triggers", """{"cmdType":"ORDER_TRIGGERS","clientNote":"secret-raw-validation-leak"}""")
        };

        foreach (var (intentId, commandJson) in cases)
        {
            var clients = new RecordingHubClients();
            var cmd = JsonDocument.Parse(commandJson).RootElement.Clone();

            await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
                .SubmitIntent("room-a", "alice", intentId, cmd);

            var error = Assert.Single(clients.CallerClient.Errors);
            var payload = Assert.IsType<ErrorDto>(error.Payload);
            Assert.Equal(ErrorCodes.InvalidPayload, payload.Code);
            Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(intentId, payload.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
            Assert.Empty(clients.GroupClient.EventMessages);
            Assert.Empty(clients.GroupClient.Snapshots);
            Assert.Empty(clients.GroupClient.Prompts);
            Assert.Empty(clients.CallerClient.Snapshots);
            Assert.Empty(clients.CallerClient.Prompts);
        }
    }

    [Fact]
    public async Task SubmitIntentPayCostWindowUsesPromptStampAndClosesRuntimeSlice()
    {
        const string roomId = "pay-cost-window-core";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "pay-cost-window", "seed-pay-cost-window");

        Assert.Empty(seedClients.CallerClient.Errors);
        var prompt = PromptFor(seedClients, "P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(["PAY_COST", "SURRENDER"], prompt.Actions);
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.False(string.IsNullOrWhiteSpace(prompt.PromptId));
        Assert.True(prompt.SnapshotTick.HasValue);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(candidate.Metadata);
        Assert.Equal("PAY-3A-MANA-1", Assert.IsType<string>(metadata["paymentId"]));
        Assert.Equal("TEST_PAYMENT", Assert.IsType<string>(metadata["paymentWindow"]));
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentChoices"]);
        Assert.Contains(choices, choice => string.Equals(choice.Id, "SPEND_MANA:1", StringComparison.Ordinal));
        var seedJournalCount = journal.Entries.Count;
        var seededEntry = Assert.Single(journal.Entries);
        var seededStateHash = MatchStateHasher.Hash(seededEntry.AuthoritativeState);
        var seededPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(seededEntry.AuthoritativeState));
        var seededSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(seededEntry.AuthoritativeState));

        var stalePromptClients = new RecordingHubClients();
        var stalePromptId = $"{prompt.PromptId}:stale";
        var stalePromptPayCost = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = "PAY-3A-MANA-1",
            paymentWindow = "TEST_PAYMENT",
            paymentChoiceIds = new[] { "SPEND_MANA:1" },
            promptId = stalePromptId,
            snapshotTick = prompt.SnapshotTick
        });

        await CreateHub(stalePromptClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-pay-cost-stale-prompt", stalePromptPayCost);

        var stalePromptError = Assert.Single(stalePromptClients.CallerClient.Errors);
        var stalePromptPayload = Assert.IsType<ErrorDto>(stalePromptError.Payload);
        Assert.Equal(ErrorCodes.PromptExpired, stalePromptPayload.Code);
        Assert.Empty(stalePromptClients.GroupClient.EventMessages);
        Assert.Empty(stalePromptClients.GroupClient.Snapshots);
        Assert.Empty(stalePromptClients.GroupClient.Prompts);
        Assert.Empty(stalePromptClients.CallerClient.Snapshots);
        Assert.Empty(stalePromptClients.CallerClient.Prompts);
        Assert.Equal(seedJournalCount + 1, journal.Entries.Count);
        var rejectedEntry = journal.Entries[^1];
        Assert.Equal(roomId, rejectedEntry.RoomId);
        Assert.Equal("P1", rejectedEntry.PlayerId);
        Assert.Equal("intent-pay-cost-stale-prompt", rejectedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PayCost, rejectedEntry.CommandType);
        Assert.False(rejectedEntry.Accepted);
        Assert.Equal(stalePromptPayload.Message, rejectedEntry.ErrorMessage);
        Assert.Empty(rejectedEntry.Events);
        Assert.Equal(seededStateHash, MatchStateHasher.Hash(rejectedEntry.AuthoritativeState));
        Assert.Equal(seededPromptsHash, MatchStateHasher.HashValue(rejectedEntry.Prompts));
        Assert.Equal(seededSnapshotsHash, MatchStateHasher.HashValue(rejectedEntry.Snapshots));
        Assert.True(rejectedEntry.RawCommand.HasValue);
        var rejectedRawCommand = rejectedEntry.RawCommand.Value;
        Assert.Equal(CommandTypes.PayCost, rejectedRawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("PAY-3A-MANA-1", rejectedRawCommand.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PAYMENT", rejectedRawCommand.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            ["SPEND_MANA:1"],
            rejectedRawCommand.GetProperty("paymentChoiceIds")
                .EnumerateArray()
                .Select(choice => choice.GetString() ?? string.Empty)
                .ToArray());
        Assert.Equal(stalePromptId, rejectedRawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick.Value, rejectedRawCommand.GetProperty("snapshotTick").GetInt64());

        var stalePromptJournalCount = journal.Entries.Count;
        var stalePromptReplayClients = new RecordingHubClients();
        await CreateHub(stalePromptReplayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-pay-cost-stale-prompt", stalePromptPayCost);

        var stalePromptReplayError = Assert.Single(stalePromptReplayClients.CallerClient.Errors);
        var stalePromptReplayPayload = Assert.IsType<ErrorDto>(stalePromptReplayError.Payload);
        Assert.Equal(ErrorCodes.PromptExpired, stalePromptReplayPayload.Code);
        Assert.Equal(stalePromptPayload.Message, stalePromptReplayPayload.Message);
        Assert.Empty(stalePromptReplayClients.GroupClient.EventMessages);
        Assert.Empty(stalePromptReplayClients.GroupClient.Snapshots);
        Assert.Empty(stalePromptReplayClients.GroupClient.Prompts);
        Assert.Empty(stalePromptReplayClients.CallerClient.Snapshots);
        Assert.Empty(stalePromptReplayClients.CallerClient.Prompts);
        Assert.Equal(stalePromptJournalCount, journal.Entries.Count);

        var stalePromptConflictClients = new RecordingHubClients();
        var changedStalePromptPayCost = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = "PAY-3A-MANA-1",
            paymentWindow = "TEST_PAYMENT",
            paymentChoiceIds = new[] { "SPEND_MANA:2" },
            promptId = stalePromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-stale-prompt"
        });

        await CreateHub(stalePromptConflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-pay-cost-stale-prompt", changedStalePromptPayCost);

        var stalePromptConflictError = Assert.Single(stalePromptConflictClients.CallerClient.Errors);
        var stalePromptConflictPayload = Assert.IsType<ErrorDto>(stalePromptConflictError.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, stalePromptConflictPayload.Code);
        Assert.Empty(stalePromptConflictClients.GroupClient.EventMessages);
        Assert.Empty(stalePromptConflictClients.GroupClient.Snapshots);
        Assert.Empty(stalePromptConflictClients.GroupClient.Prompts);
        Assert.Empty(stalePromptConflictClients.CallerClient.Snapshots);
        Assert.Empty(stalePromptConflictClients.CallerClient.Prompts);
        Assert.Equal(stalePromptJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-stale-prompt", StringComparison.Ordinal));

        var staleSnapshotClients = new RecordingHubClients();
        await CreateHub(staleSnapshotClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-pay-cost-stale-snapshot", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PAY_COST",
                paymentId = "PAY-3A-MANA-1",
                paymentWindow = "TEST_PAYMENT",
                paymentChoiceIds = new[] { "SPEND_MANA:1" },
                promptId = prompt.PromptId,
                snapshotTick = prompt.SnapshotTick.GetValueOrDefault() + 1
            }));

        var staleSnapshotError = Assert.Single(staleSnapshotClients.CallerClient.Errors);
        Assert.Equal(ErrorCodes.PromptExpired, Assert.IsType<ErrorDto>(staleSnapshotError.Payload).Code);
        Assert.Empty(staleSnapshotClients.GroupClient.EventMessages);
        Assert.Empty(staleSnapshotClients.GroupClient.Snapshots);

        var invalidChoiceClients = new RecordingHubClients();
        await CreateHub(invalidChoiceClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-pay-cost-invalid-choice", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PAY_COST",
                paymentId = "PAY-3A-MANA-1",
                paymentWindow = "TEST_PAYMENT",
                paymentChoiceIds = new[] { "SPEND_MANA:2" },
                promptId = prompt.PromptId,
                snapshotTick = prompt.SnapshotTick
            }));

        var invalidChoiceError = Assert.Single(invalidChoiceClients.CallerClient.Errors);
        Assert.Equal(ErrorCodes.InvalidTarget, Assert.IsType<ErrorDto>(invalidChoiceError.Payload).Code);
        Assert.Empty(invalidChoiceClients.GroupClient.EventMessages);
        Assert.Empty(invalidChoiceClients.GroupClient.Snapshots);

        var payClients = new RecordingHubClients();
        await CreateHub(payClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-pay-cost-valid", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PAY_COST",
                paymentId = "PAY-3A-MANA-1",
                paymentWindow = "TEST_PAYMENT",
                paymentChoiceIds = new[] { "SPEND_MANA:1" },
                promptId = prompt.PromptId,
                snapshotTick = prompt.SnapshotTick
            }));

        Assert.Empty(payClients.CallerClient.Errors);
        var events = EventsFor(payClients);
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        var snapshot = SnapshotFor(payClients, "P1");
        Assert.Null(snapshot.Timing["pendingPayment"]);
        var p1 = PlayerView(snapshot, "P1");
        var runePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(runePool["mana"]));
    }

    [Fact]
    public async Task PayCostReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        const string roomId = "pay-cost-replay-protocol-envelope";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        var development = new TestHostEnvironment(Environments.Development);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry, development)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry, development)
            .JoinRoom(roomId, "P2");

        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                development)
            .SeedScenario(roomId, "P1", "pay-cost-window", "seed-pay-cost-replay-protocol-envelope");

        Assert.Empty(seedClients.CallerClient.Errors);
        var prompt = PromptFor(seedClients, "P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.NotNull(prompt.PromptId);
        Assert.True(prompt.SnapshotTick.HasValue);
        var payCost = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = "PAY-3A-MANA-1",
            paymentWindow = "TEST_PAYMENT",
            paymentChoiceIds = new[] { "SPEND_MANA:1" },
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "pay-cost-replay-protocol-envelope", payCost);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        var acceptedEventKinds = EventsFor(acceptedClients)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();
        Assert.Contains(acceptedEventKinds, kind => string.Equals(kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(acceptedEventKinds, kind => string.Equals(kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "pay-cost-replay-protocol-envelope", payCost);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal("P1", replayEventsMessage.PlayerId);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);
        Assert.Equal(
            acceptedEventKinds,
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());

        Assert.NotEmpty(replayClients.GroupClient.Snapshots);
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());

        Assert.NotEmpty(replayClients.GroupClient.Prompts);
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task PayCostDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "pay-cost-raw-idempotency";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var development = new TestHostEnvironment(Environments.Development);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry, development)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry, development)
            .JoinRoom(roomId, "P2");

        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                development)
            .SeedScenario(roomId, "P1", "pay-cost-window", "seed-pay-cost-window");

        Assert.Empty(seedClients.CallerClient.Errors);
        var prompt = PromptFor(seedClients, "P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.NotNull(prompt.PromptId);
        Assert.True(prompt.SnapshotTick.HasValue);
        var seededJournalCount = journal.Entries.Count;
        var payCost = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = "PAY-3A-MANA-1",
            paymentWindow = "TEST_PAYMENT",
            paymentChoiceIds = new[] { "SPEND_MANA:1" },
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "pay-cost-same", payCost);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(seededJournalCount + 1, acceptedJournalCount);
        var payEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "pay-cost-same", StringComparison.Ordinal));
        Assert.Equal(roomId, payEntry.RoomId);
        Assert.Equal("P1", payEntry.PlayerId);
        Assert.Equal(CommandTypes.PayCost, payEntry.CommandType);
        Assert.NotNull(payEntry.RawCommand);
        var rawCommand = payEntry.RawCommand.Value;
        Assert.Equal(CommandTypes.PayCost, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("PAY-3A-MANA-1", rawCommand.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PAYMENT", rawCommand.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            ["SPEND_MANA:1"],
            rawCommand.GetProperty("paymentChoiceIds")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "pay-cost-same", payCost);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("P1", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedPayCost = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = "PAY-3A-MANA-1",
            paymentWindow = "TEST_PAYMENT",
            paymentChoiceIds = new[] { "SPEND_MANA:1" },
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed"
        });

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "pay-cost-same", changedPayCost);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var snapshotClients = new RecordingHubClients();
        await CreateHub(snapshotClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");

        Assert.Empty(snapshotClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(snapshotClients.CallerClient.Snapshots).Payload);
        Assert.Null(currentSnapshot.Timing["pendingPayment"]);
        var currentP1 = PlayerView(currentSnapshot, "P1");
        var currentRunePool = Assert.IsType<Dictionary<string, object?>>(currentP1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(currentRunePool["mana"]));
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
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitIntentDuplicateConflictMessagesCarryProtocolVersionsOnError()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var pass = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-conflict-protocol-envelope", pass);
        var clients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", " alice ", "intent-conflict-protocol-envelope", endTurn);

        var errorMessage = Assert.Single(clients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, errorMessage.Type);
        Assert.Equal("room-a", errorMessage.RoomId);
        Assert.Equal("alice", errorMessage.PlayerId);
        AssertProtocolDefaults(errorMessage);
        var payload = Assert.IsType<ErrorDto>(errorMessage.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
    }

    [Fact]
    public async Task PassWrapperDuplicateClientIntentRawPayloadReplaysButSubmitIntentChangedRawConflictsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var readyJournalCount = journal.Entries.Count;

        var acceptedClients = new RecordingHubClients();
        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .Pass("room-a", "alice", "pass-same");

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_ENDED", StringComparison.Ordinal));
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(readyJournalCount + 1, acceptedJournalCount);
        var passEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "pass-same", StringComparison.Ordinal));
        Assert.Equal("alice", passEntry.PlayerId);
        Assert.Equal("PASS", passEntry.CommandType);
        Assert.NotNull(passEntry.RawCommand);
        Assert.Equal("PASS", passEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.False(passEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .Pass("room-a", "alice", "pass-same");

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(2, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(2, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedPass = JsonDocument.Parse("""{"cmdType":"PASS","clientNote":"changed"}""").RootElement.Clone();

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "pass-same", changedPass);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } rawCommand
            && rawCommand.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EndTurnDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var readyJournalCount = journal.Entries.Count;

        var acceptedClients = new RecordingHubClients();
        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .EndTurn("room-a", "alice", "end-turn-same");

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_ENDED", StringComparison.Ordinal));
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "TURN_BEGAN", StringComparison.Ordinal));
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedSnapshot = SnapshotFor(acceptedClients, "bob");
        Assert.Equal("bob", acceptedSnapshot.ActivePlayerId);
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(readyJournalCount + 1, acceptedJournalCount);
        var endTurnEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "end-turn-same", StringComparison.Ordinal));
        Assert.Equal("alice", endTurnEntry.PlayerId);
        Assert.Equal("END_TURN", endTurnEntry.CommandType);
        Assert.NotNull(endTurnEntry.RawCommand);
        Assert.Equal("END_TURN", endTurnEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.False(endTurnEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .EndTurn("room-a", " alice ", "end-turn-same");

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("alice", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(2, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(2, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedEndTurn = JsonDocument.Parse("""{"cmdType":"END_TURN","clientNote":"changed"}""").RootElement.Clone();

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "end-turn-same", changedEndTurn);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } rawCommand
            && rawCommand.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), "connection-2", registry)
            .RequestSnapshot("room-a", "bob");

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Equal(acceptedSnapshot.TurnNumber, currentSnapshot.TurnNumber);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, currentSnapshot.ActivePlayerId);
    }

    [Fact]
    public async Task GameHubDuplicateClientIntentRawPayloadConflictReturnsStableErrorWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), journal);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom("room-a", "alice");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom("room-a", "bob");
        await ReadyBothAsync(registry);
        var readyJournalCount = journal.Entries.Count;
        var pass = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY","clientNote":"original"}""").RootElement.Clone();
        var acceptedClients = new RecordingHubClients();
        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-same", pass);
        var acceptedJournalCount = journal.Entries.Count;
        var acceptedEvents = EventsFor(acceptedClients);

        Assert.Equal(readyJournalCount + 1, acceptedJournalCount);
        Assert.Empty(acceptedClients.CallerClient.Errors);
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "PASS_PRIORITY", StringComparison.Ordinal));
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-same", pass);

        Assert.Empty(replayClients.CallerClient.Errors);
        Assert.Equal(MessageType.EVENTS, Assert.Single(replayClients.GroupClient.EventMessages).Type);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(2, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var clients = new RecordingHubClients();
        var changedPass = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY","clientNote":"changed"}""").RootElement.Clone();

        await CreateHub(clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent("room-a", "alice", "intent-same", changedPass);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } rawCommand
            && rawCommand.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));
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
    public async Task SeedScenarioDuplicateClientIntentRawPayloadReplaysButChangedScenarioConflictsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var development = new TestHostEnvironment(Environments.Development);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry, development)
            .JoinRoom("room-a", "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry, development)
            .JoinRoom("room-a", "P2");
        var joinedJournalCount = journal.Entries.Count;
        var acceptedClients = new RecordingHubClients();

        await CreateHub(
                acceptedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                development)
            .SeedScenario("room-a", "P1", "basic-play", "seed-same");

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessageCount = acceptedClients.GroupClient.EventMessages.Count;
        var acceptedSnapshotCount = acceptedClients.GroupClient.Snapshots.Count;
        var acceptedPromptCount = acceptedClients.GroupClient.Prompts.Count;
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(joinedJournalCount + 1, acceptedJournalCount);
        Assert.Equal(1, acceptedEventsMessageCount);
        Assert.Equal(2, acceptedSnapshotCount);
        Assert.Equal(2, acceptedPromptCount);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedEventsMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        var seedEvent = Assert.Single(acceptedEvents);
        Assert.Equal("DEV_SCENARIO_SEEDED", seedEvent.Kind);
        Assert.Equal("basic-play", Assert.IsType<string>(seedEvent.Payload["scenarioId"]));
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        var replayClients = new RecordingHubClients();
        await CreateHub(
                replayClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                development)
            .SeedScenario("room-a", "P1", "basic-play", "seed-same");

        Assert.Empty(replayClients.CallerClient.Errors);
        Assert.Equal(acceptedEventsMessageCount, replayClients.GroupClient.EventMessages.Count);
        Assert.Equal(acceptedSnapshotCount, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(acceptedPromptCount, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Description).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Description).ToArray());
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());

        var conflictClients = new RecordingHubClients();
        await CreateHub(
                conflictClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                development)
            .SeedScenario("room-a", "P1", "movement", "seed-same");

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
    }

    [Fact]
    public async Task SeedScenarioMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        var development = new TestHostEnvironment(Environments.Development);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry, development)
            .JoinRoom("room-a", "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry, development)
            .JoinRoom("room-a", "P2");
        var clients = new RecordingHubClients();

        await CreateHub(
                clients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                development)
            .SeedScenario("room-a", " P1 ", "basic-play", "seed-basic-play-protocol-envelope");

        Assert.Empty(clients.CallerClient.Errors);
        var eventsMessage = Assert.Single(clients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, eventsMessage.Type);
        Assert.Equal("P1", eventsMessage.PlayerId);
        AssertProtocolDefaults(eventsMessage);

        Assert.Equal(2, clients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in clients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(2, clients.GroupClient.Prompts.Count);
        foreach (var promptMessage in clients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }
    }

    [Fact]
    public async Task SeedScenarioReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        var development = new TestHostEnvironment(Environments.Development);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry, development)
            .JoinRoom("room-a", "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry, development)
            .JoinRoom("room-a", "P2");
        var acceptedClients = new RecordingHubClients();

        await CreateHub(
                acceptedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                development)
            .SeedScenario("room-a", "P1", "basic-play", "seed-replay-protocol-envelope");

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        var acceptedEventKinds = EventsFor(acceptedClients)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        var replayClients = new RecordingHubClients();
        await CreateHub(
                replayClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                development)
            .SeedScenario("room-a", " P1 ", "basic-play", "seed-replay-protocol-envelope");

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal("P1", replayEventsMessage.PlayerId);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);
        Assert.Equal(
            acceptedEventKinds,
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());

        Assert.Equal(2, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());

        Assert.Equal(2, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
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

        var p2Snapshot = SnapshotFor(seedClients, "P2");
        var p2BattlefieldStates = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(p2Snapshot.Lanes["battlefields"]);
        var p2Battlefield = Assert.Single(p2BattlefieldStates);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Battlefield["standbyObjectIds"]));
        Assert.Equal(1, Assert.IsType<int>(p2Battlefield["hiddenStandbyCount"]));
        var p2TaskQueue = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Timing["pendingTaskQueue"]);
        Assert.DoesNotContain(
            "P1-STANDBY-ILLEGAL-001",
            Assert.IsType<string>(p2TaskQueue["activeTaskId"]),
            StringComparison.Ordinal);
        var p2Task = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(p2TaskQueue["tasks"]));
        Assert.True(Assert.IsType<bool>(p2Task["hiddenObject"]));
        Assert.DoesNotContain("objectId", p2Task.Keys);

        var p1Prompt = PromptFor(seedClients, "P1");
        var p2Prompt = PromptFor(seedClients, "P2");
        Assert.Equal(["WAIT", "SURRENDER"], p1Prompt.Actions);
        Assert.Equal(["WAIT", "SURRENDER"], p2Prompt.Actions);
        Assert.Contains("待命清理", p1Prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE_ILLEGAL_STANDBY", p1Prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:illegal-standby", p1Prompt.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeedScenarioBroadcastsUnattachedEquipmentCleanupTask()
    {
        const string roomId = "unattached-equipment-cleanup-task-room";
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
            .SeedScenario(roomId, "P1", "battlefield-unattached-equipment-cleanup", "seed-unattached-equipment-cleanup-task");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Timing["pendingTaskQueue"]);
        Assert.True(Assert.IsType<bool>(taskQueue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(taskQueue["isBlocking"]));
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(taskQueue["phase"]));
        var tasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]);
        var task = Assert.Single(tasks);
        Assert.Equal("RECALL_UNATTACHED_EQUIPMENT", Assert.IsType<string>(task["kind"]));
        Assert.Equal("UNATTACHED_EQUIPMENT_CLEANUP", Assert.IsType<string>(task["reason"]));

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Equal(["WAIT", "SURRENDER"], p1Prompt.Actions);
        Assert.Contains("装备清理", p1Prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("RECALL_UNATTACHED_EQUIPMENT", p1Prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:unattached-equipment", p1Prompt.Reason, StringComparison.Ordinal);
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
        Assert.Equal("PLAY_CARD", Assert.IsType<string>(costEvent.Payload["paymentWindow"]));
        Assert.StartsWith("PLAY_CARD:", Assert.IsType<string>(costEvent.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal(2, costEvent.Payload["power"]);
        Assert.Equal(0, Assert.IsType<int>(costEvent.Payload["remainingPower"]));
        var remainingPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Red, remainingPowerByTrait.Keys);
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
    public async Task PlayCardReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts()
    {
        const string roomId = "p7-9-play-card-replay-protocol-envelope";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        var development = new TestHostEnvironment(Environments.Development);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry, development)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry, development)
            .JoinRoom(roomId, "P2");

        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                development)
            .SeedScenario(roomId, "P1", "typed-power-payment", "seed-p7-9-play-card-replay-protocol-envelope");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, "P1-SPELL-BULLET-TIME", StringComparison.Ordinal));
        var playCard = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-BULLET-TIME",
              "cardNo": "OGN·268/298",
              "targetObjectIds": [],
              "optionalCosts": ["SPEND_POWER:red:2"]
            }
            """).RootElement.Clone();
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "play-card-replay-protocol-envelope", playCard);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedEventsMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        var acceptedEventKinds = EventsFor(acceptedClients)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();
        Assert.Contains(acceptedEventKinds, kind => string.Equals(kind, "COST_PAID", StringComparison.Ordinal));
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "play-card-replay-protocol-envelope", playCard);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayEventsMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayEventsMessage.Type);
        Assert.Equal("P1", replayEventsMessage.PlayerId);
        Assert.Equal(acceptedEventsMessage.ServerTick, replayEventsMessage.ServerTick);
        AssertProtocolDefaults(replayEventsMessage);
        Assert.Equal(
            acceptedEventKinds,
            EventsFor(replayClients).Select(gameEvent => gameEvent.Kind).ToArray());

        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());

        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task PlayCardDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "p7-9-play-card-raw-idempotency";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var development = new TestHostEnvironment(Environments.Development);
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
                development)
            .SeedScenario(roomId, "P1", "typed-power-payment", "seed-p7-9-play-card-raw-idempotency");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, "P1-SPELL-BULLET-TIME", StringComparison.Ordinal));
        var seededJournalCount = journal.Entries.Count;
        var playCard = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-BULLET-TIME",
              "cardNo": "OGN·268/298",
              "targetObjectIds": [],
              "optionalCosts": ["SPEND_POWER:red:2"]
            }
            """).RootElement.Clone();
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "play-card-same", playCard);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        var acceptedCostEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("PLAY_CARD", Assert.IsType<string>(acceptedCostEvent.Payload["paymentWindow"]));
        Assert.StartsWith("PLAY_CARD:", Assert.IsType<string>(acceptedCostEvent.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal(2, acceptedCostEvent.Payload["power"]);
        var acceptedSnapshot = SnapshotFor(acceptedClients, "P1");
        var acceptedStackItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(acceptedSnapshot.Stack));
        Assert.Equal(2, Assert.IsType<int>(acceptedStackItem["damageAmount"]));
        var acceptedP1 = PlayerView(acceptedSnapshot, "P1");
        var acceptedRunePool = Assert.IsType<Dictionary<string, object?>>(acceptedP1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(acceptedRunePool["power"]));
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptActions = acceptedClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(seededJournalCount + 1, acceptedJournalCount);
        var playEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "play-card-same", StringComparison.Ordinal));
        Assert.Equal(roomId, playEntry.RoomId);
        Assert.Equal("P1", playEntry.PlayerId);
        Assert.Equal("PLAY_CARD", playEntry.CommandType);
        Assert.NotNull(playEntry.RawCommand);
        var rawCommand = playEntry.RawCommand.Value;
        Assert.Equal("PLAY_CARD", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("P1-SPELL-BULLET-TIME", rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal("OGN·268/298", rawCommand.GetProperty("cardNo").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Equal(
            ["SPEND_POWER:red:2"],
            rawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "play-card-same", playCard);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("P1", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var replayCostEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(acceptedCostEvent.Payload["paymentWindow"], replayCostEvent.Payload["paymentWindow"]);
        Assert.Equal(acceptedCostEvent.Payload["paymentId"], replayCostEvent.Payload["paymentId"]);
        Assert.Equal(acceptedCostEvent.Payload["power"], replayCostEvent.Payload["power"]);
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        var replaySnapshot = SnapshotFor(replayClients, "P1");
        Assert.Equal(acceptedSnapshot.Tick, replaySnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, replaySnapshot.ActivePlayerId);
        Assert.Single(replaySnapshot.Stack);
        var replayPromptActions = replayClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        Assert.Equal(acceptedPromptActions, replayPromptActions);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedPlayCard = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-BULLET-TIME",
              "cardNo": "OGN·268/298",
              "targetObjectIds": [],
              "optionalCosts": ["SPEND_POWER:red:2"],
              "clientNote": "changed"
            }
            """).RootElement.Clone();

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "play-card-same", changedPlayCard);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Single(currentSnapshot.Stack);
        var currentP1 = PlayerView(currentSnapshot, "P1");
        var currentRunePool = Assert.IsType<Dictionary<string, object?>>(currentP1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(currentRunePool["power"]));
    }

    [Fact]
    public async Task SubmitIntentAcceptsMatchingPromptStamp()
    {
        const string roomId = "prompt-stamp-matching-core";
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
            .SeedScenario(roomId, "P1", "typed-power-payment", "seed-prompt-stamp-matching");

        Assert.Empty(seedClients.CallerClient.Errors);
        var prompt = PromptFor(seedClients, "P1");
        Assert.False(string.IsNullOrWhiteSpace(prompt.PromptId));
        Assert.True(prompt.SnapshotTick.HasValue);

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-matching-prompt-stamp", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-SPELL-BULLET-TIME",
                cardNo = "OGN·268/298",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { "SPEND_POWER:red:2" },
                promptId = prompt.PromptId,
                snapshotTick = prompt.SnapshotTick
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var events = EventsFor(playClients);
        Assert.Contains(events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SubmitIntentRejectsStalePromptStamp()
    {
        const string roomId = "prompt-stamp-expired-core";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "typed-power-payment", "seed-prompt-stamp-expired");

        Assert.Empty(seedClients.CallerClient.Errors);
        var prompt = PromptFor(seedClients, "P1");
        Assert.False(string.IsNullOrWhiteSpace(prompt.PromptId));
        Assert.True(prompt.SnapshotTick.HasValue);
        var seedJournalCount = journal.Entries.Count;
        var seededEntry = Assert.Single(journal.Entries);
        var seededStateHash = MatchStateHasher.Hash(seededEntry.AuthoritativeState);
        var seededPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(seededEntry.AuthoritativeState));
        var seededSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(seededEntry.AuthoritativeState));

        var staleClients = new RecordingHubClients();
        var stalePromptId = $"{prompt.PromptId}:stale";
        var stalePlayCard = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            sourceObjectId = "P1-SPELL-BULLET-TIME",
            cardNo = "OGN·268/298",
            targetObjectIds = Array.Empty<string>(),
            optionalCosts = new[] { "SPEND_POWER:red:2" },
            promptId = stalePromptId,
            snapshotTick = prompt.SnapshotTick.Value
        });
        await CreateHub(staleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-stale-prompt-stamp", stalePlayCard);

        var error = Assert.Single(staleClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.PromptExpired, payload.Code);
        Assert.Empty(staleClients.GroupClient.EventMessages);
        Assert.Empty(staleClients.GroupClient.Snapshots);
        Assert.Empty(staleClients.GroupClient.Prompts);
        Assert.Empty(staleClients.CallerClient.EventMessages);
        Assert.Empty(staleClients.CallerClient.Snapshots);
        Assert.Empty(staleClients.CallerClient.Prompts);
        Assert.Equal(seedJournalCount + 1, journal.Entries.Count);
        var rejectedEntry = journal.Entries[^1];
        Assert.Equal(roomId, rejectedEntry.RoomId);
        Assert.Equal("P1", rejectedEntry.PlayerId);
        Assert.Equal("intent-stale-prompt-stamp", rejectedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PlayCard, rejectedEntry.CommandType);
        Assert.False(rejectedEntry.Accepted);
        Assert.Equal(payload.Message, rejectedEntry.ErrorMessage);
        Assert.Empty(rejectedEntry.Events);
        Assert.Equal(seededStateHash, MatchStateHasher.Hash(rejectedEntry.AuthoritativeState));
        Assert.Equal(seededPromptsHash, MatchStateHasher.HashValue(rejectedEntry.Prompts));
        Assert.Equal(seededSnapshotsHash, MatchStateHasher.HashValue(rejectedEntry.Snapshots));
        Assert.True(rejectedEntry.RawCommand.HasValue);
        var rejectedRawCommand = rejectedEntry.RawCommand.Value;
        Assert.Equal(CommandTypes.PlayCard, rejectedRawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("P1-SPELL-BULLET-TIME", rejectedRawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal("OGN·268/298", rejectedRawCommand.GetProperty("cardNo").GetString());
        Assert.Equal(
            ["SPEND_POWER:red:2"],
            rejectedRawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.Equal(stalePromptId, rejectedRawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick.Value, rejectedRawCommand.GetProperty("snapshotTick").GetInt64());

        var staleJournalCount = journal.Entries.Count;
        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-stale-prompt-stamp", stalePlayCard);

        var replayError = Assert.Single(replayClients.CallerClient.Errors);
        var replayPayload = Assert.IsType<ErrorDto>(replayError.Payload);
        Assert.Equal(ErrorCodes.PromptExpired, replayPayload.Code);
        Assert.Equal(payload.Message, replayPayload.Message);
        Assert.Empty(replayClients.GroupClient.EventMessages);
        Assert.Empty(replayClients.GroupClient.Snapshots);
        Assert.Empty(replayClients.GroupClient.Prompts);
        Assert.Empty(replayClients.CallerClient.EventMessages);
        Assert.Empty(replayClients.CallerClient.Snapshots);
        Assert.Empty(replayClients.CallerClient.Prompts);
        Assert.Equal(staleJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedStalePlayCard = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            sourceObjectId = "P1-SPELL-BULLET-TIME",
            cardNo = "OGN·268/298",
            targetObjectIds = Array.Empty<string>(),
            optionalCosts = new[] { "SPEND_POWER:red:1" },
            promptId = stalePromptId,
            snapshotTick = prompt.SnapshotTick.Value,
            clientNote = "changed-stale-prompt"
        });

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-stale-prompt-stamp", changedStalePlayCard);

        var conflictError = Assert.Single(conflictClients.CallerClient.Errors);
        var conflictPayload = Assert.IsType<ErrorDto>(conflictError.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflictPayload.Code);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(staleJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-stale-prompt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SubmitIntentRejectsStaleSnapshotTickWithMatchingPromptId()
    {
        const string roomId = "prompt-stamp-stale-snapshot-core";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "typed-power-payment", "seed-prompt-stamp-stale-snapshot");

        Assert.Empty(seedClients.CallerClient.Errors);
        var prompt = PromptFor(seedClients, "P1");
        Assert.False(string.IsNullOrWhiteSpace(prompt.PromptId));
        Assert.True(prompt.SnapshotTick.HasValue);
        var seedJournalCount = journal.Entries.Count;
        var seededEntry = Assert.Single(journal.Entries);
        var seededStateHash = MatchStateHasher.Hash(seededEntry.AuthoritativeState);
        var seededPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(seededEntry.AuthoritativeState));
        var seededSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(seededEntry.AuthoritativeState));

        var staleClients = new RecordingHubClients();
        var staleSnapshotTick = prompt.SnapshotTick.Value + 1;
        var stalePlayCard = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            sourceObjectId = "P1-SPELL-BULLET-TIME",
            cardNo = "OGN·268/298",
            targetObjectIds = Array.Empty<string>(),
            optionalCosts = new[] { "SPEND_POWER:red:2" },
            promptId = prompt.PromptId,
            snapshotTick = staleSnapshotTick
        });
        await CreateHub(staleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-stale-snapshot-stamp", stalePlayCard);

        var error = Assert.Single(staleClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.PromptExpired, payload.Code);
        Assert.Empty(staleClients.GroupClient.EventMessages);
        Assert.Empty(staleClients.GroupClient.Snapshots);
        Assert.Empty(staleClients.GroupClient.Prompts);
        Assert.Empty(staleClients.CallerClient.EventMessages);
        Assert.Empty(staleClients.CallerClient.Snapshots);
        Assert.Empty(staleClients.CallerClient.Prompts);
        Assert.Equal(seedJournalCount + 1, journal.Entries.Count);
        var rejectedEntry = journal.Entries[^1];
        Assert.Equal(roomId, rejectedEntry.RoomId);
        Assert.Equal("P1", rejectedEntry.PlayerId);
        Assert.Equal("intent-stale-snapshot-stamp", rejectedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PlayCard, rejectedEntry.CommandType);
        Assert.False(rejectedEntry.Accepted);
        Assert.Equal(payload.Message, rejectedEntry.ErrorMessage);
        Assert.Empty(rejectedEntry.Events);
        Assert.Equal(seededStateHash, MatchStateHasher.Hash(rejectedEntry.AuthoritativeState));
        Assert.Equal(seededPromptsHash, MatchStateHasher.HashValue(rejectedEntry.Prompts));
        Assert.Equal(seededSnapshotsHash, MatchStateHasher.HashValue(rejectedEntry.Snapshots));
        Assert.True(rejectedEntry.RawCommand.HasValue);
        var rejectedRawCommand = rejectedEntry.RawCommand.Value;
        Assert.Equal(CommandTypes.PlayCard, rejectedRawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("P1-SPELL-BULLET-TIME", rejectedRawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal("OGN·268/298", rejectedRawCommand.GetProperty("cardNo").GetString());
        Assert.Equal(
            ["SPEND_POWER:red:2"],
            rejectedRawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.Equal(prompt.PromptId, rejectedRawCommand.GetProperty("promptId").GetString());
        Assert.Equal(staleSnapshotTick, rejectedRawCommand.GetProperty("snapshotTick").GetInt64());

        var staleJournalCount = journal.Entries.Count;
        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-stale-snapshot-stamp", stalePlayCard);

        var replayError = Assert.Single(replayClients.CallerClient.Errors);
        var replayPayload = Assert.IsType<ErrorDto>(replayError.Payload);
        Assert.Equal(ErrorCodes.PromptExpired, replayPayload.Code);
        Assert.Equal(payload.Message, replayPayload.Message);
        Assert.Empty(replayClients.GroupClient.EventMessages);
        Assert.Empty(replayClients.GroupClient.Snapshots);
        Assert.Empty(replayClients.GroupClient.Prompts);
        Assert.Empty(replayClients.CallerClient.EventMessages);
        Assert.Empty(replayClients.CallerClient.Snapshots);
        Assert.Empty(replayClients.CallerClient.Prompts);
        Assert.Equal(staleJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedStalePlayCard = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            sourceObjectId = "P1-SPELL-BULLET-TIME",
            cardNo = "OGN·268/298",
            targetObjectIds = Array.Empty<string>(),
            optionalCosts = new[] { "SPEND_POWER:red:1" },
            promptId = prompt.PromptId,
            snapshotTick = staleSnapshotTick,
            clientNote = "changed-stale-snapshot"
        });

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-stale-snapshot-stamp", changedStalePlayCard);

        var conflictError = Assert.Single(conflictClients.CallerClient.Errors);
        var conflictPayload = Assert.IsType<ErrorDto>(conflictError.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflictPayload.Code);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(staleJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-stale-snapshot", StringComparison.Ordinal));
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
    public async Task P79TypedPowerPaymentMixedRecycleSeedExposesTraitsAndAcceptsMatchingResourceThroughHub()
    {
        const string roomId = "p7-9-typed-power-payment-mixed-recycle-core";
        const string redPaymentRuneObjectId = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        const string bluePaymentRuneObjectId = "P1-RUNE-BLUE-EXTRA-PAYMENT-001";
        var redPaymentResourceAction = $"RECYCLE_RUNE:{redPaymentRuneObjectId}";
        var bluePaymentResourceAction = $"RECYCLE_RUNE:{bluePaymentRuneObjectId}";
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
            .SeedScenario(roomId, "P1", "typed-power-payment-mixed-recycle", "seed-p7-9-typed-power-payment-mixed-recycle");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Contains(redPaymentResourceAction, paymentResourceChoices);
        Assert.Contains(bluePaymentResourceAction, paymentResourceChoices);
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Red, Assert.IsType<string>(paymentResourcePowerByChoice[redPaymentResourceAction]["trait"]));
        Assert.Equal(RuneTrait.Blue, Assert.IsType<string>(paymentResourcePowerByChoice[bluePaymentResourceAction]["trait"]));

        var rejectedClients = new RecordingHubClients();
        await CreateHub(rejectedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-typed-power-mixed-reject-blue", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-SPELL-BULLET-TIME",
                cardNo = "OGN·268/298",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { bluePaymentResourceAction, "SPEND_POWER:red:2" }
        }));

        var error = Assert.Single(rejectedClients.CallerClient.Errors);
        var errorPayload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.InsufficientCost, errorPayload.Code);

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-typed-power-mixed-red-bullet-time", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-SPELL-BULLET-TIME",
                cardNo = "OGN·268/298",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { redPaymentResourceAction, "SPEND_POWER:red:2" }
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        var costEvent = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([redPaymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        var snapshot = SnapshotFor(playClients, "P1");
        var stackItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
        Assert.Equal(2, Assert.IsType<int>(stackItem["damageAmount"]));
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        var baseZone = Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]);
        Assert.DoesNotContain(redPaymentRuneObjectId, baseZone);
        Assert.Contains(bluePaymentRuneObjectId, baseZone);
        var runePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(runePool["powerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Red, powerByTrait.Keys);
    }

    [Fact]
    public async Task P79TypedPowerPaymentGenericMixedRecycleSeedAcceptsAnyTraitResourceThroughHub()
    {
        const string roomId = "p7-9-typed-power-payment-generic-mixed-recycle-core";
        const string redPaymentRuneObjectId = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        const string bluePaymentRuneObjectId = "P1-RUNE-BLUE-EXTRA-PAYMENT-001";
        var redPaymentResourceAction = $"RECYCLE_RUNE:{redPaymentRuneObjectId}";
        var bluePaymentResourceAction = $"RECYCLE_RUNE:{bluePaymentRuneObjectId}";
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
            .SeedScenario(roomId, "P1", "typed-power-payment-generic-mixed-recycle", "seed-p7-9-typed-power-payment-generic-mixed-recycle");

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
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Contains("SPEND_POWER:2", optionalCostChoices);
        Assert.Contains(redPaymentResourceAction, optionalCostChoices);
        Assert.Contains(bluePaymentResourceAction, optionalCostChoices);
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["availablePower"]));
        Assert.Equal(3, Assert.IsType<int>(sourceRequirement["availablePowerWithPaymentResources"]));
        var availablePowerByTraitWithPaymentResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            sourceRequirement["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(2, availablePowerByTraitWithPaymentResources[RuneTrait.Red]);
        Assert.Equal(1, availablePowerByTraitWithPaymentResources[RuneTrait.Blue]);

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-typed-power-generic-mixed-blue-bullet-time", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-SPELL-BULLET-TIME",
                cardNo = "OGN·268/298",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { bluePaymentResourceAction, "SPEND_POWER:2" }
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        var costEvent = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(2, costEvent.Payload["power"]);
        Assert.Equal(["SPEND_POWER:2"], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal([bluePaymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        var snapshot = SnapshotFor(playClients, "P1");
        var stackItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
        Assert.Equal(2, Assert.IsType<int>(stackItem["damageAmount"]));
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        var baseZone = Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]);
        Assert.Contains(redPaymentRuneObjectId, baseZone);
        Assert.DoesNotContain(bluePaymentRuneObjectId, baseZone);
        Assert.Equal(2, Assert.IsType<int>(p1Zones["runeDeckCount"]));
        var runePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(runePool["powerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Red, powerByTrait.Keys);
        Assert.DoesNotContain(RuneTrait.Blue, powerByTrait.Keys);
    }

    [Fact]
    public async Task P79HastePaymentRecycleSeedPaysReadyBranchThroughHub()
    {
        const string roomId = "p7-9-haste-payment-recycle-core";
        const string paymentRuneObjectId = "P1-RUNE-PURPLE-HASTE-PAYMENT-001";
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
            .SeedScenario(roomId, "P1", "haste-payment-recycle", "seed-p7-9-haste-payment-recycle");

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
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Contains(HasteOptionalCostNames.HasteReady, optionalCostChoices);
        Assert.Contains(paymentResourceAction, optionalCostChoices);
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Contains(paymentResourceAction, paymentResourceChoices);
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["availablePower"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["availablePowerWithPaymentResources"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["hasteReadyPowerCost"]));

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-haste-payment-recycle-sivir", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-UNIT-SIVIR",
                cardNo = "SFD·143/221",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { paymentResourceAction, HasteOptionalCostNames.HasteReady }
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        Assert.Contains(playEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        var costEvent = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(5, costEvent.Payload["mana"]);
        Assert.Equal(1, costEvent.Payload["power"]);
        Assert.Equal([HasteOptionalCostNames.HasteReady], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        var playSnapshot = SnapshotFor(playClients, "P1");
        Assert.Single(playSnapshot.Stack);

        var passP1Clients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-haste-payment-recycle-p1-pass", passPriority);

        Assert.Empty(passP1Clients.CallerClient.Errors);

        var passP2Clients = new RecordingHubClients();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-haste-payment-recycle-p2-pass", passPriority);

        Assert.Empty(passP2Clients.CallerClient.Errors);
        var resolveEvents = EventsFor(passP2Clients);
        var unitPlayedEvent = Assert.Single(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        Assert.Equal(true, unitPlayedEvent.Payload["hasteReadyOptionalCostPaid"]);
        Assert.Equal(false, unitPlayedEvent.Payload["isExhausted"]);
        var finalSnapshot = SnapshotFor(passP2Clients, "P1");
        Assert.Empty(finalSnapshot.Stack);
        var p1 = Assert.IsType<Dictionary<string, object?>>(finalSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        var baseZone = Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]);
        Assert.Contains("P1-UNIT-SIVIR", baseZone);
        Assert.DoesNotContain(paymentRuneObjectId, baseZone);
        Assert.Equal(2, Assert.IsType<int>(p1Zones["runeDeckCount"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var sivir = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-UNIT-SIVIR"]);
        Assert.False(Assert.IsType<bool>(sivir["isExhausted"]));
    }

    [Fact]
    public async Task P79HasteColoredPaymentRecycleSeedRequiresMatchingTraitThroughHub()
    {
        const string roomId = "p7-9-haste-colored-payment-recycle-core";
        const string bluePaymentRuneObjectId = "P1-RUNE-BLUE-HASTE-PAYMENT-001";
        const string purplePaymentRuneObjectId = "P1-RUNE-PURPLE-HASTE-PAYMENT-001";
        var bluePaymentResourceAction = $"RECYCLE_RUNE:{bluePaymentRuneObjectId}";
        var purplePaymentResourceAction = $"RECYCLE_RUNE:{purplePaymentRuneObjectId}";
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
            .SeedScenario(roomId, "P1", "haste-payment-colored-recycle", "seed-p7-9-haste-colored-payment-recycle");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal(RuneTrait.Purple, Assert.IsType<string>(sourceRequirement["hasteReadyPowerTrait"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["availablePower"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["hasteReadyPowerCost"]));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.DoesNotContain(bluePaymentResourceAction, paymentResourceChoices);
        Assert.Contains(purplePaymentResourceAction, paymentResourceChoices);
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["availablePowerWithPaymentResources"]));
        var availablePowerByTraitWithPaymentResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            sourceRequirement["availablePowerByTraitWithPaymentResources"]);
        Assert.DoesNotContain(RuneTrait.Blue, availablePowerByTraitWithPaymentResources.Keys);
        Assert.Equal(1, availablePowerByTraitWithPaymentResources[RuneTrait.Purple]);
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.DoesNotContain(bluePaymentResourceAction, paymentResourcePowerByChoice.Keys);
        Assert.Equal(RuneTrait.Purple, Assert.IsType<string>(paymentResourcePowerByChoice[purplePaymentResourceAction]["trait"]));

        var rejectedClients = new RecordingHubClients();
        await CreateHub(rejectedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-haste-colored-blue-reject", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-UNIT-SIVIR",
                cardNo = "SFD·143/221",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { bluePaymentResourceAction, HasteOptionalCostNames.HasteReady }
            }));

        var rejectedError = Assert.Single(rejectedClients.CallerClient.Errors);
        var rejectedPayload = Assert.IsType<ErrorDto>(rejectedError.Payload);
        Assert.Equal(ErrorCodes.InsufficientCost, rejectedPayload.Code);

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-haste-colored-purple-sivir", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-UNIT-SIVIR",
                cardNo = "SFD·143/221",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { purplePaymentResourceAction, HasteOptionalCostNames.HasteReady }
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        var costEvent = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([HasteOptionalCostNames.HasteReady], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal([purplePaymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        var playSnapshot = SnapshotFor(playClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(playSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        var baseZone = Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]);
        Assert.Contains(bluePaymentRuneObjectId, baseZone);
        Assert.DoesNotContain(purplePaymentRuneObjectId, baseZone);
        Assert.Equal(2, Assert.IsType<int>(p1Zones["runeDeckCount"]));
    }

    [Fact]
    public async Task P79SpellshieldMultipleTaxSeedEnumeratesLegalTargetsAndPaysThroughHub()
    {
        const string roomId = "p7-9-spellshield-multiple-tax-core";
        const string firstShieldTarget = "P2-SPIRIT-FIRE-SPELLSHIELD-001";
        const string secondShieldTarget = "P2-SPIRIT-FIRE-SPELLSHIELD2-001";
        const string keeperTarget = "P2-SPIRIT-FIRE-KEEPER-001";
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
            .SeedScenario(roomId, "P1", "spellshield-multiple-tax", "seed-p7-9-spellshield-multiple-tax");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("P1-SPELL-SPIRIT-FIRE", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("OGN·256/298", Assert.IsType<string>(sourceRequirement["cardNo"]));
        Assert.Equal(3, Assert.IsType<int>(sourceRequirement["minimumManaCost"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(4, Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        var legalTargetSelections = Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(
                sourceRequirement["legalTargetSelections"])
            .Select(selection => selection.ToArray())
            .ToArray();
        Assert.Contains(legalTargetSelections, selection => selection.SequenceEqual([firstShieldTarget, secondShieldTarget]));
        Assert.DoesNotContain(legalTargetSelections, selection => selection.Contains(keeperTarget, StringComparer.Ordinal));

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-spellshield-multiple-tax-play", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-SPELL-SPIRIT-FIRE",
                cardNo = "OGN·256/298",
                targetObjectIds = new[] { firstShieldTarget, secondShieldTarget }
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        var costEvent = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(6, costEvent.Payload["mana"]);
        Assert.Equal(3, costEvent.Payload["baseMana"]);
        Assert.Equal(3, costEvent.Payload["spellshieldTaxMana"]);
        Assert.Equal([firstShieldTarget, secondShieldTarget], Assert.IsType<string[]>(costEvent.Payload["spellshieldTaxTargetObjectIds"]));
        var stackAdded = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Equal(
            [firstShieldTarget, secondShieldTarget],
            Assert.IsType<string[]>(stackAdded.Payload["targetObjectIds"]));
        var playSnapshot = SnapshotFor(playClients, "P1");
        Assert.Single(playSnapshot.Stack);

        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-spellshield-multiple-tax-p1-pass", passPriority);

        var resolveClients = new RecordingHubClients();
        await CreateHub(resolveClients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-spellshield-multiple-tax-p2-pass", passPriority);

        Assert.Empty(resolveClients.CallerClient.Errors);
        var resolveEvents = EventsFor(resolveClients);
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, firstShieldTarget, StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, secondShieldTarget, StringComparison.Ordinal));

        var finalSnapshot = SnapshotFor(resolveClients, "P1");
        Assert.Empty(finalSnapshot.Stack);
        var p1 = Assert.IsType<Dictionary<string, object?>>(finalSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-SPELL-SPIRIT-FIRE"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
        var p1RunePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(p1RunePool["mana"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(finalSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal([keeperTarget], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        Assert.Equal(
            [firstShieldTarget, secondShieldTarget],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
        Assert.Contains("END_TURN", PromptFor(resolveClients, "P1").Actions);
    }

    [Fact]
    public async Task P79SpellshieldTaxInsufficientSeedHidesUnpayablePlaySourceThroughHub()
    {
        const string roomId = "p7-9-spellshield-tax-insufficient-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "spellshield-tax-insufficient-prompt",
                "seed-p7-9-spellshield-tax-insufficient-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.False(playCandidate.Enabled);
        Assert.Empty(playCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-SPELL-INCINERATE"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        var p1RunePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        Assert.Equal(2, Assert.IsType<int>(p1RunePool["mana"]));
    }

    [Fact]
    public async Task P79UnknownPlaySourceSeedHidesHandObjectWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-play-source-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-play-source-prompt",
                "seed-p7-9-unknown-play-source-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.False(playCandidate.Enabled);
        Assert.Empty(playCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-HAND-UNKNOWN-PLAY-SOURCE"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownObject = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-HAND-UNKNOWN-PLAY-SOURCE"]);
        Assert.Null(unknownObject["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownPlayTargetSeedHidesTargetWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-play-target-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-play-target-prompt",
                "seed-p7-9-unknown-play-target-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.False(playCandidate.Enabled);
        Assert.Empty(playCandidate.Sources ?? []);
        Assert.Empty(playCandidate.Targets ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-SPELL-UNKNOWN-PLAY-TARGET"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(["P2-UNIT-UNKNOWN-PLAY-TARGET"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));

        var p2Snapshot = SnapshotFor(seedClients, "P2");
        var p2Self = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        var p2Objects = Assert.IsType<Dictionary<string, object?>>(p2Self["objects"]);
        var unknownTarget = Assert.IsType<Dictionary<string, object?>>(p2Objects["P2-UNIT-UNKNOWN-PLAY-TARGET"]);
        Assert.Null(unknownTarget["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownHideCardSourceSeedHidesStandbyWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-hide-card-source-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-hide-card-source-prompt",
                "seed-p7-9-unknown-hide-card-source-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var hideCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "HIDE_CARD", StringComparison.Ordinal));
        Assert.False(hideCandidate.Enabled);
        Assert.Empty(hideCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(hideCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-HAND-UNKNOWN-HIDE-SOURCE"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownStandby = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-HAND-UNKNOWN-HIDE-SOURCE"]);
        Assert.Null(unknownStandby["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownRevealCardSourceSeedHidesFaceDownStandbyWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-reveal-card-source-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-reveal-card-source-prompt",
                "seed-p7-9-unknown-reveal-card-source-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var revealCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "REVEAL_CARD", StringComparison.Ordinal));
        Assert.False(revealCandidate.Enabled);
        Assert.Empty(revealCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(revealCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(
            ["P1-FACEDOWN-UNKNOWN-REVEAL-SOURCE"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownStandby = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-FACEDOWN-UNKNOWN-REVEAL-SOURCE"]);
        Assert.Null(unknownStandby["cardNo"]);
        Assert.True(Assert.IsType<bool>(unknownStandby["isFaceDown"]));
    }

    [Fact]
    public async Task P79UnknownAssembleSourceSeedHidesEquipmentWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-assemble-source-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-assemble-source-prompt",
                "seed-p7-9-unknown-assemble-source-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var assembleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.False(assembleCandidate.Enabled);
        Assert.Empty(assembleCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains(
            "P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownEquipment = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE"]);
        Assert.Null(unknownEquipment["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownAssembleTargetSeedHidesTargetWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-assemble-target-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-assemble-target-prompt",
                "seed-p7-9-unknown-assemble-target-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var assembleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.False(assembleCandidate.Enabled);
        Assert.Empty(assembleCandidate.Sources ?? []);
        Assert.Empty(assembleCandidate.Targets ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains(
            "P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        Assert.Contains(
            "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var equipment = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER"]);
        Assert.Equal("SFD·022/221", Assert.IsType<string>(equipment["cardNo"]));
        var unknownTarget = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-UNIT-UNKNOWN-ASSEMBLE-TARGET"]);
        Assert.Null(unknownTarget["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownLegendActionSourceSeedHidesLegendWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-legend-action-source-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-legend-action-source-prompt",
                "seed-p7-9-unknown-legend-action-source-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var legendCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal));
        Assert.False(legendCandidate.Enabled);
        Assert.Empty(legendCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(legendCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(
            ["P1-LEGEND-UNKNOWN-ACTION-SOURCE"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["legendZone"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownLegend = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-LEGEND-UNKNOWN-ACTION-SOURCE"]);
        Assert.Null(unknownLegend["cardNo"]);
        Assert.Contains(
            "P1-BATTLEFIELD-PORO-FORGE",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
    }

    [Fact]
    public async Task P79UnknownLegendActionTargetSeedHidesUnitWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-legend-action-target-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-legend-action-target-prompt",
                "seed-p7-9-unknown-legend-action-target-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var legendCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal));
        Assert.False(legendCandidate.Enabled);
        Assert.Empty(legendCandidate.Sources ?? []);
        Assert.Empty(legendCandidate.Targets ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(legendCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains(
            "P1-LEGEND-YASUO-TARGET-FILTER",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["legendZone"]));
        Assert.Contains(
            "P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var legend = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-LEGEND-YASUO-TARGET-FILTER"]);
        Assert.Equal("FND-259/298", Assert.IsType<string>(legend["cardNo"]));
        var unknownTarget = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET"]);
        Assert.Null(unknownTarget["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownActivateAbilitySourceSeedHidesGrantedUnitWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-activate-ability-source-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-activate-ability-source-prompt",
                "seed-p7-9-unknown-activate-ability-source-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var abilityCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        Assert.False(abilityCandidate.Enabled);
        Assert.Empty(abilityCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(abilityCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains(
            "P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        Assert.Contains(
            "P1-BATTLEFIELD-MUTATION-GARDEN",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownUnit = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE"]);
        Assert.Null(unknownUnit["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownActivateAbilityTargetSeedHidesUnitWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-activate-ability-target-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-activate-ability-target-prompt",
                "seed-p7-9-unknown-activate-ability-target-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var abilityCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        Assert.True(abilityCandidate.Enabled);
        Assert.Equal(
            ["P1-UNIT-XERATH-TARGET-FILTER"],
            (abilityCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(
            ["P1-UNIT-XERATH-TARGET-FILTER"],
            (abilityCandidate.Targets ?? []).Select(target => target.Id).ToArray());
        var metadata = Assert.IsType<Dictionary<string, object?>>(abilityCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        var xerathRequirement = Assert.Single(sourceRequirements);
        Assert.Equal("P1-UNIT-XERATH-TARGET-FILTER", Assert.IsType<string>(xerathRequirement["sourceObjectId"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            xerathRequirement["targetChoicesByIndex"]);
        Assert.Equal(
            ["P1-UNIT-XERATH-TARGET-FILTER"],
            targetChoicesByIndex["0"].Select(choice => choice.Id).ToArray());

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(
            ["P1-UNIT-XERATH-TARGET-FILTER"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var xerath = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-UNIT-XERATH-TARGET-FILTER"]);
        Assert.Equal("UNL-026/219", Assert.IsType<string>(xerath["cardNo"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(
            ["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));

        var p2Snapshot = SnapshotFor(seedClients, "P2");
        var p2Self = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P2"]);
        var p2Objects = Assert.IsType<Dictionary<string, object?>>(p2Self["objects"]);
        var unknownTarget = Assert.IsType<Dictionary<string, object?>>(
            p2Objects["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"]);
        Assert.Null(unknownTarget["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownMoveUnitSourceSeedHidesUnitWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-move-unit-source-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-move-unit-source-prompt",
                "seed-p7-9-unknown-move-unit-source-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var moveCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.False(moveCandidate.Enabled);
        Assert.Empty(moveCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(moveCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains(
            "P1-UNIT-UNKNOWN-MOVE-SOURCE",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownUnit = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-UNIT-UNKNOWN-MOVE-SOURCE"]);
        Assert.Null(unknownUnit["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownMoveUnitBattlefieldSeedHidesDestinationWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-move-unit-battlefield-prompt-core";
        const string unknownBattlefieldObjectId = "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-move-unit-battlefield-prompt",
                "seed-p7-9-unknown-move-unit-battlefield-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var moveCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.True(moveCandidate.Enabled);
        Assert.Contains(
            moveCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER", StringComparison.Ordinal));
        Assert.DoesNotContain(
            moveCandidate.Destinations ?? [],
            choice => string.Equals(choice.Id, $"BATTLEFIELD:{unknownBattlefieldObjectId}", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(moveCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var roamRequirement = Assert.Single(sourceRequirements, requirement =>
            string.Equals(requirement["mode"] as string, "ROAM", StringComparison.Ordinal));
        var destinationChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            roamRequirement["destinationChoices"]);
        Assert.Equal(["BATTLEFIELD:P1-MAIN"], destinationChoices.Select(choice => choice.Id).ToArray());

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains(
            unknownBattlefieldObjectId,
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownBattlefield = Assert.IsType<Dictionary<string, object?>>(p1Objects[unknownBattlefieldObjectId]);
        Assert.Null(unknownBattlefield["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownRuneSourceSeedHidesRuneWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-rune-source-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-rune-source-prompt",
                "seed-p7-9-unknown-rune-source-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var tapCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "TAP_RUNE", StringComparison.Ordinal));
        Assert.False(tapCandidate.Enabled);
        Assert.Empty(tapCandidate.Sources ?? []);
        var recycleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "RECYCLE_RUNE", StringComparison.Ordinal));
        Assert.False(recycleCandidate.Enabled);
        Assert.Empty(recycleCandidate.Sources ?? []);

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains(
            "P1-RUNE-UNKNOWN-SOURCE",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownRune = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-RUNE-UNKNOWN-SOURCE"]);
        Assert.Null(unknownRune["cardNo"]);
    }

    [Fact]
    public async Task P79UnknownDeclareBattleSourceSeedHidesCombatantsWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-declare-battle-source-prompt-core";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-declare-battle-source-prompt",
                "seed-p7-9-unknown-declare-battle-source-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.False(battleCandidate.Enabled);
        Assert.Empty(battleCandidate.Sources ?? []);
        Assert.Empty(battleCandidate.Targets ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(battleCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains(
            "P1-BATTLE-UNKNOWN-ATTACKER",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        var p2 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Contains(
            "P2-BATTLE-UNKNOWN-DEFENDER",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownAttacker = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-BATTLE-UNKNOWN-ATTACKER"]);
        Assert.Null(unknownAttacker["cardNo"]);
        Assert.False(p1Objects.ContainsKey("P2-BATTLE-UNKNOWN-DEFENDER"));
    }

    [Fact]
    public async Task P79UnknownDeclareBattleBattlefieldSeedHidesDestinationWithoutCardNoThroughHub()
    {
        const string roomId = "p7-9-unknown-declare-battle-battlefield-prompt-core";
        const string unknownBattlefieldObjectId = "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION";
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
            .SeedScenario(
                roomId,
                "P1",
                "unknown-declare-battle-battlefield-prompt",
                "seed-p7-9-unknown-declare-battle-battlefield-prompt");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var battleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.True(battleCandidate.Enabled);
        Assert.Equal(["P1-BATTLE-KNOWN-ATTACKER"], (battleCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["P2-BATTLE-KNOWN-DEFENDER"], (battleCandidate.Targets ?? []).Select(target => target.Id).ToArray());
        Assert.DoesNotContain(
            battleCandidate.Destinations ?? [],
            choice => string.Equals(choice.Id, unknownBattlefieldObjectId, StringComparison.Ordinal));

        var metadata = Assert.IsType<Dictionary<string, object?>>(battleCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var battlefieldChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["battlefieldChoices"]);
        Assert.Contains(battlefieldChoices, choice => string.Equals(choice.Id, "BATTLEFIELD:P1-MAIN", StringComparison.Ordinal));
        Assert.DoesNotContain(
            battlefieldChoices,
            choice => string.Equals(choice.Id, unknownBattlefieldObjectId, StringComparison.Ordinal));

        var p1Snapshot = SnapshotFor(seedClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Contains(
            unknownBattlefieldObjectId,
            Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["battlefields"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var unknownBattlefield = Assert.IsType<Dictionary<string, object?>>(p1Objects[unknownBattlefieldObjectId]);
        Assert.Null(unknownBattlefield["cardNo"]);
    }

    [Fact]
    public async Task P79AssemblePaymentRecycleSeedOffersResourceAndAttachesThroughHub()
    {
        const string roomId = "p7-9-assemble-payment-recycle-core";
        const string equipmentObjectId = "P1-EQUIPMENT-LONG-SWORD-ASSEMBLE-PAYMENT";
        const string targetObjectId = "P1-UNIT-ASSEMBLE-PAYMENT-TARGET";
        const string paymentRuneObjectId = "P1-RUNE-RED-ASSEMBLE-PAYMENT-001";
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
            .SeedScenario(roomId, "P1", "assemble-payment-recycle", "seed-p7-9-assemble-payment-recycle");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var assembleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(assembleCandidate.Enabled);
        Assert.Equal([equipmentObjectId], (assembleCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .ToArray();
        Assert.Equal([paymentResourceAction], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        var availablePowerByTraitWithPaymentResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            sourceRequirement["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(1, availablePowerByTraitWithPaymentResources[RuneTrait.Red]);

        var attachClients = new RecordingHubClients();
        await CreateHub(attachClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-assemble-payment-recycle", JsonSerializer.SerializeToElement(new
            {
                cmdType = "ASSEMBLE_EQUIPMENT",
                sourceObjectId = equipmentObjectId,
                targetObjectId,
                optionalCosts = new[] { "ASSEMBLE_RED", paymentResourceAction }
            }));

        Assert.Empty(attachClients.CallerClient.Errors);
        var events = EventsFor(attachClients);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "EQUIPMENT_ATTACHED"],
            events.Select(gameEvent => gameEvent.Kind).ToArray());
        var recycleEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Equal("ASSEMBLE_EQUIPMENT", recycleEvent.Payload["paymentWindow"]);
        var costEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("ASSEMBLE_EQUIPMENT", Assert.IsType<string>(costEvent.Payload["paymentWindow"]));
        var paymentId = Assert.IsType<string>(costEvent.Payload["paymentId"]);
        Assert.StartsWith("ASSEMBLE_EQUIPMENT:", paymentId, StringComparison.Ordinal);
        Assert.Equal(paymentId, Assert.IsType<string>(recycleEvent.Payload["paymentId"]));
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));

        var snapshot = SnapshotFor(attachClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.DoesNotContain(paymentRuneObjectId, Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        Assert.Equal(2, Assert.IsType<int>(p1Zones["runeDeckCount"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var equipment = Assert.IsType<Dictionary<string, object?>>(p1Objects[equipmentObjectId]);
        Assert.Equal(targetObjectId, Assert.IsType<string>(equipment["attachedToObjectId"]));
        var runePool = Assert.IsType<Dictionary<string, object?>>(p1["runePool"]);
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(runePool["powerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Red, powerByTrait.Keys);
        var remainingAssemblePowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Red, remainingAssemblePowerByTrait.Keys);
    }

    [Fact]
    public async Task AssembleEquipmentDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "p7-9-assemble-equipment-raw-idempotency";
        const string equipmentObjectId = "P1-EQUIPMENT-LONG-SWORD-ASSEMBLE-PAYMENT";
        const string targetObjectId = "P1-UNIT-ASSEMBLE-PAYMENT-TARGET";
        const string paymentRuneObjectId = "P1-RUNE-RED-ASSEMBLE-PAYMENT-001";
        var paymentResourceAction = $"RECYCLE_RUNE:{paymentRuneObjectId}";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var development = new TestHostEnvironment(Environments.Development);
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
                development)
            .SeedScenario(roomId, "P1", "assemble-payment-recycle", "seed-p7-9-assemble-equipment-raw-idempotency");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var assembleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(assembleCandidate.Enabled);
        Assert.Equal([equipmentObjectId], (assembleCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .ToArray();
        Assert.Equal([paymentResourceAction], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        var seededJournalCount = journal.Entries.Count;
        var assembleEquipment = JsonSerializer.SerializeToElement(new
        {
            cmdType = "ASSEMBLE_EQUIPMENT",
            sourceObjectId = equipmentObjectId,
            targetObjectId,
            optionalCosts = new[] { "ASSEMBLE_RED", paymentResourceAction }
        });
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "assemble-equipment-same", assembleEquipment);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "EQUIPMENT_ATTACHED"],
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedRecycleEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Equal("ASSEMBLE_EQUIPMENT", acceptedRecycleEvent.Payload["paymentWindow"]);
        var acceptedCostEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("ASSEMBLE_EQUIPMENT", Assert.IsType<string>(acceptedCostEvent.Payload["paymentWindow"]));
        var acceptedPaymentId = Assert.IsType<string>(acceptedCostEvent.Payload["paymentId"]);
        Assert.StartsWith("ASSEMBLE_EQUIPMENT:", acceptedPaymentId, StringComparison.Ordinal);
        Assert.Equal(acceptedPaymentId, Assert.IsType<string>(acceptedRecycleEvent.Payload["paymentId"]));
        var acceptedPaymentResourceActions = Assert.IsType<string[]>(acceptedCostEvent.Payload["paymentResourceActions"]);
        Assert.Equal([paymentResourceAction], acceptedPaymentResourceActions);
        var acceptedAttachEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal(equipmentObjectId, acceptedAttachEvent.Payload["equipmentObjectId"]);
        Assert.Equal(targetObjectId, acceptedAttachEvent.Payload["attachedToObjectId"]);
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedSnapshot = SnapshotFor(acceptedClients, "P1");
        var acceptedP1 = PlayerView(acceptedSnapshot, "P1");
        var acceptedP1Zones = ZoneView(acceptedP1);
        Assert.DoesNotContain(paymentRuneObjectId, StringList(acceptedP1Zones["base"]));
        Assert.Equal(2, Assert.IsType<int>(acceptedP1Zones["runeDeckCount"]));
        var acceptedObjects = Assert.IsType<Dictionary<string, object?>>(acceptedP1["objects"]);
        var acceptedEquipment = Assert.IsType<Dictionary<string, object?>>(acceptedObjects[equipmentObjectId]);
        Assert.Equal(targetObjectId, Assert.IsType<string>(acceptedEquipment["attachedToObjectId"]));
        var acceptedRunePool = Assert.IsType<Dictionary<string, object?>>(acceptedP1["runePool"]);
        var acceptedPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(acceptedRunePool["powerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Red, acceptedPowerByTrait.Keys);
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptActions = acceptedClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(seededJournalCount + 1, acceptedJournalCount);
        var assembleEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "assemble-equipment-same", StringComparison.Ordinal));
        Assert.Equal(roomId, assembleEntry.RoomId);
        Assert.Equal("P1", assembleEntry.PlayerId);
        Assert.Equal("ASSEMBLE_EQUIPMENT", assembleEntry.CommandType);
        Assert.NotNull(assembleEntry.RawCommand);
        var rawCommand = assembleEntry.RawCommand.Value;
        Assert.Equal("ASSEMBLE_EQUIPMENT", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(equipmentObjectId, rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal(targetObjectId, rawCommand.GetProperty("targetObjectId").GetString());
        Assert.Equal(
            ["ASSEMBLE_RED", paymentResourceAction],
            rawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "assemble-equipment-same", assembleEquipment);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("P1", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var replayRecycleEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Equal(acceptedRecycleEvent.Payload["paymentWindow"], replayRecycleEvent.Payload["paymentWindow"]);
        Assert.Equal(acceptedRecycleEvent.Payload["paymentId"], replayRecycleEvent.Payload["paymentId"]);
        var replayCostEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(acceptedCostEvent.Payload["paymentWindow"], replayCostEvent.Payload["paymentWindow"]);
        Assert.Equal(acceptedCostEvent.Payload["paymentId"], replayCostEvent.Payload["paymentId"]);
        Assert.Equal(acceptedPaymentResourceActions, Assert.IsType<string[]>(replayCostEvent.Payload["paymentResourceActions"]));
        var replayAttachEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal(acceptedAttachEvent.Payload["equipmentObjectId"], replayAttachEvent.Payload["equipmentObjectId"]);
        Assert.Equal(acceptedAttachEvent.Payload["attachedToObjectId"], replayAttachEvent.Payload["attachedToObjectId"]);
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        var replayPromptActions = replayClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        Assert.Equal(acceptedPromptActions, replayPromptActions);
        var replaySnapshot = SnapshotFor(replayClients, "P1");
        Assert.Equal(acceptedSnapshot.Tick, replaySnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, replaySnapshot.ActivePlayerId);
        var replayP1 = PlayerView(replaySnapshot, "P1");
        var replayZones = ZoneView(replayP1);
        Assert.DoesNotContain(paymentRuneObjectId, StringList(replayZones["base"]));
        Assert.Equal(acceptedP1Zones["runeDeckCount"], replayZones["runeDeckCount"]);
        var replayObjects = Assert.IsType<Dictionary<string, object?>>(replayP1["objects"]);
        var replayEquipment = Assert.IsType<Dictionary<string, object?>>(replayObjects[equipmentObjectId]);
        Assert.Equal(acceptedEquipment["attachedToObjectId"], replayEquipment["attachedToObjectId"]);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedAssembleEquipment = JsonSerializer.SerializeToElement(new
        {
            cmdType = "ASSEMBLE_EQUIPMENT",
            sourceObjectId = equipmentObjectId,
            targetObjectId,
            optionalCosts = new[] { "ASSEMBLE_RED", paymentResourceAction },
            clientNote = "changed"
        });

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "assemble-equipment-same", changedAssembleEquipment);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, currentSnapshot.ActivePlayerId);
        var currentP1 = PlayerView(currentSnapshot, "P1");
        var currentZones = ZoneView(currentP1);
        Assert.DoesNotContain(paymentRuneObjectId, StringList(currentZones["base"]));
        Assert.Equal(acceptedP1Zones["runeDeckCount"], currentZones["runeDeckCount"]);
        var currentObjects = Assert.IsType<Dictionary<string, object?>>(currentP1["objects"]);
        var currentEquipment = Assert.IsType<Dictionary<string, object?>>(currentObjects[equipmentObjectId]);
        Assert.Equal(acceptedEquipment["attachedToObjectId"], currentEquipment["attachedToObjectId"]);
        var currentRunePool = Assert.IsType<Dictionary<string, object?>>(currentP1["runePool"]);
        var currentPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(currentRunePool["powerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Red, currentPowerByTrait.Keys);
    }

    [Fact]
    public async Task P79AssembleExperienceSeedOffersExperienceCostAndAttachesThroughHub()
    {
        const string roomId = "p7-9-assemble-experience-core";
        const string equipmentObjectId = "P1-EQUIPMENT-SHEPHERDS-HEIRLOOM-ASSEMBLE";
        const string targetObjectId = "P1-UNIT-SHEPHERDS-HEIRLOOM-TARGET";
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
            .SeedScenario(roomId, "P1", "assemble-experience", "seed-p7-9-assemble-experience");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var assembleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(assembleCandidate.Enabled);
        Assert.Equal([equipmentObjectId], (assembleCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("UNL-158/219", sourceRequirement["equipmentCardNo"]);
        Assert.Equal("牧人的传家宝", sourceRequirement["displayName"]);
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["experienceCost"]));
        Assert.Equal(["SPEND_EXPERIENCE:1"], Assert.IsAssignableFrom<IEnumerable<string>>(
            sourceRequirement["requiredOptionalCosts"]).ToArray());

        var attachClients = new RecordingHubClients();
        await CreateHub(attachClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-assemble-experience", JsonSerializer.SerializeToElement(new
            {
                cmdType = "ASSEMBLE_EQUIPMENT",
                sourceObjectId = equipmentObjectId,
                targetObjectId,
                optionalCosts = new[] { "SPEND_EXPERIENCE:1" }
            }));

        Assert.Empty(attachClients.CallerClient.Errors);
        var events = EventsFor(attachClients);
        Assert.Equal(
            ["COST_PAID", "EQUIPMENT_ATTACHED"],
            events.Select(gameEvent => gameEvent.Kind).ToArray());
        var costEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costEvent.Payload["experience"]);

        var snapshot = SnapshotFor(attachClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        Assert.Equal(0, Assert.IsType<int>(p1["experience"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var equipment = Assert.IsType<Dictionary<string, object?>>(p1Objects[equipmentObjectId]);
        Assert.Equal(targetObjectId, Assert.IsType<string>(equipment["attachedToObjectId"]));
    }

    [Fact]
    public async Task P79AssembleDestroyFriendlyUnitSeedOffersCostAndAttachesThroughHub()
    {
        const string roomId = "p7-9-assemble-destroy-friendly-unit-core";
        const string equipmentObjectId = "P1-EQUIPMENT-BLADE-RUINED-KING-ASSEMBLE";
        const string targetObjectId = "P1-UNIT-BLADE-RUINED-KING-TARGET";
        const string costObjectId = "P1-UNIT-BLADE-RUINED-KING-COST";
        var destroyAdditionalCost = $"DESTROY_FRIENDLY_UNIT:{costObjectId}";
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
            .SeedScenario(roomId, "P1", "assemble-destroy-friendly-unit", "seed-p7-9-assemble-destroy-friendly-unit");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var assembleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(assembleCandidate.Enabled);
        Assert.Equal([equipmentObjectId], (assembleCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("SFD·178/221", sourceRequirement["equipmentCardNo"]);
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["requiredAdditionalCostChoiceCount"]));
        Assert.Contains(
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(sourceRequirement["additionalCostChoices"]),
            choice => string.Equals(choice.Id, destroyAdditionalCost, StringComparison.Ordinal));

        var attachClients = new RecordingHubClients();
        await CreateHub(attachClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-assemble-destroy-friendly-unit", JsonSerializer.SerializeToElement(new
            {
                cmdType = "ASSEMBLE_EQUIPMENT",
                sourceObjectId = equipmentObjectId,
                targetObjectId,
                optionalCosts = new[] { "ASSEMBLE_YELLOW", destroyAdditionalCost }
            }));

        Assert.Empty(attachClients.CallerClient.Errors);
        var events = EventsFor(attachClients);
        Assert.Equal(
            ["COST_PAID", "UNIT_DESTROYED", "EQUIPMENT_ATTACHED"],
            events.Select(gameEvent => gameEvent.Kind).ToArray());
        var costEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(
            [costObjectId],
            Assert.IsAssignableFrom<IEnumerable<string>>(
                costEvent.Payload["destroyedAdditionalCostTargetObjectIds"]).ToArray());
        var destroyedEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.Equal("ADDITIONAL_COST", destroyedEvent.Payload["reason"]);

        var snapshot = SnapshotFor(attachClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.DoesNotContain(costObjectId, Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        Assert.Equal([costObjectId], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var equipment = Assert.IsType<Dictionary<string, object?>>(p1Objects[equipmentObjectId]);
        Assert.Equal(targetObjectId, Assert.IsType<string>(equipment["attachedToObjectId"]));
    }

    [Fact]
    public async Task P79AssembleRecycleGraveyardSeedOffersCostAndAttachesThroughHub()
    {
        const string roomId = "p7-9-assemble-recycle-graveyard-core";
        const string equipmentObjectId = "P1-EQUIPMENT-LAST-RITES-ASSEMBLE";
        const string targetObjectId = "P1-UNIT-LAST-RITES-TARGET";
        const string recycleObjectId1 = "P1-LAST-RITES-RECYCLE-001";
        const string recycleObjectId2 = "P1-LAST-RITES-RECYCLE-002";
        var recycleAdditionalCost1 = $"RECYCLE_GRAVEYARD_CARD:{recycleObjectId1}";
        var recycleAdditionalCost2 = $"RECYCLE_GRAVEYARD_CARD:{recycleObjectId2}";
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
            .SeedScenario(roomId, "P1", "assemble-recycle-graveyard", "seed-p7-9-assemble-recycle-graveyard");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var assembleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(assembleCandidate.Enabled);
        Assert.Equal([equipmentObjectId], (assembleCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("SFD·150/221", sourceRequirement["equipmentCardNo"]);
        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["requiredAdditionalCostChoiceCount"]));
        Assert.Equal(
            [recycleAdditionalCost1, recycleAdditionalCost2],
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(sourceRequirement["additionalCostChoices"])
                .Select(choice => choice.Id)
                .ToArray());

        var attachClients = new RecordingHubClients();
        await CreateHub(attachClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-assemble-recycle-graveyard", JsonSerializer.SerializeToElement(new
            {
                cmdType = "ASSEMBLE_EQUIPMENT",
                sourceObjectId = equipmentObjectId,
                targetObjectId,
                optionalCosts = new[] { "ASSEMBLE_PURPLE", recycleAdditionalCost1, recycleAdditionalCost2 }
            }));

        Assert.Empty(attachClients.CallerClient.Errors);
        var events = EventsFor(attachClients);
        Assert.Equal(
            ["COST_PAID", "CARDS_RECYCLED", "EQUIPMENT_ATTACHED"],
            events.Select(gameEvent => gameEvent.Kind).ToArray());
        var costEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(
            [recycleObjectId1, recycleObjectId2],
            Assert.IsAssignableFrom<IEnumerable<string>>(
                    costEvent.Payload["recycledAdditionalCostTargetObjectIds"])
                .Order(StringComparer.Ordinal)
                .ToArray());
        var recycleEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
        Assert.Equal("ADDITIONAL_COST", recycleEvent.Payload["reason"]);
        Assert.Equal(2, recycleEvent.Payload["count"]);

        var snapshot = SnapshotFor(attachClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["graveyard"]));
        Assert.Equal(2, Assert.IsType<int>(p1Zones["mainDeckCount"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var equipment = Assert.IsType<Dictionary<string, object?>>(p1Objects[equipmentObjectId]);
        Assert.Equal(targetObjectId, Assert.IsType<string>(equipment["attachedToObjectId"]));
    }

    [Fact]
    public async Task P79AssembleDynamicManaSeedOffersCostAndAttachesThroughHub()
    {
        const string roomId = "p7-9-assemble-dynamic-mana-core";
        const string equipmentObjectId = "P1-EQUIPMENT-HEXTECH-GAUNTLET-ASSEMBLE";
        const string targetObjectId = "P1-UNIT-HEXTECH-GAUNTLET-TARGET";
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
            .SeedScenario(roomId, "P1", "assemble-dynamic-mana", "seed-p7-9-assemble-dynamic-mana");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var assembleCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(assembleCandidate.Enabled);
        Assert.Equal([equipmentObjectId], (assembleCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("UNL-188/219", sourceRequirement["equipmentCardNo"]);
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["manaCost"]));
        Assert.Equal(3, Assert.IsType<int>(sourceRequirement["baseManaCost"]));
        Assert.Equal(
            [targetObjectId],
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(sourceRequirement["targetChoices"])
                .Select(choice => choice.Id)
                .ToArray());
        Assert.Equal(["ASSEMBLE_3_ANY_POWER"], Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]).Select(choice => choice.Id).ToArray());

        var attachClients = new RecordingHubClients();
        await CreateHub(attachClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-assemble-dynamic-mana", JsonSerializer.SerializeToElement(new
            {
                cmdType = "ASSEMBLE_EQUIPMENT",
                sourceObjectId = equipmentObjectId,
                targetObjectId,
                optionalCosts = new[] { "ASSEMBLE_3_ANY_POWER" }
            }));

        Assert.Empty(attachClients.CallerClient.Errors);
        var events = EventsFor(attachClients);
        Assert.Equal(["COST_PAID", "EQUIPMENT_ATTACHED"], events.Select(gameEvent => gameEvent.Kind).ToArray());
        var costEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costEvent.Payload["mana"]);
        Assert.Equal(3, costEvent.Payload["baseManaCost"]);
        Assert.Equal(2, costEvent.Payload["targetPowerManaReduction"]);
        Assert.Equal(1, costEvent.Payload["power"]);

        var snapshot = SnapshotFor(attachClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var equipment = Assert.IsType<Dictionary<string, object?>>(p1Objects[equipmentObjectId]);
        Assert.Equal(targetObjectId, Assert.IsType<string>(equipment["attachedToObjectId"]));
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
        Assert.Equal(["PLAY_CARD", "PASS_FOCUS", "SURRENDER"], p1Prompt.Actions);
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
        Assert.Equal(["PASS_PRIORITY", "SURRENDER"], p2Prompt.Actions);
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
        Assert.Equal(["PASS_FOCUS", "SURRENDER"], p1Prompt.Actions);

        var p1FocusPassClients = new RecordingHubClients();
        await CreateHub(p1FocusPassClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-battlefield-contest-p1-focus-pass", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PASS_FOCUS"
            }));

        Assert.Empty(p1FocusPassClients.CallerClient.Errors);
        var p2FocusPrompt = PromptFor(p1FocusPassClients, "P2");
        Assert.True(p2FocusPrompt.Actionable);
        Assert.Equal(["PASS_FOCUS", "SURRENDER"], p2FocusPrompt.Actions);

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
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], finalP1Prompt.Actions);
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
    public async Task P6BattlefieldContestSpellDuelCleanupSeedSkipsBattleAfterFocusPass()
    {
        const string roomId = "p6-3d-battlefield-contest-spell-duel-cleanup";
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
            .SeedScenario(roomId, "P1", "battlefield-contest-spell-duel-cleanup", "seed-p6-battlefield-contest-spell-duel-cleanup");

        Assert.Empty(seedClients.CallerClient.Errors);
        var seededP2Prompt = PromptFor(seedClients, "P2");
        Assert.True(seededP2Prompt.Actionable);
        Assert.Equal(["PASS_FOCUS", "SURRENDER"], seededP2Prompt.Actions);
        var seededP2Snapshot = SnapshotFor(seedClients, "P2");
        Assert.Equal("SPELL_DUEL_OPEN", seededP2Snapshot.Timing["timingState"]);
        Assert.Equal("P2", seededP2Snapshot.Timing["focusPlayerId"]);
        var seededTaskQueue = Assert.IsType<Dictionary<string, object?>>(seededP2Snapshot.Timing["pendingTaskQueue"]);
        var seededTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(seededTaskQueue["tasks"]);
        Assert.Contains(
            seededTasks,
            task => string.Equals(task["kind"] as string, "START_BATTLE", StringComparison.Ordinal));

        var passFocusClients = new RecordingHubClients();
        await CreateHub(passFocusClients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p6-battlefield-contest-spell-duel-cleanup-p2-focus-pass", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PASS_FOCUS"
            }));

        Assert.Empty(passFocusClients.CallerClient.Errors);
        var events = EventsFor(passFocusClients);
        Assert.Equal(
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED"],
            events.Select(gameEvent => gameEvent.Kind).ToArray());
        var closedEvent = Assert.Single(
            events,
            gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_CLOSED", StringComparison.Ordinal));
        Assert.Equal(
            ["P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001"],
            Assert.IsType<string[]>(closedEvent.Payload["completedBattlefieldObjectIds"]));

        var p1Snapshot = SnapshotFor(passFocusClients, "P1");
        Assert.Equal("NEUTRAL_OPEN", p1Snapshot.Timing["timingState"]);
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("IDLE", Assert.IsType<string>(taskQueue["phase"]));
        var tasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]);
        Assert.DoesNotContain(
            tasks,
            task => string.Equals(task["kind"] as string, "START_BATTLE", StringComparison.Ordinal));

        var p1Prompt = PromptFor(passFocusClients, "P1");
        Assert.True(p1Prompt.Actionable);
        Assert.DoesNotContain("DECLARE_BATTLE", p1Prompt.Actions);
        Assert.Equal(["MOVE_UNIT", "END_TURN", "SURRENDER"], p1Prompt.Actions);

        var resyncClients = new RecordingHubClients();
        await CreateHub(resyncClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");
        Assert.Empty(resyncClients.CallerClient.Errors);
        var resyncP1Prompt = Assert.IsType<ActionPromptDto>(
            Assert.Single(resyncClients.CallerClient.Prompts, message => string.Equals(message.PlayerId, "P1", StringComparison.Ordinal)).Payload);
        Assert.True(resyncP1Prompt.Actionable);
        var resyncDeclareBattleCandidate = (resyncP1Prompt.Candidates ?? [])
            .FirstOrDefault(candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.True(resyncDeclareBattleCandidate is null || !resyncDeclareBattleCandidate.Enabled);

        var p2 = PlayerView(p1Snapshot, "P2");
        var p2Zones = ZoneView(p2);
        Assert.DoesNotContain(
            "P2-UNIT-SPELL-DUEL-CLEANUP-001",
            StringList(p2Zones["battlefields"]));
        Assert.Contains(
            "P2-UNIT-SPELL-DUEL-CLEANUP-001",
            StringList(p2Zones["graveyard"]));
        var battlefields = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(p1Snapshot.Lanes["battlefields"]);
        var battlefield = Assert.Single(
            battlefields,
            item => string.Equals(item["battlefieldObjectId"] as string, "P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001", StringComparison.Ordinal));
        Assert.False(Assert.IsType<bool>(battlefield["contested"]));
        Assert.Equal("P1", Assert.IsType<string>(battlefield["controllerId"]));
        Assert.Equal(
            ["P1-UNIT-SPELL-DUEL-CLEANUP-001"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(battlefield["occupantObjectIds"]));
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
    public async Task DeclareBattleDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "declare-battle-raw-idempotency";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var development = new TestHostEnvironment(Environments.Development);
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
                development)
            .SeedScenario(roomId, "P1", "battle-declare", "seed-declare-battle-raw-idempotency");

        Assert.Empty(seedClients.CallerClient.Errors);
        var seededJournalCount = journal.Entries.Count;
        var declareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLE-ATTACKER-001"],
              "defenderObjectIds": ["P2-BATTLE-DEFENDER-001"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "declare-battle-same", declareBattle);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Equal(2, acceptedEvents.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Contains(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedSnapshot = SnapshotFor(acceptedClients, "P1");
        Assert.Empty(acceptedSnapshot.Stack);
        var acceptedP1Zones = ZoneView(PlayerView(acceptedSnapshot, "P1"));
        var acceptedP1Battlefields = StringList(acceptedP1Zones["battlefields"]).ToArray();
        Assert.Contains("P1-BATTLE-ATTACKER-001", acceptedP1Battlefields);
        var acceptedP2Zones = ZoneView(PlayerView(acceptedSnapshot, "P2"));
        var acceptedP2Battlefields = StringList(acceptedP2Zones["battlefields"]).ToArray();
        var acceptedP2Graveyard = StringList(acceptedP2Zones["graveyard"]).ToArray();
        Assert.DoesNotContain("P2-BATTLE-DEFENDER-001", acceptedP2Battlefields);
        Assert.Contains("P2-BATTLE-DEFENDER-001", acceptedP2Graveyard);
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(seededJournalCount + 1, acceptedJournalCount);
        var declareBattleEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "declare-battle-same", StringComparison.Ordinal));
        Assert.Equal(roomId, declareBattleEntry.RoomId);
        Assert.Equal("P1", declareBattleEntry.PlayerId);
        Assert.Equal("DECLARE_BATTLE", declareBattleEntry.CommandType);
        Assert.NotNull(declareBattleEntry.RawCommand);
        var rawCommand = declareBattleEntry.RawCommand.Value;
        Assert.Equal("DECLARE_BATTLE", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("BATTLEFIELD:P1-MAIN", rawCommand.GetProperty("battlefieldId").GetString());
        Assert.Equal(
            ["P1-BATTLE-ATTACKER-001"],
            rawCommand.GetProperty("attackerObjectIds")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.Equal(
            ["P2-BATTLE-DEFENDER-001"],
            rawCommand.GetProperty("defenderObjectIds")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.Equal(
            ["COMBAT_ASSIGNMENT"],
            rawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "declare-battle-same", declareBattle);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("P1", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        var replaySnapshot = SnapshotFor(replayClients, "P1");
        Assert.Equal(acceptedSnapshot.Tick, replaySnapshot.Tick);
        Assert.Empty(replaySnapshot.Stack);
        Assert.Equal(
            acceptedP1Battlefields,
            StringList(ZoneView(PlayerView(replaySnapshot, "P1"))["battlefields"]).ToArray());
        Assert.Equal(
            acceptedP2Battlefields,
            StringList(ZoneView(PlayerView(replaySnapshot, "P2"))["battlefields"]).ToArray());
        Assert.Equal(
            acceptedP2Graveyard,
            StringList(ZoneView(PlayerView(replaySnapshot, "P2"))["graveyard"]).ToArray());
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedDeclareBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLE-ATTACKER-001"],
              "defenderObjectIds": ["P2-BATTLE-DEFENDER-001"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"],
              "clientNote": "changed"
            }
            """).RootElement.Clone();

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "declare-battle-same", changedDeclareBattle);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Empty(currentSnapshot.Stack);
        Assert.Equal(
            acceptedP1Battlefields,
            StringList(ZoneView(PlayerView(currentSnapshot, "P1"))["battlefields"]).ToArray());
        Assert.Equal(
            acceptedP2Battlefields,
            StringList(ZoneView(PlayerView(currentSnapshot, "P2"))["battlefields"]).ToArray());
        Assert.Equal(
            acceptedP2Graveyard,
            StringList(ZoneView(PlayerView(currentSnapshot, "P2"))["graveyard"]).ToArray());
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
        Assert.Equal("battlefield-zone-controlled-ready-face-up-units-not-already-in-combat", metadata["candidateFiltering"]);

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

        var invalidTargetClients = new RecordingHubClients();
        var invalidTargetBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-BLACK-FLAME",
              "attackerObjectIds": ["P1-BATTLEFIELD-EPHEMERAL-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-EPHEMERAL-DEFENDER"],
              "battlefieldTargetObjectIds": ["P2-BATTLEFIELD-EPHEMERAL-DEFENDER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(invalidTargetClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-ephemeral-invalid-target", invalidTargetBattle);

        var invalidTargetError = Assert.Single(invalidTargetClients.CallerClient.Errors);
        var invalidTargetPayload = Assert.IsType<ErrorDto>(invalidTargetError.Payload);
        Assert.Equal(ErrorCodes.InvalidTarget, invalidTargetPayload.Code);
        Assert.Equal("只有需要选择战场效果目标时才能提交战场目标。", invalidTargetPayload.Message);
        Assert.DoesNotContain("Battlefield target choices", invalidTargetPayload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("DECLARE_BATTLE", invalidTargetPayload.Message, StringComparison.Ordinal);

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

        var invalidTargetClients = new RecordingHubClients();
        var invalidTargetBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-FORTIFIED-POSITION",
              "attackerObjectIds": ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-FORTIFIED-DEFENDER"],
              "battlefieldTargetObjectIds": ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(invalidTargetClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-defender-steadfast-invalid-target", invalidTargetBattle);

        var invalidTargetError = Assert.Single(invalidTargetClients.CallerClient.Errors);
        var invalidTargetPayload = Assert.IsType<ErrorDto>(invalidTargetError.Payload);
        Assert.Equal(ErrorCodes.InvalidTarget, invalidTargetPayload.Code);
        Assert.Equal("该战场效果需要且只能选择 1 个防守单位。", invalidTargetPayload.Message);
        Assert.DoesNotContain("Fortified Position", invalidTargetPayload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("DECLARE_BATTLE", invalidTargetPayload.Message, StringComparison.Ordinal);

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

        var invalidTargetClients = new RecordingHubClients();
        var invalidTargetBattle = JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "P2-BATTLEFIELD-PLUNDER-ALLEY",
              "attackerObjectIds": ["P1-BATTLEFIELD-PLUNDER-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-PLUNDER-DEFENDER"],
              "battlefieldTargetObjectIds": ["P2-BATTLEFIELD-PLUNDER-DEFENDER", "P1-BATTLEFIELD-PLUNDER-ATTACKER"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement.Clone();
        await CreateHub(invalidTargetClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-defend-move-invalid-target", invalidTargetBattle);

        var invalidTargetError = Assert.Single(invalidTargetClients.CallerClient.Errors);
        var invalidTargetPayload = Assert.IsType<ErrorDto>(invalidTargetError.Payload);
        Assert.Equal(ErrorCodes.InvalidTarget, invalidTargetPayload.Code);
        Assert.Equal("该战场效果最多选择 1 个防守单位。", invalidTargetPayload.Message);
        Assert.DoesNotContain("Plunder Ship Alley", invalidTargetPayload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("DECLARE_BATTLE", invalidTargetPayload.Message, StringComparison.Ordinal);

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
        var damageRemovedEvent = Assert.Single(EventsFor(battleClients), gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_REMOVED", StringComparison.Ordinal));
        Assert.Equal(["P1-BATTLEFIELD-ISOLATED-ATTACKER"], Assert.IsType<string[]>(damageRemovedEvent.Payload["objectIds"]));
        var previousDamageByObject = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(damageRemovedEvent.Payload["previousDamageByObject"]);
        Assert.Equal(2, previousDamageByObject["P1-BATTLEFIELD-ISOLATED-ATTACKER"]);
        Assert.Equal(2, damageRemovedEvent.Payload["totalDamageRemoved"]);
        Assert.Equal("BATTLE_CLEANUP", damageRemovedEvent.Payload["reason"]);

        var battleSnapshot = SnapshotFor(battleClients, "P1");
        var p1 = Assert.IsType<Dictionary<string, object?>>(battleSnapshot.Players["P1"]);
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var attacker = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-BATTLEFIELD-ISOLATED-ATTACKER"]);
        Assert.Equal(0, Assert.IsType<int>(attacker["damage"]));
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
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));
        Assert.DoesNotContain(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));

        var payPrompt = PromptFor(battleClients, "P1");
        Assert.True(payPrompt.Actionable);
        Assert.Equal(PromptTypes.PayCost, payPrompt.View?.Type);
        var payCandidate = Assert.Single(
            payPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(payCandidate.Metadata);
        var paymentId = Assert.IsType<string>(metadata["paymentId"]);
        var paymentWindow = Assert.IsType<string>(metadata["paymentWindow"]);
        Assert.Equal("TRIGGER_PAYMENT", paymentWindow);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentChoices"]).ToArray();
        Assert.Contains(choices, choice => string.Equals(choice.Id, "SPEND_MANA:1", StringComparison.Ordinal));
        Assert.Contains(choices, choice => string.Equals(choice.Id, "DECLINE", StringComparison.Ordinal));

        var payClients = new RecordingHubClients();
        await CreateHub(payClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-powerful-draw-pay", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PAY_COST",
                paymentId,
                paymentWindow,
                paymentChoiceIds = new[] { "SPEND_MANA:1" },
                promptId = payPrompt.PromptId,
                snapshotTick = payPrompt.SnapshotTick
            }));

        Assert.Empty(payClients.CallerClient.Errors);
        var payEvents = EventsFor(payClients);
        var triggerEvent = Assert.Single(payEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-POWERFUL-ATTACKER", triggerEvent.Payload["powerfulObjectId"]);
        Assert.Contains(payEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));

        var battleSnapshot = SnapshotFor(payClients, "P1");
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
        var battleEvents = EventsFor(battleClients);
        Assert.Contains(battleEvents, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD", StringComparison.Ordinal));
        Assert.DoesNotContain(battleEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD", StringComparison.Ordinal));

        var payPrompt = PromptFor(battleClients, "P1");
        Assert.True(payPrompt.Actionable);
        Assert.Equal(PromptTypes.PayCost, payPrompt.View?.Type);
        var payCandidate = Assert.Single(
            payPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(payCandidate.Metadata);
        var paymentId = Assert.IsType<string>(metadata["paymentId"]);
        var paymentWindow = Assert.IsType<string>(metadata["paymentWindow"]);
        Assert.Equal("TRIGGER_PAYMENT", paymentWindow);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentChoices"]).ToArray();
        Assert.Contains(choices, choice => string.Equals(choice.Id, "SPEND_MANA:1", StringComparison.Ordinal));
        Assert.Contains(choices, choice => string.Equals(choice.Id, "DECLINE", StringComparison.Ordinal));

        var staleClients = new RecordingHubClients();
        await CreateHub(staleClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-gold-stale-pay", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PAY_COST",
                paymentId,
                paymentWindow,
                paymentChoiceIds = new[] { "SPEND_MANA:1" },
                promptId = $"{payPrompt.PromptId}:stale",
                snapshotTick = payPrompt.SnapshotTick
            }));
        var staleError = Assert.Single(staleClients.CallerClient.Errors);
        Assert.Equal(ErrorCodes.PromptExpired, Assert.IsType<ErrorDto>(staleError.Payload).Code);
        Assert.Empty(staleClients.GroupClient.EventMessages);
        Assert.Empty(staleClients.GroupClient.Snapshots);

        var payClients = new RecordingHubClients();
        await CreateHub(payClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-gold-pay", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PAY_COST",
                paymentId,
                paymentWindow,
                paymentChoiceIds = new[] { "SPEND_MANA:1" },
                promptId = payPrompt.PromptId,
                snapshotTick = payPrompt.SnapshotTick
            }));

        Assert.Empty(payClients.CallerClient.Errors);
        var payEvents = EventsFor(payClients);
        Assert.Contains(payEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD", StringComparison.Ordinal));
        var tokenEvent = Assert.Single(payEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD", StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);

        var paidSnapshot = SnapshotFor(payClients, "P1");
        Assert.Null(paidSnapshot.Timing["pendingPayment"]);
        var p1 = Assert.IsType<Dictionary<string, object?>>(paidSnapshot.Players["P1"]);
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
    public async Task MoveUnitDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "p7-9-move-unit-raw-idempotency";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var development = new TestHostEnvironment(Environments.Development);
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
                development)
            .SeedScenario(roomId, "P1", "battlefield-static-roam", "seed-p7-9-move-unit-raw-idempotency");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var moveCandidate = Assert.Single(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.Contains(moveCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-WIND-RUNNER", StringComparison.Ordinal));
        Assert.Contains(moveCandidate.OptionalCosts ?? [], choice => string.Equals(choice.Id, "ROAM", StringComparison.Ordinal));
        var seededJournalCount = journal.Entries.Count;
        var move = JsonDocument.Parse("""
            {
              "cmdType": "MOVE_UNIT",
              "sourceObjectId": "P1-BATTLEFIELD-WIND-RUNNER",
              "origin": "BATTLEFIELD:P1-WIND-HILL",
              "destination": "BATTLEFIELD:P1-FAR-FIELD",
              "optionalCosts": ["ROAM"]
            }
            """).RootElement.Clone();
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "move-unit-same", move);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        var acceptedMoveEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Equal("游走", acceptedMoveEvent.Payload["movementKeyword"]);
        Assert.Equal("BATTLEFIELD:P1-WIND-HILL", acceptedMoveEvent.Payload["origin"]);
        Assert.Equal("BATTLEFIELD:P1-FAR-FIELD", acceptedMoveEvent.Payload["destination"]);
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedSnapshot = SnapshotFor(acceptedClients, "P1");
        var acceptedP1Zones = ZoneView(PlayerView(acceptedSnapshot, "P1"));
        var acceptedBattlefields = StringList(acceptedP1Zones["battlefields"]).ToArray();
        Assert.Equal(["P1-BATTLEFIELD-WIND-HILL", "P1-BATTLEFIELD-WIND-RUNNER"], acceptedBattlefields);
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(seededJournalCount + 1, acceptedJournalCount);
        var moveEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "move-unit-same", StringComparison.Ordinal));
        Assert.Equal(roomId, moveEntry.RoomId);
        Assert.Equal("P1", moveEntry.PlayerId);
        Assert.Equal("MOVE_UNIT", moveEntry.CommandType);
        Assert.NotNull(moveEntry.RawCommand);
        var rawCommand = moveEntry.RawCommand.Value;
        Assert.Equal("MOVE_UNIT", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("P1-BATTLEFIELD-WIND-RUNNER", rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal("BATTLEFIELD:P1-WIND-HILL", rawCommand.GetProperty("origin").GetString());
        Assert.Equal("BATTLEFIELD:P1-FAR-FIELD", rawCommand.GetProperty("destination").GetString());
        Assert.Equal(
            ["ROAM"],
            rawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "move-unit-same", move);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("P1", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var replayMoveEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Equal(acceptedMoveEvent.Payload["movementKeyword"], replayMoveEvent.Payload["movementKeyword"]);
        Assert.Equal(acceptedMoveEvent.Payload["origin"], replayMoveEvent.Payload["origin"]);
        Assert.Equal(acceptedMoveEvent.Payload["destination"], replayMoveEvent.Payload["destination"]);
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        var replaySnapshot = SnapshotFor(replayClients, "P1");
        Assert.Equal(acceptedSnapshot.Tick, replaySnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, replaySnapshot.ActivePlayerId);
        Assert.Equal(
            acceptedBattlefields,
            StringList(ZoneView(PlayerView(replaySnapshot, "P1"))["battlefields"]).ToArray());
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedMove = JsonDocument.Parse("""
            {
              "cmdType": "MOVE_UNIT",
              "sourceObjectId": "P1-BATTLEFIELD-WIND-RUNNER",
              "origin": "BATTLEFIELD:P1-WIND-HILL",
              "destination": "BATTLEFIELD:P1-FAR-FIELD",
              "optionalCosts": ["ROAM"],
              "clientNote": "changed"
            }
            """).RootElement.Clone();

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "move-unit-same", changedMove);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, currentSnapshot.ActivePlayerId);
        Assert.Equal(
            acceptedBattlefields,
            StringList(ZoneView(PlayerView(currentSnapshot, "P1"))["battlefields"]).ToArray());
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
            "该战场效果禁止单位从此战场移动回基地。",
            payload.Message);
        Assert.DoesNotContain("MOVE_UNIT", payload.Message, StringComparison.Ordinal);
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

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.DoesNotContain(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));

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
        Assert.Equal("战场效果禁止将单位打出到该战场。", payload.Message);
        Assert.DoesNotContain("PLAY_CARD", payload.Message, StringComparison.Ordinal);
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
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-static-echo-cost-reduction", "seed-p7-9-battlefield-static-echo-cost-reduction");

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
        var echoChoice = Assert.Single(
            optionalCostChoices,
            choice => string.Equals(choice.Id, "ECHO", StringComparison.Ordinal));
        Assert.Equal("回响：额外支付 1 法力", echoChoice.Label);
        Assert.Equal("战场效果已减免 1 法力", echoChoice.Reason);

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
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(
                roomId,
                "P1",
                "battlefield-static-equipment-cost-reduction",
                "seed-p7-9-battlefield-static-equipment-cost-reduction");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("P1-EQUIPMENT-LONG-SWORD", sourceRequirement["sourceObjectId"]);
        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["manaCost"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["minimumManaCost"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["battlefieldEquipmentCostReductionMana"]));

        var playClients = new RecordingHubClients();
        var longSword = JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-EQUIPMENT-LONG-SWORD",
              "cardNo": "SFD·022/221",
              "targetObjectIds": ["P1-UNIT-EQUIPMENT-COST-TARGET"]
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
    public async Task ActivateAbilityDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "p7-9-activate-ability-raw-idempotency";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var development = new TestHostEnvironment(Environments.Development);
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
                development)
            .SeedScenario(roomId, "P1", "battlefield-unit-experience-ability", "seed-p7-9-activate-ability-raw-idempotency");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var abilityCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        Assert.True(abilityCandidate.Enabled);
        Assert.Contains(abilityCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-EXPERIENCE-UNIT", StringComparison.Ordinal));
        Assert.Contains(abilityCandidate.Modes ?? [], choice => string.Equals(choice.Id, "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE", StringComparison.Ordinal));
        var seededJournalCount = journal.Entries.Count;
        var activateAbility = JsonDocument.Parse("""
            {
              "cmdType": "ACTIVATE_ABILITY",
              "sourceObjectId": "P1-BATTLEFIELD-EXPERIENCE-UNIT",
              "abilityId": "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
              "targetObjectIds": []
            }
            """).RootElement.Clone();
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "activate-ability-same", activateAbility);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        var acceptedAbilityEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal("P1", acceptedAbilityEvent.Payload["playerId"]);
        Assert.Equal("P1-BATTLEFIELD-EXPERIENCE-UNIT", acceptedAbilityEvent.Payload["sourceObjectId"]);
        Assert.Equal("BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE", acceptedAbilityEvent.Payload["abilityId"]);
        var acceptedTriggerEvent = Assert.Single(acceptedEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-EXPERIENCE-UNIT", acceptedTriggerEvent.Payload["sourceObjectId"]);
        var acceptedExperienceEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Equal(1, acceptedExperienceEvent.Payload["amount"]);
        Assert.Equal(1, acceptedExperienceEvent.Payload["totalExperience"]);
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedSnapshot = SnapshotFor(acceptedClients, "P1");
        var acceptedP1 = PlayerView(acceptedSnapshot, "P1");
        Assert.Equal(1, Assert.IsType<int>(acceptedP1["experience"]));
        var acceptedObjects = Assert.IsType<Dictionary<string, object?>>(acceptedP1["objects"]);
        var acceptedUnit = Assert.IsType<Dictionary<string, object?>>(acceptedObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"]);
        Assert.True(Assert.IsType<bool>(acceptedUnit["isExhausted"]));
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptActions = acceptedClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(seededJournalCount + 1, acceptedJournalCount);
        var activateEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "activate-ability-same", StringComparison.Ordinal));
        Assert.Equal(roomId, activateEntry.RoomId);
        Assert.Equal("P1", activateEntry.PlayerId);
        Assert.Equal("ACTIVATE_ABILITY", activateEntry.CommandType);
        Assert.NotNull(activateEntry.RawCommand);
        var rawCommand = activateEntry.RawCommand.Value;
        Assert.Equal("ACTIVATE_ABILITY", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("P1-BATTLEFIELD-EXPERIENCE-UNIT", rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal("BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE", rawCommand.GetProperty("abilityId").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "activate-ability-same", activateAbility);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("P1", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var replayAbilityEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal(acceptedAbilityEvent.Payload["sourceObjectId"], replayAbilityEvent.Payload["sourceObjectId"]);
        Assert.Equal(acceptedAbilityEvent.Payload["abilityId"], replayAbilityEvent.Payload["abilityId"]);
        var replayExperienceEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal));
        Assert.Equal(acceptedExperienceEvent.Payload["amount"], replayExperienceEvent.Payload["amount"]);
        Assert.Equal(acceptedExperienceEvent.Payload["totalExperience"], replayExperienceEvent.Payload["totalExperience"]);
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        var replayPromptActions = replayClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        Assert.Equal(acceptedPromptActions, replayPromptActions);
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        var replaySnapshot = SnapshotFor(replayClients, "P1");
        Assert.Equal(acceptedSnapshot.Tick, replaySnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, replaySnapshot.ActivePlayerId);
        var replayP1 = PlayerView(replaySnapshot, "P1");
        Assert.Equal(acceptedP1["experience"], replayP1["experience"]);
        var replayObjects = Assert.IsType<Dictionary<string, object?>>(replayP1["objects"]);
        var replayUnit = Assert.IsType<Dictionary<string, object?>>(replayObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"]);
        Assert.Equal(acceptedUnit["isExhausted"], replayUnit["isExhausted"]);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedActivateAbility = JsonDocument.Parse("""
            {
              "cmdType": "ACTIVATE_ABILITY",
              "sourceObjectId": "P1-BATTLEFIELD-EXPERIENCE-UNIT",
              "abilityId": "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
              "targetObjectIds": [],
              "clientNote": "changed"
            }
            """).RootElement.Clone();

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "activate-ability-same", changedActivateAbility);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, currentSnapshot.ActivePlayerId);
        var currentP1 = PlayerView(currentSnapshot, "P1");
        Assert.Equal(1, Assert.IsType<int>(currentP1["experience"]));
        var currentObjects = Assert.IsType<Dictionary<string, object?>>(currentP1["objects"]);
        var currentUnit = Assert.IsType<Dictionary<string, object?>>(currentObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"]);
        Assert.True(Assert.IsType<bool>(currentUnit["isExhausted"]));
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
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P1", "battlefield-held-unit-cost-increase", "seed-p7-9-battlefield-held-unit-cost-increase");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("P1-UNIT-CRAFTSMAN", sourceRequirement["sourceObjectId"]);
        Assert.Equal(3, Assert.IsType<int>(sourceRequirement["manaCost"]));
        Assert.Equal(4, Assert.IsType<int>(sourceRequirement["minimumManaCost"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["battlefieldHeldUnitCostIncreaseMana"]));

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
    public async Task P79BattlefieldHeldNextSpellEchoPromptOffersGrantedEchoAndRepeatsThroughHub()
    {
        const string roomId = "p7-9-battlefield-held-next-spell-echo-prompt";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, "P1");
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var seedClients = new RecordingHubClients();
        await CreateHub(
                seedClients,
                new RecordingGroupManager(),
                "connection-2",
                registry,
                new TestHostEnvironment(Environments.Development))
            .SeedScenario(roomId, "P2", "battlefield-held-next-spell-echo-prompt", "seed-p7-9-battlefield-held-next-spell-echo-prompt");

        var p2Prompt = PromptFor(seedClients, "P2");
        var playCandidate = Assert.Single(
            p2Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("P2-SPELL-CENTER-STAGE", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();
        var echoChoice = Assert.Single(
            optionalCostChoices,
            choice => string.Equals(choice.Id, "ECHO", StringComparison.Ordinal));
        Assert.Equal("回响：额外支付 2 法力", echoChoice.Label);
        Assert.Equal("战场效果授予此法术回响", echoChoice.Reason);

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-battlefield-held-next-spell-echo-play", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P2-SPELL-CENTER-STAGE",
                cardNo = "UNL-061/219",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = new[] { "ECHO" }
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        var costPaid = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(4, costPaid.Payload["mana"]);
        Assert.Equal(2, costPaid.Payload["baseMana"]);
        Assert.Contains(playEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["effectRepeatCount"], 2));
        Assert.Contains(playEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["effectRepeatCount"], 2));

        var passP2Clients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-battlefield-held-next-spell-echo-p2-pass", passPriority);
        Assert.Empty(passP2Clients.CallerClient.Errors);

        var passP1Clients = new RecordingHubClients();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-battlefield-held-next-spell-echo-p1-pass", passPriority);
        Assert.Empty(passP1Clients.CallerClient.Errors);
        var resolveEvents = EventsFor(passP1Clients);
        var drawEvent = Assert.Single(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(2, drawEvent.Payload["count"]);

        var finalSnapshot = SnapshotFor(passP1Clients, "P2");
        Assert.Empty(finalSnapshot.Stack);
        var p2 = Assert.IsType<Dictionary<string, object?>>(finalSnapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        Assert.Equal(["P2-DRAW-001", "P2-DRAW-002"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["hand"]));
        Assert.Equal(["P2-SPELL-CENTER-STAGE"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["graveyard"]));
        var runePool = Assert.IsType<Dictionary<string, object?>>(p2["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(runePool["mana"]));
    }

    [Fact]
    public async Task P79RagingDrakeNextSpellCostReductionPromptOffersReducedSpellThroughHub()
    {
        const string roomId = "p7-9-raging-drake-next-spell-cost-reduction-prompt";
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
            .SeedScenario(
                roomId,
                "P1",
                "raging-drake-next-spell-cost-reduction-prompt",
                "seed-p7-9-raging-drake-next-spell-cost-reduction-prompt");

        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("P1-SPELL-WELL-TRAINED", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal(0, sourceRequirement["minimumManaCost"]);
        Assert.Equal(2, sourceRequirement["nextSpellCostReductionMana"]);

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-raging-drake-next-spell-cost-reduction-play", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-SPELL-WELL-TRAINED",
                cardNo = "OGN·058/298",
                targetObjectIds = new[] { "P2-UNIT-001" },
                optionalCosts = Array.Empty<string>()
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var playEvents = EventsFor(playClients);
        var costPaid = Assert.Single(playEvents, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(0, costPaid.Payload["mana"]);
        Assert.Equal(2, costPaid.Payload["baseMana"]);
        Assert.Equal(2, costPaid.Payload["nextSpellCostReductionMana"]);
        Assert.Contains(playEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79RoyalAttendantSeedOffersLegendModesAndReadiesTarget()
    {
        const string roomId = "p7-9-royal-attendant-legend-mode";
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
            .SeedScenario(roomId, "P1", "royal-attendant-legend-mode", "seed-p7-9-royal-attendant-legend-mode");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        Assert.Equal(["EXHAUST_LEGEND", "READY_LEGEND"], sourceRequirements
            .Select(requirement => Assert.IsType<string>(requirement["mode"]))
            .Order(StringComparer.Ordinal)
            .ToArray());
        foreach (var requirement in sourceRequirements)
        {
            Assert.Equal("P1-UNIT-ROYAL-ATTENDANT", Assert.IsType<string>(requirement["sourceObjectId"]));
            Assert.Equal("传奇", Assert.IsType<string>(requirement["targetScopeLabel"]));
            Assert.Equal(1, Assert.IsType<int>(requirement["minTargetCount"]));
            Assert.Equal(1, Assert.IsType<int>(requirement["maxTargetCount"]));
            var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
                requirement["targetChoicesByIndex"]);
            var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
                .Select(choice => choice.Id)
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(["P1-LEGEND-ROYAL-TARGET", "P2-LEGEND-ROYAL-TARGET"], choices);
        }

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-royal-attendant-play", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-UNIT-ROYAL-ATTENDANT",
                cardNo = "SFD·039/221",
                targetObjectIds = new[] { "P1-LEGEND-ROYAL-TARGET" },
                mode = "READY_LEGEND",
                optionalCosts = Array.Empty<string>()
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        var passP1Clients = new RecordingHubClients();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-royal-attendant-p1-pass", passPriority);
        Assert.Empty(passP1Clients.CallerClient.Errors);

        var passP2Clients = new RecordingHubClients();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-royal-attendant-p2-pass", passPriority);
        Assert.Empty(passP2Clients.CallerClient.Errors);
        var resolveEvents = EventsFor(passP2Clients);
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        var readiedEvent = Assert.Single(resolveEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-LEGEND-ROYAL-TARGET", StringComparison.Ordinal));
        Assert.Equal(true, readiedEvent.Payload["wasExhausted"]);
        Assert.Equal(false, readiedEvent.Payload["isExhausted"]);

        var finalSnapshot = SnapshotFor(passP2Clients, "P1");
        Assert.Empty(finalSnapshot.Stack);
        var p1 = Assert.IsType<Dictionary<string, object?>>(finalSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-UNIT-ROYAL-ATTENDANT"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        var p1Objects = Assert.IsType<Dictionary<string, object?>>(p1["objects"]);
        var legendObject = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-LEGEND-ROYAL-TARGET"]);
        Assert.Equal(false, legendObject["isExhausted"]);
    }

    [Fact]
    public async Task P79OrnnSeedOffersTopEquipmentAndDrawsSelection()
    {
        const string roomId = "p7-9-ornn-equipment-look";
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
            .SeedScenario(roomId, "P1", "ornn-equipment-look", "seed-p7-9-ornn-equipment-look");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("P1-UNIT-SFD-058-ORNN", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("己方主牌堆牌", Assert.IsType<string>(sourceRequirement["targetScopeLabel"]));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["P1-ORNN-EQUIPMENT-001", "P1-ORNN-EQUIPMENT-002"], choices);

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-ornn-equipment-look-play", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-UNIT-SFD-058-ORNN",
                cardNo = "SFD·058/221",
                targetObjectIds = new[] { "P1-ORNN-EQUIPMENT-001" },
                optionalCosts = Array.Empty<string>()
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        var passP1Clients = new RecordingHubClients();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-ornn-equipment-look-p1-pass", passPriority);
        Assert.Empty(passP1Clients.CallerClient.Errors);

        var passP2Clients = new RecordingHubClients();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-ornn-equipment-look-p2-pass", passPriority);
        Assert.Empty(passP2Clients.CallerClient.Errors);
        var resolveEvents = EventsFor(passP2Clients);
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 1));
        Assert.Contains(resolveEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 3));

        var finalSnapshot = SnapshotFor(passP2Clients, "P1");
        Assert.Empty(finalSnapshot.Stack);
        var p1 = Assert.IsType<Dictionary<string, object?>>(finalSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-UNIT-SFD-058-ORNN"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        Assert.Contains("P1-ORNN-EQUIPMENT-001", Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
    }

    [Fact]
    public async Task P79OrnnSeedCanDeclineEquipmentAndRecycleViewedCards()
    {
        const string roomId = "p7-9-ornn-equipment-look-decline";
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
            .SeedScenario(roomId, "P1", "ornn-equipment-look", "seed-p7-9-ornn-equipment-look-decline");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["minTargetCount"]));

        var playClients = new RecordingHubClients();
        await CreateHub(playClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-ornn-equipment-look-decline-play", JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-UNIT-SFD-058-ORNN",
                cardNo = "SFD·058/221",
                targetObjectIds = Array.Empty<string>(),
                optionalCosts = Array.Empty<string>()
            }));

        Assert.Empty(playClients.CallerClient.Errors);
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        var passP1Clients = new RecordingHubClients();
        await CreateHub(passP1Clients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-ornn-equipment-look-decline-p1-pass", passPriority);
        Assert.Empty(passP1Clients.CallerClient.Errors);

        var passP2Clients = new RecordingHubClients();
        await CreateHub(passP2Clients, new RecordingGroupManager(), "connection-2", registry)
            .SubmitIntent(roomId, "P2", "intent-p7-9-ornn-equipment-look-decline-p2-pass", passPriority);
        Assert.Empty(passP2Clients.CallerClient.Errors);
        var resolveEvents = EventsFor(passP2Clients);
        Assert.Contains(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        Assert.DoesNotContain(resolveEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Contains(resolveEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 4));

        var finalSnapshot = SnapshotFor(passP2Clients, "P1");
        Assert.Empty(finalSnapshot.Stack);
        var p1 = Assert.IsType<Dictionary<string, object?>>(finalSnapshot.Players["P1"]);
        var p1Zones = Assert.IsType<Dictionary<string, object?>>(p1["zones"]);
        Assert.Equal(["P1-UNIT-SFD-058-ORNN"], Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["base"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(p1Zones["hand"]));
        Assert.Equal(5, Assert.IsType<int>(p1Zones["mainDeckCount"]));
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
    public async Task HideCardDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "p7-9-hide-card-raw-idempotency";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var development = new TestHostEnvironment(Environments.Development);
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
                development)
            .SeedScenario(roomId, "P1", "battlefield-extra-standby", "seed-p7-9-hide-card-raw-idempotency");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var hideCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "HIDE_CARD", StringComparison.Ordinal));
        Assert.True(hideCandidate.Enabled);
        Assert.Contains(hideCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-STANDBY-BANDLE-TEEMO", StringComparison.Ordinal));
        Assert.Contains(hideCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE", StringComparison.Ordinal));
        Assert.Contains(hideCandidate.OptionalCosts ?? [], choice => string.Equals(choice.Id, "STANDBY_A", StringComparison.Ordinal));
        var seededJournalCount = journal.Entries.Count;
        var hideCard = JsonDocument.Parse("""
            {
              "cmdType": "HIDE_CARD",
              "sourceObjectId": "P1-STANDBY-BANDLE-TEEMO",
              "cardNo": "OGN·121/298",
              "destination": "BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE",
              "optionalCosts": ["STANDBY_A"]
            }
            """).RootElement.Clone();
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "hide-card-same", hideCard);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        var acceptedTriggerEvent = Assert.Single(acceptedEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_EXTRA_STANDBY_ARRANGED", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-BANDLE-TREE", acceptedTriggerEvent.Payload["battlefieldObjectId"]);
        var acceptedHideEvent = Assert.Single(acceptedEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD", acceptedHideEvent.Payload["destinationZone"]);
        Assert.Equal("P1-BATTLEFIELD-BANDLE-TREE", acceptedHideEvent.Payload["battlefieldObjectId"]);
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedSnapshot = SnapshotFor(acceptedClients, "P1");
        var acceptedP1 = PlayerView(acceptedSnapshot, "P1");
        var acceptedP1Zones = ZoneView(acceptedP1);
        var acceptedBattlefields = StringList(acceptedP1Zones["battlefields"]).ToArray();
        Assert.Empty(StringList(acceptedP1Zones["hand"]));
        Assert.Equal(["P1-BATTLEFIELD-BANDLE-TREE", "P1-STANDBY-BANDLE-TEEMO"], acceptedBattlefields);
        var acceptedObjects = Assert.IsType<Dictionary<string, object?>>(acceptedP1["objects"]);
        var acceptedHiddenObject = Assert.IsType<Dictionary<string, object?>>(acceptedObjects["P1-STANDBY-BANDLE-TEEMO"]);
        Assert.True(Assert.IsType<bool>(acceptedHiddenObject["isFaceDown"]));
        var acceptedRunePool = Assert.IsType<Dictionary<string, object?>>(acceptedP1["runePool"]);
        Assert.Equal(0, Assert.IsType<int>(acceptedRunePool["mana"]));
        Assert.Null(acceptedSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.InProgress, acceptedSnapshot.Timing["roomStatus"]);
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptActions = acceptedClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(seededJournalCount + 1, acceptedJournalCount);
        var hideEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "hide-card-same", StringComparison.Ordinal));
        Assert.Equal(roomId, hideEntry.RoomId);
        Assert.Equal("P1", hideEntry.PlayerId);
        Assert.Equal("HIDE_CARD", hideEntry.CommandType);
        Assert.NotNull(hideEntry.RawCommand);
        var rawCommand = hideEntry.RawCommand.Value;
        Assert.Equal("HIDE_CARD", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("P1-STANDBY-BANDLE-TEEMO", rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal("OGN·121/298", rawCommand.GetProperty("cardNo").GetString());
        Assert.Equal("BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE", rawCommand.GetProperty("destination").GetString());
        Assert.Equal(
            ["STANDBY_A"],
            rawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "hide-card-same", hideCard);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("P1", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var replayTriggerEvent = Assert.Single(replayEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_EXTRA_STANDBY_ARRANGED", StringComparison.Ordinal));
        Assert.Equal(acceptedTriggerEvent.Payload["battlefieldObjectId"], replayTriggerEvent.Payload["battlefieldObjectId"]);
        var replayHideEvent = Assert.Single(replayEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.Equal(acceptedHideEvent.Payload["destinationZone"], replayHideEvent.Payload["destinationZone"]);
        Assert.Equal(acceptedHideEvent.Payload["battlefieldObjectId"], replayHideEvent.Payload["battlefieldObjectId"]);
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        var replayPromptActions = replayClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        Assert.Equal(acceptedPromptActions, replayPromptActions);
        var replaySnapshot = SnapshotFor(replayClients, "P1");
        Assert.Equal(acceptedSnapshot.Tick, replaySnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, replaySnapshot.ActivePlayerId);
        var replayP1 = PlayerView(replaySnapshot, "P1");
        var replayZones = ZoneView(replayP1);
        Assert.Empty(StringList(replayZones["hand"]));
        Assert.Equal(acceptedBattlefields, StringList(replayZones["battlefields"]).ToArray());
        var replayObjects = Assert.IsType<Dictionary<string, object?>>(replayP1["objects"]);
        var replayHiddenObject = Assert.IsType<Dictionary<string, object?>>(replayObjects["P1-STANDBY-BANDLE-TEEMO"]);
        Assert.Equal(acceptedHiddenObject["isFaceDown"], replayHiddenObject["isFaceDown"]);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedHideCard = JsonDocument.Parse("""
            {
              "cmdType": "HIDE_CARD",
              "sourceObjectId": "P1-STANDBY-BANDLE-TEEMO",
              "cardNo": "OGN·121/298",
              "destination": "BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE",
              "optionalCosts": ["STANDBY_A"],
              "clientNote": "changed"
            }
            """).RootElement.Clone();

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "hide-card-same", changedHideCard);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, currentSnapshot.ActivePlayerId);
        var currentP1 = PlayerView(currentSnapshot, "P1");
        var currentZones = ZoneView(currentP1);
        Assert.Empty(StringList(currentZones["hand"]));
        Assert.Equal(acceptedBattlefields, StringList(currentZones["battlefields"]).ToArray());
        var currentObjects = Assert.IsType<Dictionary<string, object?>>(currentP1["objects"]);
        var currentHiddenObject = Assert.IsType<Dictionary<string, object?>>(currentObjects["P1-STANDBY-BANDLE-TEEMO"]);
        Assert.True(Assert.IsType<bool>(currentHiddenObject["isFaceDown"]));
        var currentPrompt = Assert.IsType<ActionPromptDto>(Assert.Single(stateClients.CallerClient.Prompts).Payload);
        Assert.Equal("P1", currentPrompt.PlayerId);
        Assert.Equal(acceptedSnapshot.Tick, currentPrompt.SnapshotTick);
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

        var afterFinishedClients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse("""{"cmdType":"PASS_PRIORITY"}""").RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-pass", passPriority);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain("match already finished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);

        var submitDeckAfterFinishedClients = new RecordingHubClients();
        var submitDeck = JsonSerializer.SerializeToElement(new
        {
            cmdType = "SUBMIT_DECK",
            legendCardNo = "UNL-237/219",
            championCardNo = "UNL-055/219",
            mainDeck = Array.Empty<string>(),
            runeDeck = Array.Empty<string>(),
            battlefields = Array.Empty<string>()
        });
        await CreateHub(submitDeckAfterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-submit-deck", submitDeck);

        var submitDeckError = Assert.Single(submitDeckAfterFinishedClients.CallerClient.Errors);
        var submitDeckPayload = Assert.IsType<ErrorDto>(submitDeckError.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, submitDeckPayload.Code);
        Assert.Equal("对局已经结束，不能提交卡组。", submitDeckPayload.Message);
        Assert.DoesNotContain("match already finished", submitDeckPayload.Message, StringComparison.Ordinal);
        Assert.Empty(submitDeckAfterFinishedClients.GroupClient.EventMessages);
    }

    [Fact]
    public async Task SubmitIntentAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var passPriority = JsonDocument.Parse($$"""
            {
              "cmdType": "PASS_PRIORITY",
              "clientIntentId": "{{sentinel}}",
              "rawSecret": "{{sentinel}}",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "internalText": "match already finished SubmitIntent MatchFinished"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, passPriority);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PASS_PRIORITY", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("match already finished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);
    }

    [Fact]
    public async Task DeclareBattleAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-declare-battle-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-declare-battle-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-declare-battle";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-declare-battle-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-declare-battle-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var declareBattleAfterFinished = JsonDocument.Parse($$"""
            {
              "cmdType": "DECLARE_BATTLE",
              "clientIntentId": "{{clientIntentId}}",
              "battlefieldId": "P2-BATTLEFIELD-GRAND-PLAZA",
              "attackerObjectIds": ["P1-BATTLEFIELD-GRAND-ATTACKER"],
              "defenderObjectIds": ["P2-BATTLEFIELD-GRAND-UNIT-001"],
              "optionalCosts": ["COMBAT_ASSIGNMENT", "raw-secret-internal-debug-cost"],
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}} raw secret internal debug DECLARE_BATTLE SubmitIntent MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "raw": "nested raw secret DECLARE_BATTLE",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, declareBattleAfterFinished);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("DECLARE_BATTLE", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task PayCostAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-pay-cost-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-pay-cost-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-pay-cost";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-pay-cost-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-pay-cost-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var payCostAfterFinished = JsonDocument.Parse($$"""
            {
              "cmdType": "PAY_COST",
              "paymentId": "PAY-3A-MANA-1",
              "paymentWindow": "TEST_PAYMENT",
              "paymentChoiceIds": ["SPEND_MANA:1", "{{sentinel}}"],
              "clientIntentId": "{{clientIntentId}}",
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}} raw secret internal debug PAY_COST SubmitIntent MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "raw": "nested raw secret PAY_COST",
                "secret": "nested secret {{sentinel}}",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, payCostAfterFinished);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PAY_COST", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ErrorCodes.MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task AssignCombatDamageAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-assign-combat-damage-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-assign-combat-damage-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-assign-combat-damage";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-assign-combat-damage-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-assign-combat-damage-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var assignCombatDamageAfterFinished = JsonDocument.Parse($$"""
            {
              "cmdType": "ASSIGN_COMBAT_DAMAGE",
              "clientIntentId": "{{clientIntentId}}",
              "battleId": "{{sentinel}}-battleId",
              "battlefieldId": "{{sentinel}}-battlefieldId",
              "assignments": [
                {
                  "sourceObjectId": "{{sentinel}}-sourceObjectId",
                  "targetObjectId": "{{sentinel}}-targetObjectId",
                  "damage": 7,
                  "clientIntent": "{{clientIntentId}}",
                  "sentinel": "{{sentinel}}",
                  "rawSecret": "{{sentinel}} assignment raw secret internal debug ASSIGN_COMBAT_DAMAGE SubmitIntent MatchFinished ErrorCodes.MatchFinished"
                }
              ],
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "command": "raw command ASSIGN_COMBAT_DAMAGE SubmitIntent MatchFinished ErrorCodes.MatchFinished",
              "rawSecret": "{{sentinel}} raw secret internal debug ASSIGN_COMBAT_DAMAGE SubmitIntent MatchFinished ErrorCodes.MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "assignments": [
                  {
                    "sourceObjectId": "{{sentinel}}-nested-sourceObjectId",
                    "targetObjectId": "{{sentinel}}-nested-targetObjectId",
                    "damage": 1
                  }
                ],
                "battleId": "{{sentinel}}-nested-battleId",
                "battlefieldId": "{{sentinel}}-nested-battlefieldId",
                "sentinel": "{{sentinel}}",
                "command": "nested raw command ASSIGN_COMBAT_DAMAGE",
                "raw": "nested raw secret ASSIGN_COMBAT_DAMAGE",
                "secret": "nested secret {{sentinel}}",
                "internal": "nested internal SubmitIntent MatchFinished ErrorCodes.MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, assignCombatDamageAfterFinished);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("assignments", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("battleId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("battlefieldId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceObjectId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("targetObjectId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ASSIGN_COMBAT_DAMAGE", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("command", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ErrorCodes.MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task OrderTriggersAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-order-triggers-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-order-triggers-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-order-triggers";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-order-triggers-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-order-triggers-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var orderTriggersAfterFinished = JsonDocument.Parse($$"""
            {
              "cmdType": "ORDER_TRIGGERS",
              "orderedTriggerIds": ["{{sentinel}}-ordered-trigger", "{{clientIntentId}}-ordered-trigger"],
              "triggerIds": ["{{sentinel}}-legacy-trigger", "{{clientIntentId}}-legacy-trigger"],
              "clientIntentId": "{{clientIntentId}}",
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "command": "raw command ORDER_TRIGGERS SubmitIntent MatchFinished ErrorCodes.MatchFinished",
              "rawSecret": "{{sentinel}} raw secret internal debug ORDER_TRIGGERS SubmitIntent MatchFinished ErrorCodes.MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "orderedTriggerIds": ["{{sentinel}}-nested-ordered-trigger"],
                "triggerIds": ["{{sentinel}}-nested-legacy-trigger"],
                "sentinel": "{{sentinel}}",
                "command": "nested raw command ORDER_TRIGGERS",
                "raw": "nested raw secret ORDER_TRIGGERS",
                "secret": "nested secret {{sentinel}}",
                "internal": "nested internal SubmitIntent MatchFinished ErrorCodes.MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, orderTriggersAfterFinished);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("orderedTriggerIds", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("triggerIds", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ORDER_TRIGGERS", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("command", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ErrorCodes.MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task ChooseHandCardsAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-choose-hand-cards-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-choose-hand-cards-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-choose-hand-cards";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-choose-hand-cards-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-choose-hand-cards-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var chooseHandCardsAfterFinished = JsonDocument.Parse($$"""
            {
              "cmdType": "CHOOSE_HAND_CARDS",
              "clientIntentId": "{{clientIntentId}}",
              "choiceId": "{{sentinel}}-choiceId",
              "choiceWindow": "HAND_CHOICE:{{sentinel}}-choiceWindow",
              "chosenObjectIds": ["{{sentinel}}-chosen-object-001", "{{clientIntentId}}-chosen-object-002"],
              "handChoiceId": "{{sentinel}}-legacy-handChoiceId",
              "handChoiceWindow": "{{sentinel}}-legacy-handChoiceWindow",
              "selectedObjectIds": ["{{sentinel}}-legacy-selected-object"],
              "chosenCardIds": ["{{clientIntentId}}-alias-chosen-card"],
              "handChoices": [
                {
                  "objectId": "{{sentinel}}-hand-choice-object",
                  "cardNo": "OGN-178/298",
                  "clientIntent": "{{clientIntentId}}",
                  "rawSecret": "{{sentinel}} hand choice raw secret internal debug CHOOSE_HAND_CARDS SubmitIntent MatchFinished ErrorCodes.MatchFinished"
                }
              ],
              "choosingPlayerId": "{{sentinel}}-choosing-player",
              "requiredCount": 2,
              "maxCount": 2,
              "reason": "{{sentinel}} raw hand-choice reason CHOOSE_HAND_CARDS SubmitIntent MatchFinished",
              "effectKind": "{{sentinel}} raw hand-choice effectKind",
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "command": "raw command CHOOSE_HAND_CARDS SubmitIntent MatchFinished ErrorCodes.MatchFinished",
              "rawSecret": "{{sentinel}} raw secret internal debug CHOOSE_HAND_CARDS SubmitIntent MatchFinished ErrorCodes.MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "choiceId": "{{sentinel}}-nested-choiceId",
                "choiceWindow": "HAND_CHOICE:{{sentinel}}-nested-choiceWindow",
                "chosenObjectIds": ["{{sentinel}}-nested-chosen-object"],
                "handChoiceId": "{{sentinel}}-nested-legacy-handChoiceId",
                "handChoiceWindow": "{{sentinel}}-nested-legacy-handChoiceWindow",
                "selectedObjectIds": ["{{sentinel}}-nested-selected-object"],
                "chosenCardIds": ["{{clientIntentId}}-nested-chosen-card"],
                "handChoices": [
                  {
                    "objectId": "{{sentinel}}-nested-hand-choice-object",
                    "secret": "{{sentinel}} nested hand choice secret"
                  }
                ],
                "choosingPlayerId": "{{sentinel}}-nested-choosing-player",
                "requiredCount": 1,
                "maxCount": 2,
                "reason": "nested raw hand-choice reason CHOOSE_HAND_CARDS",
                "effectKind": "nested raw hand-choice effectKind",
                "sentinel": "{{sentinel}}",
                "command": "nested raw command CHOOSE_HAND_CARDS",
                "raw": "nested raw secret CHOOSE_HAND_CARDS",
                "secret": "nested secret {{sentinel}}",
                "internal": "nested internal SubmitIntent MatchFinished ErrorCodes.MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, chooseHandCardsAfterFinished);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("choiceId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("choiceWindow", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("chosenObjectIds", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("handChoiceId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("handChoiceWindow", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedObjectIds", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("chosenCardIds", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("handChoices", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("choosingPlayerId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("requiredCount", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("maxCount", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("reason", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("effectKind", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("CHOOSE_HAND_CARDS", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("HAND_CHOICE", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("command", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ErrorCodes.MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task PlayCardAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-play-card-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-play-card-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-play-card";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-play-card-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-play-card-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var playCard = JsonDocument.Parse($$"""
            {
              "cmdType": "PLAY_CARD",
              "clientIntentId": "{{clientIntentId}}",
              "sourceObjectId": "P1-SPELL-PLAY-CARD-AFTER-FINISHED",
              "cardNo": "OGN-268/298",
              "targetObjectIds": ["{{sentinel}}-target"],
              "optionalCosts": ["raw-secret-internal-debug-cost"],
              "destination": "BATTLEFIELD:P1-MAIN",
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}} raw secret internal debug PLAY_CARD SubmitIntent MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "raw": "nested raw secret PLAY_CARD",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, playCard);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PLAY_CARD", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task HideCardAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-hide-card-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-hide-card-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-hide-card";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-hide-card-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-hide-card-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var hideCard = JsonDocument.Parse($$"""
            {
              "cmdType": "HIDE_CARD",
              "clientIntentId": "{{clientIntentId}}",
              "sourceObjectId": "{{sentinel}}-source",
              "cardNo": "OGN·121/298",
              "destination": "BATTLEFIELD:{{sentinel}}-destination",
              "optionalCosts": ["STANDBY_A", "raw-secret-internal-debug-cost"],
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}} raw secret internal debug HIDE_CARD SubmitIntent MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "raw": "nested raw secret HIDE_CARD",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, hideCard);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("HIDE_CARD", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task RevealCardAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-reveal-card-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-reveal-card-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-reveal-card";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-reveal-card-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-reveal-card-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var revealCard = JsonDocument.Parse($$"""
            {
              "cmdType": "REVEAL_CARD",
              "clientIntentId": "{{clientIntentId}}",
              "sourceObjectId": "{{sentinel}}-source",
              "cardNo": "OGN·197/298",
              "targetObjectIds": ["{{sentinel}}-target"],
              "mode": "STANDBY_REACTION",
              "optionalCosts": ["STANDBY_REVEAL_0", "raw-secret-internal-debug-cost"],
              "destination": "STACK",
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}} raw secret internal debug REVEAL_CARD SubmitIntent MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "raw": "nested raw secret REVEAL_CARD",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, revealCard);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("REVEAL_CARD", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task AssembleEquipmentAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-assemble-equipment-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-assemble-equipment-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-assemble-equipment";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-assemble-equipment-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-assemble-equipment-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var assembleEquipment = JsonDocument.Parse($$"""
            {
              "cmdType": "ASSEMBLE_EQUIPMENT",
              "clientIntentId": "{{clientIntentId}}",
              "sourceObjectId": "{{sentinel}}-source-equipment",
              "targetObjectId": "{{sentinel}}-target-unit",
              "optionalCosts": ["ASSEMBLE_RED", "raw-secret-internal-debug-cost"],
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "raw": "{{sentinel}} raw secret ASSEMBLE_EQUIPMENT SubmitIntent MatchFinished",
              "internal": "top-level internal ASSEMBLE_EQUIPMENT SubmitIntent MatchFinished",
              "debug": "top-level debug raw secret internal",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "raw": "nested raw secret ASSEMBLE_EQUIPMENT",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, assembleEquipment);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ASSEMBLE_EQUIPMENT", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task TapRuneAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-tap-rune-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-tap-rune-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-tap-rune";
        const string nestedSentinelText = "nested sentinel text SECRET-RAW-clientIntentId-after-finished-tap-rune";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-tap-rune-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-tap-rune-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var tapRune = JsonDocument.Parse($$"""
            {
              "cmdType": "TAP_RUNE",
              "clientIntentId": "{{clientIntentId}}",
              "sourceObjectId": "{{sentinel}}-source-rune",
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "raw": "{{sentinel}} raw secret TAP_RUNE SubmitIntent MatchFinished",
              "secret": "top-level secret TAP_RUNE",
              "internal": "top-level internal TAP_RUNE SubmitIntent MatchFinished",
              "debug": "top-level debug raw secret internal",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "sentinelText": "{{nestedSentinelText}}",
                "raw": "nested raw secret TAP_RUNE",
                "secret": "nested secret TAP_RUNE",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, tapRune);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nestedSentinelText, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("TAP_RUNE", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task RecycleRuneAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-recycle-rune-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-recycle-rune-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-recycle-rune";
        const string nestedSentinelText = "nested sentinel text SECRET-RAW-clientIntentId-after-finished-recycle-rune";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-recycle-rune-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-recycle-rune-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var recycleRune = JsonDocument.Parse($$"""
            {
              "cmdType": "RECYCLE_RUNE",
              "clientIntentId": "{{clientIntentId}}",
              "sourceObjectId": "{{sentinel}}-source-rune",
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "raw": "{{sentinel}} raw secret RECYCLE_RUNE SubmitIntent MatchFinished",
              "secret": "top-level secret RECYCLE_RUNE",
              "internal": "top-level internal RECYCLE_RUNE SubmitIntent MatchFinished",
              "debug": "top-level debug raw secret internal",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "sentinelText": "{{nestedSentinelText}}",
                "raw": "nested raw secret RECYCLE_RUNE",
                "secret": "nested secret RECYCLE_RUNE",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, recycleRune);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nestedSentinelText, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("RECYCLE_RUNE", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task MoveUnitAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-move-unit-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-move-unit-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-move-unit";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-move-unit-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-move-unit-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var move = JsonDocument.Parse($$"""
            {
              "cmdType": "MOVE_UNIT",
              "clientIntentId": "{{clientIntentId}}",
              "sourceObjectId": "{{sentinel}}-source",
              "origin": "BATTLEFIELD:{{sentinel}}-origin",
              "destination": "BATTLEFIELD:{{sentinel}}-destination",
              "optionalCosts": ["raw-secret-internal-debug-cost"],
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}} raw secret internal debug MOVE_UNIT SubmitIntent MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "raw": "nested raw secret MOVE_UNIT",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, move);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(MessageType.ERROR, error.Type);
        Assert.Equal(roomId, error.RoomId);
        Assert.Equal("P1", error.PlayerId);
        AssertProtocolDefaults(error);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MOVE_UNIT", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task ActivateAbilityAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-activate-ability-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-activate-ability-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-activate-ability";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-activate-ability-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-activate-ability-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var activateAbility = JsonDocument.Parse($$"""
            {
              "cmdType": "ACTIVATE_ABILITY",
              "clientIntentId": "{{clientIntentId}}",
              "sourceObjectId": "{{sentinel}}-source",
              "abilityId": "{{sentinel}}-ability",
              "targetObjectIds": ["{{sentinel}}-target"],
              "optionalCosts": ["raw-secret-internal-debug-cost"],
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}} raw secret internal debug ACTIVATE_ABILITY SubmitIntent MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "raw": "nested raw secret ACTIVATE_ABILITY",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, activateAbility);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task LegendActAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-legend-act-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-legend-act-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-legend-act";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-legend-act-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-legend-act-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var legendAct = JsonDocument.Parse($$"""
            {
              "cmdType": "LEGEND_ACT",
              "clientIntentId": "{{clientIntentId}}",
              "sourceObjectId": "P1-LEGEND-POPPY",
              "abilityId": "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
              "targetObjectIds": ["{{sentinel}}-target"],
              "optionalCosts": ["SPEND_EXPERIENCE:3", "raw-secret-internal-debug-cost"],
              "clientIntent": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}} raw secret internal debug LEGEND_ACT SubmitIntent MatchFinished",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "clientIntentId": "{{clientIntentId}}",
                "clientIntent": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "raw": "nested raw secret LEGEND_ACT",
                "internal": "nested internal SubmitIntent MatchFinished",
                "debug": "nested debug raw secret internal"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, legendAct);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("LEGEND_ACT", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task EndTurnAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-end-turn-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-end-turn-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-end-turn";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-end-turn-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-end-turn-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse($$"""
            {
              "cmdType": "END_TURN",
              "clientIntentId": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}}",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "internalText": "raw secret internal END_TURN MatchFinished debug text"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, endTurn);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("END_TURN", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task EndTurnWrapperAfterFinishedRedactsClientIntentAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-end-turn-wrapper-after-finished";
        const string clientIntentId = "end-turn-after-finished-raw-secret-clientIntent-END_TURN-MatchFinished-internal-debug";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-end-turn-wrapper-after-finished");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-end-turn-wrapper-after-finished-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .EndTurn(roomId, "P1", clientIntentId);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("END_TURN", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("end-turn-after-finished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task ReadyAfterFinishedRedactsClientIntentAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-ready-redacts-client-intent";
        const string clientIntentId = "ready-after-finished-SECRET-RAW-clientIntentId-MatchFinished";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-ready-redacts-client-intent");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-ready-redacts-client-intent-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .Ready(roomId, "P1", clientIntentId);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能准备。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ready-after-finished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("READY", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task PassAfterFinishedRedactsClientIntentAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-pass-redacts-client-intent";
        const string clientIntentId = "pass-after-finished-SECRET-RAW-clientIntent-MatchFinished-internal-debug";
        const string sentinel = "SECRET-RAW-clientIntent";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-pass-redacts-client-intent");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-pass-redacts-client-intent-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .Pass(roomId, "P1", clientIntentId);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PASS", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pass-after-finished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task MulliganAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-mulligan-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-mulligan-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-mulligan";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-mulligan-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-mulligan-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var mulligan = JsonDocument.Parse($$"""
            {
              "cmdType": "MULLIGAN",
              "handObjectIds": [],
              "clientIntentId": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}}",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "internalText": "raw secret internal MULLIGAN MatchFinished debug text"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, mulligan);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MULLIGAN", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task SurrenderAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-surrender-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-surrender-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-surrender";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-surrender-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-surrender-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var p1FinishedSnapshotHash = MatchStateHasher.HashValue(SnapshotFor(battleClients, "P1"));
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        var p2FinishedSnapshotHash = MatchStateHasher.HashValue(battleSnapshot);
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var surrender = JsonDocument.Parse($$"""
            {
              "cmdType": "SURRENDER",
              "clientIntentId": "{{clientIntentId}}",
              "sentinel": "{{sentinel}}",
              "rawSecret": "{{sentinel}}",
              "debug": "raw secret internal SURRENDER MatchFinished debug text",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "sentinel": "{{sentinel}}",
                "raw": "{{sentinel}}",
                "internalText": "raw secret internal SURRENDER MatchFinished debug text"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, surrender);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能继续提交行动。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SURRENDER", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ErrorCodes.MatchFinished, payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.Errors);
        Assert.Equal(journalCountAfterFinished, journal.Entries.Count);

        var session = await registry.GetOrCreateAsync(roomId, default);
        Assert.Equal(p1FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2FinishedSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
    }

    [Fact]
    public async Task SubmitDeckAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate()
    {
        const string roomId = "p7-9-after-finished-submit-deck-redacts-sentinel";
        const string clientIntentId = "intent-p7-9-after-finished-submit-deck-SECRET-RAW-clientIntentId";
        const string sentinel = "SECRET-RAW-clientIntentId-after-finished-submit-deck";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
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
            .SeedScenario(roomId, "P1", "battlefield-held-seven-units-win", "seed-p7-9-after-finished-submit-deck-redacts-sentinel");

        var p1Prompt = PromptFor(seedClients, "P1");
        Assert.Contains(p1Prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

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
            .SubmitIntent(roomId, "P1", "intent-p7-9-after-finished-submit-deck-redacts-sentinel-win", declareBattle);

        Assert.Empty(battleClients.CallerClient.Errors);
        var battleSnapshot = SnapshotFor(battleClients, "P2");
        Assert.Equal("P2", battleSnapshot.Timing["winnerPlayerId"]);
        Assert.Equal(MatchStatuses.Finished, battleSnapshot.Timing["roomStatus"]);
        var journalCountAfterFinished = journal.Entries.Count;

        var afterFinishedClients = new RecordingHubClients();
        var submitDeck = JsonDocument.Parse($$"""
            {
              "cmdType": "SUBMIT_DECK",
              "clientIntentId": "{{sentinel}}",
              "legendCardNo": "UNL-237/219",
              "championCardNo": "UNL-055/219",
              "mainDeck": [],
              "runeDeck": [],
              "battlefields": [],
              "rawSecret": "{{sentinel}}",
              "nested": {
                "intentId": "{{clientIntentId}}",
                "internalText": "match already finished SubmitIntent MatchFinished"
              }
            }
            """).RootElement.Clone();
        await CreateHub(afterFinishedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", clientIntentId, submitDeck);

        var error = Assert.Single(afterFinishedClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.MatchFinished, payload.Code);
        Assert.Equal("对局已经结束，不能提交卡组。", payload.Message);
        Assert.DoesNotContain(clientIntentId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("intentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SUBMIT_DECK", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("match already finished", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitIntent", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchFinished", payload.Message, StringComparison.Ordinal);
        Assert.Empty(afterFinishedClients.CallerClient.EventMessages);
        Assert.Empty(afterFinishedClients.CallerClient.Snapshots);
        Assert.Empty(afterFinishedClients.CallerClient.Prompts);
        Assert.Empty(afterFinishedClients.GroupClient.EventMessages);
        Assert.Empty(afterFinishedClients.GroupClient.Snapshots);
        Assert.Empty(afterFinishedClients.GroupClient.Prompts);
        Assert.Equal(journalCountAfterFinished + 1, journal.Entries.Count);
        var rejectedEntry = journal.Entries[^1];
        Assert.Equal(roomId, rejectedEntry.RoomId);
        Assert.Equal("P1", rejectedEntry.PlayerId);
        Assert.Equal(clientIntentId, rejectedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.SubmitDeck, rejectedEntry.CommandType);
        Assert.False(rejectedEntry.Accepted);
        Assert.Equal("对局已经结束，不能提交卡组。", rejectedEntry.ErrorMessage);
        Assert.Empty(rejectedEntry.Events);
        Assert.Equal(MatchStatuses.Finished, rejectedEntry.AuthoritativeState.Status);
        Assert.Equal(["P1", "P2"], rejectedEntry.Snapshots.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        Assert.All(
            rejectedEntry.Snapshots.Values,
            snapshot => Assert.Equal(MatchStatuses.Finished, snapshot.Timing["roomStatus"]));
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
    public async Task RevealCardDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "p6-5a-reveal-card-raw-idempotency";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var development = new TestHostEnvironment(Environments.Development);
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
                development)
            .SeedScenario(roomId, "P1", "standby-reaction", "seed-p6-reveal-card-raw-idempotency");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var revealCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "REVEAL_CARD", StringComparison.Ordinal));
        Assert.True(revealCandidate.Enabled);
        Assert.Contains(revealCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-FACEDOWN-OGN-TEEMO-PURPLE", StringComparison.Ordinal));
        Assert.Contains(revealCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "STACK", StringComparison.Ordinal));
        Assert.Contains(revealCandidate.OptionalCosts ?? [], choice => string.Equals(choice.Id, "STANDBY_REVEAL_0", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(p1Prompt.PromptId));
        Assert.True(p1Prompt.SnapshotTick.HasValue);
        var seededJournalCount = journal.Entries.Count;
        var revealCard = JsonSerializer.SerializeToElement(new
        {
            cmdType = "REVEAL_CARD",
            sourceObjectId = "P1-FACEDOWN-OGN-TEEMO-PURPLE",
            cardNo = "OGN·197/298",
            targetObjectIds = Array.Empty<string>(),
            mode = "STANDBY_REACTION",
            optionalCosts = new[] { "STANDBY_REVEAL_0" },
            destination = "STACK",
            promptId = p1Prompt.PromptId,
            snapshotTick = p1Prompt.SnapshotTick
        });
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "reveal-card-same", revealCard);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        Assert.Empty(acceptedClients.CallerClient.EventMessages);
        Assert.Empty(acceptedClients.CallerClient.Snapshots);
        Assert.Empty(acceptedClients.CallerClient.Prompts);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        var acceptedRevealEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_REVEALED", StringComparison.Ordinal));
        Assert.Equal("P1", acceptedRevealEvent.Payload["playerId"]);
        Assert.Equal("P1-FACEDOWN-OGN-TEEMO-PURPLE", acceptedRevealEvent.Payload["sourceObjectId"]);
        Assert.Equal("OGN·197/298", acceptedRevealEvent.Payload["cardNo"]);
        var acceptedPlayEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Equal("P1-FACEDOWN-OGN-TEEMO-PURPLE", acceptedPlayEvent.Payload["sourceObjectId"]);
        Assert.Equal("OGN·197/298", acceptedPlayEvent.Payload["cardNo"]);
        var acceptedStackEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Equal("P1-FACEDOWN-OGN-TEEMO-PURPLE", acceptedStackEvent.Payload["sourceObjectId"]);
        Assert.Equal("OGN·197/298", acceptedStackEvent.Payload["cardNo"]);
        var acceptedSnapshot = SnapshotFor(acceptedClients, "P1");
        var acceptedStackSignature = acceptedSnapshot.Stack
            .Select(stackItem => Assert.IsType<Dictionary<string, object?>>(stackItem))
            .Select(stackItem => $"{stackItem["sourceObjectId"]}|{stackItem["cardNo"]}")
            .ToArray();
        Assert.Contains("P1-FACEDOWN-OGN-TEEMO-PURPLE|OGN·197/298", acceptedStackSignature);
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptActions = acceptedClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        var acceptedP1Prompt = PromptFor(acceptedClients, "P1");
        var acceptedP1PromptActions = string.Join("|", acceptedP1Prompt.Actions);
        Assert.Equal(acceptedSnapshot.Tick, acceptedP1Prompt.SnapshotTick);
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(seededJournalCount + 1, acceptedJournalCount);
        var revealEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "reveal-card-same", StringComparison.Ordinal));
        Assert.Equal(roomId, revealEntry.RoomId);
        Assert.Equal("P1", revealEntry.PlayerId);
        Assert.Equal("REVEAL_CARD", revealEntry.CommandType);
        Assert.NotNull(revealEntry.RawCommand);
        var rawCommand = revealEntry.RawCommand.Value;
        Assert.Equal("REVEAL_CARD", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("P1-FACEDOWN-OGN-TEEMO-PURPLE", rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal("OGN·197/298", rawCommand.GetProperty("cardNo").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Equal("STANDBY_REACTION", rawCommand.GetProperty("mode").GetString());
        Assert.Equal(
            ["STANDBY_REVEAL_0"],
            rawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.Equal("STACK", rawCommand.GetProperty("destination").GetString());
        Assert.Equal(p1Prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(p1Prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "reveal-card-same", revealCard);

        Assert.Empty(replayClients.CallerClient.Errors);
        Assert.Empty(replayClients.CallerClient.EventMessages);
        Assert.Empty(replayClients.CallerClient.Snapshots);
        Assert.Empty(replayClients.CallerClient.Prompts);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("P1", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var replayRevealEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_REVEALED", StringComparison.Ordinal));
        Assert.Equal(acceptedRevealEvent.Payload["playerId"], replayRevealEvent.Payload["playerId"]);
        Assert.Equal(acceptedRevealEvent.Payload["sourceObjectId"], replayRevealEvent.Payload["sourceObjectId"]);
        Assert.Equal(acceptedRevealEvent.Payload["cardNo"], replayRevealEvent.Payload["cardNo"]);
        var replayStackEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Equal(acceptedStackEvent.Payload["sourceObjectId"], replayStackEvent.Payload["sourceObjectId"]);
        Assert.Equal(acceptedStackEvent.Payload["cardNo"], replayStackEvent.Payload["cardNo"]);
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        var replayPromptActions = replayClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        Assert.Equal(acceptedPromptActions, replayPromptActions);
        var replaySnapshot = SnapshotFor(replayClients, "P1");
        Assert.Equal(acceptedSnapshot.Tick, replaySnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, replaySnapshot.ActivePlayerId);
        var replayStackSignature = replaySnapshot.Stack
            .Select(stackItem => Assert.IsType<Dictionary<string, object?>>(stackItem))
            .Select(stackItem => $"{stackItem["sourceObjectId"]}|{stackItem["cardNo"]}")
            .ToArray();
        Assert.Equal(acceptedStackSignature, replayStackSignature);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedRevealCard = JsonSerializer.SerializeToElement(new
        {
            cmdType = "REVEAL_CARD",
            sourceObjectId = "P1-FACEDOWN-OGN-TEEMO-PURPLE",
            cardNo = "OGN·197/298",
            targetObjectIds = Array.Empty<string>(),
            mode = "STANDBY_REACTION",
            optionalCosts = new[] { "STANDBY_REVEAL_0" },
            destination = "STACK",
            promptId = p1Prompt.PromptId,
            snapshotTick = p1Prompt.SnapshotTick,
            clientNote = "changed"
        });

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "reveal-card-same", changedRevealCard);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, currentSnapshot.ActivePlayerId);
        var currentStackSignature = currentSnapshot.Stack
            .Select(stackItem => Assert.IsType<Dictionary<string, object?>>(stackItem))
            .Select(stackItem => $"{stackItem["sourceObjectId"]}|{stackItem["cardNo"]}")
            .ToArray();
        Assert.Equal(acceptedStackSignature, currentStackSignature);
        var currentPrompt = Assert.IsType<ActionPromptDto>(Assert.Single(stateClients.CallerClient.Prompts).Payload);
        Assert.Equal("P1", currentPrompt.PlayerId);
        Assert.Equal(acceptedSnapshot.Tick, currentPrompt.SnapshotTick);
        Assert.Equal(acceptedP1PromptActions, string.Join("|", currentPrompt.Actions));
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
              "targetObjectIds": ["P1-UNIT-ASSEMBLE-TARGET"]
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
        Assert.Contains(resolveEvents, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, "P1-EQUIPMENT-LONG-SWORD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, "P1-UNIT-ASSEMBLE-TARGET", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["attachedToObjectId"] as string, "P1-UNIT-ASSEMBLE-TARGET", StringComparison.Ordinal));
        var playSnapshot = SnapshotFor(passP2Clients, "P1");
        var playP1 = Assert.IsType<Dictionary<string, object?>>(playSnapshot.Players["P1"]);
        var playP1Zones = Assert.IsType<Dictionary<string, object?>>(playP1["zones"]);
        Assert.Contains("P1-EQUIPMENT-LONG-SWORD", Assert.IsAssignableFrom<IReadOnlyList<string>>(playP1Zones["base"]));
        var playObjects = Assert.IsType<Dictionary<string, object?>>(playP1["objects"]);
        var equipment = Assert.IsType<Dictionary<string, object?>>(playObjects["P1-EQUIPMENT-LONG-SWORD"]);
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
    public async Task LegendActDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string roomId = "p7-9-legend-act-raw-idempotency";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var development = new TestHostEnvironment(Environments.Development);
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
                development)
            .SeedScenario(roomId, "P1", "legend-act", "seed-p7-9-legend-act-raw-idempotency");

        Assert.Empty(seedClients.CallerClient.Errors);
        var p1Prompt = PromptFor(seedClients, "P1");
        var legendCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal));
        Assert.True(legendCandidate.Enabled);
        Assert.Contains(legendCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-LEGEND-POPPY", StringComparison.Ordinal));
        Assert.Contains(legendCandidate.Modes ?? [], choice => string.Equals(choice.Id, "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW", StringComparison.Ordinal));
        Assert.Contains(legendCandidate.OptionalCosts ?? [], choice => string.Equals(choice.Id, "SPEND_EXPERIENCE:3", StringComparison.Ordinal));
        var seededJournalCount = journal.Entries.Count;
        var legendAct = JsonDocument.Parse("""
            {
              "cmdType": "LEGEND_ACT",
              "sourceObjectId": "P1-LEGEND-POPPY",
              "abilityId": "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
              "targetObjectIds": [],
              "optionalCosts": ["SPEND_EXPERIENCE:3"]
            }
            """).RootElement.Clone();
        var acceptedClients = new RecordingHubClients();

        await CreateHub(acceptedClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "legend-act-same", legendAct);

        Assert.Empty(acceptedClients.CallerClient.Errors);
        var acceptedMessage = Assert.Single(acceptedClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, acceptedMessage.Type);
        var acceptedEvents = EventsFor(acceptedClients);
        var acceptedLegendEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal("P1", acceptedLegendEvent.Payload["playerId"]);
        Assert.Equal("P1-LEGEND-POPPY", acceptedLegendEvent.Payload["sourceObjectId"]);
        Assert.Equal("LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW", acceptedLegendEvent.Payload["abilityId"]);
        var acceptedExperienceEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(3, acceptedExperienceEvent.Payload["amount"]);
        Assert.Equal(0, acceptedExperienceEvent.Payload["remainingExperience"]);
        Assert.Equal("LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW", acceptedExperienceEvent.Payload["abilityId"]);
        var acceptedExhaustEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-POPPY", acceptedExhaustEvent.Payload["sourceObjectId"]);
        Assert.Equal("LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW", acceptedExhaustEvent.Payload["abilityId"]);
        var acceptedDrawEvent = Assert.Single(acceptedEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal("P1", acceptedDrawEvent.Payload["playerId"]);
        Assert.Equal(1, acceptedDrawEvent.Payload["count"]);
        Assert.Equal(2, acceptedClients.GroupClient.Snapshots.Count);
        Assert.Equal(2, acceptedClients.GroupClient.Prompts.Count);
        var acceptedSnapshot = SnapshotFor(acceptedClients, "P1");
        var acceptedP1 = PlayerView(acceptedSnapshot, "P1");
        Assert.Equal(0, Assert.IsType<int>(acceptedP1["experience"]));
        var acceptedZones = ZoneView(acceptedP1);
        Assert.Equal(0, Assert.IsType<int>(acceptedZones["mainDeckCount"]));
        Assert.Equal(["P1-LEGEND-DRAW-001"], StringList(acceptedZones["hand"]));
        var acceptedObjects = Assert.IsType<Dictionary<string, object?>>(acceptedP1["objects"]);
        var acceptedLegend = Assert.IsType<Dictionary<string, object?>>(acceptedObjects["P1-LEGEND-POPPY"]);
        Assert.True(Assert.IsType<bool>(acceptedLegend["isExhausted"]));
        var acceptedSnapshotPlayers = acceptedClients.GroupClient.Snapshots
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptPlayers = acceptedClients.GroupClient.Prompts
            .Select(message => message.PlayerId)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var acceptedPromptActions = acceptedClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        var acceptedJournalCount = journal.Entries.Count;
        Assert.Equal(seededJournalCount + 1, acceptedJournalCount);
        var legendEntry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.ClientIntentId, "legend-act-same", StringComparison.Ordinal));
        Assert.Equal(roomId, legendEntry.RoomId);
        Assert.Equal("P1", legendEntry.PlayerId);
        Assert.Equal("LEGEND_ACT", legendEntry.CommandType);
        Assert.NotNull(legendEntry.RawCommand);
        var rawCommand = legendEntry.RawCommand.Value;
        Assert.Equal("LEGEND_ACT", rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("P1-LEGEND-POPPY", rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal("LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW", rawCommand.GetProperty("abilityId").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Equal(
            ["SPEND_EXPERIENCE:3"],
            rawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => Assert.IsType<string>(choice.GetString()))
                .ToArray());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));

        var replayClients = new RecordingHubClients();
        await CreateHub(replayClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, " P1 ", "legend-act-same", legendAct);

        Assert.Empty(replayClients.CallerClient.Errors);
        var replayMessage = Assert.Single(replayClients.GroupClient.EventMessages);
        Assert.Equal(MessageType.EVENTS, replayMessage.Type);
        Assert.Equal("P1", replayMessage.PlayerId);
        Assert.Equal(acceptedMessage.ServerTick, replayMessage.ServerTick);
        AssertProtocolDefaults(replayMessage);
        var replayEvents = EventsFor(replayClients);
        Assert.Equal(
            acceptedEvents.Select(gameEvent => gameEvent.Kind).ToArray(),
            replayEvents.Select(gameEvent => gameEvent.Kind).ToArray());
        var replayLegendEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal(acceptedLegendEvent.Payload["sourceObjectId"], replayLegendEvent.Payload["sourceObjectId"]);
        Assert.Equal(acceptedLegendEvent.Payload["abilityId"], replayLegendEvent.Payload["abilityId"]);
        var replayExperienceEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(acceptedExperienceEvent.Payload["amount"], replayExperienceEvent.Payload["amount"]);
        Assert.Equal(acceptedExperienceEvent.Payload["remainingExperience"], replayExperienceEvent.Payload["remainingExperience"]);
        var replayExhaustEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal));
        Assert.Equal(acceptedExhaustEvent.Payload["sourceObjectId"], replayExhaustEvent.Payload["sourceObjectId"]);
        Assert.Equal(acceptedExhaustEvent.Payload["abilityId"], replayExhaustEvent.Payload["abilityId"]);
        var replayDrawEvent = Assert.Single(replayEvents, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(acceptedDrawEvent.Payload["count"], replayDrawEvent.Payload["count"]);
        Assert.Equal(acceptedDrawEvent.Payload["playerId"], replayDrawEvent.Payload["playerId"]);
        Assert.Equal(acceptedClients.GroupClient.Snapshots.Count, replayClients.GroupClient.Snapshots.Count);
        Assert.Equal(acceptedClients.GroupClient.Prompts.Count, replayClients.GroupClient.Prompts.Count);
        Assert.Equal(
            acceptedSnapshotPlayers,
            replayClients.GroupClient.Snapshots
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            acceptedPromptPlayers,
            replayClients.GroupClient.Prompts
                .Select(message => message.PlayerId)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
        foreach (var snapshotMessage in replayClients.GroupClient.Snapshots)
        {
            Assert.Equal(MessageType.SNAPSHOT, snapshotMessage.Type);
            AssertProtocolDefaults(snapshotMessage);
        }

        foreach (var promptMessage in replayClients.GroupClient.Prompts)
        {
            Assert.Equal(MessageType.PROMPT, promptMessage.Type);
            AssertProtocolDefaults(promptMessage);
        }

        var replayPromptActions = replayClients.GroupClient.Prompts
            .Select(message => Assert.IsType<ActionPromptDto>(message.Payload))
            .OrderBy(prompt => prompt.PlayerId, StringComparer.Ordinal)
            .Select(prompt => string.Join("|", prompt.Actions))
            .ToArray();
        Assert.Equal(acceptedPromptActions, replayPromptActions);
        var replaySnapshot = SnapshotFor(replayClients, "P1");
        Assert.Equal(acceptedSnapshot.Tick, replaySnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, replaySnapshot.ActivePlayerId);
        var replayP1 = PlayerView(replaySnapshot, "P1");
        Assert.Equal(acceptedP1["experience"], replayP1["experience"]);
        var replayZones = ZoneView(replayP1);
        Assert.Equal(acceptedZones["mainDeckCount"], replayZones["mainDeckCount"]);
        Assert.Equal(StringList(acceptedZones["hand"]), StringList(replayZones["hand"]));
        var replayObjects = Assert.IsType<Dictionary<string, object?>>(replayP1["objects"]);
        var replayLegend = Assert.IsType<Dictionary<string, object?>>(replayObjects["P1-LEGEND-POPPY"]);
        Assert.Equal(acceptedLegend["isExhausted"], replayLegend["isExhausted"]);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);

        var conflictClients = new RecordingHubClients();
        var changedLegendAct = JsonDocument.Parse("""
            {
              "cmdType": "LEGEND_ACT",
              "sourceObjectId": "P1-LEGEND-POPPY",
              "abilityId": "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
              "targetObjectIds": [],
              "optionalCosts": ["SPEND_EXPERIENCE:3"],
              "clientNote": "changed"
            }
            """).RootElement.Clone();

        await CreateHub(conflictClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "legend-act-same", changedLegendAct);

        var error = Assert.Single(conflictClients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.ClientIntentConflict, payload.Code);
        Assert.Equal("该客户端行动编号已用于其他命令。", payload.Message);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.Empty(conflictClients.GroupClient.EventMessages);
        Assert.Empty(conflictClients.GroupClient.Snapshots);
        Assert.Empty(conflictClients.GroupClient.Prompts);
        Assert.Empty(conflictClients.CallerClient.EventMessages);
        Assert.Empty(conflictClients.CallerClient.Snapshots);
        Assert.Empty(conflictClients.CallerClient.Prompts);
        Assert.Equal(acceptedJournalCount, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed", StringComparison.Ordinal));

        var stateClients = new RecordingHubClients();
        await CreateHub(stateClients, new RecordingGroupManager(), "connection-1", registry)
            .RequestSnapshot(roomId, "P1");

        Assert.Empty(stateClients.CallerClient.Errors);
        var currentSnapshot = Assert.IsType<SnapshotDto>(Assert.Single(stateClients.CallerClient.Snapshots).Payload);
        Assert.Equal(acceptedSnapshot.Tick, currentSnapshot.Tick);
        Assert.Equal(acceptedSnapshot.ActivePlayerId, currentSnapshot.ActivePlayerId);
        var currentP1 = PlayerView(currentSnapshot, "P1");
        Assert.Equal(acceptedP1["experience"], currentP1["experience"]);
        var currentZones = ZoneView(currentP1);
        Assert.Equal(acceptedZones["mainDeckCount"], currentZones["mainDeckCount"]);
        Assert.Equal(StringList(acceptedZones["hand"]), StringList(currentZones["hand"]));
        var currentObjects = Assert.IsType<Dictionary<string, object?>>(currentP1["objects"]);
        var currentLegend = Assert.IsType<Dictionary<string, object?>>(currentObjects["P1-LEGEND-POPPY"]);
        Assert.Equal(acceptedLegend["isExhausted"], currentLegend["isExhausted"]);
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
    public async Task P6LifecycleEphemeralLeblancStaticSeedKeepsSameBattlefieldEphemeralInDevelopment()
    {
        const string roomId = "p6-8b-lifecycle-ephemeral-leblanc-static";
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
            .SeedScenario(roomId, "P1", "lifecycle-ephemeral-leblanc-static", "seed-p6-lifecycle-ephemeral-leblanc-static");

        var endTurnClients = new RecordingHubClients();
        var endTurn = JsonDocument.Parse("""{"cmdType":"END_TURN"}""").RootElement.Clone();
        await CreateHub(endTurnClients, new RecordingGroupManager(), "connection-1", registry)
            .SubmitIntent(roomId, "P1", "intent-p6-ephemeral-leblanc-static-end-turn", endTurn);

        Assert.Empty(endTurnClients.CallerClient.Errors);
        var events = EventsFor(endTurnClients);
        var ephemeralDestroyedObjectIds = events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "EPHEMERAL_TURN_START", StringComparison.Ordinal))
            .Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["targetObjectId"]))
            .ToArray();
        Assert.Equal(["P2-EPHEMERAL-BASE", "P2-EPHEMERAL-OTHER-BATTLEFIELD"], ephemeralDestroyedObjectIds);
        Assert.DoesNotContain("P2-EPHEMERAL-PROTECTED", ephemeralDestroyedObjectIds);

        var snapshot = SnapshotFor(endTurnClients, "P2");
        var p2 = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P2"]);
        var p2Zones = Assert.IsType<Dictionary<string, object?>>(p2["zones"]);
        var battlefields = Assert.IsAssignableFrom<IReadOnlyList<string>>(p2Zones["battlefields"]);
        Assert.Contains("P2-BATTLEFIELD-LEBLANC", battlefields);
        Assert.Contains("P2-LEBLANC-STATIC", battlefields);
        Assert.Contains("P2-EPHEMERAL-PROTECTED", battlefields);
        Assert.Contains("P2-BATTLEFIELD-OTHER", battlefields);
        Assert.DoesNotContain("P2-EPHEMERAL-OTHER-BATTLEFIELD", battlefields);
        Assert.Equal(
            ["P2-EPHEMERAL-BASE", "P2-EPHEMERAL-OTHER-BATTLEFIELD"],
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
        Assert.Equal("载入测试状态仅在开发环境可用。", payload.Message);
        Assert.DoesNotContain("SeedScenario", payload.Message, StringComparison.Ordinal);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
    }

    [Fact]
    public async Task SeedScenarioProductionRejectionRedactsSentinelInputsAndDoesNotBroadcast()
    {
        const string sentinel = "SECRET-RAW-clientIntentId";
        const string roomId = "room-SECRET-RAW-clientIntentId";
        const string playerId = "player-SECRET-RAW-clientIntentId";
        const string scenarioId = "scenario-SECRET-RAW-clientIntentId";
        const string seedId = "seed-SECRET-RAW-clientIntentId";
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-1", registry)
            .JoinRoom(roomId, playerId);
        await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "connection-2", registry)
            .JoinRoom(roomId, "P2");
        var clients = new RecordingHubClients();

        await CreateHub(
                clients,
                new RecordingGroupManager(),
                "connection-1",
                registry,
                new TestHostEnvironment(Environments.Production))
            .SeedScenario(roomId, playerId, scenarioId, seedId);

        var error = Assert.Single(clients.CallerClient.Errors);
        var payload = Assert.IsType<ErrorDto>(error.Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, payload.Code);
        Assert.Equal("载入测试状态仅在开发环境可用。", payload.Message);
        Assert.DoesNotContain("SeedScenario", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(roomId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(playerId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(scenarioId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(seedId, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIntentId", payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(clients.CallerClient.EventMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
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

    private static MatchState BuildGameHubOrderTriggersState(string roomId)
    {
        return new MatchState(
            roomId,
            12,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-ORDER-SOURCE"] = new(
                    "P1-ORDER-SOURCE",
                    cardNo: "TEST-P1-TRIGGER",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-ORDER-SOURCE"] = new(
                    "P2-ORDER-SOURCE",
                    cardNo: "TEST-P2-TRIGGER",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            triggerQueue:
            [
                new TriggerQueueItemState(
                    "TRIGGER-BATTLE-ATTACKER",
                    "P1",
                    "P1-ORDER-SOURCE",
                    "BATTLE_INITIAL_ATTACKER_REPRESENTATIVE",
                    "BATTLE_INITIAL_STACK"),
                new TriggerQueueItemState(
                    "TRIGGER-BATTLE-DEFENDER",
                    "P2",
                    "P2-ORDER-SOURCE",
                    "BATTLE_INITIAL_DEFENDER_REPRESENTATIVE",
                    "BATTLE_INITIAL_STACK")
            ]);
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

    private static void AssertProtocolDefaults(WsServerMessage message)
    {
        Assert.Equal(ProtocolDefaults.ProtocolVersion, message.ProtocolVersion);
        Assert.Equal(ProtocolDefaults.SchemaVersion, message.SchemaVersion);
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

    private sealed class FixedMatchSessionRegistry(IMatchSession session) : IMatchSessionRegistry
    {
        public ValueTask<IMatchSession> GetOrCreateAsync(string roomId, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(session);
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
            var saved = Saved.LastOrDefault(saved =>
                string.Equals(saved.RoomId, roomId, StringComparison.Ordinal)
                && string.Equals(saved.PlayerId, playerId, StringComparison.Ordinal));
            return ValueTask.FromResult(saved is not null
                && string.Equals(saved.ReconnectTokenHash, reconnectTokenHash, StringComparison.Ordinal));
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
