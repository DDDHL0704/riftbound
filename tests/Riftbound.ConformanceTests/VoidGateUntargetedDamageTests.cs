using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VoidGateUntargetedDamageTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var spell in new[] { "OGN·133/298", "OGN·123/298", "OGN·127/298" })
        foreach (var localBonus in new[] { false, true })
        foreach (var annie in new[] { false, true })
            yield return [spell, localBonus, annie];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task UntargetedDamageKeepsGlobalAndLocalBonusesSeparateForEachActualRecipient(
        string spell, bool localBonus, bool annie)
    {
        var combatOnly = spell == "OGN·127/298";
        var overload = spell == "OGN·123/298";
        var baseDamage = overload ? 12 : combatOnly ? 2 : 1;
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string player, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
            locations[id] = new(player, zone, field);
        }
        foreach (var field in new[] { "A", "B", "C" })
        {
            var player = field == "C" ? "P1" : "P2";
            Put(field, localBonus && field != "B" ? "OGN·296/298" : "OGN·278/298", player, "BATTLEFIELD", field);
            cards[field] = cards[field] with { ControllerId = player };
        }
        Put("P2-A1", "OGN·219/298", "P2", "BATTLEFIELD", "A");
        Put("P2-A2", "OGN·175/298", "P2", "BATTLEFIELD", "A");
        Put("P2-B", "OGN·219/298", "P2", "BATTLEFIELD", "B");
        Put("P1-C", "OGN·219/298", "P1", "BATTLEFIELD", "C");
        Put("P1-BASE", "OGN·219/298", "P1", "BASE");
        Put("P2-BASE", "OGN·219/298", "P2", "BASE");
        Put("SPELL", spell, "P1", "HAND");
        if (annie) Put("ANNIE", "OGS·001/024", "P1", "BASE");
        var caseId = (spell + "-" + localBonus + "-" + annie).Replace('·', '-').Replace('/', '-');
        var initial = new MatchState("UNTARGETED-DAMAGE-" + caseId, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Base = annie ? ["P1-BASE", "ANNIE"] : ["P1-BASE"], Hand = ["SPELL"], Battlefields = ["C", "P1-C"] },
                ["P2"] = PlayerZones.Empty with { Base = ["P2-BASE"], Battlefields = ["A", "B", "P2-A1", "P2-A2", "P2-B"] }
            }, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) },
            seed: 424242) with { CardPlays = new(5, [], []) };
        var state = initial;
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string? failure = null;
        try
        {
            if (combatOnly)
            {
                await Act("P1", new MoveUnitCommand("P1-BASE", "BASE", "BATTLEFIELD:A", []));
                Assert.Equal("P1", state.FocusPlayerId);
                Assert.True(state.CardObjects["P2-A1"].IsDefending);
                Assert.True(state.CardObjects["P2-A2"].IsDefending);
                Assert.False(state.CardObjects["P2-B"].IsDefending);
            }
            await Act("P1", new PlayCardCommand("SPELL", spell, []));
            Assert.Empty(Assert.Single(state.StackItems).TargetObjectIds);
            Assert.NotNull(state.PriorityPlayerId);
            await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            var events = journal.Entries.SelectMany(e => e.Events).ToArray();
            var damage = events.Where(e => e.Kind == "DAMAGE_APPLIED"
                && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "SPELL")).ToDictionary(e => (string)e.Payload["targetObjectId"]!, e => (int)e.Payload["damage"]!);
            var expectedIds = combatOnly ? new[] { "P2-A1", "P2-A2" } : ["P2-A1", "P2-A2", "P2-B", "P1-C"];
            Assert.Equal(expectedIds.Order(), damage.Keys.Order());
            Assert.DoesNotContain("P1-BASE", damage.Keys);
            Assert.DoesNotContain("P2-BASE", damage.Keys);
            Assert.DoesNotContain("ANNIE", damage.Keys);
            var globalDamage = baseDamage + (annie ? 1 : 0);
            if (!combatOnly) Assert.Equal(globalDamage, damage["P2-B"]);
            if (overload)
            {
                Assert.True(state.CardObjects["P1-BASE"].IsExhausted);
                if (annie) Assert.True(state.CardObjects["ANNIE"].IsExhausted);
            }
            var affectedDamage = globalDamage + (localBonus ? 1 : 0);
            Assert.Equal(affectedDamage, damage["P2-A1"]);
            Assert.Equal(affectedDamage, damage["P2-A2"]);
            if (!combatOnly) Assert.Equal(affectedDamage, damage["P1-C"]);
            Assert.Equal(0, state.CardObjects["P2-BASE"].Damage);
            Assert.Equal(0, state.CardObjects["P1-BASE"].Damage);
        }
        catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; throw; }
        finally
        {
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
                e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence,
                e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1,
                e.CompletedTick, i, ev))).ToArray();
            Assert.Equal(commands.Length, states.Count);
            for (var i = 0; i < commands.Length; i++)
            {
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands.Take(i + 1).ToArray(), states[i],
                    new CoreRuleEngine(), default, events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
                Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            }
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_UNTARGETED_DAMAGE_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
                using var output = new GZipStream(File.Open(Path.Combine(folder, caseId + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
                JsonSerializer.Serialize(output, new { caseId, spell, localBonus, annie, initial, states,
                    entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length, failure }, json);
            }
        }

        async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            var result = await session.SubmitAsync(player, "untargeted-damage-" + journal.Entries.Count,
                command, JsonSerializer.SerializeToElement(raw, json), default);
            state = result.State;
            states.Add(state);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
