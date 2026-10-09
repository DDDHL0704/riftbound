using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class InsightSourceContinuationTests
{
    [Theory]
    [InlineData("blossom", 0)]
    [InlineData("blossom", 1)]
    [InlineData("blossom", 2)]
    [InlineData("diana", -1)]
    [InlineData("diana", 0)]
    [InlineData("diana", 1)]
    public async Task ProductionCommandsRestoreAndReplayFullContinuation(string scenario, int recycle)
    {
        var initial = scenario == "blossom" ? OfficialInsightSourceTests.Blossom() : OfficialInsightSourceTests.Diana();
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_INSIGHT_SOURCE_EVIDENCE");
        JsonElement? suppliedRaw = null;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value)
        {
            if (string.IsNullOrEmpty(root)) return;
            Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        GameCommand Native(string name, GameCommand fallback)
        {
            var path = string.IsNullOrEmpty(root) ? null : Path.Combine(root, "native-" + name + "-command.json");
            if (path is null || !File.Exists(path)) return fallback;
            using var doc = JsonDocument.Parse(File.ReadAllText(path)); suppliedRaw = doc.RootElement.Clone(); return GameCommandJsonMapper.Map(doc.RootElement);
        }
        async Task<ResolutionResult> Submit(string player, GameCommand command)
        {
            var raw = suppliedRaw ?? JsonSerializer.SerializeToElement(command, command.GetType(), json);
            suppliedRaw = null;
            var result = await session.SubmitAsync(player, scenario + journal.Entries.Count, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage); OfficialInsightSourceTests.Restore(result.State); return result;
        }
        ResolutionResult result;
        if (scenario == "blossom")
        {
            Export("blossom-activation-prompt.json", session.PromptFor("P1"));
            result = await Submit("P1", Native("blossom-activation", new ActivateAbilityCommand("B", P4ActivatedAbilityCatalog.ScryingBlossomAbilityId, [])));
        }
        else result = await Submit("P1", new MoveUnitCommand("UNIT", Destination: "BATTLEFIELD:HILL", SourceObjectIds: ["UNIT"]));
        if (scenario == "diana")
        {
            Export("diana-payment-prompt.json", result.Prompts["P2"]);
            var payment = result.State.PendingPayment!;
            result = await Submit("P2", Native(recycle < 0 ? "diana-decline" : "diana-pay",
                new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [recycle < 0 ? "DECLINE" : "PAY"])));
        }
        if (recycle >= 0)
        {
            result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
            result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
            Export(scenario + "-prompt.json", result.Prompts[scenario == "blossom" ? "P1" : "P2"]);
            var choice = result.State.PendingCardChoice!;
            var branch = recycle == 0 ? "keep" : recycle == 1 ? "recycle" : "all";
            result = await Submit(choice.PlayerId, Native(scenario + "-" + branch,
                new ChooseCardsCommand(choice.ChoiceId, "INSIGHT", new[] { "D1", "D2" }.Take(recycle).ToArray())));
            if (result.State.PendingCardChoice is { } order)
            {
                Export("blossom-order-prompt.json", result.Prompts["P1"]);
                result = await Submit("P1", Native("blossom-order", new ChooseCardsCommand(order.ChoiceId, "INSIGHT_ORDER", ["D2", "D1"])));
                Assert.Equal("D2", Assert.Single(result.State.PlayerZones["P1"].Hand));
            }
        }
        Assert.Empty(result.State.StackItems); Assert.Null(result.State.PendingCardChoice); Assert.Null(result.State.PendingPayment);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Export(scenario + "-" + recycle + "-replay.json", replay);
    }
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
