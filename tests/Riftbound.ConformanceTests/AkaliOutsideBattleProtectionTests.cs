using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AkaliOutsideBattleProtectionTests
{
    [Theory]
    [InlineData("VEN·038", 37726)]
    [InlineData("VEN·038a", 37727)]
    public async Task OfficialPrintsAreAdmittedAndBothPrintedRulesAreParsed(string cardNo, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == cardNo);
        Assert.Equal(id, card.Id);
        Assert.Equal("无形致命", card.SubTitle);
        var specs = RuleTextParser.Parse(card);
        var movement = Assert.Single(specs.Triggers, t => t.Kind == TriggerKinds.UnitMovedPowerModifier);
        Assert.Equal("BATTLEFIELD", movement.MovementDestinationZone);
        Assert.Equal(2, movement.PowerDelta);
        var protection = Assert.Single(specs.StaticAbilities, a => a.Kind == StaticAbilityKinds.SourceUnitEnemySpellSkillTargetProtection);
        Assert.True(protection.RequiresSourceOutsideBattle);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior));
        Assert.Equal(4, behavior.ManaCost);
        Assert.Equal(4, behavior.SourceUnitPower);
    }

    [Theory]
    [InlineData("VEN·038")]
    [InlineData("VEN·038a")]
    public async Task EnemyCannotTargetOutsideCombatAndRejectedCommandDoesNotChangeState(string cardNo)
    {
        var initial = BattlePosition(cardNo);
        var command = new PlayCardCommand("BURN", "OGS·003/024", ["D"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("bad-target", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("VEN·038")]
    [InlineData("VEN·038a")]
    public async Task EnemyCanTargetDuringBattleButResponseMovingHerOutInvalidatesTheTarget(string cardNo)
    {
        var initial = BattlePosition(cardNo);
        var entered = await Act(initial, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(entered.State.BattleState.IsActive);
        Assert.True(entered.State.CardObjects["D"].IsDefending);
        var burn = await Act(entered.State, "P1", new PlayCardCommand("BURN", "OGS·003/024", ["D"]));
        var passed = await Act(burn.State, "P1", new PassPriorityCommand());
        var flash = await Act(passed.State, "P2", new PlayCardCommand("FLASH", "OGS·011/024", ["D"]));
        var moved = await Top(flash.State);
        Assert.Equal("BASE", moved.State.ObjectLocations["D"].Zone);
        Assert.False(moved.State.CardObjects["D"].IsDefending);
        var resolved = await Top(moved.State);
        Assert.Equal(0, resolved.State.CardObjects["D"].Damage);
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "D"));
        Restore(resolved.State);
    }

    [Theory]
    [InlineData("VEN·038")]
    [InlineData("VEN·038a")]
    public async Task MovingToBattlefieldQueuesPowerButMovingToBaseDoesNot(string cardNo)
    {
        var initial = MovementThresholdTests.Position(cardNo);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["A"] = initial.CardObjects["A"] with { Power = 4 } } };
        var moved = await MovementThresholdTests.Move(initial, 1);
        var trigger = Assert.Single(moved.State.StackItems);
        Assert.Equal(TriggerKinds.UnitMovedPowerModifier, trigger.MovementObserver!.Kind);
        Assert.Equal(4, moved.State.CardObjects["A"].Power);
        var resolved = await Top(moved.State);
        Assert.Equal(6, resolved.State.CardObjects["A"].Power);
        Restore(resolved.State);
        var returned = await MovementThresholdTests.Move(resolved.State, 2);
        Assert.DoesNotContain(returned.State.StackItems, item => item.MovementObserver?.CardNo == cardNo);
        Assert.Equal(6, returned.State.CardObjects["A"].Power);
        Restore(returned.State);
    }

    [Theory]
    [InlineData("VEN·038")]
    [InlineData("VEN·038a")]
    public async Task OwnerMayTargetHerOutsideCombatAndEnemyUntargetedDamageStillApplies(string cardNo)
    {
        var initial = BattlePosition(cardNo);
        var ownTurn = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var flash = await Act(ownTurn, "P2", new PlayCardCommand("FLASH", "OGS·011/024", ["D"]));
        var moved = await Top(flash.State);
        Assert.Equal("BASE", moved.State.ObjectLocations["D"].Zone);
        Restore(moved.State);

        var area = AttackDamageDistributionTests.Spell(initial, "AREA", "OGS·002/024", "P1");
        var played = await Act(area, "P1", new PlayCardCommand("AREA", "OGS·002/024", ["BF"]));
        var damaged = await Top(played.State);
        Assert.Equal(3, damaged.State.CardObjects["D"].Damage);
        Restore(damaged.State);
    }

    [Theory]
    [InlineData("VEN·038")]
    [InlineData("VEN·038a")]
    public async Task HeroCanBePlayedFromChampionZoneWithItsPrintedCost(string cardNo)
    {
        var initial = VenUnitEntryBasicTests.Position(cardNo);
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new RunePool(4, 0) } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        Assert.Empty(game.State.PlayerZones["P1"].ChampionZone);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Theory]
    [InlineData("HERO")]
    [InlineData("ALT")]
    public async Task ChosenChampionReturnMatchesEitherPhysicalPrinting(string target)
    {
        var state = OfficialChosenChampionReturnTests.Position();
        state = state with {
            PlayerDecklists = new Dictionary<string, OfficialDecklist>(state.PlayerDecklists) {
                ["P1"] = state.PlayerDecklists["P1"] with { ChampionCardNo = "VEN·038" } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HERO"] = state.CardObjects["HERO"] with { CardNo = "VEN·038" },
                ["ALT"] = state.CardObjects["ALT"] with { CardNo = "VEN·038a" } } };
        var opened = await Act(state, "P1", new PassPriorityCommand());
        Assert.Equal(new[] { "ALT", "HERO" }, opened.State.PendingCardChoice!.LegalObjectIds);
        var confirmed = await OfficialHeldConfirmationTests.Choose(opened.State, target);
        var returned = await Top(confirmed.State);
        Assert.Equal(new[] { target }, returned.State.PlayerZones["P1"].ChampionZone);
        ArmamentMightTests.PublicRecovery(returned.State);
    }

    [Fact]
    public async Task BattleResponseSurvivesRecoveryAndFullCommandReplay()
    {
        var game = new VenBasicFlowSpellTests.Driver(BattlePosition("VEN·038"));
        await game.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PlayCardCommand("BURN", "OGS·003/024", ["D"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("FLASH", "OGS·011/024", ["D"]));
        await game.Top();
        await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["D"].Zone);
        Assert.Equal(0, game.State.CardObjects["D"].Damage);
        await game.Replay();
    }

    private static MatchState BattlePosition(string cardNo)
    {
        var state = DeathAndDuelLifecycleTests.State();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = state.CardObjects["D"] with { CardNo = cardNo, Power = 4 } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20), ["P2"] = new(20, 20) } };
        state = AttackDamageDistributionTests.Spell(state, "BURN", "OGS·003/024", "P1");
        return AttackDamageDistributionTests.Spell(state, "FLASH", "OGS·011/024", "P2");
    }
}
