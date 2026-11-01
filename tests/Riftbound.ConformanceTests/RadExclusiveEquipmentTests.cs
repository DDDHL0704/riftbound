using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadExclusiveEquipmentTests
{
    private const string Ball = "RAD·146/167";
    private const string Standard = "RAD·156/167";

    [Theory]
    [InlineData(Ball, 39654, 5)] [InlineData(Standard, 39664, 2)]
    public async Task ExactPrintingAndDeployPermissionIncludeEnemyAndOpenBattlefields(string no, long id, int mana)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        var state = Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = null } } };
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.Equal(new[] { "BATTLEFIELD:BF", "BATTLEFIELD:OTHER" },
            CoreRuleEngine.UnitPlayDestinationChoices(state, "P1", behavior).Select(c => c.Id).Order().ToArray());
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:OTHER"));
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "OTHER"), game.State.ObjectLocations["UNIT"]);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        if (no == Ball) { await Choose(game, "DRAW"); await game.Top(); }
        await game.Replay();
    }

    [Theory]
    [InlineData(Ball, "BASE")] [InlineData(Standard, "BASE")]
    [InlineData(Ball, "BATTLEFIELD:missing")] [InlineData(Standard, "BATTLEFIELD:missing")]
    public async Task InvalidDeploymentCannotSpendResourcesOrChangeState(string no, string destination)
        => await Reject(Position(no), new PlayCardCommand("UNIT", no, [], Destination: destination));

    [Theory]
    [InlineData("DRAW")] [InlineData("DAMAGE")]
    public async Task BallChoosesOneModeAfterEntryAndResolvesAfterResponses(string mode)
    {
        var state = LocalDestructionRecallTests.At(Position(Ball), "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = OfficialHoldSequenceTests.AddUnit(state, "SECOND", "OGN·001/298", "BF", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECOND"] = state.CardObjects["SECOND"] with { Power = 5 },
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Power = 5, Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Ball, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal("UNIT_EVENT_MODE_CONFIRMATION", game.State.PendingCardChoice!.ChoiceWindow);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        var choice = game.State.PendingCardChoice!;
        await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DRAW", "DAMAGE"]));
        await Choose(game, mode);
        Assert.NotNull(game.State.PriorityPlayerId); Assert.Null(game.State.PendingPayment);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Top();
        Assert.Equal(mode == "DAMAGE" ? 2 : 0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(mode == "DAMAGE" ? 2 : 0, game.State.CardObjects["SECOND"].Damage);
        Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
        Assert.Equal(mode == "DRAW" ? new[] { "P1-DRAW-1" } : [], game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task MovingTheBallAsAUnitCapturesOneModeTriggerAtItsDestination()
    {
        var state = LocalDestructionRecallTests.At(Position(Ball), "UNIT", "P1", "BATTLEFIELD", "BF");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { Power = 5, Tags = [..state.CardObjects["UNIT"].Tags, CardObjectTags.UnitCard, CardEquipmentKeywordNames.Deploy, "游走"] } } };
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]));
        Assert.Equal("OTHER", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.Single(game.State.StackItems); Assert.Equal("OTHER", game.State.StackItems[0].UnitEvent!.Here!.BattlefieldObjectId);
        await Choose(game, "DRAW"); await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task StandardTriggersOnceForASimultaneousAttackAndBuffsOneOfThatBatch()
    {
        var state = AttackDamageDistributionTests.Position("OGN·001/298");
        state = OfficialHoldSequenceTests.AddUnit(state, "SECOND", "OGN·001/298", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        state = PutEquipment(state, "STANDARD", Standard, "P1", "F");
        state = state with { UntilEndOfTurnEffects = [..state.UntilEndOfTurnEffects, BattlefieldTaskMarkers.SpellDuelCompleted("F")] };
        var game = new Driver(state);
        await game.Act("P1", new DeclareBattleCommand("F", ["HOST", "SECOND"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        var item = Assert.Single(game.State.StackItems, i => i.SourceObjectId == "STANDARD");
        Assert.Equal(2, item.UnitEvent!.Attackers!.Count);
        Assert.Equal(new[] { "HOST", "SECOND" }, game.State.PendingCardChoice!.LegalObjectIds.Order().ToArray());
        var old = game.State.CardObjects["SECOND"].Power;
        await Choose(game, "SECOND"); Assert.Equal(old, game.State.CardObjects["SECOND"].Power);
        await game.Top(); Assert.Equal(old + 2, game.State.CardObjects["SECOND"].Power);
        Assert.Equal(9, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Fact]
    public async Task StandardAlsoObservesAttackRoleAcquiredByMovement()
    {
        var state = PutEquipment(AttackDamageDistributionTests.Position("OGN·001/298"), "STANDARD", Standard, "P1", "F");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Equal(new[] { "HOST" }, game.State.PendingCardChoice!.LegalObjectIds);
        await Choose(game, "HOST"); await game.Top();
        Assert.Equal(11, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Fact]
    public async Task ALaterAttackerTriggersANewBatchWithoutOfferingEarlierAttackers()
    {
        var state = PutEquipment(AttackDamageDistributionTests.Position("OGN·001/298"), "STANDARD", Standard, "P1", "F");
        state = MightyExperienceFrameTests.HandUnit(state, "AMBUSH", "UNL-002/219");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        await Choose(game, "HOST"); await game.Top();
        await game.Act("P1", new PlayCardCommand("AMBUSH", "UNL-002/219", [], Destination: "BATTLEFIELD:F"));
        Assert.Equal(new[] { "AMBUSH" }, game.State.PendingCardChoice!.LegalObjectIds);
        var choice = game.State.PendingCardChoice!;
        await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        var power = game.State.CardObjects["AMBUSH"].Power;
        await Choose(game, "AMBUSH"); await game.Top();
        Assert.Equal(power + 2, game.State.CardObjects["AMBUSH"].Power);
        Assert.Equal(11, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HoldingItsBattlefieldOffersToReturnTheStandardToItsOwner(bool accept)
    {
        var state = PutEquipment(OfficialHoldSequenceTests.State("OGN·278/298"), "STANDARD", Standard, "P1", "F");
        var game = new Driver(state);
        await game.Act("P1", new PassPriorityCommand());
        Assert.Single(game.State.StackItems, i => i.SourceObjectId == "STANDARD");
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("HOLD_RETURN", choice.Request.Kind);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [accept ? "RETURN" : "KEEP"]));
        Assert.Equal(accept, game.State.PlayerZones["P1"].Hand.Contains("STANDARD"));
        Assert.Equal(!accept, game.State.PlayerZones["P1"].Battlefields.Contains("STANDARD"));
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("generation")]
    public async Task RecoveryRejectsInvalidAttackBatchBindings(string mutation)
    {
        var state = PutEquipment(AttackDamageDistributionTests.Position("OGN·001/298"), "STANDARD", Standard, "P1", "F");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var item = Assert.Single(game.State.StackItems);
        var attacker = item.UnitEvent!.Attackers!.Single();
        var changed = item.UnitEvent with { Attackers = mutation switch {
            "missing" => null,
            "duplicate" => [attacker, attacker],
            _ => [attacker with { Binding = attacker.Binding with { Generation = attacker.Binding.Generation + 1 } }]
        } };
        var forged = game.State with { StackItems = [item with { UnitEvent = changed }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        await Choose(game, "HOST"); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("missing-location")] [InlineData("other-location")] [InlineData("mode")]
    public async Task RecoveryRejectsInvalidBallLocationAndMode(string mutation)
    {
        var game = new Driver(Position(Ball));
        await game.Act("P1", new PlayCardCommand("UNIT", Ball, [], Destination: "BATTLEFIELD:BF"));
        var item = Assert.Single(game.State.StackItems);
        var changed = mutation switch {
            "missing-location" => item.UnitEvent! with { Here = null },
            "other-location" => item.UnitEvent! with { Here = new("P1", "BATTLEFIELD", "OTHER") },
            _ => item.UnitEvent! with { Mode = "INVALID" }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { UnitEvent = changed }] }));
        await Choose(game, "DRAW"); await game.Top(); await game.Replay();
    }

    private static MatchState Position(string no) => VenUnitEntryBasicTests.Position(no);

    private static MatchState PutEquipment(MatchState state, string id, string no, string owner, string field)
    {
        var zones = state.PlayerZones.ToDictionary(); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
        cards[id] = cards[id] with { Tags = [..cards[id].Tags, CardEquipmentKeywordNames.Deploy] };
        zones[owner] = zones[owner] with { Battlefields = [..zones[owner].Battlefields, id] };
        locations[id] = new(owner, "BATTLEFIELD", field);
        return state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
    }

    private static async Task Choose(Driver game, params string[] ids)
    {
        var c = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(c.PlayerId, new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, ids));
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("rejected", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
