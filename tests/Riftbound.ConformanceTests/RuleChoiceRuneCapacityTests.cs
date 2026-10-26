using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Explicit numeric boundaries, not naturally accumulated game balances.
public sealed class RuleChoiceRuneCapacityTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("counter", "mana")]
    [InlineData("counter", "restricted")]
    [InlineData("counter", "wild")]
    [InlineData("counter", "typed")]
    [InlineData("token", "mana")]
    [InlineData("token", "restricted")]
    [InlineData("token", "wild")]
    [InlineData("token", "typed")]
    public async Task FullWorkingPoolNeverOffersAnUnrepresentableRuneGainAndRejectsAForgedSelection(string kind, string balance)
    {
        var run = await Open(kind, balance, 0);
        var request = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice).Request;
        Assert.Equal(kind == "counter" ? "COUNTER_TARGET_MANA_PAYMENT" : "TOKEN_HASTE_PAYMENT", request.Kind);
        var pool = request.Preview!.RunePools[run.Player];
        var mana = balance is "mana" or "restricted";
        Assert.True(pool.HasSupportedAmounts);
        Assert.Equal(int.MaxValue, mana ? (long)pool.Mana + pool.SpellOnlyMana : pool.TotalPower);
        await run.Cold();
        var choice = (mana ? "TAP_RUNE:" : "RECYCLE_RUNE:") + "CAP-RUNE";
        var stateHash = MatchStateHasher.Hash(run.State);
        var snapshotHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(run.State));
        var promptHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(run.State));
        ResolutionResult? result = null;
        Exception? exception = null;
        try { result = await run.Submit(new PayCostCommand(request.Id, "RULE_REPLACEMENT", [choice])); }
        catch (Exception ex) { exception = ex; }
        Evidence(kind + "-" + balance + "-full", new {
            run.Initial, request, choice, offered = request.Options.Any(o => o.Id == choice),
            result, exception = exception?.ToString(), commands = run.Commands(), events = run.Events()
        });
        Assert.DoesNotContain(request.Options, o => o.Id == choice);
        Assert.Contains(request.Options, o => o.Id == (mana ? "RECYCLE_RUNE:" : "TAP_RUNE:") + "CAP-RUNE");
        Assert.Null(exception);
        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(stateHash, MatchStateHasher.Hash(run.State));
        Assert.Equal(snapshotHash, MatchStateHasher.HashValue(result.Snapshots));
        Assert.Equal(promptHash, MatchStateHasher.HashValue(result.Prompts));
        await run.Accept(new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["DECLINE"]));
        Assert.False(run.State.CardObjects["CAP-RUNE"].IsExhausted);
        Assert.Contains("CAP-RUNE", run.State.PlayerZones[run.Player].Base);
        Assert.Null(run.State.PendingRuleChoice);
        Assert.True(run.State.RunePools[run.Player].HasSupportedAmounts);
        await run.Replay();
    }

    [Theory]
    [InlineData("counter", "mana", false)] [InlineData("counter", "mana", true)]
    [InlineData("counter", "restricted", false)] [InlineData("counter", "restricted", true)]
    [InlineData("counter", "wild", false)] [InlineData("counter", "wild", true)]
    [InlineData("counter", "typed", false)] [InlineData("counter", "typed", true)]
    [InlineData("token", "mana", false)] [InlineData("token", "mana", true)]
    [InlineData("token", "restricted", false)] [InlineData("token", "restricted", true)]
    [InlineData("token", "wild", false)] [InlineData("token", "wild", true)]
    [InlineData("token", "typed", false)] [InlineData("token", "typed", true)]
    public async Task ExactlyOneResourceFitsAndTheNextMenuUsesTheWorkingPoolBeforePayOrDecline(string kind, string balance, bool pay)
    {
        var run = await Open(kind, balance, 1);
        var request = run.State.PendingRuleChoice!.Request;
        var before = request.Preview!.RunePools[run.Player];
        var checkpoint = MatchStateHasher.HashValue(run.State.RunePools);
        var origin = MatchStateHasher.Hash(run.State.PendingRuleChoice.Origin);
        var mana = balance is "mana" or "restricted";
        var prefix = mana ? "TAP_RUNE:" : "RECYCLE_RUNE:";
        Assert.Contains(request.Options, o => o.Id == prefix + "CAP-RUNE");
        Assert.Contains(request.Options, o => o.Id == prefix + "SPARE-RUNE");
        var stalePaymentId = request.Id;
        await run.Cold();
        var firstEvent = run.Events().Length;
        var acquired = await run.Submit(new PayCostCommand(request.Id, "RULE_REPLACEMENT", [prefix + "CAP-RUNE"]));
        Assert.True(acquired.Accepted, acquired.ErrorMessage);
        // CN430.2: the accepted resource action resolves at this answer, before
        // the still-pending outer payment. The original no-event expectation
        // predates immediate resource commitment; its failed input is retained.
        Assert.Single(acquired.Events, e => e.Kind == (mana ? "RUNE_TAPPED" : "RUNE_RECYCLED"));
        Assert.Single(acquired.Events, e => e.Kind == (mana ? "MANA_GAINED" : "POWER_GAINED"));
        Assert.DoesNotContain(acquired.Events, e => e.Kind == "COST_PAID");
        request = run.State.PendingRuleChoice!.Request;
        var working = request.Preview!.RunePools[run.Player];
        Assert.True(working.HasSupportedAmounts);
        Assert.Equal(before.Mana + (mana ? 1 : 0), working.Mana);
        Assert.Equal(before.SpellOnlyMana, working.SpellOnlyMana);
        Assert.Equal(before.TotalPower + (mana ? 0 : 1), working.TotalPower);
        Assert.NotEqual(checkpoint, MatchStateHasher.HashValue(run.State.RunePools));
        Assert.Equal(working, run.State.RunePools[run.Player]);
        Assert.Equal(origin, MatchStateHasher.Hash(run.State.PendingRuleChoice.Origin));
        Assert.DoesNotContain(request.Options, o => o.Id.StartsWith(prefix, StringComparison.Ordinal));
        Assert.Contains(request.Options, o => o.Id == (mana ? "RECYCLE_RUNE:" : "TAP_RUNE:") + "SPARE-RUNE");
        await run.Cold();
        Assert.NotEqual(stalePaymentId, request.Id);
        await run.Reject(new PayCostCommand(stalePaymentId, "RULE_REPLACEMENT", ["DECLINE"]));
        await run.Reject(new PayCostCommand(request.Id, "RULE_REPLACEMENT", [prefix + "SPARE-RUNE"]));
        var payment = kind == "counter" ? "SPEND_MANA:2" : working.Power > 0 ? "PAY:ANY" : "PAY:TRAIT:red";
        await run.Accept(new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? payment : "DECLINE"]));
        Assert.Null(run.State.PendingRuleChoice);
        var final = run.State.RunePools[run.Player];
        Assert.True(final.HasSupportedAmounts);
        Assert.Equal(working.Mana - (pay ? kind == "counter" ? 2 : 1 : 0), final.Mana);
        Assert.Equal(working.SpellOnlyMana, final.SpellOnlyMana);
        Assert.Equal(working.TotalPower - (pay && kind == "token" ? 1 : 0), final.TotalPower);
        Assert.Equal(working.Power - (pay && kind == "token" && payment == "PAY:ANY" ? 1 : 0), final.Power);
        foreach (var trait in working.PowerByTrait.Keys.Union(final.PowerByTrait.Keys))
            Assert.Equal(working.PowerByTrait.GetValueOrDefault(trait) - (pay && kind == "token" && payment == "PAY:TRAIT:" + trait ? 1 : 0),
                final.PowerByTrait.GetValueOrDefault(trait));
        if (mana)
        {
            Assert.Contains("CAP-RUNE", run.State.PlayerZones[run.Player].Base);
            Assert.True(run.State.CardObjects["CAP-RUNE"].IsExhausted);
        }
        else
        {
            Assert.Contains("CAP-RUNE", run.State.PlayerZones[run.Player].RuneDeck);
            Assert.DoesNotContain("CAP-RUNE", run.State.PlayerZones[run.Player].Base);
            Assert.Equal("RUNE_DECK", run.State.ObjectLocations["CAP-RUNE"].Zone);
        }
        Assert.Contains("SPARE-RUNE", run.State.PlayerZones[run.Player].Base);
        Assert.False(run.State.CardObjects["SPARE-RUNE"].IsExhausted);
        var events = run.Events().Skip(firstEvent).Select(e => e.Event).ToArray();
        Assert.Single(events, e => e.Kind == (mana ? "RUNE_TAPPED" : "RUNE_RECYCLED"));
        Assert.Equal(pay ? 1 : 0, events.Count(e => e.Kind == "COST_PAID"
            && Equals(e.Payload.GetValueOrDefault("reason"), kind == "counter" ? "COUNTER_TARGET_MANA_PAYMENT" : "TOKEN_HASTE_PAYMENT")));
        if (kind == "counter") Assert.Equal(pay, run.State.StackItems.Any(s => s.StackItemId == "S1"));
        else Assert.Equal(!pay, Assert.Single(run.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier)).IsExhausted);
        await run.Cold();
        await run.Replay();
        Evidence(kind + "-" + balance + "-fits-" + pay, new { run.Initial, run.State, commands = run.Commands(), events = run.Events() });
    }

    [Theory]
    [InlineData("mana", false)] [InlineData("mana", true)]
    [InlineData("restricted", false)] [InlineData("restricted", true)]
    public async Task TheFirstRealEchoPaymentFreesSpaceBeforeIndependentSequentialRuneGains(string balance, bool paySecond)
    {
        var run = await Open("counter", balance, 0, echo: true);
        var request = run.State.PendingRuleChoice!.Request;
        Assert.DoesNotContain(request.Options, o => o.Id.StartsWith("TAP_RUNE:", StringComparison.Ordinal));
        var original = request.Preview!.RunePools[run.Player];
        var checkpoint = MatchStateHasher.HashValue(run.State.RunePools);
        var firstPayment = await run.Submit(new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_MANA:2"]));
        Assert.True(firstPayment.Accepted, firstPayment.ErrorMessage);
        Assert.Single(firstPayment.Events, e => e.Kind == "COST_PAID"
            && Equals(e.Payload.GetValueOrDefault("reason"), "COUNTER_TARGET_MANA_PAYMENT"));
        request = run.State.PendingRuleChoice!.Request;
        Assert.Equal(original.Mana - 2, request.Preview!.RunePools[run.Player].Mana);
        Assert.NotEqual(checkpoint, MatchStateHasher.HashValue(run.State.RunePools));
        Assert.Equal(original.Mana - 2, run.State.RunePools[run.Player].Mana);
        Assert.Equal(original.SpellOnlyMana, run.State.RunePools[run.Player].SpellOnlyMana);
        await run.Cold();
        await run.Accept(new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["TAP_RUNE:CAP-RUNE"]));
        request = run.State.PendingRuleChoice!.Request;
        Assert.Contains(request.Options, o => o.Id == "TAP_RUNE:SPARE-RUNE");
        Assert.Contains(request.Options, o => o.Id == "TAP_RUNE:EXTRA-RUNE");
        await run.Accept(new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["TAP_RUNE:SPARE-RUNE"]));
        request = run.State.PendingRuleChoice!.Request;
        Assert.Equal(int.MaxValue, (long)request.Preview!.RunePools[run.Player].Mana + request.Preview.RunePools[run.Player].SpellOnlyMana);
        Assert.DoesNotContain(request.Options, o => o.Id == "TAP_RUNE:EXTRA-RUNE");
        await run.Cold();
        await run.Reject(new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["TAP_RUNE:EXTRA-RUNE"]));
        await run.Accept(new PayCostCommand(request.Id, "RULE_REPLACEMENT", [paySecond ? "SPEND_MANA:2" : "DECLINE"]));
        Assert.Equal(original.Mana - (paySecond ? 2 : 0), run.State.RunePools[run.Player].Mana);
        Assert.Equal(original.SpellOnlyMana, run.State.RunePools[run.Player].SpellOnlyMana);
        Assert.Equal(original.TotalPower, run.State.RunePools[run.Player].TotalPower);
        Assert.Contains(run.State.StackItems, s => s.StackItemId == "S1");
        Assert.Equal(paySecond, run.State.StackItems.Any(s => s.StackItemId == "S2"));
        Assert.True(run.State.CardObjects["CAP-RUNE"].IsExhausted);
        Assert.True(run.State.CardObjects["SPARE-RUNE"].IsExhausted);
        Assert.False(run.State.CardObjects["EXTRA-RUNE"].IsExhausted);
        Assert.Equal(2, run.Events().Count(e => e.Event.Kind == "RUNE_TAPPED"));
        Assert.Equal(paySecond ? 2 : 1, run.Events().Count(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "COUNTER_TARGET_MANA_PAYMENT")));
        await run.Cold();
        await run.Replay();
    }

    [Theory]
    [InlineData("mana", false)] [InlineData("mana", true)]
    [InlineData("restricted", false)] [InlineData("restricted", true)]
    public async Task DiagnosticEarlierCounterPaymentThenRuneGainUsesTheSameRepresentablePool(string balance, bool paySecond)
    {
        // Isolate the original four sequential consumers from the separate
        // initial full-menu guard. This exact test overlay is also executed
        // against the preserved2ace production binaries for the before control.
        var run = await Open("counter", balance, 0, echo: true);
        var original = run.State.PendingRuleChoice!.Request.Preview!.RunePools[run.Player];
        await run.Accept(new PayCostCommand(run.State.PendingRuleChoice.Request.Id, "RULE_REPLACEMENT", ["SPEND_MANA:2"]));
        await run.Accept(new PayCostCommand(run.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", ["TAP_RUNE:CAP-RUNE"]));
        await run.Accept(new PayCostCommand(run.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", ["TAP_RUNE:SPARE-RUNE"]));
        Assert.True(run.State.RunePools[run.Player].HasSupportedAmounts);
        Assert.Equal(int.MaxValue, (long)run.State.RunePools[run.Player].Mana + run.State.RunePools[run.Player].SpellOnlyMana);
        await run.Cold();
        await run.Accept(new PayCostCommand(run.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT",
            [paySecond ? "SPEND_MANA:2" : "DECLINE"]));
        Assert.Equal(original.Mana - (paySecond ? 2 : 0), run.State.RunePools[run.Player].Mana);
        Assert.Equal(original.SpellOnlyMana, run.State.RunePools[run.Player].SpellOnlyMana);
        Assert.Equal(2, run.Events().Count(e => e.Event.Kind == "RUNE_TAPPED"));
        Assert.Equal(paySecond ? 2 : 1, run.Events().Count(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "COUNTER_TARGET_MANA_PAYMENT")));
        await run.Cold(); await run.Replay();
    }

    [Theory]
    [InlineData("counter", false)] [InlineData("counter", true)]
    [InlineData("token", false)] [InlineData("token", true)]
    public async Task AnEarlierCompletedPaymentSurvivesTheNextChoiceColdRetryAndConcession(string kind, bool concede)
    {
        var run = await Open(kind, "finite", 0, echo: true);
        var before = run.State.RunePools[run.Player];
        var choice = kind == "counter" ? "SPEND_MANA:2" : "PAY:ANY";
        var reason = kind == "counter" ? "COUNTER_TARGET_MANA_PAYMENT" : "TOKEN_HASTE_PAYMENT";
        var paid = await run.Submit(new PayCostCommand(run.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [choice]));
        Assert.True(paid.Accepted, paid.ErrorMessage); Assert.NotNull(run.State.PendingRuleChoice);
        var expected = before with { Mana = before.Mana - (kind == "counter" ? 2 : 1),
            Power = before.Power - (kind == "token" ? 1 : 0) };
        Assert.Equal(expected, run.State.RunePools[run.Player]);
        Assert.Single(paid.Events, e => e.Kind == "COST_PAID" && Equals(e.Payload.GetValueOrDefault("reason"), reason));
        await run.Cold(); await run.RepeatLast(); Assert.Equal(expected, run.State.RunePools[run.Player]);
        if (concede) await run.Accept(new SurrenderCommand());
        else await run.Accept(new PayCostCommand(run.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", ["DECLINE"]));
        Assert.Null(run.State.PendingRuleChoice); Assert.Equal(expected, run.State.RunePools[run.Player]);
        Assert.Single(run.Events(), e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("reason"), reason));
        await run.Cold(); await run.Replay();
        Evidence("finite-payment-" + kind + "-" + concede,
            new { run.Initial, run.State, commands = run.Commands(), events = run.Events() });
    }

    [Theory]
    [InlineData("counter", "mana")]
    [InlineData("token", "mana")]
    [InlineData("token", "power")]
    public async Task TheNextChoiceCannotForgeARefundOfAnEarlierCompletedPayment(string kind, string currency)
    {
        var run = await Open(kind, "finite", 0, echo: true);
        await run.Accept(new PayCostCommand(run.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT",
            [kind == "counter" ? "SPEND_MANA:2" : "PAY:ANY"]));
        var state = run.State; var pool = state.RunePools[run.Player];
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var refunded = currency == "mana" ? pool with { Mana = pool.Mana + (kind == "counter" ? 2 : 1) }
            : pool with { Power = pool.Power + 1 };
        var forged = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { [run.Player] = refunded } };
        Assert.True(refunded.HasSupportedAmounts); Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        var command = new PayCostCommand(forged.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", ["DECLINE"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(forged,
            new("forged-paid-cost-refund", run.Player, command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.RecoveryInconsistent, rejected.ErrorCode); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(forged), MatchStateHasher.Hash(rejected.State));
        Assert.Equal(pool, run.State.RunePools[run.Player]);
        await run.Cold();
        await run.Accept(new PayCostCommand(run.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", ["DECLINE"]));
        Assert.Equal(pool, run.State.RunePools[run.Player]); await run.Replay();
        Evidence("forged-payment-refund-" + kind + "-" + currency, new { state, forged, command, rejected,
            final = run.State, commands = run.Commands(), events = run.Events() });
    }

    private static async Task<Run> Open(string kind, string balance, int capacity, bool echo = false)
    {
        var player = kind == "counter" ? "P2" : "P1";
        var initial = kind == "counter"
            ? OfficialCounterRepeatTests.Position("SFD·136/221") with { Tick = 0, UntilEndOfTurnEffects = [] }
            : GrantedTokenHasteTests.Position("SFD·029/221");
        var pool = balance switch {
            "finite" => new RunePool(10, kind == "token" ? 2 : 0),
            "mana" => new RunePool(int.MaxValue - capacity, kind == "token" ? 1 : 0),
            "restricted" => new RunePool(kind == "token" ? 4 : 2, kind == "token" ? 1 : 0,
                spellOnlyMana: int.MaxValue - capacity - (kind == "token" ? 4 : 2)),
            "wild" => new RunePool(10, int.MaxValue - capacity),
            _ => new RunePool(10, 0, new Dictionary<string, int> { [RuneTrait.Blue] = int.MaxValue - capacity })
        };
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { [player] = pool } };
        initial = Rune(Rune(initial, player, "CAP-RUNE"), player, "SPARE-RUNE");
        if (echo) initial = Rune(initial, player, "EXTRA-RUNE");
        if (kind == "token" && balance is "mana" or "restricted")
        {
            // The real parent spell pays two first. Two separate legal gains
            // refill that space before the real token instruction resolves.
            initial = Rune(Rune(initial, player, "FILL-1"), player, "FILL-2");
        }
        Assert.True(initial.RunePools[player].HasSupportedAmounts);
        var run = new Run(initial, player);
        await run.Accept(kind == "counter" ? new PlayCardCommand("C", "SFD·136/221", ["S1"],
            OptionalCosts: echo ? ["ECHO"] : null, RepeatChoices: echo ? [new("", ["S2"])] : null)
            : new PlayCardCommand("TOKEN-SPELL", "SFD·031/221", [], OptionalCosts: echo ? ["ECHO"] : null), "P1");
        if (kind == "token" && balance is "mana" or "restricted")
        {
            await run.Accept(new TapRuneCommand("FILL-1"));
            await run.Accept(new TapRuneCommand("FILL-2"));
        }
        for (var i = 0; i < 4 && run.State.PendingRuleChoice is null; i++)
            await run.Accept(new PassPriorityCommand(), run.State.PriorityPlayerId!);
        Assert.NotNull(run.State.PendingRuleChoice);
        Assert.Equal(player, run.State.PendingRuleChoice.Request.PlayerId);
        return run;
    }

    private static MatchState Rune(MatchState state, string player, string id) => state with {
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            [player] = state.PlayerZones[player] with { Base = [..state.PlayerZones[player].Base, id] } },
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: "OGN·007/298", ownerId: player, controllerId: player,
                tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BASE") }
    };

    private static void Evidence(string name, object value)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_CHOICE_CAPACITY_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(value, value.GetType(), Web));
    }

    private sealed class Run : IMatchJournal
    {
        private MatchSession session;
        private readonly List<MatchJournalEntry> entries = [];
        private readonly string evidenceId = Guid.NewGuid().ToString("N");
        private int intents;
        public MatchState Initial { get; }
        public MatchState State { get; private set; }
        public string Player { get; }
        public Run(MatchState initial, string player)
        {
            Initial = State = initial; Player = player; session = new(initial, new CoreRuleEngine(), this);
            Evidence("initial-" + evidenceId, new { initial, player });
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { entries.Add(entry); return ValueTask.CompletedTask; }
        public RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId,
            e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence,
            e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs, TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode }).ToArray();
        public RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((ev, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        public async Task<ResolutionResult> Submit(GameCommand command, string? player = null)
        {
            var actor = player ?? Player;
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            raw["promptId"] = session.PromptFor(actor).PromptId;
            raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw);
            var intent = "capacity-choice-" + intents++; var before = State;
            ResolutionResult result;
            try { result = await session.SubmitAsync(actor, intent, GameCommandJsonMapper.Map(wire), wire, default); }
            catch (Exception exception)
            {
                Evidence("exception-" + evidenceId + "-" + intents,
                    new { actor, intent, wire, before, exception = exception.ToString(), commands = Commands(), events = Events() });
                throw;
            }
            if (result.Accepted) State = result.State;
            Evidence("command-" + evidenceId + "-" + intents, new { actor, intent, wire, before, result });
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            return result;
        }
        public async Task Accept(GameCommand command, string? player = null)
        { var result = await Submit(command, player); Assert.True(result.Accepted, result.ErrorMessage); }
        public async Task Reject(GameCommand command)
        {
            var hash = MatchStateHasher.Hash(State);
            var result = await Submit(command);
            Assert.False(result.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode); Assert.Empty(result.Events);
            Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        }
        public async Task RepeatLast()
        {
            var last = entries[^1]; var count = entries.Count; var hash = MatchStateHasher.Hash(State);
            var duplicate = await session.SubmitAsync(last.PlayerId, last.ClientIntentId,
                GameCommandJsonMapper.Map(last.RawCommand!.Value), last.RawCommand, default);
            Assert.Equal(last.Accepted, duplicate.Accepted); Assert.Empty(duplicate.Events);
            Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            Assert.Equal(hash, MatchStateHasher.Hash(State));
            Evidence("duplicate-" + evidenceId + "-" + intents, new { last.PlayerId, last.ClientIntentId, duplicate });
        }
        public Task Cold()
        {
            var sequence = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, sequence, snapshots[p], State.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var frame = new MatchRecoveryFrame(State.RoomId, State.Tick, sequence, Commands(), Events(), views, [], State, Initial, spectator);
            var clone = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
            Assert.Empty(MatchRecoveryValidator.Validate(clone.RoomId, sequence, clone.Commands, clone.Events, clone.PlayerViews,
                clone.AuthoritativeState, clone.CurrentTick, clone.SpectatorReplayFrame));
            session = MatchSession.Restore(clone, new CoreRuleEngine(), this);
            foreach (var player in State.Seats.Keys)
            {
                Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(session.SnapshotFor(player)));
                Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(session.PromptFor(player)));
            }
            State = clone.AuthoritativeState!;
            return Task.CompletedTask;
        }
        public async Task Replay()
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(Initial, Commands(), State,
                new CoreRuleEngine(), default, Events());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(State));
            ArmamentMightTests.PublicRecovery(State);
        }
    }
}
