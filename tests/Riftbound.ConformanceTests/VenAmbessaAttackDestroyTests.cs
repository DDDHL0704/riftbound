using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenAmbessaAttackDestroyTests
{
    private static MatchState Position(string no, bool empowered = true, int firstEnemy = 4, bool copied = false)
    {
        var state = AttackDamageDistributionTests.Position(no, copied);
        return state with { RoomId = "VEN-AMBESSA-ATTACK-" + no,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") with { IsEmpowered = empowered },
                ["ENEMY1"] = state.CardObjects["ENEMY1"] with { Power = firstEnemy },
                ["ENEMY2"] = state.CardObjects["ENEMY2"] with { Power = 7 },
                ["ENEMY3"] = state.CardObjects["ENEMY3"] with { Power = 8 } },
            RunePools = new Dictionary<string, RunePool> {
                ["P1"] = new(20, 10, new Dictionary<string, int> { [RuneTrait.Yellow] = 4 }),
                ["P2"] = new(20, 10, new Dictionary<string, int> { [RuneTrait.Yellow] = 4 }) } };
    }

    private static async Task Attack(Driver game) => await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
    private static async Task Choose(Driver game, string id)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
    }

    [Theory]
    [InlineData("VEN·136", 37837)] [InlineData("VEN·136a", 37838)] [InlineData("VEN·187", 37890)]
    public async Task EmpoweredAttackConfirmsAWeakerEnemyAndDestroysAfterResponses(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("战狼尊母", card.SubTitle); Assert.Equal(5, card.Energy); Assert.Equal(5, card.Power);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitEmpoweredAttackDestroyWeakerEnemyHere);
        Assert.True(spec.RequiresSourceEmpowered);
        var empower = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(1, empower.Mana); Assert.Equal(2, empower.TypedPower[RuneTrait.Yellow]);
        Assert.Contains("强攻2", EmpowerParser.Self(card.CardEffect).Keywords);
        var game = new Driver(Position(no)); await Attack(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(["ENEMY1"], choice.LegalObjectIds);
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Battlefields);
        await Choose(game, "ENEMY1");
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Battlefields);
        await game.Top();
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY1")
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "SKILL_EFFECT"));
        Assert.Contains("ENEMY2", game.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("ENEMY3", game.State.PlayerZones["P2"].Battlefields);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·136")] [InlineData("VEN·136a")] [InlineData("VEN·187")]
    public async Task UnempoweredAttackDoesNotTrigger(string no)
    {
        var game = new Driver(Position(no, empowered: false)); await Attack(game);
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.State.StackItems, i => i.FieldContext?.Kind == "EMPOWERED_ATTACK_DESTROY_WEAKER_HERE");
        Assert.DoesNotContain(game.State.TriggerQueue, i => i.FieldContext?.Kind == "EMPOWERED_ATTACK_DESTROY_WEAKER_HERE");
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·136")] [InlineData("VEN·136a")] [InlineData("VEN·187")]
    public async Task NoWeakerEnemyLeavesNoTargetChoice(string no)
    {
        var game = new Driver(Position(no, firstEnemy: 7)); await Attack(game);
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.State.StackItems, i => i.FieldContext?.Kind == "EMPOWERED_ATTACK_DESTROY_WEAKER_HERE");
        Assert.Equal("P1", game.State.FocusPlayerId); Assert.Null(game.State.DuelChainFocus);
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Battlefields); await game.Replay();
    }

    [Fact]
    public async Task EmpoweringAfterAttackerRoleDoesNotRetroactivelyTrigger()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·136", empowered: false), "BOOST", "VEN·035", "P1");
        var game = new Driver(state); await Attack(game);
        await game.Act("P1", new PlayCardCommand("BOOST", "VEN·035", ["HOST"], Mode: "EMPOWER_THEN_REMOVE")); await game.Top();
        Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.State.StackItems, i => i.FieldContext?.Kind == "EMPOWERED_ATTACK_DESTROY_WEAKER_HERE");
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·136")] [InlineData("VEN·136a")] [InlineData("VEN·187")]
    public async Task CanPayPrintedEmpowerCostBeforeAttacking(string no)
    {
        var game = new Driver(Position(no, empowered: false));
        await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        Assert.Equal(19, game.State.RunePools["P1"].Mana);
        Assert.Equal(2, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Yellow));
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered); await game.Top();
        Assert.True(game.State.CardObjects["HOST"].IsEmpowered); await Attack(game);
        await Choose(game, "ENEMY1"); await game.Top(); Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·136")] [InlineData("VEN·136a")] [InlineData("VEN·187")]
    public async Task HeroCanBePlayedFromChampionZoneOnlyAfterPayingPrintedCost(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(5, 0) } };
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        var unpaid = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(4, 0) } };
        var rejected = await new CoreRuleEngine().ResolveAsync(unpaid, new("unpaid", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(unpaid), MatchStateHasher.Hash(rejected.State));
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Empty(game.State.PlayerZones["P1"].ChampionZone); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(0, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·136")] [InlineData("VEN·136a")] [InlineData("VEN·187")]
    public async Task EmpowerRequiresTwoYellowPowerWithoutPartialPayment(string no)
    {
        var state = Position(no, empowered: false);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1, [RuneTrait.Green] = 4 }) } };
        var command = new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("wrong-color", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("VEN·136")] [InlineData("VEN·136a")] [InlineData("VEN·187")]
    public async Task EqualOrStrongerTargetsAreRejectedAtomically(string no)
    {
        var game = new Driver(Position(no)); await Attack(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY2"]);
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("invalid", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await Choose(game, "ENEMY1"); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(4, true)] [InlineData(6, false)]
    public async Task LosingEmpowermentAfterConfirmationRechecksCurrentMight(int enemyPower, bool destroyed)
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·136", firstEnemy: enemyPower), "REVERSE", "VEN·035", "P1");
        var game = new Driver(state); await Attack(game); await Choose(game, "ENEMY1");
        await game.Act("P1", new PlayCardCommand("REVERSE", "VEN·035", ["HOST"], Mode: "REMOVE_THEN_EMPOWER")); await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered); await game.Top();
        Assert.Equal(destroyed, game.State.PlayerZones["P2"].Graveyard.Contains("ENEMY1"));
        Assert.Equal(!destroyed, game.State.PlayerZones["P2"].Battlefields.Contains("ENEMY1")); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task WardIsPaidAfterTargetChoiceOrTheSkillIsDeclined(bool pay)
    {
        var state = Position("VEN·136");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY1"] = state.CardObjects["ENEMY1"] with { Tags = [..state.CardObjects["ENEMY1"].Tags, "法盾2"] } } };
        var game = new Driver(state); await Attack(game); var before = game.State.RunePools["P1"].TotalPower;
        await Choose(game, "ENEMY1");
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(2, payment.PowerCost); Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Battlefields);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await game.Top();
        Assert.Equal(pay ? before - 2 : before, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(pay, game.State.PlayerZones["P2"].Graveyard.Contains("ENEMY1")); await game.Replay();
    }

    [Fact]
    public async Task EnemyMayCounterTheConfirmedTargetedAbility()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·136"), "COUNTER", "SFD·045/221", "P2");
        var game = new Driver(state); await Attack(game); await Choose(game, "ENEMY1");
        var ability = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "SFD·045/221", [ability.StackItemId])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Battlefields);
        await game.Replay();
    }

    [Fact]
    public async Task CopiedRuleTextProducesTwoIndependentlyConfirmedAttackSkills()
    {
        var state = Position("VEN·136", copied: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY2"] = state.CardObjects["ENEMY2"] with { Power = 4 } } };
        var game = new Driver(state); await Attack(game);
        var ids = game.State.TriggerQueue.Where(t => t.FieldContext?.Kind == "EMPOWERED_ATTACK_DESTROY_WEAKER_HERE")
            .Select(t => t.TriggerId).ToArray();
        Assert.Equal(2, ids.Length);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await Choose(game, "ENEMY1"); await Choose(game, "ENEMY2");
        Assert.Equal(2, game.State.StackItems.Count(i => i.FieldContext?.Kind == "EMPOWERED_ATTACK_DESTROY_WEAKER_HERE"));
        await game.Top(); await game.Top();
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("ENEMY2", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsForgedAttackConditionAndLocation()
    {
        var game = new Driver(Position("VEN·136")); await Attack(game);
        var item = Assert.Single(game.State.StackItems, i => i.FieldContext?.Kind == "EMPOWERED_ATTACK_DESTROY_WEAKER_HERE");
        var context = item.FieldContext!; var frame = context.AttackConditionFrame!;
        foreach (var forged in new[] {
            context with { AttackConditionFrame = null },
            context with { AttackConditionFrame = frame with { CardObjects = new Dictionary<string, CardObjectState>(frame.CardObjects) {
                ["HOST"] = frame.CardObjects["HOST"] with { IsEmpowered = false } } } },
            context with { BattlefieldId = "FORGED" } })
        {
            var altered = game.State with { StackItems = game.State.StackItems.Select(i => i == item ? i with { FieldContext = forged } : i).ToArray() };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(altered));
        }
        await Choose(game, "ENEMY1"); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task ReturningSourceBeforeResolutionRemovesHereAndKeepsItsNewHandIdentityPrivate()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·136"), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await Attack(game); await Choose(game, "ENEMY1");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        var opponent = ResolutionResult.BuildSnapshots(game.State)["P2"];
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(opponent.Stack));
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(spectator.SpectatorSnapshot.Stack));
        await game.Top(); Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_RESOLVED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.Replay();
    }
}
