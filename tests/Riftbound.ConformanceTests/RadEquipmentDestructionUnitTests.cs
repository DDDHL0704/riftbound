using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadEquipmentDestructionUnitTests
{
    private const string Armor = "RAD·130/167";
    private static MatchState Position(bool battlefield = true, bool friendly = true, bool foreignOwner = false)
    {
        var state = VenUnitEntryBasicTests.Position(Armor);
        var controller = friendly ? "P1" : "P2"; var owner = foreignOwner ? "P2" : controller;
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var places = state.ObjectLocations.ToDictionary();
        cards["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·022/221", owner) with { ControllerId = controller };
        zones["P1"] = zones["P1"] with { Base = ["ALLY"], Battlefields = ["BF"] };
        zones[controller] = battlefield ? zones[controller] with { Battlefields = [..zones[controller].Battlefields, "GEAR"] }
            : zones[controller] with { Base = [..zones[controller].Base, "GEAR"] };
        places["GEAR"] = new(controller, battlefield ? "BATTLEFIELD" : "BASE", battlefield ? "BF" : null);
        if (battlefield)
        {
            cards["BF"] = cards["BF"] with { ControllerId = controller };
            cards["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "OGN·219/298", controller);
            cards["GEAR"] = cards["GEAR"] with { AttachedToObjectId = "HOST" };
            zones[controller] = zones[controller] with { Battlefields = [..zones[controller].Battlefields, "HOST"] };
            places["HOST"] = new(controller, "BATTLEFIELD", "BF");
        }
        state = state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = places };
        return AttackDamageDistributionTests.Spell(state, "DESTROY", "OGN·022/298", "P1");
    }

    [Theory]
    [InlineData(true, true, false, true)] [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)] [InlineData(true, true, true, true)]
    public async Task OnlyFriendlyEquipmentDestroyedOnABattlefieldCanReturnToItsOwner(bool battlefield, bool friendly, bool foreignOwner, bool triggers)
    {
        var game = new Driver(Position(battlefield, friendly, foreignOwner));
        await game.Act("P1", new PlayCardCommand("UNIT", Armor, [], Destination: "BASE"));
        var generation = game.State.CardObjects["GEAR"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("DESTROY", "OGN·022/298", [])); await game.Top();
        var owner = foreignOwner || !friendly ? "P2" : "P1";
        Assert.Contains("GEAR", game.State.PlayerZones[owner].Graveyard);
        Assert.Equal(triggers, game.State.PendingPayment is not null);
        if (triggers)
        {
            var payment = game.State.PendingPayment!; var mana = game.State.RunePools["P1"].Mana;
            Assert.Equal(1, payment.ManaCost); Assert.Equal(0, payment.PowerCost);
            await Reject(game.State, "P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            await Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY", "PAY"]));
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
            Assert.Contains("GEAR", game.State.PlayerZones[owner].Graveyard); Assert.NotNull(game.State.PriorityPlayerId);
            await game.Top(); Assert.Contains("GEAR", game.State.PlayerZones[owner].Hand);
            Assert.Equal(generation + 2, game.State.CardObjects["GEAR"].ObjectGeneration);
            Assert.Equal(owner, game.State.CardObjects["GEAR"].ControllerId);
        }
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task TheOptionalPaymentCanBeDeclinedAndUnaffordablePaymentCannotMutateTheGame()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Armor, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("DESTROY", "OGN·022/298", [])); await game.Top();
        var payment = game.State.PendingPayment!;
        var poor = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(poor, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Graveyard); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AChangedEquipmentIncarnationIsNotReturnedAndTheCapturedReceiptIsRequired(bool sourceLeaves)
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "KILL", "SFD·163/221", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Armor, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("DESTROY", "OGN·022/298", [])); await game.Top();
        var payment = game.State.PendingPayment!;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        var item = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with {
            FieldContext = item.FieldContext! with { EquipmentDestruction = null } }] }));
        if (sourceLeaves)
        {
            await game.Act("P1", new PlayCardCommand("KILL", "SFD·163/221", ["UNIT", "ALLY"])); await game.Top(); await game.Top();
            Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand); await game.Replay();
        }
        else
        {
            var changed = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                ["GEAR"] = game.State.CardObjects["GEAR"] with { ObjectGeneration = game.State.CardObjects["GEAR"].ObjectGeneration + 2 } } };
            var done = await OfficialGraveyardRecastTests.Top(OfficialInsightAndSpellLockTests.Restore(changed));
            Assert.Contains("GEAR", done.State.PlayerZones["P1"].Graveyard); OfficialInsightAndSpellLockTests.Restore(done.State);
        }
    }

    [Fact]
    public async Task ACopyDestroyedInTheSameActionDoesNotObserveOtherEquipment()
    {
        var state = LastBreathDrawBoundaryTests.Add(Position(), "SONG", "SFD·059/221", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Armor, [], Destination: "BASE"));
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("DESTROY", "OGN·022/298", [])); await game.Top();
        // The copied text leaves with SONG in this same destruction; only the printed text remains active.
        Assert.Single(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        var payment = game.State.PendingPayment!;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"])); await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("SONG", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task TwoSurvivingTextInstancesPaySeparatelyButReturnTheSameEquipmentOnlyOnce()
    {
        var state = LastBreathDrawBoundaryTests.Add(Position(), "SONG", "SFD·059/221", "P1", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "SINGLE", "SFD·077/221", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Armor, [], Destination: "BASE"));
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("SINGLE", "SFD·077/221", ["GEAR"], Mode: "DESTROY_EQUIPMENT")); await game.Top();
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var mana = game.State.RunePools["P1"].Mana;
        while (game.State.PendingPayment is { } payment)
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana);
        await game.Top(); await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.Events(), e => e.Event.Kind == "EQUIPMENT_RETURNED_TO_HAND"); await game.Replay();
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-equipment-return", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
