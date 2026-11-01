using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Common = Riftbound.ConformanceTests.Ven129PhysicalCombatConsumerTests;

namespace Riftbound.ConformanceTests;

public sealed class Ven159PhysicalBulwarkCombatConsumerTests
{
    internal const string Temple = "VEN·159", WallNo = "VEN·118", GuardNo = "VEN·129";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task RealPaidUnitsFromBothPlayersUseSevenMightAndLethalAndRejectSixBeforeTheOtherTarget(string defender)
    {
        var initial = Position(); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await PayPair(game, "P1"); await game.Act("P1", new EndTurnCommand()); await PayPair(game, "P2");
            await game.Act("P2", new EndTurnCommand());
            if (defender == "P2") await game.Act("P1", new EndTurnCommand());
            await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            var attacker = Other(defender); Assert.False(game.State.CardObjects[Wall(defender)].IsExhausted);
            await game.Act(defender, new MoveUnitCommand(Wall(defender), "BASE", "BATTLEFIELD:BF", [], SourceObjectIds: [Wall(defender), Guard(defender)]));
            AssertAtTemple(game.State, "BF", defender); Assert.Equal(6, Might(game.State, Wall(attacker)));
            await Common.CompleteMovementWindow(() => game.State, game.Act); await game.Act(defender, new EndTurnCommand());
            await game.Act(attacker, new MoveUnitCommand(Wall(attacker), "BASE", "BATTLEFIELD:BF", [], SourceObjectIds: [Wall(attacker), Guard(attacker)]));
            AssertAtTemple(game.State, "BF", "P1", "P2");
            foreach (var player in new[] { "P1", "P2" })
            {
                Assert.Equal(player == attacker, game.State.CardObjects[Wall(player)].IsAttacking);
                Assert.Equal(player == defender, game.State.CardObjects[Wall(player)].IsDefending);
            }
            var walls = new Dictionary<string, string> { ["P1"] = Wall("P1"), ["P2"] = Wall("P2") };
            var guards = new Dictionary<string, string> { ["P1"] = Guard("P1"), ["P2"] = Guard("P2") };
            var generations = walls.Values.Concat(guards.Values).ToDictionary(id => id, id => game.State.CardObjects[id].ObjectGeneration);
            Assert.All(generations.Values, value => Assert.Equal(1, value));
            var prefix = game.Events().Length;
            await FinishBattle(() => game.State, game.Act, game.Events, walls, guards,
                (player, command, intent) => Ven001PhysicalThresholdConsumerTests.Reject(game, player, command, intent));
            AssertBattleOutcome(game.State, game.Events().Skip(prefix).ToArray(), "BF", walls, guards, generations);
            Common.AssertTwelveEach(game.State); await game.Replay();
        }
        finally { await Save("actual-two-sides-bulwark-" + defender, initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task ARealPaidBulwarkGetsOneOnlyHereAndLosesItOnAnOrdinarySameGenerationMoveToBase(string player)
    {
        var initial = Position(); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            if (player == "P2") await game.Act("P1", new EndTurnCommand());
            await PayPair(game, player); await game.Act(player, new EndTurnCommand()); await game.Act(Other(player), new EndTurnCommand());
            await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            var id = Wall(player); var generation = game.State.CardObjects[id].ObjectGeneration;
            Assert.Equal(6, Might(game.State, id)); await game.Act(player, new MoveUnitCommand(id, "BASE", "BATTLEFIELD:BF", []));
            AssertAtTemple(game.State, "BF", player); Assert.Equal(6, Might(game.State, Guard(player)));
            await Common.CompleteMovementWindow(() => game.State, game.Act);
            await game.Act(player, new EndTurnCommand()); await game.Act(Other(player), new EndTurnCommand());
            Assert.Equal(7, Might(game.State, id)); Assert.False(game.State.CardObjects[id].IsExhausted);
            var move = Assert.Single(ResolutionResult.BuildPrompts(game.State)[player].Candidates!, c => c.Action == CommandTypes.MoveUnit && c.Enabled);
            var metadata = JsonSerializer.SerializeToElement(move.Metadata, Json);
            var requirement = Assert.Single(metadata.GetProperty("sourceRequirements").EnumerateArray(), r =>
                r.GetProperty("sourceObjectId").GetString() == id && r.GetProperty("mode").GetString() == "BATTLEFIELD_TO_BASE");
            Assert.Equal("BATTLEFIELD", requirement.GetProperty("origin").GetString());
            Assert.Contains(requirement.GetProperty("destinationChoices").EnumerateArray(), d => d.GetProperty("id").GetString() == "BASE");
            // Keep the original malformed precise-origin command as a state-invariant rejection.
            await Ven001PhysicalThresholdConsumerTests.Reject(game, player, new MoveUnitCommand(id, "BATTLEFIELD:BF", "BASE", []), "ven159-unsupported-precise-return-" + player);
            Assert.Equal(7, Might(game.State, id)); Assert.Equal(generation, game.State.CardObjects[id].ObjectGeneration);
            await game.Act(player, new MoveUnitCommand(id, "BATTLEFIELD", "BASE", []));
            Assert.Equal("BASE", game.State.ObjectLocations[id].Zone); Assert.Equal(6, Might(game.State, id));
            Assert.Equal(6, game.State.CardObjects[id].Power); Assert.Equal(generation, game.State.CardObjects[id].ObjectGeneration);
            Assert.Contains(CardCombatKeywordNames.Bulwark, game.State.CardObjects[id].Tags);
            Assert.DoesNotContain(game.State.ContinuousEffects, e => e.SourceObjectId == "BF" && e.TargetObjectId == id);
            Common.AssertTwelveEach(game.State); await game.Replay();
        }
        finally { await Save("actual-paid-aura-leaves-" + player, initial, game); }
    }

    internal static async Task FinishBattle(Func<MatchState> state, Func<string, GameCommand, Task> act, Func<RecoveredEvent[]> events,
        IReadOnlyDictionary<string, string> walls, IReadOnlyDictionary<string, string> guards, Func<string, GameCommand, string, Task>? reject = null)
    {
        var prefix = events().Length; var assigned = new HashSet<string>();
        for (var step = 0; step < 40 && !events().Skip(prefix).Any(e => e.Event.Kind == "BATTLE_CLOSED"); step++)
        {
            var current = state(); var prompts = ResolutionResult.BuildPrompts(current);
            var prompt = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            if (prompt is not null)
            {
                var player = prompt.PlayerId; var other = Other(player); var meta = JsonSerializer.SerializeToElement(prompt.View!.Metadata, Json);
                var pool = meta.GetProperty("assignableDamagePool"); var lethal = meta.GetProperty("lethalDamageThreshold");
                Assert.Equal(7, pool.GetProperty(walls[player]).GetInt32()); Assert.Equal(6, pool.GetProperty(guards[player]).GetInt32());
                Assert.Equal(7, lethal.GetProperty(walls[other]).GetInt32()); Assert.Equal(6, lethal.GetProperty(guards[other]).GetInt32());
                if (reject is not null)
                {
                    var illegal = new AssignCombatDamageCommand(current.BattleState.BattleId!, current.BattleState.BattlefieldObjectId!,
                        [new(walls[player], walls[other], 6), new(walls[player], guards[other], 1), new(guards[player], guards[other], 6)]);
                    await reject(player, illegal, "ven159-six-is-not-lethal-" + player);
                    await reject(other, illegal, "ven159-wrong-assignment-player-" + player);
                }
                Assert.True(assigned.Add(player));
                await act(player, new AssignCombatDamageCommand(current.BattleState.BattleId!, current.BattleState.BattlefieldObjectId!,
                    [new(walls[player], walls[other], 7), new(guards[player], guards[other], 6)]));
            }
            else if (current.PriorityPlayerId is { } priority) await act(priority, new PassPriorityCommand());
            else if (current.FocusPlayerId is { } focus) await act(focus, new PassFocusCommand());
            else
            {
                var player = Assert.Single(prompts.Values, p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
                await act(player, new DeclareBattleCommand(current.BattleState.BattlefieldObjectId!, current.BattleState.AttackerObjectIds, current.BattleState.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
        }
        Assert.Equal(new[] { "P1", "P2" }, assigned.Order()); Assert.Contains(events().Skip(prefix), e => e.Event.Kind == "BATTLE_CLOSED");
    }
    internal static void AssertBattleOutcome(MatchState state, RecoveredEvent[] events, string field, IReadOnlyDictionary<string, string> walls, IReadOnlyDictionary<string, string> guards,
        IReadOnlyDictionary<string, long> generations)
    {
        Assert.Single(events, e => e.Event.Kind == "BATTLE_CLOSED"); Assert.Single(events, e => e.Event.Kind == "BATTLE_NO_RESULT");
        foreach (var player in new[] { "P1", "P2" }) foreach (var id in new[] { walls[player], guards[player] })
        {
            var wall = id == walls[player]; var damage = events.Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), id) && e.Event.Payload.ContainsKey("combatRole")).ToArray();
            Assert.Equal(wall ? 7 : 6, damage.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
            Assert.All(damage, e => { Assert.Equal(6, e.Event.Payload["basePower"]); Assert.Equal(wall ? 7 : 6, e.Event.Payload["combatPower"]); Assert.Equal(0, e.Event.Payload["keywordBonus"]); Assert.Equal(wall ? 1 : 0, Convert.ToInt32(e.Event.Payload.GetValueOrDefault("staticPowerBonus", 0))); });
            Assert.Contains(id, state.PlayerZones[player].Graveyard); Assert.Equal("GRAVEYARD", state.ObjectLocations[id].Zone);
            Assert.Equal(6, state.CardObjects[id].Power); Assert.Equal(generations[id] + 1, state.CardObjects[id].ObjectGeneration);
            Assert.False(state.CardObjects[id].IsAttacking); Assert.False(state.CardObjects[id].IsDefending);
        }
        Assert.DoesNotContain(state.ContinuousEffects, e => e.SourceObjectId == field);
    }
    internal static void AssertAtTemple(MatchState state, string field, params string[] players)
    {
        Assert.Equal(Temple, state.CardObjects[field].CardNo);
        foreach (var player in players)
        {
            var wall = Wall(player); Assert.Equal(6, state.CardObjects[wall].Power); Assert.Equal(7, Might(state, wall));
            Assert.Contains(CardCombatKeywordNames.Bulwark, state.CardObjects[wall].Tags);
            Assert.Equal(6, state.CardObjects[Guard(player)].Power); Assert.Equal(6, Might(state, Guard(player)));
        }
    }
    private static async Task PayPair(Driver game, string player)
    {
        Assert.Equal(player, game.State.TurnPlayerId); Assert.Equal(0, game.State.RunePools[player].Mana); Assert.Equal(0, game.State.RunePools[player].TotalPower);
        await game.TapRunes(player, Enumerable.Range(0, 10).Select(i => Rune(player, i)));
        await game.Act(player, new RecycleRuneCommand(Rune(player, 0))); var prefix = game.Events().Length;
        await game.Act(player, new PlayCardCommand(Wall(player), WallNo, []));
        var first = Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "COST_PAID"); Assert.Equal(6, first.Event.Payload["totalManaCost"]); Assert.Equal(0, first.Event.Payload["totalPowerCost"]);
        Assert.Equal(4, game.State.RunePools[player].Mana); Assert.Equal(6, Might(game.State, Wall(player))); prefix = game.Events().Length;
        await game.Act(player, new PlayCardCommand(Guard(player), GuardNo, []));
        var second = Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "COST_PAID"); Assert.Equal(4, second.Event.Payload["totalManaCost"]); Assert.Equal(1, second.Event.Payload["totalPowerCost"]);
        Assert.Equal(0, game.State.RunePools[player].Mana); Assert.Equal(0, game.State.RunePools[player].TotalPower); Common.AssertTwelveEach(game.State);
    }
    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(GuardNo); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", Temple, "P1") with { ControllerId = null };
        cards["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", GuardNo, "P2"); locations["ENEMY"] = new("P2", "HAND");
        zones["P2"] = zones["P2"] with { Base = [], Hand = ["ENEMY"] };
        foreach (var player in new[] { "P1", "P2" })
        {
            var wall = Wall(player); cards[wall] = OfficialGarenConquestBattlefieldCountTests.Printed(wall, WallNo, player); locations[wall] = new(player, "HAND"); zones[player] = zones[player] with { Hand = [..zones[player].Hand, wall] };
            for (var i = 0; i < 12; i++)
            {
                var id = Rune(player, i); cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", player); locations[id] = new(player, "BASE"); zones[player] = zones[player] with { Base = [..zones[player].Base, id] };
            }
        }
        return state with { RoomId = "VEN159-REAL-PAID-BULWARK-BATTLE", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }
    internal static string Other(string player) => player == "P1" ? "P2" : "P1";
    internal static string Wall(string player) => player + "-WALL";
    internal static string Guard(string player) => player == "P1" ? "UNIT" : "ENEMY";
    private static string Rune(string player, int i) => "TEMPLE-RUNE-" + player + "-" + i.ToString("D2");
    internal static int Might(MatchState state, string id) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[state.CardObjects[id].ControllerId!], Json)
        .GetProperty("players").GetProperty(state.CardObjects[id].ControllerId!).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN159_PAID_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Declared turn-five material, normal printed temple and hand units, zero pools/exactly12 physical yellow runes per player. Actual6mana Bulwark unit and4mana+yellow nonBulwark, real readiness/movement/control/duels/assignments,7Might/lethal/damage and six-short priority rejection, aura loss by real move. No forced aura/keyword/role/profile/result. Per-command current privacy/recovery/dedup/original history. Submitted opening separate; latest completeL2/L3/applicability/wholecard OPEN.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
