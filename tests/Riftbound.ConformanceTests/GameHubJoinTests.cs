using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
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
        var snapshotMessage = Assert.Single(clients.CallerClient.Snapshots);
        var promptMessage = Assert.Single(clients.CallerClient.Prompts);
        Assert.Equal("alice", snapshotMessage.PlayerId);
        Assert.Equal("alice", promptMessage.PlayerId);

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
        Assert.Equal("room already has two players", error.Payload);
    }

    private static GameHub CreateHub(
        RecordingHubClients clients,
        RecordingGroupManager groups,
        string connectionId,
        IMatchSessionRegistry? registry = null)
    {
        return new GameHub(registry ?? new InMemoryMatchSessionRegistry(
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance))
        {
            Clients = clients,
            Groups = groups,
            Context = new TestHubCallerContext(connectionId)
        };
    }

    private sealed class RecordingGameClient : IGameClient
    {
        public List<WsServerMessage> Snapshots { get; } = [];

        public List<WsServerMessage> Prompts { get; } = [];

        public List<WsServerMessage> EventMessages { get; } = [];

        public List<WsServerMessage> Errors { get; } = [];

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

        private RecordingGameClient BroadcastClient { get; } = new();

        public IGameClient All => BroadcastClient;

        public IGameClient Caller => CallerClient;

        public IGameClient Others => BroadcastClient;

        public IGameClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => BroadcastClient;

        public IGameClient Client(string connectionId) => BroadcastClient;

        public IGameClient Clients(IReadOnlyList<string> connectionIds) => BroadcastClient;

        public IGameClient Group(string groupName) => BroadcastClient;

        public IGameClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => BroadcastClient;

        public IGameClient Groups(IReadOnlyList<string> groupNames) => BroadcastClient;

        public IGameClient OthersInGroup(string groupName) => BroadcastClient;

        public IGameClient User(string userId) => BroadcastClient;

        public IGameClient Users(IReadOnlyList<string> userIds) => BroadcastClient;
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
}
