using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class WellspringReflexiveRecastTests
{
    private const string Spell = "UNL-186/219";
    private static MatchState Position(int might = 3)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "WELLSPRING-CARD", Spell, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "A", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "B", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["A"] = state.CardObjects["A"] with { Power = might } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1, [RuneTrait.Red] = 1 }) } };
    }
    private static async Task Cast(Driver game)
    { await game.Act("P1", new PlayCardCommand("WELLSPRING-CARD", Spell, ["A"])); await game.Top(); }
    private static async Task Pay(Driver game, bool pay = true)
    {
        var request = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, request.PowerCost); Assert.Equal(0, request.ManaCost);
        await game.Act(request.PlayerId, new PayCostCommand(request.PaymentId, request.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
    }

    [Fact]
    public async Task AThreeMightDestructionCreatesAnIndependentOptionalReplayForOneGenericPower()
    {
        var game = new Driver(Position()); await Cast(game);
        Assert.Contains("A", game.State.PlayerZones["P2"].Graveyard); Assert.Equal(0, game.State.RunePools["P1"].Mana);
        await Pay(game); Assert.Equal(0, game.State.RunePools["P1"].TotalPower); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(["WELLSPRING-CARD"], pending.Sources.Keys); Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower);
        Assert.False(pending.IgnoreAllCosts); Assert.False(pending.Optional);
        var command = new PlayCardCommand("WELLSPRING-CARD", Spell, ["B"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", new("quote", "prompt", game.State.Tick, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command); Assert.Null(game.State.PendingEffectPlay); await game.Top();
        if (game.State.PendingPayment is not null) await Pay(game, false);
        Assert.Contains("B", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task TheOfficialHourglassRecallCaseStillEarnsTheOptionalReflexiveReplay()
    {
        var state = RadGearTestPosition.Put(Position(), "HOURGLASS", "OGN·077/298", "P2", "BASE");
        var game = new Driver(state); await Cast(game);
        if (game.State.PendingRuleChoice is { } replacement)
        {
            var use = Assert.Single(replacement.Request.Options, option => option.ObjectIds?.Contains("HOURGLASS") == true);
            await game.Act(replacement.Request.PlayerId, new PayCostCommand(replacement.Request.Id, "RULE_REPLACEMENT", [use.Id]));
        }
        Assert.Contains("A", game.State.PlayerZones["P2"].Base); Assert.Contains("HOURGLASS", game.State.PlayerZones["P2"].Graveyard);
        await Pay(game); await game.Top(); Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("WELLSPRING-CARD", Spell, ["B"])); await game.Top();
        if (game.State.PendingPayment is not null) await Pay(game, false); await game.Replay();
    }

    [Fact]
    public async Task DecliningTheConfirmationCostDoesNotUndoTheOriginalDestructionOrSpendPower()
    {
        var game = new Driver(Position()); await Cast(game); var power = game.State.RunePools["P1"].TotalPower;
        await Pay(game, false); Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("A", game.State.PlayerZones["P2"].Graveyard); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task AnActualLastBreathQueuesBesideTheOptionalReflexiveWithoutChangingEitherReceipt()
    {
        var state = RadGearTestPosition.Put(Position(), "A", "UNL-179/219", "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["A"] = state.CardObjects["A"] with { Power = 3 } } };
        var game = new Driver(state); await Cast(game);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.Single(game.State.TriggerQueue, t => t.UnitEvent?.Timing == "LAST_BREATH");
        var reflexive = Assert.Single(game.State.TriggerQueue, t => t.UnitEvent?.Timing == "SPELL_INSTRUCTION_REFLEXIVE");
        Assert.Equal("SPELL_INSTRUCTION_REFLEXIVE", reflexive.TriggeredByEventKind);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await Pay(game, false);
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Contains("A", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("WELLSPRING-CARD", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Theory]
    [InlineData(4)] [InlineData(7)]
    public async Task DestroyingAUnitAboveThreeDoesNotEarnAReflexiveReplay(int might)
    {
        var game = new Driver(Position(might)); await Cast(game); Assert.Null(game.State.PendingPayment);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Fact]
    public async Task ARealResponseChangingTheTargetsMightChangesTheResolutionCondition()
    {
        var state = RadGearTestPosition.Put(Position(2), "BUFF", "SFD·034/221", "P2", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("WELLSPRING-CARD", Spell, ["A"]));
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("BUFF", "SFD·034/221", ["A"])); await game.Top(); await game.Top();
        Assert.Contains("A", game.State.PlayerZones["P2"].Graveyard); Assert.Null(game.State.PendingPayment);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task RecyclingTheSourceAfterConfirmationPreventsReplayingItsPrivateNewIncarnation()
    {
        var state = RadGearTestPosition.Put(Position(), "RECYCLE", "UNL-103/219", "P2", "HAND");
        var game = new Driver(state); await Cast(game); await Pay(game);
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RECYCLE", "UNL-103/219", ["WELLSPRING-CARD"], Mode: "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3"));
        await game.Top(); Assert.DoesNotContain("WELLSPRING-CARD", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        Assert.Contains("WELLSPRING-CARD", game.State.PlayerZones["P1"].MainDeck); await game.Replay();
    }

    [Theory]
    [InlineData("power")] [InlineData("source")] [InlineData("root")] [InlineData("all")]
    public async Task RecoveryRejectsForgedReflexiveConditionsAndReplayPermissions(string mutation)
    {
        var game = new Driver(Position()); await Cast(game); await Pay(game); await game.Top();
        var pending = game.State.PendingEffectPlay!;
        var origin = pending.Parent.UnitEvent!.SpellInstruction!;
        var changed = pending with { IgnoreAllCosts = mutation == "all",
            Sources = mutation == "source" ? new Dictionary<string, long> { ["A"] = game.State.CardObjects["A"].ObjectGeneration } : pending.Sources,
            Parent = pending.Parent with { UnitEvent = pending.Parent.UnitEvent with { SpellInstruction = mutation switch {
                "power" => origin with { ReferencedMight = 2 }, "root" => origin with { ParentStackHash = "FORGED" }, _ => origin } } } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { PendingEffectPlay = changed }));
        await game.Act("P1", new PlayCardCommand("WELLSPRING-CARD", Spell, ["B"])); await game.Top();
        if (game.State.PendingPayment is not null) await Pay(game, false); await game.Replay();
    }
}
