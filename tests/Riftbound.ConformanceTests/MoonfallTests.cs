using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.GroupedTargetTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MoonfallTests
{
    internal const string Spell = "UNL-198/219";

    internal static async Task<MatchState> Board()
    {
        var s = OtherField(Group(3, 4), "D2");
        s = At(s, "D3", "P2", "BASE");
        s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["AOE"] = s.CardObjects["AOE"] with { CardNo = Spell } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) {
                ["P1"] = new(3, 0, new Dictionary<string, int> { ["blue"] = 1 }) } };
        // Establish the actual battle and its response window instead of seeding
        // a contested board with an unresolved battle-initialization task.
        return (await Act(s, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []))).State;
    }

    internal static PlayCardCommand Command(params string[] targets) => new("AOE", Spell, targets);

    internal static PlayCostQuoteDto Quote(MatchState s, params string[] targets)
    {
        var prompt = ResolutionResult.BuildPrompts(s)["P1"];
        return new CoreRuleEngine().PreviewPlayCard(s, "P1", new("quote", prompt.PromptId!, s.Tick, Command(targets)));
    }

    [Fact]
    public async Task MustChooseABattlefieldEvenWhenDecliningTheOptionalMove()
    {
        var s = await Board();
        Assert.False(Quote(s).IsValid);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("no-field", "P1", CommandTypes.PlayCard), Command(), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(rejected.State));
        Assert.Empty(rejected.Events);
    }

    [Fact]
    public async Task NoMoveBranchOnlyWeakensEnemiesAtTheSelectedBattlefield()
    {
        var s = await Board();
        var quote = Quote(s, "BF"); Assert.True(quote.CanPay, quote.Message);
        var done = await Top((await Act(s, "P1", Command("BF"))).State);
        Assert.Equal(2, done.State.CardObjects["D"].Power);
        Assert.Equal(4, done.State.CardObjects["D2"].Power);
        Assert.Equal(4, done.State.CardObjects["D3"].Power);
        Assert.Equal(s.CardObjects["A"].Power, done.State.CardObjects["A"].Power);
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Restore(done.State);
    }

    [Theory]
    [InlineData("D2")]
    [InlineData("D3")]
    public async Task MovesOneEnemyFromAnotherBattlefieldOrBaseBeforeTheLocalPenalty(string enemy)
    {
        var s = await Board();
        var quote = Quote(s, "BF", enemy); Assert.True(quote.CanPay, quote.Message);
        var done = await Top((await Act(s, "P1", Command("BF", enemy))).State);
        Assert.Equal("BF", done.State.ObjectLocations[enemy].BattlefieldObjectId);
        Assert.Equal(2, done.State.CardObjects[enemy].Power);
        Assert.Equal(2, done.State.CardObjects["D"].Power);
        var move = Assert.Single(done.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal(enemy == "D2" ? "BF2" : null, move.Payload["originBattlefieldObjectId"]);
        var events = done.Events.ToList();
        Assert.True(events.FindIndex(e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD")
            < events.FindIndex(e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"));
        Restore(done.State);
    }

    [Theory]
    [InlineData("other-field")]
    [InlineData("same-location")]
    [InlineData("friendly-unit")]
    [InlineData("reverse")]
    [InlineData("too-many")]
    [InlineData("missing-field")]
    [InlineData("unit-as-field")]
    public async Task InvalidSelectionsAreRejectedBeforePaymentAndMutation(string branch)
    {
        var s = await Board();
        string[] targets = branch switch {
            "other-field" => ["BF2"], "same-location" => ["BF", "D"],
            "friendly-unit" => ["BF", "A"], "reverse" => ["D2", "BF"],
            "too-many" => ["BF", "D2", "D3"], "missing-field" => ["MISSING"], _ => ["D2"] };
        Assert.False(Quote(s, targets).IsValid);
        var bad = await new CoreRuleEngine().ResolveAsync(s, new(branch, "P1", CommandTypes.PlayCard), Command(targets), default);
        Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(bad.State));
        Assert.Empty(bad.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BattlefieldRequiresAVisibleFriendlyUnitRatherThanOwnershipOrEquipment(bool hidden)
    {
        var s = await Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["A"] = s.CardObjects["A"] with { IsFaceDown = hidden,
                Tags = hidden ? [CardObjectTags.UnitCard, CardObjectTags.Standby] : [CardObjectTags.EquipmentCard] },
            ["BF"] = s.CardObjects["BF"] with { OwnerId = "P1", ControllerId = "P1" } } };
        Assert.False(Quote(s, "BF").IsValid);
        var bad = await new CoreRuleEngine().ResolveAsync(s, new("not-a-friendly-unit", "P1", CommandTypes.PlayCard), Command("BF"), default);
        Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(bad.State));
    }

    [Fact]
    public async Task AffectedWardUnitsDoNotBecomeChosenTargetsOrAdditionalCosts()
    {
        var s = await Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        Assert.True(Quote(s, "BF").CanPay);
        var done = await Top((await Act(s, "P1", Command("BF"))).State);
        Assert.Equal(2, done.State.CardObjects["D"].Power);
        Assert.Equal(0, done.State.RunePools["P1"].TotalPower); Restore(done.State);
    }

    [Fact]
    public async Task TheOptionalChosenEnemyStillChargesWard()
    {
        var s = await Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D2"] = s.CardObjects["D2"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        var quote = Quote(s, "BF", "D2"); Assert.True(quote.IsValid); Assert.False(quote.CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("unpaid-ward", "P1", CommandTypes.PlayCard), Command("BF", "D2"), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(rejected.State));
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = new(3, 2, new Dictionary<string, int> { ["blue"] = 1 }) } };
        Assert.True(Quote(s, "BF", "D2").CanPay);
        var done = await Top((await Act(s, "P1", Command("BF", "D2"))).State);
        Assert.Equal("BF", done.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Equal(0, done.State.RunePools["P1"].TotalPower); Restore(done.State);
    }

    [Fact]
    public async Task MovingAStolenUnitPreservesItsOwnerControllerDamageAndEquipment()
    {
        var s = await Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D2"] = s.CardObjects["D2"] with { OwnerId = "P1", Power = 5, Damage = 1,
                UntilEndOfTurnPowerModifier = 1, IsExhausted = true },
            ["GEAR"] = new("GEAR", power: 7, ownerId: "P2", controllerId: "P2", attachedToObjectId: "D2",
                tags: [CardObjectTags.EquipmentCard], isExhausted: true) } };
        s = At(s, "GEAR", "P2", "BATTLEFIELD", "BF2");
        Assert.False(Quote(s, "BF", "GEAR").IsValid);
        var done = await Top((await Act(s, "P1", Command("BF", "D2"))).State);
        Assert.Equal("BF", done.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal("D2", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.True(done.State.CardObjects["GEAR"].IsExhausted); Assert.Equal(7, done.State.CardObjects["GEAR"].Power);
        var unit = done.State.CardObjects["D2"];
        Assert.Equal("P1", unit.OwnerId); Assert.Equal("P2", unit.ControllerId);
        Assert.Equal(1, unit.Damage); Assert.True(unit.IsExhausted); Assert.Equal(0, unit.ObjectGeneration);
        Assert.Equal(3, unit.Power); Assert.Equal(-1, unit.UntilEndOfTurnPowerModifier); Restore(done.State);
    }

    [Fact]
    public async Task MovementProhibitionSkipsOnlyMovementAndTheLocalPenaltyStillResolves()
    {
        var s = await Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D2"] = s.CardObjects["D2"] with { UntilEndOfTurnEffects = ["MOVEMENT_PROHIBITED:P1"] } } };
        var done = await Top((await Act(s, "P1", Command("BF", "D2"))).State);
        Assert.Equal("BF2", done.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Equal(4, done.State.CardObjects["D2"].Power); Assert.Equal(2, done.State.CardObjects["D"].Power);
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD"); Restore(done.State);
    }

    [Theory]
    [InlineData("enemy")]
    [InlineData("battlefield")]
    public async Task ReturnedObjectGenerationDoesNotReplaceTheOriginalChosenTarget(string changed)
    {
        var s = (await Act(await Board(), "P1", Command("BF", "D2"))).State;
        var id = changed == "enemy" ? "D2" : "BF";
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            [id] = s.CardObjects[id] with { ObjectGeneration = 1 } } };
        var done = await Top(s);
        Assert.Equal("BF2", done.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Equal(4, done.State.CardObjects["D2"].Power);
        Assert.Equal(changed == "enemy" ? 2 : 4, done.State.CardObjects["D"].Power); Restore(done.State);
    }

    [Fact]
    public async Task LosingTheFriendlyUnitInvalidatesTheChosenBattlefield()
    {
        var s = (await Act(await Board(), "P1", Command("BF", "D2"))).State;
        var done = await Top(At(s, "A", "P1", "BASE"));
        Assert.Equal("BF2", done.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Equal(4, done.State.CardObjects["D"].Power); Assert.Equal(4, done.State.CardObjects["D2"].Power);
        Assert.DoesNotContain(done.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"); Restore(done.State);
    }

    [Fact]
    public async Task AResponseDamagingTheSelectedEnemyKillsItOnlyAfterMovingAndWeakening()
    {
        var s = await Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D2"] = s.CardObjects["D2"] with { Damage = 1 } } };
        s = EquipChainTests.Response(s, "OGN·133/298", "P2");
        var cast = await Act(s, "P1", Command("BF", "D2"));
        var passed = await Act(cast.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·133/298", []));
        var damaged = await Top(response.State); Assert.Equal(2, damaged.State.CardObjects["D2"].Damage);
        var done = await Top(damaged.State);
        Assert.Contains("D2", done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(2, done.State.CardObjects["D"].Power);
        var events = done.Events.ToList();
        Assert.True(events.FindIndex(e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD")
            < events.FindIndex(e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"));
        Assert.True(events.FindIndex(e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN")
            < events.FindIndex(e => e.Kind == "UNIT_DESTROYED")); Restore(done.State);
    }

    [Fact]
    public async Task NegativeRawMightWithoutDamageDoesNotDestroyTheUnit()
    {
        var s = await Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { Power = 1, Damage = 0 } } };
        var done = await Top((await Act(s, "P1", Command("BF"))).State);
        Assert.Equal(-1, done.State.CardObjects["D"].Power);
        Assert.Equal(0, CurrentPowerTargetTests.DisplayPower(done.State));
        Assert.Contains("D", done.State.PlayerZones["P2"].Battlefields); Restore(done.State);
    }

    [Fact]
    public async Task ARealCounterCancelsMovementAndPenaltyButDoesNotRefundPayment()
    {
        var s = EquipChainTests.Response(await Board(), "OGN·064/298", "P2");
        var cast = await Act(s, "P1", Command("BF", "D2"));
        var passed = await Act(cast.State, "P1", new PassPriorityCommand());
        var item = Assert.Single(passed.State.StackItems);
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·064/298", [item.StackItemId]));
        var done = await Top(response.State);
        Assert.Empty(done.State.StackItems); Assert.Equal("BF2", done.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Equal(4, done.State.CardObjects["D"].Power); Assert.Equal(0, done.State.RunePools["P1"].Mana);
        Assert.Equal(0, done.State.RunePools["P1"].TotalPower); Assert.Contains("AOE", done.State.PlayerZones["P1"].Graveyard);
        Assert.Single(done.Events, e => e.Kind == "STACK_ITEM_COUNTERED"); Restore(done.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EchoPreservesEachOptionalMoveChoiceAndRechecksTheSharedBattlefield(bool moveInitially)
    {
        var s = await Board();
        s = s with { UntilEndOfTurnEffects = [..s.UntilEndOfTurnEffects, EchoCostRules.GrantPrefix + "P1"],
            RunePools = new Dictionary<string, RunePool>(s.RunePools) {
                ["P1"] = new(20, 20, new Dictionary<string, int> { ["blue"] = 1 }) } };
        var command = Command(moveInitially ? ["BF", "D2"] : ["BF"]) with {
            OptionalCosts = ["ECHO"], RepeatChoices = [new("", ["BF", "D2"])] };
        var played = await Act(s, "P1", command);
        var executions = Assert.Single(played.State.StackItems).RepeatExecutions!;
        Assert.Equal(2, executions.Count); Assert.Equal(command.TargetObjectIds, executions[0].TargetObjectIds);
        Assert.Equal(new[] { "BF", "D2" }, executions[1].TargetObjectIds);
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
        var done = await Top(played.State);
        Assert.Equal(0, done.State.CardObjects["D"].Power);
        Assert.Equal(moveInitially ? 0 : 2, done.State.CardObjects["D2"].Power);
        Assert.Equal("BF", done.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Single(done.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal(2, done.Events.Count(e => e.Kind == "SPELL_EXECUTION_COMPLETED")); Restore(done.State);
    }

    [Fact]
    public async Task AnInvalidLaterBattlefieldChoiceCannotChargeEchoOrChangeState()
    {
        var s = await Board();
        s = s with { UntilEndOfTurnEffects = [..s.UntilEndOfTurnEffects, EchoCostRules.GrantPrefix + "P1"],
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20, 20) } };
        var command = Command("BF") with { OptionalCosts = ["ECHO"], RepeatChoices = [new("", ["BF2", "D3"])] };
        var bad = await new CoreRuleEngine().ResolveAsync(s, new("bad-echo", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(bad.State));
        Assert.Empty(bad.Events);
    }
}
