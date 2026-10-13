using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyReactionTideCallerTests
{
    private const string BattlefieldObjectId = "F";
    private const string TideCallerObjectId = "SHADOW";
    private const string FriendlyTargetObjectId = "SHADOW-OUTSIDE";
    private const string FriendlyEquipmentObjectId = "GEAR";

    [Fact]
    public async Task PromptMetadataExposesFriendlyUnitTargetsForStandbyReactionSwap()
    {
        var state = await BuildClosedPriorityState();
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Contains(CommandTypes.RevealCard, prompt.EnabledActions());
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.RevealCard);
        Assert.True(candidate.Enabled);
        Assert.Equal([TideCallerObjectId], candidate.Sources!.Select(s => s.Id));
        Assert.Empty(candidate.Targets ?? []);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal(TideCallerObjectId, requirement["sourceObjectId"]);
        Assert.Equal("OGN·199/298", requirement["cardNo"]);
        Assert.Equal("STANDBY_REACTION", requirement["mode"]);
        Assert.Equal(0, requirement["minTargetCount"]);
        Assert.Equal(0, requirement["maxTargetCount"]);
        Assert.Equal(CardTargetScopes.FriendlyUnit, requirement["targetScope"]);
        var choices = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(requirement["targetChoicesByIndex"]);
        Assert.Empty(choices);
        var entered = await OfficialInsightAndSpellLockTests.Act(state, "P1", new RevealCardCommand(
            TideCallerObjectId, "OGN·199/298", [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
        var pending = Assert.IsType<PendingCardChoiceState>(entered.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", pending.ChoiceWindow);
        Assert.Equal(0, pending.RequiredCount);
        Assert.Equal(1, pending.MaxCount);
        Assert.Equal([FriendlyTargetObjectId], pending.LegalObjectIds);
        Assert.Equal(BattlefieldObjectId, entered.State.ObjectLocations[TideCallerObjectId].BattlefieldObjectId);
        OfficialGraveyardRecastTests.Restore(entered.State);
    }

    [Fact]
    public async Task BaseStandbyReactionTideCallerSwapsWithFriendlyUnitOnResolution()
    {
        // The historical method identity remains; current standby starts on a battlefield (811.1.b).
        var initial = await BuildClosedPriorityState();
        var bottom = Assert.Single(initial.StackItems);
        var result = await OfficialInsightAndSpellLockTests.Act(initial, "P1", new RevealCardCommand(
            TideCallerObjectId, "OGN·199/298", [FriendlyTargetObjectId], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
        Assert.DoesNotContain(TideCallerObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Contains(FriendlyEquipmentObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(2, result.State.StackItems.Count);
        Assert.Equal(bottom, result.State.StackItems[0]);
        var standby = Assert.Single(result.State.StackItems, s => s.SourceObjectId == TideCallerObjectId);
        Assert.Equal("TIDE_CALLER_STANDBY_SWAP_PLAY_UNIT", standby.EffectKind);
        Assert.Equal([FriendlyTargetObjectId], standby.TargetObjectIds);
        Assert.Equal("BATTLEFIELD:" + BattlefieldObjectId, standby.Destination);
        Assert.Equal(BattlefieldObjectId, standby.PlayedFromStandby!.Battlefield.ObjectId);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.True(standby.SourceConfirmed);
        Assert.Equal(BattlefieldObjectId, result.State.ObjectLocations[TideCallerObjectId].BattlefieldObjectId);
        foreach (var kind in new[] { "CARD_REVEALED", "CARD_PLAYED", "COST_PAID", "UNIT_PLAYED_TO_BATTLEFIELD", "TRIGGER_QUEUED" })
            Assert.Contains(result.Events, e => e.Kind == kind);
        OfficialGraveyardRecastTests.Restore(result.State);
        result = await OfficialGraveyardRecastTests.Top(result.State);
        Assert.Equal(bottom.StackItemId, Assert.Single(result.State.StackItems).StackItemId);
        Assert.Contains(TideCallerObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Contains(FriendlyEquipmentObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Contains(FriendlyTargetObjectId, result.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(TideCallerObjectId, result.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(FriendlyTargetObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(new ObjectLocationState("P1", "BASE"), result.State.ObjectLocations[TideCallerObjectId]);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", BattlefieldObjectId), result.State.ObjectLocations[FriendlyTargetObjectId]);
        Assert.False(result.State.CardObjects[TideCallerObjectId].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects[TideCallerObjectId].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], result.State.CardObjects[TideCallerObjectId].Tags);
        Assert.Contains(result.Events, e => e.Kind == "STACK_ITEM_RESOLVED");
        var swap = Assert.Single(result.Events, e => e.Kind == "UNIT_LOCATIONS_SWAPPED");
        Assert.Equal(TideCallerObjectId, swap.Payload["sourceObjectId"]);
        Assert.Equal(TideCallerObjectId, swap.Payload["firstTargetObjectId"]);
        Assert.Equal(FriendlyTargetObjectId, swap.Payload["secondTargetObjectId"]);
        Assert.Equal("BASE", swap.Payload["firstDestinationZone"]);
        Assert.Equal("BATTLEFIELD", swap.Payload["secondDestinationZone"]);
        OfficialGraveyardRecastTests.Restore(result.State);
        ArmamentMightTests.PublicRecovery(result.State);
    }

    private static Task<MatchState> BuildClosedPriorityState() => StandbySourceRulesTests.ReadyShadow("OGN·199/298");
}
