using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SumpworksMapScoringActionTests
{
    private const string Map = "UNL-085/219";
    private static MatchState WithMap(MatchState state, string controller = "P2", string owner = "P2")
    {
        state = RadGearTestPosition.Put(state, "MAP", Map, controller, "BASE");
        for (var i = 0; i < 6; i++) state = RadGearTestPosition.Put(state, "P2-DECK-" + i, "OGN·219/298", "P2", "MAIN_DECK");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Map, out var printed));
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["MAP"] = state.CardObjects["MAP"] with { OwnerId = owner,
                Tags = state.CardObjects["MAP"].Tags.Concat((printed.SourceEquipmentTags ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct().ToArray() } } };
    }
    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
    }
    private static async Task Draw(Driver game, string scorer = "P1")
    {
        var skill = Assert.Single(game.State.StackItems); Assert.Equal(Map, skill.CardNo); Assert.Equal("P2", skill.ControllerId);
        Assert.Equal(scorer, skill.UnitEvent!.ScoringAction!.PlayerId); Assert.Empty(skill.TargetObjectIds);
        var before = game.State.PlayerZones["P2"].Hand.Count; await game.Top();
        Assert.Equal(before + 1, game.State.PlayerZones["P2"].Hand.Count); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnActualOpponentConquestEarnsOneIndependentDrawForTheCurrentController(bool stolen)
    {
        var game = new Driver(WithMap(RadGearTestPosition.Create(), owner: stolen ? "P1" : "P2")); await Conquer(game);
        Assert.Equal(1, game.State.PlayerScores["P1"]); Assert.Empty(game.State.PlayerZones["P2"].Hand); await Draw(game);
    }

    [Fact]
    public async Task AnActualSourceReturnPreservesTheEarnedDrawAndHidesThePrivateIncarnation()
    {
        var state = RadGearTestPosition.Put(WithMap(RadGearTestPosition.Create(), controller: "P1", owner: "P1"),
            "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "MOVE", "OGN·043/298", "P1", "HAND");
        state = AnimatedArmamentAttachmentTests.WithMainPhaseResources(state);
        state = RadGearTestPosition.Put(state, "MAP-GREEN", "UNL-R02", "P1", "BASE");
        state = state with { Phase = MatchPhases.TurnStart, TurnStartStep = "MAIN", TimingState = TimingStates.NeutralClosed,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        var game = new Driver(state); await game.Act("P1", new PassPriorityCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["MAP"]));
        await game.TapRunes("P1", ["ORI-MANA-0", "ORI-MANA-1"]);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"])); await game.Top();
        Assert.NotNull(game.State.CardObjects["MAP"].LegendEquipmentUnit);
        await game.TapRunes("P1", ["ORI-MANA-2", "ORI-MANA-3", "ORI-MANA-4", "ORI-MANA-5", "MAP-GREEN"]);
        await game.Act("P1", new RecycleRuneCommand("MAP-GREEN"));
        await game.Act("P1", new PlayCardCommand("MOVE", "OGN·043/298", ["ENEMY"], Destination: "BATTLEFIELD:BF")); await game.Top();
        for (var step = 0; game.State.StackItems.Count == 0 && game.State.SpellDuelState.IsActive; step++) {
            Assert.InRange(step, 0, 5);
            await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        }
        var skill = Assert.Single(game.State.StackItems); Assert.Equal(Map, skill.CardNo); Assert.Empty(skill.TargetObjectIds);
        Assert.Equal("P2", skill.UnitEvent!.ScoringAction!.PlayerId); Assert.Equal("P1", skill.ControllerId);
        if (game.State.PriorityPlayerId is { } priority && priority != "P1") await game.Act(priority, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["MAP"])); await game.Top();
        Assert.Contains("MAP", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"MAP\"", System.Text.Json.JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        var hand = game.State.PlayerZones["P1"].Hand.Count; await game.Top();
        Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count); await game.Replay();
    }

    [Fact]
    public async Task NaturalOpponentHoldAlsoEarnsTheDrawAfterTheScoringAction()
    {
        var game = new Driver(WithMap(OfficialHoldSequenceTests.State("OGN·294/298")));
        await game.Act("P1", new PassPriorityCommand()); Assert.Equal("CHANNEL", game.State.TurnStartStep);
        Assert.Equal("BATTLEFIELD_HELD_SCORE", Assert.Single(game.State.StackItems).UnitEvent!.ScoringAction!.Reason);
        await Draw(game);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReplacingTheLastConquestPointOrPreventingItsGainStillLeavesOneScoringAction(bool prevent)
    {
        var state = WithMap(RadGearTestPosition.Create());
        if (prevent) {
            state = RadGearTestPosition.Put(state, "REMOTE-F", "OGN·294/298", "P2", "BATTLEFIELD", "REMOTE-F");
            state = RadGearTestPosition.Put(state, "TIANNA", "SFD·060/221", "P2", "BATTLEFIELD", "REMOTE-F");
        } else state = state with { PlayerScores = new Dictionary<string, int> { ["P1"] = 7, ["P2"] = 0 } };
        var game = new Driver(state); await Conquer(game); Assert.Equal(prevent ? 0 : 7, game.State.PlayerScores.GetValueOrDefault("P1"));
        Assert.Contains(game.Events(), e => e.Event.Kind == (prevent ? "SCORE_GAIN_PREVENTED" : "BATTLEFIELD_SCORE_REPLACED_BY_DRAW"));
        await Draw(game);
    }

    [Fact]
    public async Task AnOwnScoringActionDoesNotTriggerTheEquipment()
    {
        var game = new Driver(WithMap(RadGearTestPosition.Create(), controller: "P1", owner: "P2")); await Conquer(game);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Fact]
    public async Task APointGainedByAnOpponentFromActualBurnoutIsNotAScoringAction()
    {
        var state = WithMap(RadGearTestPosition.Create(), controller: "P1");
        foreach (var id in state.PlayerZones["P1"].MainDeck.ToArray()) state = RadUnitEffectTestFixture.Place(state, id, "P1", "BANISHED");
        state = RadGearTestPosition.Put(state, "RECYCLE", "OGN·219/298", "P1", "GRAVEYARD");
        state = RadGearTestPosition.Put(state, "TRAIN", "OGN·058/298", "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("TRAIN", "OGN·058/298", ["ALLY"])); await game.Top();
        Assert.Equal(1, game.State.PlayerScores["P2"]); Assert.Contains("RECYCLE", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.State.StackItems, skill => skill.CardNo == Map); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("scorer")] [InlineData("battlefield")]
    public async Task RecoveryBindsTheSkillToTheOriginalAcceptedScoringAction(string change)
    {
        var game = new Driver(WithMap(RadGearTestPosition.Create())); await Conquer(game); var skill = Assert.Single(game.State.StackItems);
        var fact = skill.UnitEvent!.ScoringAction!;
        var forged = game.State with { StackItems = change == "erase" ? [] : [skill with { UnitEvent = skill.UnitEvent with {
            ScoringAction = change == "scorer" ? fact with { PlayerId = "P2" } : fact with { Battlefield = new("BF", 0) } } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await Draw(game);
    }
}
