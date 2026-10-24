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

public sealed class AttachedPredictOpeningTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("SFD·065/221", OfficialDeckFormat.CoreRules, false)]
    [InlineData("SFD·065/221", OfficialDeckFormat.CoreRules, true)]
    [InlineData("SFD·065/221", OfficialDeckFormat.ChinaStandard20260724, false)]
    [InlineData("SFD·065/221", OfficialDeckFormat.ChinaStandard20260724, true)]
    [InlineData("OGN·100/298", OfficialDeckFormat.CoreRules, false)]
    [InlineData("OGN·100/298", OfficialDeckFormat.CoreRules, true)]
    [InlineData("OGN·100/298", OfficialDeckFormat.ChinaStandard20260724, false)]
    [InlineData("OGN·100/298", OfficialDeckFormat.ChinaStandard20260724, true)]
    public async Task OrdinaryLegalCopiedGrantsKeepIndependentChoicesColdReplayAndOriginalNextTurn(string host, OfficialDeckFormat format, bool recycleFirst)
    {
        var run = await CaptureAsync(host, format);
        Assert.Equal(2, run.State.TriggerQueue.Count);
        Assert.Single(run.State.TriggerQueue, t => t.InsightContext?.PredictGrant?.Origin is not null);
        Assert.Null(run.State.PendingCardChoice);
        var captured = run.Frame(); Assert.Empty(captured.ValidationErrors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(captured, new CoreRuleEngine(), default));
        await run.Cold();
        await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        var originalDeck = run.State.PlayerZones["P1"].MainDeck.ToArray();
        var rejected = 0;
        for (var i = 0; i < 2; i++)
        {
            var top = run.State.PlayerZones["P1"].MainDeck[0];
            await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
            await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
            while (run.State.PendingRuleChoice is { Request.Kind: "DECK_OBSERVATION_BANISH" } observation)
                await run.Submit("P1", new PayCostCommand(observation.Request.Id, "RULE_REPLACEMENT", ["KEEP"]));
            var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
            Assert.Equal([top], choice.LegalObjectIds);
            Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
            await run.Reject("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); rejected++;
            await run.Reject("P1", new ChooseCardsCommand("STALE", choice.ChoiceWindow, [])); rejected++;
            await run.Cold();
            await run.Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, recycleFirst && i == 0 ? [top] : []));
            await run.Cold();
        }
        Assert.Equal(recycleFirst ? originalDeck.Skip(1).Append(originalDeck[0]) : originalDeck, run.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue);
        Assert.Null(run.State.PendingCardChoice); Assert.Null(run.State.PendingEffectPlay);
        Assert.Equal(MatchPhases.Main, run.State.Phase); Assert.Equal(TimingStates.NeutralOpen, run.State.TimingState);
        Assert.Contains(CommandTypes.EndTurn, run.Prompt("P1").EnabledActions());
        await run.Submit("P1", new EndTurnCommand());
        Assert.Equal("P2", run.State.TurnPlayerId); Assert.Equal(MatchPhases.Main, run.State.Phase);
        await run.Cold();
        Assert.Equal(4, rejected);
        var frame = run.Frame(); Assert.Empty(frame.ValidationErrors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_ATTACHED_PREDICT_EVIDENCE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            var fileName = Path.Combine(folder, (host.StartsWith("SFD", StringComparison.Ordinal) ? "mechanical" : "other-friendly")
                + "-" + format + "-" + recycleFirst + ".json.gz");
            Assert.False(File.Exists(fileName));
            await using var file = File.Create(fileName); await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
            await JsonSerializer.SerializeAsync(zip, frame, Opening.Json);
        }
        output.WriteLine("ORDINARY_ATTACHED_PREDICT " + JsonSerializer.Serialize(new { host, format, recycleFirst,
            commands = frame.Commands.Count, events = frame.Events.Count, run.ColdCount, run.OriginalNextMatches,
            engineRejectionsWithoutMutation = rejected, originalFullActionEventReplayMatched = true,
            originalBlankSeed = 0, ruleInputs = run.State.RuleInputs }, Opening.Json));
    }

    internal static async Task<Opening> CaptureAsync(string host, OfficialDeckFormat format)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "attached-predict-openings.json")));
        var row = fixture.RootElement.GetProperty("branches").EnumerateArray().Single(b => b.GetProperty("host").GetString() == host);
        var run = new Opening(host, format);
        var entries = row.GetProperty("acceptedCommands").EnumerateArray().ToArray();
        for (var i = 0; i < entries.Length; i++)
        {
            var command = GameCommandJsonMapper.Map(entries[i].GetProperty("rawCommand"));
            if (i == 6 || command is AssembleEquipmentCommand || command is PlayCardCommand { CardNo: "SFD·075/221" }) await run.Cold();
            await run.Submit(entries[i].GetProperty("playerId").GetString()!, command);
        }
        Assert.Equal(host.StartsWith("SFD", StringComparison.Ordinal) ? 33 : 35, run.Frame().Commands.Count);
        Assert.Equal(row.GetProperty("gear").GetString(), Assert.Single(run.State.TriggerQueue,
            t => t.InsightContext?.PredictGrant?.Origin is not null).InsightContext!.PredictGrant!.Origin!.Equipment.ObjectId);
        return run;
    }

    internal sealed class Opening : IMatchJournal
    {
        internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
            WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        private readonly List<MatchJournalEntry> entries = [];
        private readonly CoreRuleEngine engine = new();
        private readonly MatchSessionOptions options;
        private IMatchSession session;
        private bool afterCold;
        internal MatchState Initial { get; }
        internal MatchState State { get; private set; }
        internal int ColdCount { get; private set; }
        internal int OriginalNextMatches { get; private set; }
        internal Opening(string host, OfficialDeckFormat format)
        {
            Initial = MatchReplayInitialStateBuilder.FromSeats("ordinary-copied-predict-" + (host.StartsWith("SFD", StringComparison.Ordinal) ? "mechanical" : "other-friendly") + "-" + format,
                new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Assert.Equal(0, Initial.Tick); Assert.Equal(0, Initial.Seed); Assert.Empty(Initial.CardObjects);
            State = Initial; options = new(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            session = new MatchSession(Initial, engine, this, NoopMatchPlayerStore.Instance, options);
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { entries.Add(entry); return ValueTask.CompletedTask; }
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
            var raw = JsonSerializer.SerializeToElement(fields, Json); var intent = "ordinary-copy-" + entries.Count;
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
            var result = await engine.ResolveAsync(State, new("invalid-copy-choice", actor, command.CmdType), command, default);
            Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        }
        internal MatchRecoveryFrame Frame(MatchState? target = null)
        {
            target ??= State;
            var commands = entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
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
            var restored = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
            Assert.Equal(MatchStateHasher.Hash(State), MatchStateHasher.Hash(restored.AuthoritativeState!));
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(restored, engine, default));
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
