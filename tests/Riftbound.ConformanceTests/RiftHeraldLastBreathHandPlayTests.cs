using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RiftHeraldLastBreathHandPlayTests
{
    private static MatchState Position(string no = RiftHeraldMovementLookTests.Herald, string unit = RiftHeraldMovementLookTests.Alternate,
        int remainingMana = 1, int remainingYellow = 1, string sourceOwner = "P1")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "HERALD", no, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "PAY-UNIT", unit, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "HAND-GEAR", "SFD·022/221", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "HAND-SPELL", "OGN·275/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "KILL", "OGN·229/298", "P1", "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HERALD"] = state.CardObjects["HERALD"] with { OwnerId = sourceOwner } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(4 + remainingMana, 0, new Dictionary<string, int> {
                [RuneTrait.Yellow] = 2 + remainingYellow, [RuneTrait.Red] = 1, [RuneTrait.Green] = 1 }) } };
    }

    private static async Task Destroy(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["HERALD"])); await game.Top();
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("LAST_BREATH", skill.UnitEvent!.Timing); Assert.NotNull(skill.UnitEvent.ArmamentDeath);
        Assert.Equal("HERALD", skill.SourceObjectId); Assert.Equal("P1", skill.ControllerId);
        Assert.Equal(0, skill.UnitEvent.SourceGeneration);
        Assert.Contains("HERALD", game.State.PlayerZones[game.State.CardObjects["HERALD"].OwnerId!].Graveyard);
    }

    [Theory]
    [InlineData(RiftHeraldMovementLookTests.Herald, "P1")] [InlineData(RiftHeraldMovementLookTests.Alternate, "P1")]
    [InlineData(RiftHeraldMovementLookTests.Herald, "P2")] [InlineData(RiftHeraldMovementLookTests.Alternate, "P2")]
    public async Task RealDeathOffersOnlyHandUnitsAndWaivesBaseManaWhileChargingPrintedPower(string no, string owner)
    {
        var game = new Driver(Position(no, remainingMana: 0, sourceOwner: owner)); await Destroy(game); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(["PAY-UNIT"], pending.Sources.Keys); Assert.Equal("HAND", pending.SourceZone);
        Assert.True(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower); Assert.False(pending.IgnoreAllCosts);
        Assert.False(pending.Optional); Assert.Equal("BASE", pending.DestinationPolicy);
        Assert.DoesNotContain("PAY-UNIT", JsonSerializer.Serialize(game.PromptFor("P2")));
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("HAND-GEAR", "SFD·022/221", []));
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("HAND-SPELL", "OGN·275/298", []));
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("PAY-UNIT", RiftHeraldMovementLookTests.Alternate, [], Destination: "BATTLEFIELD:BF"));
        var before = game.State.RunePools["P1"];
        var command = new PlayCardCommand("PAY-UNIT", RiftHeraldMovementLookTests.Alternate, [], Destination: "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", new("herald-quote", "prompt", game.State.Tick, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Yellow]);
        await game.Act("P1", command); Assert.Contains("PAY-UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["PAY-UNIT"].IsExhausted);
        Assert.Equal(before.Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        Assert.Null(game.State.PendingEffectPlay); Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task OptionalHasteStillCostsOneManaAndOneRedAndEntersReadyOnlyWhenPaid(bool haste)
    {
        var game = new Driver(Position(unit: "OGN·001/298")); await Destroy(game); await game.Top();
        var before = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("PAY-UNIT", "OGN·001/298", [],
            OptionalCosts: haste ? [HasteOptionalCostNames.HasteReady] : []));
        Assert.Equal(!haste, game.State.CardObjects["PAY-UNIT"].IsExhausted);
        Assert.Equal(before.Mana - (haste ? 1 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - (haste ? 1 : 0), game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Fact]
    public async Task LackingPrintedPowerCannotBeBypassedByTheManaWaiver()
    {
        var game = new Driver(Position(remainingYellow: 0)); await Destroy(game); await game.Top();
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("PAY-UNIT", RiftHeraldMovementLookTests.Alternate, []));
        var pending = game.State.PendingEffectPlay!;
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("PAY-UNIT", game.State.PlayerZones["P1"].Hand); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task NoUnitInHandCompletesTheMandatoryInstructionWithoutADeadChoice()
    {
        var state = RadGearTestPosition.Put(Position(), "PAY-UNIT", RiftHeraldMovementLookTests.Alternate, "P1", "GRAVEYARD");
        var game = new Driver(state); await Destroy(game); await game.Top();
        Assert.Null(game.State.PendingEffectPlay); Assert.Null(game.State.PendingCardChoice);
        Assert.Contains("HAND-GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("HAND-SPELL", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task TheCapturedLastBreathSurvivesAnOpponentRecyclingTheDeadSourceIntoItsPrivateDeck()
    {
        var state = RadGearTestPosition.Put(Position(), "RECYCLE", "UNL-103/219", "P2", "HAND");
        var game = new Driver(state); await Destroy(game);
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RECYCLE", "UNL-103/219", ["HERALD"], Mode: "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3")); await game.Top();
        Assert.Contains("HERALD", game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain("HERALD", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); await game.Act("P1", new PlayCardCommand("PAY-UNIT", RiftHeraldMovementLookTests.Alternate, []));
        Assert.Contains("PAY-UNIT", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Theory]
    [InlineData("power")] [InlineData("all")] [InlineData("zone")] [InlineData("spell")]
    [InlineData("generation")] [InlineData("destination")] [InlineData("timing")]
    public async Task RecoveryRejectsForgedHandPermissionsAndCapturedSkillSelection(string mutation)
    {
        var game = new Driver(Position()); await Destroy(game); await game.Top(); var pending = game.State.PendingEffectPlay!;
        var forged = pending with { IgnoreBasePower = mutation == "power", IgnoreAllCosts = mutation == "all",
            SourceZone = mutation == "zone" ? "GRAVEYARD" : pending.SourceZone,
            DestinationPolicy = mutation == "destination" ? "BASE_OR_CONTROLLED_BATTLEFIELD" : pending.DestinationPolicy,
            Sources = mutation is "spell" or "generation" ? new Dictionary<string, long> {
                [mutation == "spell" ? "HAND-SPELL" : "PAY-UNIT"] = mutation == "generation" ? 99 : 0 } : pending.Sources,
            Parent = mutation == "timing" ? pending.Parent with { UnitEvent = pending.Parent.UnitEvent! with { Timing = "MOVE" } } : pending.Parent };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { PendingEffectPlay = forged }));
        await game.Act("P1", new PlayCardCommand("PAY-UNIT", RiftHeraldMovementLookTests.Alternate, [])); await game.Replay();
    }
}
