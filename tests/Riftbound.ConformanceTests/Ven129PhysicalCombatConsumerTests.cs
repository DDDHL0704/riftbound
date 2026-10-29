using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Common = Riftbound.ConformanceTests.Ven001PhysicalThresholdConsumerTests;

namespace Riftbound.ConformanceTests;

public sealed class Ven129PhysicalCombatConsumerTests
{
    internal const string No = "VEN·129";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(true, 0, 0)] [InlineData(true, 1, 6)] [InlineData(true, 2, 0)]
    [InlineData(false, 0, 0)] [InlineData(false, 1, 6)] [InlineData(false, 2, 0)]
    public async Task ActualYellowPaymentAndPaidCompanionsKeepSixMightWhileRealCombatPermitsOnlyExactlyOneOtherUnit(bool attacking, int companions, int damage)
    {
        var initial = Position(attacking); var game = new Driver(initial);
        try
        {
            AssertTwelveEach(game.State);
            var manaRunes = Enumerable.Range(0, 4 + 3 * companions).Select(i => Rune("P1", i)).ToArray();
            await game.Act("P1", new TapRuneCommand(manaRunes[0], manaRunes));
            await game.Act("P1", new RecycleRuneCommand(Rune("P1", 0)));
            Assert.Equal(4 + 3 * companions, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
            var prefix = game.Events().Length; await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            var payment = Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "COST_PAID");
            Assert.Equal(4, payment.Event.Payload["totalManaCost"]); Assert.Equal(1, payment.Event.Payload["totalPowerCost"]);
            Assert.Equal(6, game.State.CardObjects["UNIT"].Power); Assert.Equal(6, Common.Might(game.State)); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
            var members = new List<string> { "UNIT" };
            for (var i = 0; i < companions; i++)
            {
                var id = "COMPANION-" + i; prefix = game.Events().Length;
                await game.Act("P1", new PlayCardCommand(id, "VEN·124", []));
                var paid = Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "COST_PAID"); Assert.Equal(3, paid.Event.Payload["totalManaCost"]);
                Assert.True(game.State.CardObjects[id].IsExhausted); Assert.False(game.State.CardObjects[id].IsEmpowered); Assert.Equal(3, game.State.CardObjects[id].Power); members.Add(id);
            }
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(11, Runes(game.State, "P1").Length); Assert.Equal(new[] { Rune("P1", 0) }, game.State.PlayerZones["P1"].RuneDeck);
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
            Assert.Equal("P1", game.State.TurnPlayerId); Assert.Equal(12, Runes(game.State, "P1").Length); Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
            Assert.All(members, id => Assert.False(game.State.CardObjects[id].IsExhausted)); AssertTwelveEach(game.State);
            var field = attacking ? "OTHER" : "BF";
            await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:" + field, [], SourceObjectIds: members));
            if (!attacking)
            {
                await CompleteMovementWindow(() => game.State, game.Act);
                await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
            }
            Assert.Equal(attacking, game.State.CardObjects["UNIT"].IsAttacking); Assert.Equal(!attacking, game.State.CardObjects["UNIT"].IsDefending);
            Assert.Equal(companions, OtherFriendlyUnits(game.State, "P1", "UNIT", field));
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
            Assert.Equal(6, Common.Might(game.State)); prefix = game.Events().Length;
            await FinishCombat(() => game.State, game.Act, game.Events, "UNIT", damage);
            var dealt = game.Events().Skip(prefix).Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT") && e.Event.Payload.ContainsKey("combatRole")).ToArray();
            Assert.Equal(damage, dealt.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
            Assert.All(dealt, e => { Assert.Equal(6, e.Event.Payload["basePower"]); Assert.Equal(6, e.Event.Payload["combatPower"]); Assert.Equal(0, e.Event.Payload["keywordBonus"]); });
            Assert.Equal(6, game.State.CardObjects["UNIT"].Power); Assert.Equal(6, Common.Might(game.State)); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.False(game.State.CardObjects["UNIT"].IsAttacking); Assert.False(game.State.CardObjects["UNIT"].IsDefending); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(0, game.State.CardObjects["UNIT"].Damage); AssertTwelveEach(game.State); await game.Replay();
        }
        finally { await Save("actual-paid-combat-" + attacking + "-" + companions, initial, game); }
    }

    [Theory]
    [InlineData("mana")] [InlineData("trait")]
    public async Task APrintedManaOrYellowShortfallAndItsCachedIntentCannotConsumeLaterRealResources(string missing)
    {
        var initial = Position(true);
        if (missing == "trait") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [Rune("P1", 0)] = OfficialGarenConquestBattlefieldCountTests.Printed(Rune("P1", 0), "UNL-R04", "P1") } };
        var game = new Driver(initial);
        try
        {
            var manaRunes = Enumerable.Range(0, missing == "mana" ? 3 : 4).Select(i => Rune("P1", i)).ToArray();
            await game.Act("P1", new TapRuneCommand(manaRunes[0], manaRunes));
            await game.Act("P1", new RecycleRuneCommand(Rune("P1", 0)));
            var play = new PlayCardCommand("UNIT", No, []); var raw = JsonSerializer.SerializeToNode(play, Json)!.AsObject();
            raw["promptId"] = game.PromptFor("P1").PromptId; raw["snapshotTick"] = game.State.Tick;
            var retained = new ConformanceCommand("P1", "ven129-short-" + missing, JsonSerializer.SerializeToElement(raw, Json));
            await game.ActRetained(retained, false); Assert.Equal(ErrorCodes.InsufficientCost, game.LastRetainedResult!.ErrorCode);
            Assert.Equal(missing == "mana" ? 3 : 4, game.State.RunePools["P1"].Mana); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
            if (missing == "mana") await game.Act("P1", new TapRuneCommand(Rune("P1", 3)));
            else await game.Act("P1", new RecycleRuneCommand(Rune("P1", 1)));
            await game.ActRetained(retained, false); Assert.Equal(4, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
            var prefix = game.Events().Length; await game.Act("P1", play);
            var paid = Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "COST_PAID"); Assert.Equal(4, paid.Event.Payload["totalManaCost"]); Assert.Equal(1, paid.Event.Payload["totalPowerCost"]);
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(missing == "trait" ? 1 : 0, game.State.RunePools["P1"].TotalPower);
            if (missing == "trait") Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Equal(6, Common.Might(game.State)); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); AssertTwelveEach(game.State); await game.Replay();
        }
        finally { await Save("actual-short-" + missing, initial, game); }
    }

    internal static async Task CompleteMovementWindow(Func<MatchState> state, Func<string, GameCommand, Task> act)
    {
        for (var step = 0; step < 20; step++)
        {
            if (state().PriorityPlayerId is { } priority) await act(priority, new PassPriorityCommand());
            else if (state().FocusPlayerId is { } focus) await act(focus, new PassFocusCommand());
            else break;
        }
        Assert.Null(state().FocusPlayerId); Assert.Null(state().PriorityPlayerId);
        Assert.Equal(TimingStates.NeutralOpen, state().TimingState); Assert.Empty(state().StackItems);
    }
    internal static async Task FinishCombat(Func<MatchState> state, Func<string, GameCommand, Task> act, Func<RecoveredEvent[]> events, string unit, int expected)
    {
        var prefix = events().Length; var observedOwnAssignment = false;
        for (var step = 0; step < 40 && !events().Skip(prefix).Any(e => e.Event.Kind == "BATTLE_CLOSED"); step++)
        {
            var current = state(); var prompts = ResolutionResult.BuildPrompts(current);
            var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            if (assignment is not null)
            {
                var metadata = JsonSerializer.SerializeToElement(assignment.View!.Metadata, Json);
                if (assignment.PlayerId == current.CardObjects[unit].ControllerId)
                {
                    var pools = metadata.GetProperty("assignableDamagePool");
                    Assert.Equal(expected, pools.TryGetProperty(unit, out var amount) ? amount.GetInt32() : 0); observedOwnAssignment = true;
                }
                if (metadata.GetProperty("lethalDamageThreshold").TryGetProperty(unit, out var lethal)) Assert.Equal(6, lethal.GetInt32());
                await act(assignment.PlayerId, new AssignCombatDamageCommand(current.BattleState.BattleId!, current.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(assignment)));
            }
            else if (current.PriorityPlayerId is { } priority) await act(priority, new PassPriorityCommand());
            else if (current.FocusPlayerId is { } focus) await act(focus, new PassFocusCommand());
            else
            {
                var actor = Assert.Single(prompts.Values, p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
                await act(actor, new DeclareBattleCommand(current.BattleState.BattlefieldObjectId!, current.BattleState.AttackerObjectIds, current.BattleState.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
        }
        if (expected > 0) Assert.True(observedOwnAssignment);
        Assert.Contains(events().Skip(prefix), e => e.Event.Kind == "BATTLE_CLOSED");
    }
    internal static MatchState Position(bool attacking)
    {
        var state = VenUnitEntryBasicTests.Position(No); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "VEN·124", "P1");
        cards["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "VEN·124", "P2");
        cards["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "VEN·137", "P1");
        for (var i = 0; i < 2; i++)
        {
            var id = "COMPANION-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "VEN·124", "P1"); locations[id] = new("P1", "HAND");
            zones["P1"] = zones["P1"] with { Hand = [..zones["P1"].Hand, id] };
        }
        foreach (var actor in new[] { "P1", "P2" }) for (var i = 0; i < 12; i++)
        {
            var id = Rune(actor, i); var field = actor == "P1" || i < 8; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", actor); locations[id] = new(actor, field ? "BASE" : "RUNE_DECK");
            zones[actor] = field ? zones[actor] with { Base = [..zones[actor].Base, id] } : zones[actor] with { RuneDeck = [..zones[actor].RuneDeck, id] };
        }
        state = state with { RoomId = "VEN129-PAID-EXACT-ONE-COMPANION", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        return attacking ? LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER") : state;
    }
    private static string Rune(string actor, int i) => "GUARD-RUNE-" + actor + "-" + i.ToString("D2");
    internal static string[] Runes(MatchState state, string actor) => state.PlayerZones[actor].Base.Concat(state.PlayerZones[actor].Battlefields)
        .Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).Order().ToArray();
    internal static int OtherFriendlyUnits(MatchState state, string actor, string source, string field) => state.CardObjects.Values.Count(c => c.ObjectId != source
        && c.ControllerId == actor && c.Tags.Contains(CardObjectTags.UnitCard) && !c.IsFaceDown && state.ObjectLocations[c.ObjectId].Zone == "BATTLEFIELD" && state.ObjectLocations[c.ObjectId].BattlefieldObjectId == field);
    internal static void AssertTwelveEach(MatchState state)
    {
        foreach (var actor in new[] { "P1", "P2" })
        {
            Assert.Equal(12, state.CardObjects.Values.Count(c => c.OwnerId == actor && c.Tags.Contains(CardObjectTags.RuneCard)));
            Assert.Equal(12, Runes(state, actor).Length + state.PlayerZones[actor].RuneDeck.Count);
        }
    }
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN129_PHYSICAL_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit turn-five normal printed No129, zero pools/exactly12 physical runes per player. Actual4mana+yellow and0/1/2 real3mana No124 companions, normal turns/readiness/channeling, group movement and real battle0/6damage while Might/lethal6. No forced status/role/control/profile/damage or battle marker. Original per-command privacy/current recovery/dedup/original replay. Submitted openings separate; source applicability, latest complete L2/L3 and whole-card OPEN.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
