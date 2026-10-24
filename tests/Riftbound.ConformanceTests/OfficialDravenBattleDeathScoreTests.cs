using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialDravenBattleDeathScoreTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var no in new[] { "SFD·148/221", "SFD·148a/221" })
        foreach (var scenario in new[] { "attacker-death", "defender-death", "mutual-death", "spell-battle-death",
            "spell-outside-battle", "zhonya-replacement", "defender-winning-point" }) yield return [no, scenario];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public Task ActualDeathDuringBattleScoresForAnOpponentBeforeControlButReplacedDeathDoesNot(string cardNo, string scenario)
        => Run(cardNo, scenario);

    [Theory]
    [InlineData("SFD·148/221", false)]
    [InlineData("SFD·148/221", true)]
    [InlineData("SFD·148a/221", false)]
    [InlineData("SFD·148a/221", true)]
    public Task ReactionCardsRemainAvailableDuringTheInitialBattleDuelBeforeDamage(string cardNo, bool castResponses)
        => Run(cardNo, castResponses ? "response-damage-death" : "response-pass-death");

    [Theory]
    [InlineData("SFD·148/221")]
    [InlineData("SFD·148a/221")]
    public Task VoidGateErratumAddsOneToUntargetedReactionDamageBeforeDeath(string cardNo)
        => Run(cardNo, "response-void-gate-damage");

    private static async Task Run(string cardNo, string scenario)
    {
        // Current CN 429.1.a.1.b / 808.1.d.1 explicitly use Draven. The official
        // SFD judge FAQ p25 also includes Dark Blade during the battle duel.
        var defending = scenario is "defender-death" or "defender-winning-point";
        var outside = scenario == "spell-outside-battle";
        var replacement = scenario == "zhonya-replacement";
        var spell = outside || scenario == "spell-battle-death";
        var winning = scenario == "defender-winning-point";
        var responseWindow = scenario.StartsWith("response-", StringComparison.Ordinal);
        var responseDamage = scenario is "response-damage-death" or "response-void-gate-damage";
        var voidGateDamage = scenario == "response-void-gate-damage";
        var sourcePlayer = defending ? "P2" : "P1";
        var recipient = defending ? "P1" : "P2";
        var attacker = defending ? "ATTACKER" : "SOURCE";
        var defender = defending ? "SOURCE" : "DEFENDER";
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string player, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
            locations[id] = new(player, zone, field);
        }
        Put("SOURCE", cardNo, sourcePlayer, defending || outside ? "BATTLEFIELD" : "BASE", defending || outside ? "FIELD" : null);
        Put("FIELD", scenario == "response-damage-death" ? "OGN·278/298" : "OGN·296/298", outside ? "P1" : "P2", "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = outside ? "P1" : "P2" };
        if (defending) Put("ATTACKER", "OGN·142/298", "P1", "BASE");
        else if (!outside) Put("DEFENDER", scenario == "mutual-death" ? "OGN·125/298" : "OGN·142/298", "P2", "BATTLEFIELD", "FIELD");
        Put("DARK-BLADE", "OGN·213/298", "P2", "HAND");
        Put("HOURGLASS", "OGN·077/298", "P1", "HAND");
        if (responseWindow)
        {
            Put("BARRAGE1", "OGN·127/298", "P2", "HAND");
            Put("BARRAGE2", "OGN·127/298", "P2", "HAND");
            if (scenario == "response-damage-death") Put("BARRAGE3", "OGN·127/298", "P2", "HAND");
        }
        var zones = new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Base = outside ? [] : [attacker], Battlefields = outside ? ["FIELD", "SOURCE"] : [], Hand = ["HOURGLASS"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = outside ? [] : ["FIELD", defender],
                Hand = scenario == "response-damage-death" ? ["DARK-BLADE", "BARRAGE1", "BARRAGE2", "BARRAGE3"]
                    : responseWindow ? ["DARK-BLADE", "BARRAGE1", "BARRAGE2"] : ["DARK-BLADE"] }
        };
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 8).Select(i => "DRAW-" + player + "-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = zones[player] with { MainDeck = deck };
        }
        var caseId = (cardNo + "-" + scenario).Replace('·', '-').Replace('/', '-');
        var turnPlayer = outside ? "P2" : "P1";
        var initial = new MatchState("DRAVEN-DEATH-" + caseId, 0, 5, turnPlayer,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: turnPlayer,
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = winning ? 7 : 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) },
            seed: 424242) with { CardPlays = new(5, [], []) };
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var state = initial;
        string? failure = null;
        try
        {
            if (replacement)
            {
                await Act("P1", new PlayCardCommand("HOURGLASS", "OGN·077/298", []));
                await Settle();
                Assert.Contains("HOURGLASS", state.PlayerZones["P1"].Base);
            }
            if (!outside)
            {
                await Act("P1", new MoveUnitCommand(attacker, "BASE", "BATTLEFIELD:FIELD", []));
                if (spell || responseDamage)
                {
                    if (state.FocusPlayerId == "P1") await Act("P1", new PassFocusCommand());
                    Assert.Equal("P2", state.FocusPlayerId);
                    Assert.True(state.CardObjects["SOURCE"].IsAttacking);
                }
                else
                {
                    if (responseWindow)
                    {
                        Assert.Equal(TimingStates.SpellDuelOpen, state.TimingState);
                        Assert.Contains("BARRAGE1", state.PlayerZones["P2"].Hand);
                        Assert.Contains("BARRAGE2", state.PlayerZones["P2"].Hand);
                    }
                    for (var i = 0; i < 4 && state.FocusPlayerId is not null; i++) await Act(state.FocusPlayerId, new PassFocusCommand());
                    await Act("P1", new DeclareBattleCommand("FIELD", [attacker], [defender], ["COMBAT_ASSIGNMENT"]));
                }
            }
            if (responseDamage)
            {
                Assert.Null(state.PendingBattleResult);
                Assert.Contains("SOURCE", state.PlayerZones["P1"].Battlefields);
                await Act("P2", new PlayCardCommand("BARRAGE1", "OGN·127/298", []));
                Assert.NotNull(state.PriorityPlayerId);
                if (state.PriorityPlayerId == "P1") await Act("P1", new PassPriorityCommand());
                Assert.Equal("P2", state.PriorityPlayerId);
                await Act("P2", new PlayCardCommand("BARRAGE2", "OGN·127/298", []));
                if (!voidGateDamage)
                {
                    if (state.PriorityPlayerId == "P1") await Act("P1", new PassPriorityCommand());
                    await Act("P2", new PlayCardCommand("BARRAGE3", "OGN·127/298", []));
                }
                Assert.Equal(voidGateDamage ? 2 : 3, state.StackItems.Count);
                var top = state.StackItems[^1].StackItemId;
                for (var i = 0; state.StackItems.Any(s => s.StackItemId == top); i++)
                {
                    Assert.InRange(i, 0, 3);
                    await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                }
                Assert.Equal(voidGateDamage ? 1 : 2, state.StackItems.Count);
                Assert.Contains("SOURCE", state.PlayerZones["P1"].Battlefields);
                Assert.Equal(voidGateDamage ? 3 : 2, state.CardObjects["SOURCE"].Damage);
                if (voidGateDamage) return; // Independent damage prerequisite, not death-score coverage.
            }
            if (spell) await Act("P2", new PlayCardCommand("DARK-BLADE", "OGN·213/298", ["SOURCE"]));
            await Settle();
            var events = journal.Entries.SelectMany(e => e.Events).ToList();
            if (scenario == "response-damage-death")
            {
                Assert.Contains("SOURCE", state.PlayerZones["P1"].Graveyard);
                Assert.DoesNotContain(journal.Entries, e => e.CommandType == CommandTypes.DeclareBattle);
                Assert.Equal(3, events.Count(e => e.Kind == "DAMAGE_APPLIED"
                    && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "SOURCE")));
            }
            var abilityScores = events.Where(e => e.Kind == "SCORE_GAINED"
                && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "SOURCE")).ToArray();
            var expected = outside || replacement ? 0 : 1;
            Assert.Equal(expected, abilityScores.Length);
            Assert.Equal(initial.PlayerScores[recipient] + expected + (defending && !winning ? 1 : 0), state.PlayerScores[recipient]);
            Assert.Equal(winning ? recipient : null, state.WinnerPlayerId);
            Assert.DoesNotContain(events, e => e.Kind == "BURNOUT_APPLIED");
            if (replacement)
            {
                Assert.Contains("SOURCE", state.PlayerZones["P1"].Base);
                Assert.Contains("HOURGLASS", state.PlayerZones["P1"].Graveyard);
                Assert.DoesNotContain(events, e => e.Kind == "UNIT_DESTROYED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "SOURCE"));
                Assert.Contains(events, e => e.Kind == "UNIT_RECALLED_TO_BASE");
            }
            else
            {
                Assert.Contains("SOURCE", state.PlayerZones[sourcePlayer].Graveyard);
                Assert.Single(events, e => e.Kind == "UNIT_DESTROYED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "SOURCE"));
            }
            if (expected == 1)
            {
                Assert.Equal(recipient, abilityScores[0].Payload["playerId"]);
                Assert.Equal(1, abilityScores[0].Payload["amount"]);
                if (defending && !winning)
                    Assert.True(events.IndexOf(abilityScores[0]) < events.FindIndex(e => e.Kind == "BATTLEFIELD_CONQUERED"));
            }
            if (winning)
            {
                Assert.True(events.IndexOf(Assert.Single(abilityScores)) < events.FindIndex(e => e.Kind == "MATCH_WON"));
                Assert.DoesNotContain(events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            }
            if (outside) Assert.DoesNotContain(events, e => e.Kind is "BATTLE_RESULT_DETERMINED" or "BATTLE_CLOSED");
        }
        catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; throw; }
        finally
        {
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId,
                e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence,
                e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
                new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            Assert.Equal(commands.Length, states.Count);
            for (var i = 0; i < commands.Length; i++)
            {
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands.Take(i + 1).ToArray(), states[i],
                    new CoreRuleEngine(), default, events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
                Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            }
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_DRAVEN_DEATH_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
                using var output = new GZipStream(File.Open(Path.Combine(folder, caseId + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
                JsonSerializer.Serialize(output, new { caseId, cardNo, scenario, initial, states,
                    entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length, failure }, json);
            }
        }

        async Task Settle()
        {
            for (var i = 0; state.Status == MatchStatuses.InProgress && (state.StackItems.Count > 0 || state.PendingCardChoice is not null
                || state.PriorityPlayerId is not null && state.TimingState == TimingStates.NeutralClosed); i++)
            {
                Assert.InRange(i, 0, 15);
                Assert.Null(state.PendingPayment);
                Assert.Null(state.PendingHandChoice);
                if (state.PendingCardChoice is { } choice)
                {
                    Assert.Contains(recipient, choice.LegalObjectIds);
                    await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [recipient]));
                }
                else
                {
                    Assert.NotNull(state.PriorityPlayerId);
                    await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                }
            }
        }
        async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            var result = await session.SubmitAsync(player, "draven-death-" + journal.Entries.Count,
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
