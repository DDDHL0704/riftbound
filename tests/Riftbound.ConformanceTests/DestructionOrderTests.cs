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
    internal static async Task<ResolutionResult> AltarBattle(MatchState? initial = null, bool expectChoice = true)
    {
        var s = initial ?? AltarPlayerPool(new RunePool(0, 3));
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["BF"] = s.CardObjects["BF"] with { CardNo = "UNL-206/219" } } };
        var moved = await Act(s, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var first = await Act(moved.State, "P1", new PassFocusCommand());
        var second = await Act(first.State, "P2", new PassFocusCommand());
        var battle = await Act(second.State, "P1", new DeclareBattleCommand("BF", ["A"], ["D"], ["COMBAT_ASSIGNMENT"]));
        if (expectChoice) Assert.NotNull(battle.State.PendingRuleChoice);
        return battle;
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AltarLetsTheDyingUnitsControllerPayOrDecline(bool accept)
    {
        var state = AltarPlayerPool(new RunePool(4, 1, new Dictionary<string, int>
        {
            [RuneTrait.Red] = 1,
            [RuneTrait.Blue] = 1
        }));
        var opened = await AltarBattle(state);
        var request = opened.State.PendingRuleChoice!.Request;
        Assert.Equal("P2", request.PlayerId);
        Assert.Contains(request.Options, option => option.Id == "ALTAR:D:BF" && option.Label.Contains("3 点任意符能"));
        var done = await Select(OfficialInsightAndSpellLockTests.Restore(opened.State), accept ? "ALTAR:D:BF" : "DECLINE");
        Assert.Equal(4, done.State.RunePools["P2"].Mana);
        Assert.Equal(accept ? 0 : 3, done.State.RunePools["P2"].TotalPower);
        Assert.Equal(accept ? 3 : 0, done.State.RunePools["P2"].PaidPowerThisTurn);
        Assert.Equal(accept, done.State.PlayerZones["P2"].Base.Contains("D"));
        Assert.Equal(!accept, done.State.PlayerZones["P2"].Graveyard.Contains("D"));
        Assert.Empty(done.State.BattleResolutions[0].SurvivingDefenderObjectIds);
        Restore(done.State);
    }
    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    public async Task AltarDoesNotOfferPaymentWithOnlyManaOrInsufficientUnrestrictedPower(int power, int restricted)
    {
        var state = AltarPlayerPool(new RunePool(3, power, unitOrEquipmentPlayOnlyPower: restricted));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = state.CardObjects["D"] with { Tags = [CardObjectTags.UnitCard] }
        } };
        var done = await AltarBattle(state, expectChoice: false);
        Assert.Null(done.State.PendingRuleChoice);
        Assert.Contains("D", done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(3, done.State.RunePools["P2"].Mana);
        Assert.Equal(power, done.State.RunePools["P2"].TotalPower);
        Assert.Equal(restricted, done.State.RunePools["P2"].UnitOrEquipmentPlayOnlyPower);
    }

    [Fact]
    public async Task AltarCanGeneratePowerBeforePayingAndKeepsItAfterDeclining()
    {
        var state = ReplacementResourceTests.AddResource(AltarPlayerPool(new RunePool(0, 2)),
            P4ActivatedAbilityCatalog.GoldTokenUnlCardNo);
        var opened = await AltarBattle(state);
        var request = Assert.IsType<RuleChoiceContinuation>(opened.State.PendingRuleChoice).Request;
        Assert.DoesNotContain(request.Options, option => option.Id == "ALTAR:D:BF");
        await RadGearTestPosition.Reject(opened.State, request.PlayerId,
            new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["ALTAR:D:BF"]));
        var resource = request.Options.Single(option => option.Id.StartsWith("RESOURCE:", StringComparison.Ordinal));
        var generated = await Select(opened.State, resource.Id);
        Assert.Contains(generated.State.PendingRuleChoice!.Request.Options, option => option.Id == "ALTAR:D:BF");
        var done = await Select(generated.State, "DECLINE");
        Assert.Equal(3, done.State.RunePools["P2"].TotalPower);
        Assert.False(done.State.CardObjects.ContainsKey("RESOURCE"));
        Restore(done.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AltarLetsTheControllerConfirmOrCancelItsPowerAllocation(bool confirm)
    {
        var state = AltarPlayerPool(new RunePool(4, 2, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }));
        var opened = await AltarBattle(state);
        var allocation = await Select(opened.State, "ALTAR:D:BF");
        var allocationRequest = Assert.IsType<RuleChoiceContinuation>(allocation.State.PendingRuleChoice).Request;
        var chooseOneUnrestricted = Assert.Single(allocationRequest.Options, option =>
            option.Id.StartsWith("QUANTITY:", StringComparison.Ordinal) && option.Id.EndsWith(":1:1", StringComparison.Ordinal));
        var selected = await Select(OfficialInsightAndSpellLockTests.Restore(allocation.State), chooseOneUnrestricted.Id);
        Assert.Equal(4, selected.State.RunePools["P2"].TotalPower);
        Assert.Equal(0, selected.State.RunePools["P2"].PaidPowerThisTurn);

        var decision = confirm ? "CONFIRM_DESTRUCTION_REPLACEMENT_POWER" : "CANCEL_DESTRUCTION_REPLACEMENT_POWER";
        var done = await Select(OfficialInsightAndSpellLockTests.Restore(selected.State), decision);
        if (!confirm)
        {
            var replacement = Assert.IsType<RuleChoiceContinuation>(done.State.PendingRuleChoice).Request;
            Assert.Contains(replacement.Options, option => option.Id == "ALTAR:D:BF");
            Assert.Equal(4, done.State.RunePools["P2"].TotalPower);
            done = await Select(done.State, "DECLINE");
        }

        var pool = done.State.RunePools["P2"];
        Assert.Equal(4, pool.Mana);
        Assert.Equal(confirm ? 1 : 2, pool.Power);
        Assert.Equal(confirm ? 0 : 2, pool.PowerByTrait.GetValueOrDefault(RuneTrait.Blue));
        Assert.Equal(confirm ? 3 : 0, pool.PaidPowerThisTurn);
        Assert.Equal(confirm, done.State.PlayerZones["P2"].Base.Contains("D"));
        Assert.Equal(!confirm, done.State.PlayerZones["P2"].Graveyard.Contains("D"));
        Restore(done.State);
    }

    [Fact]
    public async Task AltarLetsTheControllerRecycleARuneBeforePaying()
    {
        var state = RadGearTestPosition.Put(AltarPlayerPool(new RunePool(4, 2)), "ALTAR-RUNE", "UNL-R03", "P2", "BASE");
        var opened = await AltarBattle(state);
        var request = Assert.IsType<RuleChoiceContinuation>(opened.State.PendingRuleChoice).Request;
        Assert.DoesNotContain(request.Options, option => option.Id == "ALTAR:D:BF");
        Assert.Contains(request.Options, option => option.Id == "RECYCLE_RUNE:ALTAR-RUNE");

        var generated = await Select(OfficialInsightAndSpellLockTests.Restore(opened.State), "RECYCLE_RUNE:ALTAR-RUNE");
        var payment = Assert.IsType<RuleChoiceContinuation>(generated.State.PendingRuleChoice).Request;
        Assert.Contains(payment.Options, option => option.Id == "ALTAR:D:BF");
        var done = await Select(OfficialInsightAndSpellLockTests.Restore(generated.State), "ALTAR:D:BF");
        Assert.Contains("ALTAR-RUNE", done.State.PlayerZones["P2"].RuneDeck);
        Assert.DoesNotContain("ALTAR-RUNE", done.State.PlayerZones["P2"].Base);
        Assert.Contains("D", done.State.PlayerZones["P2"].Base);
        Assert.Equal(4, done.State.RunePools["P2"].Mana);
        Assert.Equal(0, done.State.RunePools["P2"].TotalPower);
        Assert.Equal(3, done.State.RunePools["P2"].PaidPowerThisTurn);
        Restore(done.State);
    }

    private static MatchState AltarPlayerPool(RunePool pool)
    {
        var state = NoReplacement();
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = pool } };
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
            ["D2"] = s.CardObjects["D2"] with { Tags = [CardObjectTags.UnitCard] },
            ["AOE"] = s.CardObjects["AOE"] with { OwnerId = "P2", ControllerId = "P2" }
        }, PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with { Hand = [] },
            ["P2"] = s.PlayerZones["P2"] with { Battlefields = ["BF", "D", "OTHER", "D2"], Hand = ["AOE"] }
        }, ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
            ["OTHER"] = new("P2", "BATTLEFIELD"), ["D2"] = new("P2", "BATTLEFIELD", "OTHER"), ["AOE"] = new("P2", "HAND")
        }, RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new RunePool(20, 3) } };
        var moved = await Act(s, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var first = await Act(moved.State, "P1", new PassFocusCommand());
        var spell = await Act(first.State, "P2", new PlayCardCommand("AOE", "OGN·133/298", []));
        var done = await Top(spell.State);
        Assert.Null(done.State.PendingRuleChoice);
        Assert.Contains("D2", done.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(done.Events, e => e.Kind == "COST_PAID" && e.Payload.GetValueOrDefault("reason") as string == "BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL");
        Restore(done.State);
    }

}
