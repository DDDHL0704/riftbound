using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenModalReactionTests
{
    private const string No = "VEN·052";
    private const string Return = "RETURN_FRIENDLY_UNIT";
    private const string Reduce = "ENEMY_UNIT_POWER_MINUS_2";

    [Fact]
    public async Task ExactCurrentSourceRequiresOneOfTwoSeparateReactionEffects()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var card = Assert.Single(catalog.Cards, c => c.CardNo == No);
        Assert.Equal(37743, card.Id); Assert.Equal(1, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Null(card.Power);
        Assert.Equal("{{反应}}（可在任意时机打出，甚至先于其他法术和技能的结算。）\n选择一个效果 —\n- 让一名友方单位返回其所属的手牌。\n- 给予一名敌方单位在本回合内{{S}}-2。", card.CardEffect);
        foreach (var mode in new[] { Return, Reduce }) {
            Assert.True(CardBehaviorRegistry.TryGetByCardNoAndMode(No, mode, out var behavior));
            Assert.Equal(1, behavior.ManaCost); Assert.Equal(1, behavior.RequiredTargetCount);
            Assert.True(behavior.CanPlayDuringPriority);
            Assert.Equal(mode == Return, behavior.ReturnsTargetToHand);
            Assert.Equal(mode == Reduce ? -2 : 0, behavior.PowerModifierAmount);
            Assert.Equal(0, behavior.MinimumPowerAfterModifier);
        }
        Assert.False(CardBehaviorRegistry.TryGetByCardNoAndMode(No, "", out _));
    }

    public static IEnumerable<object[]> Windows() => new[] { Return, Reduce }.SelectMany(mode => new[] {
        "OWN_NEUTRAL", "OWN_RESPONSE", "OFFTURN_RESPONSE", "OWN_DUEL", "OFFTURN_DUEL" }.Select(window => new object[] { mode, window }));

    [Theory]
    [MemberData(nameof(Windows))]
    public async Task EachModeKeepsItsTargetMeaningInRealOwnTurnAndOffTurnReactionWindows(string mode, string window)
    {
        var initial = Position(); var turn = window.StartsWith("OFFTURN", StringComparison.Ordinal) ? "P2" : "P1";
        initial = initial with { TurnPlayerId = turn, ActivePlayerId = turn };
        if (window.Contains("RESPONSE", StringComparison.Ordinal))
            initial = AttackDamageDistributionTests.Spell(initial, "PENDING", "OGN·048/298", turn);
        if (window == "OFFTURN_DUEL") {
            initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
            initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        }
        var driver = new Driver(initial);
        if (window.Contains("RESPONSE", StringComparison.Ordinal)) {
            await driver.Act(turn, new PlayCardCommand("PENDING", "OGN·048/298", []));
            if (driver.State.PriorityPlayerId != "P1") await driver.Act(turn, new PassPriorityCommand());
        }
        if (window.Contains("DUEL", StringComparison.Ordinal)) {
            await driver.Act(turn, new MoveUnitCommand(turn == "P1" ? "ALLY" : "ENEMY", "BASE", turn == "P1" ? "BATTLEFIELD:OTHER" : "BATTLEFIELD:BF", []));
            if (driver.State.FocusPlayerId != "P1") await driver.Act(turn, new PassFocusCommand());
        }
        var target = mode == Return ? "ALLY" : "ENEMY";
        var mana = driver.State.RunePools["P1"].Mana; var blue = driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue];
        await driver.Act("P1", new PlayCardCommand("SPELL", No, [target], Mode: mode));
        var item = driver.State.StackItems[^1];
        Assert.Equal(mode == Return ? "VEN_MODAL_REACTION_RETURN_FRIENDLY_UNIT" : "VEN_MODAL_REACTION_ENEMY_POWER_MINUS_2", item.EffectKind);
        Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power);
        Assert.Equal(mana - 1, driver.State.RunePools["P1"].Mana); Assert.Equal(blue - 1, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        await driver.Top();
        if (mode == Return) {
            Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Hand); Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power);
        } else {
            Assert.Equal(2, driver.State.CardObjects["ENEMY"].Power);
            Assert.DoesNotContain("ALLY", driver.State.PlayerZones["P1"].Hand);
            var modifier = Assert.Single(driver.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifiers);
            Assert.Equal(-2, modifier.PowerDelta); Assert.Equal(-2, modifier.RequestedPowerDelta); Assert.Equal(0, modifier.MinimumPower);
        }
        while (driver.State.StackItems.Count > 0) await driver.Top();
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard); await driver.Replay();
        Export($"window-{mode}-{window}", initial, driver);
    }

    public static IEnumerable<object[]> Invalid() => new[] { Return, Reduce }.SelectMany(mode => new[] {
        "opposite-controller", "equipment", "battlefield", "graveyard", "deck", "missing", "extra", "mana", "trait" }
        .Select(branch => new object[] { mode, branch }));

    [Theory]
    [MemberData(nameof(Invalid))]
    public async Task WrongModeTargetsOrCostsDoNotAlterTheSourceResourcesPromptsOrEffectState(string mode, string branch)
    {
        var state = Position(); string[] target = [mode == Return ? "ALLY" : "ENEMY"];
        if (branch == "opposite-controller") target = [mode == Return ? "ENEMY" : "ALLY"];
        if (branch == "equipment") target = ["GEAR"];
        if (branch == "battlefield") target = ["BF"];
        if (branch == "graveyard") target = ["GRAVE"];
        if (branch == "deck") target = ["P1-DRAW-1"];
        if (branch == "missing") target = [];
        if (branch == "extra") target = ["ALLY", "ENEMY"];
        if (branch == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 10 }) } };
        if (branch == "trait") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Red] = 10 }) } };
        await OptionalEntryTargetBoundaryTests.Reject(state, "P1", new PlayCardCommand("SPELL", No, target, Mode: mode));
    }

    [Theory]
    [InlineData("")] [InlineData("DRAW_1")] [InlineData("RETURN_FRIENDLY_UNIT,ENEMY_UNIT_POWER_MINUS_2")]
    public Task OmittedForeignOrCombinedModesAreNotAlternativeWaysToExecuteBothEffects(string mode)
        => OptionalEntryTargetBoundaryTests.Reject(Position(), "P1", new PlayCardCommand("SPELL", No, ["ALLY"], Mode: mode));

    [Theory]
    [InlineData("OGN·010/298", 2)] [InlineData("OGN·271/298", 1)]
    public async Task TheUnflooredEnemyReductionCanLeaveAnUndamagedUnitAtZeroOrNegativeUntilExpiry(string enemyNo, int power)
    {
        var initial = Position(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", enemyNo, "P2") } };
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ENEMY"], Mode: Reduce)); await driver.Top();
        Assert.Equal(power - 2, driver.State.CardObjects["ENEMY"].Power); Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Battlefields);
        await driver.Act("P1", new EndTurnCommand()); Assert.Equal(power, driver.State.CardObjects["ENEMY"].Power); await driver.Replay();
    }

    [Fact]
    public async Task FriendlyReturnUsesCurrentControllerForLegalityAndOriginalOwnerForTheDestination()
    {
        var initial = Position(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { OwnerId = "P2", ControllerId = "P1" } } };
        var driver = new Driver(initial); var generation = initial.CardObjects["ALLY"].ObjectGeneration;
        await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], Mode: Return)); await driver.Top();
        Assert.Contains("ALLY", driver.State.PlayerZones["P2"].Hand); Assert.DoesNotContain("ALLY", driver.State.PlayerZones["P1"].Hand);
        Assert.Equal("P2", driver.State.CardObjects["ALLY"].ControllerId); Assert.True(driver.State.CardObjects["ALLY"].ObjectGeneration > generation);
        await driver.Replay(); Export("owner-hand-return", initial, driver);
    }

    [Fact]
    public async Task EnemyReductionUsesTheControllerEvenWhenTheCasterOwnsTheCard()
    {
        var initial = Position(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { OwnerId = "P1", ControllerId = "P2" } } };
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ENEMY"], Mode: Reduce)); await driver.Top();
        Assert.Equal(2, driver.State.CardObjects["ENEMY"].Power); Assert.Equal("P2", driver.State.CardObjects["ENEMY"].ControllerId);
        Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Battlefields); await driver.Replay();
    }

    [Theory]
    [InlineData(Return)] [InlineData(Reduce)]
    public async Task CounteringEitherModePreventsItsEffectButKeepsItsPrintedCostPaid(string mode)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "COUNTER", "OGN·045/298", "P2");
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("SPELL", No, [mode == Return ? "ALLY" : "ENEMY"], Mode: mode));
        var target = Assert.Single(driver.State.StackItems).StackItemId; await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [target])); await driver.Top();
        Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Base); Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power);
        Assert.Equal(19, driver.State.RunePools["P1"].Mana); Assert.Equal(9, driver.State.RunePools["P1"].TotalPower);
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard); await driver.Replay();
    }

    internal static MatchState Position()
    {
        var state = OptionalEntryTargetConfirmationTests.Position(No); var cards = state.CardObjects.ToDictionary(); cards.Remove("UNIT");
        cards["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", No, "P1");
        var locations = state.ObjectLocations.ToDictionary(); locations.Remove("UNIT"); locations["SPELL"] = new("P1", "HAND");
        return state with { RoomId = "VEN-MODAL-REACTION", CardObjects = cards, ObjectLocations = locations,
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = ["SPELL"] } : p.Value),
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 10 }) } };
    }

    internal static void Export(string name, MatchState initial, Driver driver)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_MODAL_REACTION_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new { initial, final = driver.State,
            commands = driver.Commands(), events = driver.Events() }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
