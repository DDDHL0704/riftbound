using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class FateWeaverOpeningTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OrdinaryBrightFutureOpeningResolvesWeaverAfterTheOffTurnOwnersSkillsAndReplaysItsOriginalChoice(bool draw)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "fate-weaver-bright-future-opening.json")));
        var recipe = document.RootElement;
        var initial = MatchReplayInitialStateBuilder.FromSeats("ordinary-weaver-" + draw,
            recipe.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        Assert.Equal(0, initial.Seed); Assert.Equal(0, initial.Tick); Assert.Empty(initial.CardObjects);
        var engine = new CoreRuleEngine(); var journal = new Journal();
        IMatchSession session = new MatchSession(initial, engine, journal);
        var current = initial;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        foreach (var entry in recipe.GetProperty("acceptedCommands").EnumerateArray())
        {
            var actor = entry.GetProperty("playerId").GetString()!;
            var raw = JsonNode.Parse(entry.GetProperty("rawCommand").GetRawText())!.AsObject();
            await Submit(actor, entry.GetProperty("clientIntentId").GetString()!, raw);
            if (journal.Entries.Count == 24)
            {
                Assert.Equal("P1", current.TurnPlayerId);
                Assert.Equal(2, current.TriggerQueue.Count);
                Assert.All(current.TriggerQueue, t => Assert.Equal("P2", t.ControllerId));
                Assert.Contains(session.PromptFor("P2").Candidates!, c => c.Action == CommandTypes.OrderTriggers && c.Enabled);
                Assert.False(session.PromptFor("P1").Actionable);
            }
        }
        Assert.Equal(32, journal.Entries.Count);
        var choice = Assert.IsType<PendingCardChoiceState>(current.PendingCardChoice);
        var spells = recipe.GetProperty("expectedSpellIds").Deserialize<string[]>()!;
        Assert.Equal("DECK_EFFECT", choice.ChoiceWindow); Assert.Equal("P1", choice.PlayerId);
        Assert.Equal(recipe.GetProperty("expectedWeaver").GetString(), choice.SourceObjectId);
        Assert.Equal(spells, choice.LegalObjectIds); Assert.Equal(4, choice.ContextObjectIds.Count);
        Assert.DoesNotContain(spells[0], JsonSerializer.Serialize(session.SnapshotFor("P2")));
        Assert.DoesNotContain(spells[0], JsonSerializer.Serialize(session.PromptFor("P2")));
        var pending = current; var handCount = current.PlayerZones["P1"].Hand.Count;
        var tail = current.PlayerZones["P1"].MainDeck.Skip(4).ToArray();
        var expectedRest = choice.ContextObjectIds.Except(draw ? [spells[0]] : []).Order().ToArray();
        await Cold();
        var selected = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, draw ? [spells[0]] : []);
        var expected = await engine.ResolveAsync(current, new("weaver-original-choice", "P1", selected.CmdType), selected, default);
        var result = await Submit("P1", "weaver-original-choice", JsonSerializer.SerializeToNode(selected, json)!.AsObject());
        Assert.True(expected.Accepted, expected.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(expected.State), MatchStateHasher.Hash(result.State));
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)));
        Assert.Null(current.PendingCardChoice); Assert.Empty(current.StackItems);
        Assert.Equal(handCount + (draw ? 1 : 0), current.PlayerZones["P1"].Hand.Count);
        Assert.Equal(tail, current.PlayerZones["P1"].MainDeck.Take(tail.Length));
        Assert.Equal(expectedRest, current.PlayerZones["P1"].MainDeck.Skip(tail.Length).Order());
        Assert.Equal(draw ? 3 : 4, Assert.Single(result.Events, e => e.Kind == "CARDS_RECYCLED").Payload["count"]);
        Assert.Equal(1, current.PlayerExperience["P2"]);
        await Cold();
        var frame = Frame();
        if (Environment.GetEnvironmentVariable("RIFTBOUND_WEAVER_OPENING_EVIDENCE") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "normal-weaver-" + draw.ToString().ToLowerInvariant() + ".json.gz");
            Assert.False(File.Exists(path));
            await using var file = File.Create(path); await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
            await zip.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { initial, pending, finalState = current,
                commands = frame.Commands, events = frame.Events, draw, originalBlank = true, originalNextCommandMatched = true }, json));
        }
        output.WriteLine($"WEAVER_ORIGINAL_BLANK_PASS: draw={draw}; {frame.Commands.Count} commands/{frame.Events.Count} events; pending and final cold recovery, original next command and complete original replay.");

        async Task<ResolutionResult> Submit(string actor, string intent, JsonObject fields)
        {
            fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = current.Tick;
            var raw = JsonSerializer.SerializeToElement(fields, json); var command = GameCommandJsonMapper.Map(raw);
            if (command is SubmitDeckCommand submittedDeck)
                foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                    Assert.True(OfficialDeckValidator.Validate(new(submittedDeck.LegendCardNo, submittedDeck.ChampionCardNo,
                        submittedDeck.MainDeck, submittedDeck.RuneDeck, submittedDeck.Battlefields), catalog, format).IsValid);
            var result = command switch {
                SubmitDeckCommand deck => await session.SubmitDeckAsync(actor, intent, deck, raw, default),
                ReadyCommand => await session.ReadyAsync(actor, intent, raw, default),
                _ => await session.SubmitAsync(actor, intent, command, raw, default) };
            Assert.True(result.Accepted, result.ErrorMessage); current = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            return result;
        }
        MatchRecoveryFrame Frame()
        {
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
                e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence,
                e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
                new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var views = initial.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, current.Tick,
                sequence, session.SnapshotFor(id), current.Tick, sequence, session.PromptFor(id)));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(initial.RoomId, current.Tick, sequence, [], current);
            var errors = MatchRecoveryValidator.Validate(initial.RoomId, sequence, commands, events, views, current, current.Tick, spectator);
            Assert.Empty(errors);
            return new(initial.RoomId, current.Tick, sequence, commands, events, views, errors, current, initial, spectator);
        }
        async Task Cold()
        {
            var frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(Frame(), json), json)!;
            Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(frame.AuthoritativeState!));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, current, engine, default, frame.Events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            session = MatchSession.Restore(frame, engine, journal);
        }
    }
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
