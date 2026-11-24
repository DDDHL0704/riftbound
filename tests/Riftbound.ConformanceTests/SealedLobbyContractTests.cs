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
        yield return [new CreatePublicMatchOptionsDto("sealed", null, 1, [new("XYZ", 6)]), "卡包组合中有尚未开放现开赛的系列。"];
        yield return [new CreatePublicMatchOptionsDto("sealed", null, 1, [new("OGN", 3), new("OGN", 3)]), "自定义卡包组合中的系列不能重复。"];
        yield return [new CreatePublicMatchOptionsDto("sealed", null, 1, [new("OGN", 5)]), "自定义卡包组合的总包数必须为 6 至 12 包。"];
        yield return [new CreatePublicMatchOptionsDto("sealed", null, 1, [new("OGN", 13)]), "自定义卡包组合的总包数必须为 6 至 12 包。"];
        yield return [new CreatePublicMatchOptionsDto("sealed", null, 1, [new("OGN", 0), new("SFD", 6)]), "每个系列的卡包数量必须至少为 1。"];
        yield return [new CreatePublicMatchOptionsDto("sealed", "OGN", 1, [new("OGN", 6)]), "请选择自定义卡包组合或单一系列。"];
    }

    [Theory]
    [MemberData(nameof(InvalidMixedPackOptions))]
    public async Task InvalidMixedPackChoicesAreRejectedBeforeRoomCreationOrDirectoryPublication(
        CreatePublicMatchOptionsDto options, string expectedMessage)
    {
        var error = Assert.Throws<ArgumentException>(() => PublicMatchCreation.Normalize(options));
        Assert.Equal(expectedMessage, error.Message);
        var directory = new InMemoryPublicMatchDirectory();
        var directoryError = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await directory.CreateAsync("mixed-invalid", "alice", options, default));
        Assert.Equal(expectedMessage, directoryError.Message);
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
        var clientError = Assert.IsType<ErrorDto>(Assert.Single(clients.CallerClient.Errors).Payload);
        Assert.Equal(ErrorCodes.InvalidPayload, clientError.Code);
        Assert.Equal(expectedMessage, clientError.Message);
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

    private const string InvalidRoomOptionsMessage =
        "建房参数无效：构筑赛只支持单局制；现开赛须指定已开放系列或卡包组合及 1 或 3 局制。";

    [Theory]
    [InlineData("sealed", "unknown", 1, "该系列尚未开放现开赛。")]
    [InlineData("sealed", null, 3, InvalidRoomOptionsMessage)]
    [InlineData("sealed", "OGN", 2, "现开比赛只支持单局制或三局两胜。")]
    [InlineData("constructed", null, 3, InvalidRoomOptionsMessage)]
    [InlineData("invalid", null, 1, InvalidRoomOptionsMessage)]
    public async Task SealedLobbyUnavailableModesRejectBeforeCreatingOrPublishingARoom(
        string format, string? setCode, int bestOf, string expectedMessage)
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
        var error = Assert.IsType<ErrorDto>(Assert.Single(clients.CallerClient.Errors).Payload);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Equal(expectedMessage, error.Message);
    }

    [Theory]
    [InlineData((OfficialDeckFormat)(-1), null, "房间的卡组格式无效。")]
    [InlineData(OfficialDeckFormat.Sealed, null, "现开房间必须校验卡组并隐藏提交内容。")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, "unknown", "未知的开局规则。")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, OpeningRules.S5CityMode1, "城市挑战赛测试开局只支持提交正式卡组的构筑单局制。")]
    public async Task MatchmakingRoomRejectionSendsOnlyTheChineseReason(
        OfficialDeckFormat deckFormat, string? openingRule, string expectedMessage)
    {
        var options = new MatchSessionOptions(DeckFormat: deckFormat) { OpeningRule = openingRule };
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchRecoveryStore.Instance, NoopMatchPlayerStore.Instance, options);
        var queue = new InMemoryMatchmakingQueue(registry, () => "RB-MATCH1");
        Assert.Equal(MatchmakingStates.Queued, (await queue.EnqueueAsync("alice", default)).State);
        var identity = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        var clients = new RecordingHubClients();
        var hub = CreateHub(clients, new RecordingGroupManager(), "room-rejection", registry,
            playerIdentity: identity, matchmakingQueue: queue);
        Assert.True((await hub.Authenticate("Bob", "bob-room-rejection-key")).Authenticated);

        var result = await hub.EnqueueMatchmaking("Bob");

        Assert.Equal(MatchmakingStates.Rejected, result.State);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal(expectedMessage, result.Message);
        var error = Assert.IsType<ErrorDto>(Assert.Single(clients.CallerClient.Errors).Payload);
        Assert.Equal(ErrorCodes.UnsupportedCommand, error.Code);
        Assert.Equal(expectedMessage, error.Message);
        Assert.DoesNotContain("(Parameter", error.Message, StringComparison.Ordinal);
        Assert.Null(await registry.FindAsync("RB-MATCH1", default));
    }

    [Theory]
    [InlineData("OGN", 1)]
    [InlineData("SFD", 3)]
    [InlineData("UNL", 1)]
    [InlineData("VEN", 3)]
    [InlineData("RAD", 1)]
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
        Assert.Equal(78, own.SealedPool.Pool.Sum(card => card.Count));
        Assert.All(own.SealedPool.Packs, pack => Assert.Equal(setCode, pack.SetCode));
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
        Assert.Equal(78, own.SealedPool.Pool.Sum(card => card.Count));
        Assert.All(own.SealedPool.Packs, pack => Assert.Equal(setCode, pack.SetCode));
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
