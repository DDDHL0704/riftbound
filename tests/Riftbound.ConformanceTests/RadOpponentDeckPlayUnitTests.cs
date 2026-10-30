using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadOpponentDeckPlayUnitTests
{
    private const string Trainer = "RAD·018/167";

    // The room recovery protocol currently admits P1/P2 only. Exercise the
    // multi-opponent rule at the engine/session layer, and run full recovery
    // validation and restoration for the supported two-player room format.
    private sealed class Driver : IMatchJournal
    {
        private readonly MatchState initial;
        private readonly List<MatchJournalEntry> entries = [];
        private MatchSession? session;
        internal MatchState State { get; private set; }
        internal Driver(MatchState initial) { this.initial = initial; State = initial; }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { entries.Add(entry); return ValueTask.CompletedTask; }
        internal RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
            e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) {
                TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((ev, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        internal async Task Act(string player, GameCommand command)
        {
            session ??= new(initial, new CoreRuleEngine(), this);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, json); command = GameCommandJsonMapper.Map(wire);
            var id = "opponent-deck-play-" + entries.Count;
            var result = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State;
            AssertPrivacy(result);
            if (State.Seats.Count == 2) {
                var sequence = entries[^1].CompletedEventSequence;
                var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, sequence,
                    result.Snapshots[p], State.Tick, sequence, result.Prompts[p]));
                var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
                var errors = MatchRecoveryValidator.Validate(State.RoomId, sequence, Commands(), Events(), views, State, State.Tick, spectator);
                Assert.True(errors.Count == 0, string.Join("; ", errors));
                session = MatchSession.Restore(new(State.RoomId, State.Tick, sequence, Commands(), Events(), views, errors, State, initial, spectator) {
                    OpeningSeed = initial.Seed }, new CoreRuleEngine(), this);
            }
            var count = entries.Count; var hash = MatchStateHasher.Hash(State);
            var duplicate = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(duplicate.Accepted); Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
        }
        private static void AssertPrivacy(ResolutionResult result)
        {
            var parent = result.State.StackItems.SingleOrDefault(i => i.StackItemId == result.State.PendingCardChoice?.DeckContext?.StackItemId);
            var shown = parent?.DeckPlaySequence is { Kind: "OPPONENT_REVEAL_PLAY" } c
                ? c.Viewed.Values.SelectMany(v => v).Where(b => result.State.CardObjects[b.ObjectId].ObjectGeneration == b.Generation)
                    .Select(b => b.ObjectId).ToHashSet() : [];
            foreach (var viewer in result.State.Seats.Keys) {
                var snapshot = JsonSerializer.SerializeToNode(result.Snapshots[viewer])!;
                var prompt = JsonSerializer.SerializeToNode(result.Prompts[viewer])!;
                var strings = new HashSet<string>();
                void Collect(JsonNode? node) {
                    if (node is JsonValue value && value.TryGetValue<string>(out var text)) strings.Add(text);
                    else if (node is JsonObject obj) foreach (var property in obj) Collect(property.Value);
                    else if (node is JsonArray array) foreach (var child in array) Collect(child);
                }
                Collect(snapshot); Collect(prompt);
                foreach (var other in result.State.PlayerZones.Where(p => p.Key != viewer))
                    foreach (var id in other.Value.Hand.Concat(other.Value.MainDeck).Concat(other.Value.RuneDeck))
                        if (!shown.Contains(id)) Assert.DoesNotContain(id, strings);
            }
        }
        internal async Task Replay()
        {
            if (State.Seats.Count == 2) {
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), State, new CoreRuleEngine(), default, Events());
                Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); return;
            }
            var replaySession = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance);
            foreach (var e in entries) {
                var command = GameCommandJsonMapper.Map(e.RawCommand!.Value);
                var result = await replaySession.SubmitAsync(e.PlayerId, e.ClientIntentId, command, e.RawCommand, default);
                Assert.True(result.Accepted, result.ErrorMessage);
                Assert.Equal(JsonSerializer.Serialize(e.Events), JsonSerializer.Serialize(result.Events));
                if (e == entries[^1]) Assert.Equal(MatchStateHasher.Hash(State), MatchStateHasher.Hash(result.State));
            }
        }
    }

    private static MatchState Position(string source = Trainer, bool multiplayer = false)
    {
        var state = VenUnitEntryBasicTests.Position(source);
        if (multiplayer)
            state = state with {
                Seats = new Dictionary<string, string>(state.Seats) { ["P3"] = "P3" },
                ReadyPlayerIds = state.ReadyPlayerIds.Append("P3").ToArray(),
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P3"] = PlayerZones.Empty },
                RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P3"] = RunePool.Empty } };
        foreach (var player in state.Seats.Keys.Where(p => p != "P1"))
            state = TopCard(state, player, "OGN·219/298");
        return state;
    }

    private static MatchState TopCard(MatchState state, string player, string cardNo)
    {
        var id = player + "-TOP";
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "MAIN_DECK") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = state.PlayerZones[player] with {
                MainDeck = state.PlayerZones[player].MainDeck.Where(x => x != id).Prepend(id).ToArray() } } };
    }

    private static async Task ResolveTop(Driver game)
    {
        var id = game.State.StackItems.Last().StackItemId;
        for (var i = 0; i < game.State.Seats.Count + 1 && game.State.PendingCardChoice is null
            && game.State.StackItems.Any(s => s.StackItemId == id); i++)
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
    }

    private static async Task Select(Driver game, params string[] cards)
    {
        var choice = game.State.PendingCardChoice!;
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, cards));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-deck-play", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("OGN·219/298", false)]
    [InlineData("OGN·208/298", false)]
    [InlineData("SFD·044/221", false)]
    [InlineData("SFD·022/221", true)]
    [InlineData("UNL-091/219", false)]
    public async Task EntersBeforeRevealingAndActuallyPlaysTheChosenCardIgnoringEveryCost(string cardNo, bool equipment)
    {
        var state = TopCard(Position(), "P2", cardNo);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(9, 2) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Trainer, [], Destination: "BASE"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARDS_REVEALED");
        await ResolveTop(game);
        var choice = game.State.PendingCardChoice!;
        Assert.Equal("P1", choice.PlayerId); Assert.Equal(["P2-TOP"], choice.LegalObjectIds);
        await Select(game, "P2-TOP");
        Assert.True(game.State.PendingEffectPlay!.IgnoreAllCosts);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        await game.Act("P1", new PlayCardCommand("P2-TOP", cardNo, equipment ? ["ALLY"] : []));
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        Assert.Equal("P2", game.State.CardObjects["P2-TOP"].OwnerId);
        if (cardNo == "UNL-091/219") {
            await ResolveTop(game); Assert.Contains("P2-TOP", game.State.PlayerZones["P2"].Graveyard);
        } else {
            Assert.Equal("P1", game.State.CardObjects["P2-TOP"].ControllerId);
            Assert.Contains("P2-TOP", game.State.PlayerZones["P1"].Base);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(Trainer, "P2")]
    [InlineData(Trainer, "P3")]
    [InlineData("OGN·025/298", "P2")]
    [InlineData("OGN·025/298", "P3")]
    public async Task AllOpponentsRevealButOnlyOneCardCanBeSelectedAcrossTheirDecks(string source, string owner)
    {
        var initial = Position(source, multiplayer: true); var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", source, [])); await ResolveTop(game);
        var choice = game.State.PendingCardChoice!;
        Assert.Equal(["P2-TOP", "P3-TOP"], choice.LegalObjectIds);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "CARDS_REVEALED"));
        await Select(game, owner + "-TOP");
        Assert.Null(game.State.PendingCardChoice);
        Assert.Single(game.State.PendingEffectPlay!.Sources);
        var other = owner == "P2" ? "P3" : "P2";
        Assert.Equal(other + "-TOP", game.State.PlayerZones[other].MainDeck.Last());
        Assert.Contains(game.Events(), e => e.Event.Kind == "CARDS_RECYCLED" && Equals(e.Event.Payload["ownerPlayerId"], other));
        await game.Act("P1", new PlayCardCommand(owner + "-TOP", "OGN·219/298", []));
        Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        Assert.Equal(owner, game.State.CardObjects[owner + "-TOP"].OwnerId);
        await game.Replay();
    }

    [Theory]
    [InlineData("wrong-player")] [InlineData("own-deck")] [InlineData("unseen")]
    [InlineData("two-cards")] [InlineData("empty")] [InlineData("wrong-window")]
    public async Task IllegalSelectionsLeaveTheSameChoiceAndStateAvailable(string branch)
    {
        var game = new Driver(Position(multiplayer: true));
        await game.Act("P1", new PlayCardCommand("UNIT", Trainer, [])); await ResolveTop(game);
        var choice = game.State.PendingCardChoice!;
        await Reject(game.State, branch == "wrong-player" ? "P2" : "P1", new ChooseCardsCommand(choice.ChoiceId,
            branch == "wrong-window" ? "INSIGHT" : choice.ChoiceWindow, branch switch {
                "own-deck" => ["P1-DRAW-1"], "unseen" => ["P2-DRAW-1"], "two-cards" => ["P2-TOP", "P3-TOP"],
                "empty" => [], _ => ["P3-TOP"] }));
        await Select(game, "P3-TOP");
        await game.Act("P1", new PlayCardCommand("P3-TOP", "OGN·219/298", []));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EmptyOpponentDecksAreSkippedWithoutDrawingOrBurnout(bool allEmpty)
    {
        var state = Position(multiplayer: true);
        state = state with { PlayerZones = state.PlayerZones.ToDictionary(p => p.Key,
            p => p.Key == "P2" || allEmpty && p.Key == "P3" ? p.Value with { MainDeck = [] } : p.Value) };
        var emptyIds = state.ObjectLocations.Where(p => p.Value.Zone == "MAIN_DECK"
            && !state.PlayerZones[p.Value.PlayerId].MainDeck.Contains(p.Key)).Select(p => p.Key).ToHashSet();
        state = state with { CardObjects = state.CardObjects.Where(p => !emptyIds.Contains(p.Key)).ToDictionary(),
            ObjectLocations = state.ObjectLocations.Where(p => !emptyIds.Contains(p.Key)).ToDictionary() };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Trainer, [])); await ResolveTop(game);
        if (allEmpty) { Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); }
        else { Assert.Equal(["P3-TOP"], game.State.PendingCardChoice!.LegalObjectIds); await Select(game, "P3-TOP");
            await game.Act("P1", new PlayCardCommand("P3-TOP", "OGN·219/298", [])); }
        Assert.Null(game.State.PendingEffectPlay);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARD_DRAWN" or "BURNOUT");
        await game.Replay();
    }

    [Fact]
    public async Task AResponseReturningTheTrainerDoesNotCancelItsEarnedDeckPlay()
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "RETURN", "VEN·052", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Trainer, []));
        await game.Act("P1", new PlayCardCommand("RETURN", "VEN·052", ["UNIT"], Mode: "RETURN_FRIENDLY"));
        await ResolveTop(game); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await ResolveTop(game); await Select(game, "P2-TOP");
        await game.Act("P1", new PlayCardCommand("P2-TOP", "OGN·219/298", []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task HiddenDeckIdentitiesCannotBeDeclaredAsPlayTargetsBeforePayment()
    {
        await Reject(Position(), "P1", new PlayCardCommand("UNIT", Trainer, ["P2-TOP"]));
    }

    [Fact]
    public async Task RecoveryRejectsInventedChoicesGenerationsAndFreePlayPermissions()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Trainer, [])); await ResolveTop(game);
        var item = Assert.Single(game.State.StackItems); var context = item.DeckPlaySequence!;
        var choice = game.State.PendingCardChoice!;
        foreach (var forged in new[] {
            game.State with { PendingCardChoice = choice with { LegalObjectIds = ["P2-DRAW-1"] } },
            game.State with { PendingCardChoice = choice with { MaxCount = 2 } },
            game.State with { StackItems = [item with { DeckPlaySequence = context with { ControllerId = "P2" } }] },
            game.State with { StackItems = [item with { DeckPlaySequence = context with {
                Source = context.Source with { Generation = context.Source.Generation + 1 } } }] } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        await Select(game, "P2-TOP");
        var pending = game.State.PendingEffectPlay!;
        foreach (var forged in new[] {
            pending with { IgnoreAllCosts = false }, pending with { PlayerId = "P2" }, pending with { SourceZone = "HAND" },
            pending with { Sources = new Dictionary<string, long> { ["P2-DRAW-1"] = game.State.CardObjects["P2-DRAW-1"].ObjectGeneration } } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingEffectPlay = forged }));
        await game.Act("P1", new PlayCardCommand("P2-TOP", "OGN·219/298", [])); await game.Replay();
    }

    [Fact]
    public async Task TheOfficialPrintingRetainsItsCostsPowerAndText()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Trainer);
        Assert.Equal(39508, card.Id); Assert.Equal(9, card.Energy); Assert.Equal(2, card.ReturnEnergy); Assert.Equal(9, card.Power);
        Assert.Equal("当你打出我时，展示每名对手主牌堆顶部的一张牌。你从中选择一张，将其放逐，然后将其打出，无视其费用。回收其余的卡牌。", card.CardEffect);
    }
}
