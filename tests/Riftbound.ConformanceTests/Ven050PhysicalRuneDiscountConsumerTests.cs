using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Common = Riftbound.ConformanceTests.Ven001PhysicalThresholdConsumerTests;

namespace Riftbound.ConformanceTests;

public sealed class Ven050PhysicalRuneDiscountConsumerTests
{
    internal const string No = "VEN·050";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(8, 4)] [InlineData(12, 0)]
    public async Task ActualFourManaPlayPaysFourOrZeroAndLaterRecyclingCannotRepriceIt(int controlled, int mana)
    {
        var initial = Position(true); var game = new Driver(initial);
        try
        {
            await Prepare(game, controlled); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.TapRunes("P1", Common.Runes(game.State).Take(mana));
            Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(controlled, Common.Runes(game.State).Length); Assert.Equal(12 - controlled, game.State.PlayerZones["P1"].RuneDeck.Count);
            var prefix = game.Events().Length; await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
            var paid = Assert.Single(game.State.StackItems).EmpowerActivation!;
            Assert.Equal(controlled, paid.ControlledRunes); Assert.Equal(mana, paid.PaidMana); Assert.Equal(generation, paid.Source.Generation); Assert.Null(paid.DestructionCost);
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Common.Might(game.State));
            var payment = Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "COST_PAID"); Assert.Equal(mana, payment.Event.Payload["totalManaCost"]);
            await game.Act("P1", new RecycleRuneCommand(Common.Runes(game.State)[0]));
            Assert.Equal(controlled - 1, Common.Runes(game.State).Length); Assert.Equal(paid, Assert.Single(game.State.StackItems).EmpowerActivation);
            Assert.Equal(13 - controlled, game.State.PlayerZones["P1"].RuneDeck.Count); Assert.Equal(0, game.State.RunePools["P1"].Mana);
            Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Common.Might(game.State)); Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
            Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects["UNIT"].Tags);
            var payments = game.Events().Count(e => e.Event.Kind == "COST_PAID");
            await Common.Reject(game, "P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []), "ven050-no-repeat-" + controlled);
            Assert.Equal(payments, game.Events().Count(e => e.Event.Kind == "COST_PAID"));
            await game.Act("P1", new EndTurnCommand()); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.Equal(4, Common.Might(game.State)); await game.Replay();
        }
        finally { await Save("actual-paid-and-later-rune-" + controlled, initial, game); }
    }

    [Fact]
    public async Task OneActualManaShortAndItsCachedIntentNeedAFreshTapAndFreshIntent()
    {
        var initial = Position(true); var game = new Driver(initial);
        try
        {
            await Prepare(game, 8); var runes = Common.Runes(game.State);
            await game.TapRunes("P1", runes.Take(3)); Assert.Equal(3, game.State.RunePools["P1"].Mana);
            var command = new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []); var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            raw["promptId"] = game.PromptFor("P1").PromptId; raw["snapshotTick"] = game.State.Tick;
            var retained = new ConformanceCommand("P1", "ven050-one-mana-short", JsonSerializer.SerializeToElement(raw, Json));
            await game.ActRetained(retained, false); Assert.Equal(ErrorCodes.InsufficientCost, game.LastRetainedResult!.ErrorCode);
            Assert.Empty(game.State.StackItems); Assert.Equal(3, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Act("P1", new TapRuneCommand(runes[3])); Assert.Equal(4, game.State.RunePools["P1"].Mana);
            await game.ActRetained(retained, false); Assert.Equal(4, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.StackItems);
            await game.Act("P1", command); Assert.Equal(0, game.State.RunePools["P1"].Mana);
            var paid = Assert.Single(game.State.StackItems).EmpowerActivation!; Assert.Equal(8, paid.ControlledRunes); Assert.Equal(4, paid.PaidMana);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Common.Might(game.State)); await game.Replay();
        }
        finally { await Save("actual-one-short-at-eight", initial, game); }
    }

    [Theory]
    [InlineData(true, false, 4)] [InlineData(true, true, 4)]
    [InlineData(false, false, 4)] [InlineData(false, true, 7)]
    public async Task ActualMovementGivesThreeMightOnlyToTheGenuinelyEmpoweredDefender(bool attacking, bool empowered, int expected)
    {
        var initial = Position(attacking); var game = new Driver(initial);
        try
        {
            await Prepare(game, 12, attacking ? "BASE" : "BATTLEFIELD:BF"); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            if (empowered)
            {
                await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
                Assert.Equal(12, Assert.Single(game.State.StackItems).EmpowerActivation!.ControlledRunes); Assert.Equal(0, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
                Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Top();
            }
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Common.Might(game.State));
            if (!attacking) await game.Act("P1", new EndTurnCommand());
            await game.Act(attacking ? "P1" : "P2", new MoveUnitCommand(attacking ? "UNIT" : "ENEMY", "BASE", attacking ? "BATTLEFIELD:OTHER" : "BATTLEFIELD:BF", []));
            Assert.Equal(attacking, game.State.CardObjects["UNIT"].IsAttacking); Assert.Equal(!attacking, game.State.CardObjects["UNIT"].IsDefending);
            Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.Equal(expected, Common.Might(game.State));
            await Ven014And030EmpoweredCombatConsumerTests.FinishCombat(game);
            var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT") && e.Event.Payload.ContainsKey("combatRole")).ToArray();
            Assert.NotEmpty(damage); Assert.Equal(expected, damage.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
            Assert.All(damage, e => { Assert.Equal(4, e.Event.Payload["basePower"]); Assert.Equal(expected, e.Event.Payload["combatPower"]); Assert.Equal(expected - 4, e.Event.Payload["keywordBonus"]); });
            Assert.False(game.State.CardObjects["UNIT"].IsAttacking); Assert.False(game.State.CardObjects["UNIT"].IsDefending);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Battlefields); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Common.Might(game.State));
            Assert.Empty(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers); await game.Replay();
        }
        finally { await Save("actual-combat-" + attacking + "-" + empowered, initial, game); }
    }

    internal static async Task Prepare(Driver game, int controlled, string destination = "BASE")
    {
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(6, Common.Runes(game.State).Length); Assert.Equal(6, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.TapRunes("P1", Common.Runes(game.State).Take(4));
        Assert.Equal(4, game.State.RunePools["P1"].Mana); await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        for (var expected = 8; expected <= controlled; expected += 2)
        {
            await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
            Assert.Equal("P1", game.State.TurnPlayerId); Assert.Equal(expected, Common.Runes(game.State).Length);
            Assert.Equal(12 - expected, game.State.PlayerZones["P1"].RuneDeck.Count); Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        }
    }
    internal static MatchState Position(bool attacking)
    {
        var state = Common.Position(attacking); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", No, "P1");
        for (var i = 0; i < 12; i++) cards["THRESHOLD-RUNE-" + i] = OfficialGarenConquestBattlefieldCountTests.Printed("THRESHOLD-RUNE-" + i, "UNL-R03", "P1");
        foreach (var actor in new[] { "P1", "P2" })
            for (var i = 4; i <= 12; i++)
            {
                var id = actor + "-DRAW-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·219/298", actor); locations[id] = new(actor, "MAIN_DECK");
                zones[actor] = zones[actor] with { MainDeck = [..zones[actor].MainDeck, id] };
            }
        return state with { RoomId = "VEN050-REAL-PHYSICAL-DISCOUNT", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
    internal static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN050_PHYSICAL_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit turn-five material, zero pools/exactly12 physical own blue runes, actual4mana No050 play, natural two-rune channeling to8/12, actual4/0mana native Empower and later real rune recycling. Roles use real movement/combat; Ward uses real enemy spell/runes. No forced status/role/result/source/profile, submitted opening, SQL/network, full applicability, latest complete L2/L3 or whole-card claim. Original per-command hidden/current recovery/dedup/original action-event replay retained.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
