using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EmpoweredMovementTests
{
    private const string No = "VEN·057";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public void EmpowermentBelongsToItsOwnMovementClause()
    {
        var specs = TriggerParser.Parse("{{强化3}}\n{{已强化>}} 当我移动时，抽一张牌。\n当我移动时，抽两张牌。");
        var draw = specs.Where(s => s.Kind == TriggerKinds.UnitMovedDraw).ToArray();
        Assert.Equal(2, draw.Length);
        Assert.True(draw[0].RequiresSourceEmpowered); Assert.Equal(1, draw[0].DrawCount);
        Assert.Null(draw[1].RequiresSourceEmpowered); Assert.Equal(2, draw[1].DrawCount);
        Assert.DoesNotContain("requiresSourceEmpowered", JsonSerializer.Serialize(draw[1], Web));
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public async Task ActualRepeatedMovesCaptureEachCurrentlyActiveTextInstance(int copies, bool empowered)
    {
        var initial = HandDiscardObserverTests.Put(CopiedMovementObserverTests.Position(No, copies), "MOVE", "OGS·011/024", "P2", "HAND");
        var game = new Driver(initial);
        try {
            await CopiedMovementObserverTests.Assemble(game, copies);
            if (empowered) { await ActivateHost(game, "P2"); await game.Top(); }
            Assert.Equal(empowered, game.State.CardObjects["HOST"].IsEmpowered);
            var before = Draws(game, "P2");
            await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []));
            AssertCaptured(game, empowered ? copies + 1 : 0);
            await CopiedMovementObserverTests.Finish(game);
            Assert.Equal(before + (empowered ? copies + 1 : 0), Draws(game, "P2"));
            await game.Act("P2", new PlayCardCommand("MOVE", "OGS·011/024", ["HOST"])); await game.Top();
            AssertCaptured(game, empowered ? copies + 1 : 0);
            await CopiedMovementObserverTests.Finish(game);
            Assert.Equal(2, game.State.CardObjects["HOST"].MovementCountThisTurn);
            Assert.Equal(before + (empowered ? 2 * (copies + 1) : 0), Draws(game, "P2"));
            await game.Replay(); AssertJsonState(game.State);
        }
        finally { Save(game, initial, $"repeated-{copies}-{empowered}"); }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task ActuallyDetachedAndReturnedSourcesKeepEveryEarnedDraw(int copies)
    {
        var initial = HandDiscardObserverTests.Put(CopiedMovementObserverTests.Position(No, copies), "DETACH", "SFD·011/221", "P2", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "RETURN", "OGN·104/298", "P2", "HAND");
        var game = new Driver(initial);
        try {
            await CopiedMovementObserverTests.Assemble(game, copies);
            await ActivateHost(game, "P2"); await game.Top();
            await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []));
            await Order(game, "P2"); AssertCaptured(game, copies + 1);
            var earned = Contexts(game).Select(MatchStateHasher.HashValue).ToArray();
            await game.Act("P2", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR0"])); await game.Top();
            Assert.Null(game.State.CardObjects["GEAR0"].AttachedRuleText);
            Assert.Equal(earned, Contexts(game).Select(MatchStateHasher.HashValue));
            Assert.Equal(1, Draws(game, "P2")); // SFD011's own mandatory draw.
            var generation = game.State.CardObjects["HOST"].ObjectGeneration;
            await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
            Assert.Contains("HOST", game.State.PlayerZones["P2"].Hand);
            Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > generation);
            Assert.Equal(earned, Contexts(game).Select(MatchStateHasher.HashValue));
            foreach (var snapshot in new[] { ResolutionResult.BuildSnapshots(game.State)["P1"],
                MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State).SpectatorSnapshot })
                Assert.All(snapshot.Stack, row => Assert.Equal("HIDDEN", Assert.IsType<Dictionary<string, object?>>(row)["sourceObjectId"]));
            await CopiedMovementObserverTests.Finish(game);
            Assert.Equal(copies + 2, Draws(game, "P2"));
            await game.Replay(); AssertJsonState(game.State);
        }
        finally { Save(game, initial, $"earned-detached-returned-{copies}"); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealCopiedFaceUsesTheHostsStatusAndKeepsItsDrawAfterUncopying(bool empowerOriginal)
    {
        var initial = HandDiscardObserverTests.Put(ShadySpectaclesTests.Position(No), "DETACH", "SFD·011/221", "P1", "HAND");
        initial = initial with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1, [RuneTrait.Blue] = 2 }), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        try {
            if (empowerOriginal) { await game.Act("P1", new ActivateAbilityCommand("COPY-SOURCE", "EMPOWER:0:0", [])); await game.Top(); }
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await game.Top();
            var request = game.State.PendingRuleChoice!.Request;
            await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
            Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            await ActivateHost(game, "P1"); await game.Top();
            Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.Equal(empowerOriginal, game.State.CardObjects["COPY-SOURCE"].IsEmpowered);
            await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
            var captured = Assert.Single(Contexts(game)); Assert.True(captured.SourceWasEmpowered);
            Assert.Equal("GEAR", captured.Origin!.Equipment.ObjectId);
            var identity = MatchStateHasher.HashValue(captured);
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
            Assert.Equal("OGN·096/298", game.State.CardObjects["HOST"].CardNo);
            Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.Equal(identity, MatchStateHasher.HashValue(Assert.Single(Contexts(game))));
            Assert.Equal(1, Draws(game, "P1"));
            await CopiedMovementObserverTests.Finish(game);
            Assert.Equal(2, Draws(game, "P1"));
            await game.Replay(); AssertJsonState(game.State);
        }
        finally { Save(game, initial, $"copied-face-{empowerOriginal}"); }
    }

    [Theory]
    [InlineData("missing")] [InlineData("false")] [InlineData("ungated")]
    public async Task RecoveryRejectsMissingFalseOrUnrelatedEmpowermentFacts(string forgery)
    {
        var initial = CopiedMovementObserverTests.Position(forgery == "ungated" ? "SFD·048/221" : No, 0);
        var game = new Driver(initial);
        if (forgery != "ungated") { await ActivateHost(game, "P2"); await game.Top(); }
        await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []));
        var original = MatchStateHasher.Hash(game.State);
        MovementObserverContext Change(MovementObserverContext context)
            => context with { SourceWasEmpowered = forgery == "missing" ? null : forgery == "ungated" };
        var attack = game.State with {
            TriggerQueue = game.State.TriggerQueue.Select(t => t with { MovementObserver = Change(t.MovementObserver!) }).ToArray(),
            StackItems = game.State.StackItems.Select(t => t with { MovementObserver = Change(t.MovementObserver!) }).ToArray() };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(attack));
        Assert.Equal(original, MatchStateHasher.Hash(game.State));
        Save(game, initial, "forgery-" + forgery, attack);
    }

    [Fact]
    public async Task AnInternallyCoherentEmpoweredBirthStillNeedsItsOriginalAcceptedHistory()
    {
        var initial = CopiedMovementObserverTests.Position(No, 0);
        var game = new Driver(initial);
        await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []));
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered); Assert.Empty(Contexts(game));
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        var context = new MovementObserverContext(No, TriggerKinds.UnitMovedDraw, generation,
            new("HOST", generation, "P2", "P2", new("P2", "BASE"), new("P2", "BATTLEFIELD", "F2"))) {
                SourceWasEmpowered = true };
        // A coordinated hostile checkpoint, not a played disempower action.
        // Current status alone cannot disprove an already earned skill.
        var attack = game.State with { TriggerQueue = [new("movement-forged-birth", "P2", "HOST",
            "CAPTURED_UNIT_MOVEMENT_OBSERVER", "UNIT_MOVED_TO_BATTLEFIELD", TimingStates.NeutralOpen) {
                MovementObserver = context, SourceCardNo = No, EnqueuedAtTick = game.State.Tick }] };
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(attack));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), attack,
            new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch);
        Assert.Empty(Contexts(game));
        Save(game, initial, "coherent-unearned-birth", attack);
    }

    private static Task ActivateHost(Driver game, string player)
    {
        var row = ResolutionResult.BuildPrompts(game.State)[player].Candidates!.Where(c => c.Action == CommandTypes.ActivateAbility)
            .SelectMany(c => Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(c.Metadata!["sourceRequirements"]))
            .First(r => (string?)r["sourceObjectId"] == "HOST" && (string?)r["abilityId"] == "EMPOWER:0:0");
        return game.Act(player, new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []) {
            AbilityInstanceId = row.GetValueOrDefault("abilityInstanceId") as string });
    }
    private static async Task Order(Driver game, string player)
    {
        if (game.State.TriggerQueue.Count > 0)
            await game.Act(player, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }
    private static MovementObserverContext[] Contexts(Driver game) => game.State.TriggerQueue.Select(t => t.MovementObserver)
        .Concat(game.State.StackItems.Select(t => t.MovementObserver)).OfType<MovementObserverContext>().ToArray();
    private static void AssertCaptured(Driver game, int expected)
    {
        var contexts = Contexts(game); Assert.Equal(expected, contexts.Length);
        Assert.All(contexts, c => { Assert.Equal(No, c.CardNo); Assert.True(c.SourceWasEmpowered); });
        Assert.Equal(Math.Max(0, expected - 1), contexts.Count(c => c.Origin is not null));
        Assert.Equal(expected, game.State.TriggerQueue.Select(t => t.TriggerId).Concat(game.State.StackItems.Select(t => t.StackItemId)).Distinct().Count());
        AssertJsonState(game.State);
    }
    private static int Draws(Driver game, string player) => game.Events().Count(e => e.Event.Kind == "CARD_DRAWN"
        && e.Event.Payload.GetValueOrDefault("playerId") as string == player);
    private static void AssertJsonState(MatchState state) => Assert.Equal(MatchStateHasher.Hash(state),
        MatchStateHasher.Hash(OfficialInsightAndSpellLockTests.Restore(state)));
    private static void Save(Driver game, MatchState initial, string label, MatchState? attack = null)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN057_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "shared-" + label + ".json"), JsonSerializer.Serialize(new { initial,
            final = game.State, commands = game.Commands(), events = game.Events(), constructedSecurityAttack = attack,
            scope = "Explicit finite position with actual activation/assembly/movement/response commands, JSON cold equality and unchanged original replay. No direct state edit is claimed as played disempower." }, Web));
    }
}
