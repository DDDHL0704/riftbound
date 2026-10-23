using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RengarAttackingBattlefieldPermissionTests
{
    [Fact]
    public Task ReactionAttackerAfterTheResultLosesItsRoleWhenNoFurtherBattleIsDue()
        => BattleWinLateParticipantTests.Run("UNL-114/219", lateDefender: false, lateAttacker: true);

    public static IEnumerable<object[]> Permissions()
    {
        foreach (var no in new[] { "SFD·025/221", "SFD·025a/221·P" })
        foreach (var scenario in new[] { "battle-focus", "result", "result-source-returned", "other-field", "controlled-defender" })
            yield return [no, scenario];
    }

    [Theory]
    [MemberData(nameof(Permissions))]
    public async Task PrintedAttackPermissionUsesThePlayersCurrentBattleRoleAndPreciseField(string cardNo, string scenario)
    {
        var actor = scenario == "controlled-defender" ? "P2" : "P1";
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string player, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
            locations[id] = new(player, zone, field);
        }
        Put("SOURCE", "UNL-114/219", "P1", "BASE");
        Put("DEFENDER", "OGN·175/298", "P2", "BATTLEFIELD", "FIELD");
        Put("FIELD", "OGN·296/298", "P2", "BATTLEFIELD", "FIELD");
        Put("OTHER", "OGN·296/298", "P2", "BATTLEFIELD", "OTHER");
        Put("GUARD", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = "P2" };
        cards["OTHER"] = cards["OTHER"] with { ControllerId = "P2" };
        Put("RENGAR", cardNo, actor, "HAND");
        Put("RETURN", "OGN·104/298", "P1", "HAND");
        Put("DRAW", "OGN·219/298", "P1", "MAIN_DECK");
        var caseId = (cardNo + "-" + scenario).Replace('·', '-').Replace('/', '-');
        var initial = new MatchState("RENGAR-PERMISSION-" + caseId, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Base = ["SOURCE"], Hand = actor == "P1" ? ["RETURN", "RENGAR"] : ["RETURN"], MainDeck = ["DRAW"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD", "DEFENDER", "OTHER", "GUARD"], Hand = actor == "P2" ? ["RENGAR"] : [] }
            }, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) },
            seed: 424242) with { CardPlays = new(5, [], []) };
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var probes = new List<object>();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var state = initial;
        string? failure = null;
        try
        {
            await Act("P1", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:FIELD", []));
            if (scenario != "battle-focus")
            {
                for (var i = 0; i < 4 && state.FocusPlayerId is not null; i++) await Act(state.FocusPlayerId, new PassFocusCommand());
                await Act("P1", new DeclareBattleCommand("FIELD", ["SOURCE"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
                Assert.Equal("P1", state.PendingBattleResult!.AttackingPlayerId);
                Assert.Single(state.StackItems);
            }
            if (scenario == "result-source-returned")
            {
                await Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"]));
                var top = state.StackItems[^1].StackItemId;
                for (var i = 0; state.StackItems.Any(s => s.StackItemId == top); i++)
                {
                    Assert.InRange(i, 0, 3);
                    await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                }
                Assert.Contains("SOURCE", state.PlayerZones["P1"].Hand);
                Assert.DoesNotContain(state.CardObjects.Values, c => c.ControllerId == "P1" && c.IsAttacking);
                Assert.Equal("P1", state.PendingBattleResult!.AttackingPlayerId);
            }
            if (actor == "P2" && state.PriorityPlayerId == "P1") await Act("P1", new PassPriorityCommand());
            var prompt = session.PromptFor(actor);
            probes.Add(new { kind = "before-rengar-play", prompt, state.BattleState, state.PendingBattleResult });
            var destination = scenario == "other-field" ? "BATTLEFIELD:OTHER" : "BATTLEFIELD:FIELD";
            await Act(actor, new PlayCardCommand("RENGAR", cardNo, [], Destination: destination), accepted: scenario != "other-field");
            if (scenario == "other-field")
            {
                Assert.Contains("RENGAR", state.PlayerZones["P1"].Hand);
                Assert.DoesNotContain(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "UNIT_PLAYED_TO_BATTLEFIELD");
            }
            else
            {
                Assert.Contains(prompt.Candidates ?? [], c => c.Action == CommandTypes.PlayCard
                    && (c.Sources ?? []).Any(s => s.Id == "RENGAR")
                    && (c.Destinations ?? []).Any(d => d.Id == "BATTLEFIELD:FIELD"));
                Assert.Equal("FIELD", state.ObjectLocations["RENGAR"].BattlefieldObjectId);
                Assert.Equal(actor == "P1", state.CardObjects["RENGAR"].IsAttacking);
                Assert.Equal(actor == "P2", state.CardObjects["RENGAR"].IsDefending);
                Assert.Single(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "UNIT_PLAYED_TO_BATTLEFIELD");
            }
        }
        catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; throw; }
        finally
        {
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId,
                e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence,
                e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
                new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            Assert.Equal(commands.Length, states.Count);
            for (var i = 0; i < commands.Length; i++)
            {
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands.Take(i + 1).ToArray(), states[i],
                    new CoreRuleEngine(), default, events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
                Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            }
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_RENGAR_PERMISSION_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
                using var output = new GZipStream(File.Open(Path.Combine(folder, caseId + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
                JsonSerializer.Serialize(output, new { caseId, cardNo, scenario, initial, states, probes,
                    entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length, failure }, json);
            }
        }

        async Task Act(string player, GameCommand command, bool accepted = true)
        {
            var before = JsonSerializer.Serialize(state, json);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            var result = await session.SubmitAsync(player, "rengar-permission-" + journal.Entries.Count,
                command, JsonSerializer.SerializeToElement(raw, json), default);
            state = result.State;
            states.Add(state);
            Assert.True(result.Accepted == accepted, result.ErrorMessage ?? "Unexpected accepted command");
            if (!accepted)
            {
                Assert.Equal(before, JsonSerializer.Serialize(state, json));
                probes.Add(new { kind = "rejected-destination", stateUnchanged = true, result.ErrorMessage });
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
