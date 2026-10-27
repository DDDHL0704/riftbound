using System.Text.Json;
using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MovementDiscardDuelContinuationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    public Task TriggerOnlyArrivalChainKeepsOriginalFocusAfterEveryColdRestore(int handCount, int merchants)
        => DriveArrival(handCount, merchants, MovementDiscardTests.Merchant, false);

    [Theory]
    [InlineData("SFD·130/221")]
    [InlineData("SFD·048/221")]
    [InlineData("UNL-115/219")]
    [InlineData("UNL-127/219")]
    public Task OtherMovementTriggerConsumersKeepOriginalFocus(string cardNo)
        => DriveArrival(0, 1, cardNo, false);

    [Fact]
    public Task ResponseAddedAboveTheMovementTriggerPreservesItsOriginalFocus()
        => DriveArrival(3, 1, MovementDiscardTests.Merchant, true);

    private static async Task DriveArrival(int handCount, int merchants, string cardNo, bool response)
    {
        var initial = MovementDiscardTests.Position(handCount, "BASE");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["D"] = initial.CardObjects["D"] with { CardNo = cardNo },
            ["BF2"] = initial.CardObjects["BF2"] with { ControllerId = "P1" },
            ["SHADOW"] = new("SHADOW", cardNo: "UNL-194/219", ownerId: "P1", controllerId: "P1",
                power: 4, tags: [CardObjectTags.UnitCard])
        };
        var zones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations)
        { ["SHADOW"] = new("P1", "BATTLEFIELD", "BF2") };
        zones["P1"] = zones["P1"] with { Battlefields = [..zones["P1"].Battlefields, "SHADOW"] };
        if (response)
        {
            // This branch checks focus with a surviving merchant that can then
            // be stunned. A neutral field keeps the response's printed 1 damage;
            // Void Gate's lethal response is covered by MovementDiscardTests.
            cards["BF2"] = cards["BF2"] with { CardNo = "OGN·278/298" };
            cards["RESPONSE"] = new("RESPONSE", cardNo: "OGN·133/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
            zones["P1"] = zones["P1"] with { Hand = [..zones["P1"].Hand, "RESPONSE"] };
            locations["RESPONSE"] = new("P1", "HAND");
        }
        var sources = new[] { "D", "D2" }.Take(merchants).ToArray();
        if (merchants == 2)
        {
            cards["D2"] = cards["D"] with { ObjectId = "D2" };
            zones["P2"] = zones["P2"] with { Base = [..zones["P2"].Base, "D2"] };
            locations["D2"] = new("P2", "BASE");
        }
        initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2", CardObjects = cards,
            PlayerZones = zones, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(10, 10) } };
        var engine = new CoreRuleEngine();
        var journal = new Journal();
        var session = new MatchSession(initial, engine, journal);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId,
            e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence,
            e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((value, index) =>
            new RecoveredEvent(e.StartedEventSequence + index + 1, e.CompletedTick, index, value))).ToArray();
        async Task<ResolutionResult> Submit(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
            var result = await session.SubmitAsync(player, "discard-duel-" + journal.Entries.Count,
                GameCommandJsonMapper.Map(raw), raw, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            var restored = OfficialInsightAndSpellLockTests.Restore(result.State);
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored));
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var views = restored.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, restored.Tick,
                sequence, result.Snapshots[id], restored.Tick, sequence, result.Prompts[id]));
            var commands = Commands(); var events = Events();
            var errors = MatchRecoveryValidator.Validate(restored.RoomId, sequence, commands, events, views, restored, restored.Tick);
            Assert.Empty(errors);
            // This is an explicit midgame fixture. Its checkpoint replay is
            // separate from the empty-opening audit exercised below.
            var frame = new MatchRecoveryFrame(restored.RoomId, restored.Tick, sequence, commands, events, views,
                errors, restored) { OpeningSeed = initial.Seed };
            var checkpointReplay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, restored, engine, default, events);
            Assert.True(checkpointReplay.IsMatch, string.Join("; ", checkpointReplay.Errors));
            session = MatchSession.Restore(frame, engine, journal);
            if (restored.DuelChainFocus is { } continuation)
            {
                foreach (var forged in new[] { continuation with { FocusPlayerId = "FOREIGN" },
                    continuation with { PassedFocusPlayerIds = ["P2"] },
                    continuation with { PassedFocusPlayerIds = ["P1", "P1"] } })
                    Assert.Contains(OfficialInsightAndSpellLockTests.Errors(restored with { DuelChainFocus = forged }),
                        error => error.Contains("duel chain focus", StringComparison.Ordinal));
                var changedFocus = restored with { DuelChainFocus = continuation with { FocusPlayerId = "P1" } };
                var mismatch = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, changedFocus, engine, default, events);
                Assert.False(mismatch.IsMatch);
                var missing = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands,
                    restored with { DuelChainFocus = null }, engine, default, events);
                Assert.False(missing.IsMatch);
            }
            return result;
        }
        var current = await Submit("P2", new MoveUnitCommand("D", "BASE", "BATTLEFIELD:BF2", [], SourceObjectIds: sources));
        if (merchants == 2)
            current = await Submit("P2", new OrderTriggersCommand(OrderedTriggerIds:
                current.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        if (response)
        {
            current = await Submit("P2", new PassPriorityCommand());
            current = await Submit("P1", new PlayCardCommand("RESPONSE", "OGN·133/298", []));
            current = await Submit(current.State.PriorityPlayerId!, new PassPriorityCommand());
            current = await Submit(current.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal(1, current.State.CardObjects["D"].Damage);
            Assert.Equal("P2", current.State.DuelChainFocus!.FocusPlayerId);
        }
        Assert.Equal(merchants, current.State.StackItems.Count);
        for (var index = 0; index < merchants; index++)
        {
            current = await Submit(current.State.PriorityPlayerId!, new PassPriorityCommand());
            current = await Submit(current.State.PriorityPlayerId!, new PassPriorityCommand());
            if (current.State.PendingHandChoice is { } choice)
            {
                Assert.Equal(TimingStates.SpellDuelClosed, current.State.TimingState);
                Assert.True(current.State.SpellDuelState.IsActive);
                Assert.Equal("P2", choice.PlayerId);
                current = await Submit("P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                    [choice.LegalObjectIds[0]]));
            }
            if (index < merchants - 1)
            {
                Assert.Equal("P2", current.State.PriorityPlayerId);
                Assert.Null(current.State.FocusPlayerId);
                Assert.Single(current.State.StackItems);
            }
        }
        Assert.Empty(current.State.StackItems);
        Assert.Null(current.State.PendingHandChoice);
        Assert.Equal(TimingStates.SpellDuelOpen, current.State.TimingState);
        // CN 340.2.a / 346.1: a chain opened by a trigger does not pass focus.
        Assert.Equal("P2", current.State.FocusPlayerId);
        current = await Submit("P2", new PassFocusCommand());
        Assert.Equal("P1", current.State.FocusPlayerId);
        Assert.Contains(CommandTypes.ActivateAbility, current.Prompts["P1"].EnabledActions());
        current = await Submit("P1", new ActivateAbilityCommand("SHADOW", P4ActivatedAbilityCatalog.ShadowStunAbilityId, ["D"]));
        current = await Submit(current.State.PriorityPlayerId!, new PassPriorityCommand());
        current = await Submit(current.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Contains("STUNNED", current.State.CardObjects["D"].UntilEndOfTurnEffects);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), current.State, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(current);
    }

    [Fact]
    public async Task OriginalSeed31EmptyOpeningColdRecoversTheDiscardAndReplaysThroughScoreVictory()
    {
        var original = typeof(FullGameEndToEndTests);
        object Call(string name, params object[] args) => original.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = (OfficialDecklist)Call("BuildStandbyReactionOfficialDeck", catalog);
        var initial = (MatchState)Call("BuildSeatedInitialState", "b0-full-game-standby-reaction-shadow-score-replay-room-31", 31L);
        Assert.Equal(0, initial.Tick); Assert.Equal(1, initial.TurnNumber); Assert.Empty(initial.CardObjects);
        var journal = new Journal();
        var stage = await (ValueTask<(MatchSession, ResolutionResult, ResolutionResult, ResolutionResult,
            ResolutionResult, ResolutionResult, ResolutionResult, ResolutionResult, string, string)>)Call(
                "DriveOfficialDecksToStandbyReactionShadowBattleCloseAsync", initial, journal, deck, deck);
        var result = await (ValueTask<ResolutionResult>)Call("DriveBattleCloseToScoreVictoryAsync", stage.Item1, stage.Item8,
            "b0-standby-reaction-shadow-score");
        Call("AssertScoreVictory", result);
        Assert.DoesNotContain(journal.Entries, e => !e.Accepted || e.CommandType.StartsWith("DEV_", StringComparison.Ordinal)
            || e.CommandType == CommandTypes.Surrender);
        var choices = journal.Entries.Select((e, i) => (e, i)).Where(x => x.e.AuthoritativeState.PendingHandChoice is not null
            && x.e.AuthoritativeState.DuelChainFocus is not null).Select(x => x.i).ToArray();
        Assert.NotEmpty(choices);
        foreach (var index in choices.Append(journal.Entries.Count - 1))
        {
            var entry = journal.Entries[index];
            var state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(entry.AuthoritativeState))!;
            if (MatchStateHasher.Hash(entry.AuthoritativeState) != MatchStateHasher.Hash(state))
            {
                var expected = JsonSerializer.SerializeToElement(entry.AuthoritativeState);
                var actual = JsonSerializer.SerializeToElement(state);
                var differences = expected.EnumerateObject().Where(p => !JsonElement.DeepEquals(p.Value, actual.GetProperty(p.Name)))
                    .Select(p => p.Name).ToArray();
                var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_TRIGGER_DUEL_EVIDENCE");
                if (evidence is not null)
                {
                    Directory.CreateDirectory(evidence);
                    await File.WriteAllTextAsync(Path.Combine(evidence, "state-roundtrip-mismatch.json"),
                        JsonSerializer.Serialize(new { index, entry.CommandType, differences, expected, actual }));
                }
                Assert.Fail("cold state JSON roundtrip differs at " + string.Join(", ", differences));
            }
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
            var prefix = journal.Entries.Take(index + 1).ToArray();
            var commands = prefix.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
                { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
            var events = prefix.SelectMany(e => e.Events.Select((value, order) =>
                new RecoveredEvent(e.StartedEventSequence + order + 1, e.CompletedTick, order, value))).ToArray();
            var sequence = entry.CompletedEventSequence;
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence,
                entry.Snapshots[id], state.Tick, sequence, entry.Prompts[id]));
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, commands, events, views, errors,
                state, initial, MatchReplayRedactor.BuildSpectatorFrame(entry)) { OpeningSeed = initial.Seed };
            Assert.Equal(9, frame.SpectatorReplayFrame!.SpectatorSnapshot.Timing["winningScore"]);
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
            var recovered = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance,
                new Store(frame)).GetOrCreateAsync(state.RoomId, default);
            Assert.NotNull(recovered);
            var spectator = frame.SpectatorReplayFrame;
            var forged = frame with { SpectatorReplayFrame = spectator with { SpectatorSnapshot = spectator.SpectatorSnapshot with {
                Timing = new Dictionary<string, object?>(spectator.SpectatorSnapshot.Timing) { ["winningScore"] = 8 } } } };
            var rejected = await Assert.ThrowsAsync<MatchSessionException>(async () =>
                await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance, new Store(forged))
                    .GetOrCreateAsync(state.RoomId, default));
            Assert.Contains("winning score", rejected.Message, StringComparison.Ordinal);
            if (index + 1 < journal.Entries.Count)
            {
                var next = journal.Entries[index + 1];
                var resumed = await recovered.SubmitAsync(next.PlayerId, next.ClientIntentId,
                    GameCommandJsonMapper.Map(next.RawCommand!.Value), next.RawCommand, default);
                Assert.True(resumed.Accepted, resumed.ErrorMessage);
                var expected = JsonSerializer.SerializeToElement(next.AuthoritativeState);
                var actual = JsonSerializer.SerializeToElement(resumed.State);
                var differences = expected.EnumerateObject().Where(p => !JsonElement.DeepEquals(p.Value, actual.GetProperty(p.Name)))
                    .Select(p => p.Name).ToArray();
                var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_TRIGGER_DUEL_EVIDENCE");
                if (evidence is not null && differences.Length > 0)
                {
                    Directory.CreateDirectory(evidence);
                    await File.WriteAllTextAsync(Path.Combine(evidence, "cold-resume-mismatch.json"),
                        JsonSerializer.Serialize(new { index, next.CommandType, next.RawCommand, differences, expected, actual }));
                }
                Assert.True(MatchStateHasher.Hash(next.AuthoritativeState) == MatchStateHasher.Hash(resumed.State),
                    "cold next command " + next.CommandType + " differs at " + string.Join(", ", differences));
            }
        }
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(room == frame.RoomId ? frame : null);
    }

    [Fact]
    public async Task NormalCardOriginPassesFocusAfterItsLastBreathDiscardChoice()
    {
        var initial = MovementDiscardTests.Position(origin: "BASE");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["AGENT"] = new("AGENT", cardNo: "OGN·178/298", ownerId: "P2", controllerId: "P2", power: 5, damage: 4, tags: [CardObjectTags.UnitCard]),
            ["KEEP"] = new("KEEP", cardNo: "OGN·178/298", ownerId: "P2", controllerId: "P2", power: 5, tags: [CardObjectTags.UnitCard]),
            ["ATTACKER"] = new("ATTACKER", cardNo: "OGN·178/298", ownerId: "P1", controllerId: "P1", power: 5, tags: [CardObjectTags.UnitCard]),
            ["NORMAL-SPELL"] = new("NORMAL-SPELL", cardNo: "OGN·133/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
            ["BF2"] = initial.CardObjects["BF2"] with { ControllerId = "P2" }
        };
        initial = initial with { ActivePlayerId = "P1", TurnPlayerId = "P1", CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "ATTACKER"],
                    Hand = [..initial.PlayerZones["P1"].Hand, "NORMAL-SPELL"] },
                ["P2"] = initial.PlayerZones["P2"] with { Battlefields = [..initial.PlayerZones["P2"].Battlefields, "AGENT", "KEEP"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["ATTACKER"] = new("P1", "BASE"), ["KEEP"] = new("P2", "BATTLEFIELD", "BF2"),
                ["AGENT"] = new("P2", "BATTLEFIELD", "BF2"), ["NORMAL-SPELL"] = new("P1", "HAND") },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(10, 10) } };
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        async Task<ResolutionResult> Submit(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
            var result = await session.SubmitAsync(player, "normal-discard-" + journal.Entries.Count, GameCommandJsonMapper.Map(raw), raw, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            var errors = OfficialInsightAndSpellLockTests.Errors(result.State);
            var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_TRIGGER_DUEL_EVIDENCE");
            if (errors.Count > 0 && evidence is not null)
            {
                Directory.CreateDirectory(evidence);
                await File.WriteAllTextAsync(Path.Combine(evidence, "normal-origin-state-errors.json"),
                    JsonSerializer.Serialize(new { raw, errors, initial, state = result.State, result.Events }));
            }
            Assert.True(errors.Count == 0, string.Join("; ", errors));
            OfficialInsightAndSpellLockTests.Restore(result.State); return result;
        }
        var current = await Submit("P1", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:BF2", []));
        Assert.Equal("P1", current.State.FocusPlayerId);
        current = await Submit("P1", new PlayCardCommand("NORMAL-SPELL", "OGN·133/298", []));
        current = await Submit(current.State.PriorityPlayerId!, new PassPriorityCommand());
        current = await Submit(current.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Contains("AGENT", current.State.PlayerZones["P2"].Graveyard);
        current = await Submit(current.State.PriorityPlayerId!, new PassPriorityCommand());
        current = await Submit(current.State.PriorityPlayerId!, new PassPriorityCommand());
        var choice = Assert.IsType<PendingHandChoiceState>(current.State.PendingHandChoice);
        Assert.Equal(2, choice.RequiredCount); Assert.Equal("P2", choice.PlayerId);
        Assert.Equal(TimingStates.SpellDuelClosed, current.State.TimingState);
        current = await Submit("P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H1", "H2"]));
        Assert.Equal(TimingStates.SpellDuelOpen, current.State.TimingState);
        Assert.Equal("P2", current.State.FocusPlayerId);
        Assert.Contains(CommandTypes.PassFocus, current.Prompts["P2"].EnabledActions());
        Assert.Equal(new[] { "H3", "DRAW-TOP", "DRAW-NEXT" }, current.State.PlayerZones["P2"].Hand);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
            { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((value, index) =>
            new RecoveredEvent(e.StartedEventSequence + index + 1, e.CompletedTick, index, value))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
