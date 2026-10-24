using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MightyTransitionNativeTests
{
    [Theory]
    [InlineData("unit-pay")] [InlineData("unit-decline")]
    [InlineData("legend-pay")] [InlineData("legend-decline")]
    public async Task ProductionAssemblyOptionalCostAndResponsesReplay(string branch)
    {
        var legend = branch.StartsWith("legend", StringComparison.Ordinal); var accept = branch.EndsWith("-pay", StringComparison.Ordinal);
        var initial = OfficialInsightAndSpellLockTests.Restore(legend ? MightyTransitionTests.LegendSource() : MightyTransitionTests.UnitSource());
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_MIGHTY_TRANSITION_EVIDENCE"); var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; var native = true;
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        var steps = accept ? new[] { "assemble", "equip-pass-1", "equip-pass-2", "cost", "pass-1", "pass-2" }
            : new[] { "assemble", "equip-pass-1", "equip-pass-2", "cost" };
        Export("steps.json", steps); var state = initial; ResolutionResult? done = null;
        foreach (var name in steps) {
            var actor = state.PendingPayment?.PlayerId ?? state.PendingCardChoice?.PlayerId ?? state.PriorityPlayerId ?? "P1";
            var prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt); Export(name + "-snapshot.json", session.SnapshotFor(actor));
            GameCommand command = name switch {
                "assemble" => new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]),
                "cost" when legend => new ChooseCardsCommand(state.PendingCardChoice!.ChoiceId, state.PendingCardChoice.ChoiceWindow, accept ? ["LEGEND"] : []),
                "cost" => new PayCostCommand(state.PendingPayment!.PaymentId, state.PendingPayment.PaymentWindow, [accept ? "SPEND_POWER:yellow:1" : "DECLINE"]),
                _ => new PassPriorityCommand() };
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), json), json)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(state.Tick);
            var raw = JsonSerializer.SerializeToElement(fields, json); var path = dir is null ? null : Path.Combine(dir, name + "-command.json");
            if (path is not null && File.Exists(path)) { using var doc = JsonDocument.Parse(File.ReadAllText(path)); raw = doc.RootElement.Clone(); }
            else native = false;
            command = GameCommandJsonMapper.Map(raw);
            done = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(done.Accepted, done.ErrorMessage);
            if (name == "assemble") {
                var duplicate = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(duplicate.Accepted); Assert.Single(journal.Entries);
                var stale = await session.SubmitAsync(actor, "stale", command, raw, default); Assert.False(stale.Accepted);
                Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(stale.State));
            }
            state = done.State; OfficialGraveyardRecastTests.Restore(state); ArmamentMightTests.PublicRecovery(state);
            if (name == "equip-pass-2") {
                Assert.Equal(5, ArmamentMightTests.Might(state));
                Assert.NotNull(Assert.Single(state.StackItems).MightySkill);
                Assert.Equal(legend, state.PendingCardChoice is not null); Assert.Equal(!legend, state.PendingPayment is not null);
            }
            if (name == "cost") {
                Assert.DoesNotContain(done.Events, e => e.Kind is "UNIT_READIED" or "RUNES_CALLED");
                Assert.Equal(3, state.PlayerZones["P1"].RuneDeck.Count);
                if (legend) Assert.Equal(accept, state.CardObjects["LEGEND"].IsExhausted);
                else Assert.True(state.CardObjects["HOST"].IsExhausted);
            }
        }
        Assert.Empty(state.StackItems); Assert.Null(state.PendingPayment); Assert.Null(state.PendingCardChoice);
        if (legend) Assert.Equal(accept ? 2 : 3, state.PlayerZones["P1"].RuneDeck.Count);
        else Assert.Equal(!accept, state.CardObjects["HOST"].IsExhausted);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, state, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Equal(steps.Length, commands.Count(c => c.Accepted));
        Export("replay.json", new { native, accepted = steps.Length, staleRejected = commands.Count(c => !c.Accepted), replay, finalHash = MatchStateHasher.Hash(state) });
        Export("result-snapshot.json", done!.Snapshots["P1"]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
