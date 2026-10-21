using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Riftbound.Api.Hubs;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RoomSeatPeerReceivesCurrentPrivateViewsWithoutRequestingSnapshot(bool publicRoom, bool deckBeforeOpponent)
    {
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var directory = new InMemoryPublicMatchDirectory();
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var routes = new Dictionary<string, RecordingGameClient>(StringComparer.Ordinal);
        var firstClients = new RoomSeatRoutingClients(routes);
        var secondClients = new RoomSeatRoutingClients(routes);
        var first = RoomSeatHub(registry, firstClients, "peer-a", identity, directory);
        var second = RoomSeatHub(registry, secondClients, "peer-b", identity, directory);
        Assert.True((await first.Authenticate("alice", "room-seat-alice-private-key")).Authenticated);
        Assert.True((await second.Authenticate("bob", "room-seat-bob-private-key")).Authenticated);
        var room = "room-seat-peer-ordinary";
        if (publicRoom)
        {
            var created = await first.CreatePublicMatch("alice");
            Assert.NotNull(created);
            room = created.Match.RoomId;
        }
        else await first.JoinRoom(room, "alice");
        routes[$"room:{room}:player:alice"] = firstClients.CallerClient;
        routes[$"room:{room}:player:bob"] = secondClients.CallerClient;
        Assert.Single(firstClients.CallerClient.Snapshots);
        Assert.Empty(firstClients.RequestedGroups);
        var deck = BuildValidDeck(await OfficialCardCatalog.LoadDefaultAsync(default));
        if (deckBeforeOpponent)
        {
            var submitted = await first.SubmitIntent(room, "alice", "first-original-deck",
                RoomSeatDeckIntent(deck, Assert.IsType<ActionPromptDto>(firstClients.CallerClient.Prompts[^1].Payload)));
            Assert.True(submitted.Accepted);
        }
        var previousPrompt = Assert.IsType<ActionPromptDto>(firstClients.CallerClient.Prompts[^1].Payload);
        var previousSnapshotCount = firstClients.CallerClient.Snapshots.Count;
        var previousPromptCount = firstClients.CallerClient.Prompts.Count;

        await second.JoinRoom(room, "bob");

        var session = Assert.IsType<MatchSession>(await registry.FindAsync(room, default));
        var currentTick = session.SnapshotFor("alice").Tick;
        Assert.Equal(previousPrompt.SnapshotTick + 1, currentTick);
        Assert.Equal(previousSnapshotCount + 1, firstClients.CallerClient.Snapshots.Count);
        Assert.Equal(previousPromptCount + 1, firstClients.CallerClient.Prompts.Count);
        Assert.Equal([ $"room:{room}:player:alice" ], secondClients.RequestedGroups);
        Assert.Single(secondClients.CallerClient.JoinedMessages);
        Assert.Single(secondClients.CallerClient.Snapshots);
        Assert.Single(secondClients.CallerClient.Prompts);
        foreach (var (actor, client) in new[] { ("alice", firstClients), ("bob", secondClients) })
        {
            var snapshot = client.CallerClient.Snapshots[^1];
            var prompt = client.CallerClient.Prompts[^1];
            Assert.Equal(actor, snapshot.PlayerId);
            Assert.Equal(actor, prompt.PlayerId);
            Assert.Equal(currentTick, snapshot.ServerTick);
            Assert.Equal(currentTick, prompt.ServerTick);
            Assert.Equal(MatchStateHasher.HashValue(session.SnapshotFor(actor)), MatchStateHasher.HashValue(snapshot.Payload));
            Assert.Equal(MatchStateHasher.HashValue(session.PromptFor(actor)), MatchStateHasher.HashValue(prompt.Payload));
            Assert.Empty(client.Unscoped.Snapshots);
            Assert.Empty(client.Unscoped.Prompts);
        }
        Assert.Equal(2, journal.Entries.Count(e => e.CommandType == "ROOM_SEAT"));
        Assert.Equal(deckBeforeOpponent ? 3 : 2, journal.Entries.Count);
        if (!deckBeforeOpponent)
        {
            var stale = await first.SubmitIntent(room, "alice", "retained-original-stale-deck", RoomSeatDeckIntent(deck, previousPrompt));
            Assert.False(stale.Accepted);
            Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode);
            Assert.Equal(currentTick, session.SnapshotFor("alice").Tick);
            var delivered = Assert.IsType<ActionPromptDto>(firstClients.CallerClient.Prompts[^1].Payload);
            Assert.Equal(currentTick, delivered.SnapshotTick);
            var accepted = await first.SubmitIntent(room, "alice", "fresh-delivered-deck", RoomSeatDeckIntent(deck, delivered));
            Assert.True(accepted.Accepted);
            Assert.Equal(currentTick + 1, accepted.ServerTick);
            Assert.Equal(4, journal.Entries.Count);
            Assert.False(journal.Entries[^2].Accepted);
            Assert.True(journal.Entries[^1].Accepted);
        }
    }

    [Theory]
    [InlineData("alice", "bob")]
    [InlineData("bob", "alice")]
    public async Task RoomSeatPeerDeliveryUsesActualSeatedIdentityInEitherOrder(string firstActor, string secondActor)
    {
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        var routes = new Dictionary<string, RecordingGameClient>(StringComparer.Ordinal);
        var firstClients = new RoomSeatRoutingClients(routes);
        var secondClients = new RoomSeatRoutingClients(routes);
        const string room = "room-seat-peer-reverse";
        routes[$"room:{room}:player:{firstActor}"] = firstClients.CallerClient;
        routes[$"room:{room}:player:{secondActor}"] = secondClients.CallerClient;
        await RoomSeatHub(registry, firstClients, "first", actor: firstActor).JoinRoom(room, firstActor);
        await RoomSeatHub(registry, secondClients, "second", actor: secondActor).JoinRoom(room, secondActor);
        Assert.Equal([ $"room:{room}:player:{firstActor}" ], secondClients.RequestedGroups);
        Assert.Equal(firstActor, firstClients.CallerClient.Prompts[^1].PlayerId);
        Assert.Equal(2, Assert.IsType<ActionPromptDto>(firstClients.CallerClient.Prompts[^1].Payload).SnapshotTick);
        Assert.Empty(secondClients.Unscoped.Snapshots);
        Assert.Empty(secondClients.Unscoped.Prompts);
    }

    [Fact]
    public async Task RoomSeatPeerRefreshKeepsEachExistingPlayersHiddenHandAndDeckPrivate()
    {
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var routes = new Dictionary<string, RecordingGameClient>(StringComparer.Ordinal);
        var clients = new RoomSeatRoutingClients(routes);
        var peerClients = new RoomSeatRoutingClients(routes);
        var peer = peerClients.CallerClient;
        const string room = "room-seat-peer-original-private-opening";
        routes[$"room:{room}:player:alice"] = clients.CallerClient;
        routes[$"room:{room}:player:bob"] = peer;
        var first = RoomSeatHub(registry, clients, "known-player", actor: "alice");
        var second = RoomSeatHub(registry, peerClients, "peer", actor: "bob");
        await first.JoinRoom(room, "alice");
        await second.JoinRoom(room, "bob");
        var deck = BuildValidDeck(await OfficialCardCatalog.LoadDefaultAsync(default));
        Assert.True((await first.SubmitIntent(room, "alice", "private-first-deck",
            RoomSeatDeckIntent(deck, Assert.IsType<ActionPromptDto>(clients.CallerClient.Prompts[^1].Payload)))).Accepted);
        Assert.True((await second.SubmitIntent(room, "bob", "private-second-deck",
            RoomSeatDeckIntent(deck, Assert.IsType<ActionPromptDto>(peer.Prompts[^1].Payload)))).Accepted);
        Assert.True((await first.Ready(room, "alice", "private-first-ready")).Accepted);
        Assert.True((await second.Ready(room, "bob", "private-second-ready")).Accepted);
        var initial = journal.Entries[^1].AuthoritativeState;
        Assert.NotEmpty(initial.PlayerZones["alice"].Hand);
        Assert.NotEmpty(initial.PlayerZones["alice"].MainDeck);
        var count = journal.Entries.Count;
        peer.Snapshots.Clear();
        peer.Prompts.Clear();
        clients.RequestedGroups.Clear();
        await first.JoinRoom(room, "alice");
        var snapshot = Assert.Single(peer.Snapshots);
        var prompt = Assert.Single(peer.Prompts);
        Assert.Equal("bob", snapshot.PlayerId);
        Assert.Equal("bob", prompt.PlayerId);
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(initial)["bob"]), MatchStateHasher.HashValue(snapshot.Payload));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(initial)["bob"]), MatchStateHasher.HashValue(prompt.Payload));
        var serialized = JsonSerializer.Serialize(new[] { snapshot, prompt });
        foreach (var hidden in initial.PlayerZones["alice"].Hand.Concat(initial.PlayerZones["alice"].MainDeck))
            Assert.DoesNotContain('"' + hidden + '"', serialized, StringComparison.Ordinal);
        Assert.Empty(clients.Unscoped.Snapshots);
        Assert.Empty(clients.Unscoped.Prompts);
        Assert.Equal(count, journal.Entries.Count);
        Assert.Equal(initial.Tick, (await registry.FindAsync(room, default))!.SnapshotFor("alice").Tick);
    }

    [Theory]
    [InlineData("third")]
    [InlineData("identity")]
    [InlineData("unauthenticated")]
    public async Task RoomSeatPeerViewsAreNotPublishedForRejectedJoin(string rejection)
    {
        const string room = "room-seat-peer-rejected";
        var journal = new RecordingMatchJournal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        foreach (var actor in new[] { "alice", "bob" })
            await CreateHub(new RecordingHubClients(), new RecordingGroupManager(), actor, registry, authenticatedHandle: actor).JoinRoom(room, actor);
        var session = (await registry.FindAsync(room, default))!;
        var original = MatchStateHasher.HashValue(session.SnapshotFor("alice"));
        var clients = new RoomSeatRoutingClients(new());
        var hub = RoomSeatHub(registry, clients, "rejected", actor: rejection == "unauthenticated" ? null : rejection == "identity" ? "intruder" : "charlie");
        await hub.JoinRoom(room, "charlie");
        var error = Assert.IsType<ErrorDto>(Assert.Single(clients.CallerClient.Errors).Payload);
        Assert.Equal(rejection == "third" ? ErrorCodes.RoomFull : rejection == "identity" ? ErrorCodes.IdentityMismatch : ErrorCodes.AuthenticationRequired, error.Code);
        Assert.Empty(clients.RequestedGroups);
        Assert.Empty(clients.CallerClient.JoinedMessages);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Empty(clients.CallerClient.Prompts);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(original, MatchStateHasher.HashValue(session.SnapshotFor("alice")));
    }

    private static JsonElement RoomSeatDeckIntent(OfficialDecklist deck, ActionPromptDto prompt)
    {
        var raw = JsonNode.Parse(SubmitDeckJson(deck).GetRawText())!.AsObject();
        raw["promptId"] = prompt.PromptId;
        raw["snapshotTick"] = prompt.SnapshotTick;
        return JsonSerializer.SerializeToElement(raw);
    }

    private static GameHub RoomSeatHub(IMatchSessionRegistry registry, RoomSeatRoutingClients clients,
        string connection, PlayerIdentityService? identity = null, IPublicMatchDirectory? directory = null, string? actor = null)
        => new(registry, playerIdentity: identity, publicMatches: directory)
        {
            Clients = clients, Groups = new RecordingGroupManager(), Context = new TestHubCallerContext(connection, actor)
        };

    private sealed class RoomSeatRoutingClients(Dictionary<string, RecordingGameClient> routes) : IHubCallerClients<IGameClient>
    {
        public RecordingGameClient CallerClient { get; } = new();
        public RecordingGameClient Unscoped { get; } = new();
        public List<string> RequestedGroups { get; } = [];
        public IGameClient All => Unscoped;
        public IGameClient Caller => CallerClient;
        public IGameClient Others => Unscoped;
        public IGameClient AllExcept(IReadOnlyList<string> ids) => Unscoped;
        public IGameClient Client(string id) => Unscoped;
        public IGameClient Clients(IReadOnlyList<string> ids) => Unscoped;
        public IGameClient Group(string name)
        {
            RequestedGroups.Add(name);
            return routes.TryGetValue(name, out var client) ? client : Unscoped;
        }
        public IGameClient GroupExcept(string name, IReadOnlyList<string> ids) => Unscoped;
        public IGameClient Groups(IReadOnlyList<string> names) => Unscoped;
        public IGameClient OthersInGroup(string name) => Unscoped;
        public IGameClient User(string id) => Unscoped;
        public IGameClient Users(IReadOnlyList<string> ids) => Unscoped;
    }
}
