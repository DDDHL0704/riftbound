using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SivirPaymentNativeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ProductionInlineAndDeferredPaymentControlsColdRecoverDeduplicateAndReplay(bool deferred, bool pay)
    {
        var initial = deferred ? await SivirPaymentTriggerTests.PendingWardPosition("SFD·203/221")
            : SivirPaymentTriggerTests.InlinePosition("SFD·203/221");
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_SIVIR_PAYMENT_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, (deferred ? "deferred" : "inline") + (pay ? "-pay" : "-decline"));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var steps = new List<string>(); var native = 0;
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        Export("initial.json", initial);
        if (deferred) {
            await Submit("resource", "P1", new RecycleRuneCommand("RUNE"));
            Assert.Null(current.PendingCardChoice); Assert.Single(current.TriggerQueue);
            var pending = current.PendingPayment!;
            await Submit("parent-payment", "P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]));
        } else await Submit("play", "P1", new PlayCardCommand("CARD", "OGS·003/024", ["T"], OptionalCosts: ["RECYCLE_RUNE:RUNE"]));
        var choice = Assert.IsType<PendingCardChoiceState>(current.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow); Assert.False(current.CardObjects["SIVIR"].IsExhausted);
        await Submit("choose", "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, pay ? ["SIVIR"] : []));
        Assert.Equal(pay, current.CardObjects["SIVIR"].IsExhausted);
        if (pay) for (var i = 0; i < 2; i++) await Submit("gold-pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
        for (var i = 0; i < 2; i++) await Submit("parent-pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Empty(current.StackItems); Assert.Empty(current.TriggerQueue);
        Assert.Equal(deferred ? 5 : 2, current.CardObjects[deferred ? "ENEMY" : "T"].Damage);
        Assert.Equal(pay ? 1 : 0, current.PlayerZones["P1"].Base.Except(initial.PlayerZones["P1"].Base).Count());
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), current, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("steps.json", steps); Export("receipt.json", new { deferred, pay, native, commands = journal.Entries.Count, restores = steps.Count, events = Events().Length, replay });
        if (native > 0) Assert.Equal(steps.Count, native);
        async Task Submit(string name, string actor, GameCommand fallback) {
            var prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt); Export(name + "-selection.json", fallback);
            if (fallback is PlayCardCommand play) Export(name + "-quote.json", engine.PreviewPlayCard(current, actor, new("quote", prompt.PromptId!, current.Tick, play)));
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fallback, fallback.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields); var command = fallback;
            if (dir is not null && File.Exists(Path.Combine(dir, name + "-command.json"))) {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-command.json")));
                raw = document.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw); native++;
            }
            Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString()); Assert.Equal(current.Tick, raw.GetProperty("snapshotTick").GetInt64());
            var result = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(result.Accepted, result.ErrorMessage);
            current = OfficialInsightAndSpellLockTests.Restore(result.State); steps.Add(name);
            var count = journal.Entries.Count; Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            var sequence = journal.Entries[^1].CompletedEventSequence;
            session = MatchSession.Restore(new(initial.RoomId, current.Tick, sequence, Commands(), Events(), new Dictionary<string, RecoveredPlayerView>(), [], current, initial), engine, journal);
            Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            var stale = await session.SubmitAsync(actor, "stale-" + name, command, raw, default);
            Assert.False(stale.Accepted); Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode); Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(stale.State));
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
