using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven014And030EmpoweredCombatConsumerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static int Printed(string no) => no == "VEN·014" ? 2 : 3;

    [Theory]
    [InlineData("VEN·014", 37699, "暗影之魔", "red")]
    [InlineData("VEN·030", 37718, "无情苦修者", "green")]
    public async Task ExactPrintedSourcesKeepTheirActivationCostSeparateFromConditionalCombatKeywords(string no, long id, string name, string trait)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(name, card.CardName); Assert.Equal(Printed(no), card.Energy); Assert.Equal(Printed(no), card.Power);
        Assert.Null(card.ReturnEnergy); Assert.Equal([trait], card.CardColorList); Assert.Empty(card.Tag); Assert.Equal("艾欧尼亚", card.Region);
        Assert.Equal(no == "VEN·014"
            ? "{{强化2红色}}（支付{{2}}和{{红色}}：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{强攻3}}。（如果我是进攻方，则{{S}}+3。）"
            : "{{强化3}}（支付{{3}}：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{法盾}}和{{坚守3}}。（对手必须支付{{A}}才能将我选作法术或技能的目标。如果我是防守方，则{{S}}+3。）", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior)); Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(Printed(no), behavior.ManaCost); Assert.Equal(Printed(no), behavior.SourceUnitPower); Assert.Equal(0, PrintedPowerCostRules.ForCard(no).Amount);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.True(cost.SupportedResources); Assert.Equal(Printed(no), cost.Mana); Assert.Equal(0, cost.Power); Assert.False(cost.Exhaust);
        if (no == "VEN·014") { Assert.Single(cost.TypedPower); Assert.Equal(1, cost.TypedPower[RuneTrait.Red]); }
        else Assert.Empty(cost.TypedPower);
        var self = EmpowerParser.Self(card.CardEffect); Assert.Equal(0, self.Might);
        Assert.Equal(no == "VEN·014" ? new[] { "强攻3" } : new[] { "法盾", "坚守3" }, self.Keywords);
    }

    [Theory]
    [InlineData("VEN·014", false, false, 2)] [InlineData("VEN·014", false, true, 2)]
    [InlineData("VEN·014", true, false, 2)] [InlineData("VEN·014", true, true, 5)]
    [InlineData("VEN·030", false, false, 3)] [InlineData("VEN·030", false, true, 6)]
    [InlineData("VEN·030", true, false, 3)] [InlineData("VEN·030", true, true, 3)]
    public async Task ActualPhysicalPlayEmpowerMovementAndCombatUseOnlyTheMatchingRole(string no, bool attacking, bool empowered, int expected)
    {
        var initial = Position(no, attacking); var game = new Driver(initial);
        try
        {
            await game.TapRunes("P1", Enumerable.Range(0, Printed(no) * 2).Select(i => "EMPOWER-COMBAT-RUNE-" + i));
            Assert.Equal(Printed(no) * 2, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: attacking ? "BASE" : "BATTLEFIELD:BF"));
            Assert.Equal(Printed(no), game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(Printed(no), Might(game.State)); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            if (empowered)
            {
                if (no == "VEN·014")
                {
                    await game.Act("P1", new RecycleRuneCommand("EMPOWER-COMBAT-RUNE-0"));
                    Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
                }
                await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
                Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(Printed(no), Might(game.State));
                var paid = Assert.Single(game.State.StackItems).EmpowerActivation!;
                Assert.Equal(Printed(no), paid.PaidMana); Assert.Equal(generation, paid.Source.Generation);
                await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            }
            Assert.Equal(Printed(no), Might(game.State));
            Assert.Equal(no == "VEN·030" && empowered, game.State.CardObjects["UNIT"].Tags.Contains(CardObjectTags.Spellshield));
            await game.Act("P1", new EndTurnCommand());
            if (attacking) await game.Act("P2", new EndTurnCommand());
            await game.Act(attacking ? "P1" : "P2", new MoveUnitCommand(attacking ? "UNIT" : "ENEMY", "BASE", attacking ? "BATTLEFIELD:OTHER" : "BATTLEFIELD:BF", []));
            Assert.Equal(attacking, game.State.CardObjects["UNIT"].IsAttacking); Assert.Equal(!attacking, game.State.CardObjects["UNIT"].IsDefending);
            Assert.Equal(Printed(no), game.State.CardObjects["UNIT"].Power); Assert.Equal(expected, Might(game.State));
            await FinishCombat(game);
            var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT")
                && e.Event.Payload.ContainsKey("combatRole")).ToArray();
            Assert.NotEmpty(damage); Assert.Equal(expected, damage.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
            Assert.All(damage, e => { Assert.Equal(Printed(no), e.Event.Payload["basePower"]); Assert.Equal(expected, e.Event.Payload["combatPower"]);
                Assert.Equal(expected - Printed(no), e.Event.Payload["keywordBonus"]); });
            Assert.False(game.State.CardObjects["UNIT"].IsAttacking); Assert.False(game.State.CardObjects["UNIT"].IsDefending);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Battlefields); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(Printed(no), Might(game.State));
            Assert.Empty(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers); await game.Replay();
        }
        finally { await Save(no + "-role-" + attacking + "-" + empowered, initial, game); }
    }

    [Fact]
    public async Task ActualBluePowerCannotPayTheRedEmpowerCostAndFreshRedPaymentLeavesBlueUntouched()
    {
        var initial = Position("VEN·014", true, true); var game = new Driver(initial);
        try
        {
            await game.TapRunes("P1", Enumerable.Range(0, 4).Select(i => "EMPOWER-COMBAT-RUNE-" + i));
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·014", []));
            await game.Act("P1", new RecycleRuneCommand("EMPOWER-COMBAT-RUNE-0"));
            Assert.Equal(2, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            var command = new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []); var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            raw["promptId"] = game.PromptFor("P1").PromptId; raw["snapshotTick"] = game.State.Tick;
            await game.ActRetained(new("P1", "empowered-combat-wrong-blue", JsonSerializer.SerializeToElement(raw, Json)), false);
            Assert.Equal(ErrorCodes.InsufficientCost, game.LastRetainedResult!.ErrorCode); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(2, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            await game.Act("P1", new RecycleRuneCommand("RED-CORRECTION"));
            Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]); await game.Act("P1", command);
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(2, Might(game.State)); await game.Replay();
        }
        finally { await Save("VEN014-wrong-blue-then-real-red", initial, game); }
    }

    internal static MatchState Position(string no, bool attacking, bool blueFirst = false)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·096/298", "P2") } };
        if (attacking) state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var added = new List<string>();
        void Rune(string id, string cardNo)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, "P1"); locations[id] = new("P1", "BASE"); added.Add(id); }
        for (var i = 0; i < Printed(no) * 2; i++) Rune("EMPOWER-COMBAT-RUNE-" + i, blueFirst ? "UNL-R03" : no == "VEN·014" ? "UNL-R01" : "UNL-R02");
        if (blueFirst) Rune("RED-CORRECTION", "UNL-R01");
        return state with { RoomId = "VEN014-030-EXPLICIT-COMBAT-" + no, CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, ..added] } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }
    internal static async Task FinishCombat(Driver game)
    {
        for (var step = 0; step < 40 && !game.Events().Any(e => e.Event.Kind == "BATTLE_CLOSED"); step++)
        {
            var prompts = ResolutionResult.BuildPrompts(game.State);
            var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            if (assignment is not null)
                await game.Act(assignment.PlayerId, new AssignCombatDamageCommand(game.State.BattleState.BattleId!, game.State.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(assignment)));
            else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else
            {
                var actor = Assert.Single(prompts.Values, p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
                var battle = game.State.BattleState;
                await game.Act(actor, new DeclareBattleCommand(battle.BattlefieldObjectId!, battle.AttackerObjectIds, battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
        }
        Assert.Contains(game.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
    }
    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_EMPOWER_COMBAT_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Exact native No014/030, existing explicit turn-five unit-entry fixture, zero pools and physical runes. Actual play, optional real native empowerment, natural turn changes, movement-established roles, declarations/assignments and combat damage. OGN096 opposing unit pre-exists; no assigned role, empowered state, source/profile or result injection. Existing per-command private/current recovery/dedup/original replay guards remain. No canonical opening, SQL/network, whole-card, applicability or complete L2/L3 claim.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
