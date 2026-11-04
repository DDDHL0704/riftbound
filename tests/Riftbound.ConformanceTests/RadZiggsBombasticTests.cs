using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadZiggsBombasticTests
{
    private const string Ability = "RAD_112_EXHAUST_PLAY_BOMB";

    private static MatchState Ready(string no = "RAD·112/167")
    {
        var s = LocalDestructionRecallTests.At(VenUnitEntryBasicTests.Position(no), "UNIT", "P1", "BASE");
        return s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with { Hand = [] } },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["UNIT"] = s.CardObjects["UNIT"] with { IsExhausted = false } } };
    }

    [Theory]
    [InlineData("RAD·112/167", 39615L)] [InlineData("RAD·112a/167", 39616L)]
    public async Task BothPrintingsPayFourAndTheirActivationPaysOneAndExhaustsBeforePlayingABomb(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("狂轰滥炸", card.SubTitle);
        var play = new Driver(VenUnitEntryBasicTests.Position(no));
        await play.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(16, play.State.RunePools["P1"].Mana);
        Assert.Equal(20, play.State.RunePools["P1"].TotalPower);
        Assert.True(play.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(4, play.State.CardObjects["UNIT"].Power);
        var game = new Driver(Ready(no));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", Ability, ["OTHER"]));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(19, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        await game.Top();
        var bomb = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "OTHER"), game.State.ObjectLocations[bomb.ObjectId]);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, bomb.Tags); Assert.False(bomb.IsExhausted);
        await game.Replay(); await play.Replay();
    }

    [Theory]
    [InlineData("mana")] [InlineData("exhausted")] [InlineData("target")] [InlineData("controller")] [InlineData("wrong-turn")]
    public async Task IllegalActivationDoesNotSpendResourcesOrChangeTheBoard(string branch)
    {
        var s = Ready();
        if (branch == "mana") s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = RunePool.Empty } };
        if (branch == "exhausted") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["UNIT"] = s.CardObjects["UNIT"] with { IsExhausted = true } } };
        if (branch == "controller") s = LocalDestructionRecallTests.At(s, "UNIT", "P2", "BASE");
        if (branch == "wrong-turn") s = s with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var command = new ActivateAbilityCommand("UNIT", Ability, [branch == "target" ? "ALLY" : "BF"]);
        var hash = MatchStateHasher.Hash(s);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("invalid-ziggs", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task SpellOnlyCounterCannotChooseTheBombSkill()
    {
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(Ready(), "COUNTER", "OGN·064/298"));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", Ability, ["BF"]));
        var skill = Assert.Single(game.State.StackItems);
        var hash = MatchStateHasher.Hash(game.State);
        var counter = new PlayCardCommand("COUNTER", "OGN·064/298", [skill.StackItemId]);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("invalid-counter", "P1", counter.CmdType), counter, default);
        Assert.False(rejected.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        await game.Top(); Assert.Empty(game.State.StackItems); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        await game.Replay();
    }

    private static MatchState Defense(string no, bool gear = true)
    {
        var s = AhriCombatMightTests.Board(no, defending: true);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY1"] = s.CardObjects["ENEMY1"] with { Power = 4 } } };
        // The equipment can be in the base, away from the combat battlefield.
        if (gear) s = LocalDestructionRecallTests.At(s, "GEAR", "P2", "BASE");
        return s;
    }

    [Theory]
    [InlineData("RAD·112/167", false)] [InlineData("RAD·112/167", true)]
    [InlineData("RAD·112a/167", false)] [InlineData("RAD·112a/167", true)]
    public async Task DefensePaysOptionalEquipmentCostBeforeResponsesAndRewardsOnlyOnResolution(string no, bool destroy)
    {
        var game = new Driver(Defense(no));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Equal("DEFEND", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        var item = game.State.StackItems.Single();
        Assert.Equal(game.State.Tick, item.UnitEvent!.CapturedTick);
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with {
            StackItems = [item with { UnitEvent = item.UnitEvent with { CapturedTick = game.State.Tick + 1 } }] }));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Base); Assert.Equal(4, game.State.CardObjects["ENEMY1"].Power);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(CoreRuleEngine.TriggerCostWindow, choice.ChoiceWindow);
        Assert.Equal("P2", choice.PlayerId);
        Assert.Equal(new[] { "GEAR" }, choice.LegalObjectIds);
        Assert.Null(game.State.PriorityPlayerId);
        await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            destroy ? ["GEAR"] : []));
        Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(destroy, game.State.StackItems.SingleOrDefault()?.TriggerCost?.DestroyedPermanents is not null);
        Assert.Equal(destroy, game.State.PlayerZones["P1"].Graveyard.Contains("GEAR"));
        Assert.Equal(4, game.State.CardObjects["ENEMY1"].Power);
        Assert.Equal(destroy ? "P2" : null, game.State.PriorityPlayerId);
        if (destroy) await game.Top();
        Assert.Equal(destroy ? 6 : 4, game.State.CardObjects["ENEMY1"].Power);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task LeadingEquipmentCostRejectsFriendlyUnitsAndOpponentsEquipmentWithoutChangingState()
    {
        var initial = Defense("RAD·112/167");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["P2-UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-UNIT", "SFD·125/221", "P2"),
            ["P1-GEAR"] = initial.CardObjects["GEAR"] with { ObjectId = "P1-GEAR", OwnerId = "P1", ControllerId = "P1" } };
        var zones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "P1-GEAR"] },
            ["P2"] = initial.PlayerZones["P2"] with { Base = [..initial.PlayerZones["P2"].Base, "P2-UNIT"] } };
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
            ["P2-UNIT"] = new("P2", "BASE"), ["P1-GEAR"] = new("P1", "BASE") };
        initial = initial with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { "GEAR" }, choice.LegalObjectIds);
        var hash = MatchStateHasher.Hash(game.State);
        var invalid = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2-UNIT"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
            new("invalid-ziggs-equipment-cost", "P2", invalid.CmdType), invalid, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.Empty(rejected.Events);
        await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        Assert.Equal("P2", game.State.PriorityPlayerId);
        Assert.Equal(4, game.State.CardObjects["ENEMY1"].Power);
        Assert.Empty(game.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        var item = Assert.Single(game.State.StackItems);
        var forgedReceipt = item.TriggerCost! with { DestroyedPermanents = [new("P2-UNIT", 0)] };
        var forged = game.State with { StackItems = [item with { TriggerCost = forgedReceipt }] };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(forged));
        await game.Top();
        Assert.Equal(6, game.State.CardObjects["ENEMY1"].Power);
        Assert.Single(game.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        await game.Replay();
    }

    [Fact]
    public async Task NoFriendlyEquipmentMakesDefenseResolveWithoutAChoiceAndAttackDoesNotTrigger()
    {
        var defend = new Driver(Defense("RAD·112/167", gear: false));
        await defend.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Null(defend.State.PendingRuleChoice); Assert.Equal(4, defend.State.CardObjects["ENEMY1"].Power);
        await defend.Replay();
        var attack = new Driver(AhriCombatMightTests.Board("RAD·112/167"));
        await attack.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.DoesNotContain(attack.State.StackItems, i => i.UnitEvent?.Timing == "DEFEND"); await attack.Replay();
    }
}
