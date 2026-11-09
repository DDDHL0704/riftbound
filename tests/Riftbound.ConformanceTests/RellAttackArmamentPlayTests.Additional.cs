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
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            rellFirst ? [child.TriggerId, agile.TriggerId] : [agile.TriggerId, child.TriggerId]));
        await game.Top();
        Assert.Equal(rellFirst ? "RELL" : "ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(rellFirst ? "BF" : null, game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        await game.Top();
        Assert.Equal(rellFirst ? "ALLY" : "RELL", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(rellFirst ? null : "BF", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "EQUIPMENT_ATTACHED"));
        Assert.Single(game.Events(), entry => entry.Event.Kind == "EQUIPMENT_DETACHED");
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task BothAttachmentSkillsChoosingTheSameHostAttachOnlyOnce(bool rellFirst)
    {
        var game = new Driver(Position("SFD·022/221")); await Permission(game);
        await game.Act("P1", new PlayCardCommand("GEAR", "SFD·022/221", ["RELL"]));
        var child = Assert.Single(game.State.TriggerQueue, trigger => trigger.UnitEvent?.ReflexiveTick is not null);
        var agile = Assert.Single(game.State.TriggerQueue, trigger => trigger.PermanentPlayStackItemId is not null);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            rellFirst ? [child.TriggerId, agile.TriggerId] : [agile.TriggerId, child.TriggerId]));
        await game.Top(); await game.Top();
        Assert.Equal("RELL", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("BF", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "EQUIPMENT_ATTACHED");
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "EQUIPMENT_DETACHED");
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AnAgileArmamentStillEntersWhenItsOnlyFriendlyHostDeparts(bool beforePlay)
    {
        var initial = RadUnitEffectTestFixture.Place(Position("SFD·022/221"), "ALLY", "P1", "GRAVEYARD");
        var game = new Driver(initial); await Attack(game);
        if (beforePlay) {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["RELL"])); await game.Top();
        }
        await game.Top();
        await game.Act("P1", new PlayCardCommand("GEAR", "SFD·022/221", beforePlay ? [] : ["RELL"]));
        if (!beforePlay) {
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["RELL"])); await game.Top();
        }
        for (var i = 0; game.State.StackItems.Count > 0 && i < 4; i++) await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Contains("RELL", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "EQUIPMENT_ATTACHED");
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AnUndeclaredAgileTargetIsChosenAfterTheSimultaneousTriggerOrder(bool rellFirst)
    {
        var game = new Driver(Position("SFD·022/221")); await Permission(game);
        await game.Act("P1", new PlayCardCommand("GEAR", "SFD·022/221", []));
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.PendingCardChoice);
        var child = Assert.Single(game.State.TriggerQueue, trigger => trigger.UnitEvent?.ReflexiveTick is not null);
        var agile = Assert.Single(game.State.TriggerQueue, trigger => trigger.PermanentPlayStackItemId is not null);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            rellFirst ? [child.TriggerId, agile.TriggerId] : [agile.TriggerId, child.TriggerId]));
        if (rellFirst) await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        if (!rellFirst) { await game.Top(); Assert.Equal("RELL", game.State.CardObjects["GEAR"].AttachedToObjectId); }
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnAgileTargetCannotBeRewrittenAfterItsPostEntryConfirmation()
    {
        var game = new Driver(Position("SFD·022/221")); await Permission(game);
        await game.Act("P1", new PlayCardCommand("GEAR", "SFD·022/221", []));
        var child = Assert.Single(game.State.TriggerQueue, trigger => trigger.UnitEvent?.ReflexiveTick is not null);
        var agile = Assert.Single(game.State.TriggerQueue, trigger => trigger.PermanentPlayStackItemId is not null);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: [agile.TriggerId, child.TriggerId]));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var original = game.State.StackItems.Single(item => item.SourceObjectId == "GEAR");
        var forged = original with { TargetObjectIds = ["RELL"], TargetGenerations = new Dictionary<string, long> {
            ["RELL"] = game.State.CardObjects["RELL"].ObjectGeneration } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with {
            StackItems = game.State.StackItems.Select(item => item.StackItemId == original.StackItemId ? forged : item).ToArray() }));
        await game.Top(); await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AResolvedAgileAttachmentCannotBeResurrectedWithItsOldConfirmedTargets()
    {
        var game = new Driver(Position("SFD·022/221")); await Permission(game);
        await game.Act("P1", new PlayCardCommand("GEAR", "SFD·022/221", []));
        var child = Assert.Single(game.State.TriggerQueue, trigger => trigger.UnitEvent?.ReflexiveTick is not null);
        var agile = Assert.Single(game.State.TriggerQueue, trigger => trigger.PermanentPlayStackItemId is not null);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: [agile.TriggerId, child.TriggerId]));
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var original = game.State.StackItems.Single(item => item.SourceObjectId == "GEAR");
        await game.Top(); await game.Top();
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game,
            game.State with { StackItems = [original] }));
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
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            await game.Top(); await game.Top();
        }
        Assert.Null(game.State.PendingEffectPlay); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AReturnedArmamentCannotReuseItsResolvedAttachmentAndSwiftRecallCannotInterruptIt()
    {
        var initial = RadGearTestPosition.Put(Position(), "GEAR-RETURN", "SFD·135/221", "P1", "HAND");
        var game = new Driver(initial); await Permission(game);
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, []));
        var child = Assert.Single(game.State.StackItems);
        var binding = child.UnitEvent!.Other!.Binding;
        // CN310.4, 159.2.a: Swift is legal in an open showdown, but only
        // Reaction can interrupt this independent attachment skill.
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("GEAR-RETURN", "SFD·135/221", ["GEAR"]));
        await game.Top();
        Assert.Equal("RELL", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Act("P1", new PlayCardCommand("GEAR-RETURN", "SFD·135/221", ["GEAR"])); await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["GEAR"].ObjectGeneration > binding.Generation);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game,
            game.State with { StackItems = [child] }));
        await game.ColdCheckpoint();
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
