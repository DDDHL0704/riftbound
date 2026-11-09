using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class RellAttackArmamentPlayTests
{
    [Fact]
    public async Task AnActuallyPlayedRellKeepsBarrierAndAttacksAfterItsNextReadyStep()
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "RELL", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("RELL", Rell, []));
        Assert.True(game.State.CardObjects["RELL"].IsExhausted);
        Assert.Contains("壁垒", game.State.CardObjects["RELL"].Tags);
        Assert.Empty(game.State.StackItems);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.False(game.State.CardObjects["RELL"].IsExhausted);
        await Permission(game); await game.Act("P1", new PlayCardCommand("GEAR", Shield, [])); await game.Top();
        Assert.Equal("RELL", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AgileAndTheReflexiveAttachmentCanResolveInEitherChosenOrder(bool rellFirst)
    {
        var game = new Driver(Position("SFD·022/221")); await Permission(game);
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("GEAR", "SFD·022/221", ["ALLY"]));
        Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        var child = Assert.Single(game.State.TriggerQueue, trigger => trigger.UnitEvent?.ReflexiveTick is not null);
        var agile = Assert.Single(game.State.TriggerQueue, trigger => trigger.PermanentPlayStackItemId is not null);
        await game.Act("P1", new OrderTriggersCommand(rellFirst ? [child.TriggerId, agile.TriggerId] : [agile.TriggerId, child.TriggerId]));
        await game.Top();
        Assert.Equal(rellFirst ? "RELL" : "ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Top();
        Assert.Equal(rellFirst ? "ALLY" : "RELL", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "EQUIPMENT_ATTACHED"));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task IgnoringPrintedCostsStillRequiresTheMysticVortexReactionIncrease(bool funded)
    {
        var initial = RadGearTestPosition.Put(Position("SFD·022/221"), "BF", "VEN·160", "P1", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P2" } } };
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, funded ? 1 : 0) } };
        var game = new Driver(initial); await Permission(game);
        var play = new PlayCardCommand("GEAR", "SFD·022/221", ["ALLY"]);
        if (!funded) {
            await RadGearTestPosition.Reject(game.State, "P1", play);
            var permission = game.State.PendingEffectPlay!;
            await game.Act("P1", new ChooseCardsCommand(permission.ChoiceId, "EFFECT_PLAY", []));
            Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        } else {
            await game.Act("P1", play);
            Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(2, game.State.TriggerQueue.Count);
            await game.Act("P1", new OrderTriggersCommand(game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            await game.Top(); await game.Top();
        }
        Assert.Null(game.State.PendingEffectPlay); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ReturningTheActualPlayedArmamentBeforeAttachmentCannotReuseItsNewHandIncarnation()
    {
        var initial = RadGearTestPosition.Put(Position(), "GEAR-RETURN", "SFD·135/221", "P1", "HAND");
        var game = new Driver(initial); await Permission(game);
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, []));
        var binding = Assert.Single(game.State.StackItems).UnitEvent!.Other!.Binding;
        await game.Act("P1", new PlayCardCommand("GEAR-RETURN", "SFD·135/221", ["GEAR"])); await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["GEAR"].ObjectGeneration > binding.Generation);
        await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AResolvedAttachmentCannotBeResurrectedByRebuildingCurrentViews()
    {
        var game = new Driver(Position()); await Permission(game);
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, []));
        var child = Assert.Single(game.State.StackItems); await game.Top();
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game,
            game.State with { StackItems = game.State.StackItems.Append(child).ToArray() }));
        await game.ColdCheckpoint();
    }
    [Fact]
    public async Task AThreeManaArmamentCannotQualifyThroughTheFirstEquipmentDiscount()
    {
        var initial = RadGearTestPosition.Put(Position("SFD·059/221"), "FORGE", "SFD·213/221", "P1", "BATTLEFIELD", "FORGE");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "FORGE");
        var game = new Driver(initial); await Attack(game); await game.Top();
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "PLAYED_ARMAMENT_ATTACHMENT_CREATED");
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task OptionalConfirmationCannotBeInventedOrRewoundAgainstItsOriginalAcceptedAnswer(bool rewind)
    {
        var game = new Driver(Position()); await Attack(game, confirm: null);
        var pending = game.State.PendingCardChoice!;
        var original = Assert.Single(game.State.StackItems);
        MatchState forged;
        if (rewind) {
            await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["RELL"]));
            forged = game.State with { StackItems = [original], PendingCardChoice = pending, PriorityPlayerId = null };
        } else {
            forged = game.State with { StackItems = [original with {
                TriggerCost = new(new("RELL", original.UnitEvent!.SourceGeneration)) }], PendingCardChoice = null,
                PriorityPlayerId = "P1" };
        }
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnAlreadyAcceptedZeroCostPlayIsNotASecondOptionalConfirmation()
    {
        var game = new Driver(Position()); await Permission(game);
        var pending = game.State.PendingEffectPlay!;
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, [])); await game.Top();
        Assert.Equal("RELL", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.ColdCheckpoint();
    }

}
