using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DeployedEquipmentCleanupTests
{
    [Theory]
    [InlineData("P1", false)]
    [InlineData("P2", false)]
    [InlineData("P1", true)]
    [InlineData("P2", true)]
    public async Task CleanupKeepsDeployedEquipmentAtItsBattlefieldAndRecallsOrdinaryEquipment(
        string controller, bool exhausted)
    {
        var initial = Position(controller, exhausted);
        var pending = Assert.Single(initial.PendingCleanupTasks,
            task => task.Kind == "RECALL_UNATTACHED_EQUIPMENT");
        Assert.Equal("ORDINARY", pending.ObjectId);
        Assert.Equal(controller, pending.PlayerId);
        Assert.DoesNotContain(initial.PendingTaskQueue.Tasks, task => task.ObjectId == "DEPLOYED");

        var game = new Driver(initial);
        await game.Act("P2", new PassPriorityCommand());

        Assert.Empty(game.State.StackItems);
        Assert.Equal(new ObjectLocationState(controller, "BATTLEFIELD", "BF"), game.State.ObjectLocations["DEPLOYED"]);
        Assert.Contains("DEPLOYED", game.State.PlayerZones[controller].Battlefields);
        Assert.DoesNotContain("DEPLOYED", game.State.PlayerZones[controller].Base);
        Assert.Equal(exhausted, game.State.CardObjects["DEPLOYED"].IsExhausted);
        Assert.Equal("P1", game.State.CardObjects["DEPLOYED"].OwnerId);
        Assert.Equal(controller, game.State.CardObjects["DEPLOYED"].ControllerId);
        Assert.Equal(new ObjectLocationState(controller, "BASE"), game.State.ObjectLocations["ORDINARY"]);
        var recalled = Assert.Single(game.Events(), entry => entry.Event.Kind == "EQUIPMENT_RECALLED_TO_BASE");
        Assert.Equal("ORDINARY", recalled.Event.Payload["equipmentObjectId"]);
        Assert.Empty(game.State.PendingCleanupTasks);
        // Act performs cold recovery, duplicate submission, hidden-information
        // checks and replay of the complete original journal.
        await game.Replay();
    }

    [Theory]
    [InlineData("plain", true)]
    [InlineData("similar-keyword", true)]
    [InlineData("deployed", false)]
    [InlineData("deployed-twice", false)]
    [InlineData("unit-equipment", false)]
    [InlineData("attached", false)]
    [InlineData("face-down", false)]
    public void PendingCleanupUsesExactDeployKeywordAndPreservesExistingTypeAndAttachmentBoundaries(
        string kind, bool recalled)
    {
        var state = Position("P2", false);
        var card = state.CardObjects["DEPLOYED"] with
        {
            Tags = kind switch
            {
                "plain" or "attached" or "face-down" => [CardObjectTags.EquipmentCard],
                "similar-keyword" => [CardObjectTags.EquipmentCard, "部署其他"],
                "deployed-twice" => [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy, CardEquipmentKeywordNames.Deploy],
                "unit-equipment" => [CardObjectTags.EquipmentCard, CardObjectTags.UnitCard],
                _ => [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy]
            },
            AttachedToObjectId = kind == "attached" ? "HOST" : null,
            IsFaceDown = kind == "face-down"
        };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["DEPLOYED"] = card } };
        Assert.Equal(recalled, state.PendingCleanupTasks.Any(task => task.Kind == "RECALL_UNATTACHED_EQUIPMENT" && task.ObjectId == card.ObjectId));
        Assert.Equal(recalled, state.PendingTaskQueue.Tasks.Any(task => task.Kind == "RECALL_UNATTACHED_EQUIPMENT" && task.ObjectId == card.ObjectId));
    }

    private static MatchState Position(string controller, bool exhausted)
    {
        var zones = new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Battlefields = ["BF", "HOST"] },
            ["P2"] = PlayerZones.Empty
        };
        zones[controller] = zones[controller] with { Battlefields = [.. zones[controller].Battlefields, "DEPLOYED", "ORDINARY"] };
        var cards = new Dictionary<string, CardObjectState>
        {
            ["BF"] = new("BF", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1", controllerId: "P1"),
            ["HOST"] = new("HOST", power: 5, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["DEPLOYED"] = new("DEPLOYED", isExhausted: exhausted,
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy], ownerId: "P1", controllerId: controller),
            ["ORDINARY"] = new("ORDINARY", isExhausted: exhausted,
                tags: [CardObjectTags.EquipmentCard], ownerId: "P1", controllerId: controller)
        };
        return new MatchState("DEPLOY-CLEANUP", 0, 3, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralClosed,
            playerZones: zones, cardObjects: cards,
            objectLocations: new Dictionary<string, ObjectLocationState>
            {
                ["BF"] = new("P1", "BATTLEFIELD", "BF"), ["HOST"] = new("P1", "BATTLEFIELD", "BF"),
                ["DEPLOYED"] = new(controller, "BATTLEFIELD", "BF"), ["ORDINARY"] = new(controller, "BATTLEFIELD", "BF")
            },
            priorityPlayerId: "P2", passedPriorityPlayerIds: ["P1"],
            stackItems: [new("NOOP", "P1", "HOST", "UNKNOWN_NOOP_EFFECT")], seed: 424242);
    }
}
