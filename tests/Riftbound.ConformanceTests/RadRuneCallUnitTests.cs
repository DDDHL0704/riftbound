using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadRuneCallUnitTests
{
    private const string Wyvern = "RAD·092/167";
    private const string Mobilize = "OGN·134/298";

    [Fact]
    public async Task RuntimeAdmitsTheCurrentGalleryPrintingAndPreservesTheReviewedSource()
    {
        var original = await OfficialCardCatalog.LoadDefaultAsync();
        var runtime = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.DoesNotContain(original.Cards, c => c.CardNo == Wyvern);
        var card = Assert.Single(runtime.Cards, c => c.CardNo == Wyvern);
        Assert.Equal(39594, card.Id);
        Assert.Equal("天界双足龙", card.CardName);
        Assert.Equal(7, card.Energy); Assert.Equal(7, card.Power);
        Assert.Equal("龙", card.Tag);
        Assert.Equal("如果你将召出符文，则改为召出同等数量的符文，外加一枚休眠的符文。", card.CardEffect);
        Assert.True(ReviewedRuneCardSources.TryGet(Wyvern, out var reviewed));
        Assert.Equal(40170, reviewed.Id);
        Assert.Equal(reviewed.CardEffect, card.CardEffect);
    }

    [Theory]
    [InlineData("BASE", 0)] [InlineData("BASE", 1)]
    [InlineData("BASE", 2)] [InlineData("BASE", 3)]
    [InlineData("BATTLEFIELD:BF", 0)] [InlineData("BATTLEFIELD:BF", 1)]
    [InlineData("BATTLEFIELD:BF", 2)] [InlineData("BATTLEFIELD:BF", 3)]
    public async Task NormalPlayPaysSevenAndItsExhaustedBodyAddsOnlyAnAvailableExtraRune(string destination, int available)
    {
        var initial = AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position(Wyvern), "SPELL", Mobilize, "P1");
        var cards = initial.CardObjects.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary();
        var runes = Enumerable.Range(0, available).Select(i => "WYVERN-RUNE-" + i).ToArray();
        foreach (var id in runes)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", "P1");
            locations[id] = new("P1", "RUNE_DECK");
        }
        var zones = initial.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { RuneDeck = runes };
        var game = new Driver(initial with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations });
        await game.Act("P1", new PlayCardCommand("UNIT", Wyvern, [], Destination: destination));
        Assert.Equal(13, game.State.RunePools["P1"].Mana);
        Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains("龙", game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck);
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNES_CALLED");

        await game.Act("P1", new PlayCardCommand("SPELL", Mobilize, []));
        Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck);
        await game.Top();
        var count = Math.Min(2, available);
        var called = game.State.PlayerZones["P1"].Base.Where(id => id.StartsWith("WYVERN-RUNE-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(runes.Take(count), called);
        Assert.Equal(runes.Skip(count), game.State.PlayerZones["P1"].RuneDeck);
        Assert.All(called, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.Equal(count, Convert.ToInt32(Assert.Single(game.Events(), e => e.Event.Kind == "RUNES_CALLED").Event.Payload["count"]));
        Assert.Equal(available == 0 ? new[] { "P1-DRAW-1" } : [], game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData("short-mana")] [InlineData("wrong-turn")]
    [InlineData("graveyard")] [InlineData("opponent-hand")]
    public async Task InvalidPaymentTurnOrZoneCannotChangeState(string branch)
    {
        var state = VenUnitEntryBasicTests.Position(Wyvern);
        if (branch == "short-mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(6, 20) } };
        if (branch == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "graveyard") state = state with {
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [], Graveyard = [..p.Value.Graveyard, "UNIT"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        if (branch == "opponent-hand") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["UNIT"] = state.CardObjects["UNIT"] with { OwnerId = "P2", ControllerId = "P2" } },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [] } : p.Value with { Hand = ["UNIT"] }),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P2", "HAND") } };
        var before = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("wyvern-illegal", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", Wyvern, []), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
