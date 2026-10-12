using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.CardCatalog;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PowerfulConsumerMightTests
{
    internal static MatchState Hand(MatchState state, string card, string id = "SPELL")
    {
        state = OfficialCopyIdentityTests.AddSpell(state, id, card);
        if (card is "SFD·103/221" or "OGN·038/298")
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { Tags = [CardObjectTags.UnitCard] } } };
        return state;
    }

    internal static MatchState Equipped(int power = 4) => ArmamentMightTests.Position(power: power, attached: true);

    [Fact]
    public async Task MightyDrawCountsAnEquippedUnitUsingItsCurrentMight()
    {
        var state = Hand(Equipped(), "SFD·106/221");
        Assert.Equal(6, ArmamentMightTests.Might(state)); Assert.Equal(4, state.CardObjects["HOST"].Power);
        var done = await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "SFD·106/221", []))).State);
        Assert.Single(done.State.PlayerZones["P1"].Hand);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task AUnitPlayDrawCountsItsSelfAndAnEquippedPowerfulUnit()
    {
        var state = Hand(Equipped(), "OGN·038/298");
        var done = await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·038/298", []))).State);
        Assert.Equal(2, done.State.PlayerZones["P1"].Hand.Count);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task SelfDiscountCountsCurrentPowerfulUnitsForPreviewAndActualPayment()
    {
        var state = Hand(Equipped(), "SFD·103/221");
        var command = new PlayCardCommand("SPELL", "SFD·103/221", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(5, quote.Cost!.Mana);
        var done = await Act(state, "P1", command);
        Assert.Equal(15, done.State.RunePools["P1"].Mana);
        Assert.Contains("SPELL", done.State.PlayerZones["P1"].Base);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(2, 7)] [InlineData(3, 5)] [InlineData(4, 5)]
    public async Task SelfDiscountUsesTheFiveMightBoundary(int printed, int cost)
    {
        var state = Hand(Equipped(printed), "SFD·103/221");
        var command = new PlayCardCommand("SPELL", "SFD·103/221", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(cost, quote.Cost!.Mana);
        var done = await Act(state, "P1", command);
        Assert.Equal(20 - cost, done.State.RunePools["P1"].Mana);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(1, false, 5)] [InlineData(2, false, 3)] [InlineData(4, false, 0)]
    [InlineData(1, true, 6)] [InlineData(2, true, 4)] [InlineData(4, true, 0)]
    public async Task MultiplePowerfulUnitsReduceTheSharedTotalIncludingSelectedHaste(int count, bool haste, int expected)
    {
        var state = Equipped();
        for (var i = 1; i < count; i++) {
            var id = "EXTRA" + i;
            state = OfficialHoldSequenceTests.AddUnit(state, id, "OGN·001/298", "F", "P1");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { Power = 5 } } };
            state = LocalDestructionRecallTests.At(state, id, "P1", "BASE");
        }
        state = Hand(state, "SFD·103/221");
        var command = new PlayCardCommand("SPELL", "SFD·103/221", [], OptionalCosts: haste ? ["HASTE_READY"] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.ErrorCode + ": " + quote.Message); Assert.Equal(expected, quote.Cost!.Mana);
        var done = await Act(state, "P1", command);
        Assert.Equal(20 - expected, done.State.RunePools["P1"].Mana);
        Assert.Equal(!haste, done.State.CardObjects["SPELL"].IsExhausted);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    internal static MatchState Suppressed()
    {
        var state = Equipped();
        state = OfficialHoldSequenceTests.AddUnit(state, "LEONA", "OGN·079/298", "F", "P2");
        state = LocalDestructionRecallTests.At(state, "F", "P2", "BATTLEFIELD", "F");
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        state = LocalDestructionRecallTests.At(state, "GEAR", "P1", "BATTLEFIELD", "F");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Power = 8, UntilEndOfTurnEffects = ["STUNNED"] },
            ["LEONA"] = state.CardObjects["LEONA"] with { Power = 6 } } };
    }

    [Fact]
    public async Task LiveEnemyAuraDisablesAStoredPowerfulUnitsDraw()
    {
        var state = Suppressed();
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "GEAR", "P1", "BASE");
        state = Hand(state, "SFD·106/221");
        var moved = await Act(state, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Equal(2, ArmamentMightTests.Might(moved.State));
        var drawn = await Top((await Act(moved.State, "P1", new PlayCardCommand("SPELL", "SFD·106/221", []))).State);
        Assert.Empty(drawn.State.PlayerZones["P1"].Hand);
        Restore(drawn.State); ArmamentMightTests.PublicRecovery(drawn.State);
    }

    [Fact]
    public async Task AResponseDetachingTheArmamentChangesTheDrawCountAtResolution()
    {
        var state = Hand(Equipped(), "SFD·106/221");
        state = AttackDamageDistributionTests.Spell(state, "DETACH", "SFD·011/221", "P2");
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["P2-DECK"] = new("P2-DECK", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { MainDeck = ["P2-DECK"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["P2-DECK"] = new("P2", "MAIN_DECK") } };
        var played = await Act(state, "P1", new PlayCardCommand("SPELL", "SFD·106/221", []));
        var priority = await Act(played.State, "P1", new PassPriorityCommand());
        var responded = await Act(priority.State, "P2", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
        var recalled = await Top(responded.State); Assert.Equal(4, ArmamentMightTests.Might(recalled.State));
        Assert.Single(recalled.State.StackItems);
        var drawn = await Top(recalled.State); Assert.Empty(drawn.State.PlayerZones["P1"].Hand);
        Restore(drawn.State); ArmamentMightTests.PublicRecovery(drawn.State);
    }

    [Fact]
    public async Task CurrentOfficialReactionCanBePlayedDuringPriority()
    {
        var state = Hand(Equipped(), "OGN·053/298");
        state = AttackDamageDistributionTests.Spell(state, "DRAW", "SFD·106/221", "P2");
        var opened = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·053/298", ["HOST"]));
        var priority = await Act(opened.State, "P1", new PassPriorityCommand());
        var response = await Act(priority.State, "P2", new PlayCardCommand("DRAW", "SFD·106/221", []));
        Assert.Equal(2, response.State.StackItems.Count); Assert.Equal(18, response.State.RunePools["P2"].Mana);
        Restore(response.State); ArmamentMightTests.PublicRecovery(response.State);
    }

    [Theory]
    [InlineData("hidden")] [InlineData("standby")] [InlineData("non-unit")]
    [InlineData("enemy-controlled")] [InlineData("out-of-field")]
    public void IneligibleObjectsCannotReduceTheSourceCost(string branch)
    {
        var state = Equipped(8); var host = state.CardObjects["HOST"];
        if (branch == "hidden") host = host with { IsFaceDown = true };
        if (branch == "standby") host = host with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby] };
        if (branch == "non-unit") host = host with { Tags = [CardObjectTags.EquipmentCard] };
        if (branch == "enemy-controlled") host = host with { ControllerId = "P2" };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["HOST"] = host } };
        if (branch == "out-of-field") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Base = ["GEAR"], Hand = ["HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["HOST"] = new("P1", "HAND") } };
        state = Hand(state, "SFD·103/221");
        var command = new PlayCardCommand("SPELL", "SFD·103/221", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(7, quote.Cost!.Mana);
    }

    [Fact]
    public async Task APreviewDoesNotAuthorizeAnEarlierDiscountAfterTheEquipmentDeparts()
    {
        var before = Hand(Equipped(), "SFD·103/221");
        var command = new PlayCardCommand("SPELL", "SFD·103/221", []);
        Assert.Equal(5, new CoreRuleEngine().PreviewPlayCard(before, "P1", PlayCostPreviewTests.Request(before, command)).Cost!.Mana);
        var state = before with { CardObjects = new Dictionary<string, CardObjectState>(before.CardObjects) {
            ["GEAR"] = before.CardObjects["GEAR"] with { AttachedToObjectId = null } },
            RunePools = new Dictionary<string, RunePool>(before.RunePools) { ["P1"] = new(5, 20) } };
        var hash = MatchStateHasher.Hash(state);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("CHANGED", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.InsufficientCost, rejected.ErrorCode);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task CurrentPrintedSelfDiscountProducesGenericAmountAndThresholdMetadata()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == "SFD·103/221");
        var spec = Assert.Single(StaticAbilityParser.Parse(card.CardEffect, []),
            s => s.Kind == StaticAbilityKinds.SourceCostReductionPerControlledPowerfulUnit);
        Assert.Equal(2, spec.Amount); Assert.Equal(5, spec.RequiredPowerThreshold);
        Assert.Contains("你每控制一名{{强力}}单位，我的费用便减少{{2}}", spec.Text);
    }
}
