using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class HandDiscardObserverTests
{
    internal static MatchState Position(string cardNo = "OGN·202/298", string owner = "P1")
    {
        var s = VenUnitEntryBasicTests.Position("OGN·096/298") with { RoomId = "DISCARD-OBSERVER-EXPLICIT-MIDGAME" };
        s = Put(s, "SPELL", "VEN·008", "P1", "HAND");
        s = Put(s, "FODDER", "VEN·049", "P1", "HAND");
        s = Put(s, "JINX", cardNo, owner, "BASE");
        s = LocalDestructionRecallTests.At(s, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["JINX"] = s.CardObjects["JINX"] with { IsExhausted = true } } };
    }

    internal static MatchState Put(MatchState s, string id, string no, string player, string zone)
    {
        var z = s.PlayerZones[player];
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { [id] = new(player, zone) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { [player] = zone == "HAND"
                ? z with { Hand = [..z.Hand, id] } : z with { Base = [..z.Base, id] } } };
    }

    internal static PlayCardCommand DiscardCost() => new("SPELL", "VEN·008", ["ENEMY"],
        OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]);

    [Theory]
    [InlineData("OGN·202/298")] [InlineData("OGN·202a/298")] [InlineData("ARC-005/006")]
    public async Task EveryPrintingKeepsPaidCostAndItsOwnOrdinaryResponseWindow(string cardNo)
    {
        var s = Position(cardNo); var game = new Driver(s);
        await game.Act("P1", DiscardCost());
        var item = Assert.Single(game.State.StackItems, i => i.HandDiscardObserver is not null);
        Assert.Equal(cardNo, item.CardNo); Assert.Equal("P1", item.HandDiscardObserver!.ControllerId);
        Assert.Equal(s.CardObjects["JINX"].ObjectGeneration, item.HandDiscardObserver.Source.Generation);
        Assert.True(game.State.CardObjects["JINX"].IsExhausted);
        Assert.Equal(s.CardObjects["JINX"].Power, game.State.CardObjects["JINX"].Power);
        Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
        await game.Top();
        Assert.False(game.State.CardObjects["JINX"].IsExhausted);
        Assert.Equal(s.CardObjects["JINX"].Power + 1, game.State.CardObjects["JINX"].Power);
        Assert.Single(game.State.StackItems, i => i.SourceObjectId == "SPELL");
        Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "JINX"));
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task ConditionalCountersCannotChooseTheUntargetedEarnedSkillOrRefundDiscard()
    {
        var s = EquipChainTests.Response(Position(), "SFD·045/221", "P2"); var game = new Driver(s);
        await game.Act("P1", DiscardCost());
        var trigger = Assert.Single(game.State.StackItems, i => i.HandDiscardObserver is not null);
        Assert.Empty(trigger.TargetObjectIds);
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        var counter = new PlayCardCommand("RESPONSE", "SFD·045/221", [trigger.StackItemId]);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(game.State, "P2", PlayCostPreviewTests.Request(game.State, counter)).CanPay);
        var raw = JsonSerializer.SerializeToNode(counter, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P2"].PromptId; raw["snapshotTick"] = game.State.Tick;
        await game.ActRetained(new("P2", "illegal-conditional-counter", JsonSerializer.SerializeToElement(raw)), false);
        Assert.Single(game.State.StackItems, i => i.HandDiscardObserver is not null);
        Assert.Single(game.State.StackItems, i => i.SourceObjectId == "SPELL");
        Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("RESPONSE", game.State.PlayerZones["P2"].Hand);
        Assert.True(game.State.CardObjects["JINX"].IsExhausted);
        Assert.Equal(s.CardObjects["JINX"].Power, game.State.CardObjects["JINX"].Power);
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.False(game.State.CardObjects["JINX"].IsExhausted);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task ActualReturnToPrivateHandKeepsThePublicEarnedAbilityButCannotBuffTheCard()
    {
        var s = LocalDestructionRecallTests.At(Position(), "JINX", "P1", "BATTLEFIELD", "BF");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["JINX"] = s.CardObjects["JINX"] with { Power = 3 } } };
        s = EquipChainTests.Response(s, "OGN·169/298", "P2"); var game = new Driver(s);
        await game.Act("P1", DiscardCost());
        var earned = Assert.Single(game.State.StackItems, i => i.HandDiscardObserver is not null).HandDiscardObserver!;
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["JINX"]));
        await game.Top();
        Assert.Contains("JINX", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["JINX"].ObjectGeneration > earned.Source.Generation);
        Assert.Equal(MatchStateHasher.HashValue(earned), MatchStateHasher.HashValue(
            Assert.Single(game.State.StackItems, i => i.HandDiscardObserver is not null).HandDiscardObserver));
        var before = MatchStateHasher.HashValue(game.State.CardObjects["JINX"]);
        await game.Top();
        Assert.Equal(before, MatchStateHasher.HashValue(game.State.CardObjects["JINX"]));
        Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "JINX"));
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("controller")] [InlineData("new-generation")] [InlineData("new-face")]
    public async Task CapturedSelfEffectKeepsTheOriginalObjectAndText(string change)
    {
        var game = new Driver(Position()); await game.Act("P1", DiscardCost());
        var earned = game.State; var c = earned.CardObjects["JINX"];
        var modified = change switch {
            "controller" => LocalDestructionRecallTests.At(earned, "JINX", "P2", "BASE"),
            "new-generation" => earned with { CardObjects = new Dictionary<string, CardObjectState>(earned.CardObjects) {
                ["JINX"] = c with { ObjectGeneration = c.ObjectGeneration + 1 } } },
            _ => earned with { CardObjects = new Dictionary<string, CardObjectState>(earned.CardObjects) {
                ["JINX"] = c with { CardNo = "OGN·001/298", CopyFaceTransitions = [new(earned.Tick, c.ObjectGeneration, c.CardNo!, "OGN·001/298")] } } }
        };
        // These are explicit component continuation states, not rewritten journals.
        modified = OfficialInsightAndSpellLockTests.Restore(modified);
        var done = await OfficialGraveyardRecastTests.Top(modified);
        Assert.True(done.Accepted, done.ErrorMessage); OfficialInsightAndSpellLockTests.Restore(done.State);
        Assert.Single(done.Events, e => e.Kind == "TRIGGER_RESOLVED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "JINX"));
        Assert.Equal(change == "new-generation", done.State.CardObjects["JINX"].IsExhausted);
        Assert.Equal(c.Power + (change == "new-generation" ? 0 : 1), done.State.CardObjects["JINX"].Power);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task PrintedAndAttachedTextInstancesEachEarnOneSkillForTheSameDiscard(int copies)
    {
        var s = Position();
        for (var n = 0; n < copies; n++)
        {
            var id = "GEAR" + n; s = Put(s, id, "SFD·059/221", "P1", "BASE");
            s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                [id] = s.CardObjects[id] with { AttachedToObjectId = "JINX", AttachmentRevision = 1,
                    AttachedRuleText = new(new("JINX", 0), "OGN·202/298", 1, 0, 0) } } };
        }
        var game = new Driver(s); await game.Act("P1", DiscardCost());
        Assert.Equal(copies + 1, game.State.TriggerQueue.Count);
        Assert.Equal(copies + 1, game.State.TriggerQueue.Select(t => t.HandDiscardObserver!.InstanceId).Distinct().Count());
        Assert.True(game.State.CardObjects["JINX"].IsExhausted);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var n = 0; n <= copies; n++)
        {
            await game.Top(); Assert.Equal(s.CardObjects["JINX"].Power + n + 1, game.State.CardObjects["JINX"].Power);
            Assert.Single(game.State.StackItems, i => i.SourceObjectId == "SPELL");
        }
        Assert.Equal(copies + 1, game.State.CardObjects["JINX"].UntilEndOfTurnPowerModifiers.Count);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task SimultaneousDiscardByBothPlayersUsesOneBatchPerController()
    {
        var s = Position(); s = Put(s, "REWIND", "OGN·201/298", "P1", "HAND");
        s = Put(s, "SECOND", "OGN·202/298", "P2", "BASE"); s = Put(s, "P2-CARD", "VEN·049", "P2", "HAND");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["SECOND"] = s.CardObjects["SECOND"] with { IsExhausted = true } } };
        var game = new Driver(s); await game.Act("P1", new PlayCardCommand("REWIND", "OGN·201/298", []));
        await game.Top();
        var queued = game.State.TriggerQueue;
        Assert.Equal(2, queued.Count); Assert.All(queued, t => Assert.NotNull(t.HandDiscardObserver));
        var ordering = ResolutionResult.BuildPrompts(game.State).Single(p => p.Value.Actionable && p.Value.Actions.Contains(CommandTypes.OrderTriggers));
        var metadata = JsonSerializer.SerializeToElement(ordering.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == CommandTypes.OrderTriggers && c.GetProperty("enabled").GetBoolean())
            .GetProperty("metadata");
        await game.Act(ordering.Key, new OrderTriggersCommand(OrderedTriggerIds: metadata.GetProperty("orderedTriggerIds").EnumerateArray().Select(v => v.GetString()!).ToArray()));
        var triggers = game.State.StackItems.Where(i => i.HandDiscardObserver is not null).ToArray();
        Assert.Equal(2, triggers.Length);
        Assert.True(game.State.CardObjects["JINX"].IsExhausted); Assert.True(game.State.CardObjects["SECOND"].IsExhausted);
        Assert.Equal(3, triggers.Single(i => i.ControllerId == "P1").HandDiscardObserver!.DiscardedCards.Count);
        Assert.Single(triggers.Single(i => i.ControllerId == "P2").HandDiscardObserver!.DiscardedCards);
        await game.Top(); await game.Top();
        Assert.Single(game.State.CardObjects["JINX"].UntilEndOfTurnPowerModifiers);
        Assert.Single(game.State.CardObjects["SECOND"].UntilEndOfTurnPowerModifiers);
        await game.Replay();
    }

    [Theory]
    [InlineData("context")] [InlineData("controller")] [InlineData("source-generation")]
    [InlineData("instance")] [InlineData("card-no")] [InlineData("future-tick")]
    [InlineData("discard-generation")] [InlineData("empty-batch")]
    public async Task IndependentRecoveryRejectsForgedCapturedFacts(string mutation)
    {
        var game = new Driver(Position()); await game.Act("P1", DiscardCost());
        var state = game.State; var c = state.StackItems.Single(i => i.HandDiscardObserver is not null).HandDiscardObserver!;
        var forged = state with { StackItems = state.StackItems.Select(i => i.HandDiscardObserver is null ? i : i with {
            HandDiscardObserver = mutation switch {
                "context" => null, "controller" => c with { ControllerId = "P2" },
                "source-generation" => c with { Source = c.Source with { Generation = 99 } },
                "instance" => c with { InstanceId = "PRINTED:UNKNOWN:0" },
                "card-no" => c with { CardNo = "OGN·001/298" }, "future-tick" => c with { CapturedTick = state.Tick + 1 },
                "discard-generation" => c with { DiscardedCards = [c.DiscardedCards[0] with { Generation = 99 }] },
                _ => c with { DiscardedCards = [] } } }).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("hand discard observer"));
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(game.State));
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")] [InlineData("spectator")]
    public async Task RecoveryRejectsAViewThatChangesTheCapturedDiscardController(string viewer)
    {
        var game = new Driver(Position()); await game.Act("P1", DiscardCost()); var s = game.State;
        var snapshots = ResolutionResult.BuildSnapshots(s); var prompts = ResolutionResult.BuildPrompts(s);
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, s.Tick, 0, snapshots[id], s.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, 0, [], s);
        var snapshot = viewer == "spectator" ? spectator.SpectatorSnapshot : snapshots[viewer];
        var row = snapshot.Stack.OfType<Dictionary<string, object?>>().Single(r => r.ContainsKey("handDiscardObserver"));
        var original = Assert.IsType<HandDiscardObserverView>(row["handDiscardObserver"]);
        row["handDiscardObserver"] = original with { ControllerId = "P2" };
        Assert.Contains(MatchRecoveryValidator.ValidateComponentStateAndViews(s, views, spectator), e => e.Contains("hand discard observer"));
    }
}
