using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VenBasicFlowSpellTests
{
    private static readonly string[] Cards = ["VEN·003", "VEN·049", "VEN·081"];
    [Theory]
    [InlineData("VEN·003", 37688, 2, 1, 4, 1)]
    [InlineData("VEN·049", 37740, 2, 0, 2, 0)]
    [InlineData("VEN·081", 37775, 4, 0, 4, 0)]
    public async Task ExactOfficialPrintingRetainsPrintedCostAndIndependentFlowCost(string no, long id, int mana, int power, int flowMana, int flowPower)
    {
        var source = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(source.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.ReturnEnergy ?? 0);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.Equal(mana, behavior.ManaCost); Assert.Equal(flowMana, behavior.FlowCost!.Mana);
        Assert.Equal(flowPower, behavior.FlowCost.Power); Assert.Equal(flowPower == 1 ? new[] { RuneTrait.Red } : [], behavior.FlowCost.Traits);
        Assert.False(behavior.CanPlayDuringPriority); Assert.False(behavior.CanPlayDuringSpellDuel);
    }

    public static IEnumerable<object[]> Plays() => Cards.SelectMany(no => new[] { false, true }.Select(flow => new object[] { no, flow }));
    [Theory]
    [MemberData(nameof(Plays))]
    public async Task OrdinaryAndFlowPlayPayThenResolveTheExactEffectAndLeaveToTheCorrectZone(string no, bool flow)
    {
        var initial = Position(no, flow); var command = Play(no);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(no == "VEN·081" || no == "VEN·003" && flow ? 4 : 2, quote.Cost!.Mana);
        Assert.Equal(no == "VEN·003" ? 1 : 0, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Assert.Equal(0, quote.Cost.GenericPower);
        var driver = new Driver(initial);
        await driver.Act("P1", command);
        Assert.Equal(flow, Assert.Single(driver.State.StackItems).FlowPlay is not null);
        Assert.Empty(driver.State.PlayerZones["P1"].Banished);
        Assert.Equal(20 - quote.Cost.Mana, driver.State.RunePools["P1"].Mana);
        await driver.Top();
        Assert.Contains("SPELL", flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("SPELL", flow ? driver.State.PlayerZones["P1"].Graveyard : driver.State.PlayerZones["P1"].Banished);
        Assert.Empty(driver.State.StackItems);
        if (no == "VEN·003")
        { Assert.Contains("GEAR", driver.State.PlayerZones["P2"].Graveyard); Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Base); }
        if (no == "VEN·049")
        {
            Assert.Equal(new[] { "P1-DRAW-1" }, driver.State.PlayerZones["P1"].Hand);
            Assert.Equal(new[] { "P1-DRAW-2", "P1-DRAW-3" }, driver.State.PlayerZones["P1"].MainDeck);
            Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
            Assert.DoesNotContain("P1-DRAW-1", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        }
        if (no == "VEN·081")
        {
            Assert.Equal(10, driver.State.CardObjects["ENEMY"].Power);
            Assert.Equal(4, driver.State.CardObjects["HOST"].Power);
            await driver.Act("P1", new EndTurnCommand());
            Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power);
        }
        await driver.Replay();
    }

    public static IEnumerable<object[]> Rejections() => Cards.SelectMany(no => new[] { "no-mana", "wrong-turn", "response", "opponent-graveyard", "banished", "wrong-target" }
        .Select(branch => new object[] { no, branch }));
    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task FlowPreservesTimingOwnershipSourceZoneTargetsAndPayment(string no, string branch)
    {
        var state = Position(no, true); var command = Play(no);
        if (branch == "no-mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        if (branch == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "response")
        {
            state = OfficialCopyIdentityTests.AddSpell(state, "RESPONSE", "OGN·048/298");
            var opened = await new CoreRuleEngine().ResolveAsync(state, new("open-response", "P1", CommandTypes.PlayCard), new PlayCardCommand("RESPONSE", "OGN·048/298", []), default);
            Assert.True(opened.Accepted, opened.ErrorMessage); state = opened.State;
        }
        if (branch is "opponent-graveyard" or "banished")
        {
            var zones = state.PlayerZones.ToDictionary(); zones["P1"] = zones["P1"] with { Graveyard = [] };
            var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
            if (branch == "opponent-graveyard")
            { zones["P2"] = zones["P2"] with { Graveyard = ["SPELL"] }; cards["SPELL"] = cards["SPELL"] with { OwnerId = "P2", ControllerId = "P2" }; locations["SPELL"] = new("P2", "GRAVEYARD"); }
            else { zones["P1"] = zones["P1"] with { Banished = ["SPELL"] }; locations["SPELL"] = new("P1", "BANISHED"); }
            state = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
        }
        if (branch == "wrong-target") command = command with { TargetObjectIds = no == "VEN·003" ? ["HOST"] : ["GEAR"] };
        var before = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-flow", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
        Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData("VEN·003")]
    [InlineData("VEN·049")]
    [InlineData("VEN·081")]
    public async Task CounteredFlowStillBanishesTheOnePhysicalSpellWithoutItsEffect(string no)
    {
        var initial = Position(no, true);
        initial = AttackDamageDistributionTests.Spell(initial, "COUNTER", "OGN·045/298", "P2");
        var driver = new Driver(initial); await driver.Act("P1", Play(no));
        var target = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [target]));
        await driver.Top();
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Banished);
        Assert.Empty(driver.State.PlayerZones["P1"].Hand); Assert.Equal(initial.PlayerZones["P1"].MainDeck, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("GEAR", driver.State.PlayerZones["P2"].Base); Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power);
        Assert.Empty(driver.State.StackItems); await driver.Replay();
    }

    [Fact]
    public async Task RedFlowCostCannotBePaidWithAnotherTrait()
    {
        var state = Position("VEN·003", true) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }), ["P2"] = new(20, 20) } };
        var command = Play("VEN·003"); var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay); Assert.Equal(1, quote.Cost!.PowerByTrait[RuneTrait.Red]);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("wrong-trait", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("VEN·003", false)] [InlineData("VEN·003", true)]
    [InlineData("VEN·081", false)] [InlineData("VEN·081", true)]
    public async Task ATargetWithoutAnEnemyRestrictionCanBeFriendly(string no, bool flow)
    {
        var initial = Position(no, flow);
        if (no == "VEN·003")
            initial = initial with {
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P1"] = initial.PlayerZones["P1"] with { Base = ["HOST", "GEAR"] },
                    ["P2"] = initial.PlayerZones["P2"] with { Base = ["ENEMY"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["GEAR"] = new("P1", "BASE") },
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["GEAR"] = initial.CardObjects["GEAR"] with { OwnerId = "P1", ControllerId = "P1" } } };
        var driver = new Driver(initial);
        await driver.Act("P1", Play(no) with { TargetObjectIds = no == "VEN·003" ? ["GEAR"] : ["HOST"] });
        await driver.Top();
        if (no == "VEN·003") Assert.Contains("GEAR", driver.State.PlayerZones["P1"].Graveyard);
        else {
            Assert.Equal(10, driver.State.CardObjects["HOST"].Power);
            Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power);
            await driver.Act("P1", new EndTurnCommand());
            Assert.Equal(4, driver.State.CardObjects["HOST"].Power);
        }
        Assert.Contains("SPELL", flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard);
        await driver.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DrawOneFromAnEmptyDeckBurnsOutAndDrawsOneRecycledCardBeforeSpellExit(bool flow)
    {
        var initial = Position("VEN·049", flow);
        var deck = initial.PlayerZones["P1"].MainDeck;
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                MainDeck = [], Graveyard = initial.PlayerZones["P1"].Graveyard.Concat(deck).ToArray() } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => deck.Contains(p.Key) ? new ObjectLocationState("P1", "GRAVEYARD") : p.Value) };
        var driver = new Driver(initial); await driver.Act("P1", Play("VEN·049")); await driver.Top();
        var drawn = Assert.Single(driver.State.PlayerZones["P1"].Hand);
        Assert.Contains(drawn, deck); Assert.Equal(2, driver.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(1, driver.State.PlayerScores["P2"]);
        Assert.Single(driver.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
        Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
        Assert.Equal(MatchStatuses.InProgress, driver.State.Status); Assert.Empty(driver.State.StackItems);
        Assert.Contains("SPELL", flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("SPELL", driver.State.PlayerZones["P1"].MainDeck);
        await driver.Replay();
    }

    private static PlayCardCommand Play(string no) => new("SPELL", no, no == "VEN·049" ? [] : no == "VEN·003" ? ["GEAR"] : ["ENEMY"]);
    private static MatchState Position(string no, bool flow)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, owner); locations[id] = new(owner, zone); }
        Put("SPELL", no, "P1", flow ? "GRAVEYARD" : "HAND");
        Put("HOST", "OGN·219/298", "P1", "BASE"); Put("ENEMY", "OGN·219/298", "P2", "BASE"); Put("GEAR", "SFD·022/221", "P2", "BASE");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var p in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 3).Select(i => p + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", p, "MAIN_DECK");
            zones[p] = PlayerZones.Empty with { Base = p == "P1" ? ["HOST"] : ["ENEMY", "GEAR"], MainDeck = deck,
                Hand = p == "P1" && !flow ? ["SPELL"] : [], Graveyard = p == "P1" && flow ? ["SPELL"] : [] };
        }
        return new MatchState("VEN-BASIC-FLOW", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Red] = 20 }), ["P2"] = new(20, 20) }, seed: 424242)
            with { CardPlays = new(5, [], []) };
    }
    internal sealed class Driver : IMatchJournal
    {
        private readonly MatchState initial;
        private readonly List<MatchJournalEntry> entries = [];
        private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
        private MatchSession session;
        internal MatchState State { get; private set; }
        internal ResolutionResult? LastRetainedResult { get; private set; }
        // Most fixtures intentionally cold-recover every command. Slow canonical
        // openings can temporarily batch ordinary commands and call ColdCheckpoint
        // at an explicit stage boundary; the default remains the original behavior.
        internal bool RecoverAfterEveryCommand { get; set; } = true;
        private (string Player, string Intent, GameCommand Command, JsonElement Raw)? last;
        internal ActionPromptDto PromptFor(string player) => session.PromptFor(player);
        internal Driver(MatchState initial) { this.initial = initial; State = initial; session = new(initial, new CoreRuleEngine(), this); }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { entries.Add(entry); return ValueTask.CompletedTask; }
        internal RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        internal async Task TapRunes(string player, IEnumerable<string> runeIds)
        {
            // Prepare the same physical resources with production batches. Act still
            // checks privacy, recovery, retry and the complete original replay after
            // every batch and every subsequent gameplay command.
            foreach (var batch in runeIds.ToArray().Chunk(12))
            {
                var mana = State.RunePools[player].Mana;
                var power = State.RunePools[player].TotalPower;
                var prefix = Events().Length;
                await Act(player, new TapRuneCommand(batch[0], batch));
                Assert.Equal(mana + batch.Length, State.RunePools[player].Mana);
                Assert.Equal(power, State.RunePools[player].TotalPower);
                Assert.All(batch, id => Assert.True(State.CardObjects[id].IsExhausted));
                Assert.Equal(batch, Events().Skip(prefix).Where(e => e.Event.Kind == "RUNE_TAPPED")
                    .Select(e => (string)e.Event.Payload["sourceObjectId"]!));
            }
        }
        internal async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            var prompt = session.PromptFor(player);
            if (prompt.EnabledActions().Contains(command.CmdType, StringComparer.Ordinal))
            {
                raw["promptId"] = prompt.PromptId;
                raw["snapshotTick"] = State.Tick;
            }
            var wire = JsonSerializer.SerializeToElement(raw, json); command = GameCommandJsonMapper.Map(wire);
            var id = "basic-flow-" + entries.Count;
            last = (player, id, command, wire);
            var result = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var frame = Frame();
            Assert.Empty(frame.ValidationErrors);
            if (RecoverAfterEveryCommand) session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            var count = entries.Count; var hash = MatchStateHasher.Hash(State);
            var duplicate = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(duplicate.Accepted);
            Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            Assert.Equal(hash, MatchStateHasher.Hash(State));
            if (RecoverAfterEveryCommand) await Replay();
        }

        internal async Task ColdCheckpoint()
        {
            var frame = Frame(); Assert.Empty(frame.ValidationErrors);
            var hash = MatchStateHasher.Hash(State); var count = entries.Count;
            session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            if (last is { } previous)
            {
                var duplicate = await session.SubmitAsync(previous.Player, previous.Intent, previous.Command, previous.Raw, default);
                Assert.True(duplicate.Accepted); Assert.Equal(count, entries.Count);
                Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            }
            await Replay();
        }

        private MatchRecoveryFrame Frame()
        {
            var sequence = entries.Count == 0 ? 0 : entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, sequence, snapshots[p], State.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, sequence, Commands(), Events(), views, State, State.Tick, spectator);
            return new(State.RoomId, State.Tick, sequence, Commands(), Events(), views, errors, State, initial, spectator) { OpeningSeed = initial.Seed };
        }
        internal async Task ActRetained(ConformanceCommand retained, bool expectedAccepted)
        {
            var before = MatchStateHasher.Hash(State); var snapshotsBefore = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State));
            var promptsBefore = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State));
            var raw = retained.Cmd.Clone(); var command = GameCommandJsonMapper.Map(raw);
            var result = await session.SubmitAsync(retained.PlayerId, retained.ClientIntentId, command, raw, default);
            LastRetainedResult = result;
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
            Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
            var frame = new MatchRecoveryFrame(State.RoomId, State.Tick, sequence, Commands(), Events(), views, errors, State, initial, spectator) { OpeningSeed = initial.Seed };
            session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            if (expectedAccepted) {
                var count = entries.Count; var hash = MatchStateHasher.Hash(State);
                var duplicate = await session.SubmitAsync(retained.PlayerId, retained.ClientIntentId, command, raw, default);
                Assert.True(duplicate.Accepted); Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            }
            await Replay();
        }
        internal async Task Top()
        {
            var top = State.StackItems[^1].StackItemId;
            for (var passes = 0; State.StackItems.Any(item => item.StackItemId == top)
                && State.PriorityPlayerId is { } player; passes++)
            {
                Assert.True(passes < 4, "The top item did not resolve or open a choice after both players passed.");
                await Act(player, new PassPriorityCommand());
            }
        }
        internal async Task Replay()
        {
            var result = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), State, new CoreRuleEngine(), default, Events());
            Assert.True(result.IsMatch, string.Join("; ", result.Errors));
        }
    }

}
