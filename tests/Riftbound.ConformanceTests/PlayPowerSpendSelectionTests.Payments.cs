using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class PlayPowerSpendSelectionTests
{
    [Theory]
    [InlineData(0, 0, 2, false)] [InlineData(0, 1, 1, false)] [InlineData(0, 2, 0, false)]
    [InlineData(1, 0, 1, false)] [InlineData(1, 1, 0, false)]
    [InlineData(0, 0, 2, true)] [InlineData(0, 1, 1, true)] [InlineData(0, 2, 0, true)]
    [InlineData(1, 0, 1, true)] [InlineData(1, 1, 0, true)]
    public async Task EveryFundedTwoPowerVectorCanPayTheSameMulticolorPrintedPrice(int wild, int blue, int green, bool reverse)
    {
        var colors = reverse ? Colors(("green", 2), ("blue", 2)) : Colors(("blue", 2), ("green", 2));
        var run = new Run(Hand("mixed-fee-" + wild + blue + green + reverse, "SFD·190/221", new(4, 1, colors)));
        var command = new PlayCardCommand("CARD", "SFD·190/221", [], OptionalCosts: [Spend(wild, Colors(("blue", blue), ("green", green)))]);
        var quote = await run.Quote("P1", command); Assert.True(quote.CanPay, quote.Message);
        var result = await run.Submit("P1", command); Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 1 - wild, Colors(("blue", 2 - blue), ("green", 2 - green))), run.State.RunePools["P1"]);
        Assert.Equal(0, quote.Cost!.GenericPower); Assert.Equal(2, quote.Cost.PowerByTrait.Values.Sum());
        AssertQuoteRemainder(quote, run.State.RunePools["P1"]);
        AssertFee(result, quote, 2);
        await run.Cold("mixed-fee-final");
    }

    [Theory]
    [InlineData("PRINTED_POWER:blue:2", "POWER_SPEND:green:2", false)]
    [InlineData("PRINTED_POWER:blue:2", "POWER_SPEND:A:1,blue:1", true)]
    [InlineData("PRINTED_POWER:green:2", "POWER_SPEND:A:1,blue:1", false)]
    [InlineData("PRINTED_POWER:green:2", "POWER_SPEND:A:1,green:1", true)]
    public async Task ExplicitPrintedAllocationStillRestrictsTheRequiredTraits(string printed, string spend, bool accepted)
    {
        var initial = Hand("explicit-" + printed + spend, "SFD·190/221", new(4, 1, Colors(("blue", 2), ("green", 2))));
        var run = new Run(initial);
        var command = new PlayCardCommand("CARD", "SFD·190/221", [], OptionalCosts: [printed, spend]);
        var quote = await run.Quote("P1", command); var result = await run.Submit("P1", command);
        Assert.Equal(accepted, result.Accepted); Assert.Equal(accepted, quote.CanPay);
        if (!accepted) AssertRejected(initial, result);
        else
        {
            var required = printed.Contains("blue", StringComparison.Ordinal) ? "blue" : "green";
            Assert.Equal(Colors((required, 2)), quote.Cost!.PowerByTrait);
            Assert.Equal(new RunePool(0, 0, Colors(("blue", required == "blue" ? 1 : 2), ("green", required == "green" ? 1 : 2))), run.State.RunePools["P1"]);
            AssertFee(result, quote, 2);
        }
        await run.Cold("explicit-final");
    }

    [Fact]
    public async Task IndependentBaseCostEchoesFindTheFeeAllocationThatTheSelectedWalletCanPay()
    {
        var initial = Hand("base-echo", "SFD·182/221", new(3, 1, Colors(("blue", 2), ("red", 2))));
        initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(EchoCostRules.AddBaseCostGrant([], "P1"), "P1") };
        var run = new Run(initial);
        var command = new PlayCardCommand("CARD", "SFD·182/221", [], OptionalCosts: ["PRINTED_POWER:red:1",
            "ECHO:GRANTED:1", "ECHO:GRANTED:2", "POWER_SPEND:A:1,blue:2"]);
        var quote = await run.Quote("P1", command); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(Colors(("blue", 2), ("red", 1)), quote.Cost!.PowerByTrait); Assert.Equal(0, quote.Cost.GenericPower);
        var result = await run.Submit("P1", command); Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0, Colors(("red", 2))), run.State.RunePools["P1"]);
        Assert.Equal(3, Assert.Single(run.State.StackItems).EffectRepeatCount);
        AssertFee(result, quote, 3); await run.Cold("base-echo-final");
    }

    [Fact]
    public async Task AllSevenComponentsCanPayOneRealCombinedPrintedEchoAndSpellshieldFee()
    {
        var initial = Hand("seven-components", "SFD·023/221", new(14, 1,
            Colors(("blue", 1), ("green", 1), ("orange", 1), ("purple", 1), ("red", 9), ("yellow", 1))));
        initial = WithBattlefieldTarget(initial, Shielded("TARGET", "P2"));
        for (var n = 0; n < 5; n++) initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1") };
        var costs = new[] { "ECHO" }.Concat(Enumerable.Range(1, 5).Select(n => "ECHO:GRANTED:" + n))
            .Append("POWER_SPEND:A:1,blue:1,green:1,orange:1,purple:1,red:8,yellow:1").ToArray();
        var command = new PlayCardCommand("CARD", "SFD·023/221", ["TARGET"], OptionalCosts: costs,
            RepeatChoices: Enumerable.Range(0, 6).Select(_ => new SpellRepeatChoice("", ["TARGET"])).ToArray());
        var run = new Run(initial); var quote = await run.Quote("P1", command);
        var result = await run.Submit("P1", command); Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(7, quote.Cost!.GenericPower); Assert.Equal(Colors(("red", 7)), quote.Cost.PowerByTrait);
        Assert.Equal(new RunePool(0, 0, Colors(("red", 1))), run.State.RunePools["P1"]);
        Assert.Equal(7, Assert.Single(run.State.StackItems).EffectRepeatCount);
        AssertFee(result, quote, 14); await run.Cold("seven-components-final");
    }

    [Fact]
    public async Task ActualSacrificeReductionIsAppliedOnceBeforeCheckingTheSelectedSources()
    {
        var initial = OptionalSacrificePlayCostTests.Position("SFD·006/221", 7, 2) with { RoomId = "sacrifice-spend", Tick = 0 };
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(7, 1, Colors(("yellow", 2))) } };
        var run = new Run(initial);
        var command = new PlayCardCommand("CARD", "UNL-170/219", [],
            OptionalCosts: ["DESTROY_FRIENDLY_UNIT:SACRIFICE", "POWER_SPEND:A:1,yellow:1"]);
        var quote = await run.Quote("P1", command); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(Colors(("yellow", 2)), quote.Cost!.PowerByTrait);
        Assert.Equal(-1, Assert.Single(quote.Cost.Adjustments, a => a.Label == "所选额外费用带来的减免").Power);
        var result = await run.Submit("P1", command); Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains("SACRIFICE", run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new RunePool(0, 0, Colors(("yellow", 1))), run.State.RunePools["P1"]);
        AssertFee(result, quote, 2); await run.Cold("sacrifice-final");
    }

    [Fact]
    public async Task OptionalAdditionalReductionKeepsTheOriginalFeeAuditAndSelectedRainbowDebitSeparate()
    {
        var initial = WithBase(Hand("reduced-haste", "SFD·143/221", new(5, 1, Colors(("purple", 1)))), "P1",
            OfficialGarenConquestBattlefieldCountTests.Printed("EZREAL", "SFD·149/221", "P1"));
        var run = new Run(initial);
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: [HasteOptionalCostNames.HasteReady,
            "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:POWER-purple", "POWER_SPEND:A:1"]);
        var quote = await run.Quote("P1", command); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(Colors(("purple", 1)), quote.Cost!.PowerByTrait);
        var result = await run.Submit("P1", command); Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0, Colors(("purple", 1))), run.State.RunePools["P1"]);
        var paid = Assert.Single(result.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(new[] { "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:POWER-purple" }, Assert.IsType<string[]>(paid.Payload["costReductionChoices"]));
        AssertFee(result, quote, 1); await run.Cold("reduced-haste-final");
    }

    [Theory]
    [InlineData("recycle", false)] [InlineData("recycle", true)] [InlineData("lux", false)] [InlineData("lux", true)]
    public async Task ProspectiveResourceActionsAreValidatedBeforeAnyActualGainOrExhaustion(string resource, bool accepted)
    {
        var lux = resource == "lux";
        var initial = Hand("resource-" + resource + accepted, lux ? "SFD·023/221" : "SFD·006/221",
            new(lux ? 0 : 3, 0, lux ? Colors(("blue", 1), ("red", 1)) : Colors(("blue", 1))));
        initial = WithBase(initial, "P1", OfficialGarenConquestBattlefieldCountTests.Printed(lux ? "LUX" : "RUNE", lux ? "OGS·014/024" : "OGN·007/298", "P1"));
        if (lux) initial = WithBattlefieldTarget(initial, OfficialGarenConquestBattlefieldCountTests.Printed("TARGET", "SFD·029/221", "P2"));
        var run = new Run(initial);
        var command = new PlayCardCommand("CARD", lux ? "SFD·023/221" : "SFD·006/221", lux ? ["TARGET"] : [],
            OptionalCosts: [lux ? "LUX_SPELL_ONLY_RESOURCE:LUX" : "RECYCLE_RUNE:RUNE", accepted ? "POWER_SPEND:red:1" : "POWER_SPEND:blue:1"]);
        var quote = await run.Quote("P1", command); var result = await run.Submit("P1", command);
        Assert.Equal(accepted, quote.CanPay); Assert.Equal(accepted, result.Accepted);
        if (!accepted) AssertRejected(initial, result);
        else
        {
            Assert.Equal(new RunePool(0, 0, Colors(("blue", 1))), run.State.RunePools["P1"]);
            if (lux) Assert.True(run.State.CardObjects["LUX"].IsExhausted);
            else { Assert.Contains("RUNE", run.State.PlayerZones["P1"].RuneDeck); Assert.Single(result.Events, e => e.Kind == "RUNE_RECYCLED"); }
            AssertQuoteRemainder(quote, run.State.RunePools["P1"]); AssertFee(result, quote, 1);
        }
        await run.Cold("resource-final");
    }

    private static MatchState Hand(string room, string card, RunePool pool)
        => PlayCostPreviewTests.Position(pool, card) with { RoomId = room, Tick = 0 };

    private static MatchState WithBase(MatchState state, string player, CardObjectState card)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [card.ObjectId] = card },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [player] = state.PlayerZones[player] with { Base = [..state.PlayerZones[player].Base, card.ObjectId] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [card.ObjectId] = new(player, "BASE") } };

    private static CardObjectState Shielded(string id, string player)
    {
        var card = OfficialGarenConquestBattlefieldCountTests.Printed(id, "SFD·029/221", player);
        return card with { Tags = card.Tags.Append(CardObjectTags.Spellshield).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
    }

    private static MatchState WithBattlefieldTarget(MatchState state, CardObjectState target)
    {
        var field = OfficialGarenConquestBattlefieldCountTests.Printed("FIELD", "OGN·280/298", "P2") with { ControllerId = "P2" };
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["FIELD"] = field, [target.ObjectId] = target },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Battlefields = ["FIELD", target.ObjectId] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["FIELD"] = new("P2", "BATTLEFIELD", "FIELD"), [target.ObjectId] = new("P2", "BATTLEFIELD", "FIELD") } };
    }

    private static string Spend(int wildcard, IReadOnlyDictionary<string, int> typed)
        => "POWER_SPEND:" + string.Join(',', (wildcard > 0 ? new[] { "A:" + wildcard } : []).Concat(
            typed.Where(p => p.Value > 0).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value)));

    private static void AssertQuoteRemainder(PlayCostQuoteDto quote, RunePool pool)
    {
        Assert.Equal(pool.Power, quote.Cost!.RemainingRainbowPower);
        Assert.Equal(pool.PowerByTrait.OrderBy(p => p.Key), quote.Cost.RemainingPowerByTrait!.OrderBy(p => p.Key));
        Assert.Equal(pool.Mana + pool.SpellOnlyMana, quote.Cost.RemainingMana);
    }

    private static void AssertFee(ResolutionResult result, PlayCostQuoteDto quote, int total)
    {
        var cost = Assert.Single(result.Events, e => e.Kind == "COST_PAID");
        var payload = JsonSerializer.SerializeToElement(cost.Payload, Web);
        Assert.Equal(total, payload.GetProperty("totalPowerCost").GetInt32());
        Assert.Equal(quote.Cost!.GenericPower, payload.GetProperty("genericPower").GetInt32());
        Assert.Equal(quote.Cost.PowerByTrait.OrderBy(p => p.Key), payload.GetProperty("powerByTrait").Deserialize<Dictionary<string, int>>(Web)!.OrderBy(p => p.Key));
    }

    private static void AssertRejected(MatchState initial, ResolutionResult result)
    {
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
        Assert.Equal(Hash(ResolutionResult.BuildSnapshots(initial)), Hash(result.Snapshots));
        Assert.Equal(Hash(ResolutionResult.BuildPrompts(initial)), Hash(result.Prompts));
    }
}
