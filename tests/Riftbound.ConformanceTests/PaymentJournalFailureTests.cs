using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PaymentJournalFailureTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OrdinaryRuneAcquisitionOnlyCommitsAfterItsJournalAndRetriesOnce(bool recycle, bool rawPresent)
    {
        var room = $"resource-journal-failure-{recycle}-{rawPresent}";
        var journal = new FailingJournal();
        var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(room, engine, journal);
        await session.EnsurePlayerAsync("p1", default);
        await session.EnsurePlayerAsync("p2", default);
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "lillia-lowcurve").Decklist;
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await Accept("p1", "deck-p1", submit); await Accept("p2", "deck-p2", submit);
        await Accept("p1", "ready-p1", new ReadyCommand()); await Accept("p2", "ready-p2", new ReadyCommand());
        Assert.Equal(6, journal.Entries.Count);
        Assert.Equal(12, journal.Entries.Sum(entry => entry.Events.Count));
        for (var i = 0; i < 2; i++)
        {
            var openingActor = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(State()));
            await Accept(openingActor, "mulligan-" + i, new MulliganCommand([]));
        }
        Assert.Null(ResolutionResult.OpeningMulliganPlayerId(State()));
        var actor = State().ActivePlayerId;
        var rune = State().PlayerZones[actor].Base.First(id =>
            State().CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && !State().CardObjects[id].IsExhausted);
        var trait = Assert.Single(State().CardObjects[rune].Tags, tag => tag.StartsWith("COLOR:", StringComparison.Ordinal))[6..];
        var before = State();
        var beforeHash = MatchStateHasher.Hash(before);
        var beforeJournal = JsonSerializer.Serialize(journal.Entries, Json);
        var beforeViews = Views();
        var count = journal.Entries.Count;
        GameCommand input = recycle ? new RecycleRuneCommand(rune) : new TapRuneCommand(rune);
        JsonElement? raw = rawPresent ? Wire(actor, input) : null;
        const string intent = "acquire-resource-after-journal-failure";
        journal.FailNext = true;
        await Assert.ThrowsAsync<InjectedJournalFailure>(async () =>
            await session.SubmitAsync(actor, intent, input, raw, default));
        var attempted = Assert.IsType<MatchJournalEntry>(journal.FailedAttempt);
        Assert.True(attempted.Accepted, attempted.ErrorMessage);
        Assert.Single(attempted.Events, e => e.Kind == (recycle ? "RUNE_RECYCLED" : "RUNE_TAPPED"));
        Assert.Equal(before.Tick, attempted.StartedTick);
        Assert.Equal(before.Tick + 1, attempted.CompletedTick);
        Assert.Equal(beforeViews, Views());
        Assert.Equal(beforeHash, MatchStateHasher.Hash(before));
        Assert.Equal(beforeJournal, JsonSerializer.Serialize(journal.Entries, Json));

        var retry = await session.SubmitAsync(actor, intent, input, raw, default);
        Assert.True(retry.Accepted, retry.ErrorMessage);
        Assert.Equal(count + 1, journal.Entries.Count);
        Assert.Single(journal.Entries, entry => entry.ClientIntentId == intent);
        Assert.Equal(before.Tick + 1, retry.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(attempted.AuthoritativeState), MatchStateHasher.Hash(retry.State));
        Assert.Single(retry.Events, e => e.Kind == (recycle ? "RUNE_RECYCLED" : "RUNE_TAPPED"));
        Assert.Equal(before.RunePools[actor].Mana + (recycle ? 0 : 1), retry.State.RunePools[actor].Mana);
        Assert.Equal(before.RunePools[actor].TotalPower + (recycle ? 1 : 0), retry.State.RunePools[actor].TotalPower);
        if (recycle)
        {
            Assert.Equal(before.RunePools[actor].PowerByTrait.GetValueOrDefault(trait) + 1,
                retry.State.RunePools[actor].PowerByTrait.GetValueOrDefault(trait));
            Assert.DoesNotContain(rune, retry.State.PlayerZones[actor].Base);
            Assert.Contains(rune, retry.State.PlayerZones[actor].RuneDeck);
        }
        else
        {
            Assert.Contains(rune, retry.State.PlayerZones[actor].Base);
            Assert.True(retry.State.CardObjects[rune].IsExhausted);
        }
        var committedJournal = JsonSerializer.Serialize(journal.Entries, Json);
        var committedHash = MatchStateHasher.Hash(retry.State);
        var warm = await session.SubmitAsync(actor, intent, input, raw, default);
        Assert.True(warm.Accepted, warm.ErrorMessage);
        Assert.Equal(committedHash, MatchStateHasher.Hash(warm.State));
        Assert.Equal(committedJournal, JsonSerializer.Serialize(journal.Entries, Json));
        var frame = Frame(journal.Entries);
        var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands,
            frame.Events, frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
        Assert.Empty(errors);
        var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default);
        Assert.Empty(audit);
        var cold = await new InMemoryMatchSessionRegistry(engine, journal, new RawEnvelopeFrameStore(frame))
            .GetOrCreateAsync(room, default);
        var duplicate = await cold.SubmitAsync(actor, intent, input, raw, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Empty(duplicate.Events);
        Assert.Equal(committedHash, MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(committedJournal, JsonSerializer.Serialize(journal.Entries, Json));
        var output = Environment.GetEnvironmentVariable("RIFTBOUND_RAW_ENVELOPE_EVIDENCE");
        if (output is not null)
        {
            var folder = Path.Combine(output, room); Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "proof.json"), JsonSerializer.Serialize(new {
                recycle, rawPresent, scope = "Injected journal failure only; real ordinary opening, resources, replay and registry; no SQL/network/restart claim.",
                beforeHash, beforeViews, beforeJournal, attempted, retry, warm, frame, errors, audit, duplicate,
                originalJournal = journal.Entries, ruleInputs = RuleInputIdentity.Current }, Json));
        }

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        string Views() => JsonSerializer.Serialize(new[] { "p1", "p2" }.Select(player => new {
            player, snapshot = session.SnapshotFor(player), prompt = session.PromptFor(player) }), Json);
        JsonElement Wire(string player, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            if (command is TapRuneCommand or RecycleRuneCommand) node.Remove("sourceObjectIds");
            node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = State().Tick;
            return JsonSerializer.SerializeToElement(node);
        }
        async Task Accept(string player, string id, GameCommand command)
        {
            var wire = Wire(player, command);
            var result = command switch {
                SubmitDeckCommand deckCommand => await session.SubmitDeckAsync(player, id, deckCommand, wire, default),
                ReadyCommand => await session.ReadyAsync(player, id, wire, default),
                _ => await session.SubmitAsync(player, id, command, wire, default)
            };
            Assert.True(result.Accepted, result.ErrorMessage);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LoggedInstructionPaymentOnlyCommitsAfterItsJournalAndRetriesOnce(bool spend, bool rawPresent)
    {
        // Keep the actual empty-room seats and existing finite QA seed command.
        // This is an instruction-payment control, not an ordinary opening proof.
        var room = $"instruction-journal-failure-{spend}-{rawPresent}";
        var journal = new FailingJournal(); var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(room, engine, journal);
        await session.EnsurePlayerAsync("P1", default); await session.EnsurePlayerAsync("P2", default);
        const string scenario = "typed-power-payment-mixed-recycle";
        var seeded = await session.SeedScenarioAsync("P1", "original-instruction-qa", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        Assert.Equal(new[] { "ROOM_SEAT", "ROOM_SEAT", "DEV_SEED_SCENARIO:" + scenario },
            journal.Entries.Select(entry => entry.CommandType));
        await Act("P1", new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", ["N54-SELECTED-FIELD"]));
        await Act(State().PriorityPlayerId!, new PassPriorityCommand());
        await Act(State().PriorityPlayerId!, new PassPriorityCommand());
        var resourceChoice = Assert.IsType<RuleChoiceContinuation>(State().PendingRuleChoice).Request;
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", resourceChoice.Kind);
        await Act(resourceChoice.PlayerId, new PayCostCommand(resourceChoice.Id, "RULE_REPLACEMENT",
            ["RECYCLE_RUNE:P1-RUNE-RED-PARTIAL-PAYMENT-001"]));
        var before = State();
        var pending = Assert.IsType<RuleChoiceContinuation>(before.PendingRuleChoice);
        Assert.Single(pending.Answers);
        // The resource-selection command is already journaled. The final
        // instruction payment must preserve that committed acquisition on failure.
        Assert.Equal(2, before.RunePools["P1"].TotalPower);
        Assert.Equal(2, pending.Request.Preview!.RunePools["P1"].TotalPower);
        Assert.Equal(0, before.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        Assert.Single(journal.Entries.SelectMany(entry => entry.Events), entry => entry.Kind == "RUNE_RECYCLED");
        var beforeHash = MatchStateHasher.Hash(before);
        var beforeViews = Views(); var beforeJournal = JsonSerializer.Serialize(journal.Entries, Json);
        var count = journal.Entries.Count; var quantity = spend ? 2 : 0;
        var input = new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:" + quantity]);
        JsonElement? raw = rawPresent ? Wire("P1", input) : null;
        const string intent = "instruction-after-journal-failure";
        journal.FailNext = true;
        await Assert.ThrowsAsync<InjectedJournalFailure>(async () =>
            await session.SubmitAsync("P1", intent, input, raw, default));
        var attempted = Assert.IsType<MatchJournalEntry>(journal.FailedAttempt);
        Assert.True(attempted.Accepted, attempted.ErrorMessage);
        Assert.Equal(beforeViews, Views());
        Assert.Equal(beforeHash, MatchStateHasher.Hash(before));
        Assert.Equal(beforeJournal, JsonSerializer.Serialize(journal.Entries, Json));

        var retry = await session.SubmitAsync("P1", intent, input, raw, default);
        Assert.True(retry.Accepted, retry.ErrorMessage);
        Assert.Equal(count + 1, journal.Entries.Count);
        var committed = Assert.Single(journal.Entries, entry => entry.ClientIntentId == intent);
        Assert.Equal(rawPresent, committed.RawCommand.HasValue);
        Assert.Equal(!rawPresent, committed.TypedCommand is not null);
        Assert.Equal(before.Tick + 1, retry.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(attempted.AuthoritativeState), MatchStateHasher.Hash(retry.State));
        foreach (var resolved in new[] { attempted.AuthoritativeState, retry.State })
        {
            Assert.Null(resolved.PendingRuleChoice);
            Assert.Empty(resolved.StackItems);
            Assert.Equal(2 - quantity, resolved.RunePools["P1"].TotalPower);
            Assert.Equal(quantity, resolved.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
            Assert.Contains("P1-RUNE-RED-PARTIAL-PAYMENT-001", resolved.PlayerZones["P1"].RuneDeck);
        }
        Assert.DoesNotContain(retry.Events, entry => entry.Kind == "RUNE_RECYCLED");
        Assert.Single(journal.Entries.SelectMany(entry => entry.Events), entry => entry.Kind == "RUNE_RECYCLED");
        var payment = Assert.Single(retry.Events, entry => entry.Kind == "COST_PAID"
            && Equals(entry.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT"));
        Assert.Equal(quantity, Convert.ToInt32(payment.Payload["power"]));
        var committedJournal = JsonSerializer.Serialize(journal.Entries, Json);
        var committedHash = MatchStateHasher.Hash(retry.State);
        var warm = await session.SubmitAsync("P1", intent, input, raw, default);
        Assert.True(warm.Accepted, warm.ErrorMessage);
        Assert.Equal(committedHash, MatchStateHasher.Hash(warm.State));
        Assert.Equal(committedJournal, JsonSerializer.Serialize(journal.Entries, Json));
        var frame = Frame(journal.Entries);
        var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands,
            frame.Events, frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
        Assert.Empty(errors);
        var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default);
        Assert.Empty(audit);
        var cold = await new InMemoryMatchSessionRegistry(engine, journal, new RawEnvelopeFrameStore(frame))
            .GetOrCreateAsync(room, default);
        var duplicate = await cold.SubmitAsync("P1", intent, input, raw, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Empty(duplicate.Events);
        Assert.Equal(committedHash, MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(committedJournal, JsonSerializer.Serialize(journal.Entries, Json));
        var output = Environment.GetEnvironmentVariable("RIFTBOUND_RAW_ENVELOPE_EVIDENCE");
        if (output is not null)
        {
            var folder = Path.Combine(output, room); Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "proof.json"), JsonSerializer.Serialize(new {
                spend, rawPresent, quantity, ordinaryOpeningCertified = false,
                scope = "Existing logged finite QA seed and injected final instruction journal failure; no SQL/network/restart claim.",
                beforeHash, beforeViews, beforeJournal, attempted, retry, warm, frame, errors, audit, duplicate,
                originalJournal = journal.Entries, ruleInputs = RuleInputIdentity.Current }, Json));
        }

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        string Views() => JsonSerializer.Serialize(new[] { "P1", "P2" }.Select(player => new {
            player, snapshot = session.SnapshotFor(player), prompt = session.PromptFor(player) }), Json);
        JsonElement Wire(string player, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = State().Tick;
            return JsonSerializer.SerializeToElement(node);
        }
        async Task Act(string player, GameCommand command)
        {
            var result = await session.SubmitAsync(player, "instruction-prefix-" + journal.Entries.Count,
                command, Wire(player, command), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResourceQuoteIsReadOnlyAndCannotAuthorizeAChangedResourceSource(bool rawPresent)
    {
        var room = "resource-quote-authority-" + rawPresent;
        var journal = new FailingJournal(); var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(room, engine, journal);
        await session.EnsurePlayerAsync("P1", default); await session.EnsurePlayerAsync("P2", default);
        const string scenario = "typed-power-payment-mixed-recycle";
        var seeded = await session.SeedScenarioAsync("P1", "original-quote-qa", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        const string rune = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        var command = new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", ["N54-SELECTED-FIELD"],
            OptionalCosts: ["RECYCLE_RUNE:" + rune]);
        var prompt = session.PromptFor("P1");
        var request = new PlayCostPreviewRequestDto("original-resource-quote", prompt.PromptId!, State().Tick, command);
        JsonElement? originalRaw = rawPresent ? Wire(command) : null;
        var originalHash = MatchStateHasher.Hash(State()); var originalViews = Views();
        var originalJournal = JsonSerializer.Serialize(journal.Entries, Json);
        var first = await session.PreviewPlayCardAsync("P1", request, default);
        var second = await session.PreviewPlayCardAsync("P1", request, default);
        Assert.True(first.CanPay, first.Message);
        Assert.Equal(JsonSerializer.Serialize(first, Json), JsonSerializer.Serialize(second, Json));
        Assert.Equal(0, first.Cost!.GenericPower); Assert.Empty(first.Cost.PowerByTrait);
        Assert.Equal(originalHash, MatchStateHasher.Hash(State()));
        Assert.Equal(originalViews, Views());
        Assert.Equal(originalJournal, JsonSerializer.Serialize(journal.Entries, Json));

        var acquired = await session.SubmitAsync("P1", "separate-rune-acquisition", new RecycleRuneCommand(rune),
            Wire(new RecycleRuneCommand(rune)), default);
        Assert.True(acquired.Accepted, acquired.ErrorMessage);
        Assert.Equal(2, acquired.State.RunePools["P1"].TotalPower);
        Assert.Single(acquired.Events, entry => entry.Kind == "RUNE_RECYCLED");
        var afterResource = MatchStateHasher.Hash(State()); var afterResourceViews = Views();
        var afterResourceJournal = JsonSerializer.Serialize(journal.Entries, Json);
        var staleQuote = await session.PreviewPlayCardAsync("P1", request, default);
        Assert.Equal(ErrorCodes.PromptExpired, staleQuote.ErrorCode); Assert.Null(staleQuote.Cost);
        Assert.Equal(afterResourceViews, Views());
        Assert.Equal(afterResourceJournal, JsonSerializer.Serialize(journal.Entries, Json));
        var staleCommit = await session.SubmitAsync("P1", request.RequestId, command, originalRaw, default);
        Assert.False(staleCommit.Accepted); Assert.Empty(staleCommit.Events);
        if (rawPresent) Assert.Equal(ErrorCodes.PromptExpired, staleCommit.ErrorCode);
        else Assert.False(string.IsNullOrWhiteSpace(staleCommit.ErrorCode));
        Assert.Equal(afterResource, MatchStateHasher.Hash(staleCommit.State));
        Assert.Equal(afterResourceViews, Views());
        Assert.DoesNotContain(staleCommit.Events, entry => entry.Kind == "RUNE_RECYCLED" || entry.Kind == "COST_PAID");

        var current = command with { OptionalCosts = [] };
        var currentPrompt = session.PromptFor("P1");
        var freshRequest = new PlayCostPreviewRequestDto("fresh-resource-quote", currentPrompt.PromptId!, State().Tick, current);
        var beforeFreshQuote = JsonSerializer.Serialize(journal.Entries, Json);
        var freshQuote = await session.PreviewPlayCardAsync("P1", freshRequest, default);
        Assert.True(freshQuote.CanPay, freshQuote.Message);
        Assert.Equal(beforeFreshQuote, JsonSerializer.Serialize(journal.Entries, Json));
        var committed = await session.SubmitAsync("P1", freshRequest.RequestId, current,
            rawPresent ? Wire(current) : null, default);
        Assert.True(committed.Accepted, committed.ErrorMessage);
        Assert.Equal(acquired.State.RunePools["P1"].Mana - freshQuote.Cost!.Mana, committed.State.RunePools["P1"].Mana);
        Assert.Equal(2, committed.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(committed.Events, entry => entry.Kind == "RUNE_RECYCLED");
        var cost = Assert.Single(committed.Events, entry => entry.Kind == "COST_PAID");
        Assert.Equal(freshQuote.Cost.Mana, Convert.ToInt32(cost.Payload["mana"]));
        Assert.Equal(0, Convert.ToInt32(cost.Payload["power"]));
        Assert.Equal(0, committed.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        var frame = Frame(journal.Entries);
        var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands,
            frame.Events, frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
        Assert.Empty(errors);
        var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default);
        Assert.Empty(audit);
        var committedJournal = JsonSerializer.Serialize(journal.Entries, Json);
        var cold = await new InMemoryMatchSessionRegistry(engine, journal, new RawEnvelopeFrameStore(frame))
            .GetOrCreateAsync(room, default);
        var duplicate = await cold.SubmitAsync("P1", freshRequest.RequestId, current,
            rawPresent ? journal.Entries[^1].RawCommand : null, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage); Assert.Empty(duplicate.Events);
        Assert.Equal(MatchStateHasher.Hash(committed.State), MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(committedJournal, JsonSerializer.Serialize(journal.Entries, Json));
        Assert.Single(journal.Entries.SelectMany(entry => entry.Events), entry => entry.Kind == "RUNE_RECYCLED");
        var output = Environment.GetEnvironmentVariable("RIFTBOUND_RAW_ENVELOPE_EVIDENCE");
        if (output is not null)
        {
            var folder = Path.Combine(output, room); Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "proof.json"), JsonSerializer.Serialize(new {
                rawPresent, ordinaryOpeningCertified = false, scope = "Existing logged finite QA seed; resource preview read-only and stale command reauthorization; no SQL/network/restart claim.",
                request, originalRaw, originalHash, originalViews, originalJournal, first, second, acquired,
                staleQuote, staleCommit, freshRequest, freshQuote, committed, frame, errors, audit, duplicate,
                originalJournalEntries = journal.Entries, ruleInputs = RuleInputIdentity.Current }, Json));
        }

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        string Views() => JsonSerializer.Serialize(new[] { "P1", "P2" }.Select(player => new {
            player, snapshot = session.SnapshotFor(player), prompt = session.PromptFor(player) }), Json);
        JsonElement Wire(GameCommand input)
        {
            var node = JsonSerializer.SerializeToNode(input, input.GetType(), Json)!.AsObject();
            if (input is RecycleRuneCommand) node.Remove("sourceObjectIds");
            node["promptId"] = session.PromptFor("P1").PromptId; node["snapshotTick"] = State().Tick;
            return JsonSerializer.SerializeToElement(node);
        }
    }

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

    private sealed class FailingJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public bool FailNext { get; set; }
        public MatchJournalEntry? FailedAttempt { get; private set; }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        {
            if (FailNext) { FailNext = false; FailedAttempt = entry; throw new InjectedJournalFailure(); }
            Entries.Add(entry); return ValueTask.CompletedTask;
        }
    }

    private sealed class InjectedJournalFailure : IOException { }
}
