using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// SFD146; CN356.3–356.4, 466.2, 740.2.c. Every positive command retains
// its original accepted journal, retry, privacy, cold recovery and replay.
public sealed class VexCombatSpellCostTests
{
    private const string Vex = "SFD·146/221";
    private const string Smoke = "OGN·093/298";
    private const string SpellObject = "VEX-PRIVATE-SPELL";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static MatchState Position(bool defending = false, string spell = Smoke)
    {
        var state = RadGearTestPosition.Create();
        state = RadGearTestPosition.Put(state, "BF", "OGN·296/298", defending ? "P1" : "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "HOST", Vex, "P1", defending ? "BATTLEFIELD" : "BASE", defending ? "BF" : null);
        state = RadGearTestPosition.Put(state, "ENEMY", "OGN·219/298", "P2", defending ? "BASE" : "BATTLEFIELD", defending ? null : "BF");
        state = RadGearTestPosition.Put(state, SpellObject, spell, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "NEXT", Smoke, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "OPP-SPELL", Smoke, "P2", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return state with { TurnPlayerId = defending ? "P2" : "P1", ActivePlayerId = defending ? "P2" : "P1" };
    }

    private static Task Challenge(Driver game, bool defending = false)
        => game.Act(defending ? "P2" : "P1", new MoveUnitCommand(defending ? "ENEMY" : "HOST", "BASE", "BATTLEFIELD:BF", []));

    private static async Task Focus(Driver game, string player)
    {
        if (game.State.FocusPlayerId != player)
            await game.Act(Assert.IsType<string>(game.State.FocusPlayerId), new PassFocusCommand());
        Assert.Equal(player, game.State.FocusPlayerId);
    }

    private static PlayCostQuoteDto Quote(MatchState state, string player, PlayCardCommand command)
        => new CoreRuleEngine().PreviewPlayCard(state, player,
            new("vex-cost", ResolutionResult.BuildPrompts(state)[player].PromptId!, state.Tick, command));

    private static PlayCardCommand Cast(string source = SpellObject, string target = "ENEMY") => new(source, Smoke, [target]);

    private static void Balanced(PlayCostQuoteDto quote)
    {
        Assert.True(quote.IsValid, quote.Message);
        var cost = Assert.IsType<PlayCostBreakdownDto>(quote.Cost);
        Assert.Equal(cost.Mana, cost.PrintedMana + cost.Adjustments.Sum(a => a.Mana));
        Assert.Equal(cost.GenericPower + cost.PowerByTrait.Values.Sum(), cost.PrintedPower + cost.Adjustments.Sum(a => a.Power));
    }

    [Fact]
    public async Task TheRuntimePrintingKeepsBothSidesOfTheConditionalCostProfile()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Vex);
        Assert.Equal(33238, card.Id); Assert.Equal("郁郁", card.SubTitle);
        Assert.Equal("如果我处于战斗中，则所有友方法术的费用减少{{1}}和{{A}}，不得低于{{1}}，而所有敌方法术的费用增加{{1}}和{{A}}。", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Vex, out var behavior));
        Assert.Equal(new CombatSpellCostAdjustmentSpec(1, 1, 1, 1, 1), behavior.CombatSpellCostAdjustment);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task EitherActualCombatRoleMakesAnOtherwiseUnaffordableSpellAvailable(bool defending)
    {
        var initial = Position(defending) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(1, 0), ["P2"] = new(30, 30) } };
        var game = new Driver(initial); await Challenge(game, defending); await Focus(game, "P1");
        Assert.Equal(!defending, game.State.CardObjects["HOST"].IsAttacking);
        Assert.Equal(defending, game.State.CardObjects["HOST"].IsDefending);
        var quote = Quote(game.State, "P1", Cast()); Balanced(quote);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var candidate = Assert.Single(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        Assert.Equal(1, Assert.Single(requirements, r => Equals(r["sourceObjectId"], SpellObject))["minimumManaCost"]);
        Assert.DoesNotContain(SpellObject, JsonSerializer.Serialize(game.PromptFor("P2"), Web));
        await RadGearTestPosition.Reject(game.State, "P1", Cast() with { OptionalCosts = ["SPELL_POWER_REDUCTION:blue:2"] });
        await game.Act("P1", Cast() with { OptionalCosts = ["SPELL_POWER_REDUCTION:blue:1"] });
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event;
        Assert.Equal(1, paid.Payload["combatSpellCostReductionMana"]); Assert.Equal(1, paid.Payload["combatSpellCostReductionPower"]);
        await game.Top(); await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task EitherActualCombatRoleAddsManaAndGenericPowerToEveryEnemySpell(bool defending)
    {
        var initial = Position(defending) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(30, 30), ["P2"] = new(3, 1, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var game = new Driver(initial); await Challenge(game, defending); await Focus(game, "P2");
        var command = Cast("OPP-SPELL", "HOST"); var quote = Quote(game.State, "P2", command); Balanced(quote);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.GenericPower); Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Blue]);
        var poor = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P2"] = new(2, 2) } };
        await RadGearTestPosition.Reject(poor, "P2", command);
        await game.Act("P2", command);
        Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
        await game.Top(); await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("HAND")] [InlineData("GRAVEYARD")]
    [InlineData("BANISHED")] [InlineData("unstarted")] [InlineData("face-down")]
    public void ARegistrationOrUnstartedOrHiddenSourceDoesNotCreateACombatDiscount(string branch)
    {
        var state = Position(); var zone = branch is "unstarted" or "face-down" ? "BATTLEFIELD" : branch;
        state = RadGearTestPosition.Put(state, "HOST", Vex, "P1", zone, zone == "BATTLEFIELD" ? "BF" : null);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsFaceDown = branch == "face-down" } } };
        if (branch is "unstarted" or "face-down")
        {
            // Such a seed has no legal play window. Inspect the common cost
            // kernel without granting permission to skip the battle task.
            Assert.False(state.BattleState.IsActive);
            Assert.True(CardBehaviorRegistry.TryGetByCardNo(Smoke, out var spell));
            Assert.Equal(2, CoreRuleEngine.CalculatePlayManaCost(state, "P1", spell).Total);
            Assert.True(CoreRuleEngine.TrySelectPlayPowerCost(state, "P1", spell, null, new(30, 30), 0,
                new Dictionary<string, int>(), out var generic, out var typed));
            Assert.Equal(1, generic + typed.Values.Sum());
            return;
        }
        var quote = Quote(state, "P1", Cast()); Balanced(quote);
        Assert.Equal(2, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.DoesNotContain(quote.Cost.Adjustments, a => a.Label == "战斗中单位法术减费");
    }

    [Theory, InlineData("BASE"), InlineData("HAND"), InlineData("GRAVEYARD"), InlineData("OTHER")]
    public async Task AnotherUnitsActualBattleDoesNotEnableANonparticipatingVex(string zone)
    {
        var initial = Position();
        if (zone == "OTHER") initial = RadGearTestPosition.Put(initial, "OTHER", "OGN·296/298", "P1", "BATTLEFIELD", "OTHER");
        initial = RadGearTestPosition.Put(initial, "HOST", Vex, "P1", zone == "OTHER" ? "BATTLEFIELD" : zone, zone == "OTHER" ? "OTHER" : null);
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.CardObjects["ALLY"].IsAttacking);
        var quote = Quote(game.State, "P1", Cast()); Balanced(quote);
        Assert.Equal(2, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", Cast()); await game.Top(); await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task TheManaFloorAndColoredPowerReductionApplyToTheCombinedEchoCost(bool echo)
    {
        var game = new Driver(Position(spell: "SFD·080/221")); await Challenge(game);
        var command = new PlayCardCommand(SpellObject, "SFD·080/221", ["ENEMY"], OptionalCosts: echo ? ["ECHO"] : []);
        var quote = Quote(game.State, "P1", command); Balanced(quote);
        Assert.Equal(1, quote.Cost!.Mana); Assert.Equal(echo ? 1 : 0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command); Assert.Equal(echo ? 2 : 1, Assert.Single(game.State.StackItems).EffectRepeatCount);
        await game.Top();
        // Each hit is amplified by the actual Void Gate battlefield. Two
        // hits destroy this four-Might unit and reset its graveyard face.
        if (echo) Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        else Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ZeroPrintedManaIsNotRaisedToTheFloorAndItsDeckChoiceStaysPrivate()
    {
        var initial = Position(spell: "SFD·122/221") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = RunePool.Empty, ["P2"] = new(30, 30) } };
        var game = new Driver(initial); await Challenge(game);
        var command = new PlayCardCommand(SpellObject, "SFD·122/221", []); var quote = Quote(game.State, "P1", command); Balanced(quote);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command); await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        foreach (var id in choice.ContextObjectIds)
            Assert.DoesNotContain(id, JsonSerializer.Serialize(game.PromptFor("P2"), Web));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.LegalObjectIds[0]]));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task GenericReductionCanRemoveColoredPowerInsteadOfWardForASpellTargetingAnotherLocation()
    {
        var initial = RadGearTestPosition.Put(Position(defending: true), "WARDED", "OGN·013/298", "P2", "HAND") with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(1, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }), ["P2"] = new(30, 30) } };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("WARDED", "OGN·013/298", []));
        Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects["WARDED"].Tags);
        await Challenge(game, defending: true); await Focus(game, "P1");
        var command = Cast(target: "WARDED"); var quote = Quote(game.State, "P1", command); Balanced(quote);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.GenericPower); Assert.Empty(quote.Cost.PowerByTrait);
        Assert.Equal(new[] { "SPELL_POWER_REDUCTION:any:1", "SPELL_POWER_REDUCTION:blue:1" },
            quote.Cost.SpellPowerReductionChoices!.Select(c => c.Id).Order(StringComparer.Ordinal));
        await RadGearTestPosition.Reject(game.State, "P1", command with { OptionalCosts = ["SPELL_POWER_REDUCTION:any:1"] });
        await game.Act("P1", command with { OptionalCosts = ["SPELL_POWER_REDUCTION:blue:1"] });
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower); await game.Top(); await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task RealSourceDepartureRepricesFutureSpellsWithoutRefundingTheConfirmedOne(bool alreadyPaid)
    {
        var game = new Driver(Position()); await Challenge(game);
        if (alreadyPaid) await game.Act("P1", Cast());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        if (game.State.StackItems.Count == 0 && game.State.FocusPlayerId == "P2")
            await game.Act("P2", new PassFocusCommand());
        var future = Quote(game.State, "P1", Cast("NEXT")); Balanced(future); Assert.Equal(2, future.Cost!.Mana);
        if (alreadyPaid) await game.Top(); else { await game.Act("P1", Cast("NEXT")); await game.Top(); }
        Assert.Equal(alreadyPaid ? 28 : 27, game.State.RunePools["P1"].Mana);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ActualSongAssemblyCopiesBothCostComponentsButNeverDoublesTheLastPowerPip()
    {
        var initial = RadGearTestPosition.Put(Position(spell: "SFD·080/221"), "GEAR", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        await Challenge(game);
        var echo = new PlayCardCommand(SpellObject, "SFD·080/221", ["ENEMY"], OptionalCosts: ["ECHO"]);
        var quote = Quote(game.State, "P1", echo); Balanced(quote);
        Assert.Equal(1, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.Equal(-2, Assert.Single(quote.Cost.Adjustments, a => a.Label == "战斗中单位法术减费").Power);
        var onePip = Quote(game.State, "P1", Cast("NEXT")); Balanced(onePip);
        Assert.Equal(-1, Assert.Single(onePip.Cost!.Adjustments, a => a.Label == "战斗中单位法术减费").Power);
        await game.Act("P1", echo); await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task OpposedCombatSourcesIncreaseBeforeReducingTheSameTotalCost()
    {
        var initial = RadGearTestPosition.Put(Position(), "ENEMY", Vex, "P2", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await Challenge(game);
        var command = Cast(target: "ALLY"); var quote = Quote(game.State, "P1", command); Balanced(quote);
        Assert.Equal(2, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.Equal(1, Assert.Single(quote.Cost.Adjustments, a => a.Label == "对手法术增费").Power);
        await game.Act("P1", command); await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task DifferentTotalReducersCannotBothClaimTheSameRemovedPowerInThePreview()
    {
        var initial = RadGearTestPosition.Put(Position(), "RESEARCHER", "VEN·055", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", new ActivateAbilityCommand("RESEARCHER", "EMPOWER:0:0", [])); await game.Top();
        await Challenge(game);
        var quote = Quote(game.State, "P1", Cast()); Balanced(quote);
        Assert.Equal(1, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.Equal(-1, quote.Cost.Adjustments.Sum(a => a.Power));
        await game.Act("P1", Cast()); await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnActualHeronReductionCombinesWithTheCombatFloorWithoutDuplicatingItsPowerCredit()
    {
        var initial = RadGearTestPosition.Put(Position(), "OTHER", "OGN·296/298", "P1", "BATTLEFIELD", "OTHER");
        initial = RadGearTestPosition.Put(initial, "HERON", "VEN·044", "P1", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", Cast());
        while (game.State.StackItems.Count > 0) await game.Top();
        await Challenge(game);
        var quote = Quote(game.State, "P1", Cast("NEXT")); Balanced(quote);
        Assert.Equal(0, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.Equal(-1, Assert.Single(quote.Cost.Adjustments, a => a.Label == "下次法术减费").Power);
        Assert.Equal(-1, Assert.Single(quote.Cost.Adjustments, a => a.Label == "战斗中单位法术减费").Mana);
        await game.Act("P1", Cast("NEXT")); await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheSharedAttributionAlsoExplainsTheActualNextUnitManaAndPowerReduction()
    {
        var initial = LegendAttachmentTests.PositionFor("RAD·139/167");
        initial = RadGearTestPosition.Put(initial, "TIMED", "SFD·143/221", "P1", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["LEGEND"] = initial.CardObjects["LEGEND"] with { IsEmpowered = true } },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 20) } };
        var game = new Driver(initial);
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, [], [])); await game.Top();
        var command = new PlayCardCommand("TIMED", "SFD·143/221", []);
        var quote = Quote(game.State, "P1", command); Balanced(quote);
        var adjustment = Assert.Single(quote.Cost!.Adjustments, a => a.Label == "下次单位减费");
        Assert.Equal(-2, adjustment.Mana); Assert.Equal(-1, adjustment.Power);
        await game.Act("P1", command); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task FriendshipUsesCurrentControlAndARealRecallRemovesTheNewControllersCombatAura()
    {
        var initial = RadGearTestPosition.Put(Position(), "TAKE", "OGN·203/298", "P2", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = "P2" } } };
        var game = new Driver(initial); await Challenge(game);
        Assert.Equal(1, Quote(game.State, "P1", Cast()).Cost!.Mana);
        await Focus(game, "P2");
        var take = new PlayCardCommand("TAKE", "OGN·203/298", ["HOST"]); var quote = Quote(game.State, "P2", take); Balanced(quote);
        Assert.Equal(9, quote.Cost!.Mana);
        await game.Act("P2", take); await game.Top();
        Assert.Equal("P2", game.State.CardObjects["HOST"].ControllerId); Assert.Contains("HOST", game.State.PlayerZones["P2"].Base);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Smoke, out var behavior));
        Assert.Equal(2, CoreRuleEngine.CalculatePlayManaCost(game.State, "P1", behavior).Total);
        Assert.Equal(2, CoreRuleEngine.CalculatePlayManaCost(game.State, "P2", behavior).Total);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("OGN·219/298"), InlineData("SFD·022/221")]
    public async Task UnitsAndEquipmentDoNotAcquireTheSpellOnlyAdjustment(string card)
    {
        var game = new Driver(Position()); await Challenge(game);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(card, out var behavior));
        Assert.Equal(behavior.ManaCost, CoreRuleEngine.CalculatePlayManaCost(game.State, "P1", behavior).Total);
        Assert.Equal(behavior.ManaCost, CoreRuleEngine.CalculatePlayManaCost(game.State, "P2", behavior).Total);
        Assert.True(CoreRuleEngine.TrySelectPlayPowerCost(game.State, "P2", behavior, null, new(30, 30), 0,
            new Dictionary<string, int>(), out var generic, out var typed));
        Assert.Equal(PrintedPowerCostRules.ForCard(card).Amount, generic + typed.Values.Sum());
        await game.ColdCheckpoint();
    }
}
