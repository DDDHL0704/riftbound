using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class RavenbloomPutIntoHandOpeningTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("spell", OfficialDeckFormat.CoreRules)]
    [InlineData("unit", OfficialDeckFormat.CoreRules)]
    [InlineData("spell", OfficialDeckFormat.ChinaStandard20260724)]
    [InlineData("unit", OfficialDeckFormat.ChinaStandard20260724)]
    public async Task OrdinaryDefendedRevealPreservesTheGenuineDrawCountAndOriginalNextTurn(string branch, OfficialDeckFormat format)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "ravenbloom-put-hand-openings.json")));
        var recipe = document.RootElement.GetProperty("branches").EnumerateArray().Single(row => row.GetProperty("branch").GetString() == branch);
        var spell = branch == "spell";
        var initial = MatchReplayInitialStateBuilder.FromSeats("ordinary-raven-replay-" + branch + format,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        Assert.Equal(0, initial.Tick); Assert.Equal(0, initial.Seed); Assert.Empty(initial.CardObjects);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
        var engine = new CoreRuleEngine(); var journal = new Journal();
        IMatchSession session = new MatchSession(initial, engine, journal, NoopMatchPlayerStore.Instance, options);
        var state = initial; var coldCount = 0; var nextMatched = 0; var afterCold = false;
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var entries = recipe.GetProperty("acceptedCommands").EnumerateArray().ToArray();
        var battlefield = recipe.GetProperty("battlefield").GetString()!;
        var top = recipe.GetProperty("expectedRevealedObjectId").GetString()!;
        MatchState? beforeReveal = null;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i]; var command = GameCommandJsonMapper.Map(entry.GetProperty("rawCommand"));
            if (i == 6 || command is PlayCardCommand { CardNo: "UNL-074/219" }) await Cold();
            if (command is DeclareBattleCommand battle)
            {
                beforeReveal = state;
                Assert.Equal(1, state.DrawLedger.Counts.GetValueOrDefault("P2"));
                Assert.Equal(top, state.PlayerZones["P2"].MainDeck[0]);
                await Reject("P2", battle);
                await Reject("P1", battle with { BattlefieldId = "UNCONTROLLED-MISSING-FIELD" });
                await Cold();
            }
            await Submit(entry.GetProperty("playerId").GetString()!, command);
        }
        Assert.Equal(31, journal.Entries.Count);
        Assert.NotNull(beforeReveal);
        Assert.Equal(1, state.DrawLedger.Counts.GetValueOrDefault("P2"));
        Assert.Null(state.PendingCardChoice); Assert.Null(state.PendingEffectPlay);
        Assert.Empty(state.StackItems); Assert.Empty(state.TriggerQueue);
        Assert.Equal(MatchPhases.Main, state.Phase); Assert.Equal(TimingStates.NeutralOpen, state.TimingState);
        Assert.Null(state.FocusPlayerId); Assert.Null(state.PriorityPlayerId);
        var last = journal.Entries[^1].Events;
        Assert.DoesNotContain(last, ev => ev.Kind == "CARD_DRAWN");
        if (spell)
        {
            var added = Assert.Single(last, ev => ev.Kind == "CARD_ADDED_TO_HAND");
            Assert.Equal(battlefield, added.Payload["sourceObjectId"]);
            Assert.Equal([top], Assert.IsType<string[]>(added.Payload["cardIds"]));
            Assert.Equal(beforeReveal.PlayerZones["P2"].MainDeck.Skip(1), state.PlayerZones["P2"].MainDeck);
            Assert.Equal(beforeReveal.PlayerZones["P2"].Hand.Append(top), state.PlayerZones["P2"].Hand);
            var counts = new Dictionary<string, int>(state.DrawLedger.Counts) { ["P2"] = 2 };
            var forged = state with { DrawLedger = new(state.TurnNumber, counts) };
            var forgedEvents = Events().Select(ev => ev.Event.Kind == "CARD_ADDED_TO_HAND"
                && ev.Event.Payload.GetValueOrDefault("sourceObjectId") as string == battlefield
                    ? ev with { Event = ev.Event with { Kind = "CARD_DRAWN" } } : ev).ToArray();
            var forgedFrame = Frame(forged, forgedEvents);
            Assert.Empty(forgedFrame.ValidationErrors);
            Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forgedFrame, engine, default));
        }
        else
        {
            Assert.DoesNotContain(last, ev => ev.Kind == "CARD_ADDED_TO_HAND");
            var recycled = Assert.Single(last, ev => ev.Kind == "CARDS_RECYCLED");
            Assert.Equal([top], Assert.IsType<string[]>(recycled.Payload["cardIds"]));
            Assert.Equal(beforeReveal.PlayerZones["P2"].Hand, state.PlayerZones["P2"].Hand);
            Assert.Equal(beforeReveal.PlayerZones["P2"].MainDeck.Skip(1).Append(top), state.PlayerZones["P2"].MainDeck);
        }
        await Cold();
        Assert.Contains(CommandTypes.EndTurn, session.PromptFor("P1").EnabledActions());
        await Submit("P1", new EndTurnCommand());
        Assert.Equal("P2", state.TurnPlayerId); Assert.Equal(MatchPhases.Main, state.Phase);
        await Cold();
        Assert.Equal(5, coldCount); Assert.Equal(4, nextMatched);
        Assert.Equal(32, journal.Entries.Count);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_RAVENBLOOM_OPENING_EVIDENCE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder); var path = Path.Combine(folder, branch + "-" + format + ".json.gz");
            Assert.False(File.Exists(path));
            await using var file = File.Create(path); await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
            await JsonSerializer.SerializeAsync(zip, new { initial, beforeReveal, state,
                commands = Commands(), events = Events(), format, coldCount, nextMatched }, json);
        }
        output.WriteLine("ORDINARY_RAVENBLOOM " + JsonSerializer.Serialize(new { branch, format,
            commands = journal.Entries.Count, events = Events().Length, originalBlankSeed = 0,
            coldCount, originalNextCommandsMatched = nextMatched, engineRejectionsWithoutMutation = 2,
            originalFullActionAndEventReplayMatched = true, ruleInputs = state.RuleInputs }, json));

        async Task Submit(string actor, GameCommand command)
        {
            if (command is SubmitDeckCommand deck)
                foreach (var checkFormat in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                {
                    var validation = OfficialDeckValidator.Validate(new(deck.LegendCardNo, deck.ChampionCardNo,
                        deck.MainDeck, deck.RuneDeck, deck.Battlefields), catalog, checkFormat);
                    Assert.True(validation.IsValid, string.Join(';', validation.Errors));
                }
            var fields = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(fields, json); var intent = "raven-open-" + journal.Entries.Count;
            var expected = afterCold ? await engine.ResolveAsync(state, new(intent, actor, command.CmdType), command, default) : null;
            var result = command switch {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, intent, submitted, raw, default),
                ReadyCommand => await session.ReadyAsync(actor, intent, raw, default),
                _ => await session.SubmitAsync(actor, intent, command, raw, default) };
            Assert.True(result.Accepted, result.ErrorMessage);
            if (afterCold)
            {
                Assert.True(expected!.Accepted, expected.ErrorMessage);
                Assert.Equal(MatchStateHasher.Hash(expected.State), MatchStateHasher.Hash(result.State));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)));
                nextMatched++;
            }
            state = result.State; afterCold = false; FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        }
        async Task Reject(string actor, GameCommand command)
        {
            var bad = await engine.ResolveAsync(state, new("bad-raven", actor, command.CmdType), command, default);
            Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(bad.State));
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
            e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(
            e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        MatchRecoveryFrame Frame(MatchState target, IReadOnlyList<RecoveredEvent> events)
        {
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(target); var prompts = ResolutionResult.BuildPrompts(target);
            var views = initial.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
                target.Tick, sequence, snapshots[player], target.Tick, sequence, prompts[player]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(initial.RoomId, target.Tick, sequence, [], target);
            var errors = MatchRecoveryValidator.Validate(initial.RoomId, sequence, Commands(), events, views, target, target.Tick, spectator);
            return new(initial.RoomId, target.Tick, sequence, Commands(), events, views, errors, target, initial, spectator);
        }
        async Task Cold()
        {
            var frame = Frame(state, Events()); Assert.Empty(frame.ValidationErrors);
            var restored = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, json), json)!;
            Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored.AuthoritativeState!));
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(restored, engine, default));
            var old = session;
            session = MatchSession.Restore(restored, engine, journal, NoopMatchPlayerStore.Instance, options);
            foreach (var player in initial.Seats.Keys)
            {
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(old.SnapshotFor(player)), JsonSerializer.SerializeToNode(session.SnapshotFor(player))));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(old.PromptFor(player)), JsonSerializer.SerializeToNode(session.PromptFor(player))));
            }
            coldCount++; afterCold = true;
        }
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
