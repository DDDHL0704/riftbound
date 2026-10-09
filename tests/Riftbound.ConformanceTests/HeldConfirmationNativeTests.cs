using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialHeldConfirmationTests;

namespace Riftbound.ConformanceTests;

public sealed class HeldConfirmationNativeTests
{
    [Theory]
    [InlineData("hero", true)]
    [InlineData("hero", false)]
    [InlineData("boon", true)]
    [InlineData("move", true)]
    [InlineData("move", false)]
    [InlineData("channel", true)]
    [InlineData("channel", false)]
    [InlineData("ward", true)]
    [InlineData("ward", false)]
    [InlineData("return", true)]
    [InlineData("return", false)]
    public async Task ProductionHeldChoicesRestoreAndReplay(string name, bool accept)
    {
        var initial = name == "ward" ? WardPosition(true) : name == "hero" ? OfficialChosenChampionReturnTests.Position() : Position(name);
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_HELD_CONFIRMATION_EVIDENCE");
        var branch = name + (accept ? "-accept" : "-decline");
        var directory = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var usedNative = false;
        void Export(string name, object value) {
            if (directory is null) return; Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        async ValueTask<ResolutionResult> Submit(string player, GameCommand command) {
            var result = await session.SubmitAsync(player, branch + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), json), default);
            Assert.True(result.Accepted, result.ErrorMessage); OfficialGraveyardRecastTests.Restore(result.State); return result;
        }
        var result = await Submit("P1", new PassPriorityCommand());
        if (name == "ward") result = await Submit("P1", new ChooseCardsCommand(result.State.PendingCardChoice!.ChoiceId,
            result.State.PendingCardChoice.ChoiceWindow, ["ENEMY"]));
        var prompt = session.PromptFor("P1"); Export("prompt.json", prompt); Export("snapshot.json", result.Snapshots["P1"]);
        GameCommand command = name == "ward"
            ? new PayCostCommand(result.State.PendingPayment!.PaymentId, result.State.PendingPayment.PaymentWindow, [accept ? "PAY" : "DECLINE"])
            : new ChooseCardsCommand(result.State.PendingCardChoice!.ChoiceId, result.State.PendingCardChoice.ChoiceWindow, accept ? [name == "hero" ? "HERO" : Pick(name)] : []);
        var path = directory is null ? null : Path.Combine(directory, "command.json");
        if (path is not null && File.Exists(path)) {
            using var doc = JsonDocument.Parse(File.ReadAllText(path)); command = GameCommandJsonMapper.Map(doc.RootElement);
            Assert.Equal(prompt.PromptId, doc.RootElement.GetProperty("promptId").GetString());
            Assert.Equal(result.State.Tick, doc.RootElement.GetProperty("snapshotTick").GetInt64()); usedNative = true;
        }
        result = await Submit("P1", command);
        Assert.DoesNotContain(result.Events, e => e.Kind is "BOON_GRANTED" or "UNIT_MOVED_TO_BASE" or "CARD_RETURNED_TO_HAND");
        if (accept) for (var i = 0; i < 2; i++) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
        if (name == "ward") Assert.Equal(accept, result.State.CardObjects["ENEMY"].Tags.Contains(CardObjectTags.Boon));
        else if (name == "hero") Assert.Equal(accept ? new[] { "HERO" } : [], result.State.PlayerZones["P1"].ChampionZone);
        else AssertOutcome(result.State, name, accept);
        Assert.Empty(result.State.StackItems); Assert.Null(result.State.PendingCardChoice);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Export("replay.json", new { usedNative, replay });
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
