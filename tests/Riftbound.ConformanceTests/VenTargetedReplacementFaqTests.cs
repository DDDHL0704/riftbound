using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenTargetedReplacementFaqTests
{
    private static MatchState Spell(MatchState state, string id, string cardNo)
        => AttackDamageDistributionTests.Spell(state, id, cardNo, "P1");

    private static async Task EnterAndEmpower(Driver game, string printing, bool empower, bool battlefield = false)
    {
        await game.Act("P1", new PlayCardCommand("HOST", printing, [], Destination: battlefield ? "BATTLEFIELD:F" : ""));
        if (game.State.StackItems.Count > 0) await game.Top();
        if (empower) {
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
            await game.Top();
            Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        }
    }

    // CN VEN official FAQ pp. 12-14 and Gangplank's printed replacement.
    [Theory]
    [InlineData("VEN·086", false)]
    [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)]
    [InlineData("VEN·181", true)]
    public async Task TargetedMightReductionBecomesThreeMightOnlyForAnEmpoweredSource(string printing, bool empower)
    {
        var initial = AttackDamageDistributionTests.Spell(EmpowerTests.Position(printing), "EXTORT", "OGN·095/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", printing, []));
        Assert.Equal(6, ArmamentMightTests.Might(game.State));
        if (empower) {
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
            await game.Top();
            Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.Equal(6, ArmamentMightTests.Might(game.State));
        }
        var command = new PlayCardCommand("EXTORT", "OGN·095/298", ["HOST"]);
        var unpayable = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
            ["P1"] = RunePool.Empty } };
        var rejected = await new CoreRuleEngine().ResolveAsync(unpayable,
            new("unpayable-extort", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(unpayable), MatchStateHasher.Hash(rejected.State));

        await game.Act("P1", command);
        await game.Top();
        Assert.Equal(empower ? 9 : 5, ArmamentMightTests.Might(game.State));
        Assert.Equal(empower, game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(6, ArmamentMightTests.Might(game.State));
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086", "UNL-042/219", false)]
    [InlineData("VEN·086", "UNL-042/219", true)]
    [InlineData("VEN·181", "UNL-042/219", false)]
    [InlineData("VEN·181", "UNL-042/219", true)]
    [InlineData("VEN·086", "OGN·104/298", false)]
    [InlineData("VEN·086", "OGN·104/298", true)]
    [InlineData("VEN·181", "OGN·104/298", false)]
    [InlineData("VEN·181", "OGN·104/298", true)]
    public async Task TargetedStunAndReturnAreReplacedOnlyWhileEmpowered(string printing, string spell, bool empower)
    {
        var game = new Driver(Spell(EmpowerTests.Position(printing), "SPELL", spell));
        await EnterAndEmpower(game, printing, empower);
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        var runes = game.State.PlayerZones["P1"].RuneDeck.Count;
        await game.Act("P1", new PlayCardCommand("SPELL", spell, ["HOST"]));
        await game.Top();
        if (empower) {
            Assert.Equal(9, ArmamentMightTests.Might(game.State));
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
            Assert.DoesNotContain("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
            Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
            Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck.Count);
        } else if (spell == "UNL-042/219") {
            Assert.Equal(6, ArmamentMightTests.Might(game.State));
            Assert.Contains("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        } else {
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
            Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > generation);
            Assert.Equal(runes - 1, game.State.PlayerZones["P1"].RuneDeck.Count);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086", false)]
    [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)]
    [InlineData("VEN·181", true)]
    public async Task SwapSnapshotsBothDeltasBeforeReplacingTheReduction(string printing, bool reverse)
    {
        var initial = OfficialHoldSequenceTests.AddUnit(EmpowerTests.Position(printing), "SMALL", "OGN·001/298", "F", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SMALL"] = initial.CardObjects["SMALL"] with { Power = 2 } } };
        var game = new Driver(Spell(initial, "SWAP", "SFD·145/221"));
        await EnterAndEmpower(game, printing, true, battlefield: true);
        await game.Act("P1", new PlayCardCommand("SWAP", "SFD·145/221", reverse ? ["HOST", "SMALL"] : ["SMALL", "HOST"]));
        await game.Top();
        Assert.Equal(9, ArmamentMightTests.Might(game.State));
        Assert.Equal(6, game.State.CardObjects["SMALL"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086")]
    [InlineData("VEN·181")]
    public async Task AReductionFlooredToZeroSuppliesNoEventToReplace(string printing)
    {
        var initial = Spell(Spell(Spell(EmpowerTests.Position(printing), "SMOKE1", "OGN·093/298"),
            "SMOKE2", "OGN·093/298"), "EXTORT", "OGN·095/298");
        var game = new Driver(initial);
        await EnterAndEmpower(game, printing, false);
        foreach (var id in new[] { "SMOKE1", "SMOKE2" }) {
            await game.Act("P1", new PlayCardCommand(id, "OGN·093/298", ["HOST"]));
            await game.Top();
        }
        Assert.Equal(1, ArmamentMightTests.Might(game.State));
        await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("EXTORT", "OGN·095/298", ["HOST"]));
        await game.Top();
        Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Equal(1, ArmamentMightTests.Might(game.State));
        Assert.Equal(2, game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers.Count);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086")]
    [InlineData("VEN·181")]
    public async Task AnAlreadyStunnedUnitHasNoNewStunToReplace(string printing)
    {
        var game = new Driver(Spell(Spell(EmpowerTests.Position(printing), "FIRST", "UNL-042/219"), "SECOND", "UNL-042/219"));
        await EnterAndEmpower(game, printing, false);
        await game.Act("P1", new PlayCardCommand("FIRST", "UNL-042/219", ["HOST"]));
        await game.Top();
        await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("SECOND", "UNL-042/219", ["HOST"]));
        await game.Top();
        Assert.Equal(6, ArmamentMightTests.Might(game.State));
        Assert.Contains("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ATargetedEquipmentAbilityUsesTheSameReplacement(bool empower)
    {
        var initial = EmpowerTests.Position("VEN·086");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = new("GEAR", cardNo: OrbOfRegretTests.Card, ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.EquipmentCard]) } };
        var game = new Driver(initial);
        await EnterAndEmpower(game, "VEN·086", empower);
        await game.Act("P1", new ActivateAbilityCommand("GEAR", OrbOfRegretTests.Ability, ["HOST"]));
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Top();
        Assert.Equal(empower ? 9 : 5, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnEnemyActivatedStunUsesTheSameReplacement(bool empower)
    {
        var initial = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(
            EmpowerTests.Position("VEN·086"), "STALKER", "RAD·104/167", "OTHER", "P2"), "STALKER", "P2", "BASE");
        var cards = initial.CardObjects.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary();
        var deck = Enumerable.Range(0, 4).Select(i => "P2-DRAW-" + i).ToArray();
        foreach (var id in deck) {
            cards[id] = new(id, cardNo: "OGN·175/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]);
            locations[id] = new("P2", "MAIN_DECK");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { MainDeck = deck } } };
        var game = new Driver(initial);
        await EnterAndEmpower(game, "VEN·086", empower);
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new ActivateAbilityCommand("STALKER", P4ActivatedAbilityCatalog.RelentlessStalkerAbilityId, ["HOST"]));
        await game.Top();
        Assert.True(game.State.CardObjects["STALKER"].IsExhausted);
        Assert.Equal(empower ? 9 : 6, ArmamentMightTests.Might(game.State));
        Assert.Equal(!empower, game.State.CardObjects["HOST"].UntilEndOfTurnEffects.Contains("STUNNED"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ATargetedReturnAbilityReplacesTheReturnAndContinuesItsInstructions(bool empower)
    {
        var initial = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(
            EmpowerTests.Position("VEN·086"), "MAGE", "RAD·115/167", "F", "P1"), "MAGE", "P1", "BASE");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Orange] = 10, [RuneTrait.Purple] = 10 }) } };
        var game = new Driver(initial);
        await EnterAndEmpower(game, "VEN·086", empower);
        await game.Act("P1", new ActivateAbilityCommand("MAGE", P4ActivatedAbilityCatalog.TranspositionMageAbilityId, ["HOST"]));
        await game.Top();
        Assert.True(game.State.CardObjects["MAGE"].IsExhausted);
        Assert.Null(game.State.PendingEffectPlay);
        if (empower) {
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
            Assert.Equal(9, ArmamentMightTests.Might(game.State));
        } else {
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
            Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086")]
    [InlineData("VEN·181")]
    public async Task AReturnThatChoosesNoUnitStillReturnsAnEmpoweredUnit(string printing)
    {
        var game = new Driver(Spell(EmpowerTests.Position(printing), "UNDERTOW", "SFD·147/221"));
        await EnterAndEmpower(game, printing, true);
        await game.Act("P1", new PlayCardCommand("UNDERTOW", "SFD·147/221", []));
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086")]
    [InlineData("VEN·181")]
    public async Task APrintedAndAnAttachedCopyCannotReplaceTheSameEventTwice(string printing)
    {
        var game = new Driver(Spell(EmpowerTests.Position(printing), "STUN", "UNL-042/219"));
        await EnterAndEmpower(game, printing, true);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        if (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(printing, game.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        await game.Act("P1", new PlayCardCommand("STUN", "UNL-042/219", ["HOST"]));
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("TARGETED_ACTION_REPLACEMENT", choice.Kind);
        Assert.Equal("P1", choice.PlayerId); Assert.Equal(2, choice.Options.Count);
        Assert.Equal(6, ArmamentMightTests.Might(game.State));
        var attached = Assert.Single(choice.Options, o => o.ObjectIds!.Contains("GEAR"));
        await game.Act("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [attached.Id]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(9, ArmamentMightTests.Might(game.State));
        Assert.Single(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086", false)]
    [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)]
    [InlineData("VEN·181", true)]
    public async Task AnImmediateLegendAbilityStillCreatesItsCoinAfterTheReturnIsReplaced(string printing, bool empower)
    {
        var initial = EmpowerTests.Position(printing);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["PYKE"] = new("PYKE", cardNo: "UNL-185/219", ownerId: "P1", controllerId: "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["PYKE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["PYKE"] = new("P1", "LEGEND") } };
        var game = new Driver(initial);
        await EnterAndEmpower(game, printing, empower, battlefield: true);
        await game.Act("P1", new LegendActCommand("PYKE", LegendActionAbilityCatalog.PykeLegendAbilityId,
            ["HOST"], ["SPEND_MANA:1"]));
        Assert.True(game.State.CardObjects["PYKE"].IsExhausted);
        // Historical method identity retained; the paid skill now resolves after responses.
        Assert.Single(game.State.StackItems);
        Assert.DoesNotContain(game.State.PlayerZones["P1"].Base, id => id.StartsWith("PYKE-TOKEN-", StringComparison.Ordinal));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Battlefields);
        await game.Top();
        var coin = Assert.Single(game.State.PlayerZones["P1"].Base, id => id.StartsWith("PYKE-TOKEN-", StringComparison.Ordinal));
        Assert.True(game.State.CardObjects[coin].IsExhausted);
        Assert.Contains(CardObjectTags.EquipmentCard, game.State.CardObjects[coin].Tags);
        if (empower) {
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Battlefields);
            Assert.Equal(9, ArmamentMightTests.Might(game.State));
        } else Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
