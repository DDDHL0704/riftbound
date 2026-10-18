using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class CopiedDuelInsightOpeningTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases()
        => from songs in new[] { 1, 2 }
           from format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 }
           from profile in new[] { "ALL_PAY_KEEP", "ALL_PAY_RECYCLE", "MIXED_PAY_RECYCLE", "ALL_DECLINE" }
           select new object[] { songs, format, profile };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OrdinaryLegalCopiedDuelCostsCurrentDeckRecoveryAndOriginalInitiatorContinue(int songs, OfficialDeckFormat format, string profile)
    {
        var run = await CaptureAsync(songs, format); var state = run.State;
        var captured = state.TriggerQueue.First(t => t.InsightContext?.Origin is not null);
        Assert.Equal(songs + 1, state.TriggerQueue.Count); Assert.Equal("P2", state.TurnPlayerId);
        Assert.All(state.TriggerQueue, t => {
            Assert.Equal("P1", t.ControllerId); Assert.Equal("UNL-079/219", t.SourceCardNo);
            Assert.Equal("DUEL", t.InsightContext!.Kind); Assert.Equal(1, t.InsightContext.Count);
            Assert.Equal("P2", t.InsightContext.ReturnFocusPlayerId);
        });
        foreach (var changeFocus in new[] { false, true })
        {
            var changed = state with { TriggerQueue = state.TriggerQueue.Select(t => t.TriggerId == captured.TriggerId
                ? t with { InsightContext = changeFocus ? t.InsightContext! with { ReturnFocusPlayerId = "P1" }
                    : t.InsightContext! with { Origin = null } } : t).ToArray() };
            var forged = run.Frame(changed); Assert.True(forged.ValidationErrors.Count == 0, string.Join(';', forged.ValidationErrors));
            Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, new CoreRuleEngine(), default));
        }
        await run.Cold();
        await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        await run.Cold(); var startEvents = run.Frame().Events.Count;
        var deckBeforeConfirm = run.State.PlayerZones["P1"].MainDeck.ToArray(); var handBeforeConfirm = run.State.PlayerZones["P1"].Hand.ToArray();
        var payments = new HashSet<string>(); var paid = 0; var rejected = 0;
        for (var i = 0; i <= songs; i++)
        {
            var pending = Assert.IsType<PendingPaymentState>(run.State.PendingPayment);
            Assert.Equal("P1", pending.PlayerId); Assert.Equal("INSIGHT_EFFECT", pending.PaymentWindow); Assert.Equal(1, pending.ManaCost);
            Assert.True(payments.Add(pending.PaymentId)); Assert.Null(run.State.PendingCardChoice);
            await run.Reject("P2", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"])); rejected++;
            await run.Reject("P1", new PayCostCommand("STALE", pending.PaymentWindow, ["DECLINE"])); rejected++;
            await run.Cold(); var pay = profile != "ALL_DECLINE" && (profile != "MIXED_PAY_RECYCLE" || i % 2 == 1);
            if (pay && run.State.RunePools["P1"].Mana == 0)
            {
                var rune = run.State.PlayerZones["P1"].Base.First(id => run.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && !run.State.CardObjects[id].IsExhausted);
                await run.Submit("P1", new TapRuneCommand(rune));
            }
            var mana = run.State.RunePools["P1"].Mana;
            await run.Submit("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
            Assert.Equal(mana - (pay ? 1 : 0), run.State.RunePools["P1"].Mana); if (pay) paid++;
            Assert.Equal(deckBeforeConfirm, run.State.PlayerZones["P1"].MainDeck); Assert.Equal(handBeforeConfirm, run.State.PlayerZones["P1"].Hand);
            Assert.DoesNotContain(run.Frame().Events.Skip(startEvents), e => e.Event.Kind is "INSIGHT_REQUESTED" or "CARD_REVEALED");
            await run.Cold();
        }
        Assert.Null(run.State.PendingPayment); Assert.Equal(paid, run.State.StackItems.Count);
        Assert.All(run.State.StackItems, s => Assert.True(s.InsightContext!.PaymentAccepted));
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var byNo = catalog.Cards.ToDictionary(c => c.CardNo);
        for (var i = 0; i < paid; i++)
        {
            var before = run.State.PlayerZones["P1"]; var deck = before.MainDeck.ToList(); var hand = before.Hand.ToList();
            var eventStart = run.Frame().Events.Count;
            await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
            await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
            await KeepObservation(run);
            var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
            Assert.Equal("P1", choice.PlayerId); Assert.Equal("INSIGHT", choice.ChoiceWindow); Assert.Equal([deck[0]], choice.ContextObjectIds);
            Assert.DoesNotContain("\"" + deck[0] + "\"", JsonSerializer.Serialize(run.Prompt("P2")));
            await run.Reject("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); rejected++;
            await run.Reject("P1", new ChooseCardsCommand("STALE", choice.ChoiceWindow, [])); rejected++;
            await run.Reject("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [deck[1]])); rejected++;
            await run.Cold(); var recycle = i == 0 && profile is "ALL_PAY_RECYCLE" or "MIXED_PAY_RECYCLE";
            var selected = recycle ? new[] { deck[0] } : [];
            if (recycle) { var old = deck[0]; deck.RemoveAt(0); deck.Add(old); }
            var revealed = deck[0]; var shouldDraw = byNo[run.State.CardObjects[revealed].CardNo!].CardCategoryNameList.Contains("法术");
            if (shouldDraw) { deck.RemoveAt(0); hand.Add(revealed); }
            await run.Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected)); await KeepObservation(run);
            var revealedEvent = Assert.Single(run.Frame().Events.Skip(eventStart), e => e.Event.Kind == "CARD_REVEALED");
            Assert.Equal(revealed, revealedEvent.Event.Payload["cardObjectId"]);
            Assert.Equal(deck, run.State.PlayerZones["P1"].MainDeck); Assert.Equal(hand, run.State.PlayerZones["P1"].Hand);
            Assert.Null(run.State.PendingCardChoice); await run.Cold();
        }
        Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue); Assert.Null(run.State.PendingPayment);
        Assert.Equal(TimingStates.SpellDuelOpen, run.State.TimingState); Assert.Equal("P2", run.State.FocusPlayerId);
        await run.Cold(); await run.Submit("P2", new PassFocusCommand()); Assert.Equal("P1", run.State.FocusPlayerId); await run.Cold();
        var frame = run.Frame(); Assert.Empty(frame.ValidationErrors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_COPIED_DUEL_EVIDENCE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder); var path = Path.Combine(folder, $"songs-{songs}-{format}-{profile}.json.gz"); Assert.False(File.Exists(path));
            await using var file = File.Create(path); await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
            await JsonSerializer.SerializeAsync(zip, frame, Opening.JournalJson);
        }
        output.WriteLine("ORDINARY_COPIED_DUEL " + JsonSerializer.Serialize(new { songs, format, profile, paid,
            commands = frame.Commands.Count, events = frame.Events.Count, run.ColdCount, run.OriginalNextMatches,
            engineRejectionsWithoutMutation = rejected, coordinatedShapeValidOriginalLogRejections = 2,
            originalFullActionEventReplayMatched = true, originalBlankSeed = 0, ruleInputs = run.State.RuleInputs }, Opening.Json));
    }

    private static async Task KeepObservation(Opening run)
    {
        while (run.State.PendingRuleChoice is { Request.Kind: "DECK_OBSERVATION_BANISH" } observation)
            await run.Submit("P1", new PayCostCommand(observation.Request.Id, "RULE_REPLACEMENT", ["KEEP"]));
    }

    internal static async Task<Opening> CaptureAsync(int songs, OfficialDeckFormat format)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "copied-duel-insight-openings.json")));
        var row = fixture.RootElement.GetProperty("branches").EnumerateArray().Single(b => b.GetProperty("songs").GetInt32() == songs);
        var run = new Opening(songs, format); var entries = row.GetProperty("acceptedCommands").EnumerateArray().ToArray();
        for (var i = 0; i < entries.Length; i++)
        {
            var command = GameCommandJsonMapper.Map(entries[i].GetProperty("rawCommand"));
            if (i == 6 || command is AssembleEquipmentCommand or MoveUnitCommand or PlayCardCommand { CardNo: "UNL-079/219" or "SFD·059/221" }) await run.Cold();
            await run.Submit(entries[i].GetProperty("playerId").GetString()!, command);
        }
        Assert.Equal(songs == 1 ? 36 : 49, run.Frame().Commands.Count);
        Assert.Equal(songs + 1, run.State.TriggerQueue.Count); Assert.Equal(songs, run.State.TriggerQueue.Count(t => t.InsightContext?.Origin is not null));
        Assert.Equal(row.GetProperty("host").GetString(), run.State.TriggerQueue[0].SourceObjectId);
        return run;
    }
    internal sealed class Opening : IMatchJournal
    {
        internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
            WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        internal static readonly JsonSerializerOptions JournalJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        private readonly List<MatchJournalEntry> entries = [];
        private readonly CoreRuleEngine engine = new();
        private readonly MatchSessionOptions options;
        private IMatchSession session;
        private bool afterCold;
        internal MatchState Initial { get; }
        internal MatchState State { get; private set; }
        internal int ColdCount { get; private set; }
        internal int OriginalNextMatches { get; private set; }
        internal Opening(int songs, OfficialDeckFormat format)
        {
            Initial = MatchReplayInitialStateBuilder.FromSeats($"ordinary-copied-duel-{songs}-{format}",
                new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Assert.Equal(0, Initial.Tick); Assert.Equal(0, Initial.Seed); Assert.Empty(Initial.CardObjects);
            State = Initial; options = new(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            session = new MatchSession(Initial, engine, this, NoopMatchPlayerStore.Instance, options);
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { entries.Add(entry); return ValueTask.CompletedTask; }
        internal ActionPromptDto Prompt(string player) => session.PromptFor(player);
        internal async Task Submit(string actor, GameCommand command)
        {
            if (command is SubmitDeckCommand deck)
            {
                var catalog = await OfficialCardCatalog.LoadDefaultAsync();
                foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                {
                    var valid = OfficialDeckValidator.Validate(new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields), catalog, format);
                    Assert.True(valid.IsValid, string.Join(';', valid.Errors));
                }
            }
            var fields = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = State.Tick;
            var raw = JsonSerializer.SerializeToElement(fields, Json); var intent = "ordinary-duel-" + entries.Count;
            var expected = afterCold ? await engine.ResolveAsync(State, new(intent, actor, command.CmdType), command, default) : null;
            var result = command switch { SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, intent, submitted, raw, default),
                ReadyCommand => await session.ReadyAsync(actor, intent, raw, default), _ => await session.SubmitAsync(actor, intent, command, raw, default) };
            Assert.True(result.Accepted, result.ErrorMessage);
            if (afterCold)
            {
                Assert.True(expected!.Accepted, expected.ErrorMessage);
                Assert.Equal(MatchStateHasher.Hash(expected.State), MatchStateHasher.Hash(result.State));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)));
                OriginalNextMatches++;
            }
            State = result.State; afterCold = false; FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        }
        internal async Task Reject(string actor, GameCommand command)
        {
            var before = MatchStateHasher.Hash(State);
            var result = await engine.ResolveAsync(State, new("invalid-last-breath-choice", actor, command.CmdType), command, default);
            Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        }
        internal MatchRecoveryFrame Frame(MatchState? target = null)
        {
            target ??= State;
            var commands = entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
            var events = entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var sequence = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(target); var prompts = ResolutionResult.BuildPrompts(target);
            var views = Initial.Seats.Keys.ToDictionary(player => player,
                player => new RecoveredPlayerView(player, target.Tick, sequence, snapshots[player], target.Tick, sequence, prompts[player]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(Initial.RoomId, target.Tick, sequence, [], target);
            var errors = MatchRecoveryValidator.Validate(Initial.RoomId, sequence, commands, events, views, target, target.Tick, spectator);
            return new(Initial.RoomId, target.Tick, sequence, commands, events, views, errors, target, Initial, spectator);
        }
        internal async Task Cold()
        {
            var frame = Frame(); Assert.Empty(frame.ValidationErrors);
            var restored = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, JournalJson), JournalJson)!;
            Assert.Equal(MatchStateHasher.Hash(State), MatchStateHasher.Hash(restored.AuthoritativeState!));
            var replayErrors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(restored, engine, default);
            if (replayErrors.Count > 0 && Environment.GetEnvironmentVariable("RIFTBOUND_COPIED_DUEL_EVIDENCE") is { Length: > 0 } diagnosticFolder)
            {
                Directory.CreateDirectory(diagnosticFolder); var path = Path.Combine(diagnosticFolder, Initial.RoomId + "-failed-cold-" + State.Tick + ".json.gz");
                Assert.False(File.Exists(path)); await using var file = File.Create(path);
                await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
                await JsonSerializer.SerializeAsync(zip, restored, JournalJson);
            }
            Assert.True(replayErrors.Count == 0, string.Join("; ", replayErrors));
            var previous = session;
            session = MatchSession.Restore(restored, engine, this, NoopMatchPlayerStore.Instance, options);
            foreach (var player in Initial.Seats.Keys)
            {
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(previous.SnapshotFor(player)), JsonSerializer.SerializeToNode(session.SnapshotFor(player))));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(previous.PromptFor(player)), JsonSerializer.SerializeToNode(session.PromptFor(player))));
            }
            ColdCount++; afterCold = true;
        }
    }
}
