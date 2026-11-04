using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class MordekaiserSacrificeRecallTests
{
    // DOC-17 p10: the same minion cannot pay for three simultaneous replacements.
    [Theory]
    [InlineData("ALLY", "ALLY2")]
    [InlineData("ALLY2", "ALLY")]
    public async Task ThreeSimultaneouslyDestroyedHeroesCannotShareOneMinion(string first, string second)
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", No, "P1"),
            ["ALLY2"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY2", No, "P1") } };
        foreach (var id in new[] { "UNIT", "ALLY", "ALLY2" })
            state = LocalDestructionRecallTests.At(state, id, "P1", "BATTLEFIELD", "BF");
        state = FaqMinion(state, "MINION", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "RUIN", "UNL-180/219", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("RUIN", "UNL-180/219", []));
        await game.Top();
        await FaqReplacement(game, "UNIT", "MINION");
        Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options,
            option => option.Id.EndsWith(":MINION", StringComparison.Ordinal));
        await FaqReplacement(game, first, second);
        // Actually destroy the other hero to pay, rather than replacing its cost again.
        var nested = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Contains(nested.Options, option => option.Id == "DECLINE");
        var decline = new PayCostCommand(nested.Id, "RULE_REPLACEMENT", ["DECLINE"]);
        await Reject(game.State, "P2", decline);
        await game.Act("P1", decline);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Empty(game.State.StackItems);
        var heroes = new[] { "UNIT", "ALLY", "ALLY2" };
        Assert.Equal(2, heroes.Count(id => game.State.PlayerZones["P1"].Base.Contains(id)));
        Assert.Contains(second, game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("MINION", game.State.CardObjects.Keys);
        foreach (var id in new[] { "UNIT", first })
        {
            Assert.True(game.State.CardObjects[id].IsExhausted);
            Assert.Equal(0, game.State.CardObjects[id].Damage);
            Assert.Equal(0, game.State.CardObjects[id].ObjectGeneration);
        }
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "MINION"));
        await game.ColdCheckpoint(); await game.Replay();
    }

    // DOC-17 p14 Q1/Q2: replacing destruction pays the powerful-unit cost.
    [Theory]
    [InlineData("RAD·020/167")]
    [InlineData("RAD·020a/167")]
    public async Task SacrificeCanDestroyAOnePowerMinionInsteadOfTheHeroAndStillResolve(string no)
    {
        var state = RadGearTestPosition.Put(FaqMinion(Position(no), "ALLY", "BASE"), "SACRIFICE", "UNL-173/219", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "CALLED", "OGN·007/298", "P1", "RUNE_DECK");
        Assert.Equal(1, state.CardObjects["ALLY"].Power);
        Assert.Equal(5, state.CardObjects["UNIT"].Power);
        var game = new Driver(state);
        var pool = game.State.RunePools["P1"];
        var draw = game.State.PlayerZones["P1"].MainDeck.Take(2).ToArray();
        await Reject(state, "P1", new PlayCardCommand("SACRIFICE", "UNL-173/219", [],
            OptionalCosts: ["DESTROY_FRIENDLY_POWERFUL_UNIT:ALLY"]));
        await game.Act("P1", new PlayCardCommand("SACRIFICE", "UNL-173/219", [],
            OptionalCosts: ["DESTROY_FRIENDLY_POWERFUL_UNIT:UNIT"]));
        await FaqReplacement(game, "UNIT", "ALLY");
        Assert.DoesNotContain("ALLY", game.State.CardObjects.Keys);
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(0, game.State.CardObjects["UNIT"].Damage);
        Assert.Equal(pool.Mana - 1, game.State.RunePools["P1"].Mana);
        await game.Top();
        Assert.All(draw, id => Assert.Contains(id, game.State.PlayerZones["P1"].Hand));
        Assert.Contains("CALLED", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["CALLED"].IsExhausted);
        Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains("SACRIFICE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingRuleChoice);
        await game.ColdCheckpoint(); await game.Replay();
    }

    private static MatchState FaqMinion(MatchState state, string id, string zone, string? battlefield = null)
    {
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("OGN·271/298", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = factory.CreateObject(id, "P1", "P1") } };
        return RadUnitEffectTestFixture.Place(state, id, "P1", zone, battlefield);
    }

    private static async Task FaqReplacement(Driver game, string source, string cost)
    {
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        var option = $"SACRIFICE_RECALL:{source}:{cost}";
        Assert.Contains(request.Options, value => value.Id == option);
        var command = new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]);
        await Reject(game.State, "P2", command);
        await Reject(game.State, "P1", command with { PaymentChoiceIds = [$"SACRIFICE_RECALL:{source}:missing"] });
        await game.ColdCheckpoint();
        await game.Act("P1", command);
    }
}
