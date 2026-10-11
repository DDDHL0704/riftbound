using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ArmamentMightTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class TemperedPlayNativeTests
{
    [Theory]
    [InlineData("axe")]
    [InlineData("decline")]
    [InlineData("colored-rune")]
    [InlineData("jax")]
    public async Task ProductionPlayConfirmationResourceAndResponseCommandsRecoverAndReplayExactly(string scenario)
    {
        var initial = TemperedPlayTriggerAuditTests.PositionFor(scenario == "jax" ? "SFD·119/221" : "SFD·008/221",
            scenario == "colored-rune" ? "SFD·022/221" : "SFD·186/221");
        if (scenario == "colored-rune") initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 0) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["RUNE"] = new("RUNE", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = initial.PlayerZones["P1"].Base.Append("RUNE").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["RUNE"] = new("P1", "BASE") }
        };
        if (scenario == "jax") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["DRAW"] = new("DRAW", cardNo: "OGN·114/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = initial.PlayerZones["P1"].MainDeck.Prepend("DRAW").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["DRAW"] = new("P1", "MAIN_DECK") }
        };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var root = Environment.GetEnvironmentVariable("RIFTBOUND_TEMPERED_EVIDENCE");
        if (root is not null) root = Path.Combine(root, scenario);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; var native = 0;
        void Export(string name, object value) {
            if (root is null) return; Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task<ResolutionResult> Submit(string name, string player, GameCommand fallback) {
            var prompt = session.PromptFor(player); Export(name + "-prompt.json", prompt);
            Export(name + "-snapshot.json", ResolutionResult.BuildSnapshots(current)[player]); var command = fallback;
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), options);
            var path = root is null ? null : Path.Combine(root, name + "-command.json");
            if (path is not null && File.Exists(path)) {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                Assert.Equal(prompt.PromptId, doc.RootElement.GetProperty("promptId").GetString());
                Assert.Equal(current.Tick, doc.RootElement.GetProperty("snapshotTick").GetInt64());
                raw = doc.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw); native++;
            }
            var result = await session.SubmitAsync(player, scenario + "-" + journal.Entries.Count, command,
                raw, default);
            Assert.True(result.Accepted, result.ErrorMessage); current = result.State;
            Restore(current); PublicRecovery(current); return result;
        }
        var result = await Submit("play", "P1", new PlayCardCommand("HOST", initial.CardObjects["HOST"].CardNo!, []));
        Assert.Contains("HOST", result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["GEAR"].AttachedToObjectId);
        var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        result = await Submit("confirm", "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, scenario == "decline" ? [] : ["GEAR"]));
        if (scenario != "decline") {
            Assert.Null(result.State.CardObjects["GEAR"].AttachedToObjectId);
            result = await Submit("pass-1", "P1", new PassPriorityCommand());
            result = await Submit("pass-2", "P2", new PassPriorityCommand());
        }
        if (scenario == "colored-rune") {
            Assert.NotNull(result.State.PendingPayment); Assert.Null(result.State.CardObjects["GEAR"].AttachedToObjectId);
            result = await Submit("tap", "P1", new TapRuneCommand("RUNE"));
            Assert.True(result.State.CardObjects["RUNE"].IsExhausted);
            result = await Submit("recycle", "P1", new RecycleRuneCommand("RUNE"));
            Assert.Contains("RUNE", result.State.PlayerZones["P1"].RuneDeck);
            var payment = result.State.PendingPayment!;
            result = await Submit("pay", "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            Assert.Equal(0, result.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        }
        if (scenario == "jax") {
            Assert.DoesNotContain("DRAW", result.State.PlayerZones["P1"].Hand);
            var payment = Assert.IsType<PendingPaymentState>(result.State.PendingPayment);
            result = await Submit("draw-pay", "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            Assert.DoesNotContain("DRAW", result.State.PlayerZones["P1"].Hand);
            result = await Submit("draw-pass-1", "P1", new PassPriorityCommand());
            result = await Submit("draw-pass-2", "P2", new PassPriorityCommand());
            Assert.Contains("DRAW", result.State.PlayerZones["P1"].Hand);
            Assert.Single(result.Events, e => e.Kind == "CARD_DRAWN");
        }
        Assert.Equal(scenario == "decline" ? null : "HOST", result.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems); Assert.Null(result.State.PendingPayment); Assert.Null(result.State.PendingCardChoice);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (native > 0) Assert.Equal(commands.Length, native);
        Export("result-snapshot.json", result.Snapshots["P1"]); Export("replay.json", new { nativeCommands = native, commands = commands.Length, replay });
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
