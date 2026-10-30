using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedDeathObserverTests
{
    [Fact]
    public async Task QueueAfterSourceDepartureHidesTheCurrentPrivateObjectAndKeepsPublicDeathFacts()
    {
        var game = await OpenWatcher("UNL-129/219", 1); await Kill(game); var earned = game.State;
        // Explicit component continuation, separate from the original journal.
        var s = earned with {
            CardObjects = new Dictionary<string, CardObjectState>(earned.CardObjects) {
                ["HOST"] = earned.CardObjects["HOST"] with { ObjectGeneration = 1 },
                ["GEAR0"] = earned.CardObjects["GEAR0"] with { AttachedToObjectId = null, AttachedRuleText = null } },
            PlayerZones = new Dictionary<string, PlayerZones>(earned.PlayerZones) { ["P2"] = earned.PlayerZones["P2"] with {
                Base = earned.PlayerZones["P2"].Base.Where(id => id != "HOST").ToArray(), Hand = [..earned.PlayerZones["P2"].Hand, "HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(earned.ObjectLocations) { ["HOST"] = new("P2", "HAND") } };
        s = OfficialInsightAndSpellLockTests.Restore(s);
        var snapshots = ResolutionResult.BuildSnapshots(s);
        foreach (var snapshot in new[] { snapshots["P1"], MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, 0, [], s).SpectatorSnapshot })
        {
            var rows = Assert.IsAssignableFrom<IEnumerable<object>>(snapshot.Timing!["triggerQueue"]);
            Assert.All(rows, value => {
                var row = Assert.IsType<Dictionary<string, object?>>(value);
                Assert.Equal("HIDDEN", row["sourceObjectId"]); Assert.Equal("HIDDEN", row["sourceVisibility"]);
                var fact = Assert.IsType<DeathObserverContext>(row["deathObserver"]);
                Assert.Null(fact.Origin); Assert.Null(fact.RuleTextInstanceId); Assert.Equal("D", fact.Destroyed.ObjectId);
            });
        }
        var done = await OfficialGraveyardRecastTests.Act(s, "P2", new OrderTriggersCommand(
            OrderedTriggerIds: s.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        done = await OfficialGraveyardRecastTests.Top((await OfficialGraveyardRecastTests.Top(done.State)).State);
        Assert.Equal(2, done.State.PlayerExperience["P2"]); OfficialInsightAndSpellLockTests.Restore(done.State);
    }

    [Fact]
    public async Task NativePhysicalIdCannotBeMistakenForACopiedTextMarker()
    {
        const string id = "HOST:ATTACHED:legacy";
        var s = Position("UNL-129/219", 0); var cards = s.CardObjects.ToDictionary();
        cards[id] = cards["HOST"] with { ObjectId = id }; cards.Remove("HOST");
        var locations = s.ObjectLocations.ToDictionary(); locations[id] = locations["HOST"]; locations.Remove("HOST");
        s = s with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with {
                Base = s.PlayerZones["P2"].Base.Select(x => x == "HOST" ? id : x).ToArray() } } };
        var game = new Driver(s); await Kill(game);
        var item = Assert.Single(game.State.StackItems); Assert.Equal(id, item.SourceObjectId);
        Assert.Null(item.DeathObserver!.Origin); await Finish(game); Assert.Equal(1, game.State.PlayerExperience["P2"]);
    }

    [Fact]
    public async Task ConfirmedResourceDeathKeepsBothFirstDeathInstancesAtItsOwnAnswerTick()
    {
        var s = DeathObserverAuditTests.Source(OfficialSettReplacementTests.Position(power: 0), "OGN·118/298");
        s = ReplacementResourceTests.AddResource(s, P4ActivatedAbilityCatalog.GoldTokenUnlCardNo);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["RESOURCE"] = s.CardObjects["RESOURCE"] with { Power = 5,
                Tags = s.CardObjects["RESOURCE"].Tags.Append(CardObjectTags.UnitCard).Order(StringComparer.Ordinal).ToArray() },
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·059/221", "P2") with {
                AttachedToObjectId = "OBSERVER", AttachmentRevision = 1,
                AttachedRuleText = new(new("OBSERVER", 0), "OGN·118/298", 1, 0, 0) } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["GEAR"] = new("P2", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Base = [..s.PlayerZones["P2"].Base, "GEAR"] } } };
        var resource = await ReplacementResourceTests.Resource((await OfficialSettReplacementTests.Open(s)).State);
        var earned = resource.State.TriggerQueue.Where(t => t.SourceObjectId == "OBSERVER").ToArray();
        Assert.Equal(2, earned.Length); Assert.Single(earned, t => t.DeathObserver!.Origin is not null);
        Assert.All(earned, t => { Assert.Equal(resource.State.Tick, t.EnqueuedAtTick); Assert.Equal("RESOURCE", t.DeathObserver!.Destroyed.ObjectId); });
        var restored = OfficialInsightAndSpellLockTests.Restore(resource.State);
        var completed = await OfficialSettReplacementTests.Choose(restored, "DECLINE");
        Assert.Equal(2, completed.State.DeathLedger.Counts["P2"]);
        Assert.DoesNotContain(completed.Events, e => e.Kind == "TRIGGER_QUEUED"
            && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "OBSERVER"));
        Assert.Equal(earned.Select(MatchStateHasher.HashValue), completed.State.TriggerQueue
            .Where(t => t.SourceObjectId == "OBSERVER").Select(MatchStateHasher.HashValue));
        OfficialInsightAndSpellLockTests.Restore(completed.State);
    }

    [Theory]
    [InlineData("UNL-068/219")] [InlineData("UNL-129/219")] [InlineData("ARC-006/006")]
    public async Task EachRealSimultaneousDeathEarnsEveryIndependentTextInstance(string no)
    {
        var game = await OpenWatcher(no, 2, true); await Kill(game);
        Assert.Equal(2, game.State.DeathLedger.Counts["P2"]);
        Assert.Equal(6, game.State.TriggerQueue.Count);
        Assert.Equal(6, game.State.TriggerQueue.Select(t => t.TriggerId).Distinct().Count());
        Assert.Equal(3, game.State.TriggerQueue.Count(t => t.DeathObserver!.Destroyed.ObjectId == "D"));
        Assert.Equal(3, game.State.TriggerQueue.Count(t => t.DeathObserver!.Destroyed.ObjectId == "D2"));
        Assert.Equal(4, game.State.TriggerQueue.Count(t => t.DeathObserver!.Origin is not null));
        await Finish(game);
        if (no == "UNL-129/219") Assert.Equal(6, game.State.PlayerExperience["P2"]);
        if (no == "UNL-068/219") Assert.Equal(12, game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
        if (no == "ARC-006/006") Assert.Equal(6, game.Events().Count(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
        await game.Replay();
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task FirstSimultaneousDeathOccurrenceIsChosenIndependentlyForEachText(int copies)
    {
        var game = await OpenWatcher("OGN·118/298", copies, true); await Kill(game);
        var hand = game.State.PlayerZones["P2"].Hand.Count;
        for (var i = 0; i <= copies; i++)
        {
            var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            Assert.Equal("FIRST_DEATH_OCCURRENCE", pending.Request.Kind);
            Assert.Equal(2, pending.Request.Options.Count);
            Assert.Equal(2, pending.Request.Options.Select(o => o.Id).Distinct().Count());
            var victim = i % 2 == 0 ? "D" : "D2";
            var choice = pending.Request.Options.Single(o => o.ObjectIds!.Contains(victim));
            await game.Act("P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [choice.Id]));
        }
        Assert.Null(game.State.PendingRuleChoice); Assert.Equal(2, game.State.DeathLedger.Counts["P2"]);
        Assert.Equal(copies + 1, game.State.TriggerQueue.Count);
        var native = Assert.Single(game.State.TriggerQueue, t => t.DeathObserver!.Origin is null);
        Assert.Equal("D", native.DeathObserver!.Destroyed.ObjectId);
        var copied = game.State.TriggerQueue.Where(t => t.DeathObserver!.Origin is not null)
            .OrderBy(t => t.DeathObserver!.Origin!.Equipment.ObjectId).ToArray();
        for (var i = 0; i < copies; i++) Assert.Equal(i % 2 == 0 ? "D2" : "D", copied[i].DeathObserver!.Destroyed.ObjectId);
        await Finish(game); Assert.Equal(hand + copies + 1, game.State.PlayerZones["P2"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task RealDetachKeepsTheEarnedCopyButPreventsAnotherCopyOnTheNextDeath()
    {
        var initial = Position("UNL-129/219", 1, true);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["D2"] = initial.CardObjects["D2"] with { Power = 3, Damage = 0 } } };
        initial = HandDiscardObserverTests.Put(initial, "DETACH", "SFD·011/221", "P1", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "AOE2", "OGN·133/298", "P1", "HAND");
        var game = new Driver(initial); await Assemble(game, 1); await Kill(game);
        Assert.Contains("D2", game.State.PlayerZones["P2"].Battlefields);
        var earned = Assert.Single(game.State.TriggerQueue, t => t.DeathObserver!.Origin is not null).DeathObserver!;
        await Order(game); await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new TapRuneCommand("PAY1", ["PAY1", "PAY2"]));
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR0"]));
        await game.Top();
        Assert.Null(game.State.CardObjects["GEAR0"].AttachedToObjectId);
        Assert.Null(game.State.CardObjects["GEAR0"].AttachedRuleText);
        Assert.Equal(MatchStateHasher.HashValue(earned), MatchStateHasher.HashValue(
            Assert.Single(game.State.StackItems, t => t.DeathObserver?.Origin is not null).DeathObserver));
        await Finish(game); Assert.Equal(2, game.State.PlayerExperience["P2"]);
        await game.Act("P1", new TapRuneCommand("PAY3"));
        await game.Act("P1", new PlayCardCommand("AOE2", "OGN·133/298", [])); await game.Top();
        var second = Assert.Single(game.State.StackItems); Assert.NotNull(second.DeathObserver);
        Assert.Null(second.DeathObserver.Origin); Assert.Equal("D2", second.DeathObserver.Destroyed.ObjectId);
        await Finish(game); Assert.Equal(3, game.State.PlayerExperience["P2"]); await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("equipment")] [InlineData("equipment-generation")]
    [InlineData("unit")] [InlineData("source-generation")] [InlineData("revision")]
    [InlineData("future-time")] [InlineData("face-revision")]
    public async Task IndependentRecoveryRejectsForgedCopiedDeathBindings(string change)
    {
        var game = await OpenWatcher("UNL-129/219", 1); await Kill(game);
        var state = game.State; var original = MatchStateHasher.Hash(state);
        var c = state.TriggerQueue.Single(t => t.DeathObserver!.Origin is not null).DeathObserver!;
        var origin = c.Origin!;
        var changed = change switch {
            "equipment" => origin with { Equipment = origin.Equipment with { ObjectId = "PAY0" } },
            "equipment-generation" => origin with { Equipment = origin.Equipment with { Generation = 99 } },
            "unit" => origin with { Unit = origin.Unit with { ObjectId = "D" } },
            "source-generation" => origin with { Unit = origin.Unit with { Generation = 99 } },
            "revision" => origin with { Revision = 99 }, "future-time" => origin with { CapturedTick = state.Tick + 1 },
            "face-revision" => origin with { FaceRevision = 99 }, _ => origin };
        var forgedContext = change == "missing" ? c with { Origin = null, RuleTextInstanceId = null }
            : c with { Origin = changed, SourceGeneration = change == "source-generation" ? 99 : c.SourceGeneration,
                RuleTextInstanceId = $"ATTACHED:{changed.Equipment.ObjectId}:{changed.Equipment.Generation}:{changed.Revision}" };
        var forged = state with { TriggerQueue = state.TriggerQueue.Select(t => t.DeathObserver == c
            ? t with { DeathObserver = forgedContext } : t).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("captured death observer"));
        Assert.Equal(original, MatchStateHasher.Hash(game.State));
    }

    [Fact]
    public async Task PublicQueueDoesNotPublishThePrivateGrantBinding()
    {
        var game = await OpenWatcher("UNL-129/219", 1); await Kill(game); var s = game.State;
        var snapshots = ResolutionResult.BuildSnapshots(s);
        foreach (var snapshot in snapshots.Values.Append(MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, 0, [], s).SpectatorSnapshot))
        {
            var json = JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            foreach (var trigger in json.GetProperty("timing").GetProperty("triggerQueue").EnumerateArray())
            {
                var context = trigger.GetProperty("deathObserver");
                Assert.False(context.TryGetProperty("origin", out _));
                Assert.False(context.TryGetProperty("ruleTextInstanceId", out _));
                Assert.Equal("D", context.GetProperty("destroyed").GetProperty("objectId").GetString());
            }
        }
        Assert.NotNull(Assert.Single(s.TriggerQueue, t => t.DeathObserver!.Origin is not null).DeathObserver!.Origin);
    }

    private static async Task<Driver> OpenWatcher(string no, int copies, bool two = false)
    { var game = new Driver(Position(no, copies, two)); await Assemble(game, copies); return game; }
    private static async Task Assemble(Driver game, int copies)
    {
        for (var i = 0; i < copies; i++)
        { await game.Act("P2", new AssembleEquipmentCommand("GEAR" + i, "HOST", ["ASSEMBLE_GREEN"])); await game.Top(); }
    }
    private static async Task Kill(Driver game)
    {
        await game.Act("P2", new EndTurnCommand()); await game.Act("P1", new TapRuneCommand("PAY0"));
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", [])); await game.Top();
    }
    private static async Task Order(Driver game)
    {
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }
    private static async Task Finish(Driver game)
    {
        await Order(game); while (game.State.StackItems.Count > 0) await game.Top();
    }
}
