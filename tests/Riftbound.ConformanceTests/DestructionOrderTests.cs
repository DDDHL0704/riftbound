using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.DeathObserverAuditTests;

namespace Riftbound.ConformanceTests;

public sealed class DestructionOrderTests
{
    internal static MatchState Gear(MatchState s, string id = "G1", bool unit = false) => s with {
        CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { [id] = new(id, cardNo: "OGN·077/298", power: 5,
            ownerId: "P2", controllerId: "P2", tags: unit ? [CardObjectTags.EquipmentCard, CardObjectTags.UnitCard] : [CardObjectTags.EquipmentCard]) },
        PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Base = [..s.PlayerZones["P2"].Base, id] } },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { [id] = new("P2", "BASE") }
    };
    internal static async Task<ResolutionResult> Select(MatchState state, string option)
    {
        var request = state.PendingRuleChoice!.Request;
        return await Act(state, request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
    }
    [Fact]
    public async Task DirectDestroyUsesTheSameEquipmentReplacementAsDamage()
    {
        var done = await Open(Gear(NoReplacement()), "OGN·229/298", "D");
        Assert.Contains("D", done.State.PlayerZones["P2"].Base);
        Assert.Contains("G1", done.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_DESTROYED" && e.Payload.GetValueOrDefault("targetObjectId") as string == "D");
        Restore(done.State);
    }
    [Fact]
    public async Task ControllerChoosesWhichUnitAndWhichHourglass()
    {
        var opened = await Open(Gear(Gear(NoReplacement(true)), "G2"));
        var request = Assert.IsType<RuleChoiceContinuation>(opened.State.PendingRuleChoice).Request;
        Assert.Equal(4, request.Options.Count);
        Assert.DoesNotContain(request.Options, o => o.Id == "DECLINE");
        var done = await Select(OfficialInsightAndSpellLockTests.Restore(opened.State), "GEAR:D2:G2");
        // The other mandatory hourglass now saves the other unit.
        Assert.Contains("D2", done.State.PlayerZones["P2"].Base);
        Assert.Contains("D", done.State.PlayerZones["P2"].Base);
        Assert.Equal(2, done.Events.Count(e => e.Kind == "EQUIPMENT_DESTROYED"));
        Restore(done.State);
    }
    [Theory]
    [InlineData("SETT:D:LEGEND:ANY", false)]
    [InlineData("GEAR:D:G1", true)]
    public async Task EquipmentAndSettAreChosenTogether(string choice, bool gearDies)
    {
        var opened = await Open(Gear(Position()));
        Assert.Contains(opened.State.PendingRuleChoice!.Request.Options, o => o.Id == "GEAR:D:G1");
        var done = await Select(opened.State, choice);
        Assert.Equal(gearDies, done.State.PlayerZones["P2"].Graveyard.Contains("G1"));
        Assert.Equal(gearDies ? 1 : 0, done.State.RunePools["P2"].Power);
        Restore(done.State);
    }
    [Fact]
    public async Task DecliningOptionalSettDoesNotDeclineMandatoryHourglass()
    {
        var done = await Select((await Open(Gear(Position()))).State, "DECLINE");
        Assert.Null(done.State.PendingRuleChoice);
        Assert.Contains("D", done.State.PlayerZones["P2"].Base);
        Assert.Contains("G1", done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(1, done.State.RunePools["P2"].Power);
        Restore(done.State);
    }
    [Theory]
    [InlineData("BANISH:D", true)]
    [InlineData("GEAR:D:G1", false)]
    public async Task TemporaryBanishAndEquipmentHaveNoFixedPriority(string choice, bool banished)
    {
        var s = Gear(NoReplacement());
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { UntilEndOfTurnEffects = ["BANISH_IF_DESTROYED_THIS_TURN"] } } };
        var opened = await Open(s);
        Assert.Equal(2, opened.State.PendingRuleChoice!.Request.Options.Count);
        var done = await Select(opened.State, choice);
        Assert.Equal(banished, done.State.PlayerZones["P2"].Banished.Contains("D"));
        Assert.Equal(!banished, done.State.PlayerZones["P2"].Graveyard.Contains("G1"));
        Restore(done.State);
    }
    [Fact]
    public async Task AnimatedHourglassesCannotReplaceEachOtherForever()
    {
        var s = Source(Gear(Gear(NoReplacement(), unit: true), "G2", unit: true), "UNL-129/219");
        var first = await Open(s, "OGN·229/298", "D");
        Assert.Equal(2, first.State.PendingRuleChoice!.Request.Options.Count);
        var done = await Select(first.State, "GEAR:D:G1");
        Assert.Null(done.State.PendingRuleChoice);
        Assert.Contains("G2", done.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("G1", done.State.PlayerZones["P2"].Base);
        Assert.Contains("D", done.State.PlayerZones["P2"].Base);
        Assert.Single(done.Events, e => e.Kind == "UNIT_DESTROYED");
        var trigger = Assert.Single(done.State.StackItems);
        Assert.Equal("G2", trigger.DeathObserver!.Destroyed.ObjectId);
        Restore(done.State);
    }
    [Theory]
    [InlineData("RECALL:D", false)]
    [InlineData("BANISH:D", true)]
    public async Task TemporaryRecallAndBanishAreChosenByController(string choice, bool banished)
    {
        var s = NoReplacement();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { UntilEndOfTurnEffects = [
                "RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN", "BANISH_IF_DESTROYED_THIS_TURN"] } } };
        var opened = await Open(s);
        Assert.Equal(2, opened.State.PendingRuleChoice!.Request.Options.Count);
        var done = await Select(opened.State, choice);
        Assert.Equal(banished, done.State.PlayerZones["P2"].Banished.Contains("D"));
        Assert.Equal(!banished, done.State.PlayerZones["P2"].Base.Contains("D"));
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_DESTROYED");
        if (!banished) {
            Assert.True(done.State.CardObjects["D"].IsExhausted);
            Assert.Equal(0, done.State.CardObjects["D"].Damage);
            Assert.DoesNotContain("RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN", done.State.CardObjects["D"].UntilEndOfTurnEffects);
            Assert.Contains("BANISH_IF_DESTROYED_THIS_TURN", done.State.CardObjects["D"].UntilEndOfTurnEffects);
        }
        Restore(done.State);
    }
    internal static async Task<ResolutionResult> AltarBattle(MatchState? initial = null)
    {
        var s = initial ?? NoReplacement();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["BF"] = s.CardObjects["BF"] with { CardNo = "UNL-206/219" } } };
        var moved = await Act(s, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var first = await Act(moved.State, "P1", new PassFocusCommand());
        var second = await Act(first.State, "P2", new PassFocusCommand());
        var battle = await Act(second.State, "P1", new DeclareBattleCommand("BF", ["A"], ["D"], ["COMBAT_ASSIGNMENT"]));
        return await Top(battle.State);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AltarLetsTheDyingUnitsControllerPayOrDecline(bool accept)
    {
        var opened = await AltarBattle(); var before = opened.State.RunePools["P2"].Mana;
        Assert.Equal("P2", opened.State.PendingRuleChoice!.Request.PlayerId);
        var done = await Select(OfficialInsightAndSpellLockTests.Restore(opened.State), accept ? "ALTAR:D:BF" : "DECLINE");
        Assert.Equal(before - (accept ? 3 : 0), done.State.RunePools["P2"].Mana);
        Assert.Equal(accept, done.State.PlayerZones["P2"].Base.Contains("D"));
        Assert.Equal(!accept, done.State.PlayerZones["P2"].Graveyard.Contains("D"));
        Assert.Empty(done.State.BattleResolutions[0].SurvivingDefenderObjectIds);
        Restore(done.State);
    }
    [Fact]
    public async Task AltarCanGenerateManaBeforePayingAndKeepsItAfterDeclining()
    {
        var s = NoReplacement() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20,20), ["P2"] = new(2,0) } };
        s = ReplacementResourceTests.AddResource(s, P4ActivatedAbilityCatalog.EnergyChannelCardNo);
        var opened = await AltarBattle(s);
        var resource = opened.State.PendingRuleChoice!.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:"));
        var generated = await Select(opened.State, resource.Id);
        Assert.Contains(generated.State.PendingRuleChoice!.Request.Options, o => o.Id == "ALTAR:D:BF");
        var done = await Select(generated.State, "DECLINE");
        Assert.Equal(3, done.State.RunePools["P2"].Mana);
        Assert.True(done.State.CardObjects["RESOURCE"].IsExhausted);
        Restore(done.State);
    }
    [Fact]
    public async Task ProtectedObserverSurvivesTheBatchAndSeesTheOtherDeath()
    {
        var s = Gear(NoReplacement(true));
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D2"] = s.CardObjects["D2"] with { CardNo = "UNL-129/219" } } };
        var done = await Select((await Open(s)).State, "GEAR:D2:G1");
        var trigger = Assert.Single(done.State.StackItems);
        Assert.Equal("D2", trigger.SourceObjectId);
        Assert.Equal("D", trigger.DeathObserver!.Destroyed.ObjectId);
        Restore(done.State);
    }
    [Fact]
    public async Task MandatoryReplacementCannotBeDeclinedOrChosenByOpponent()
    {
        var opened = await Open(Gear(NoReplacement(true))); var p = opened.State.PendingRuleChoice!.Request;
        foreach (var (player, option) in new[] { ("P1", "GEAR:D:G1"), ("P2", "DECLINE"), ("P2", "GEAR:D:MISSING") }) {
            var result = await new CoreRuleEngine().ResolveAsync(opened.State, new("forged", player, CommandTypes.PayCost),
                new PayCostCommand(p.Id, "RULE_REPLACEMENT", [option]), default);
            Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(opened.State), MatchStateHasher.Hash(result.State));
        }
    }

    [Fact]
    public async Task AltarAtAnotherBattlefieldCannotReplaceDeathsDuringThisBattle()
    {
        var s = NoReplacement(true);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["OTHER"] = s.CardObjects["BF"] with { ObjectId = "OTHER", CardNo = "UNL-206/219" },
            ["D"] = s.CardObjects["D"] with { Power = 9, Damage = 0 },
            ["AOE"] = s.CardObjects["AOE"] with { OwnerId = "P2", ControllerId = "P2" }
        }, PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with { Hand = [] },
            ["P2"] = s.PlayerZones["P2"] with { Battlefields = ["BF", "D", "OTHER", "D2"], Hand = ["AOE"] }
        }, ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
            ["OTHER"] = new("P2", "BATTLEFIELD"), ["D2"] = new("P2", "BATTLEFIELD", "OTHER"), ["AOE"] = new("P2", "HAND")
        } };
        var moved = await Act(s, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var first = await Act(moved.State, "P1", new PassFocusCommand());
        var second = await Act(first.State, "P2", new PassFocusCommand());
        var battle = await Act(second.State, "P1", new DeclareBattleCommand("BF", ["A"], ["D"], ["COMBAT_ASSIGNMENT"]));
        var spell = await Act(battle.State, "P2", new PlayCardCommand("AOE", "OGN·133/298", []));
        var done = await Top(spell.State);
        Assert.Null(done.State.PendingRuleChoice);
        Assert.Contains("D2", done.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(done.Events, e => e.Kind == "COST_PAID" && e.Payload.GetValueOrDefault("reason") as string == "BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL");
        Restore(done.State);
    }

}
