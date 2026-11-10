using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN Spiritforged functional errata 260114, revised Rell text; CN383,
// 388–389, 356.1.b: optional real play followed by a separate attachment skill.
public sealed partial class RellAttackArmamentPlayTests
{
    private const string Rell = "SFD·024/221", Shield = "SFD·033/221";

    private static MatchState Position(string gear = Shield)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "RELL", Rell, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "GEAR", gear, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ENEMY", "OGN·219/298", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
    }

    private static async Task Attack(Driver game, bool? confirm = true)
    {
        await game.Act("P1", new MoveUnitCommand("RELL", "BASE", "BATTLEFIELD:BF", []));
        for (var i = 0; i < 4 && game.State.FocusPlayerId is { } focus; i++)
            await game.Act(focus, new PassFocusCommand());
        Assert.Single(game.State.StackItems, item => item.UnitEvent?.CardNo == Rell);
        Assert.Null(game.State.PendingEffectPlay);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_OPTIONAL_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(["RELL"], choice.LegalObjectIds);
        if (confirm is { } accept)
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["RELL"] : []));
    }

    private static async Task Permission(Driver game)
    {
        await Attack(game); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.False(pending.Optional); Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower);
        Assert.False(pending.IgnoreAllCosts); Assert.Equal("HAND", pending.SourceZone);
    }

    [Theory, InlineData(Shield), InlineData("SFD·016/221"), InlineData("SFD·172/221")]
    public async Task ActualAttackWaivesOnlyBaseCostsAndCreatesAnIndependentUntargetedAttachment(string gear)
    {
        var game = new Driver(Position(gear)); await Permission(game);
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("GEAR", gear, []));
        Assert.Null(game.State.PendingEffectPlay); Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(pool, game.State.RunePools["P1"]);
        var child = Assert.Single(game.State.StackItems);
        Assert.Equal("GEAR", child.UnitEvent!.Other!.Binding.ObjectId);
        Assert.Equal(game.State.CardObjects["GEAR"].ObjectGeneration, child.UnitEvent.Other.Binding.Generation);
        Assert.True(child.UnitEvent.ReflexiveTick > child.UnitEvent.CapturedTick);
        Assert.Empty(child.TargetObjectIds); Assert.Null(game.State.PendingPayment);
        Assert.Single(game.State.CardPlays!.Resolved, play => play.Source.ObjectId == "GEAR");
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Top();
        Assert.Equal("RELL", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("BF", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "EQUIPMENT_ATTACHED");
        await game.Replay();
    }

    [Fact]
    public async Task OptionalAttackConfirmationPrecedesResponsesAndDoesNotRevealOrPlayTheArmament()
    {
        var game = new Driver(Position()); await Attack(game, confirm: null);
        var choice = game.State.PendingCardChoice!;
        Assert.DoesNotContain("GEAR", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        Assert.Null(game.State.PriorityPlayerId);
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["RELL"]));
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("GEAR", Shield, []));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "PLAYED_ARMAMENT_ATTACHMENT_CREATED");
        await game.Replay();
    }

    [Fact]
    public async Task PermissionListsOnlyOwnPhysicalHandArmamentsWhosePrintedManaIsAtMostTwo()
    {
        var initial = RadGearTestPosition.Put(Position(), "TOO-LARGE", "SFD·059/221", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "NOT-ARMAMENT", "OGN·023/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "UNIT", "OGN·219/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "THEIR-GEAR", Shield, "P2", "HAND");
        initial = RadGearTestPosition.Put(initial, "DEAD-GEAR", Shield, "P1", "GRAVEYARD");
        var game = new Driver(initial); await Permission(game);
        Assert.Equal(["GEAR"], game.State.PendingEffectPlay!.Sources.Keys);
        foreach (var id in new[] { "TOO-LARGE", "NOT-ARMAMENT", "UNIT", "THEIR-GEAR", "DEAD-GEAR" })
            await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand(id, game.State.CardObjects[id].CardNo!, []));
        await game.Replay();
    }

    [Theory, InlineData("OGN·023/298"), InlineData("SFD·059/221")]
    public async Task AnAttackWithoutAnyQualifyingArmamentFinishesWithoutAPermission(string gear)
    {
        var game = new Driver(Position(gear)); await Attack(game); await game.Top();
        Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task SourceDepartureDoesNotCancelThePlayButCannotAttachToADepartedIncarnation(bool beforePlay)
    {
        var game = new Driver(Position()); await Attack(game);
        var firstPrivateEvent = -1;
        if (beforePlay) {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["RELL"])); await game.Top();
            firstPrivateEvent = game.Events().Count();
        }
        await game.Top(); Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, []));
        if (!beforePlay) {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["RELL"])); await game.Top();
            firstPrivateEvent = game.Events().Count();
        }
        Assert.Contains("RELL", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("RELL", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var child = Assert.Single(game.State.StackItems);
        var queued = Assert.Single(game.Events(), record => record.Event.Kind == "TRIGGER_QUEUED"
            && Equals(record.Event.Payload.GetValueOrDefault("triggerId"), child.StackItemId[8..]));
        Assert.Equal(beforePlay ? "HIDDEN" : "RELL", queued.Event.Payload["sourceObjectId"]);
        if (beforePlay) Assert.Equal(MatchStateHasher.HashValue("RELL"), queued.Event.Payload["timingSourceHash"]);
        Assert.Empty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State));
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.True(firstPrivateEvent >= 0);
        // Only inspect events born after the real return. Earlier accepted
        // public records remain untouched, including a skill earned on field.
        Assert.All(game.Events().Skip(firstPrivateEvent), record =>
            Assert.DoesNotContain("\"RELL\"", JsonSerializer.Serialize(record.Event.Payload)));
        await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task APrivateQueuedSourceCannotBeReplacedByAnotherRealRell(bool changeCapture)
    {
        var initial = RadGearTestPosition.Put(Position(), "OTHER-RELL", Rell, "P1", "HAND");
        var game = new Driver(initial); await Attack(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["RELL"])); await game.Top();
        await game.Top();
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, []));
        var child = Assert.Single(game.State.StackItems);
        var changed = child with { SourceObjectId = "OTHER-RELL", UnitEvent = changeCapture
            ? child.UnitEvent! with { SourceGeneration = game.State.CardObjects["OTHER-RELL"].ObjectGeneration }
            : child.UnitEvent };
        var forged = game.State with { StackItems = [changed] };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged), error =>
            error.Contains("battlefield timing skill differs from its original accepted capture", StringComparison.Ordinal));
        await game.Top(); await game.Replay();
    }

    [Theory, InlineData("all-costs"), InlineData("mana"), InlineData("power"), InlineData("optional"),
     InlineData("sources"), InlineData("source-generation"), InlineData("destination"), InlineData("extra-target")]
    public async Task RecoveryRejectsAlteredPermissionsEvenWithRebuiltPublicViews(string mutation)
    {
        var game = new Driver(Position()); await Permission(game);
        var p = game.State.PendingEffectPlay!;
        var forged = game.State with { PendingEffectPlay = mutation switch {
            "all-costs" => p with { IgnoreAllCosts = true }, "mana" => p with { IgnoreBaseMana = false },
            "power" => p with { IgnoreBasePower = false }, "optional" => p with { Optional = true },
            "sources" => p with { Sources = new Dictionary<string, long>() },
            "source-generation" => p with { Sources = new Dictionary<string, long> { ["GEAR"] = p.Sources["GEAR"] + 1 } },
            "destination" => p with { DestinationPolicy = "SOURCE_HERE_IF_UNIT" },
            _ => p with { Parent = p.Parent with { TargetObjectIds = ["ALLY"] } } } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }

    [Theory, InlineData("erase"), InlineData("duplicate"), InlineData("other"), InlineData("other-generation"),
     InlineData("birth"), InlineData("original-capture"), InlineData("controller"), InlineData("resolved-play")]
    public async Task TheAttachmentRequiresItsOriginalAcceptedCaptureAndSuccessfulPlay(string mutation)
    {
        var game = new Driver(Position()); await Permission(game);
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, []));
        var child = Assert.Single(game.State.StackItems); var c = child.UnitEvent!;
        var changed = child with { UnitEvent = mutation switch {
            "other" => c with { Other = c.Other! with { Binding = new("ALLY", 0) } },
            "other-generation" => c with { Other = c.Other! with { Binding = c.Other!.Binding with { Generation = c.Other.Binding.Generation + 1 } } },
            "birth" => c with { ReflexiveTick = c.ReflexiveTick - 1 },
            "original-capture" => c with { CapturedTick = c.CapturedTick + 1 }, _ => c },
            ControllerId = mutation == "controller" ? "P2" : child.ControllerId };
        var forged = mutation switch {
            "erase" => game.State with { StackItems = [] },
            "duplicate" => game.State with { StackItems = [child, child with { StackItemId = child.StackItemId + "-clone" }] },
            "resolved-play" => game.State with { CardPlays = game.State.CardPlays! with { Resolved = [] } },
            _ => game.State with { StackItems = [changed] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Top(); await game.Replay();
    }
}
