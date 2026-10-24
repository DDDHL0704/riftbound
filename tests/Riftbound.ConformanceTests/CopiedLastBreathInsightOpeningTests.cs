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

public sealed class CopiedLastBreathInsightOpeningTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1, OfficialDeckFormat.CoreRules, false)] [InlineData(1, OfficialDeckFormat.CoreRules, true)]
    [InlineData(1, OfficialDeckFormat.ChinaStandard20260724, false)] [InlineData(1, OfficialDeckFormat.ChinaStandard20260724, true)]
    [InlineData(2, OfficialDeckFormat.CoreRules, false)] [InlineData(2, OfficialDeckFormat.CoreRules, true)]
    [InlineData(2, OfficialDeckFormat.ChinaStandard20260724, false)] [InlineData(2, OfficialDeckFormat.ChinaStandard20260724, true)]
    public async Task OrdinaryLegalLastBreathsKeepIndependentPrivateChoicesOffTurnRecoveryAndOriginalNextTurn(int songs, OfficialDeckFormat format, bool recycleFirst)
    {
        var run = await CaptureAsync(songs, format);
        Assert.Equal(songs + 1, run.State.TriggerQueue.Count);
        Assert.Equal(songs, run.State.TriggerQueue.Count(t => t.InsightContext?.Origin is not null));
        Assert.All(run.State.TriggerQueue, t => {
            Assert.Equal("P1", t.ControllerId); Assert.Equal("UNL-062/219", t.InsightContext!.CardNo);
            Assert.Equal(2, t.InsightContext.Count);
        });
        Assert.Equal("P2", run.State.TurnPlayerId);
        var captured = run.Frame(); Assert.Empty(captured.ValidationErrors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(captured, new CoreRuleEngine(), default));
        var copied = run.State.TriggerQueue.First(t => t.InsightContext?.Origin is not null);
        var printed = run.State.TriggerQueue.Single(t => t.InsightContext?.Origin is null);
        var removed = run.State with { TriggerQueue = run.State.TriggerQueue.Select(t => t.TriggerId == copied.TriggerId
            ? t with { InsightContext = t.InsightContext! with { Origin = null } } : t).ToArray() };
        var swapped = run.State with { TriggerQueue = run.State.TriggerQueue.Select(t => t.TriggerId == copied.TriggerId
            ? t with { InsightContext = t.InsightContext! with { Origin = null } } : t.TriggerId == printed.TriggerId
                ? t with { InsightContext = t.InsightContext! with { Origin = copied.InsightContext!.Origin } } : t).ToArray() };
        foreach (var changed in new[] { removed, swapped })
        {
            var forged = run.Frame(changed); Assert.Empty(forged.ValidationErrors);
            Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, new CoreRuleEngine(), default));
        }
        await run.Cold();
        await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        await run.Cold();
        var originalHand = run.State.PlayerZones["P1"].Hand.ToArray();
        var originalDeck = run.State.PlayerZones["P1"].MainDeck.ToArray();
        var rejected = 0;
        for (var i = 0; i <= songs; i++)
        {
            var deck = run.State.PlayerZones["P1"].MainDeck.ToArray(); var top = deck.Take(2).ToArray();
            await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
            await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
            while (run.State.PendingRuleChoice is { Request.Kind: "DECK_OBSERVATION_BANISH" } observation)
                await run.Submit("P1", new PayCostCommand(observation.Request.Id, "RULE_REPLACEMENT", ["KEEP"]));
            var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
            Assert.Equal("P1", choice.PlayerId); Assert.Equal("INSIGHT", choice.ChoiceWindow);
            Assert.Equal(top, choice.ContextObjectIds); Assert.Equal(0, choice.RequiredCount); Assert.Equal(2, choice.MaxCount);
            await run.Reject("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); rejected++;
            await run.Reject("P1", new ChooseCardsCommand("STALE", choice.ChoiceWindow, [])); rejected++;
            await run.Reject("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [deck[2]])); rejected++;
            await run.Cold();
            var selected = recycleFirst && i == 0 ? top : [];
            await run.Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
            await run.Cold();
            var kept = top.Except(selected).Reverse().ToArray();
            if (kept.Length == 2)
            {
                var order = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
                Assert.Equal("INSIGHT_ORDER", order.ChoiceWindow);
                await run.Reject("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, [kept[0], kept[0]])); rejected++;
                await run.Submit("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, kept));
                await run.Cold();
            }
            var actual = run.State.PlayerZones["P1"].MainDeck;
            Assert.Equal(kept.Concat(deck.Skip(2)), actual.Take(actual.Count - selected.Length));
            Assert.Equal(selected.Order(), actual.Skip(actual.Count - selected.Length).Order());
        }
        Assert.Equal(originalHand, run.State.PlayerZones["P1"].Hand);
        Assert.Equal(originalDeck.Order(), run.State.PlayerZones["P1"].MainDeck.Order());
        Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue); Assert.Null(run.State.PendingCardChoice);
        Assert.Equal("P2", run.State.TurnPlayerId); Assert.Equal(MatchPhases.Main, run.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, run.State.TimingState);
        Assert.Contains(CommandTypes.EndTurn, run.Prompt("P2").EnabledActions());
        await run.Cold(); await run.Submit("P2", new EndTurnCommand());
        Assert.Equal("P1", run.State.TurnPlayerId); await run.Cold();
        var frame = run.Frame(); Assert.Empty(frame.ValidationErrors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_COPIED_LAST_BREATH_EVIDENCE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder); var path = Path.Combine(folder, $"songs-{songs}-{format}-{recycleFirst}.json.gz");
            Assert.False(File.Exists(path)); await using var file = File.Create(path);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize); await JsonSerializer.SerializeAsync(zip, frame, Opening.JournalJson);
        }
        output.WriteLine("ORDINARY_COPIED_LAST_BREATH " + JsonSerializer.Serialize(new { songs, format, recycleFirst,
            commands = frame.Commands.Count, events = frame.Events.Count, run.ColdCount, run.OriginalNextMatches,
            engineRejectionsWithoutMutation = rejected, coordinatedShapeValidOriginalLogRejections = 2,
            originalFullActionEventReplayMatched = true, originalBlankSeed = 0, ruleInputs = run.State.RuleInputs }, Opening.Json));
    }

    internal static async Task<Opening> CaptureAsync(int songs, OfficialDeckFormat format)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "copied-last-breath-insight-openings.json")));
        var row = fixture.RootElement.GetProperty("branches").EnumerateArray().Single(b => b.GetProperty("songs").GetInt32() == songs);
        var run = new Opening(songs, format); var entries = row.GetProperty("acceptedCommands").EnumerateArray().ToArray();
        for (var i = 0; i < entries.Length; i++)
        {
            var command = GameCommandJsonMapper.Map(entries[i].GetProperty("rawCommand"));
            if (i == 6 || command is AssembleEquipmentCommand or MoveUnitCommand or PlayCardCommand { CardNo: "OGS·003/024" }) await run.Cold();
            await run.Submit(entries[i].GetProperty("playerId").GetString()!, command);
        }
        Assert.Equal(songs == 1 ? 45 : 57, run.Frame().Commands.Count);
        Assert.Equal(row.GetProperty("host").GetString(), Assert.Single(run.State.PlayerZones["P1"].Graveyard,
            id => run.State.CardObjects[id].CardNo == "UNL-062/219"));
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
            Initial = MatchReplayInitialStateBuilder.FromSeats($"ordinary-copied-last-breath-{songs}-{format}",
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
            var raw = JsonSerializer.SerializeToElement(fields, Json); var intent = "ordinary-last-breath-" + entries.Count;
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
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
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
            if (replayErrors.Count > 0 && Environment.GetEnvironmentVariable("RIFTBOUND_COPIED_LAST_BREATH_EVIDENCE") is { Length: > 0 } diagnosticFolder)
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
