using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven051RobotFlowConsumerTests
{
    private const string No = "VEN·051";
    private const string Text = "打出一名3{{S}}的“机器人”。\n{{流转2蓝色}}（你可以选择支付此牌的流转费用，以此将其从你的废牌堆中打出。然后将其放逐。）";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactSourceRegistersOnlyRobotCreationAndItsIndependentFlowCost()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var card = Assert.Single(catalog.Cards, c => c.CardNo == No);
        Assert.Equal(37742, card.Id); Assert.Equal("迭代式设计", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(4, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Null(card.Power); Assert.Equal([RuneTrait.Blue], card.CardColorList);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.Equal(4, behavior.ManaCost); Assert.Equal(0, behavior.RequiredTargetCount); Assert.Equal(0, behavior.DrawCount);
        Assert.Equal(1, behavior.CreatedBaseUnitTokenCount); Assert.Equal(3, behavior.CreatedBaseUnitTokenPower);
        Assert.Equal("机器人", behavior.CreatedBaseUnitTokenName); Assert.False(behavior.CreatedBaseUnitTokenEntersReady);
        Assert.Equal(CardCostReductionConditionKinds.None, behavior.CostReductionConditionKind);
        Assert.False(behavior.CanPlayDuringPriority); Assert.False(behavior.CanPlayDuringSpellDuel);
        Assert.Equal(2, behavior.FlowCost!.Mana); Assert.Equal(1, behavior.FlowCost.Power); Assert.Equal([RuneTrait.Blue], behavior.FlowCost.Traits);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OrdinaryAndFlowPayBeforeResolutionAndCreateOneDormantRobotWithoutDrawing(bool flow)
    {
        var initial = Position(flow); var run = new Driver(initial); var command = new PlayCardCommand("SPELL", No, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(flow ? 2 : 4, quote.Cost!.Mana);
        Assert.Equal(flow ? 1 : 0, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Blue)); Assert.Equal(0, quote.Cost.GenericPower);
        await run.Act("P1", command);
        Assert.Empty(Robots(run.State)); Assert.Equal(flow, Assert.Single(run.State.StackItems).FlowPlay is not null);
        Assert.Equal(flow ? 18 : 16, run.State.RunePools["P1"].Mana); Assert.Equal(flow ? 19 : 20, run.State.RunePools["P1"].TotalPower);
        await Export("pending-" + flow, initial, run); await run.Top();
        var robot = Assert.Single(Robots(run.State)); AssertRobot(run.State, robot);
        Assert.Contains("SPELL", flow ? run.State.PlayerZones["P1"].Banished : run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, run.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(run.State.PlayerZones["P1"].Hand); Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "CARD_DRAWN");
        var created = Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_TOKEN_CREATED");
        Assert.Equal(robot.ObjectId, created.Event.Payload["tokenObjectId"]); Assert.Equal(3, created.Event.Payload["power"]);
        Assert.Equal("机器人", created.Event.Payload["tokenName"]); Assert.Equal("BASE", created.Event.Payload["destinationZone"]);
        await Export("resolved-" + flow, initial, run);
    }

    public static IEnumerable<object[]> IllegalPlays() => new[] { false, true }.SelectMany(flow => new[] {
        "mana", "actor", "target", "destination", "mode", "extra", "wrong-turn", "duel" }
        .Select(kind => new object[] { flow, kind }));

    [Theory]
    [MemberData(nameof(IllegalPlays))]
    public async Task IllegalCostTimingActorAndExtraChoicesLeaveTheWholeProjectionAndEventsUnchanged(bool flow, string kind)
    {
        var initial = Position(flow);
        if (kind == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(flow ? 1 : 3, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 20 }) } };
        if (kind == "wrong-turn") initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (kind == "duel") initial = initial with { TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P1" };
        var run = new Driver(initial);
        await Reject(run, kind == "actor" ? "P2" : "P1", new PlayCardCommand("SPELL", No, kind == "target" ? ["ALLY"] : [],
            Mode: kind == "mode" ? "FORGED" : "", OptionalCosts: kind == "extra" ? ["FORGED"] : [],
            Destination: kind == "destination" ? "BATTLEFIELD:ABSENT" : ""));
        Assert.Empty(Robots(run.State)); await Export($"reject-{flow}-{kind}", initial, run);
    }

    [Theory]
    [InlineData("red")] [InlineData("none")]
    public async Task FlowRequiresThePrintedBlueTraitRatherThanAnotherTraitOrMissingPower(string kind)
    {
        var initial = Position(true) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(20, 0, kind == "red" ? new Dictionary<string, int> { [RuneTrait.Red] = 20 } : null), ["P2"] = new(20, 20) } };
        var run = new Driver(initial); await Reject(run, "P1", new PlayCardCommand("SPELL", No, []));
        await Export("reject-trait-" + kind, initial, run);
    }

    [Theory]
    [InlineData("BANISHED")] [InlineData("MAIN_DECK")] [InlineData("OPPONENT_GRAVEYARD")]
    public async Task FlowDoesNotAuthorizeAnUnavailableSource(string zone)
    {
        var initial = Position(true); var owner = zone == "OPPONENT_GRAVEYARD" ? "P2" : "P1";
        var sourceZone = zone == "OPPONENT_GRAVEYARD" ? "GRAVEYARD" : zone;
        var zones = initial.PlayerZones.ToDictionary(); zones["P1"] = zones["P1"] with { Graveyard = zones["P1"].Graveyard.Where(id => id != "SPELL").ToArray() };
        zones[owner] = sourceZone switch {
            "BANISHED" => zones[owner] with { Banished = ["SPELL"] },
            "MAIN_DECK" => zones[owner] with { MainDeck = [..zones[owner].MainDeck, "SPELL"] },
            _ => zones[owner] with { Graveyard = [..zones[owner].Graveyard, "SPELL"] } };
        initial = initial with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["SPELL"] = initial.CardObjects["SPELL"] with { OwnerId = owner, ControllerId = owner } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SPELL"] = new(owner, sourceZone) } };
        var run = new Driver(initial); await Reject(run, "P1", new PlayCardCommand("SPELL", No, []));
        await Export("reject-zone-" + zone, initial, run);
    }

    [Fact]
    public async Task TheSamePhysicalSpellCanBePlayedThenFlowedOnceWithoutReplayingTheFirstToken()
    {
        var initial = Position(false); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, [])); await run.Top(); var first = Assert.Single(Robots(run.State));
        await run.Act("P1", new PlayCardCommand("SPELL", No, [])); await run.Top();
        Assert.Equal(2, Robots(run.State).Length); Assert.Equal(2, Robots(run.State).Select(c => c.ObjectId).Distinct().Count());
        Assert.Equal(first, run.State.CardObjects[first.ObjectId]); Assert.Equal(14, run.State.RunePools["P1"].Mana);
        Assert.Equal(19, run.State.RunePools["P1"].TotalPower); Assert.Contains("SPELL", run.State.PlayerZones["P1"].Banished);
        await Reject(run, "P1", new PlayCardCommand("SPELL", No, [])); await Export("ordinary-then-flow-then-rejected", initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CounteringCreatesNoRobotButKeepsPaymentAndThePhysicalFlowExit(bool flow)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(flow), "COUNTER", "OGN·045/298", "P2"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, [])); var stackId = Assert.Single(run.State.StackItems).StackItemId;
        await run.Act("P1", new PassPriorityCommand()); await run.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [stackId])); await run.Top();
        Assert.Empty(Robots(run.State)); Assert.Empty(run.State.StackItems);
        Assert.Contains("SPELL", flow ? run.State.PlayerZones["P1"].Banished : run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(flow ? 18 : 16, run.State.RunePools["P1"].Mana); Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "UNIT_TOKEN_CREATED");
        await Export("countered-" + flow, initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnExistingEchoGrantCreatesIndependentRobotsButExitsOnePhysicalSpell(bool flow)
    {
        var initial = Position(flow); initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1") };
        var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("SPELL", No, [], OptionalCosts: ["ECHO"]));
        await Export("echo-pending-" + flow, initial, run); await run.Top();
        Assert.Equal(2, Robots(run.State).Length); Assert.All(Robots(run.State), c => AssertRobot(run.State, c));
        Assert.Equal(2, run.Events().Count(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
        Assert.Equal(flow ? 14 : 12, run.State.RunePools["P1"].Mana);
        Assert.Single(flow ? run.State.PlayerZones["P1"].Banished : run.State.PlayerZones["P1"].Graveyard, id => id == "SPELL");
        await Export("echo-resolved-" + flow, initial, run);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task ExactResourcesAreEnoughAndAnExistingMechanicalUnitDoesNotDiscountThisSpell(bool flow, bool mechanical)
    {
        var initial = Position(flow);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(flow ? 2 : 4, 0, flow ? new Dictionary<string, int> { [RuneTrait.Blue] = 1 } : null) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ALLY"] = mechanical ? OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·088/298", "P1") : initial.CardObjects["ALLY"] } };
        if (mechanical) Assert.Contains("机械", initial.CardObjects["ALLY"].Tags);
        var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("SPELL", No, [])); await run.Top();
        Assert.Equal(0, run.State.RunePools["P1"].Mana); Assert.Equal(0, run.State.RunePools["P1"].TotalPower);
        AssertRobot(run.State, Assert.Single(Robots(run.State))); await Export($"exact-cost-{flow}-{mechanical}", initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnEmptyMainDeckDoesNotDrawRecycleOrBurnOutWhileCreatingTheRobot(bool flow)
    {
        var initial = Position(flow); var deck = initial.PlayerZones["P1"].MainDeck;
        initial = initial with { PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1"
            ? p.Value with { MainDeck = [], Graveyard = [..p.Value.Graveyard, ..deck] } : p.Value),
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => deck.Contains(p.Key) ? new ObjectLocationState("P1", "GRAVEYARD") : p.Value) };
        var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("SPELL", No, [])); await run.Top();
        Assert.Empty(run.State.PlayerZones["P1"].MainDeck); Assert.Empty(run.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind is "CARD_DRAWN" or "BURNOUT_APPLIED");
        Assert.Equal(initial.PlayerScores, run.State.PlayerScores); AssertRobot(run.State, Assert.Single(Robots(run.State)));
        await Export("empty-deck-" + flow, initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealPendingSpellDoesNotMakeThisOrdinarySpellAReaction(bool flow)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(flow), "OPEN", "OGN·048/298", "P1"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("OPEN", "OGN·048/298", []));
        await Reject(run, "P1", new PlayCardCommand("SPELL", No, [])); await run.Top();
        Assert.Empty(Robots(run.State)); Assert.Contains("SPELL", flow ? run.State.PlayerZones["P1"].Graveyard : run.State.PlayerZones["P1"].Hand);
        await Export("actual-response-rejection-" + flow, initial, run);
    }

    [Theory]
    [InlineData("power")] [InlineData("owner")] [InlineData("factory")]
    public async Task OriginalActionLogRejectsAChangedTokenEvenWithARecomputedFinalStateHash(string kind)
    {
        var initial = Position(true); var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("SPELL", No, [])); await run.Top();
        await Export("original-for-forgery-" + kind, initial, run); var token = Assert.Single(Robots(run.State));
        var forgedToken = kind switch { "power" => token with { Power = 4 }, "owner" => token with { OwnerId = "P2" },
            _ => token with { TokenFactoryCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo } };
        var forged = run.State with { CardObjects = new Dictionary<string, CardObjectState>(run.State.CardObjects) { [token.ObjectId] = forgedToken } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), forged, new CoreRuleEngine(), default, run.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN051_EVIDENCE") is { Length: > 0 } output)
            File.WriteAllText(Path.Combine(output, "forgery-rejected-" + kind + ".json"), JsonSerializer.Serialize(new { forged, recomputedHash = MatchStateHasher.Hash(forged), replay.IsMatch, replay.Errors }, Json));
    }

    private static MatchState Position(bool flow)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner); locations[id] = new(owner, zone); }
        Put("SPELL", No, "P1", flow ? "GRAVEYARD" : "HAND");
        Put("ALLY", "OGN·219/298", "P1", "BASE"); Put("ENEMY", "OGN·219/298", "P2", "BASE");
        Put("GEAR", "SFD·022/221", "P1", "BASE"); Put("GRAVE", "OGN·219/298", "P1", "GRAVEYARD");
        cards["ALLY"] = cards["ALLY"] with { IsExhausted = true };
        cards["ENEMY"] = cards["ENEMY"] with { IsExhausted = true };
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 3).Select(i => player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { Base = player == "P1" ? ["ALLY", "GEAR"] : ["ENEMY"], MainDeck = deck,
                Hand = player == "P1" && !flow ? ["SPELL"] : [],
                Graveyard = player == "P1" ? flow ? ["GRAVE", "SPELL"] : ["GRAVE"] : [] };
        }
        return new MatchState("VEN051-ROBOT-FLOW", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 20 }),
                ["P2"] = new(20, 20) }, seed: 424242) with { CardPlays = new(5, [], []) };
    }

    private static CardObjectState[] Robots(MatchState state) => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo == "SFD·T01").ToArray();
    private static void AssertRobot(MatchState state, CardObjectState robot)
    {
        Assert.Equal(3, robot.Power); Assert.Equal(0, robot.Damage); Assert.True(robot.IsExhausted);
        Assert.Equal("P1", robot.OwnerId); Assert.Equal("P1", robot.ControllerId); Assert.Equal("SFD·T01", robot.CardNo);
        Assert.Contains(CardObjectTags.UnitCard, robot.Tags); Assert.Contains("机械", robot.Tags);
        Assert.Contains(robot.ObjectId, state.PlayerZones["P1"].Base); Assert.Equal("BASE", state.ObjectLocations[robot.ObjectId].Zone);
    }

    private static async Task Reject(Driver run, string player, GameCommand command)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(run.State)[player].PromptId; raw["snapshotTick"] = run.State.Tick;
        await run.ActRetained(new(player, "ven051-reject-" + run.Commands().Length, JsonSerializer.SerializeToElement(raw, Json)), false);
    }

    private static async Task Export(string name, MatchState initial, Driver run)
    {
        await run.Replay(); var state = run.State; var sequence = run.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), run);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        foreach (var player in state.Seats.Keys) {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN051_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        System.Reflection.Assembly[] assemblies = [typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly];
        var runtime = new { ruleInputs = RuleInputIdentity.Current, appContextBaseDirectory = AppContext.BaseDirectory,
            catalogPath = OfficialCardCatalog.DefaultCatalogPath(), assemblies = assemblies.Select(a => new { name = a.GetName().Name,
                loadedPath = a.Location, mvid = a.ManifestModule.ModuleVersionId.ToString(), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) };
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new { initial, final = state,
            commands = run.Commands(), events = run.Events(), snapshots, prompts, runtime }, Json));
    }
    private sealed class Driver : IMatchJournal
    {
        private readonly MatchState initial;
        private readonly List<MatchJournalEntry> entries = [];
        private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
        private MatchSession session;
        internal MatchState State { get; private set; }
        internal Driver(MatchState initial) { this.initial = initial; State = initial; session = new(initial, new CoreRuleEngine(), this); }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { entries.Add(entry); return ValueTask.CompletedTask; }
        internal RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs, TypedCommand = e.TypedCommand }).ToArray();
        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        internal async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, json); command = GameCommandJsonMapper.Map(wire);
            var id = "ven051-" + entries.Count;
            var result = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var seq = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, seq, snapshots[p], State.Tick, seq, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, seq, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, seq, Commands(), Events(), views, State, State.Tick, spectator);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(State.RoomId, State.Tick, seq, Commands(), Events(), views, errors, State, initial, spectator) { OpeningSeed = initial.Seed };
            session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            var count = entries.Count; var hash = MatchStateHasher.Hash(State);
            var duplicate = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(duplicate.Accepted);
            Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            Assert.Equal(hash, MatchStateHasher.Hash(State));
            await Replay();
        }
        internal async Task ActRetained(ConformanceCommand retained, bool expectedAccepted)
        {
            var before = MatchStateHasher.Hash(State); var snapshotsBefore = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State));
            var promptsBefore = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State));
            var raw = retained.Cmd.Clone(); var command = GameCommandJsonMapper.Map(raw);
            var result = await session.SubmitAsync(retained.PlayerId, retained.ClientIntentId, command, raw, default);
            Assert.Equal(expectedAccepted, result.Accepted); State = result.State;
            if (!expectedAccepted) {
                Assert.Equal(before, MatchStateHasher.Hash(State)); Assert.Empty(result.Events);
                Assert.Equal(snapshotsBefore, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State)));
                Assert.Equal(promptsBefore, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State)));
            }
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var sequence = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, sequence, snapshots[p], State.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, sequence, Commands(), Events(), views, State, State.Tick, spectator);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(State.RoomId, State.Tick, sequence, Commands(), Events(), views, errors, State, initial, spectator) { OpeningSeed = initial.Seed };
            session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            if (expectedAccepted) {
                var count = entries.Count; var hash = MatchStateHasher.Hash(State);
                var duplicate = await session.SubmitAsync(retained.PlayerId, retained.ClientIntentId, command, raw, default);
                Assert.True(duplicate.Accepted); Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            }
            await Replay();
        }
        internal async Task Top() { await Act(State.PriorityPlayerId!, new PassPriorityCommand()); await Act(State.PriorityPlayerId!, new PassPriorityCommand()); }
        internal async Task Replay()
        {
            var result = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), State, new CoreRuleEngine(), default, Events());
            Assert.True(result.IsMatch, string.Join("; ", result.Errors));
        }
    }

}
