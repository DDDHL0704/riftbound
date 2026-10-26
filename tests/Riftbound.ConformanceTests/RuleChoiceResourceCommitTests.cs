using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RuleChoiceResourceCommitTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("P1", false)]
    [InlineData("P2", false)]
    [InlineData("P1", true)]
    [InlineData("P2", true)]
    public async Task ConfirmedRuneResourceCommitsBeforeTheOuterPaymentAndSurvivesSurrender(string concedingPlayer, bool cold)
    {
        // CN430.2/430.2.a: a resource ability confirms/resolves immediately
        // while the containing task is suspended. CN650/651 permits conceding.
        var room = $"confirmed-resource-surrender-{concedingPlayer}-{cold}";
        var journal = new Journal(); var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(room, engine, journal);
        await session.EnsurePlayerAsync("P1", default); await session.EnsurePlayerAsync("P2", default);
        const string scenario = "typed-power-payment-mixed-recycle";
        var seeded = await session.SeedScenarioAsync("P1", "original-resource-qa", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        await Act("P1", new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", ["N54-SELECTED-FIELD"]));
        await Act(State().PriorityPlayerId!, new PassPriorityCommand());
        await Act(State().PriorityPlayerId!, new PassPriorityCommand());
        var request = Assert.IsType<RuleChoiceContinuation>(State().PendingRuleChoice).Request;
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", request.Kind);
        const string rune = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        await Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["RECYCLE_RUNE:" + rune]));
        var confirmation = journal.Entries[^1];
        var before = Frame(journal.Entries);
        var normalBefore = Validate(before);
        var formalBefore = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(before, engine, default);
        Assert.Empty(normalBefore); Assert.Empty(formalBefore);
        var originalPrefix = JsonSerializer.Serialize(journal.Entries, Json);
        var beforeSnapshot = session.SnapshotFor("P1");
        if (cold)
            session = await new InMemoryMatchSessionRegistry(engine, journal, new RawEnvelopeFrameStore(before))
                .GetOrCreateAsync(room, default);
        Assert.Equal(JsonSerializer.Serialize(beforeSnapshot, Json), JsonSerializer.Serialize(session.SnapshotFor("P1"), Json));
        var surrendered = await Act(concedingPlayer, new SurrenderCommand());
        var after = Frame(journal.Entries);
        var normalAfter = Validate(after);
        var formalAfter = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(after, engine, default);
        var prefixUnchanged = originalPrefix == JsonSerializer.Serialize(journal.Entries.Take(7), Json);
        var output = Environment.GetEnvironmentVariable("RIFTBOUND_RAW_ENVELOPE_EVIDENCE");
        if (output is not null)
        {
            var folder = Path.Combine(output, room); Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "proof.json"), JsonSerializer.Serialize(new {
                concedingPlayer, cold, ordinaryOpeningCertified = false,
                scope = "Actual empty room/seats/existing finite QA seed/resource confirmation/surrender; no SQL/API/L3 claim.",
                source = "DOC-01 430.2/430.2.a/430.3 and 650/651.1", before, after,
                normalBefore, formalBefore, normalAfter, formalAfter, originalPrefixUnchanged = prefixUnchanged,
                confirmation, beforeSnapshot, afterSnapshot = session.SnapshotFor("P1"), surrendered,
                originalJournal = journal.Entries, ruleInputs = RuleInputIdentity.Current }, Json));
        }
        Assert.Equal(7, before.Commands.Count); Assert.Equal(8, after.Commands.Count);
        Assert.Equal(new[] { "ROOM_SEAT", "ROOM_SEAT", "DEV_SEED_SCENARIO:" + scenario },
            before.Commands.Take(3).Select(command => command.CommandType));
        Assert.True(prefixUnchanged);
        Assert.Empty(normalAfter); Assert.Empty(formalAfter);
        Assert.Equal(MatchStatuses.Finished, surrendered.State.Status);
        Assert.Equal(concedingPlayer == "P1" ? "P2" : "P1", surrendered.State.WinnerPlayerId);
        Assert.Equal(2, before.AuthoritativeState!.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(rune, before.AuthoritativeState.PlayerZones["P1"].Base);
        Assert.Contains(rune, before.AuthoritativeState.PlayerZones["P1"].RuneDeck);
        Assert.Single(confirmation.Events, value => value.Kind == "RUNE_RECYCLED");
        Assert.Equal(2, surrendered.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(rune, surrendered.State.PlayerZones["P1"].Base);
        Assert.Contains(rune, surrendered.State.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain(surrendered.Events, value => value.Kind == "RUNE_RECYCLED");
        Assert.Equal(0, surrendered.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        async Task<ResolutionResult> Act(string actor, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = session.PromptFor(actor).PromptId; node["snapshotTick"] = State().Tick;
            var result = await session.SubmitAsync(actor, "resource-prefix-" + journal.Entries.Count,
                command, JsonSerializer.SerializeToElement(node), default);
            Assert.True(result.Accepted, result.ErrorMessage); return result;
        }
    }

    [Theory]
    [InlineData("RED")]
    [InlineData("BLUE")]
    public async Task ResourceStepLedgerReplaysActualChildMutationsAndRejectsChangedChoices(string color)
    {
        var journal = new Journal(); IMatchSession session = new MatchSession("resource-step-ledger-" + color, new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("P1", default); await session.EnsurePlayerAsync("P2", default);
        const string scenario = "typed-power-payment-mixed-recycle";
        var seeded = await session.SeedScenarioAsync("P1", "original-ledger-qa", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        await Act("P1", new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", ["N54-SELECTED-FIELD"]));
        await Act(State().PriorityPlayerId!, new PassPriorityCommand());
        await Act(State().PriorityPlayerId!, new PassPriorityCommand());
        var request = State().PendingRuleChoice!.Request;
        var rune = color == "RED" ? "P1-RUNE-RED-PARTIAL-PAYMENT-001" : "P1-RUNE-BLUE-EXTRA-PAYMENT-001";
        await Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["RECYCLE_RUNE:" + rune]));
        var pending = Assert.IsType<RuleChoiceContinuation>(State().PendingRuleChoice);
        var original = JsonSerializer.Serialize(journal.Entries, Json);
        var ledger = Replay(pending, true);
        var step = Assert.Single(ledger.EnumerateArray().ToArray());
        Assert.Equal("P1", step.GetProperty("playerId").GetString());
        Assert.Equal("RECYCLE_RUNE:" + rune, step.GetProperty("choiceId").GetString());
        var before = step.GetProperty("before").Deserialize<MatchState>(Json)!;
        var after = step.GetProperty("after").Deserialize<MatchState>(Json)!;
        Assert.Equal(1, before.RunePools["P1"].TotalPower);
        Assert.Equal(2, after.RunePools["P1"].TotalPower);
        Assert.Contains(rune, before.PlayerZones["P1"].Base);
        Assert.DoesNotContain(rune, after.PlayerZones["P1"].Base);
        Assert.Contains(rune, after.PlayerZones["P1"].RuneDeck);
        Assert.Equal("RUNE_DECK", after.ObjectLocations[rune].Zone);
        Assert.Single(step.GetProperty("events").EnumerateArray().ToArray(), value => value.GetProperty("kind").GetString() == "RUNE_RECYCLED");
        Assert.Equal(ledger.GetRawText(), Replay(pending, true).GetRawText());
        _ = Replay(pending with { Request = pending.Request with { Reason = pending.Request.Reason + " forged" } }, false);
        _ = Replay(pending with { Answers = pending.Answers.Select(answer => answer with { OptionId = "RECYCLE_RUNE:FOREIGN" }).ToArray() }, false);
        Assert.Equal(original, JsonSerializer.Serialize(journal.Entries, Json));
        var output = Environment.GetEnvironmentVariable("RIFTBOUND_RAW_ENVELOPE_EVIDENCE");
        if (output is not null)
        {
            var folder = Path.Combine(output, "resource-step-ledger-" + color); Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "proof.json"), JsonSerializer.Serialize(new {
                color, ledger, pending, originalJournal = journal.Entries,
                scope = "Producer ledger only; consumer commit/surrender still requires the owning integration. No SQL/API/full claim.",
                ruleInputs = RuleInputIdentity.Current }, Json));
        }

        JsonElement Replay(RuleChoiceContinuation continuation, bool expected)
        {
            var method = typeof(CoreRuleEngine).GetMethod("TryReplayRuleChoiceResources",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            object?[] args = [continuation, null];
            Assert.Equal(expected, Assert.IsType<bool>(method.Invoke(null, args)));
            Assert.NotNull(args[1]); return JsonSerializer.SerializeToElement(args[1], args[1]!.GetType(), Json);
        }
        MatchState State() => journal.Entries[^1].AuthoritativeState;
        async Task Act(string actor, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = session.PromptFor(actor).PromptId; node["snapshotTick"] = State().Tick;
            var result = await session.SubmitAsync(actor, "ledger-prefix-" + journal.Entries.Count,
                command, JsonSerializer.SerializeToElement(node), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
    }

    private static IReadOnlyList<string> Validate(MatchRecoveryFrame frame)
        => MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);

    private static MatchRecoveryFrame Frame(IReadOnlyList<MatchJournalEntry> entries)
    {
        var last = entries[^1];
        var commands = entries.Select(entry => JsonSerializer.Deserialize<RecoveredCommand>(JsonSerializer.Serialize(entry, Json), Json)!).ToArray();
        var events = entries.SelectMany(entry => entry.Events.Select((value, i) =>
            new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, value))).ToArray();
        var views = last.Snapshots.ToDictionary(pair => pair.Key, pair => new RecoveredPlayerView(pair.Key,
            last.CompletedTick, last.CompletedEventSequence, pair.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[pair.Key]));
        var initial = MatchState.Create(last.RoomId) with { Seed = entries[0].OpeningSeed };
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(last.RoomId, last.CompletedTick,
            last.CompletedEventSequence, events.Select(entry => entry.Event).ToArray(), last.AuthoritativeState);
        return new MatchRecoveryFrame(last.RoomId, last.CompletedTick, last.CompletedEventSequence,
            commands, events, views, [], last.AuthoritativeState, initial, spectator) { OpeningSeed = entries[0].OpeningSeed };
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
