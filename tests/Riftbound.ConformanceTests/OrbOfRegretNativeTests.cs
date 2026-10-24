using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OrbOfRegretNativeTests
{
    [Theory]
    [InlineData("aura-floor")]
    [InlineData("deflect-recycle")]
    public async Task ProductionAbilityCommandsPayResolveRecoverAndReplay(string branch)
    {
        var initial = OrbOfRegretTests.Board(branch == "aura-floor" ? 1 : 4, branch == "aura-floor");
        string[] costs = branch == "deflect-recycle" ? ["RECYCLE_RUNE:RUNE"] : [];
        if (branch == "deflect-recycle") initial = initial with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = new(20, 20) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["D"] = initial.CardObjects["D"] with { CardNo = "SFD·125/221", Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] },
                ["RUNE"] = new("RUNE", isExhausted: true, cardNo: "UNL-R01", ownerId: "P1", controllerId: "P1",
                    tags: [CardObjectTags.RuneCard, "COLOR:RED"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "RUNE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["RUNE"] = new("P1", "BASE") } };
        initial = OfficialInsightAndSpellLockTests.Restore(initial);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_ORB_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        JsonElement Raw(string name, object fallback) {
            if (dir is null || !File.Exists(Path.Combine(dir, name + "-command.json"))) return JsonSerializer.SerializeToElement(fallback, json);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-command.json")));
            return document.RootElement.Clone();
        }
        Export("selection.json", new { sourceObjectId = "ORB", abilityId = OrbOfRegretTests.Ability, targetObjectIds = new[] { "D" }, optionalCosts = costs });
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var prompt = session.PromptFor("P1"); Export("activate-prompt.json", prompt);
        Export("snapshot.json", ResolutionResult.BuildSnapshots(initial)["P1"]);
        var raw = Raw("activate", new { cmdType = "ACTIVATE_ABILITY", sourceObjectId = "ORB", abilityId = OrbOfRegretTests.Ability,
            targetObjectIds = new[] { "D" }, optionalCosts = costs, promptId = prompt.PromptId, snapshotTick = initial.Tick });
        var command = Assert.IsType<ActivateAbilityCommand>(GameCommandJsonMapper.Map(raw));
        Assert.Equal("ORB", command.SourceObjectId); Assert.Equal(OrbOfRegretTests.Ability, command.AbilityId);
        Assert.Equal(new[] { "D" }, command.TargetObjectIds); Assert.Equal(costs, command.OptionalCosts);
        Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString()); Assert.Equal(initial.Tick, raw.GetProperty("snapshotTick").GetInt64());
        var activated = await session.SubmitAsync("P1", "native-orb", command, raw, default);
        Assert.True(activated.Accepted, activated.ErrorMessage); Assert.True(activated.State.CardObjects["ORB"].IsExhausted);
        Assert.Equal(initial.CardObjects["D"].Power, activated.State.CardObjects["D"].Power);
        Assert.Equal(branch == "deflect-recycle" ? 1 : 0, Assert.Single(activated.Events, e => e.Kind == "COST_PAID").Payload["spellshieldTaxPower"]);
        OfficialGraveyardRecastTests.Restore(activated.State); Assert.Empty(MinimumMightAuditTests.PublicErrors(activated.State));
        var duplicate = await session.SubmitAsync("P1", "native-orb", command, raw, default);
        Assert.Equal(MatchStateHasher.Hash(activated.State), MatchStateHasher.Hash(duplicate.State));
        var stale = await session.SubmitAsync("P1", "stale-orb", command, raw, default);
        Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(activated.State), MatchStateHasher.Hash(stale.State));
        var done = activated;
        foreach (var name in new[] { "pass-1", "pass-2" }) {
            var player = done.State.PriorityPlayerId!; prompt = session.PromptFor(player); Export(name + "-prompt.json", prompt);
            var passRaw = Raw(name, new { cmdType = "PASS_PRIORITY", promptId = prompt.PromptId, snapshotTick = done.State.Tick });
            Assert.Equal(prompt.PromptId, passRaw.GetProperty("promptId").GetString()); Assert.Equal(done.State.Tick, passRaw.GetProperty("snapshotTick").GetInt64());
            done = await session.SubmitAsync(player, name, Assert.IsType<PassPriorityCommand>(GameCommandJsonMapper.Map(passRaw)), passRaw, default);
            Assert.True(done.Accepted, done.ErrorMessage);
        }
        Assert.Equal(branch == "aura-floor" ? 0 : 3, done.State.CardObjects["D"].Power);
        Assert.Equal(branch == "aura-floor" ? 2 : 4, Assert.Single(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers).SnapshotMight);
        if (branch == "deflect-recycle") { Assert.Equal(0, done.State.RunePools["P1"].TotalPower); Assert.Contains("RUNE", done.State.PlayerZones["P1"].RuneDeck); }
        OfficialGraveyardRecastTests.Restore(done.State); Assert.Empty(MinimumMightAuditTests.PublicErrors(done.State));
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
            e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, done.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("replay.json", new { native = dir is not null && File.Exists(Path.Combine(dir, "activate-command.json")), commands = commands.Length,
            replay, finalHash = MatchStateHasher.Hash(done.State) });
        Export("result-snapshot.json", done.Snapshots["P1"]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
