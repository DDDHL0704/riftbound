using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DeploymentHoldTests
{
    private const string Kind = "DEPLOYMENT_OPPONENT_HELD_DESTROY";

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task OpponentHoldOffersAResponseBeforeOneDeploymentTriggerDestroysItsSource(bool duplicate, bool exhausted, bool unitEquipment)
    {
        var game = new Driver(Position(duplicate, exhausted, unitEquipment));
        await game.Act("P1", new PassPriorityCommand());
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(Kind, item.HeldContext!.Kind);
        Assert.Equal("P2", item.ControllerId);
        Assert.Equal("DEPLOYED", item.SourceObjectId);
        Assert.Equal("CHANNEL", game.State.TurnStartStep);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["DEPLOYED"].Zone);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal("P2", game.State.PriorityPlayerId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "EQUIPMENT_DESTROYED");
        await game.Top();
        Assert.Contains("DEPLOYED", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("DEPLOYED", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(1, game.State.CardObjects["DEPLOYED"].ObjectGeneration);
        Assert.Equal("P1", game.State.CardObjects["DEPLOYED"].OwnerId);
        Assert.Equal(new ObjectLocationState("P1", "GRAVEYARD"), game.State.ObjectLocations["DEPLOYED"]);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "F"), game.State.ObjectLocations["FRIENDLY"]);
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "OTHER"), game.State.ObjectLocations["ELSEWHERE"]);
        Assert.Equal("BASE", game.State.ObjectLocations["ORDINARY"].Zone);
        Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_RESOLVED" && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), "HOLD_" + Kind));
        Assert.Null(game.State.TurnStartStep);
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData("friendly")]
    [InlineData("other-field")]
    [InlineData("base")]
    [InlineData("similar-keyword")]
    public async Task OwnHoldOtherLocationsAndSimilarTextDoNotTriggerDeployment(string branch)
    {
        var initial = Position();
        var card = initial.CardObjects["DEPLOYED"];
        if (branch == "friendly") initial = Relocate(initial, card with { ControllerId = "P1" }, "BATTLEFIELD", "F");
        if (branch == "other-field") initial = Relocate(initial, card, "BATTLEFIELD", "OTHER");
        if (branch == "base") initial = Relocate(initial, card, "BASE");
        if (branch == "similar-keyword") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { [card.ObjectId] = card with { Tags = [CardObjectTags.EquipmentCard, "部署其他"] } } };
        var game = new Driver(initial);
        await game.Act("P1", new PassPriorityCommand());
        Assert.DoesNotContain(game.State.StackItems, i => i.HeldContext?.Kind == Kind);
        Assert.DoesNotContain(game.State.TriggerQueue, i => i.HeldContext?.Kind == Kind);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED" && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), "HOLD_" + Kind));
        await game.Replay();
    }

    [Fact]
    public async Task CounteringTheDeploymentAbilityLeavesEquipmentAndResumesTheTurn()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "COUNTER", "SFD·045/221", "P1"));
        await game.Act("P1", new PassPriorityCommand());
        var item = Assert.Single(game.State.StackItems);
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("COUNTER", "SFD·045/221", [item.StackItemId]));
        await game.Top();
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "F"), game.State.ObjectLocations["DEPLOYED"]);
        Assert.DoesNotContain("DEPLOYED", game.State.PlayerZones["P1"].Graveyard);
        Assert.Null(game.State.TurnStartStep);
        await game.Replay();
    }

    [Theory]
    [InlineData("move", true)]
    [InlineData("control", true)]
    [InlineData("keyword", true)]
    [InlineData("leave", false)]
    [InlineData("reenter", false)]
    public async Task CapturedDeploymentUsesTheOriginalObjectAfterResponseChanges(string branch, bool destroyed)
    {
        var capture = await new CoreRuleEngine().ResolveAsync(Position(), new("capture", "P1", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(capture.Accepted, capture.ErrorMessage);
        var state = capture.State;
        var source = state.CardObjects["DEPLOYED"];
        state = branch switch
        {
            "move" => Relocate(state, source, "BASE"),
            "control" => Relocate(state, source with { ControllerId = "P1" }, "BATTLEFIELD", "F"),
            "keyword" => Relocate(state, source with { Tags = [CardObjectTags.EquipmentCard] }, "BATTLEFIELD", "F"),
            "leave" => Relocate(state, source with { ObjectGeneration = source.ObjectGeneration + 1,
                ControllerId = source.OwnerId, Tags = [CardObjectTags.EquipmentCard] }, "HAND"),
            _ => Relocate(state, source with { ObjectGeneration = source.ObjectGeneration + 2 }, "BATTLEFIELD", "F")
        };
        if (branch == "leave")
        {
            foreach (var snapshot in new[] { ResolutionResult.BuildSnapshots(state)["P2"], ResolutionResult.BuildSpectatorSnapshot(state) })
            {
                var visible = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
                Assert.Equal("HIDDEN", visible["sourceObjectId"]);
                Assert.Equal("HOLD_" + Kind, visible["effectKind"]);
                Assert.Equal("SFD·022/221", visible["cardNo"]);
            }
        }
        var game = new Driver(state);
        await game.Top();
        Assert.Equal(destroyed, game.State.PlayerZones["P1"].Graveyard.Contains("DEPLOYED"));
        if (!destroyed) Assert.Equal(branch == "leave" ? "HAND" : "BATTLEFIELD", game.State.ObjectLocations["DEPLOYED"].Zone);
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("source")]
    [InlineData("generation")]
    [InlineData("field")]
    [InlineData("field-generation")]
    [InlineData("controller")]
    [InlineData("holder")]
    [InlineData("tick")]
    [InlineData("amount")]
    [InlineData("target")]
    public async Task RecoveryRejectsForgedDeploymentOriginsAndStackShape(string mutation)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PassPriorityCommand());
        var item = Assert.Single(game.State.StackItems);
        var context = item.HeldContext!;
        var origin = context.Deployment!;
        var changed = mutation switch
        {
            "missing" => context with { Deployment = null },
            "source" => context with { Deployment = origin with { Source = new("FRIENDLY", origin.Source.Generation) } },
            "generation" => context with { SourceGeneration = context.SourceGeneration + 1 },
            "field" => context with { Deployment = origin with { Battlefield = new("OTHER", 0) } },
            "field-generation" => context with { Deployment = origin with { Battlefield = origin.Battlefield with { Generation = 1 } } },
            "controller" => context with { Deployment = origin with { ControllerId = "P1" } },
            "holder" => context with { Deployment = origin with { HoldingPlayerId = "P2" } },
            "tick" => context with { Deployment = origin with { CapturedTick = game.State.Tick + 1 } },
            "amount" => context with { Amount = 1 },
            _ => context
        };
        var forged = game.State with { StackItems = [item with { HeldContext = changed,
            TargetObjectIds = mutation == "target" ? ["FRIENDLY"] : item.TargetObjectIds }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        await game.Top();
        await game.Replay();
    }

    private static MatchState Position(bool duplicate = false, bool exhausted = false, bool unitEquipment = false)
    {
        var state = OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·294/298"), "HOST", "SFD·125/221", "F", "P1") with { Seed = 494949 };
        state = Relocate(state, new("DEPLOYED", cardNo: "SFD·022/221", ownerId: "P1", controllerId: "P2", isExhausted: exhausted, power: 5,
            tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy,
                ..duplicate ? new[] { CardEquipmentKeywordNames.Deploy } : [],
                ..unitEquipment ? new[] { CardObjectTags.UnitCard } : []]), "BATTLEFIELD", "F");
        state = Relocate(state, new("FRIENDLY", cardNo: "SFD·022/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy]), "BATTLEFIELD", "F");
        state = Relocate(state, new("ELSEWHERE", cardNo: "SFD·022/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy]), "BATTLEFIELD", "OTHER");
        return Relocate(state, new("ORDINARY", cardNo: "SFD·022/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.EquipmentCard]), "BATTLEFIELD", "F");
    }

    private static MatchState Relocate(MatchState state, CardObjectState card, string zone, string? field = null)
    {
        var id = card.ObjectId;
        var player = card.ControllerId!;
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
        foreach (var key in zones.Keys.ToArray()) zones[key] = zones[key] with {
            Base = zones[key].Base.Where(i => i != id).ToArray(), Battlefields = zones[key].Battlefields.Where(i => i != id).ToArray(), Hand = zones[key].Hand.Where(i => i != id).ToArray() };
        zones[player] = zone switch {
            "BASE" => zones[player] with { Base = [..zones[player].Base, id] },
            "HAND" => zones[player] with { Hand = [..zones[player].Hand, id] },
            _ => zones[player] with { Battlefields = [..zones[player].Battlefields, id] }
        };
        return state with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = card },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, zone, field) } };
    }
}
