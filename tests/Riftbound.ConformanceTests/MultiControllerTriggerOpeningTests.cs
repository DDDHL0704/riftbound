using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class MultiControllerTriggerOpeningTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("P1", false, false)]
    [InlineData("P1", false, true)]
    [InlineData("P1", true, false)]
    [InlineData("P1", true, true)]
    [InlineData("P2", false, false)]
    [InlineData("P2", false, true)]
    [InlineData("P2", true, false)]
    [InlineData("P2", true, true)]
    public async Task OrdinaryBlankOpeningKeepsBothControllersChoicesAcrossColdRecoveryAndOriginalReplay(
        string caster, bool reverseFirst, bool reverseSecond)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "multi-controller-trigger-openings.json")));
        var fixture = document.RootElement;
        var recipe = fixture.GetProperty("branches").EnumerateArray().Single(branch => branch.GetProperty("caster").GetString() == caster);
        var initial = MatchReplayInitialStateBuilder.FromSeats($"ordinary-controller-{caster}-{reverseFirst}-{reverseSecond}",
            fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        Assert.Equal(0, initial.Seed); Assert.Equal(0, initial.Tick); Assert.Empty(initial.CardObjects);
        var engine = new CoreRuleEngine(); var journal = new Journal();
        IMatchSession session = new MatchSession(initial, engine, journal);
        var state = initial;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        foreach (var entry in recipe.GetProperty("acceptedCommands").EnumerateArray())
            await Submit(entry.GetProperty("playerId").GetString()!,
                GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
        Assert.Equal(recipe.GetProperty("expectedPrefixCommands").GetInt32(), journal.Entries.Count);
        Assert.Equal(caster, state.TurnPlayerId);
        Assert.Equal(4, state.TriggerQueue.Count);
        Assert.Single(state.TriggerQueue.Select(trigger => trigger.EnqueuedAtTick).Distinct());
        foreach (var player in initial.Seats.Keys)
        {
            Assert.Equal(2, state.TriggerQueue.Count(trigger => trigger.ControllerId == player));
            Assert.Equal(2, state.PlayerZones[player].Base.Count(id => state.CardObjects[id].CardNo == "ARC-006/006"));
        }
        Assert.All(state.TriggerQueue, trigger => Assert.NotNull(trigger.DeathObserver));
        await Cold();

        var secondPlayer = caster == "P1" ? "P2" : "P1";
        var first = OrderFor(caster, reverseFirst);
        await Reject(secondPlayer, first);
        var badFirst = first.OrderedTriggerIds!.ToArray(); ReverseOwned(badFirst, secondPlayer);
        await Reject(caster, new OrderTriggersCommand(badFirst));
        await Submit(caster, first);
        Assert.Equal(4, state.TriggerQueue.Count); Assert.Empty(state.StackItems);
        Assert.All(state.TriggerQueue, trigger => Assert.Equal(trigger.ControllerId == caster, trigger.ControllerOrderConfirmed));
        Assert.False(session.PromptFor(caster).Actionable);
        Assert.Contains(session.PromptFor(secondPlayer).Candidates!, candidate => candidate.Action == CommandTypes.OrderTriggers && candidate.Enabled);
        await Cold();
        var second = OrderFor(secondPlayer, reverseSecond);
        await Reject(caster, second);
        var badSecond = second.OrderedTriggerIds!.ToArray(); ReverseOwned(badSecond, caster);
        await Reject(secondPlayer, new OrderTriggersCommand(badSecond));
        await Submit(secondPlayer, second);
        Assert.Empty(state.TriggerQueue);
        Assert.Equal(second.OrderedTriggerIds!.Select(id => "ordered-" + id), state.StackItems.Reverse().Select(item => item.StackItemId));
        await Cold();
        for (var step = 0; step < 12 && state.StackItems.Count > 0; step++)
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Empty(state.StackItems); Assert.Empty(state.TriggerQueue);
        foreach (var player in initial.Seats.Keys)
            Assert.Equal(2, state.PlayerZones[player].Base.Count(id => state.CardObjects[id].TokenFactoryCardNo is not null));
        await Cold();
        output.WriteLine("ORDINARY_MULTI_CONTROLLER " + JsonSerializer.Serialize(new {
            caster, reverseFirst, reverseSecond, actualRuleInputs = state.RuleInputs,
            commands = journal.Entries.Count, events = journal.Entries.Sum(entry => entry.Events.Count),
            originalBlankSeed = initial.Seed, coldCheckpoints = 4, bothDeckFormatsValidated = true,
            allOriginalActionAndEventReplayChecksMatched = true
        }));

        OrderTriggersCommand OrderFor(string player, bool reverse)
        {
            var candidate = session.PromptFor(player).Candidates!.Single(candidate => candidate.Action == CommandTypes.OrderTriggers && candidate.Enabled);
            var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
            var ids = metadata.GetProperty("orderedTriggerIds").Deserialize<string[]>()!;
            Assert.Equal(state.TriggerQueue.Where(trigger => trigger.ControllerId == player).Select(trigger => trigger.TriggerId).Order(),
                metadata.GetProperty("reorderableTriggerIds").Deserialize<string[]>()!.Order());
            if (reverse) ReverseOwned(ids, player);
            return new(OrderedTriggerIds: ids);
        }
        void ReverseOwned(string[] ids, string player)
        {
            var positions = Enumerable.Range(0, ids.Length).Where(index => state.TriggerQueue.Single(trigger => trigger.TriggerId == ids[index]).ControllerId == player).ToArray();
            Assert.Equal(2, positions.Length);
            (ids[positions[0]], ids[positions[1]]) = (ids[positions[1]], ids[positions[0]]);
        }
        async Task Reject(string player, OrderTriggersCommand command)
        {
            var before = MatchStateHasher.Hash(state);
            var result = await engine.ResolveAsync(state, new("invalid-ordinary-controller", player, command.CmdType), command, default);
            Assert.False(result.Accepted); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        }
        async Task Submit(string player, GameCommand command)
        {
            if (command is SubmitDeckCommand submittedDeck)
                foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                    Assert.True(OfficialDeckValidator.Validate(new(submittedDeck.LegendCardNo, submittedDeck.ChampionCardNo, submittedDeck.MainDeck, submittedDeck.RuneDeck, submittedDeck.Battlefields), catalog, format).IsValid);
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(node, json); var intent = "ordinary-controller-" + journal.Entries.Count;
            var result = command switch {
                SubmitDeckCommand deck => await session.SubmitDeckAsync(player, intent, deck, raw, default),
                ReadyCommand => await session.ReadyAsync(player, intent, raw, default),
                _ => await session.SubmitAsync(player, intent, command, raw, default)
            };
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        }
        async Task Cold()
        {
            var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId, entry.CommandType, entry.RawCommand,
                entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage) { TypedCommand = entry.TypedCommand, RuleInputs = entry.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(entry => entry.Events.Select((gameEvent, index) => new RecoveredEvent(entry.StartedEventSequence + index + 1, entry.CompletedTick, index, gameEvent))).ToArray();
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var views = initial.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player, state.Tick, sequence,
                session.SnapshotFor(player), state.Tick, sequence, session.PromptFor(player)));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(initial.RoomId, state.Tick, sequence, [], state);
            var errors = MatchRecoveryValidator.Validate(initial.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(initial.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator);
            var restored = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, json), json)!;
            Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored.AuthoritativeState!));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, state, engine, default, events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            session = MatchSession.Restore(restored, engine, journal);
        }
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
