using Riftbound.Api;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
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
    [InlineData("sealed", "OGN", 1)]
    [InlineData("sealed", "SFD", 3)]
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
}
