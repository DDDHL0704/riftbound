using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven005And006PaidBeginningConsumers
{
    [Theory]
    [InlineData("VEN·005", 37690, 2, 2)]
    [InlineData("VEN·006", 37691, 4, 4)]
    public void ExactReviewedPrintingIsAPlayableUnit(string no, long id, int mana, int might)
    {
        Assert.True(ReviewedRuneCardSources.TryGet(no, out var source));
        Assert.Equal(id, source.Id); Assert.Equal(mana, source.Energy); Assert.Equal(might, source.Power);
        Assert.Equal(["red"], source.CardColorList); Assert.Null(source.ReturnEnergy);
        Assert.Equal(0, PrintedPowerCostRules.ForCard(no).Amount);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(mana, behavior.ManaCost);
        Assert.Equal(might, behavior.SourceUnitPower);
    }

    [Theory]
    [InlineData("VEN·005", 2, 2, 1, false, false)]
    [InlineData("VEN·005", 2, 2, 1, false, true)]
    [InlineData("VEN·006", 4, 4, 2, true, false)]
    [InlineData("VEN·006", 4, 4, 2, true, true)]
    public async Task PaidPhysicalEntryFeedsTheNextRealBeginningCondition(
        string no, int mana, int might, int bonus, bool roam, bool equal)
    {
        var game = new Driver(Position(no));
        var initialGeneration = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.TapRunes("P1", Runes(game.State, "P1").Take(mana));
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal("BASE", game.State.ObjectLocations["UNIT"].Zone);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(initialGeneration + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Equal(might, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);

        await game.Act("P1", new EndTurnCommand());
        Assert.Equal("P2", game.State.TurnPlayerId);
        Assert.Equal(6, Runes(game.State, "P1").Length);
        Assert.Equal(8, Runes(game.State, "P2").Length);
        Assert.Equal(might, game.State.CardObjects["UNIT"].Power);
        if (equal)
        {
            foreach (var id in Runes(game.State, "P2").Take(2)) await game.Act("P2", new RecycleRuneCommand(id));
            Assert.Equal(6, Runes(game.State, "P2").Length);
        }
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId);
        if (equal)
        {
            Assert.Equal(MatchPhases.Main, game.State.Phase);
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            Assert.Equal(might, game.State.CardObjects["UNIT"].Power);
            Assert.DoesNotContain("ROAM", game.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        }
        else
        {
            Assert.Equal(MatchPhases.TurnStart, game.State.Phase);
            Assert.Equal(6, Runes(game.State, "P1").Length);
            Assert.Equal(8, Runes(game.State, "P2").Length);
            var item = Assert.Single(game.State.StackItems);
            Assert.Equal("UNIT", item.SourceObjectId); Assert.Equal(no, item.BeginningRune!.CardNo);
            Assert.Equal(initialGeneration + 1, item.BeginningRune.Source.Generation);
            Assert.Equal(might, game.State.CardObjects["UNIT"].Power);
            await game.Top();
            Assert.Equal(MatchPhases.Main, game.State.Phase);
            Assert.Equal(might + bonus, game.State.CardObjects["UNIT"].Power);
            Assert.Equal(roam, game.State.CardObjects["UNIT"].UntilEndOfTurnEffects.Contains("ROAM"));
        }
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(might, game.State.CardObjects["UNIT"].Power);
        Assert.DoesNotContain("ROAM", game.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        Assert.Equal(initialGeneration + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
        await game.Replay();
    }

    private static string[] Runes(MatchState state, string player) => state.PlayerZones[player].Base
        .Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();

    private static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        foreach (var player in new[] { "P1", "P2" })
        {
            var baseRunes = new List<string>(); var deckRunes = new List<string>();
            for (var i = 0; i < 12; i++)
            {
                var id = player + "-BEGINNING-RUNE-" + i;
                cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, player == "P1" ? "UNL-R01" : "UNL-R04", player);
                locations[id] = new(player, i < 6 ? "BASE" : "RUNE_DECK");
                (i < 6 ? baseRunes : deckRunes).Add(id);
            }
            zones[player] = zones[player] with { Base = [..zones[player].Base, ..baseRunes], RuneDeck = deckRunes };
        }
        return state with { RoomId = "VEN-PAID-BEGINNING-" + no, CardObjects = cards, ObjectLocations = locations,
            PlayerZones = zones, RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }
}
