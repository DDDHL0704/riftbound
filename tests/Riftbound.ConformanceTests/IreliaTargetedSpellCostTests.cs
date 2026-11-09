using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// SFD141/141a: one mana OR one power per targeted text instance. CN356.4.c-f:
// total reductions follow component reductions and can remove additional costs.
public sealed class IreliaTargetedSpellCostTests
{
    private const string Irelia = "SFD·141/221", Alternate = "SFD·141a/221";
    private const string TypedSpell = "VEN·012", Boost = "SFD·034/221";
    private const string Prefix = "TARGETED_SPELL_REDUCTION:";

    [Theory]
    [InlineData(Irelia, false, false)] [InlineData(Irelia, true, false)]
    [InlineData(Alternate, false, false)] [InlineData(Alternate, true, false)]
    [InlineData(Irelia, false, true)] [InlineData(Irelia, true, true)]
    [InlineData(Alternate, false, true)] [InlineData(Alternate, true, true)]
    public async Task BothRealPrintingsOfferOneJointPriceAndTheOriginalCommandsRecoverAndReplay(
        string printing, bool reducePower, bool explicitChoice)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.Equal("你以我为目标的法术费用减少{{1}}或{{A}}。", Assert.Single(catalog.Cards, c => c.CardNo == printing).CardEffect);
        var mana = reducePower ? 7 : 6;
        var initial = Position(printing, "HAND", TypedSpell, new(mana, 0, Colors(("purple", 1), ("red", reducePower ? 0 : 1))));
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", printing, [], Destination: "BASE"));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted); // passive text does not require ready
        var options = Choices(game.State);
        Assert.Equal(2, options.Length);
        var command = new PlayCardCommand("SPELL", TypedSpell, ["HOST"], OptionalCosts: explicitChoice
            ? [Choice(game.State, "HOST", reducePower ? "POWER" : "MANA")] : []);
        var quote = Quote(game.State, command);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(reducePower ? 3 : 2, quote.Cost!.Mana);
        Assert.Equal(reducePower ? 0 : 1, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var adjustment = Assert.Single(quote.Cost.Adjustments, a => a.Label == "目标减费");
        Assert.Equal(reducePower ? 0 : -1, adjustment.Mana);
        Assert.Equal(reducePower ? -1 : 0, adjustment.Power);
        await game.Act("P1", command);
        AssertPrice(game, quote);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task OneSourceCannotCoverBothMissingResourceDimensions()
    {
        var initial = Position(pool: new(2, 0));
        var command = new PlayCardCommand("SPELL", TypedSpell, ["HOST"]);
        var quote = Quote(initial, command);
        Assert.True(quote.IsValid, quote.Message); Assert.False(quote.CanPay);
        Assert.True(quote.Cost!.MissingMana + quote.Cost.MissingPower > 0);
        await Reject(initial, command);
    }

    [Theory]
    [InlineData("UNRESTRICTED", 1, 0)]
    [InlineData("SPELL_ONLY", 2, 2)]
    public async Task AutomaticModeSelectionHonorsTheSubmittedActualManaSourceTotal(string source, int mana, int spellMana)
    {
        var initial = Position(spell: Boost, pool: new(mana, 0) { SpellOnlyMana = spellMana });
        var command = new PlayCardCommand("SPELL", Boost, ["HOST"], OptionalCosts: [$"MANA_SOURCE:{source}:{(source == "SPELL_ONLY" ? 2 : 1)}"]);
        var game = new Driver(initial); var quote = Quote(initial, command);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(source == "SPELL_ONLY" ? 2 : 1, quote.Cost!.Mana);
        await game.Act("P1", command); AssertPrice(game, quote); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RepeatedSelectionCountsOneSourceOnceButDifferentSourcesEachApply(bool different)
    {
        var initial = Position(spell: Boost, pool: new(different ? 2 : 3, 0));
        initial = RadGearTestPosition.Put(initial, "HOST2", Alternate, "P1", "BASE");
        var command = new PlayCardCommand("SPELL", Boost, ["HOST"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", [different ? "HOST2" : "HOST"])]);
        var quote = Quote(initial, command); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(different ? 2 : 3, quote.Cost!.Mana);
        var game = new Driver(initial); await game.Act("P1", command); AssertPrice(game, quote);
        var paid = game.Events().Last(e => e.Event.Kind == "COST_PAID").Event;
        Assert.Equal(different ? 2 : 1, JsonSerializer.SerializeToElement(paid.Payload["targetedSpellCostReductionChoices"]).GetArrayLength());
        await game.Top(); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("MANA", "POWER", 5, 1)]
    [InlineData("POWER", "POWER", 6, 0)]
    [InlineData("MANA", "MANA", 4, 2)]
    public async Task MultipleTargetedInstancesCanSplitOneTotalManaAndTypedPowerPrice(string first, string second, int mana, int power)
    {
        const string fight = "UNL-110/219";
        var initial = Position(spell: fight, pool: new(mana, 0, Colors(("orange", power))));
        initial = RadGearTestPosition.Put(initial, "HOST2", Alternate, "P1", "BASE");
        var command = new PlayCardCommand("SPELL", fight, ["HOST", "HOST2"], OptionalCosts:
            [Choice(initial, "HOST", first), Choice(initial, "HOST2", second)]);
        var quote = Quote(initial, command); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana); Assert.Equal(power, quote.Cost.PowerByTrait.Values.Sum());
        var game = new Driver(initial); await game.Act("P1", command); AssertPrice(game, quote); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("any", "red")] [InlineData("red", "blue")]
    public async Task TargetReductionCanRemoveTheActualWardOrPrintedColoredPower(string reduction, string wallet)
    {
        const string spell = "SFD·023/221";
        var initial = Position(zone: "BATTLEFIELD", spell: spell, pool: new(2, 0, Colors((wallet, 1))));
        initial = RadGearTestPosition.Put(initial, "WARD", "OGN·013/298", "P2", "BATTLEFIELD", "OTHER");
        // PrintedCardFactory creates an outside-play card; this midgame ward
        // fixture explicitly supplies its printed in-play keyword.
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["WARD"] = initial.CardObjects["WARD"] with { Tags = initial.CardObjects["WARD"].Tags
                .Append(CardObjectTags.Spellshield).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() } } };
        var command = new PlayCardCommand("SPELL", spell, ["HOST", "WARD"], OptionalCosts:
            [Choice(initial, "HOST", "POWER"), "SPELL_POWER_REDUCTION:" + reduction + ":1"]);
        var quote = Quote(initial, command); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(1, quote.Cost!.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var game = new Driver(initial); await game.Act("P1", command); AssertPrice(game, quote); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AZeroBasePriceCanStillReduceAnAdditionalFeeWithoutRemovingTheDeclaredRepeat()
    {
        var initial = Position(spell: Boost, pool: new(0, 0)) with {
            UntilEndOfTurnEffects = ["RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:DRAKE"] };
        var command = new PlayCardCommand("SPELL", Boost, ["HOST"], OptionalCosts: ["ECHO", Choice(initial, "HOST", "MANA")]);
        var quote = Quote(initial, command); Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        var game = new Driver(initial); await game.Act("P1", command); AssertPrice(game, quote);
        Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task TheSameContinuousSourceReducesLaterSpellsWithoutBeingConsumed()
    {
        var initial = RadGearTestPosition.Put(Position(spell: Boost, pool: new(2, 0)), "SPELL2", Boost, "P1", "HAND");
        var game = new Driver(initial);
        foreach (var id in new[] { "SPELL", "SPELL2" })
        {
            var command = new PlayCardCommand(id, Boost, ["HOST"]);
            var quote = Quote(game.State, command); Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana);
            await game.Act("P1", command); AssertPrice(game, quote); await game.Top();
        }
        Assert.Equal(0, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Theory]
    [InlineData("other-target")] [InlineData("opponent-casts")]
    [InlineData("hand-source")] [InlineData("grave-source")] [InlineData("hidden-source")]
    public async Task OnlyTheActualFriendlyVisibleTargetSuppliesTheDiscount(string branch)
    {
        var initial = Position(zone: branch == "hand-source" ? "HAND" : branch == "grave-source" ? "GRAVEYARD" : "BASE", spell: Boost, pool: new(2, 0));
        if (branch == "hidden-source") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsFaceDown = true } } };
        var player = branch == "opponent-casts" ? "P2" : "P1";
        if (player == "P2") initial = RadGearTestPosition.Put(initial with { ActivePlayerId = "P2", TurnPlayerId = "P2" },
            "THEIR-SPELL", Boost, "P2", "HAND");
        var command = new PlayCardCommand(player == "P2" ? "THEIR-SPELL" : "SPELL", Boost,
            [player == "P2" ? "HOST" : "ALLY"]);
        var quote = Quote(initial, command, player); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana); Assert.DoesNotContain(quote.Cost.Adjustments, a => a.Label == "目标减费");
        var game = new Driver(initial); await game.Act(player, command); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("both-modes")] [InlineData("invented")]
    [InlineData("old-generation")] [InlineData("different-target")]
    public async Task InvalidOrStaleModeDeclarationsAreRejectedWithoutMutatingState(string branch)
    {
        var initial = Position(pool: new(20, 20));
        var mana = Choice(initial, "HOST", "MANA"); var power = Choice(initial, "HOST", "POWER");
        if (branch == "old-generation") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { ObjectGeneration = 1 } } };
        string[] choices = branch switch { "duplicate" => [mana, mana], "both-modes" => [mana, power],
            "invented" => [Prefix + "invented:MANA"], _ => [mana] };
        var command = new PlayCardCommand("SPELL", TypedSpell, [branch == "different-target" ? "ALLY" : "HOST"], OptionalCosts: choices);
        var quote = Quote(initial, command); Assert.False(quote.IsValid); Assert.Equal(ErrorCodes.InvalidTarget, quote.ErrorCode);
        await Reject(initial, command);
    }

    [Fact]
    public void MissingTargetsKeepTheQuoteIncompleteAndNeverPromiseBothIndependentMinima()
    {
        var initial = Position(pool: new(2, 0));
        var quote = Quote(initial, new("SPELL", TypedSpell, []));
        Assert.True(quote.IsValid, quote.Message); Assert.True(quote.RequiresTargetSelection);
        Assert.True(quote.TargetDependentCosts); Assert.False(quote.CanPay);
        Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.PowerByTrait.Values.Sum());
    }

    [Fact]
    public async Task ActuallyAttachedCopiedTextSuppliesAnotherIndependentReductionAndCanReachZero()
    {
        var initial = RadGearTestPosition.Put(Position(spell: Boost), "SONG", "SFD·059/221", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SONG", "SFD·059/221", [], Destination: "BASE"));
        while (game.State.StackItems.Count > 0) await game.Top();
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.NotNull(game.State.CardObjects["SONG"].AttachedRuleText);
        var reductions = Choices(game.State).Where(c => c.Id.EndsWith(":MANA", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, reductions.Length);
        var command = new PlayCardCommand("SPELL", Boost, ["HOST"], OptionalCosts: reductions.Select(c => c.Id).ToArray());
        var quote = Quote(game.State, command); Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", command); AssertPrice(game, quote); Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AnActualImageCopyUsesItsOwnFriendlyBindingRatherThanTheOpponentsOriginal()
    {
        var initial = Position(spell: Boost, pool: new(4, 20));
        initial = LocalDestructionRecallTests.At(initial, "HOST", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["HOST"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        var copy = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal(Irelia, copy.CardNo); Assert.Equal("P1", copy.ControllerId);
        var command = new PlayCardCommand("SPELL", Boost, [copy.ObjectId], OptionalCosts: [Choice(game.State, copy.ObjectId, "MANA")]);
        var quote = Quote(game.State, command); Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana);
        await game.Act("P1", command); AssertPrice(game, quote); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task TotalTargetReductionFollowsTheOptionalComponentReduction()
    {
        var initial = RadGearTestPosition.Put(Position(spell: Boost, pool: new(2, 0)), "EZREAL", "SFD·149/221", "P1", "BASE");
        var command = new PlayCardCommand("SPELL", Boost, ["HOST"], OptionalCosts:
            ["ECHO", "OPTIONAL_COST_REDUCTION:EZREAL:0:ECHO:MANA", Choice(initial, "HOST", "MANA")]);
        var quote = Quote(initial, command); Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana);
        Assert.Contains(quote.Cost.Adjustments, a => a.Label == "可选额外费用减免" && a.Mana == -1);
        Assert.Contains(quote.Cost.Adjustments, a => a.Label == "目标减费" && a.Mana == -1);
        var game = new Driver(initial); await game.Act("P1", command); AssertPrice(game, quote);
        Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task TargetingTheUnitWithAnEquipmentPlayDoesNotMakeThatPlayASpell()
    {
        var initial = RadGearTestPosition.Put(Position(), "GEAR", "SFD·022/221", "P1", "HAND");
        var command = new PlayCardCommand("GEAR", "SFD·022/221", ["HOST"], Destination: "BASE");
        var quote = Quote(initial, command); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.PowerByTrait.Values.Sum());
        Assert.DoesNotContain(quote.Cost.Adjustments, a => a.Label == "目标减费");
        var game = new Driver(initial); await game.Act("P1", command);
        while (game.State.StackItems.Count > 0) await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AutomaticModeUsesTheActualPlannedLuxWalletAndTheExplicitRestrictedManaSource()
    {
        var initial = RadGearTestPosition.Put(Position(spell: Boost, pool: RunePool.Empty), "LUX", P4ActivatedAbilityCatalog.LuxCardNo, "P1", "BASE");
        var command = new PlayCardCommand("SPELL", Boost, ["HOST"], OptionalCosts:
            [P4ActivatedAbilityCatalog.LuxSpellOnlyResourceActionPrefix + "LUX", "MANA_SOURCE:SPELL_ONLY:2"]);
        var quote = Quote(initial, command); Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.AvailableSpellOnlyMana);
        var game = new Driver(initial); await game.Act("P1", command); AssertPrice(game, quote);
        Assert.Equal(0, game.State.RunePools["P1"].SpellOnlyMana); Assert.True(game.State.CardObjects["LUX"].IsExhausted);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task OpponentIncreaseIsAddedBeforeTheTargetReduction()
    {
        var initial = RadGearTestPosition.Put(Position(spell: Boost, pool: new(2, 0)), "HELM", "VEN·045", "P2", "BASE");
        var command = new PlayCardCommand("SPELL", Boost, ["HOST"]);
        var quote = Quote(initial, command); Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana);
        Assert.Contains(quote.Cost.Adjustments, a => a.Label == "对手法术增费" && a.Mana == 1);
        Assert.Contains(quote.Cost.Adjustments, a => a.Label == "目标减费" && a.Mana == -1);
        var game = new Driver(initial); await game.Act("P1", command); AssertPrice(game, quote); await game.Top(); await game.Replay();
    }

    private static MatchState Position(string printing = Irelia, string zone = "BASE", string spell = TypedSpell, RunePool? pool = null)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", printing, "P1", zone, zone == "BATTLEFIELD" ? "BF" : null);
        state = RadGearTestPosition.Put(state, "SPELL", spell, "P1", "HAND");
        return state with { RoomId = "IRELIA-TARGET-COST-" + printing + "-" + zone + "-" + spell,
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool ?? new(20, 20) },
            CardPlays = new(5, [], []) };
    }

    private static Dictionary<string, int> Colors(params (string Trait, int Amount)[] values)
        => values.Where(v => v.Amount > 0).ToDictionary(v => v.Trait, v => v.Amount, StringComparer.Ordinal);
    private static ActionPromptChoiceDto[] Choices(MatchState state)
    {
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        return (prompt.Candidates ?? []).Where(c => c.Action == CommandTypes.PlayCard)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Where(r => Equals(r["sourceObjectId"], "SPELL"))
            .SelectMany(r => (IEnumerable<ActionPromptChoiceDto>)r["optionalCostChoices"]!)
            .Where(c => c.Id.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
    }
    private static string Choice(MatchState state, string id, string mode)
        => Assert.Single(Choices(state), c => c.Id.EndsWith(":" + mode, StringComparison.Ordinal)
            && c.ObjectIds!.Contains(id)).Id;
    private static PlayCostQuoteDto Quote(MatchState state, PlayCardCommand command, string player = "P1")
    {
        var before = MatchStateHasher.Hash(state);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, player, new("irelia-cost", ResolutionResult.BuildPrompts(state)[player].PromptId!, state.Tick, command));
        Assert.Equal(before, MatchStateHasher.Hash(state)); return quote;
    }
    private static void AssertPrice(Driver game, PlayCostQuoteDto quote)
    {
        var paid = game.Events().Last(e => e.Event.Kind == "COST_PAID").Event;
        Assert.Equal(quote.Cost!.Mana, paid.Payload["mana"]);
        Assert.Equal(quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum(), paid.Payload["power"]);
    }
    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("irelia-cost-rejected", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(state)); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
