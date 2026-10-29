using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Common = Riftbound.ConformanceTests.Ven129PhysicalCombatConsumerTests;

namespace Riftbound.ConformanceTests;

public sealed class Ven117PaidConditionalSteadfastConsumerTests
{
    internal const string No = "VEN·117", FriendNo = "VEN·124", EnemyNo = "VEN·118";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("P1", 0, false, 1)] [InlineData("P1", 1, false, 4)] [InlineData("P1", 2, false, 1)]
    [InlineData("P2", 0, false, 1)] [InlineData("P2", 1, false, 4)] [InlineData("P2", 2, false, 1)]
    [InlineData("P1", 1, true, 1)] [InlineData("P2", 1, true, 1)]
    public async Task NormalTwoManaPlayAndRealPaidCompanionsUseTheCurrentCountAndOnlyTheDefendingRole(string actor, int companions, bool attacking, int expected)
    {
        var initial = Position(); var game = new Driver(initial); var friends = Enumerable.Range(0, companions).Select(i => Friend(actor, i)).ToArray();
        try
        {
            foreach (var player in new[] { "P1", "P2" })
            {
                await Pay(game, player, player == actor ? [Host(actor), ..friends] : [Enemy(player)]);
                await game.Act(player, new EndTurnCommand());
            }
            var defender = attacking ? Other(actor) : actor;
            if (defender == "P2") await game.Act("P1", new EndTurnCommand());
            var army = new[] { Host(actor) }.Concat(friends).ToArray();
            var first = defender == actor ? army : [Enemy(defender)];
            Assert.All(first, id => Assert.False(game.State.CardObjects[id].IsExhausted));
            await game.Act(defender, new MoveUnitCommand(first[0], "BASE", "BATTLEFIELD:BF", [], SourceObjectIds: first));
            await Common.CompleteMovementWindow(() => game.State, game.Act); await game.Act(defender, new EndTurnCommand());
            var attacker = Other(defender); var second = attacker == actor ? army : [Enemy(attacker)];
            await game.Act(attacker, new MoveUnitCommand(second[0], "BASE", "BATTLEFIELD:BF", [], SourceObjectIds: second));
            AssertCondition(game.State, actor, Host(actor), "BF", companions, attacking, expected);
            var prefix = game.Events().Length; var generation = game.State.CardObjects[Host(actor)].ObjectGeneration;
            Assert.Equal(1, generation);
            await FinishBattle(() => game.State, game.Act, game.Events, actor, Host(actor), friends, Enemy(Other(actor)), expected,
                (p, c, id) => Ven001PhysicalThresholdConsumerTests.Reject(game, p, c, id));
            AssertOutcome(game.State, game.Events().Skip(prefix).ToArray(), Host(actor), expected, attacking, generation);
            Common.AssertTwelveEach(game.State); await game.Replay();
        }
        finally { await Save("actual-two-mana-" + actor + "-" + companions + "-" + attacking, initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task OneRealRecycledPowerHidesTheNativeCardAndLaterZeroCostReactionJoinsAnActualDefense(string actor)
    {
        var initial = Position(); var game = new Driver(initial); var friend = Friend(actor, 0); var host = Host(actor); var enemy = Other(actor);
        try
        {
            foreach (var player in new[] { "P1", "P2" })
            {
                await Pay(game, player, player == actor ? [friend] : [Enemy(player)]);
                await game.Act(player, new EndTurnCommand());
            }
            if (actor == "P2") await game.Act("P1", new EndTurnCommand());
            await game.Act(actor, new MoveUnitCommand(friend, "BASE", "BATTLEFIELD:BF", []));
            await Common.CompleteMovementWindow(() => game.State, game.Act);
            Assert.Equal(actor, game.State.CardObjects["BF"].ControllerId);
            await game.Act(actor, new RecycleRuneCommand(Rune(actor, 0))); var prefix = game.Events().Length;
            await game.Act(actor, new HideCardCommand(host, No, "BATTLEFIELD:BF", ["STANDBY_A"]));
            AssertCost(game.Events().Skip(prefix), 0, 1); AssertShadowPrivate(game.State, actor, host);
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            var placement = game.State.CardObjects[host].StandbyPlacement!; var generation = game.State.CardObjects[host].ObjectGeneration;
            await Ven001PhysicalThresholdConsumerTests.Reject(game, actor, new RevealCardCommand(host, No, [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:BF"), "ven117-same-turn-" + actor);
            Assert.Equal(generation, game.State.CardObjects[host].ObjectGeneration); AssertShadowPrivate(game.State, actor, host);
            await game.Act(actor, new EndTurnCommand());
            Assert.True(game.State.TurnNumber > placement.TurnNumber);
            await game.Act(enemy, new MoveUnitCommand(Enemy(enemy), "BASE", "BATTLEFIELD:BF", []));
            await RevealDuringRealBattle(() => game.State, game.Act, game.Events, actor, host, "BF");
            AssertCondition(game.State, actor, host, "BF", 1, false, 4);
            prefix = game.Events().Length; generation = game.State.CardObjects[host].ObjectGeneration;
            await FinishBattle(() => game.State, game.Act, game.Events, actor, host, [friend], Enemy(enemy), 4,
                (p, c, id) => Ven001PhysicalThresholdConsumerTests.Reject(game, p, c, id));
            AssertOutcome(game.State, game.Events().Skip(prefix).ToArray(), host, 4, false, generation);
            Common.AssertTwelveEach(game.State); await game.Replay();
        }
        finally { await Save("actual-one-power-standby-" + actor, initial, game); }
    }

    internal static async Task RevealDuringRealBattle(Func<MatchState> state, Func<string, GameCommand, Task> act, Func<RecoveredEvent[]> events,
        string actor, string host, string field)
    {
        for (var step = 0; step < 6 && !ResolutionResult.BuildPrompts(state())[actor].EnabledActions().Contains(CommandTypes.RevealCard); step++)
        {
            var current = state();
            if (current.PriorityPlayerId is { } priority) await act(priority, new PassPriorityCommand());
            else if (current.FocusPlayerId is { } focus) await act(focus, new PassFocusCommand());
            else Assert.Fail("The actual initial battle duel did not offer the native standby reaction.");
        }
        Assert.Contains(CommandTypes.RevealCard, ResolutionResult.BuildPrompts(state())[actor].EnabledActions());
        Assert.True(state().CardObjects[host].IsFaceDown); Assert.False(state().CardObjects[host].IsDefending);
        var prefix = events().Length;
        await act(actor, new RevealCardCommand(host, No, [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:" + field));
        AssertCost(events().Skip(prefix), 0, 0);
        for (var step = 0; step < 12 && state().StackItems.Count > 0; step++)
            await act(Assert.IsType<string>(state().PriorityPlayerId), new PassPriorityCommand());
        Assert.Empty(state().StackItems); Assert.False(state().CardObjects[host].IsFaceDown);
        Assert.Equal(field, state().ObjectLocations[host].BattlefieldObjectId);
    }

    internal static async Task FinishBattle(Func<MatchState> state, Func<string, GameCommand, Task> act, Func<RecoveredEvent[]> events,
        string actor, string host, string[] friends, string enemy, int expected, Func<string, GameCommand, string, Task>? reject = null)
    {
        var prefix = events().Length; var assigned = new HashSet<string>();
        for (var step = 0; step < 40 && !events().Skip(prefix).Any(e => e.Event.Kind == "BATTLE_CLOSED"); step++)
        {
            var current = state(); var prompts = ResolutionResult.BuildPrompts(current);
            var prompt = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            if (prompt is not null)
            {
                var player = prompt.PlayerId; var metadata = JsonSerializer.SerializeToElement(prompt.View!.Metadata, Json);
                var pool = metadata.GetProperty("assignableDamagePool"); var lethal = metadata.GetProperty("lethalDamageThreshold");
                List<CombatDamageAssignmentDto> assignments = [];
                if (player == actor)
                {
                    Assert.Equal(expected, pool.GetProperty(host).GetInt32()); Assert.Equal(6, lethal.GetProperty(enemy).GetInt32());
                    Assert.All(friends, id => Assert.Equal(3, pool.GetProperty(id).GetInt32()));
                    assignments.Add(new(host, enemy, expected)); assignments.AddRange(friends.Select(id => new CombatDamageAssignmentDto(id, enemy, 3)));
                    if (reject is not null)
                    {
                        var illegal = assignments.ToArray(); illegal[0] = new(host, enemy, expected + 1);
                        await reject(actor, new AssignCombatDamageCommand(current.BattleState.BattleId!, current.BattleState.BattlefieldObjectId!, illegal), "ven117-excess-current-pool-" + actor);
                    }
                }
                else
                {
                    Assert.Equal(6, pool.GetProperty(enemy).GetInt32()); Assert.Equal(expected, lethal.GetProperty(host).GetInt32());
                    Assert.All(friends, id => Assert.Equal(3, lethal.GetProperty(id).GetInt32()));
                    var hostDamage = friends.Length == 0 ? 6 : expected; assignments.Add(new(enemy, host, hostDamage));
                    var remaining = 6 - hostDamage;
                    for (var i = 0; i < friends.Length && remaining > 0; i++)
                    { var amount = i == friends.Length - 1 ? remaining : Math.Min(3, remaining); assignments.Add(new(enemy, friends[i], amount)); remaining -= amount; }
                    Assert.Equal(0, remaining);
                }
                Assert.True(assigned.Add(player));
                await act(player, new AssignCombatDamageCommand(current.BattleState.BattleId!, current.BattleState.BattlefieldObjectId!, assignments));
            }
            else if (current.PriorityPlayerId is { } priority) await act(priority, new PassPriorityCommand());
            else if (current.FocusPlayerId is { } focus) await act(focus, new PassFocusCommand());
            else
            {
                var player = Assert.Single(prompts.Values, p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
                await act(player, new DeclareBattleCommand(current.BattleState.BattlefieldObjectId!, current.BattleState.AttackerObjectIds, current.BattleState.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
        }
        // A single unit on each side uses the real automatic combat path.
        // Multi-unit cases keep both actual assignment windows and profile checks.
        Assert.Equal(friends.Length == 0 ? Array.Empty<string>() : new[] { "P1", "P2" }, assigned.Order());
        if (friends.Length == 0)
        {
            var automatic = events().Skip(prefix).Where(e => e.Event.Kind == "DAMAGE_APPLIED" && e.Event.Payload.ContainsKey("combatRole")).ToArray();
            Assert.Equal(2, automatic.Length); Assert.All(automatic, e => Assert.False(e.Event.Payload.ContainsKey("assignmentRole")));
        }
        Assert.Single(events().Skip(prefix), e => e.Event.Kind == "BATTLE_CLOSED");
    }

    internal static void AssertCondition(MatchState state, string actor, string host, string field, int companions, bool attacking, int expected)
    {
        Assert.Equal(1, state.CardObjects[host].Power); Assert.Equal(expected, Might(state, actor, host));
        Assert.Equal(attacking, state.CardObjects[host].IsAttacking); Assert.Equal(!attacking, state.CardObjects[host].IsDefending);
        Assert.Equal(companions == 1, state.CardObjects[host].RuleDerivedTags?.Contains("坚守3") == true);
        Assert.Equal(companions, state.CardObjects.Values.Count(c => c.ObjectId != host && c.ControllerId == actor && !c.IsFaceDown
            && c.Tags.Contains(CardObjectTags.UnitCard) && state.ObjectLocations.TryGetValue(c.ObjectId, out var at) && at.Zone == "BATTLEFIELD" && at.BattlefieldObjectId == field));
    }
    internal static void AssertOutcome(MatchState state, RecoveredEvent[] events, string host, int expected, bool attacking, long generation)
    {
        var dealt = events.Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), host) && e.Event.Payload.ContainsKey("combatRole")).ToArray();
        Assert.Equal(expected, dealt.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
        Assert.All(dealt, e => { Assert.Equal(1, e.Event.Payload["basePower"]); Assert.Equal(expected, e.Event.Payload["combatPower"]); Assert.Equal(expected - 1, e.Event.Payload["keywordBonus"]); Assert.Equal(attacking ? "ATTACKER" : "DEFENDER", e.Event.Payload["combatRole"]); });
        var incoming = Assert.Single(events, e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), host) && e.Event.Payload.ContainsKey("combatRole"));
        Assert.Equal(6, incoming.Event.Payload["basePower"]); Assert.Equal(6, incoming.Event.Payload["combatPower"]);
        Assert.Equal(generation, Convert.ToInt64(incoming.Event.Payload["targetGeneration"]));
        if (incoming.Event.Payload.TryGetValue("targetLethalDamageThreshold", out var threshold)) Assert.Equal(expected, Convert.ToInt32(threshold));
        else { Assert.Equal(1, expected); Assert.Equal(6, incoming.Event.Payload["damage"]); }
        Assert.Equal("GRAVEYARD", state.ObjectLocations[host].Zone); Assert.Equal(generation + 1, state.CardObjects[host].ObjectGeneration);
        Assert.Equal(1, state.CardObjects[host].Power); Assert.False(state.CardObjects[host].IsAttacking); Assert.False(state.CardObjects[host].IsDefending);
        Assert.DoesNotContain("坚守3", state.CardObjects[host].Tags); Assert.Null(state.CardObjects[host].RuleDerivedTags);
    }
    internal static void AssertShadowPrivate(MatchState state, string actor, string host)
    {
        Assert.True(state.CardObjects[host].IsFaceDown); Assert.Null(state.CardObjects[host].RuleDerivedTags);
        var snapshots = ResolutionResult.BuildSnapshots(state);
        foreach (var view in new[] { snapshots[Other(actor)], ResolutionResult.BuildSpectatorSnapshot(state) })
        {
            var subject = JsonSerializer.SerializeToElement(view, Json).GetProperty("players").GetProperty(actor);
            Assert.False(subject.GetProperty("objects").TryGetProperty(host, out _));
            Assert.DoesNotContain(host, subject.GetProperty("zones").GetProperty("battlefields").EnumerateArray().Select(v => v.GetString()));
            Assert.Equal(1, subject.GetProperty("zones").GetProperty("battlefieldHiddenStandbyCount").GetInt32());
        }
        var own = JsonSerializer.SerializeToElement(snapshots[actor], Json).GetProperty("players").GetProperty(actor).GetProperty("objects").GetProperty(host);
        Assert.Equal(No, own.GetProperty("cardNo").GetString());
    }
    internal static void AssertCost(IEnumerable<RecoveredEvent> events, int mana, int power)
    { var cost = Assert.Single(events, e => e.Event.Kind == "COST_PAID"); Assert.Equal(mana, cost.Event.Payload["totalManaCost"]); Assert.Equal(power, cost.Event.Payload["totalPowerCost"]); }
    private static async Task Pay(Driver game, string player, string[] units)
    {
        Assert.Equal(player, game.State.TurnPlayerId); Assert.Equal(0, game.State.RunePools[player].Mana); Assert.Equal(0, game.State.RunePools[player].TotalPower);
        var costs = units.Select(id => game.State.CardObjects[id].CardNo == No ? 2 : game.State.CardObjects[id].CardNo == FriendNo ? 3 : 6).ToArray();
        var runes = Enumerable.Range(0, costs.Sum()).Select(i => Rune(player, i)).ToArray();
        foreach (var batch in runes.Chunk(12)) await game.Act(player, new TapRuneCommand(batch[0], batch));
        for (var i = 0; i < units.Length; i++)
        { var prefix = game.Events().Length; var id = units[i]; await game.Act(player, new PlayCardCommand(id, game.State.CardObjects[id].CardNo!, [])); AssertCost(game.Events().Skip(prefix), costs[i], 0); Assert.True(game.State.CardObjects[id].IsExhausted); Assert.False(game.State.CardObjects[id].IsEmpowered); }
        Assert.Equal(0, game.State.RunePools[player].Mana); Assert.Equal(0, game.State.RunePools[player].TotalPower); Common.AssertTwelveEach(game.State);
    }
    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(No); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["BF"] = cards["BF"] with { ControllerId = null };
        foreach (var player in new[] { "P1", "P2" })
        {
            if (player == "P2") Put(Host(player), No, player);
            Put(Enemy(player), EnemyNo, player); for (var i = 0; i < 2; i++) Put(Friend(player, i), FriendNo, player);
            for (var i = 0; i < 12; i++)
            { var id = Rune(player, i); cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", player); locations[id] = new(player, "BASE"); zones[player] = zones[player] with { Base = [..zones[player].Base, id] }; }
        }
        void Put(string id, string no, string player)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player); locations[id] = new(player, "HAND"); zones[player] = zones[player] with { Hand = [..zones[player].Hand, id] }; }
        return state with { RoomId = "VEN117-PAID-CONDITIONAL-STEADFAST", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }
    internal static int Might(MatchState state, string actor, string host) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[actor], Json).GetProperty("players").GetProperty(actor).GetProperty("objects").GetProperty(host).GetProperty("effectivePower").GetInt32();
    internal static string Other(string player) => player == "P1" ? "P2" : "P1";
    private static string Host(string player) => player == "P1" ? "UNIT" : "P2-DISCIPLE";
    private static string Friend(string player, int i) => player + "-DISCIPLE-FRIEND-" + i;
    private static string Enemy(string player) => player + "-DISCIPLE-ENEMY";
    private static string Rune(string player, int i) => player + "-DISCIPLE-RUNE-" + i.ToString("D2");
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN117_PAID_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Declared turn-five hand material, zero pools andexactly12physicalyellowruneseach. ActualShen2mana/friend3/enemy6 normalplays oractual1genericpowerhide/later0reaction duringrealdefense; current0/1/2friends andattackrole, explicitMight/lethal/damage1/4/1 vsattack1, actual6enemydamage andnormalsettlement. No forcedkeyword/role/mana/history/result. Everycommandprivacy/currentcoldrecovery/duplicates/originalreplay. Legal40/12/3 separate; applicability/L2/L3/wholecard OPEN.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
