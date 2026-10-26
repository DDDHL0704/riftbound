using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class HereLocationOpeningTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ActualBlankOpeningKeepsTheAbilityControllerAfterTakeoverAcrossColdRecoveryAndOriginalReplay()
    {
        const string actor = "here-rengar", opponent = "here-renata";
        const string merfolk = "UNL-003/219", control = "SFD·202/221", guard = "OGN·210/298";
        var initial = MatchReplayInitialStateBuilder.FromSeats("here-location-rengar-confirmation-opening-20261018",
            new Dictionary<string, string> { [opponent] = "P1", [actor] = "P2" });
        Assert.Equal(0, initial.Seed); Assert.Empty(initial.CardObjects);
        var journal = new Journal(); var engine = new CoreRuleEngine();
        IMatchSession live = new MatchSession(initial, engine, journal);
        var state = initial;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        // A new legal QA deck; none of the eleven original ordered decks changes.
        var opponentMain = new[] { "SFD·088/221", control, control, control }
            .Concat(new[] { "UNL-065/219", "UNL-076/219", "UNL-152/219", "UNL-162/219", "SFD·065/221", "SFD·069/221",
                "SFD·155/221", "SFD·159/221", "SFD·167/221", "OGN·087/298", guard, "OGN·211/298" }
                .SelectMany(no => Enumerable.Repeat(no, 3))).ToArray();
        var main = new[] { "UNL-024/219", BanishEffectPlayTests.Hunt, BanishEffectPlayTests.Hunt, BanishEffectPlayTests.Hunt }
            .Concat(new[] { "UNL-002/219", merfolk, "UNL-092/219", "UNL-093/219", "UNL-094/219", "UNL-111/219",
                "SFD·018/221", "SFD·098/221", "SFD·099/221", "OGN·010/298", "OGN·013/298", "OGN·036/298" }
                .SelectMany(no => Enumerable.Repeat(no, 3))).ToArray();
        Assert.Equal(40, opponentMain.Length);
        await Submit(opponent, new SubmitDeckCommand("SFD·201/221", "SFD·088/221", opponentMain,
            Enumerable.Repeat("UNL-R03", 6).Concat(Enumerable.Repeat("UNL-R06", 6)).ToArray(),
            ["OGN·278/298", "OGN·275/298", "OGN·294/298"]));
        await Submit(actor, new SubmitDeckCommand("UNL-183/219", "UNL-024/219", main,
            Enumerable.Repeat("UNL-R01", 6).Concat(Enumerable.Repeat("UNL-R04", 6)).ToArray(),
            ["OGN·278/298", "OGN·275/298", "OGN·294/298"]));
        await Submit(opponent, new ReadyCommand()); await Submit(actor, new ReadyCommand());
        foreach (var player in new[] { actor, opponent }) await Submit(player, new MulliganCommand([]));
        string? enemy = null; var moved = false;
        for (var turn = 0; turn < 70 && !moved; turn++)
        {
            if (state.TurnPlayerId == opponent)
            {
                if (enemy is null && Hand(opponent, guard) is { } hand && Runes(opponent).Count >= 2)
                {
                    await TapAll(opponent);
                    await Submit(opponent, new PlayCardCommand(hand, guard, [], Destination: "BASE"));
                    await Drain(); enemy = hand;
                }
                if (enemy is not null && !state.CardObjects[enemy].IsExhausted && Runes(opponent).Count >= 3
                    && Runes(actor).Count >= 5 && Hand(actor, merfolk) is not null && Hand(actor, BanishEffectPlayTests.Hunt) is not null
                    && Hand(opponent, control) is not null)
                {
                    var field = state.BattlefieldStates.Keys.First(id => state.CardObjects[id].OwnerId == actor);
                    await Submit(opponent, new MoveUnitCommand(enemy, "BASE", "BATTLEFIELD:" + field, []));
                    await Drain();
                    await TapAll(opponent);
                    await Submit(opponent, new RecycleRuneCommand(Runes(opponent).First()));
                    await Submit(opponent, new HideCardCommand(Hand(opponent, control)!, control, "BATTLEFIELD:" + field, ["STANDBY_A"]));
                    moved = true;
                }
            }
            await Submit(state.TurnPlayerId, new EndTurnCommand());
        }
        Assert.True(moved); Assert.NotNull(enemy); Assert.Equal(actor, state.TurnPlayerId);
        var battlefield = state.ObjectLocations[enemy!].BattlefieldObjectId!;
        var source = Hand(actor, merfolk)!; var hunt = Hand(actor, BanishEffectPlayTests.Hunt)!;
        var controlCard = state.PlayerZones[opponent].Battlefields.Single(id => state.CardObjects[id].CardNo == control);
        await TapAll(actor);
        await Submit(actor, new RecycleRuneCommand(Runes(actor).First(id => state.CardObjects[id].CardNo == "UNL-R04")));
        await Submit(actor, new PlayCardCommand(source, merfolk, [enemy!], Destination: "BASE")); await Drain();
        await Submit(actor, new PlayCardCommand(hunt, BanishEffectPlayTests.Hunt, [source])); await Drain();
        await Submit(actor, new PlayCardCommand(source, merfolk, [], Destination: "BATTLEFIELD:" + battlefield));
        var legendTrigger = Assert.Single(state.TriggerQueue, t => t.UnitPlayMight is not null);
        var ownTrigger = Assert.Single(state.TriggerQueue, t => t.PermanentPlayStackItemId is not null);
        Assert.Equal(2, state.TriggerQueue.Count);
        var pendingOrderAt = journal.Entries.Count;
        await OriginalNextAfterCold(actor, new OrderTriggersCommand(OrderedTriggerIds: [legendTrigger.TriggerId, ownTrigger.TriggerId]));
        var choice = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice);
        Assert.Equal(source, choice.SourceObjectId);
        Assert.Equal(new[] { enemy! }, choice.LegalObjectIds);
        await Submit(actor, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [enemy!]));
        var legendChoice = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice);
        Assert.Equal(legendTrigger.SourceObjectId, legendChoice.SourceObjectId);
        var pendingRengarChoiceAt = journal.Entries.Count;
        await OriginalNextAfterCold(actor, new ChooseCardsCommand(legendChoice.ChoiceId, legendChoice.ChoiceWindow, [source]));
        var confirmedRengarAt = journal.Entries.Count;
        await OriginalNextAfterCold(state.PriorityPlayerId!, new PassPriorityCommand());
        await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
        var binding = Assert.Single(state.StackItems).PermanentPlaySource!;
        await Submit(actor, new PassPriorityCommand());
        await Submit(opponent, new PlayCardCommand(controlCard, control, [source])); await Top();
        Assert.Equal(opponent, state.CardObjects[source].ControllerId);
        Assert.Equal(actor, state.CardObjects[source].OwnerId);
        Assert.Equal(battlefield, state.ObjectLocations[source].BattlefieldObjectId);
        Assert.Equal(battlefield, state.ObjectLocations[enemy!].BattlefieldObjectId);
        Assert.Equal(binding, Assert.Single(state.StackItems).PermanentPlaySource);
        Assert.Equal(actor, Assert.Single(state.StackItems).ControllerId);
        Assert.Equal(binding.Generation, state.CardObjects[source].ObjectGeneration);
        var pendingAt = journal.Entries.Count;
        await Cold();
        for (var nextIndex = 0; nextIndex < 2; nextIndex++)
        {
            var next = new PassPriorityCommand(); var nextPlayer = state.PriorityPlayerId!;
            var expected = await engine.ResolveAsync(state, new("here-opening-" + journal.Entries.Count, nextPlayer, next.CmdType), next, default);
            var actual = await Submit(nextPlayer, next);
            Assert.True(expected.Accepted, expected.ErrorMessage);
            Assert.Equal(MatchStateHasher.Hash(expected.State), MatchStateHasher.Hash(actual.State));
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(actual.Events)));
            await Cold();
        }
        await Drain(); await Cold();
        output.WriteLine("HERE_OPENING_TARGET " + JsonSerializer.Serialize(new {
            target = state.CardObjects[enemy!], location = state.ObjectLocations[enemy!],
            damageEvents = journal.Entries.SelectMany(e => e.Events).Where(e => e.Kind == "DAMAGE_APPLIED") }));
        Assert.Equal(2, state.CardObjects[enemy!].Damage);
        Assert.Contains(enemy!, state.PlayerZones[opponent].Battlefields);
        Assert.Contains(source, state.PlayerZones[opponent].Battlefields);
        Assert.Contains(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "DAMAGE_APPLIED"
            && Equals(e.Payload.GetValueOrDefault("targetObjectId"), enemy) && Convert.ToInt32(e.Payload.GetValueOrDefault("damage")) == 2);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_HERE_OPENING_EVIDENCE") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "here-controller-rengar-confirmation-opening.json"), JsonSerializer.Serialize(new {
                schemaVersion = 1, openingSeed = 0, sourceRoomId = initial.RoomId, seats = initial.Seats,
                expectedActor = actor, expectedOpponent = opponent, expectedSource = source, expectedSourceCardNo = merfolk,
                expectedEnemy = enemy, expectedBattlefield = battlefield, pendingHereCommandCount = pendingAt,
                pendingOrderCommandCount = pendingOrderAt, pendingRengarChoiceCommandCount = pendingRengarChoiceAt,
                confirmedRengarCommandCount = confirmedRengarAt,
                acceptedCommands = journal.Entries.Select(e => new { playerId = e.PlayerId, clientIntentId = e.ClientIntentId,
                    commandType = e.CommandType, rawCommand = e.RawCommand!.Value }),
                scope = "Fresh room and legal Rengar/Renata decks from original blank seed0, including mandatory independent legend confirmation and own/legend ordering before Takeover. Three Rengar and both original Here cold boundaries, five original next commands, original ability controller,damage2 and full replay. Prior83-command fixture and accepted histories remain unchanged. No whole-card or base-positive certification."
            }, json));
        }
        output.WriteLine($"HERE_CONTROLLER_OPENING_PASS: {journal.Entries.Count} commands,{Frame().Events.Count} events; source owner differs from controller, same battlefield,damage2,original next,cold and full original replay.");

        string? Hand(string id, string no) => state.PlayerZones[id].Hand.FirstOrDefault(card => state.CardObjects[card].CardNo == no);
        IReadOnlyList<string> Runes(string id) => state.PlayerZones[id].Base.Where(card => state.CardObjects[card].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
        async Task TapAll(string id)
        {
            foreach (var card in Runes(id).Where(card => !state.CardObjects[card].IsExhausted).ToArray()) await Submit(id, new TapRuneCommand(card));
        }
        async Task Top()
        {
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
        }
        async Task OriginalNextAfterCold(string id, GameCommand command)
        {
            await Cold();
            var expected = await engine.ResolveAsync(state, new("here-opening-" + journal.Entries.Count, id, command.CmdType), command, default);
            var actual = await Submit(id, command);
            Assert.True(expected.Accepted, expected.ErrorMessage);
            Assert.Equal(MatchStateHasher.Hash(expected.State), MatchStateHasher.Hash(actual.State));
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(actual.Events)));
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
            throw new InvalidOperationException("The finite Here opening did not finish its current window.");
        }
        async Task<ResolutionResult> Submit(string id, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            node["promptId"] = live.PromptFor(id).PromptId; node["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(node); var intent = "here-opening-" + journal.Entries.Count;
            var result = command switch {
                SubmitDeckCommand deck => await live.SubmitDeckAsync(id, intent, deck, raw, default),
                ReadyCommand => await live.ReadyAsync(id, intent, raw, default),
                _ => await live.SubmitAsync(id, intent, command, raw, default)
            };
            Assert.True(result.Accepted, $"{journal.Entries.Count} {id} {command.CmdType}: {result.ErrorMessage}"); state = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result); return result;
        }
        MatchRecoveryFrame Frame()
        {
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id,
                state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand?.Clone(),
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
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
            foreach (var id in state.Seats.Keys)
            {
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(frame.PlayerViews[id].Snapshot, json), JsonSerializer.SerializeToNode(live.SnapshotFor(id), json)));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(frame.PlayerViews[id].Prompt, json), JsonSerializer.SerializeToNode(live.PromptFor(id), json)));
            }
        }
    }
    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
