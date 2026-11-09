using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class GlascBartenderLastBreathTests
{
    private const string Bartender = "SFD·165/221", Chosen = "OGN·061/298", Kill = "OGN·229/298";
    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "BARTENDER", Bartender, "P1", "BASE");
        foreach (var (id, no, player) in new[] { ("CHOSEN", Chosen, "P1"), ("CHEAP", "OGN·010/298", "P1"),
            ("TOO-MUCH-MANA", "OGN·001/298", "P1"), ("TOO-MUCH-POWER", "OGN·150/298", "P1"),
            ("GEAR", "OGN·023/298", "P1"), ("THEIR-UNIT", Chosen, "P2") })
            state = RadGearTestPosition.Put(state, id, no, player, "GRAVEYARD");
        return RadGearTestPosition.Put(state, "KILL", Kill, "P1", "HAND");
    }

    private static async Task Destroy(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["BARTENDER"])); await game.Top();
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }
    private static Task Confirm(Driver game, string? selected)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        return game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected is null ? [] : [selected]));
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task LastBreathConfirmsOnlyTheControllersUnitsWithinBothPrintedCostCapsAndWaivesBothBaseCosts(string destination)
    {
        var initial = Position();
        if (destination == "BATTLEFIELD:BF") initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await Destroy(game);
        Assert.Equal(new[] { "CHEAP", "CHOSEN" }, game.State.PendingCardChoice!.LegalObjectIds.Order(StringComparer.Ordinal));
        await Confirm(game, "CHOSEN"); var pool = game.State.RunePools["P1"];
        Assert.Contains("CHOSEN", game.State.PlayerZones["P1"].Graveyard);
        await game.Top(); var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(["CHOSEN"], pending.Sources.Keys); Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower);
        Assert.False(pending.IgnoreAllCosts); Assert.False(pending.Optional);
        var play = new PlayCardCommand("CHOSEN", Chosen, [], Destination: destination);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", play);
        Assert.Equal(pool.Mana, game.State.RunePools["P1"].Mana); Assert.Equal(pool.TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("CHOSEN", destination == "BASE" ? game.State.PlayerZones["P1"].Base : game.State.PlayerZones["P1"].Battlefields);
        Assert.Null(game.State.PendingEffectPlay); await game.Replay();
    }

    [Fact]
    public async Task TheControllerCanDeclineBeforeConfirmationButCannotDeclineAConfirmedCompletelyFreePlay()
    {
        var game = new Driver(Position()); await Destroy(game); await Confirm(game, null);
        Assert.Empty(game.State.StackItems); Assert.Contains("CHOSEN", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
        var accepted = new Driver(Position()); await Destroy(accepted); await Confirm(accepted, "CHOSEN"); await accepted.Top();
        var pending = accepted.State.PendingEffectPlay!;
        await RadGearTestPosition.Reject(accepted.State, "P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        await accepted.Act("P1", new PlayCardCommand("CHOSEN", Chosen, [], Destination: "BASE")); await accepted.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IgnoringBaseCostsRetainsTheOptionalHasteManaAndPowerPayment(bool haste)
    {
        var game = new Driver(Position()); await Destroy(game); await Confirm(game, "CHEAP"); await game.Top();
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("CHEAP", "OGN·010/298", [], Destination: "BASE",
            OptionalCosts: haste ? [HasteOptionalCostNames.HasteReady] : []));
        Assert.Equal(pool.Mana - (haste ? 1 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower - (haste ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        Assert.Equal(!haste, game.State.CardObjects["CHEAP"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task TheOfficialFaqAllowsChoosingAUnitDestroyedInTheSameBatch()
    {
        var initial = RadGearTestPosition.Put(Position(), "RUIN", "UNL-180/219", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "CHEAP", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("RUIN", "UNL-180/219", [])); await game.Top();
        Assert.Contains("BARTENDER", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("CHEAP", game.State.PendingCardChoice!.LegalObjectIds);
        await Confirm(game, "CHEAP"); await game.Top();
        await game.Act("P1", new PlayCardCommand("CHEAP", "OGN·010/298", [], Destination: "BASE"));
        Assert.Contains("CHEAP", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task CopiedTextAndALocalAmplifierCreateFourIndependentOptionalLastBreaths()
    {
        var initial = RadGearTestPosition.Put(Position(), "SONG", "SFD·059/221", "P1", "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "BARTENDER", "P1", "BATTLEFIELD", "BF");
        initial = LastBreathRepetitionTests.AddAmplifier(initial, "AMP", "P1", "BF");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "BARTENDER", ["ASSEMBLE_GREEN"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["BARTENDER"])); await game.Top();
        var contexts = game.State.TriggerQueue.Select(t => t.UnitEvent).Concat(game.State.StackItems.Select(t => t.UnitEvent))
            .OfType<UnitEventContext>().Where(c => c.CardNo == Bartender).ToArray();
        Assert.Equal(4, contexts.Length); Assert.Equal(2, contexts.Count(c => c.Origin is not null));
        Assert.All(contexts, context => Assert.NotNull(context.ArmamentDeath!.Repetition));
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; i < 4; i++) await Confirm(game, null);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("all-costs")] [InlineData("missing-waiver")] [InlineData("wrong-source")] [InlineData("erased-death")]
    public async Task RecoveryBindsTheDeathTargetAndExactBaseCostWaiverPolicy(string branch)
    {
        var game = new Driver(Position()); await Destroy(game); await Confirm(game, "CHOSEN"); await game.Top();
        var pending = game.State.PendingEffectPlay!;
        var forged = game.State with { PendingEffectPlay = branch switch {
            "all-costs" => pending with { IgnoreAllCosts = true },
            "missing-waiver" => pending with { IgnoreBasePower = false },
            "wrong-source" => pending with { Sources = new Dictionary<string, long> { ["TOO-MUCH-POWER"] = 0 } },
            _ => pending with { Parent = pending.Parent with { UnitEvent = pending.Parent.UnitEvent! with { ArmamentDeath = null } } }
        } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
    }
}
