using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SilkDancerMovementTests
{
    private const string Dancer = "SFD·038/221";

    private static MatchState Board(bool copied = false, bool targets = true)
    {
        var state = ArmamentMightTests.Position(unit: Dancer, gear: copied ? "SFD·059/221" : "SFD·022/221", attached: copied);
        if (targets)
        {
            state = OfficialHoldSequenceTests.AddUnit(state, "ALLY", "OGN·001/298", "F", "P1");
            state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BASE");
            state = OfficialHoldSequenceTests.AddUnit(state, "REMOTE", "OGN·001/298", "OTHER", "P1");
            state = OfficialHoldSequenceTests.AddUnit(state, "ENEMY", "OGN·001/298", "OTHER", "P2");
            state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BASE");
            state = RadGearTestPosition.Put(state, "HAND_UNIT", "OGN·001/298", "P1", "HAND");
        }
        return state with { RoomId = "SILK-DANCER-MOVEMENT", CardObjects = state.CardObjects.ToDictionary(pair => pair.Key,
            pair => pair.Key == "HOST" ? OfficialGarenConquestBattlefieldCountTests.Printed("HOST", Dancer, "P1")
                : pair.Key is "ALLY" or "REMOTE" or "ENEMY"
                    ? OfficialGarenConquestBattlefieldCountTests.Printed(pair.Key, "OGN·001/298", pair.Value.ControllerId!) : pair.Value) };
    }

    private static Task Move(Driver game) => game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
    private static Task Choose(Driver game, params string[] ids)
        => game.Act("P1", new ChooseCardsCommand(game.State.PendingCardChoice!.ChoiceId, "TRIGGER_CONFIRMATION", ids));

    private static async Task CloseDuel(Driver game)
    {
        for (var passes = 0; game.State.SpellDuelState.IsActive; passes++)
        {
            Assert.InRange(passes, 0, 7);
            if (game.State.PriorityPlayerId is { } priority)
                await game.Act(priority, new PassPriorityCommand());
            else await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        }
    }

    [Fact]
    public async Task ActualPrintingPlaysForThreeThenReadiesMovesAndConfirmsAnotherFriendlyUnit()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Dancer);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), s => s.Kind == TriggerKinds.UnitMovedOtherFriendlyPowerModifier);
        Assert.Equal(1, spec.PowerDelta); Assert.False(spec.Optional); Assert.Equal("BATTLEFIELD", spec.MovementDestinationZone);
        var state = RadUnitEffectTestFixture.Place(Board(), "HOST", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "READY", "UNL-009/219", "P1", "HAND");
        var game = new Driver(state); var mana = state.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("HOST", Dancer, [], Destination: "BASE"));
        Assert.Equal(mana - 3, game.State.RunePools["P1"].Mana); Assert.Equal(3, game.State.CardObjects["HOST"].Power);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await CloseDuel(game);
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["HOST"])); await game.Top();
        await CloseDuel(game);
        await Move(game); Assert.Equal(1, game.State.PendingCardChoice!.RequiredCount);
        Assert.Equal(new[] { "ALLY", "REMOTE" }, game.State.PendingCardChoice.LegalObjectIds.Order());
        var before = game.State.CardObjects["ALLY"].Power; await Choose(game, "ALLY");
        Assert.Equal(before, game.State.CardObjects["ALLY"].Power); Assert.NotNull(game.State.PriorityPlayerId);
        await game.Top(); Assert.Equal(before + 1, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Theory]
    [InlineData("ALLY")] [InlineData("REMOTE")]
    public async Task AnotherFriendlyUnitMayBeAtBaseOrADifferentBattlefield(string target)
    {
        var game = new Driver(Board()); var before = game.State.CardObjects[target].Power;
        await Move(game); await Choose(game, target); await game.Top();
        Assert.Equal(before + 1, game.State.CardObjects[target].Power);
        Assert.Equal(1, game.State.CardObjects[target].UntilEndOfTurnPowerModifier); await game.Replay();
    }

    [Theory]
    [InlineData("")] [InlineData("HOST")] [InlineData("ENEMY")] [InlineData("GEAR")] [InlineData("HAND_UNIT")]
    public async Task MandatoryOtherFriendlyTargetsRejectDeclineSelfEnemiesEquipmentAndHiddenCards(string target)
    {
        var game = new Driver(Board()); await Move(game);
        var state = game.State; var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-silk-target", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(state.PendingCardChoice!.ChoiceId, "TRIGGER_CONFIRMATION", target.Length == 0 ? [] : [target]), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        await Choose(game, "ALLY"); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task MovingToBaseDoesNotTriggerTheBattlefieldOnlySkill()
    {
        var game = new Driver(LocalDestructionRecallTests.At(Board(), "HOST", "P1", "BATTLEFIELD", "F"));
        await game.Act("P1", new MoveUnitCommand("HOST", "BATTLEFIELD", "BASE", []));
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"); await game.Replay();
    }

    [Fact]
    public async Task NoOtherFriendlyUnitDoesNotBlockTheCompletedMovement()
    {
        var game = new Driver(Board(targets: false)); await Move(game);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Battlefields);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReturningASourceKeepsItsIndependentSkillWhileReturningATargetInvalidatesItsOldGeneration(bool returnSource)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Board(), "RETURN", "OGN·104/298", "P1"));
        var before = game.State.CardObjects["ALLY"].Power;
        await Move(game); await Choose(game, "ALLY");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returnSource ? "HOST" : "ALLY"])); await game.Top();
        if (returnSource)
            Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Equal(before + (returnSource ? 1 : 0), game.State.CardObjects["ALLY"].Power);
        if (returnSource)
            Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Timing));
        Assert.Equal(returnSource, game.Events().Any(e => e.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"));
        if (returnSource) Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.Replay();
    }

    [Fact]
    public async Task CopiedPrintedTextCreatesTwoSeparatelyConfirmedMovementSkills()
    {
        var game = new Driver(Board(copied: true)); var before = game.State.CardObjects["ALLY"].Power; await Move(game);
        var triggers = game.State.TriggerQueue.Where(t => t.MovementObserver?.Kind == TriggerKinds.UnitMovedOtherFriendlyPowerModifier)
            .Select(t => t.TriggerId).ToArray(); Assert.Equal(2, triggers.Length);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: triggers));
        await Choose(game, "ALLY"); await Choose(game, "ALLY"); await game.Top(); await game.Top();
        Assert.Equal(before + 2, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Fact]
    public async Task TheSharedMightModifierExpiresAtTheEndOfTheTurn()
    {
        var game = new Driver(Board()); var before = game.State.CardObjects["ALLY"].Power;
        await Move(game); await Choose(game, "ALLY"); await game.Top();
        for (var passes = 0; game.State.SpellDuelState.IsActive && passes < 8; passes++)
            if (game.State.StackItems.Count > 0 && game.State.PriorityPlayerId is { } priority)
                await game.Act(priority, new PassPriorityCommand());
            else await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        Assert.False(game.State.SpellDuelState.IsActive); await game.Act("P1", new EndTurnCommand());
        Assert.Equal(before, game.State.CardObjects["ALLY"].Power);
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsAForgedDestinationOrTheSourcesOwnTargetIncarnation()
    {
        var game = new Driver(Board()); await Move(game); await Choose(game, "ALLY");
        var item = Assert.Single(game.State.StackItems); var context = item.MovementObserver!;
        foreach (var forged in new[] {
            item with { MovementObserver = context with { Unit = context.Unit with { Destination = new("P1", "BASE") } } },
            item with { TargetObjectIds = ["HOST"], TargetGenerations = new Dictionary<string, long> { ["HOST"] = context.SourceGeneration } },
            item with { TargetGenerations = new Dictionary<string, long> { ["ALLY"] = -1 } } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        await game.Top(); await game.Replay();
    }
}
