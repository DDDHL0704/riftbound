using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SelectedBattlefieldAreaDamageTests
{
    public static IEnumerable<object[]> DamageCases()
    {
        foreach (var card in new[] { "OGN·268/298", "OGN·250/298", "UNL-072/219" })
        foreach (var gate in new[] { false, true })
        foreach (var annie in new[] { false, true })
            yield return [card, gate, annie];
    }

    [Theory]
    [MemberData(nameof(DamageCases))]
    public Task SelectedBattlefieldSubmissionCannotDamageEnemiesAtTwoDifferentBattlefields(
        string card, bool gate, bool annie) => Run("scope", card, gate, annie, true);

    [Theory]
    [MemberData(nameof(DamageCases))]
    public Task SingleOccupiedBattlefieldIsolatesGlobalAndLocalDamageBonuses(
        string card, bool gate, bool annie) => Run("bonus", card, gate, annie, false);

    // Canonical integration contract: Bullet [field], Drop [baseUnit, field],
    // Crescent [field, enemyUnitAtField]. The pre-integration probe bytes and failures
    // are preserved in the source-red evidence archive committed before this adaptation.
    [Theory]
    [InlineData("OGN·268/298", "A")]
    [InlineData("OGN·268/298", "B")]
    [InlineData("OGN·250/298", "A")]
    [InlineData("OGN·250/298", "B")]
    [InlineData("UNL-072/219", "A")]
    [InlineData("UNL-072/219", "B")]
    public Task ExplicitFieldTargetSubmissionPreservesTheRequestedLocation(string card, string field) =>
        Run("explicit-target", card, false, false, true, field);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CompletedThunderingDropMoveNamesAnActualBattlefield(bool secondOccupied) =>
        Run(secondOccupied ? "movement-wide" : "movement-narrow", "OGN·250/298", false, false, secondOccupied);

    public static IEnumerable<object[]> IllegalCases()
    {
        foreach (var card in new[] { "OGN·268/298", "OGN·250/298", "UNL-072/219" })
        foreach (var invalid in new[] { "no-field", "unknown-field", "base-as-field", "extra-target",
            "duplicate-target", "wrong-player", "stale-prompt", "stale-tick" })
            yield return [card, invalid];
        yield return ["OGN·268/298", "unit-as-field"];
        yield return ["OGN·250/298", "enemy-base"];
        yield return ["OGN·250/298", "field-unit"];
        yield return ["UNL-072/219", "wrong-field"];
        yield return ["UNL-072/219", "base-unit"];
        yield return ["UNL-072/219", "friendly-unit"];
    }

    [Theory]
    [MemberData(nameof(IllegalCases))]
    public Task InvalidCanonicalSelectionsAndAuthorityCannotMutateStateOrPrivateViews(string card, string invalid) =>
        Run("invalid-" + invalid, card, true, true, true);

    private static async Task Run(string kind, string card, bool gate, bool annie, bool secondOccupied, string field = "A")
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string player, string zone, string? at = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
            locations[id] = new(player, zone, at);
        }
        Put("A", gate ? "OGN·296/298" : "OGN·278/298", "P2", "BATTLEFIELD", "A");
        Put("B", "OGN·278/298", "P2", "BATTLEFIELD", "B");
        cards["A"] = cards["A"] with { ControllerId = "P2" };
        cards["B"] = cards["B"] with { ControllerId = secondOccupied ? "P2" : null };
        Put("P2-A1", "OGN·142/298", "P2", "BATTLEFIELD", "A");
        Put("P2-A2", "OGN·142/298", "P2", "BATTLEFIELD", "A");
        Put("P2-B", "OGN·142/298", "P2", secondOccupied ? "BATTLEFIELD" : "BASE", secondOccupied ? "B" : null);
        Put("P1-BASE", "OGN·219/298", "P1", "BASE");
        Put("P2-BASE", "OGN·219/298", "P2", "BASE");
        Put("SPELL", card, "P1", "HAND");
        if (annie) Put("ANNIE", "OGS·001/024", "P1", "BASE");
        var caseId = (kind + "-" + card + "-" + gate + "-" + annie + "-" + field).Replace('·', '-').Replace('/', '-');
        var initial = new MatchState("SELECTED-AOE-" + caseId, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Base = annie ? ["P1-BASE", "ANNIE"] : ["P1-BASE"], Hand = ["SPELL"] },
                ["P2"] = PlayerZones.Empty with { Base = secondOccupied ? ["P2-BASE"] : ["P2-BASE", "P2-B"],
                    Battlefields = secondOccupied ? ["A", "B", "P2-A1", "P2-A2", "P2-B"] : ["A", "B", "P2-A1", "P2-A2"] }
            }, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) },
            seed: 424242) with { CardPlays = new(5, [], []) };
        var state = initial;
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var prompts = new List<object>();
        var submissions = new List<object>();
        var duplicates = new List<object>();
        var initialPrompt = session.PromptFor("P1");
        string? lastIntent = null;
        GameCommand? lastCommand = null;
        JsonElement lastRaw = default;
        string? failure = null;
        try
        {
            var unit = card == "OGN·250/298" ? "P1-BASE" : field == "A" ? "P2-A1" : "P2-B";
            string[] Targets(string battlefield, string target) => card switch
            {
                "OGN·250/298" => [target, battlefield], "UNL-072/219" => [battlefield, target], _ => [battlefield]
            };
            var targets = Targets(field, unit);
            if (kind.StartsWith("invalid-", StringComparison.Ordinal))
            {
                var invalid = kind[8..];
                targets = invalid switch
                {
                    "no-field" => card == "OGN·268/298" ? [] : [unit],
                    "unknown-field" => Targets("UNKNOWN-FIELD", unit),
                    "base-as-field" => Targets("P2-BASE", unit),
                    "unit-as-field" => Targets("P2-A1", unit),
                    "extra-target" => targets.Concat(["P2-BASE"]).ToArray(),
                    "duplicate-target" => targets.Concat([targets[0]]).ToArray(),
                    "enemy-base" => Targets(field, "P2-BASE"),
                    "field-unit" => Targets(field, "P2-A1"),
                    "wrong-field" => Targets("B", "P2-A1"),
                    "base-unit" => Targets(field, "P2-BASE"),
                    "friendly-unit" => Targets(field, "P1-BASE"),
                    _ => targets
                };
                await Act(invalid == "wrong-player" ? "P2" : "P1", new PlayCardCommand("SPELL", card, targets,
                    OptionalCosts: card == "OGN·268/298" ? ["SPEND_POWER:3"] : null), false, invalid);
                return;
            }
            await Act("P1", new PlayCardCommand("SPELL", card, targets,
                OptionalCosts: card == "OGN·268/298" ? ["SPEND_POWER:3"] : null));
            await RepeatAcceptedSubmission();
            Assert.NotNull(state.PriorityPlayerId);
            await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            if (kind.StartsWith("movement-", StringComparison.Ordinal))
            {
                Assert.Contains("P1-BASE", state.PlayerZones["P1"].Battlefields);
                Assert.Equal("BATTLEFIELD", state.ObjectLocations["P1-BASE"].Zone);
                Assert.NotNull(state.ObjectLocations["P1-BASE"].BattlefieldObjectId);
                Assert.Equal(field, state.ObjectLocations["P1-BASE"].BattlefieldObjectId);
                AssertCanonicalPrompt();
                return;
            }
            var damage = journal.Entries.SelectMany(e => e.Events).Where(e => e.Kind == "DAMAGE_APPLIED"
                && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "SPELL"))
                .ToDictionary(e => (string)e.Payload["targetObjectId"]!, e => (int)e.Payload["damage"]!);
            Assert.DoesNotContain("P1-BASE", damage.Keys);
            Assert.DoesNotContain("P2-BASE", damage.Keys);
            Assert.DoesNotContain("ANNIE", damage.Keys);
            if (field == "A")
            {
                Assert.DoesNotContain("P2-B", damage.Keys);
                Assert.Equal(new[] { "P2-A1", "P2-A2" }, damage.Keys.Order());
                var main = (card == "OGN·268/298" ? 3 : 4) + (annie ? 1 : 0) + (gate ? 1 : 0);
                var other = (card == "UNL-072/219" ? 1 : card == "OGN·268/298" ? 3 : 4) + (annie ? 1 : 0) + (gate ? 1 : 0);
                Assert.Equal(main, damage["P2-A1"]);
                Assert.Equal(other, damage["P2-A2"]);
            }
            else
            {
                Assert.Equal(new[] { "P2-B" }, damage.Keys.Order());
                Assert.Equal(card == "OGN·268/298" ? 3 : 4, damage["P2-B"]);
            }
            if (kind == "explicit-target" && card == "OGN·250/298")
                Assert.Equal(field, state.ObjectLocations["P1-BASE"].BattlefieldObjectId);
            AssertCanonicalPrompt();
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
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_SELECTED_AOE_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
                using var output = new GZipStream(File.Open(Path.Combine(folder, caseId + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
                JsonSerializer.Serialize(output, new { caseId, kind, card, gate, annie, secondOccupied, field, initial, states,
                    prompts, submissions, duplicates, entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length, failure }, json);
            }
        }

        async Task Act(string player, GameCommand command, bool expectedAccepted = true, string? invalid = null)
        {
            var beforeState = MatchStateHasher.Hash(state);
            var beforePrompts = new[] { session.PromptFor("P1"), session.PromptFor("P2") };
            var beforeViews = new[] { session.SnapshotFor("P1"), session.SnapshotFor("P2") };
            prompts.Add(new { player, beforePrompts, beforeViews });
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            if (invalid == "stale-prompt") raw["promptId"] = "STALE-PROMPT";
            if (invalid == "stale-tick") raw["snapshotTick"] = state.Tick - 1;
            lastIntent = "selected-aoe-" + journal.Entries.Count;
            lastCommand = command;
            lastRaw = JsonSerializer.SerializeToElement(raw, json);
            var result = await session.SubmitAsync(player, lastIntent, command, lastRaw, default);
            state = result.State;
            states.Add(state);
            submissions.Add(new { result.Accepted, result.ErrorCode, result.ErrorMessage, beforeState,
                afterState = MatchStateHasher.Hash(state) });
            if (!result.Accepted)
            {
                Assert.Empty(result.Events);
                Assert.Equal(beforeState, MatchStateHasher.Hash(state));
                Assert.Equal(MatchStateHasher.HashValue(beforePrompts), MatchStateHasher.HashValue(new[] { session.PromptFor("P1"), session.PromptFor("P2") }));
                Assert.Equal(MatchStateHasher.HashValue(beforeViews), MatchStateHasher.HashValue(new[] { session.SnapshotFor("P1"), session.SnapshotFor("P2") }));
            }
            Assert.True(result.Accepted == expectedAccepted, result.ErrorMessage ?? "Unexpected accepted invalid command");
        }

        async Task RepeatAcceptedSubmission()
        {
            var hash = MatchStateHasher.Hash(state);
            var promptHash = MatchStateHasher.HashValue(new[] { session.PromptFor("P1"), session.PromptFor("P2") });
            var viewHash = MatchStateHasher.HashValue(new[] { session.SnapshotFor("P1"), session.SnapshotFor("P2") });
            var count = journal.Entries.Count;
            Assert.NotNull(lastIntent);
            Assert.NotNull(lastCommand);
            var result = await session.SubmitAsync("P1", lastIntent, lastCommand, lastRaw, default);
            duplicates.Add(new { clientIntentId = lastIntent, rawCommand = lastRaw, result.Accepted, result.ErrorCode,
                journalBefore = count, journalAfter = journal.Entries.Count, beforeState = hash,
                afterState = MatchStateHasher.Hash(result.State) });
            Assert.True(result.Accepted, result.ErrorMessage);
            Assert.Equal(hash, MatchStateHasher.Hash(result.State));
            Assert.Equal(count, journal.Entries.Count);
            Assert.Equal(promptHash, MatchStateHasher.HashValue(new[] { session.PromptFor("P1"), session.PromptFor("P2") }));
            Assert.Equal(viewHash, MatchStateHasher.HashValue(new[] { session.SnapshotFor("P1"), session.SnapshotFor("P2") }));
        }

        void AssertCanonicalPrompt()
        {
            var prompt = JsonSerializer.SerializeToElement(initialPrompt, json);
            var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PLAY_CARD");
            var requirement = candidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray()
                .Single(r => r.GetProperty("sourceObjectId").GetString() == "SPELL");
            var count = card == "OGN·268/298" ? 1 : 2;
            Assert.Equal(count, requirement.GetProperty("minTargetCount").GetInt32());
            Assert.Equal(count, requirement.GetProperty("maxTargetCount").GetInt32());
            var index = card == "OGN·250/298" ? "1" : "0";
            var fields = requirement.GetProperty("targetChoicesByIndex").GetProperty(index).EnumerateArray()
                .Select(choice => choice.GetProperty("id").GetString()).Order().ToArray();
            Assert.Contains(field, fields);
            Assert.All(fields, id => Assert.Contains(id, new[] { "A", "B" }));
            if (card != "UNL-072/219" || secondOccupied)
                Assert.Equal(new[] { "A", "B" }, fields);
            if (card != "OGN·268/298")
            {
                var unitIndex = card == "OGN·250/298" ? "0" : "1";
                var units = requirement.GetProperty("targetChoicesByIndex").GetProperty(unitIndex).EnumerateArray()
                    .Select(choice => choice.GetProperty("id").GetString()).Order().ToArray();
                string[] expectedUnits = card == "OGN·250/298"
                    ? annie ? ["ANNIE", "P1-BASE"] : ["P1-BASE"]
                    : secondOccupied ? ["P2-A1", "P2-A2", "P2-B"] : ["P2-A1", "P2-A2"];
                Assert.Equal(expectedUnits, units);
            }
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
