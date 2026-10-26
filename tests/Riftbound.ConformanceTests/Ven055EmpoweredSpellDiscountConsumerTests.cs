using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven055EmpoweredSpellDiscountConsumerTests
{
    private const string No = "VEN·055";
    private const string Text = "{{强化3}}（支付{{3}}：强化我。仅在未强化时可用。）\n{{已强化>}} 你的法术费用减少{{1}}和{{A}}，不得低于{{1}}。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactNewPrintingPreservesEveryPriorFunctionalGroupAndMember()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var priorCards = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1081, priorCards.Length); Assert.Equal(1082, catalog.Cards.Count);
        var prior = FunctionalUnitBuilder.Build(priorCards); var current = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(862, prior.Count); Assert.Equal(863, current.Count);
        foreach (var unit in prior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == unit.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
    }

    [Fact]
    public async Task ExactCnPrintingSeparatesFourManaFourMightFromTheThreeManaEmpowerAndConditionalSpellDiscount()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37746, card.Id); Assert.Equal("实干研究员", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(4, card.Energy); Assert.Equal(4, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal([RuneTrait.Blue], card.CardColorList); Assert.Equal("恕瑞玛", card.Region);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(4, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower);
        Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(3, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower); Assert.True(cost.SupportedResources);
    }

    [Theory]
    [InlineData(false, "BASE")] [InlineData(true, "BASE")]
    [InlineData(false, "BATTLEFIELD:BF")] [InlineData(true, "BATTLEFIELD:BF")]
    public async Task ActualEmpowerEnablesBothSpellDiscountComponentsInBaseOrBattlefield(bool empower, string destination)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination));
            Assert.Equal(36, game.State.RunePools["P1"].Mana); Assert.Equal(4, Might(game.State, "UNIT"));
            if (empower) { await game.Act("P1", Activate()); await game.Top(); }
            Assert.Equal(empower, game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Might(game.State, "UNIT"));
            var before = game.State.RunePools["P1"];
            await game.Act("P1", Smoke());
            Assert.Equal(before.Mana - (empower ? 1 : 2), game.State.RunePools["P1"].Mana);
            Assert.Equal(before.PowerByTrait[RuneTrait.Blue] - (empower ? 0 : 1), game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Assert.Equal(4, Might(game.State, "ENEMY", "P2")); Restore(initial, game);
            await game.Top(); Assert.Equal(1, Might(game.State, "ENEMY", "P2"));
            Assert.Equal(4, Might(game.State, "UNIT")); Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("mana-power-" + empower + "-" + destination.Replace(':', '-'), initial, game); }
    }

    [Fact]
    public async Task AReactionCastBeforeEmpowerResolvesStillPaysItsFullPrintedCost()
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate());
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(33, game.State.RunePools["P1"].Mana);
            await game.Act("P1", Smoke()); Assert.Equal(31, game.State.RunePools["P1"].Mana);
            Assert.Equal(3, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            await game.Top(); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(1, Might(game.State, "ENEMY", "P2")); Restore(initial, game);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(31, game.State.RunePools["P1"].Mana); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("pending-empower-no-discount", initial, game); }
    }

    [Fact]
    public async Task TheOneManaFloorDoesNotPreventTheBluePowerCostFromBecomingZero()
    {
        var initial = Position("SFD·080/221"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            await game.Act("P1", new PlayCardCommand("SPELL", "SFD·080/221", ["ENEMY"]));
            Assert.Equal(32, game.State.RunePools["P1"].Mana); Assert.Equal(4, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage); await game.Top();
            Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("floor-one-power-zero", initial, game); }
    }

    [Theory]
    [InlineData(8)] [InlineData(40)]
    public async Task ActualEmpowerAllowsARealBlueSpellWithNoPowerInThePool(int initialMana)
    {
        var initial = Position() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(initialMana, 0), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            Assert.Equal(initialMana - 7, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", Smoke()); Assert.Equal(initialMana - 8, game.State.RunePools["P1"].Mana);
            Assert.Equal(0, game.State.RunePools["P1"].TotalPower); await game.Top();
            Assert.Equal(1, Might(game.State, "ENEMY", "P2")); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("no-power-pool-" + initialMana, initial, game); }
    }

    [Fact]
    public async Task GenericPowerReductionAlsoRemovesARedSpellsPowerAndIsNotLimitedToTheSourcesBlueTrait()
    {
        var initial = Position("UNL-007/219") with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(40, 0), ["P2"] = new(20, 20) } };
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            await game.Act("P1", new PlayCardCommand("SPELL", "UNL-007/219", ["ENEMY"]));
            Assert.Equal(32, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage); Restore(initial, game);
            await game.Top(); Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
            Assert.DoesNotContain("ENEMY", game.State.PlayerZones["P2"].Graveyard); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("generic-reduction-removes-red-power", initial, game); }
    }

    [Fact]
    public async Task AZeroPrintedManaSpellIsNotRaisedToTheAurasFloorAndStillKeepsItsLookPrivate()
    {
        var initial = Position("SFD·122/221") with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(7, 0), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", new PlayCardCommand("SPELL", "SFD·122/221", []));
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Null(game.State.PendingCardChoice); Restore(initial, game); await game.Top();
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal(new[] { "P1-DRAW-1", "P1-DRAW-2" }, choice.ContextObjectIds);
            foreach (var viewer in new[] { "P2" })
                foreach (var id in choice.ContextObjectIds)
                    Assert.DoesNotContain(id, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)[viewer], Json));
            Restore(initial, game); await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.LegalObjectIds[0]]));
            Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("zero-base-mana-floor-no-increase", initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualSourceReturnChangesFutureCostsButCannotRefundAnAlreadyPaidSpell(bool afterPayment)
    {
        var initial = Add(Position(), "RETURN", "OGN·104/298"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            if (afterPayment) await game.Act("P1", Smoke());
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            if (!afterPayment) await game.Act("P1", Smoke());
            Assert.Equal(afterPayment ? 31 : 30, game.State.RunePools["P1"].Mana);
            Assert.Equal(afterPayment ? 4 : 3, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Restore(initial, game); await game.Top(); Assert.Equal(1, Might(game.State, "ENEMY", "P2"));
            Assert.Equal(afterPayment ? 31 : 30, game.State.RunePools["P1"].Mana); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("source-return-payment-" + afterPayment, initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnExplicitQuotedPowerReductionIsRecheckedAfterTheSourceReturns(bool sourceReturns)
    {
        var initial = Add(Position(), "RETURN", "OGN·104/298"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            var hash = MatchStateHasher.Hash(game.State);
            var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, Smoke()));
            Assert.Equal(hash, MatchStateHasher.Hash(game.State));
            var encoded = JsonSerializer.SerializeToElement(quote.Cost, Json);
            Assert.True(encoded.TryGetProperty("spellPowerReductionChoices", out var choices));
            var selected = Assert.Single(choices.EnumerateArray()).GetProperty("id").GetString()!;
            Assert.Equal("SPELL_POWER_REDUCTION:blue:1", selected);
            await Reject(game.State, "P1", Smoke() with { OptionalCosts = ["SPELL_POWER_REDUCTION:blue:2"] });
            if (sourceReturns)
            {
                await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
                await Reject(game.State, "P1", Smoke() with { OptionalCosts = [selected] });
                await game.Act("P1", Smoke()); Assert.Equal(30, game.State.RunePools["P1"].Mana);
                Assert.Equal(3, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            }
            else
            {
                await game.Act("P1", Smoke() with { OptionalCosts = [selected] });
                Assert.Equal(32, game.State.RunePools["P1"].Mana); Assert.Equal(4, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            }
            Assert.Equal(1, quote.Cost!.Mana); Restore(initial, game);
            await game.Top(); Assert.Equal(1, Might(game.State, "ENEMY", "P2")); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("explicit-choice-current-source-" + sourceReturns, initial, game); }
    }

    [Fact]
    public async Task TwoActualAurasRespectTheirOwnFloorAndDoNotReduceUnitOrEmpowerCosts()
    {
        var initial = Add(Position(), "SECOND", No); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            await game.Act("P1", new PlayCardCommand("SECOND", No, [])); Assert.Equal(29, game.State.RunePools["P1"].Mana);
            await game.Act("P1", Activate("SECOND")); Assert.Equal(26, game.State.RunePools["P1"].Mana); await game.Top();
            Assert.All(new[] { "UNIT", "SECOND" }, id => Assert.True(game.State.CardObjects[id].IsEmpowered));
            await game.Act("P1", Smoke()); Assert.Equal(25, game.State.RunePools["P1"].Mana);
            Assert.Equal(4, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]); await game.Top();
            Assert.Equal(1, Might(game.State, "ENEMY", "P2")); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("two-auras-unit-ability-exclusion", initial, game); }
    }

    [Fact]
    public async Task TwoActualSourcesRemoveBothEchoPowerPipsAndKeepThePaidRepeat()
    {
        var initial = Add(Position("SFD·080/221"), "SECOND", No); var game = new Driver(initial);
        try
        {
            foreach (var id in new[] { "UNIT", "SECOND" })
            { await game.Act("P1", new PlayCardCommand(id, No, [])); await game.Act("P1", Activate(id)); await game.Top(); }
            await game.Act("P1", new PlayCardCommand("SPELL", "SFD·080/221", ["ENEMY"],
                OptionalCosts: ["ECHO", "SPELL_POWER_REDUCTION:blue:2"]));
            Assert.Equal(25, game.State.RunePools["P1"].Mana); Assert.Equal(4, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount); Restore(initial, game);
            await game.Top(); Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("two-real-auras-two-power", initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AQuotedVectorSurvivesOneOfTwoSourcesLeavingOnlyWhenTheRemainingAmountIsEnough(bool twoPips)
    {
        var spell = twoPips ? "SFD·080/221" : "OGN·093/298";
        var initial = Add(Add(Position(spell), "SECOND", No), "RETURN", "OGN·104/298"); var game = new Driver(initial);
        try
        {
            foreach (var id in new[] { "UNIT", "SECOND" })
            { await game.Act("P1", new PlayCardCommand(id, No, [])); await game.Act("P1", Activate(id)); await game.Top(); }
            var command = new PlayCardCommand("SPELL", spell, ["ENEMY"], OptionalCosts: twoPips ? ["ECHO"] : []);
            var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
            var encoded = JsonSerializer.SerializeToElement(quote.Cost, Json);
            var selected = Assert.Single(encoded.GetProperty("spellPowerReductionChoices").EnumerateArray()).GetProperty("id").GetString()!;
            Assert.Equal(twoPips ? "SPELL_POWER_REDUCTION:blue:2" : "SPELL_POWER_REDUCTION:blue:1", selected);
            Assert.Equal(0, quote.Cost!.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.True(game.State.CardObjects["SECOND"].IsEmpowered);
            if (twoPips)
            {
                await Reject(game.State, "P1", command with { OptionalCosts = ["ECHO", selected] });
                var fresh = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
                var freshId = Assert.Single(JsonSerializer.SerializeToElement(fresh.Cost, Json).GetProperty("spellPowerReductionChoices").EnumerateArray()).GetProperty("id").GetString()!;
                Assert.Equal("SPELL_POWER_REDUCTION:blue:1", freshId);
                await game.Act("P1", command with { OptionalCosts = ["ECHO", freshId] });
            }
            else await game.Act("P1", command with { OptionalCosts = [selected] });
            Assert.Equal(24, game.State.RunePools["P1"].Mana); Assert.Equal(twoPips ? 3 : 4, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Restore(initial, game); await game.Top();
            if (twoPips) Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
            else Assert.Equal(1, Might(game.State, "ENEMY", "P2"));
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("current-aggregate-not-source-identity-" + twoPips, initial, game); }
    }

    [Fact]
    public async Task AnotherCardsReductionCanReachZeroAfterThisAurasOwnOneManaFloor()
    {
        var initial = Position("VEN·059") with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(7, 0), ["P2"] = new(20, 20) } };
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", new PlayCardCommand("SPELL", "VEN·059", ["ENEMY"]));
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Restore(initial, game); await game.Top(); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("own-floor-then-ven059-zero", initial, game); }
    }

    [Theory]
    [InlineData("BASE", 2)] [InlineData("BATTLEFIELD:BF", 1)]
    public async Task EagerApprenticeKeepsItsDifferentBattlefieldOnlyManaOnlyRestriction(string destination, int mana)
    {
        var initial = Add(VenUnitEntryBasicTests.Position("OGN·084/298"), "SPELL", "OGN·093/298"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", "OGN·084/298", [], Destination: destination));
            var before = game.State.RunePools["P1"]; await game.Act("P1", Smoke());
            Assert.Equal(before.Mana - mana, game.State.RunePools["P1"].Mana);
            Assert.Equal(before.TotalPower - 1, game.State.RunePools["P1"].TotalPower);
            await game.Top(); Assert.Equal(1, Might(game.State, "ENEMY", "P2")); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("old-apprentice-scope-" + destination.Replace(':', '-'), initial, game); }
    }

    [Fact]
    public async Task AnEmpoweredSpellAuraCannotReduceThePrintedEquipmentManaOrRedPower()
    {
        var initial = Add(Position(), "SWORD", "SFD·022/221") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 4, [RuneTrait.Red] = 1 }), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            await game.Act("P1", new PlayCardCommand("SWORD", "SFD·022/221", ["UNIT"]));
            Assert.Equal(31, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
            Assert.Equal(4, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            if (game.State.StackItems.Count > 0) await game.Top();
            Assert.Equal("UNIT", game.State.CardObjects["SWORD"].AttachedToObjectId); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("equipment-is-not-spell", initial, game); }
    }

    [Fact]
    public async Task TheAuraReducesTheTotalEchoCostWithoutRemovingThePaidEchoOrBothBlueSymbols()
    {
        var initial = Position("SFD·080/221"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            await game.Act("P1", new PlayCardCommand("SPELL", "SFD·080/221", ["ENEMY"], OptionalCosts: ["ECHO"]));
            Assert.Equal(32, game.State.RunePools["P1"].Mana); Assert.Equal(3, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount); Restore(initial, game);
            await game.Top(); Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("echo-total-mana-power", initial, game); }
    }

    [Theory]
    [InlineData(RuneTrait.Blue)] [InlineData(RuneTrait.Red)]
    public async Task GenericPowerReductionAndWardCanBePaidWithEitherRemainingTrait(string remainingTrait)
    {
        var initial = Add(Position(), "WARDED", "OGN·013/298", "P2") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        for (var i = 0; i < 9; i++) initial = AddBase(initial, "PAY-RUNE-" + i, remainingTrait == RuneTrait.Blue ? "UNL-R03" : "UNL-R01", "P1");
        var game = new Driver(initial);
        try
        {
            await game.Act("P2", new PlayCardCommand("WARDED", "OGN·013/298", []));
            Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects["WARDED"].Tags);
            await game.Act("P2", new EndTurnCommand()); Assert.Equal(0, game.State.RunePools["P1"].Mana);
            for (var i = 0; i < 9; i++) await game.Act("P1", new TapRuneCommand("PAY-RUNE-" + i));
            await game.Act("P1", new RecycleRuneCommand("PAY-RUNE-0"));
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            Assert.Equal(2, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].TotalPower);
            var before = MatchStateHasher.Hash(game.State);
            var command = Smoke() with { TargetObjectIds = ["WARDED"] };
            var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
            Assert.Equal(before, MatchStateHasher.Hash(game.State));
            await game.Act("P1", command); Assert.True(quote.CanPay, quote.Message);
            Assert.Equal(1, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
            Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Top(); Assert.Equal(1, Might(game.State, "WARDED", "P2")); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("ward-legal-allocation-" + remainingTrait, initial, game); }
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task AnExplicitEchoReductionMayRemoveWardPowerFromOnlyOneOfTwoDifferentTargets(bool wardFirst)
    {
        var initial = Add(Position("SFD·080/221"), "WARDED", "OGN·013/298", "P2") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        for (var i = 0; i < 9; i++) initial = AddBase(initial, "ECHO-RUNE-" + i, "UNL-R03", "P1");
        var game = new Driver(initial);
        try
        {
            await game.Act("P2", new PlayCardCommand("WARDED", "OGN·013/298", []));
            Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects["WARDED"].Tags);
            await game.Act("P2", new EndTurnCommand());
            for (var i = 0; i < 9; i++) await game.Act("P1", new TapRuneCommand("ECHO-RUNE-" + i));
            foreach (var i in new[] { 0, 1 }) await game.Act("P1", new RecycleRuneCommand("ECHO-RUNE-" + i));
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(2, game.State.RunePools["P1"].Mana); Assert.Equal(2, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            var first = wardFirst ? "WARDED" : "ENEMY"; var second = wardFirst ? "ENEMY" : "WARDED";
            var command = new PlayCardCommand("SPELL", "SFD·080/221", [first], OptionalCosts: ["ECHO"],
                RepeatChoices: [new SpellRepeatChoice("", [second])]);
            var before = MatchStateHasher.Hash(game.State); var engine = new CoreRuleEngine();
            var quote = engine.PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
            Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana);
            Assert.Equal(2, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
            Assert.Contains(JsonSerializer.SerializeToElement(quote.Cost, Json).GetProperty("spellPowerReductionChoices").EnumerateArray(),
                choice => choice.GetProperty("id").GetString() == "SPELL_POWER_REDUCTION:any:1");
            command = command with { OptionalCosts = ["ECHO", "SPELL_POWER_REDUCTION:any:1"] };
            var explicitQuote = engine.PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
            Assert.Equal(before, MatchStateHasher.Hash(game.State));
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN055_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "echo-distinct-ward-" + wardFirst + "-quotes.json"),
                    JsonSerializer.Serialize(new { before, quote, explicitQuote, command }, Json));
            }
            await game.Act("P1", command); Assert.True(explicitQuote.CanPay, explicitQuote.Message);
            Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            var item = Assert.Single(game.State.StackItems); Assert.Equal(2, item.EffectRepeatCount);
            Assert.Equal(new[] { first, second }, item.RepeatExecutions!.SelectMany(execution => execution.TargetObjectIds));
            Restore(initial, game); await game.Top();
            Assert.Equal(1, game.State.CardObjects["WARDED"].Damage); Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage);
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("echo-distinct-ward-" + wardFirst, initial, game); }
    }

    [Theory]
    [InlineData("keep")] [InlineData("before")] [InlineData("after")]
    public async Task AnActuallyCopiedEmpowerSourceGrantsTheAuraOnlyWhileItsCopiedTextRemains(string detach)
    {
        var initial = Add(Add(ShadySpectaclesTests.Position(No), "SPELL", "OGN·093/298"), "DETACH", "SFD·011/221"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await game.Top();
            var choice = game.State.PendingRuleChoice!; Assert.Equal("ATTACHMENT_COPY_SELECTION", choice.Request.Kind);
            await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
            Assert.Equal(No, game.State.CardObjects["HOST"].CardNo); Assert.False(game.State.CardObjects["COPY-SOURCE"].IsEmpowered);
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
            var row = Assert.Single(rows, r => (string?)r["sourceObjectId"] == "HOST" && (string?)r["abilityId"] == "EMPOWER:0:0");
            var instance = Assert.IsType<string>(row["abilityInstanceId"]); await Reject(game.State, "P1", Activate("HOST"));
            await game.Act("P1", Activate("HOST") with { AbilityInstanceId = instance });
            Assert.Equal("GEAR", Assert.Single(game.State.StackItems).EmpowerActivation!.Origin!.Equipment.ObjectId);
            await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered); Assert.False(game.State.CardObjects["COPY-SOURCE"].IsEmpowered);
            if (detach == "before") { await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top(); }
            var pool = game.State.RunePools["P1"];
            await game.Act("P1", Smoke() with { TargetObjectIds = ["COPY-SOURCE"] });
            Assert.Equal(pool.Mana - (detach == "before" ? 2 : 1), game.State.RunePools["P1"].Mana);
            Assert.Equal(pool.TotalPower - (detach == "before" ? 1 : 0), game.State.RunePools["P1"].TotalPower);
            if (detach == "after") { await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top(); }
            Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            if (detach != "keep") Assert.Null(game.State.CardObjects["HOST"].AttachmentUnitCopies);
            Restore(initial, game); await game.Top(); Assert.Equal(1, Might(game.State, "COPY-SOURCE")); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("actual-copy-discount-" + detach, initial, game); }
    }

    [Fact]
    public async Task ActualControlTransferGivesTheAuraToTheNewControllerAndChargesTheFormerOwnerFullCost()
    {
        var initial = Add(Add(Position(), "TAKE", "OGN·203/298", "P2"), "P2-SMOKE", "OGN·093/298", "P2");
        for (var i = 0; i < 9; i++) initial = AddBase(initial, "PURPLE-" + i, "UNL-R05", "P2");
        for (var i = 0; i < 2; i++) initial = AddBase(initial, "BLUE-" + i, "UNL-R03", "P1");
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BATTLEFIELD:BF")); await game.Act("P1", Activate()); await game.Top();
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new EndTurnCommand()); Assert.Equal(0, game.State.RunePools["P2"].Mana);
            for (var i = 0; i < 9; i++) await game.Act("P2", new TapRuneCommand("PURPLE-" + i));
            for (var i = 0; i < 3; i++) await game.Act("P2", new RecycleRuneCommand("PURPLE-" + i));
            await game.Act("P2", new PlayCardCommand("TAKE", "OGN·203/298", ["UNIT"])); await game.Top();
            Assert.Equal("P1", game.State.CardObjects["UNIT"].OwnerId); Assert.Equal("P2", game.State.CardObjects["UNIT"].ControllerId);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(1, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            await game.Act("P2", new PlayCardCommand("P2-SMOKE", "OGN·093/298", ["UNIT"]));
            Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower); await game.Top();
            Assert.Equal(1, VisibleMight(game.State, "UNIT", "P2")); Restore(initial, game);
            await game.Act("P2", new EndTurnCommand()); Assert.Equal(4, VisibleMight(game.State, "UNIT", "P1"));
            await game.Act("P1", new TapRuneCommand("BLUE-0")); await game.Act("P1", new TapRuneCommand("BLUE-1"));
            await game.Act("P1", new RecycleRuneCommand("BLUE-0")); await game.Act("P1", Smoke() with { TargetObjectIds = ["UNIT"] });
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Top(); Assert.Equal(1, VisibleMight(game.State, "UNIT", "P1")); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("actual-current-controller-not-owner", initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task CanonicalOpeningPaysOneManaWithNoPowerAndRejectsCoherentAuraAndPaymentForgeries(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "lillia-lowcurve", "UNL-R03", [No, No, No, "OGN·093/298", "OGN·093/298", "OGN·093/298"]);
        try
        {
            await game.WaitFor(actor, 8, No, "OGN·093/298");
            foreach (var rune in game.Runes(actor).Take(8)) await game.Act(actor, new TapRuneCommand(rune));
            Assert.Equal(8, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            var unit = game.Hand(actor, No).First(); await game.Act(actor, new PlayCardCommand(unit, No, []));
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", [])); Assert.Equal(3, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
            await game.ColdCheckpoint(); await game.Top(); Assert.Equal(1, game.State.RunePools[actor].Mana); Assert.Equal(4, game.Might(actor, unit));
            var spell = game.Hand(actor, "OGN·093/298").First(); await game.Act(actor, new PlayCardCommand(spell, "OGN·093/298", [unit]));
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower); await game.ColdCheckpoint();
            await game.RejectCoordinatedForgery("refunded-spell-aura-mana", game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
                [actor] = game.State.RunePools[actor] with { Mana = 1 } } });
            await game.RejectCoordinatedForgery("erased-resolved-aura-source-empower", game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                [unit] = game.State.CardObjects[unit] with { IsEmpowered = false } } });
            await game.Top(); Assert.Equal(1, game.Might(actor, unit)); Assert.True(game.State.CardObjects[unit].IsEmpowered);
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(4, game.Might(actor, unit)); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven055-" + actor); }
    }

    private static ActivateAbilityCommand Activate(string id = "UNIT") => new(id, "EMPOWER:0:0", []);
    private static PlayCardCommand Smoke() => new("SPELL", "OGN·093/298", ["ENEMY"]);
    private static MatchState Position(string spell = "OGN·093/298") => Add(VenUnitEntryBasicTests.Position(No), "SPELL", spell) with {
        RunePools = new Dictionary<string, RunePool> { ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 4 }), ["P2"] = new(20, 20) } };
    private static MatchState Add(MatchState state, string id, string no, string owner = "P1") => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static int Might(MatchState state, string id, string owner = "P1") => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json)
        .GetProperty("players").GetProperty(owner).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static int VisibleMight(MatchState state, string id, string viewer) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[viewer], Json)
        .GetProperty("players").EnumerateObject().SelectMany(p => p.Value.GetProperty("objects").EnumerateObject()).Single(p => p.Name == id).Value.GetProperty("effectivePower").GetInt32();
    private static MatchState AddBase(MatchState state, string id, string no, string owner) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "BASE") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] } } };
    private static async Task Reject(MatchState state, string actor, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("invalid-ven055", actor, command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(rejected.State)));
    }
    private static void Restore(MatchState initial, Driver game)
    {
        var state = game.State; var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator); Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var player in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static async Task Save(string name, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN055_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit midgame actual play and Empower commands; not canonical opening or whole-card certification",
            initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
    }
}
