using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class PrintedAlternativeEchoTests
{
    private const string Card = "UNL-182/219";
    private static readonly string[] Fees = ["ECHO", "ECHO:PRINTED:2", "ECHO:PRINTED:3"];
    private static readonly SpellRepeatChoice[] Modes = [
        new("BATTLEFIELD_UNIT_DAMAGE_2", ["FIELD-TARGET"]),
        new("BASE_UNIT_DAMAGE_3", ["ENEMY"]),
        new("BATTLEFIELD_UNIT_POWER_MINUS_4", ["POWER-TARGET"])];

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(Card);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["FIELD-TARGET"] = Printed("FIELD-TARGET", "OGN·219/298", "P2"),
            ["POWER-TARGET"] = Printed("POWER-TARGET", "OGN·001/298", "P2") },
            RunePools = state.RunePools.ToDictionary(p => p.Key, _ => new RunePool(40, 10,
                new Dictionary<string, int> { ["red"] = 10, ["green"] = 10, ["blue"] = 10,
                    ["orange"] = 10, ["purple"] = 10, ["yellow"] = 10 })) };
        state = LocalDestructionRecallTests.At(state, "FIELD-TARGET", "P2", "BATTLEFIELD", "OTHER");
        return LocalDestructionRecallTests.At(state, "POWER-TARGET", "P2", "BATTLEFIELD", "OTHER");
    }

    private static PlayCardCommand Command(int mask = 7)
    {
        var selected = Fees.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
        return new("UNIT", Card, [], Mode: "DRAW_1", OptionalCosts: selected,
            RepeatChoices: selected.Length == 0 ? null : Modes.Take(selected.Length).ToArray());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public async Task EverySubsetOfTheThreePrintedFeesPaysOnceAndExecutesDifferentModes(int mask)
    {
        var initial = Position(); var command = Command(mask);
        var count = command.OptionalCosts!.Count;
        var mana = 4 + ((mask & 1) != 0 ? 1 : 0) + ((mask & 4) != 0 ? 1 : 0);
        var power = ((mask & 2) != 0 ? 1 : 0) + ((mask & 4) != 0 ? 1 : 0);
        var before = MatchStateHasher.Hash(initial);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.GenericPower); Assert.Equal(before, MatchStateHasher.Hash(initial));
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, action => action.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements, requirement => requirement["sourceObjectId"] is "UNIT" && requirement["mode"] is "DRAW_1");
        var costs = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]);
        Assert.All(Fees, id => Assert.Contains(costs, cost => cost.Id == id));
        var game = new Driver(initial); await game.Act("P1", command);
        var item = Assert.Single(game.State.StackItems); Assert.Equal(count + 1, item.EffectRepeatCount);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_PLAYED");
        Assert.Single(game.Events(), entry => entry.Event.Kind == "COST_PAID");
        if (count == 0) Assert.Null(item.RepeatExecutions);
        else {
            Assert.Equal(count + 1, item.RepeatExecutions!.Count);
            Assert.Equal(count + 1, item.RepeatExecutions.Select(execution => execution.EffectKind).Distinct().Count());
        }
        Assert.Equal(initial.RunePools["P1"].Mana - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(initial.RunePools["P1"].TotalPower - power, game.State.RunePools["P1"].TotalPower);
        await game.Top(); Assert.Empty(game.State.StackItems);
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(count >= 1 ? 2 : 0, game.State.CardObjects["FIELD-TARGET"].Damage);
        Assert.Equal(count >= 2 ? 3 : 0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(count >= 3 ? -4 : 0, game.State.CardObjects["POWER-TARGET"].UntilEndOfTurnPowerModifier);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_PLAYED"); await game.Replay();
    }

    [Theory]
    [InlineData("duplicate-cost")] [InlineData("duplicate-original-mode")]
    [InlineData("duplicate-later-mode")] [InlineData("excess-grant")]
    [InlineData("no-mana")] [InlineData("no-power")] [InlineData("missing-repeat-choice")]
    [InlineData("unknown-cost")]
    public async Task DuplicateFeesRepeatedModesAndIncompletePaymentsRejectAtomically(string branch)
    {
        var state = Position(); var command = Command();
        var repeats = command.RepeatChoices!.ToArray();
        if (branch == "duplicate-cost") command = command with { OptionalCosts = [..Fees, "ECHO"] };
        if (branch == "unknown-cost") command = command with { OptionalCosts = ["ECHO:PRINTED:99"], RepeatChoices = [Modes[0]] };
        if (branch == "duplicate-original-mode") { repeats[0] = new("DRAW_1", []); command = command with { RepeatChoices = repeats }; }
        if (branch == "duplicate-later-mode") { repeats[2] = repeats[1]; command = command with { RepeatChoices = repeats }; }
        if (branch == "excess-grant") {
            state = state with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
            command = command with { OptionalCosts = [..Fees, "ECHO:GRANTED:1"], RepeatChoices = [..Modes, new("DRAW_1", [])] };
        }
        if (branch is "no-mana" or "no-power") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = branch == "no-mana" ? new(5, 10) : new(40, 1) } };
        if (branch == "missing-repeat-choice") command = command with { RepeatChoices = null };
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-" + branch, "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task RecoveryRejectsARepeatedModeInTheCapturedExecutionPlan(int index)
    {
        var game = new Driver(Position()); await game.Act("P1", Command());
        var item = Assert.Single(game.State.StackItems); var executions = item.RepeatExecutions!.ToArray();
        executions[index] = executions[1];
        var bad = game.State with { StackItems = [item with { RepeatExecutions = executions }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), error => error.Contains("duplicate modes in spell repeat execution plan"));
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task ARecalledTargetDoesNotStopTheOtherDistinctExecutionsOrExposeItsNewIncarnation()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RECALL", "OGN·104/298", "P2");
        var oldGeneration = initial.CardObjects["FIELD-TARGET"].ObjectGeneration;
        var game = new Driver(initial); await game.Act("P1", Command());
        if (game.State.PriorityPlayerId == "P1") await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RECALL", "OGN·104/298", ["FIELD-TARGET"])); await game.Top();
        Assert.Contains("FIELD-TARGET", game.State.PlayerZones["P2"].Hand);
        Assert.True(game.State.CardObjects["FIELD-TARGET"].ObjectGeneration > oldGeneration);
        await game.Top(); Assert.Empty(game.State.StackItems);
        Assert.Equal(0, game.State.CardObjects["FIELD-TARGET"].Damage); Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(-4, game.State.CardObjects["POWER-TARGET"].UntilEndOfTurnPowerModifier);
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Theory, InlineData(0), InlineData(1), InlineData(2)]
    public async Task ASeparateBaseCostEchoGrantCanBeChosenWithoutPayingAllPrintedFees(int printedFees)
    {
        var state = Position() with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var selected = Fees.Take(printedFees).Append("ECHO:GRANTED:1").ToArray();
        var command = new PlayCardCommand("UNIT", Card, [], Mode: "DRAW_1", OptionalCosts: selected,
            RepeatChoices: Modes.Take(selected.Length).ToArray());
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(8 + (printedFees > 0 ? 1 : 0), quote.Cost!.Mana);
        Assert.Equal(printedFees > 1 ? 1 : 0, quote.Cost.GenericPower);
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Equal(2 + printedFees, Assert.Single(game.State.StackItems).EffectRepeatCount);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, effect => EchoCostRules.IsGrant(effect, "P1"));
        await game.Top(); Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task EachPrintedManaFeeUsesTheSharedEchoDiscountWithoutRemovingItsGenericPower()
    {
        var state = RadGearTestPosition.Put(Position(), "BF", "SFD·211/221", "P1", "BATTLEFIELD", "BF");
        var command = Command();
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(4, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.GenericPower);
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Equal(state.RunePools["P1"].Mana - 4, game.State.RunePools["P1"].Mana);
        Assert.Equal(state.RunePools["P1"].TotalPower - 2, game.State.RunePools["P1"].TotalPower);
        await game.Top(); Assert.Empty(game.State.StackItems); await game.Replay();
    }
}
