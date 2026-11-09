using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 359/383/477-479: this is an untargeted group instruction, evaluated at
// resolution using current Might, with one eligible group before anyone leaves.
public sealed class LanternSeaFiendGroupReturnTests
{
    private const string Fiend = "UNL-132/219";
    private static MatchState Position()
        => RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "FIEND", Fiend, "P1", "HAND");
    private static async Task Play(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("FIEND", Fiend, []));
        var item = Assert.Single(game.State.StackItems); Assert.Empty(item.TargetObjectIds);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
    }

    [Theory]
    [InlineData("P1", "BASE")] [InlineData("P2", "BASE")]
    [InlineData("P1", "BATTLEFIELD")] [InlineData("P2", "BATTLEFIELD")]
    public async Task ActualPlayReturnsBothSidesLowMightUnitsFromBaseAndBattlefields(string player, string zone)
    {
        var state = Position();
        state = RadGearTestPosition.Put(state, "TWO", "OGN·010/298", player, zone, zone == "BASE" ? null : player == "P1" ? "BF" : "OTHER");
        state = RadGearTestPosition.Put(state, "THREE", "OGN·175/298", player, zone, zone == "BASE" ? null : player == "P1" ? "BF" : "OTHER");
        var game = new Driver(state); await Play(game); Assert.DoesNotContain("TWO", game.State.PlayerZones[player].Hand);
        await game.Top();
        Assert.Contains("TWO", game.State.PlayerZones[player].Hand); Assert.DoesNotContain("THREE", game.State.PlayerZones[player].Hand);
        Assert.Contains("FIEND", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task ARealResponseBuffChangesEligibilityBeforeTheGroupIsReturned()
    {
        var state = RadGearTestPosition.Put(Position(), "TWO", "OGN·010/298", "P2", "BASE");
        state = RadGearTestPosition.Put(state, "BUFF", "OGN·058/298", "P2", "HAND");
        var game = new Driver(state); await Play(game); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("BUFF", "OGN·058/298", ["TWO"])); await game.Top();
        Assert.Equal(4, game.State.CardObjects["TWO"].Power); await game.Top();
        Assert.Contains("TWO", game.State.PlayerZones["P2"].Base); await game.Replay();
    }

    [Fact]
    public async Task LeavingAnAuraDoesNotAddNewUnitsToTheAlreadyDeterminedGroup()
    {
        var state = RadGearTestPosition.Put(Position(), "AURA", "UNL-147/219", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "TWO", "OGN·010/298", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["AURA"] = state.CardObjects["AURA"] with { Power = 2, UntilEndOfTurnPowerModifier = -10 } } };
        Assert.Equal(2, CoreRuleEngine.ResolveCurrentFieldUnitPower(state, state.CardObjects["AURA"]));
        Assert.Equal(4, CoreRuleEngine.ResolveCurrentFieldUnitPower(state, state.CardObjects["TWO"]));
        var game = new Driver(state); await Play(game); await game.Top();
        Assert.Contains("AURA", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("TWO", game.State.PlayerZones["P1"].Base);
        Assert.Equal(2, CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["TWO"])); await game.Replay();
    }

    [Fact]
    public async Task ARealSourceReturnDoesNotCancelTheIndependentGroupOrExposeThePrivateSource()
    {
        var state = RadGearTestPosition.Put(Position(), "TWO", "OGN·010/298", "P2", "BASE");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(state); await Play(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["FIEND"])); await game.Top();
        Assert.Contains("FIEND", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"FIEND\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Contains("TWO", game.State.PlayerZones["P2"].Hand); await game.Replay();
    }

    [Fact]
    public async Task AWeakenedSourceReturnsItselfAndAStolenLowMightUnitReturnsToItsOwner()
    {
        var state = RadGearTestPosition.Put(Position(), "STOLEN", "OGN·010/298", "P2", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "STOLEN", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["STOLEN"] = state.CardObjects["STOLEN"] with { ControllerId = "P1" } } };
        state = RadGearTestPosition.Put(state, "SHRINK", "OGN·093/298", "P1", "HAND");
        var game = new Driver(state); await Play(game);
        await game.Act("P1", new PlayCardCommand("SHRINK", "OGN·093/298", ["FIEND"])); await game.Top();
        Assert.True(CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["FIEND"]) <= 2);
        await game.Top(); Assert.Contains("FIEND", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("STOLEN", game.State.PlayerZones["P2"].Hand); await game.Replay();
    }

    [Fact]
    public async Task EquipmentDetachesWithoutBecomingAGroupMember()
    {
        var state = RadGearTestPosition.Put(Position(), "TWO", "OGN·010/298", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "GEAR", "SFD·059/221", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("GEAR", "SFD·059/221", [], Destination: "BASE"));
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "TWO", ["ASSEMBLE_GREEN"])); await game.Top();
        await Play(game); await game.Top();
        Assert.Contains("TWO", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base); Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Replay();
    }

    [Fact]
    public async Task AnActuallyPlayedSpellshieldWarhawkReturnsWithoutTargetTaxAndCeasesToExist()
    {
        var state = RadGearTestPosition.Put(Position(), "PERCH", "UNL-130/219", "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("PERCH", "UNL-130/219", []));
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2"])); await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.WarhawkTokenCardNo);
        var power = game.State.RunePools["P1"].TotalPower;
        await Play(game); Assert.Equal(power - 1, game.State.RunePools["P1"].TotalPower); await game.Top();
        Assert.DoesNotContain(token.ObjectId, game.State.CardObjects.Keys);
        Assert.DoesNotContain(token.ObjectId, game.State.PlayerZones["P2"].Hand); await game.Replay();
    }

    [Theory]
    [InlineData("capture")] [InlineData("target")]
    public async Task RecoveryRejectsARewrittenCaptureOrInventedTargets(string mode)
    {
        var game = new Driver(Position()); await Play(game); var item = Assert.Single(game.State.StackItems);
        var forged = mode == "capture" ? item with { UnitEvent = item.UnitEvent! with { CapturedTick = item.UnitEvent!.CapturedTick - 1 } }
            : item with { TargetObjectIds = ["ENEMY"], TargetGenerations = new Dictionary<string, long> { ["ENEMY"] = 0 } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [forged] }));
    }

    [Fact]
    public async Task TheUntargetedGroupDoesNotActivateGangplanksTargetedEmpoweredReplacement()
    {
        var state = RadGearTestPosition.Put(Position(), "GANGPLANK", "VEN·086", "P2", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GANGPLANK"] = state.CardObjects["GANGPLANK"] with { Power = 2, IsEmpowered = true, UntilEndOfTurnPowerModifier = -4 } } };
        Assert.Equal(2, CoreRuleEngine.ResolveCurrentFieldUnitPower(state, state.CardObjects["GANGPLANK"]));
        var game = new Driver(state); await Play(game); await game.Top();
        Assert.Contains("GANGPLANK", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Payload.ContainsKey("replacedEvent")); await game.Replay();
    }
}
