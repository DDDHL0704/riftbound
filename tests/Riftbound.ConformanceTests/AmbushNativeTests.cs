using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AmbushNativeTests
{
    [Theory]
    [InlineData("pay-exp")] [InlineData("no-exp")] [InlineData("opponent-pay-exp")]
    public async Task ProductionBattlefieldSelectionExperienceCostAndResponsesReplay(string branch)
    {
        var actor = branch.StartsWith("opponent") ? "P2" : "P1"; var pay = branch != "no-exp";
        var initial = AmbushPermissionTests.Position(friendly: actor == "P1");
        if (actor == "P2") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["AMBUSH"] = initial.CardObjects["AMBUSH"] with { OwnerId = "P2", ControllerId = "P2" } },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "AMBUSH").ToArray() },
                ["P2"] = initial.PlayerZones["P2"] with { Hand = ["AMBUSH"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["AMBUSH"] = new("P2", "HAND") },
            PlayerExperience = new Dictionary<string, int>(initial.PlayerExperience) { ["P2"] = 6 },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(10, 2, powerByTrait: new Dictionary<string, int> { [RuneTrait.Yellow] = 2 }) } };
        initial = await AmbushPermissionTests.Response(initial);
        if (actor == "P2") initial = (await OfficialGraveyardRecastTests.Act(initial, "P1", new PassPriorityCommand())).State;
        initial = OfficialInsightAndSpellLockTests.Restore(initial);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_AMBUSH_EVIDENCE"); var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        JsonElement Raw(string name, object fallback) {
            if (dir is null || !File.Exists(Path.Combine(dir, name + "-command.json"))) return JsonSerializer.SerializeToElement(fallback, json);
            using var d = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-command.json"))); return d.RootElement.Clone();
        }
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var prompt = session.PromptFor(actor); var optional = pay ? new[] { "SPEND_EXPERIENCE:3" } : [];
        Export("snapshot.json", session.SnapshotFor(actor)); Export("play-prompt.json", prompt);
        Export("selection.json", new { actor, sourceObjectId = "AMBUSH", cardNo = "UNL-178/219", destination = "BATTLEFIELD:F", optionalCosts = optional });
        var command = new PlayCardCommand("AMBUSH", "UNL-178/219", [], OptionalCosts: optional, Destination: "BATTLEFIELD:F");
        var request = new PlayCostPreviewRequestDto("quote", prompt.PromptId!, initial.Tick, command);
        var nativeRequest = dir is not null && File.Exists(Path.Combine(dir, "play-preview.json"));
        if (nativeRequest) request = JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir!, "play-preview.json")), json)!;
        Assert.Equal("BATTLEFIELD:F", request.Command.Destination); Assert.Equal(optional, request.Command.OptionalCosts);
        var quote = engine.PreviewPlayCard(initial, actor, request); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(pay ? 3 : 6, quote.Cost!.Mana); Assert.Equal(pay ? 3 : 0, quote.Cost.Experience);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Yellow]); Export("play-quote.json", quote);
        var raw = Raw("play", new { cmdType = "PLAY_CARD", sourceObjectId = "AMBUSH", cardNo = "UNL-178/219", mode = "",
            destination = "BATTLEFIELD:F", targetObjectIds = Array.Empty<string>(), optionalCosts = optional, promptId = prompt.PromptId, snapshotTick = initial.Tick });
        var mapped = GameCommandJsonMapper.Map(raw); var done = await session.SubmitAsync(actor, "play", mapped, raw, default); Assert.True(done.Accepted, done.ErrorMessage);
        Assert.Contains("AMBUSH", done.State.PlayerZones[actor].Battlefields); Assert.Equal(1, done.State.CardObjects["AMBUSH"].ObjectGeneration);
        Assert.Equal(pay ? 3 : 6, done.State.PlayerExperience[actor]); Assert.DoesNotContain(done.State.StackItems, s => s.SourceObjectId == "AMBUSH");
        var duplicate = await session.SubmitAsync(actor, "play", mapped, raw, default); Assert.True(duplicate.Accepted); Assert.Single(journal.Entries);
        var stale = await session.SubmitAsync(actor, "stale", mapped, raw, default); Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(stale.State));
        var steps = new[] { "pass-1", "pass-2" }; Export("steps.json", steps);
        var native = dir is not null && File.Exists(Path.Combine(dir, "play-command.json")) ? 1 : 0;
        foreach (var step in steps) {
            var player = done.State.PriorityPlayerId!; prompt = session.PromptFor(player); Export(step + "-prompt.json", prompt);
            var nextRaw = Raw(step, new { cmdType = "PASS_PRIORITY", promptId = prompt.PromptId, snapshotTick = done.State.Tick });
            if (dir is not null && File.Exists(Path.Combine(dir, step + "-command.json"))) native++;
            done = await session.SubmitAsync(player, step, GameCommandJsonMapper.Map(nextRaw), nextRaw, default); Assert.True(done.Accepted, done.ErrorMessage);
            OfficialGraveyardRecastTests.Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        }
        Assert.Empty(done.State.StackItems); Assert.True(done.State.CardObjects["AMBUSH"].IsExhausted);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, done.State, engine, default, events); Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("replay.json", new { actor, nativeRequest, nativeCommands = native, accepted = 3, staleRejected = 1, replay, finalHash = MatchStateHasher.Hash(done.State) });
        Export("result-snapshot.json", done.Snapshots[actor]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
