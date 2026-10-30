using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven001PhysicalThresholdConsumerTests
{
    internal const string No = "VEN·001";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCn37686SeparatesSixManaPlayFromTheFourRuneEmpowerThresholdAndCombatBonus()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37686, card.Id); Assert.Equal("巴凯旋沙者", card.CardName); Assert.Equal(6, card.Energy); Assert.Equal(6, card.Power);
        Assert.Null(card.ReturnEnergy); Assert.Equal([RuneTrait.Red], card.CardColorList); Assert.Equal("恕瑞玛", card.Region); Assert.Empty(card.Tag);
        Assert.Equal("{{强化5}}。如果你控制的符文数量不超过四枚，则此技能的费用减少{{3}}。（支付此费用：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{法盾}}和{{强攻2}}。（对手必须支付{{A}}才能将我选作法术或技能的目标。如果我是进攻方，则{{S}}+2。）", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior)); Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(6, behavior.ManaCost); Assert.Equal(6, behavior.SourceUnitPower); Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
        var spec = Assert.Single(EmpowerParser.Parse(card.CardEffect)); var cost = Assert.Single(spec.Alternatives);
        Assert.Equal(4, spec.RuneLimit); Assert.Equal(3, spec.ManaReductionAtMostRunes); Assert.False(spec.ReduceManaPerRune);
        Assert.Equal(5, cost.Mana); Assert.True(cost.SupportedResources); Assert.Empty(cost.TypedPower); Assert.Equal(0, cost.Power);
        var self = EmpowerParser.Self(card.CardEffect); Assert.Equal(0, self.Might); Assert.Equal(new[] { "法盾", "强攻2" }, self.Keywords);
    }

    [Theory]
    [InlineData(4, 2)] [InlineData(5, 5)]
    public async Task ActualSixManaPlayAndRealRecyclingCaptureTheThresholdBeforeALaterRuneLeaves(int controlled, int mana)
    {
        var initial = Position(true); var game = new Driver(initial);
        try
        {
            await Prepare(game); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            var eight = Runes(game.State); await game.TapRunes("P1", eight);
            foreach (var id in eight.Skip(controlled)) await game.Act("P1", new RecycleRuneCommand(id));
            Assert.Equal(controlled, Runes(game.State).Length); Assert.Equal(12 - controlled, game.State.PlayerZones["P1"].RuneDeck.Count);
            Assert.All(Runes(game.State), id => Assert.True(game.State.CardObjects[id].IsExhausted));
            Assert.Equal(8, game.State.RunePools["P1"].Mana); Assert.Equal(8 - controlled, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
            var captured = Assert.Single(game.State.StackItems).EmpowerActivation!;
            Assert.Equal(controlled, captured.ControlledRunes); Assert.Equal(mana, captured.PaidMana); Assert.Equal(generation, captured.Source.Generation);
            Assert.Null(captured.DestructionCost); Assert.Equal(8 - mana, game.State.RunePools["P1"].Mana);
            Assert.Equal(8 - controlled, game.State.RunePools["P1"].TotalPower); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(6, Might(game.State));
            await game.Act("P1", new RecycleRuneCommand(Runes(game.State)[0]));
            Assert.Equal(controlled - 1, Runes(game.State).Length); Assert.Equal(captured, Assert.Single(game.State.StackItems).EmpowerActivation);
            Assert.Equal(8 - mana, game.State.RunePools["P1"].Mana); Assert.Equal(9 - controlled, game.State.RunePools["P1"].TotalPower); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, Might(game.State)); Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
            Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects["UNIT"].Tags);
            var paidCount = game.Events().Count(e => e.Event.Kind == "COST_PAID");
            await Reject(game, "P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []), "ven001-repeat-" + controlled);
            Assert.Equal(paidCount, game.Events().Count(e => e.Event.Kind == "COST_PAID"));
            await game.Act("P1", new EndTurnCommand()); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.Equal(6, Might(game.State)); await game.Replay();
        }
        finally { await Save("actual-threshold-and-later-rune-" + controlled, initial, game); }
    }

    [Theory]
    [InlineData(4, 2)] [InlineData(5, 5)]
    public async Task OneActualManaShortRejectsAtomicallyAndAFreshTapNeedsAFreshIntent(int controlled, int mana)
    {
        var initial = Position(true); var game = new Driver(initial);
        try
        {
            await Prepare(game); var eight = Runes(game.State);
            await game.TapRunes("P1", eight.Take(mana - 1));
            foreach (var id in eight.Skip(controlled)) await game.Act("P1", new RecycleRuneCommand(id));
            Assert.Equal(controlled, Runes(game.State).Length); Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
            var command = new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []);
            var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject(); raw["promptId"] = game.PromptFor("P1").PromptId; raw["snapshotTick"] = game.State.Tick;
            var retained = new ConformanceCommand("P1", "ven001-one-mana-short-" + controlled, JsonSerializer.SerializeToElement(raw, Json));
            await game.ActRetained(retained, false); Assert.Equal(ErrorCodes.InsufficientCost, game.LastRetainedResult!.ErrorCode);
            Assert.Empty(game.State.StackItems); Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Act("P1", new TapRuneCommand(eight[mana - 1])); Assert.Equal(mana, game.State.RunePools["P1"].Mana);
            await game.ActRetained(retained, false); Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.StackItems);
            await game.Act("P1", command); Assert.Equal(0, game.State.RunePools["P1"].Mana);
            var paid = Assert.Single(game.State.StackItems).EmpowerActivation!; Assert.Equal(controlled, paid.ControlledRunes); Assert.Equal(mana, paid.PaidMana);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, Might(game.State)); await game.Replay();
        }
        finally { await Save("actual-one-short-" + controlled, initial, game); }
    }

    [Theory]
    [InlineData(true, false, 6)] [InlineData(true, true, 8)]
    [InlineData(false, false, 6)] [InlineData(false, true, 6)]
    public async Task ActualMovementAndCombatOnlyGiveTheTwoMightBonusToAnEmpoweredAttacker(bool attacking, bool empowered, int expected)
    {
        var initial = Position(attacking); var game = new Driver(initial);
        try
        {
            await Prepare(game, attacking ? "BASE" : "BATTLEFIELD:BF"); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            var eight = Runes(game.State); await game.TapRunes("P1", eight);
            foreach (var id in eight.Skip(4)) await game.Act("P1", new RecycleRuneCommand(id));
            if (empowered)
            {
                await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
                Assert.Equal(2, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Top();
            }
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, Might(game.State));
            if (!attacking) await game.Act("P1", new EndTurnCommand());
            await game.Act(attacking ? "P1" : "P2", new MoveUnitCommand(attacking ? "UNIT" : "ENEMY", "BASE", attacking ? "BATTLEFIELD:OTHER" : "BATTLEFIELD:BF", []));
            Assert.Equal(attacking, game.State.CardObjects["UNIT"].IsAttacking); Assert.Equal(!attacking, game.State.CardObjects["UNIT"].IsDefending);
            Assert.Equal(6, game.State.CardObjects["UNIT"].Power); Assert.Equal(expected, Might(game.State));
            await Ven014And030EmpoweredCombatConsumerTests.FinishCombat(game);
            var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT") && e.Event.Payload.ContainsKey("combatRole")).ToArray();
            Assert.NotEmpty(damage); Assert.Equal(expected, damage.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
            Assert.All(damage, e => { Assert.Equal(6, e.Event.Payload["basePower"]); Assert.Equal(expected, e.Event.Payload["combatPower"]); Assert.Equal(expected - 6, e.Event.Payload["keywordBonus"]); });
            Assert.False(game.State.CardObjects["UNIT"].IsAttacking); Assert.False(game.State.CardObjects["UNIT"].IsDefending);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Battlefields); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, Might(game.State)); Assert.Empty(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers); await game.Replay();
        }
        finally { await Save("actual-combat-" + attacking + "-" + empowered, initial, game); }
    }

    internal static async Task Prepare(Driver game, string destination = "BASE")
    {
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(6, Runes(game.State).Length); Assert.Equal(6, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.TapRunes("P1", Enumerable.Range(0, 6).Select(i => "THRESHOLD-RUNE-" + i));
        Assert.Equal(6, game.State.RunePools["P1"].Mana); await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId); Assert.Equal(8, Runes(game.State).Length); Assert.Equal(4, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
    }
    internal static string[] Runes(MatchState state) => state.PlayerZones["P1"].Base.Concat(state.PlayerZones["P1"].Battlefields)
        .Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && state.CardObjects[id].ControllerId == "P1").Order().ToArray();
    internal static MatchState Position(bool attacking)
    {
        var state = VenUnitEntryBasicTests.Position(No);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·096/298", "P2") } };
        if (attacking) state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        for (var i = 0; i < 12; i++)
        {
            var id = "THRESHOLD-RUNE-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R01", "P1"); locations[id] = new("P1", i < 6 ? "BASE" : "RUNE_DECK");
            zones["P1"] = i < 6 ? zones["P1"] with { Base = [..zones["P1"].Base, id] } : zones["P1"] with { RuneDeck = [..zones["P1"].RuneDeck, id] };
        }
        return state with { RoomId = "VEN001-REAL-PHYSICAL-THRESHOLD", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }
    internal static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    internal static async Task Reject(Driver game, string actor, GameCommand command, string intent)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject(); raw["promptId"] = game.PromptFor(actor).PromptId; raw["snapshotTick"] = game.State.Tick;
        await game.ActRetained(new(actor, intent, JsonSerializer.SerializeToElement(raw, Json)), false);
    }
    internal static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN001_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit turn-five material with zero pools/exactly12 physical own runes; actual6mana No001 play, natural turns/channeling, real rune taps/recycling to four/five and2/5mana native Empower. Combat roles use actual movement/declarations/assignments; Ward enemy pays actual rune resources. No forced empowerment/role/result/source/profile, canonical opening, SQL/network, latest complete L2/L3, full applicability or whole-card claim. Original per-command privacy/current recovery/dedup/original action replay retained.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
