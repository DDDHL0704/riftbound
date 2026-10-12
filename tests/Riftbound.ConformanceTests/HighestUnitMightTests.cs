using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class HighestUnitMightTests
{
    internal static MatchState Position(int stored = 2, bool attached = true, int mana = 3, int red = 1)
    {
        var state = PowerfulConsumerMightTests.Hand(ArmamentMightTests.Position(
            gear: "SFD·056/221", unit: "SFD·167/221", power: stored, attached: attached), "OGN·014/298");
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(mana, 0, red == 0 ? null : new Dictionary<string, int> { [RuneTrait.Red] = red }) } };
    }

    [Fact]
    public void PreviewReducesEightManaByTheHighestCurrentMightOfFive()
    {
        var state = Position(); var hash = MatchStateHasher.Hash(state);
        Assert.Equal(5, ArmamentMightTests.Might(state)); Assert.Equal(2, state.CardObjects["HOST"].Power);
        var command = new PlayCardCommand("SPELL", "OGN·014/298", ["HOST"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.NotNull(quote.Cost); Assert.Equal(3, quote.Cost.Mana); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(hash, MatchStateHasher.Hash(state));
    }

    [Fact]
    public async Task RealPaymentAcceptsExactlyThreeManaAndOneRedPowerWithTheEquippedUnit()
    {
        var state = Position();
        var played = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·014/298", ["HOST"]));
        Assert.Equal(0, played.State.RunePools["P1"].Mana); Assert.Empty(played.State.RunePools["P1"].PowerByTrait);
        Assert.Equal(3, Assert.Single(played.Events, e => e.Kind == "COST_PAID").Payload["mana"]);
        Assert.Single(played.State.StackItems); Assert.Equal(5, ArmamentMightTests.Might(played.State));
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
    }

    [Theory]
    [InlineData(2, false, 6)] [InlineData(2, true, 3)]
    [InlineData(3, true, 2)] [InlineData(5, true, 0)] [InlineData(8, true, 0)]
    public async Task CurrentMightReductionAndManaFloorAgreeInPreviewAndCommit(int stored, bool attached, int cost)
    {
        var state = Position(stored, attached, cost);
        var command = new PlayCardCommand("SPELL", "OGN·014/298", ["HOST"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.Equal(cost, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]); Assert.True(quote.CanPay);
        var played = await Act(state, "P1", command);
        Assert.Equal(0, played.State.RunePools["P1"].Mana); Assert.Empty(played.State.RunePools["P1"].PowerByTrait);
        Assert.Equal(cost, Assert.Single(played.Events, e => e.Kind == "COST_PAID").Payload["mana"]);
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
    }

    [Theory]
    [InlineData("enemy-unit")] [InlineData("hand-unit")] [InlineData("equipment")]
    [InlineData("rune")] [InlineData("face-down")]
    public void OnlyControlledVisibleFieldUnitsContributeToTheMaximum(string kind)
    {
        var state = Position();
        var owner = kind == "enemy-unit" ? "P2" : "P1";
        var tag = kind == "equipment" ? CardObjectTags.EquipmentCard : kind == "rune" ? CardObjectTags.RuneCard : CardObjectTags.UnitCard;
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["EXCLUDED"] = new("EXCLUDED", power: 99, ownerId: owner, controllerId: owner, isFaceDown: kind == "face-down", tags: [tag]) } };
        if (kind == "hand-unit") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = [..state.PlayerZones["P1"].Hand, "EXCLUDED"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["EXCLUDED"] = new("P1", "HAND") } };
        else state = LocalDestructionRecallTests.At(state, "EXCLUDED", owner, "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state,
            new PlayCardCommand("SPELL", "OGN·014/298", ["HOST"])));
        Assert.Equal(3, quote.Cost!.Mana); Assert.True(quote.CanPay);
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD")]
    public async Task AControlledUnitCountsInEitherFieldZoneRegardlessOfItsOwner(string zone)
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = new("OTHER", cardNo: "SFD·167/221", power: 6, ownerId: "P2", controllerId: "P1", tags: [CardObjectTags.UnitCard]) } };
        state = LocalDestructionRecallTests.At(state, "OTHER", "P1", zone, zone == "BASE" ? null : "F");
        var command = new PlayCardCommand("SPELL", "OGN·014/298", ["HOST"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.Equal(2, quote.Cost!.Mana); Assert.True(quote.CanPay);
        var played = await Act(state, "P1", command); Assert.Equal(1, played.State.RunePools["P1"].Mana);
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
    }

    [Fact]
    public async Task ARealDetachInvalidatesTheEarlierAffordableQuoteWithoutAuthorizingPayment()
    {
        var state = OfficialCopyIdentityTests.AddSpell(Position(), "DETACH", "SFD·011/221");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
        var command = new PlayCardCommand("SPELL", "OGN·014/298", ["HOST"]); var engine = new CoreRuleEngine();
        var earlier = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(earlier.CanPay); Assert.Equal(3, earlier.Cost!.Mana);
        var detached = await Top((await Act(state, "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]))).State);
        Assert.Equal(2, ArmamentMightTests.Might(detached.State)); Assert.Equal(3, detached.State.RunePools["P1"].Mana);
        var current = engine.PreviewPlayCard(detached.State, "P1", PlayCostPreviewTests.Request(detached.State, command));
        Assert.Equal(6, current.Cost!.Mana); Assert.False(current.CanPay);
        var rejected = await engine.ResolveAsync(detached.State, new("stale-quote", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.InsufficientCost, rejected.ErrorCode); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(detached.State), MatchStateHasher.Hash(rejected.State));
        Restore(detached.State); ArmamentMightTests.PublicRecovery(detached.State);
    }

    [Fact]
    public async Task AZeroManaCostStillRequiresThePrintedRedPower()
    {
        var state = Position(stored: 5, mana: 0, red: 0); var command = new PlayCardCommand("SPELL", "OGN·014/298", ["HOST"]);
        var engine = new CoreRuleEngine(); var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.Equal(0, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]); Assert.False(quote.CanPay);
        var rejected = await engine.ResolveAsync(state, new("missing-red", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.InsufficientCost, rejected.ErrorCode); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task WithNoControlledUnitTheSpellStillCostsEightMana()
    {
        var state = LocalDestructionRecallTests.At(Position(attached: false, mana: 8), "HOST", "P2", "BATTLEFIELD", "F");
        state = LocalDestructionRecallTests.At(state, "F", "P2", "BATTLEFIELD");
        var command = new PlayCardCommand("SPELL", "OGN·014/298", ["HOST"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.IsValid, quote.Message); Assert.NotNull(quote.Cost);
        Assert.Equal(8, quote.Cost.Mana); Assert.True(quote.CanPay);
        var played = await Act(state, "P1", command); Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
    }

    [Theory]
    [InlineData(8, 5)] [InlineData(14, 0)]
    public async Task TheHighestMightReductionCanAlsoReduceGrantedEchoAdditionalMana(int stored, int cost)
    {
        var state = Position(stored: stored, mana: cost, red: 2) with {
            UntilEndOfTurnEffects = ["BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:P1:1"] };
        var command = new PlayCardCommand("SPELL", "OGN·014/298", ["HOST"], OptionalCosts: ["ECHO"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.IsValid, quote.Message); Assert.Equal(cost, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.PowerByTrait[RuneTrait.Red]); Assert.True(quote.CanPay);
        var played = await Act(state, "P1", command); Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(2, Assert.Single(played.State.StackItems).EffectRepeatCount);
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
    }
}
