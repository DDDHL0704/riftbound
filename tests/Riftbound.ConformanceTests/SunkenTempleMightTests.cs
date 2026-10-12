using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class SunkenTempleMightTests
{
    internal static MatchState Position(int power = 2, bool equipped = true)
    {
        var state = ConquestLifecycleRegressionTests.State("SFD·218/221", handCount: 0);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { CardNo = "SFD·167/221", Power = power },
            ["GEAR"] = new("GEAR", cardNo: "SFD·056/221", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], attachedToObjectId: equipped ? "UNIT" : null) };
        return LocalDestructionRecallTests.At(state with { CardObjects = cards,
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(5, 5) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["BF"] = new("P2", "BATTLEFIELD", "BF"), ["UNIT"] = new("P1", "BASE") } }, "GEAR", "P1", "BASE");
    }

    internal static Task<ResolutionResult> Pay(MatchState state, bool pay = true)
        => Act(state, "P1", new PayCostCommand(state.PendingPayment!.PaymentId, state.PendingPayment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));

    [Fact]
    public async Task AUnitEquippedToCurrentFiveQualifiesAtActualConquest()
    {
        var state = Position();
        var conquered = await ConquestLifecycleRegressionTests.Conquer(state);
        Assert.Contains(conquered.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Equal(2, conquered.State.CardObjects["UNIT"].Power);
        var unit = CurrentPowerTargetTests.Snapshot(conquered.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty("UNIT");
        Assert.Equal(5, unit.GetProperty("effectivePower").GetInt32());
        Assert.NotNull(conquered.State.PendingPayment);
        Assert.Equal(5, Assert.Single(conquered.State.StackItems).BattlefieldConquestMight!.PowerfulUnits[0].Might);
        Restore(conquered.State); ArmamentMightTests.PublicRecovery(conquered.State);
    }

    [Fact]
    public async Task PayingTheConquestCostDoesNotDrawBeforeIndependentResponses()
    {
        var conquered = await ConquestLifecycleRegressionTests.Conquer(Position(power: 5, equipped: false));
        var payment = Assert.IsType<PendingPaymentState>(conquered.State.PendingPayment);
        var paid = await Act(conquered.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
            [payment.LegalPaymentChoiceIds.First(c => c != "DECLINE")]));
        Assert.Equal(4, paid.State.RunePools["P1"].Mana);
        Assert.Empty(paid.State.PlayerZones["P1"].Hand);
        Assert.Single(paid.State.StackItems);
        Restore(paid.State); ArmamentMightTests.PublicRecovery(paid.State);
        var done = await Top(paid.State);
        Assert.Equal(new[] { "DRAW1" }, done.State.PlayerZones["P1"].Hand);
        Assert.Empty(done.State.StackItems); Assert.Equal(4, done.State.RunePools["P1"].Mana);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, true)] [InlineData(3, true)]
    public async Task ConquestQualificationUsesTheCurrentFiveBoundary(int stored, bool qualifies)
    {
        var result = await ConquestLifecycleRegressionTests.Conquer(Position(power: stored));
        Assert.Equal(qualifies, result.State.PendingPayment is not null);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task DecliningLeavesManaAndDeckUnchangedAndDoesNotLeaveAnAbility()
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position());
        var declined = await Pay(opened.State, false);
        Assert.Equal(5, declined.State.RunePools["P1"].Mana); Assert.Empty(declined.State.PlayerZones["P1"].Hand);
        Assert.Equal(new[] { "DRAW1", "DRAW2", "DRAW3" }, declined.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(declined.State.StackItems); Assert.Null(declined.State.PendingPayment);
        Restore(declined.State); ArmamentMightTests.PublicRecovery(declined.State);
    }

    [Fact]
    public async Task ARealResponseDetachDoesNotEraseAnAlreadyConfirmedConquest()
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "DETACH", "SFD·011/221", "P2");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(5, 5) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RESPONSE-DRAW"] = new("RESPONSE-DRAW", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { MainDeck = ["RESPONSE-DRAW"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RESPONSE-DRAW"] = new("P2", "MAIN_DECK") } };
        var paid = await Pay((await ConquestLifecycleRegressionTests.Conquer(state)).State);
        var priority = await Act(paid.State, "P1", new PassPriorityCommand());
        var response = await Act(priority.State, "P2", new PlayCardCommand("DETACH", "SFD·011/221", ["UNIT", "GEAR"]));
        var detached = await Top(response.State);
        Assert.Null(detached.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Empty(detached.State.PlayerZones["P1"].Hand);
        Assert.Equal(5, Assert.Single(detached.State.StackItems).BattlefieldConquestMight!.PowerfulUnits[0].Might);
        Restore(detached.State); ArmamentMightTests.PublicRecovery(detached.State);
        var done = await Top(detached.State); Assert.Single(done.State.PlayerZones["P1"].Hand);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("might")] [InlineData("source-generation")] [InlineData("future-tick")]
    [InlineData("private-pile")] [InlineData("wrong-field")] [InlineData("missing-context")]
    public async Task RecoveryRejectsForgedConquestQualification(string change)
    {
        var state = (await Pay((await ConquestLifecycleRegressionTests.Conquer(Position())).State)).State;
        var item = Assert.Single(state.StackItems); var context = item.BattlefieldConquestMight!;
        context = change switch {
            "might" => context with { PowerfulUnits = [context.PowerfulUnits[0] with { Might = 99 }] },
            "source-generation" => context with { Source = context.Source with { Generation = 99 } },
            "future-tick" => context with { ConquestTick = state.Tick + 1 },
            "private-pile" => context with { Frame = context.Frame with { PlayerZones = new Dictionary<string, PlayerZones>(context.Frame.PlayerZones) {
                ["P2"] = context.Frame.PlayerZones["P2"] with { Hand = ["PRIVATE"] } } } },
            "wrong-field" => context with { Frame = context.Frame with { ObjectLocations = new Dictionary<string, ObjectLocationState>(context.Frame.ObjectLocations) {
                ["UNIT"] = new("P1", "BATTLEFIELD", "OTHER") } } },
            _ => context
        };
        var forged = state with { StackItems = [item with { BattlefieldConquestMight = change == "missing-context" ? null : context }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("captured conquest Might", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")] [InlineData("spectator")]
    public async Task PublicRecoveryRejectsAChangedHistoricalQualification(string viewer)
    {
        var state = (await Pay((await ConquestLifecycleRegressionTests.Conquer(Position())).State)).State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0,
            snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        var target = viewer == "spectator" ? spectator.SpectatorSnapshot : snapshots[viewer];
        var payload = Assert.IsType<Dictionary<string, object?>>(Assert.Single(target.Stack));
        var context = Assert.IsType<BattlefieldConquestMightContext>(payload["battlefieldConquestMight"]);
        payload["battlefieldConquestMight"] = context with { PowerfulUnits = [context.PowerfulUnits[0] with { Might = 99 }] };
        Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator),
            e => e.Contains("captured conquest Might", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AssaultCountsAtCombatConquestButDoesNotQualifyUnopposedMovement(bool combat)
    {
        var state = Position(equipped: false);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { Tags = [CardObjectTags.UnitCard, "强攻3"] } } };
        ResolutionResult result;
        if (!combat) result = await ConquestLifecycleRegressionTests.Conquer(state);
        else {
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" },
                ["DEFENDER"] = new("DEFENDER", cardNo: "SFD·125/221", power: 1, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Battlefields = ["BF", "DEFENDER"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["DEFENDER"] = new("P2", "BATTLEFIELD", "BF") } };
            var moved = await Act(state, "P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            var first = await Act(moved.State, "P1", new PassFocusCommand());
            var second = await Act(first.State, "P2", new PassFocusCommand());
            result = await CombatTestDriver.FinishAsync(await Act(second.State, "P1",
                new DeclareBattleCommand("BF", ["UNIT"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"])));
        }
        Assert.Equal(combat, result.State.PendingPayment is not null);
        if (combat) {
            var context = Assert.Single(result.State.StackItems).BattlefieldConquestMight!;
            Assert.Equal(5, Assert.Single(context.PowerfulUnits).Might); Assert.True(context.Frame.CardObjects["UNIT"].IsAttacking);
            var paid = await Pay(result.State); var done = await Top(paid.State);
            Assert.Single(done.State.PlayerZones["P1"].Hand); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        }
    }

    [Theory]
    [InlineData("legacy-choice")] [InlineData("duplicate")] [InlineData("empty")] [InlineData("other-player")]
    public async Task InvalidCostConfirmationDoesNotChangeTheMatch(string change)
    {
        var state = (await ConquestLifecycleRegressionTests.Conquer(Position())).State;
        var payment = state.PendingPayment!;
        var choices = change switch { "legacy-choice" => new[] { "SPEND_MANA:1" },
            "duplicate" => new[] { "PAY", "PAY" }, "empty" => [], _ => new[] { "PAY" } };
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid", change == "other-player" ? "P2" : "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, choices), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task HistoricalQualificationContainsNoPrivateCardOrPileOrder()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECRET"] = new("SECRET", cardNo: "SFD·011/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Hand = ["SECRET"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SECRET"] = new("P2", "HAND") } };
        var context = Assert.Single((await ConquestLifecycleRegressionTests.Conquer(state)).State.StackItems).BattlefieldConquestMight!;
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(context));
        Assert.All(context.Frame.PlayerZones.Values, z => { Assert.Empty(z.Hand); Assert.Empty(z.MainDeck); Assert.Empty(z.RuneDeck); });
    }
}
