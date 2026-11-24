using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
    [Fact]
    public async Task OneQuoteCanValidateAddedTapsWithoutChangingTheRoom()
    {
        var state = PlayCostPreviewTests.Position(new(0, 1));
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value);
        var runes = new[] { "RUNE-1", "RUNE-2", "RUNE-3", "RUNE-4" };
        foreach (var id in runes)
            cards[id] = new(id, cardNo: "OGN·228/298", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.RuneCard, "COLOR:purple"]);
        state = state with
        {
            CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P1"] = state.PlayerZones["P1"] with { Base = runes }
            }
        };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        var clients = new RecordingHubClients();
        var hub = CreateHub(clients, new RecordingGroupManager(), "quote-with-taps", new FixedMatchSessionRegistry(session));
        var request = PlayCostPreviewTests.Request(state, PlayCostPreviewTests.Command([]));
        var taps = runes.Select(id => "TAP_RUNE:" + id).ToArray();
        Assert.Equal(ErrorCodes.AuthenticationRequired,
            (await hub.PreviewPlayCardWithRuneTaps(state.RoomId, request, taps)).Quote.ErrorCode);
        hub.Context.Items["riftbound:authenticatedHandle"] = "P1";

        var snapshotBefore = JsonSerializer.Serialize(session.SnapshotFor("P1"));
        var initial = await hub.PreviewPlayCard(state.RoomId, request);
        Assert.Equal(4, initial.Cost!.MissingMana);
        var insufficient = await hub.PreviewPlayCardWithRuneTaps(state.RoomId, request, [taps[0], taps[0]]);
        Assert.Empty(insufficient.AddedOptionalCosts);
        Assert.Equal(JsonSerializer.Serialize(initial), JsonSerializer.Serialize(insufficient.Quote));

        var result = await hub.PreviewPlayCardWithRuneTaps(state.RoomId, request, taps);
        var previousSecondQuote = await hub.PreviewPlayCard(state.RoomId,
            request with { Command = request.Command with { OptionalCosts = taps } });
        Assert.Equal(taps, result.AddedOptionalCosts);
        Assert.Equal(JsonSerializer.Serialize(previousSecondQuote), JsonSerializer.Serialize(result.Quote));
        Assert.True(result.Quote.CanPay, result.Quote.Message);
        Assert.Empty(journal.Entries);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
        Assert.Equal(snapshotBefore, JsonSerializer.Serialize(session.SnapshotFor("P1")));
        Assert.Equal(request.PromptId, session.PromptFor("P1").PromptId);

        var invalid = await hub.PreviewPlayCardWithRuneTaps(state.RoomId, request, [.. taps[..3], "TAP_RUNE:UNKNOWN"]);
        Assert.False(invalid.Quote.IsValid);
        Assert.Empty(invalid.AddedOptionalCosts);
        var stale = await hub.PreviewPlayCardWithRuneTaps(state.RoomId, request with { SnapshotTick = state.Tick - 1 }, taps);
        Assert.Equal(ErrorCodes.PromptExpired, stale.Quote.ErrorCode);
        Assert.Empty(stale.AddedOptionalCosts);
    }
}
