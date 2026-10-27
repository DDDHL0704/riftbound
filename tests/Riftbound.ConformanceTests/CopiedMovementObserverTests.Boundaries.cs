using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedMovementObserverTests
{
    [Fact]
    public async Task RealDetachKeepsEarnedExperienceAndTheNextActualMoveUsesOnlyNativeText()
    {
        var initial = Position("UNL-115/219", 1);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Power = 2 } } };
        initial = HandDiscardObserverTests.Put(initial, "DETACH", "SFD·011/221", "P2", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "MOVE", "OGS·011/024", "P2", "HAND");
        var game = new Driver(initial);
        try
        {
            await Assemble(game, 1); await MoveAndOrder(game);
            var earned = game.State.StackItems.Select(t => MatchStateHasher.HashValue(t.MovementObserver)).ToArray();
            await game.Act("P2", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR0"])); await game.Top();
            Assert.Null(game.State.CardObjects["GEAR0"].AttachedRuleText);
            Assert.Equal(earned, game.State.StackItems.Select(t => MatchStateHasher.HashValue(t.MovementObserver)));
            await Finish(game); Assert.Equal(2, game.State.PlayerExperience["P2"]);
            await game.Act("P2", new PlayCardCommand("MOVE", "OGS·011/024", ["HOST"])); await game.Top();
            var next = Assert.Single(game.State.StackItems); Assert.Null(next.MovementObserver!.Origin);
            Assert.Equal(2, game.State.CardObjects["HOST"].MovementCountThisTurn);
            await Finish(game); Assert.Equal(3, game.State.PlayerExperience["P2"]); await game.Replay();
        }
        finally { Save(game, initial, nameof(RealDetachKeepsEarnedExperienceAndTheNextActualMoveUsesOnlyNativeText)); }
    }

    [Theory]
    [InlineData("SFD·048/221")] [InlineData("UNL-115/219")]
    [InlineData("UNL-080/219")] [InlineData("OGN·222/298")] [InlineData("UNL-082/219")]
    public async Task ActualReturnPreservesBothEarnedInstancesAndHidesThePrivateCurrentSource(string no)
    {
        var initial = Position(no, 1);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Power = 2 } } };
        initial = HandDiscardObserverTests.Put(initial, "RETURN", "OGN·169/298", "P2", "HAND");
        if (no == "UNL-080/219")
            initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key,
                p => p.Key.StartsWith("FOOD") ? OfficialGarenConquestBattlefieldCountTests.Printed(p.Key, "OGN·096/298", "P2") : p.Value) };
        var game = new Driver(initial);
        try
        {
            await Assemble(game, 1); await MoveAndOrder(game);
            var earned = game.State.StackItems.Select(t => MatchStateHasher.HashValue(t.MovementObserver)).ToArray();
            await game.Act("P2", new PlayCardCommand("RETURN", "OGN·169/298", ["HOST"])); await game.Top();
            Assert.Contains("HOST", game.State.PlayerZones["P2"].Hand); Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > 0);
            Assert.Equal(earned, game.State.StackItems.Select(t => MatchStateHasher.HashValue(t.MovementObserver)));
            var snapshots = ResolutionResult.BuildSnapshots(game.State);
            foreach (var snapshot in new[] { snapshots["P1"], MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State).SpectatorSnapshot })
                Assert.All(snapshot.Stack, value => {
                    var row = Assert.IsType<Dictionary<string, object?>>(value);
                    Assert.Equal("HIDDEN", row["sourceObjectId"]); Assert.Equal(no, row["cardNo"]);
                });
            var source = MatchStateHasher.HashValue(game.State.CardObjects["HOST"]);
            var hand = game.State.PlayerZones["P2"].Hand.Count;
            await Finish(game);
            Assert.Equal(source, MatchStateHasher.HashValue(game.State.CardObjects["HOST"]));
            if (no == "SFD·048/221") Assert.Equal(hand + 2, game.State.PlayerZones["P2"].Hand.Count);
            if (no == "UNL-115/219") Assert.Equal(2, game.State.PlayerExperience["P2"]);
            if (no == "OGN·222/298") Assert.DoesNotContain(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null);
            if (no == "UNL-082/219")
            {
                var tokens = game.State.CardObjects.Values.Where(c => c.TokenFactoryCardNo is not null).ToArray();
                Assert.Equal(2, tokens.Length); Assert.All(tokens, token => {
                    Assert.Contains(token.ObjectId, game.State.PlayerZones["P2"].Base); Assert.Equal(3, token.Power);
                });
            }
            await game.Replay();
        }
        finally { Save(game, initial, "private-return-" + no.Replace('/', '_')); }
    }

    [Theory]
    [InlineData("missing")] [InlineData("equipment")] [InlineData("equipment-generation")]
    [InlineData("unit")] [InlineData("revision")] [InlineData("future-time")] [InlineData("face-revision")] [InlineData("source-generation")]
    public async Task ComponentRecoveryRejectsForgedMovementGrantBindings(string change)
    {
        var game = new Driver(Position("UNL-115/219", 1)); await Assemble(game, 1);
        await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []));
        var state = game.State; var copy = state.TriggerQueue.Single(t => t.MovementObserver!.Origin is not null);
        var c = copy.MovementObserver!; var origin = c.Origin!;
        var changed = change switch {
            "equipment" => origin with { Equipment = origin.Equipment with { ObjectId = "MOVER0" } },
            "equipment-generation" => origin with { Equipment = origin.Equipment with { Generation = 99 } },
            "unit" => origin with { Unit = origin.Unit with { ObjectId = "MOVER0" } },
            "source-generation" => origin with { Unit = origin.Unit with { Generation = 99 } },
            "revision" => origin with { Revision = 99 }, "future-time" => origin with { CapturedTick = state.Tick + 1 },
            "face-revision" => origin with { FaceRevision = 99 }, _ => origin };
        var context = change == "missing" ? c with { Origin = null, RuleTextInstanceId = null }
            : c with { Origin = changed, SourceGeneration = change == "source-generation" ? 99 : c.SourceGeneration,
                Unit = change == "source-generation" ? c.Unit with { Generation = 99 } : c.Unit, RuleTextInstanceId = $"ATTACHED:{changed.Equipment.ObjectId}:{changed.Equipment.Generation}:{changed.Revision}" };
        var forged = state with { TriggerQueue = state.TriggerQueue.Select(t => t == copy ? t with { MovementObserver = context } : t).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("captured movement observer"));
    }

    [Fact]
    public async Task CopiedPendingDiscardCannotLoseItsCapturedGrantIdentity()
    {
        var game = new Driver(Position("UNL-080/219", 1)); await Assemble(game, 1);
        await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []));
        await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        await game.Top(); var s = game.State; var pending = s.PendingHandChoice!;
        Assert.NotNull(pending.MovementObserver!.Origin);
        foreach (var changed in new[] {
            pending with { MovementObserver = pending.MovementObserver with { Origin = null, RuleTextInstanceId = null } },
            pending with { ChoiceId = "CHOICE-ordered-movement-forged-native" }
        })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with { PendingHandChoice = changed }), e => e.Contains("movement discard"));
    }

    [Fact]
    public async Task MalformedPublicMovementFactReturnsErrorsInsteadOfThrowing()
    {
        var game = new Driver(Position("UNL-115/219", 1)); await Assemble(game, 1);
        await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", [])); var s = game.State;
        var snapshots = ResolutionResult.BuildSnapshots(s); var prompts = ResolutionResult.BuildPrompts(s);
        var rows = Assert.IsAssignableFrom<IEnumerable<object>>(snapshots["P1"].Timing!["triggerQueue"]);
        var row = Assert.IsType<Dictionary<string, object?>>(rows.First());
        row["movementObserver"] = Assert.IsType<MovementObserverContext>(row["movementObserver"]) with { Unit = null! };
        row["sourceObjectId"] = "HIDDEN"; row["sourceVisibility"] = "HIDDEN";
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, s.Tick, 0, snapshots[id], s.Tick, 0, prompts[id]));
        Assert.NotEmpty(MatchRecoveryValidator.Validate(s.RoomId, 0, [], [], views, s, s.Tick));
    }

    [Fact]
    public async Task PublicGrantIsPrivateAndForgedPublicMovementFactCannotDisagreeWithAuthority()
    {
        var game = new Driver(Position("UNL-115/219", 1)); await Assemble(game, 1);
        await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", [])); var s = game.State;
        var snapshots = ResolutionResult.BuildSnapshots(s);
        foreach (var snapshot in snapshots.Values.Append(MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, 0, [], s).SpectatorSnapshot))
        {
            var json = JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.Equal(2, json.GetProperty("timing").GetProperty("triggerQueue").GetArrayLength());
            foreach (var row in json.GetProperty("timing").GetProperty("triggerQueue").EnumerateArray())
            {
                var context = row.GetProperty("movementObserver");
                Assert.False(context.TryGetProperty("origin", out _)); Assert.False(context.TryGetProperty("ruleTextInstanceId", out _));
            }
        }
        var queue = Assert.IsAssignableFrom<IEnumerable<object>>(snapshots["P1"].Timing!["triggerQueue"]);
        var first = Assert.IsType<Dictionary<string, object?>>(queue.First());
        var publicContext = Assert.IsType<MovementObserverContext>(first["movementObserver"]);
        first["movementObserver"] = publicContext with { SourceGeneration = 99, Unit = publicContext.Unit with { Generation = 99 } };
        var prompts = ResolutionResult.BuildPrompts(s);
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, s.Tick, 0, snapshots[id], s.Tick, 0, prompts[id]));
        Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId, 0, [], [], views, s, s.Tick),
            e => e.Contains("captured movement observer differs"));
    }

    private static async Task MoveAndOrder(Driver game)
    {
        await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []));
        await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count);
    }
}
