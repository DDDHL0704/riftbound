using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class EmptyObservedDeckRepeatOpeningTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("SFD·122/221", true)]
    [InlineData("UNL-032/219", true)]
    [InlineData("UNL-032/219", false)]
    public async Task OrdinaryBlankOpeningExecutesPaidSecondViewBeforeAllObservedUnitPlayOffers(string card, bool draw)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "empty-observed-deck-repeat-openings.json")));
        var recipe = document.RootElement.GetProperty("branches").EnumerateArray().Single(row => row.GetProperty("cardNo").GetString() == card);
        var firstIds = recipe.GetProperty("expectedFirstView").Deserialize<string[]>()!;
        var secondIds = recipe.GetProperty("expectedSecondView").Deserialize<string[]>()!;
        var sourceId = recipe.GetProperty("spellSource").GetString()!;
        var initial = MatchReplayInitialStateBuilder.FromSeats("ordinary-empty-view-" + card + draw,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        Assert.Equal(0, initial.Tick); Assert.Equal(0, initial.Seed); Assert.Empty(initial.CardObjects);
        var engine = new CoreRuleEngine(); var journal = new Journal();
        IMatchSession session = new MatchSession(initial, engine, journal); var state = initial;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var entries = recipe.GetProperty("acceptedCommands").EnumerateArray().ToArray();
        var coldCount = 0; var nextMatched = 0; var afterCold = false;
        for (var i = 0; i < entries.Length; i++)
        {
            if (i == entries.Length - 1) await Cold();
            await Submit(entries[i].GetProperty("playerId").GetString()!,
                GameCommandJsonMapper.Map(entries[i].GetProperty("rawCommand")));
        }
        Assert.Equal(recipe.GetProperty("expectedPrefixCommands").GetInt32(), journal.Entries.Count);
        var choice = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice);
        Assert.Equal("DECK_EFFECT", choice.ChoiceWindow); Assert.Equal("P1", choice.PlayerId);
        Assert.Equal(sourceId, choice.SourceObjectId); Assert.Equal(secondIds, choice.ContextObjectIds);
        Assert.Equal(secondIds, choice.LegalObjectIds); Assert.Equal(card == "SFD·122/221" ? 1 : 0, choice.RequiredCount);
        Assert.Equal(1, choice.MaxCount); Assert.Equal(1, choice.DeckContext!.ExecutionIndex);
        Assert.Equal(1, state.StackItems.Single(item => item.SourceObjectId == sourceId).CompletedDeckExecutions);
        Assert.Null(state.PendingEffectPlay);
        Assert.DoesNotContain(firstIds, id => state.PlayerZones["P1"].MainDeck.Contains(id));
        Assert.Equal(firstIds.Order(), state.ObservedDeckPlays!.Select(observed => observed.Card.ObjectId).Order());
        Assert.Equal(firstIds.Order(), journal.Entries.SelectMany(entry => entry.Events).Where(ev => ev.Kind == "CARD_BANISHED").Select(ev => ev.Payload["sourceObjectId"] as string).Order());
        foreach (var id in secondIds)
        {
            Assert.DoesNotContain(id, JsonSerializer.Serialize(session.SnapshotFor("P2"), json));
            Assert.DoesNotContain(id, JsonSerializer.Serialize(session.PromptFor("P2"), json));
        }
        var pendingState = state; var handCount = state.PlayerZones["P1"].Hand.Count;
        var tail = state.PlayerZones["P1"].MainDeck.Skip(secondIds.Length).ToArray();
        await Cold();
        await Reject("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [secondIds[0]]));
        await Reject("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [firstIds[0]]));
        await Reject("P1", new ChooseCardsCommand("earlier-execution", choice.ChoiceWindow, [secondIds[0]]));
        if (card == "SFD·122/221") await Reject("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, draw ? [secondIds[0]] : []));
        Assert.Equal(handCount + (draw ? 1 : 0), state.PlayerZones["P1"].Hand.Count);
        Assert.Equal(tail, state.PlayerZones["P1"].MainDeck.Take(tail.Length));
        Assert.Equal(secondIds.Where(id => !draw || id != secondIds[0]).Order(), state.PlayerZones["P1"].MainDeck.Skip(tail.Length).Order());
        Assert.Single(state.PlayerZones["P1"].Graveyard, id => id == sourceId);
        Assert.Null(state.PendingCardChoice);
        await Cold();
        for (var i = 0; i < firstIds.Length; i++)
        {
            var offered = Assert.IsType<PendingEffectPlayState>(state.PendingEffectPlay);
            await Submit("P1", new ChooseCardsCommand(offered.ChoiceId, "EFFECT_PLAY", []));
        }
        Assert.Null(state.PendingEffectPlay); Assert.Empty(state.StackItems); Assert.Empty(state.TriggerQueue);
        Assert.Equal(firstIds.Order(), state.PlayerZones["P1"].Banished.Order());
        Assert.Equal(3, nextMatched); await Cold(); Assert.Equal(4, coldCount);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_EMPTY_VIEW_OPENING_EVIDENCE") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "ordinary-empty-view-" + firstIds.Length + "-" + draw.ToString().ToLowerInvariant() + ".json.gz");
            Assert.False(File.Exists(path));
            await using var file = File.Create(path); await using var compressed = new GZipStream(file, CompressionLevel.SmallestSize);
            await JsonSerializer.SerializeAsync(compressed, new { initial, pendingState, finalState = state,
                commands = Commands(), events = Events(), firstIds, secondIds, draw, originalBlank = true }, json);
        }
        output.WriteLine("ORDINARY_EMPTY_OBSERVED_REPEAT " + JsonSerializer.Serialize(new { card, draw,
            commands = journal.Entries.Count, events = Events().Length, coldCount, originalNextCommandsMatched = nextMatched,
            originalBlankSeed = 0, ruleInputs = state.RuleInputs, fullOriginalReplayMatched = true,
            formats = recipe.GetProperty("validatedFormats").Deserialize<string[]>() }, json));

        async Task Submit(string actor, GameCommand command)
        {
            if (command is SubmitDeckCommand submitted)
            {
                var deck = new OfficialDecklist(submitted.LegendCardNo, submitted.ChampionCardNo, submitted.MainDeck, submitted.RuneDeck, submitted.Battlefields);
                Assert.True(OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.CoreRules).IsValid);
                var standard = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
                if (card == "SFD·122/221")
                {
                    Assert.False(standard.IsValid);
                    Assert.Contains(standard.Errors, error => error.Contains("预判攻势", StringComparison.Ordinal));
                }
                else Assert.True(standard.IsValid, string.Join(';', standard.Errors));
            }
            var fields = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(fields, json); var intent = "empty-view-" + journal.Entries.Count;
            var expected = afterCold ? await engine.ResolveAsync(state, new(intent, actor, command.CmdType), command, default) : null;
            var result = command switch {
                SubmitDeckCommand deck => await session.SubmitDeckAsync(actor, intent, deck, raw, default),
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
            afterCold = false; state = result.State; FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        }
        async Task Reject(string actor, GameCommand command)
        {
            var result = await engine.ResolveAsync(state, new("invalid-empty-view", actor, command.CmdType), command, default);
            Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
            e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        async Task Cold()
        {
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var views = initial.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player, state.Tick, sequence,
                session.SnapshotFor(player), state.Tick, sequence, session.PromptFor(player)));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(initial.RoomId, state.Tick, sequence, [], state);
            var errors = MatchRecoveryValidator.Validate(initial.RoomId, sequence, Commands(), Events(), views, state, state.Tick, spectator);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(initial.RoomId, state.Tick, sequence, Commands(), Events(), views, errors, state, initial, spectator);
            var restored = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, json), json)!;
            Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored.AuthoritativeState!));
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(restored, engine, default));
            var oldSession = session; session = MatchSession.Restore(restored, engine, journal);
            foreach (var player in initial.Seats.Keys)
            {
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(oldSession.SnapshotFor(player)), JsonSerializer.SerializeToNode(session.SnapshotFor(player))));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(oldSession.PromptFor(player)), JsonSerializer.SerializeToNode(session.PromptFor(player))));
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
