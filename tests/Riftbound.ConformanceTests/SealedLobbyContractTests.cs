using System.Text.Json;
using Riftbound.Api;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
    public static IEnumerable<object[]> InvalidMixedPackOptions()
    {
        yield return [new CreatePublicMatchOptionsDto("sealed", null, 1, [new("UNL", 6)]), "系列 UNL"];
        yield return [new CreatePublicMatchOptionsDto("sealed", null, 1, [new("OGN", 3), new("OGN", 3)]), "不能重复"];
        yield return [new CreatePublicMatchOptionsDto("sealed", null, 1, [new("OGN", 5)]), "6 至 12"];
        yield return [new CreatePublicMatchOptionsDto("sealed", null, 1, [new("OGN", 13)]), "6 至 12"];
        yield return [new CreatePublicMatchOptionsDto("sealed", null, 1, [new("OGN", 0), new("SFD", 6)]), "至少为 1"];
        yield return [new CreatePublicMatchOptionsDto("sealed", "OGN", 1, [new("OGN", 6)]), "不能同时指定"];
    }

    [Theory]
    [MemberData(nameof(InvalidMixedPackOptions))]
    public async Task InvalidMixedPackChoicesAreRejectedBeforeRoomCreationOrDirectoryPublication(
        CreatePublicMatchOptionsDto options, string expectedMessage)
    {
        var error = Assert.Throws<ArgumentException>(() => PublicMatchCreation.Normalize(options));
        Assert.Contains(expectedMessage, error.Message);
        var directory = new InMemoryPublicMatchDirectory();
        var directoryError = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await directory.CreateAsync("mixed-invalid", "alice", options, default));
        Assert.Contains(expectedMessage, directoryError.Message);
        Assert.Empty(await directory.ListOpenAsync(default));

        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        var hub = CreateHub(clients, groups, "mixed-invalid", registry,
            playerIdentity: identity, publicMatches: directory);
        await hub.Authenticate("Alice", "alice-mixed-invalid-key");
        Assert.Null(await hub.CreatePublicMatchWithOptions("Alice", options));
        Assert.Empty(await directory.ListOpenAsync(default));
        Assert.Empty(groups.Added);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.IsType<ErrorDto>(Assert.Single(clients.CallerClient.Errors).Payload);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(12)]
    public void MixedPackChoicesAcceptTheInclusivePackCountBounds(int count)
    {
        var normalized = PublicMatchCreation.Normalize(new(MatchFormats.Sealed, null, 1, [new("OGN", count)]));
        Assert.Equal(count, normalized.Packs!.Sum(choice => choice.Count));
        Assert.Null(normalized.SetCode);
    }

    [Fact]
    public async Task SealedLobbyOptionsDefaultToConstructedWithoutChangingTheOriginalRoomFlow()
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var directory = new InMemoryPublicMatchDirectory();
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var clients = new RecordingHubClients();
        var hub = CreateHub(clients, new RecordingGroupManager(), "options-connection", registry,
            playerIdentity: identity, publicMatches: directory);
        await hub.Authenticate("Alice", "alice-options-private-key");

        var created = await hub.CreatePublicMatchWithOptions("Alice", new CreatePublicMatchOptionsDto());

        Assert.NotNull(created);
        Assert.Equal(MatchFormats.Constructed, created.Match.Format);
        Assert.Null(created.Match.SetCode);
        Assert.Equal(1, created.Match.BestOf);
        Assert.Single(await directory.ListOpenAsync(default));
        Assert.Single(clients.CallerClient.Snapshots);
    }

    [Theory]
    [InlineData("sealed", "RAD", 1)]
    [InlineData("sealed", null, 3)]
    [InlineData("sealed", "OGN", 2)]
    [InlineData("constructed", null, 3)]
    [InlineData("invalid", null, 1)]
    public async Task SealedLobbyUnavailableModesRejectBeforeCreatingOrPublishingARoom(string format, string? setCode, int bestOf)
    {
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var directory = new InMemoryPublicMatchDirectory();
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        var hub = CreateHub(clients, groups, "options-connection", registry,
            playerIdentity: identity, publicMatches: directory);
        await hub.Authenticate("Alice", "alice-options-private-key");

        var created = await hub.CreatePublicMatchWithOptions("Alice", new(format, setCode, bestOf));

        Assert.Null(created);
        Assert.Empty(await directory.ListOpenAsync(default));
        Assert.Empty(groups.Added);
        Assert.Empty(clients.CallerClient.Snapshots);
        Assert.Equal(ErrorCodes.InvalidPayload, Assert.IsType<ErrorDto>(Assert.Single(clients.CallerClient.Errors).Payload).Code);
    }

    [Theory]
    [InlineData("OGN", 1)]
    [InlineData("SFD", 3)]
    public async Task SealedLobbyCreatesListsAndSeatsRealPrivatePoolsAndEnablesTheDesignatedOrderChoice(string setCode, int bestOf)
    {
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        var directory = new InMemoryPublicMatchDirectory();
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var aliceClients = new RecordingHubClients();
        var alice = CreateHub(aliceClients, new RecordingGroupManager(), "sealed-alice", registry,
            playerIdentity: identity, publicMatches: directory);
        await alice.Authenticate("Alice", "alice-sealed-private-key");
        var created = await alice.CreatePublicMatchWithOptions("Alice", new("sealed", setCode, bestOf));
        Assert.NotNull(created);
        var listed = Assert.Single(await directory.ListOpenAsync(default));
        Assert.Equal(created.Match, listed);
        Assert.Equal(MatchFormats.Sealed, listed.Format);
        Assert.Equal(setCode, listed.SetCode);
        Assert.Equal(bestOf, listed.BestOf);
        var own = Assert.IsType<SnapshotDto>(Assert.Single(aliceClients.CallerClient.Snapshots).Payload);
        Assert.Equal(setCode, own.SealedPool!.SetCode);
        Assert.Equal(6, own.SealedPool.Packs.Count);
        Assert.Null(own.Series);

        var bobClients = new RecordingHubClients();
        var bob = CreateHub(bobClients, new RecordingGroupManager(), "sealed-bob", registry,
            playerIdentity: identity, publicMatches: directory);
        await bob.Authenticate("Bob", "bob-sealed-private-key");
        await bob.JoinRoom(created.Match.RoomId, "Bob");
        Assert.Empty(await directory.ListOpenAsync(default));
        var session = Assert.IsAssignableFrom<IMatchSession>(await registry.FindAsync(created.Match.RoomId, default));
        own = session.SnapshotFor("bob");
        Assert.Equal(MatchFormats.Sealed, own.Format);
        Assert.Equal(setCode, own.SetCode);
        Assert.Equal(bestOf, own.BestOf);
        Assert.Equal(6, own.SealedPool!.Packs.Count);
        var chooser = own.Series!.PlayOrderChooserPlayerId!;
        var chooserHub = chooser == "alice" ? alice : bob;
        var receipt = await chooserHub.SubmitIntent(created.Match.RoomId, chooser, "public-choose", System.Text.Json.JsonSerializer.SerializeToElement(
            new { cmdType = CommandTypes.ChoosePlayOrder, goFirst = true }));
        Assert.True(receipt.Accepted, receipt.Message);
        Assert.Equal(MatchSeriesPhases.SubmittingDecks, session.SnapshotFor("alice").Series!.Phase);
        Assert.Empty(aliceClients.CallerClient.Errors);
        Assert.Empty(bobClients.CallerClient.Errors);
    }

    [Fact]
    public async Task MixedPackLobbyListsTheOrderedCompositionAndKeepsEachPoolPrivate()
    {
        var choices = new[] { new SealedPackChoiceDto("OGN", 3), new SealedPackChoiceDto("SFD", 3) };
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        var directory = new InMemoryPublicMatchDirectory();
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var aliceClients = new RecordingHubClients();
        var alice = CreateHub(aliceClients, new RecordingGroupManager(), "mixed-alice", registry,
            playerIdentity: identity, publicMatches: directory);
        await alice.Authenticate("Alice", "alice-mixed-private-key");

        var created = await alice.CreatePublicMatchWithOptions("Alice", new(MatchFormats.Sealed, null, 1, choices));

        Assert.NotNull(created);
        Assert.Null(created.Match.SetCode);
        Assert.Equal(choices, created.Match.SealedPacks);
        Assert.Equal(created.Match, Assert.Single(await directory.ListOpenAsync(default)));
        var aliceSession = Assert.IsType<SnapshotDto>(Assert.Single(aliceClients.CallerClient.Snapshots).Payload);
        Assert.Contains("\"sealedPacks\"", JsonSerializer.Serialize(aliceSession, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Null(aliceSession.SetCode);
        Assert.Equal(choices, aliceSession.SealedPacks);
        Assert.Equal(string.Empty, aliceSession.SealedPool!.SetCode);
        Assert.Equal(["OGN", "OGN", "OGN", "SFD", "SFD", "SFD"],
            aliceSession.SealedPool.Packs.Select(pack => pack.SetCode));

        var bobClients = new RecordingHubClients();
        var bob = CreateHub(bobClients, new RecordingGroupManager(), "mixed-bob", registry,
            playerIdentity: identity, publicMatches: directory);
        await bob.Authenticate("Bob", "bob-mixed-private-key");
        await bob.JoinRoom(created.Match.RoomId, "Bob");
        var session = Assert.IsAssignableFrom<IMatchSession>(await registry.FindAsync(created.Match.RoomId, default));
        var bobSnapshot = session.SnapshotFor("bob");
        Assert.Equal(["OGN", "OGN", "OGN", "SFD", "SFD", "SFD"],
            bobSnapshot.SealedPool!.Packs.Select(pack => pack.SetCode));
        Assert.NotEqual(JsonSerializer.Serialize(session.SnapshotFor("alice").SealedPool),
            JsonSerializer.Serialize(bobSnapshot.SealedPool));
        Assert.NotEqual(JsonSerializer.Serialize(session.SnapshotFor("alice").SealedPool),
            JsonSerializer.Serialize(bobSnapshot.SealedPool));
        Assert.Empty(await directory.ListOpenAsync(default));
    }
}
