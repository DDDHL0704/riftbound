using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadTimeAssassinLegendTests
{
    private const string Ability = LegendActionAbilityCatalog.TimeAssassinLegendAbilityId;
    private const string Unit = "SFD·143/221";

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(Unit);
        return LocalDestructionRecallTests.At(state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"], Hand = ["UNIT", "SECOND"] }
            },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·139/167", "P1") with { IsEmpowered = true },
                ["SECOND"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND", Unit, "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND"), ["SECOND"] = new("P1", "HAND")
            }
        }, "ALLY", "P1", "BATTLEFIELD", "BF");
    }

    private static LegendActCommand Activate() => new("LEGEND", Ability, [], []);
    private static async Task Activate(Driver game)
    {
        await game.Act("P1", Activate());
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await game.Top();
    }
    private static PlayCardCommand Play(string id = "UNIT", string destination = "BASE") => new(id, Unit, [], Destination: destination);

    public static IEnumerable<object[]> Printings() => new[] {
        ("RAD·139/167", 39647L), ("RAD·168*/167", 39676L), ("RAD·168/167", 39677L)
    }.Select(row => new object[] { row.Item1, row.Item2 });

    [Theory, MemberData(nameof(Printings))]
    public async Task EveryOfficialPrintingKeepsTheEmpowerTextAndExposesTheLegendAction(string cardNo, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, value => value.CardNo == cardNo);
        Assert.Equal(id, card.Id);
        Assert.Contains("下一名单位的费用减少{{2}}和{{A}}", card.CardEffect, StringComparison.Ordinal);
        Assert.Contains(cardNo, LegendActionAbilityCatalog.SourceCardNosForAbility(Ability));
    }

    [Fact]
    public async Task EmpoweredTimeAssassinCreatesOneNextUnitReductionAndRemovesItsEmpower()
    {
        var state = LegendAttachmentTests.PositionFor("RAD·139/167") with {
            CardObjects = new Dictionary<string, CardObjectState>(LegendAttachmentTests.PositionFor("RAD·139/167").CardObjects) {
                ["LEGEND"] = LegendAttachmentTests.PositionFor("RAD·139/167").CardObjects["LEGEND"] with { IsEmpowered = true }
            }
        };
        var game = new Driver(state);
        await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], []));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.NotNull(Assert.Single(game.State.StackItems).LegendUnitEffect);
        await game.Top();
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.TimeAssassinNextUnitReductionPrefix, StringComparison.Ordinal));
        Assert.True(CoreRuleEngine.ValidCurrentTimeAssassinNextUnitReductions(game.State));
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "TIME_ASSASSIN_NEXT_UNIT_REDUCTION_CREATED");
        Assert.NotNull(CoreRuleEngine.ReadTimeAssassinNextUnitReduction(marker));
    }

    [Fact]
    public async Task TargetRestrictedCounterCannotSelectTheUntargetedLegendSkill()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "COUNTER", "SFD·045/221", "P2");
        var game = new Driver(initial);
        await game.Act("P1", Activate());
        var skill = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("illegal-counter", "P2", CommandTypes.PlayCard),
            new PlayCardCommand("COUNTER", "SFD·045/221", [skill]), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State));
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF")]
    public async Task NextUnitPaysTwoLessManaAndOneLessTypedPowerThenDiesThroughTheEndStep(string destination)
    {
        var game = new Driver(Position());
        await Activate(game);
        var before = game.State.RunePools["P1"];
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, Play(destination: destination)));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", Play(destination: destination));
        Assert.Equal(before.Mana - 2, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.Single(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));
        var paid = game.Events().Last(entry => entry.Event.Kind == "COST_PAID").Event;
        Assert.Equal(2, paid.Payload["timeAssassinCostReductionMana"]);
        var second = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, Play("SECOND")));
        Assert.Equal(4, second.Cost!.Mana);
        Assert.Equal(1, second.Cost.PowerByTrait[RuneTrait.Purple]);
        await game.Act("P1", Play("SECOND"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Contains("UNIT", destination == "BASE" ? game.State.PlayerZones["P1"].Base : game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(CoreRuleEngine.TimeAssassinEndDestructionKind, Assert.Single(game.State.StackItems).EndAbility!.Kind);
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("SECOND", game.State.PlayerZones["P1"].Base);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "UNIT"));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·048/298")]
    [InlineData("SFD·022/221")]
    public async Task NonUnitPlaysLeaveTheReductionAvailable(string cardNo)
    {
        var initial = Position();
        var card = OfficialGarenConquestBattlefieldCountTests.Printed("OTHER-CARD", cardNo, "P1");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = [..initial.PlayerZones["P1"].Hand, "OTHER-CARD"] }
            },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["OTHER-CARD"] = card },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["OTHER-CARD"] = new("P1", "HAND") }
        };
        var game = new Driver(initial);
        await Activate(game);
        await game.Act("P1", new PlayCardCommand("OTHER-CARD", cardNo,
            card.Tags.Contains(CardObjectTags.EquipmentCard) ? ["ALLY"] : [],
            Destination: card.Tags.Contains(CardObjectTags.EquipmentCard) ? "BASE" : ""));
        if (game.State.StackItems.Count > 0) await game.Top();
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await game.Act("P1", Play());
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualReturnClearsTheMarkerAndTheOldEndSkillCannotDestroyTheNewGeneration(bool duringEndStep)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position(), "RETURN", "OGN·104/298");
        var game = new Driver(initial);
        await Activate(game);
        await game.Act("P1", Play());
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        if (duringEndStep) await game.Act("P1", new EndTurnCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation);
        Assert.DoesNotContain(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));
        if (duringEndStep) await game.Top();
        else {
            await game.Act("P1", Play());
            await game.Act("P1", new EndTurnCommand());
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        }
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "UNIT"));
        await game.Replay();
    }

    [Theory]
    [InlineData("unempowered")]
    [InlineData("exhausted")]
    [InlineData("enemy")]
    [InlineData("target")]
    public async Task IllegalActivationPreservesEveryResourceAndObject(string scenario)
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsEmpowered = scenario != "unempowered", IsExhausted = scenario == "exhausted" }
        } };
        var command = Activate() with { TargetObjectIds = scenario == "target" ? ["ALLY"] : [] };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-time-assassin", scenario == "enemy" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("turn")]
    [InlineData("source")]
    [InlineData("generation")]
    [InlineData("future")]
    [InlineData("duplicate")]
    public async Task RecoveryRejectsForgedNextUnitReductionAndKeepsItsBindingPrivate(string scenario)
    {
        var game = new Driver(Position()); await Activate(game);
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.TimeAssassinNextUnitReductionPrefix, StringComparison.Ordinal));
        var reduction = CoreRuleEngine.ReadTimeAssassinNextUnitReduction(marker)!;
        var forged = CoreRuleEngine.TimeAssassinNextUnitReductionMarker(
            scenario == "turn" ? reduction.TurnNumber + 1 : reduction.TurnNumber, reduction.ControllerId,
            scenario == "source" ? new("MISSING", 0) : scenario == "generation" ? reduction.Source with { Generation = 99 } : reduction.Source,
            reduction.CardNo, scenario == "future" ? game.State.Tick + 1 : reduction.CreatedTick);
        var altered = game.State with { UntilEndOfTurnEffects = scenario == "duplicate" ? [marker, marker] : [forged] };
        Assert.Contains("invalid time assassin next unit reduction", MatchRecoveryValidator.ValidateComponentStateAndViews(altered));
        foreach (var snapshot in ResolutionResult.BuildSnapshots(game.State).Values)
            Assert.DoesNotContain(CoreRuleEngine.TimeAssassinNextUnitReductionPrefix, JsonSerializer.Serialize(snapshot));
    }
}
