using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class BanishEffectPlayOpeningTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ActualBlankOpeningRetainsMandatoryOwnerPlayAcrossColdRecoveryAndOriginalReplay()
    {
        const string actor = "paired-339c-b", opponent = "paired-339c-a";
        var initial = MatchReplayInitialStateBuilder.FromSeats("banish-effect-play-opening-20261018",
            new Dictionary<string, string> { [opponent] = "P1", [actor] = "P2" });
        Assert.Equal(0, initial.Seed); Assert.Empty(initial.CardObjects);
        var journal = new Journal();
        var engine = new CoreRuleEngine();
        IMatchSession live = new MatchSession(initial, engine, journal);
        var state = initial;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var opponentDeck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "jhin-lowcurve").Decklist;
        var main = new[] { "UNL-024/219", BanishEffectPlayTests.Hunt, BanishEffectPlayTests.Hunt, BanishEffectPlayTests.Hunt }
            .Concat(new[] { "UNL-002/219", "UNL-003/219", "UNL-092/219", "UNL-093/219", "UNL-094/219", "UNL-111/219",
                "SFD·018/221", "SFD·098/221", "SFD·099/221", "OGN·010/298", "OGN·013/298", "OGN·036/298" }
                .SelectMany(no => Enumerable.Repeat(no, 3))).ToArray();
        Swap("UNL-111/219", 32); Swap(BanishEffectPlayTests.Hunt, 31);
        var ownDeck = new SubmitDeckCommand("UNL-183/219", "UNL-024/219", main,
            Enumerable.Repeat("UNL-R01", 6).Concat(Enumerable.Repeat("UNL-R04", 6)).ToArray(),
            ["OGN·278/298", "OGN·275/298", "OGN·294/298"]);
        await Submit(opponent, new SubmitDeckCommand(opponentDeck.LegendCardNo, opponentDeck.ChampionCardNo,
            opponentDeck.MainDeck, opponentDeck.RuneDeck, opponentDeck.Battlefields));
        await Submit(actor, ownDeck);
        await Submit(opponent, new ReadyCommand()); await Submit(actor, new ReadyCommand());
        foreach (var player in new[] { actor, opponent }) await Submit(player, new MulliganCommand([]));

        string? enemy = null;
        var moved = false;
        for (var i = 0; i < 70 && !moved; i++)
        {
            if (state.TurnPlayerId == opponent)
            {
                if (enemy is null && Hand(opponent, "UNL-003/219") is { } enemyHand && Runes(opponent).Count >= 2)
                {
                    await TapAll(opponent);
                    await Submit(opponent, new PlayCardCommand(enemyHand, "UNL-003/219", [], Destination: "BASE"));
                    await Drain(); enemy = enemyHand;
                }
                if (enemy is not null && !state.CardObjects[enemy].IsExhausted
                    && Hand(actor, BanishEffectPlayTests.Hunt) is not null && Hand(actor, "UNL-111/219") is not null
                    && Runes(actor).Count >= 4 && Runes(actor).Any(id => state.CardObjects[id].CardNo == "UNL-R04"))
                {
                    var field = state.BattlefieldStates.Keys.First(id => state.CardObjects[id].OwnerId == actor);
                    await Submit(opponent, new MoveUnitCommand(enemy, "BASE", "BATTLEFIELD:" + field, []));
                    await Drain(); moved = true;
                }
            }
            await Submit(state.TurnPlayerId, new EndTurnCommand());
        }
        Assert.True(moved); Assert.NotNull(enemy); Assert.Equal(actor, state.TurnPlayerId);
        var battlefield = state.ObjectLocations[enemy!].BattlefieldObjectId!;
        Assert.Equal(opponent, state.BattlefieldStates[battlefield].ControllerId);
        var source = Hand(actor, "UNL-111/219")!;
        var hunt = Hand(actor, BanishEffectPlayTests.Hunt)!;
        await TapAll(actor);
        await Submit(actor, new RecycleRuneCommand(Runes(actor).First(id => state.CardObjects[id].CardNo == "UNL-R04")));
        // The separate legend instruction has a real, already visible target.
        await Submit(actor, new PlayCardCommand(source, "UNL-111/219", [enemy!], Destination: "BASE"));
        await Drain();
        await Submit(actor, new PlayCardCommand(hunt, BanishEffectPlayTests.Hunt, [source]));
        await Drain();
        Assert.Equal(actor, state.PendingEffectPlay!.PlayerId);
        Assert.Equal("BANISHED", state.ObjectLocations[source].Zone);
        var pendingAt = journal.Entries.Count;
        await Cold();
        var originalNext = new PlayCardCommand(source, "UNL-111/219", [enemy!], Destination: "BATTLEFIELD:" + battlefield);
        var expected = await engine.ResolveAsync(state, new("banish-opening-" + journal.Entries.Count, actor, originalNext.CmdType), originalNext, default);
        var played = await Submit(actor, originalNext);
        Assert.True(expected.Accepted, expected.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(expected.State), MatchStateHasher.Hash(played.State));
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(played.Events)));
        Assert.Null(state.PendingEffectPlay);
        Assert.Equal(battlefield, state.ObjectLocations[source].BattlefieldObjectId);
        var afterChildAt = journal.Entries.Count;
        await Cold(); await Drain(); await Cold();
        Assert.Contains(hunt, state.PlayerZones[actor].Graveyard);
        Assert.Contains(source, state.PlayerZones[actor].Battlefields);
        Assert.Equal(actor, state.CardObjects[source].OwnerId);
        var final = Frame();
        if (Environment.GetEnvironmentVariable("RIFTBOUND_BANISH_PLAY_EVIDENCE") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "banish-owner-current-opening.json"), JsonSerializer.Serialize(new {
                schemaVersion = 1, openingSeed = 0, sourceRoomId = initial.RoomId, seats = initial.Seats,
                expectedActor = actor, expectedOpponent = opponent, expectedSource = source, expectedSourceCardNo = "UNL-111/219",
                expectedHunt = hunt, expectedEnemy = enemy, expectedBattlefield = battlefield,
                pendingOwnerPlayCommandCount = pendingAt, afterChildCommandCount = afterChildAt,
                acceptedCommands = journal.Entries.Select(e => new { playerId = e.PlayerId, clientIntentId = e.ClientIntentId,
                    commandType = e.CommandType, rawCommand = e.RawCommand!.Value }),
                scope = "New legal Rengar deck from original blank seed0, base unit, ordinary Hunting Rhythm, owner selects an enemy-controlled battlefield; pending owner-play and child-response cold recovery, original next command and full original replay. Other legend/own-ability combinations remain separate."
            }, json));
        }
        output.WriteLine($"BANISH_OWNER_ORIGINAL_OPENING_PASS: {final.Commands.Count} commands,{final.Events.Count} events; pending owner play,{afterChildAt} child response, original next and complete replay.");

        void Swap(string no, int index)
        {
            var from = Array.IndexOf(main, no); Assert.True(from >= 0);
            (main[from], main[index]) = (main[index], main[from]);
        }
        string? Hand(string player, string no) => state.PlayerZones[player].Hand.FirstOrDefault(id => state.CardObjects[id].CardNo == no);
        IReadOnlyList<string> Runes(string player) => state.PlayerZones[player].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
        async Task TapAll(string player)
        {
            var ready = Runes(player).Where(id => !state.CardObjects[id].IsExhausted).ToArray();
            if (ready.Length > 0) await Submit(player, new TapRuneCommand(ready[0], ready));
        }
        async Task Drain()
        {
            for (var count = 0; count < 40; count++)
            {
                if (state.PendingEffectPlay is not null) return;
                Assert.Null(state.PendingCardChoice);
                if (state.StackItems.Count > 0) await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
                else if (state.FocusPlayerId is { } focus) await Submit(focus, new PassFocusCommand());
                else return;
            }
            throw new InvalidOperationException("The finite opening did not finish its current window.");
        }
        async Task<ResolutionResult> Submit(string player, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            node["promptId"] = live.PromptFor(player).PromptId; node["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(node); var id = "banish-opening-" + journal.Entries.Count;
            var result = command switch {
                SubmitDeckCommand deck => await live.SubmitDeckAsync(player, id, deck, raw, default),
                ReadyCommand => await live.ReadyAsync(player, id, raw, default),
                _ => await live.SubmitAsync(player, id, command, raw, default)
            };
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            return result;
        }
        MatchRecoveryFrame Frame()
        {
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
                state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand?.Clone(),
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, journal.Entries[^1].Events, state);
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
            return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }
        async Task Cold()
        {
            var frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(Frame(), json), json)!;
            Assert.True(frame.ValidationErrors.Count == 0, string.Join("; ", frame.ValidationErrors));
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default));
            live = MatchSession.Restore(frame, engine, journal);
            foreach (var player in state.Seats.Keys)
            {
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(frame.PlayerViews[player].Snapshot, json), JsonSerializer.SerializeToNode(live.SnapshotFor(player), json)), "Snapshot differs for " + player);
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(frame.PlayerViews[player].Prompt, json), JsonSerializer.SerializeToNode(live.PromptFor(player), json)), "Prompt differs for " + player);
            }
        }
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
