using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenAkaliMovementDamageTests
{
    private static MatchState Position(string no, bool empowered = false)
    {
        var state = AttackDamageDistributionTests.Position(no);
        return state with { RoomId = "VEN-AKALI-MOVE-" + no,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") with { IsEmpowered = empowered },
                ["ENEMY1"] = state.CardObjects["ENEMY1"] with { Power = 20 } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 4 }) } };
    }

    private static async Task Move(Driver game)
        => await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));

    private static async Task Choose(Driver game, params string[] ids)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }

    [Theory]
    [InlineData("VEN·021", 37707, false, 1)] [InlineData("VEN·021", 37707, true, 2)]
    [InlineData("VEN·021a", 37708, false, 1)] [InlineData("VEN·021a", 37708, true, 2)]
    public async Task MoveMayDamageAUnitAtTheRecordedBattlefieldAfterResponses(string no, long id, bool empowered, int expected)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("游离之刃", card.SubTitle); Assert.Equal(3, card.Energy); Assert.Equal(3, card.Power);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), trigger => trigger.Kind == TriggerKinds.UnitMovedDamageAtOriginOrDestination);
        Assert.Equal(1, spec.DamageAmount); Assert.Equal(2, spec.EmpoweredDamageAmount); Assert.True(spec.Optional);
        Assert.Equal(TriggerTargetScopes.UnitAtMovementEndpointBattlefield, spec.TargetScope);
        var game = new Driver(Position(no, empowered)); await Move(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("HOST", choice.LegalObjectIds); Assert.Contains("ENEMY1", choice.LegalObjectIds);
        Assert.DoesNotContain("GEAR", choice.LegalObjectIds); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        await Choose(game, "ENEMY1"); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        await game.Top(); Assert.Equal(expected, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·021")] [InlineData("VEN·021a")]
    public async Task FriendlyTargetAndDecliningTheOptionalSkillAreBothLegal(string no)
    {
        var game = new Driver(Position(no)); await Move(game); await Choose(game, "HOST"); await game.Top();
        Assert.Equal(1, game.State.CardObjects["HOST"].Damage); await game.Replay();
        game = new Driver(Position(no)); await Move(game); await Choose(game);
        Assert.Empty(game.State.StackItems); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·021")] [InlineData("VEN·021a")]
    public async Task ABaseEquipmentIsAnIllegalTargetAndLeavesTheChoiceIntact(string no)
    {
        var game = new Driver(Position(no)); await Move(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]);
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("invalid-target", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await Choose(game, "ENEMY1"); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·021", true)] [InlineData("VEN·021", false)]
    [InlineData("VEN·021a", true)] [InlineData("VEN·021a", false)]
    public async Task EnemyWardCanBePaidOrTheSkillDeclined(string no, bool pay)
    {
        var state = Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY1"] = state.CardObjects["ENEMY1"] with { Tags = [..state.CardObjects["ENEMY1"].Tags, "法盾2"] } } };
        var game = new Driver(state); await Move(game); var before = game.State.RunePools["P1"].TotalPower;
        await Choose(game, "ENEMY1"); var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(2, payment.PowerCost);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await game.Top();
        Assert.Equal(pay ? before - 2 : before, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(pay ? 1 : 0, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·021")] [InlineData("VEN·021a")]
    public async Task EmpowermentPaysThePrintedCostAndAddsOneMight(string no)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        Assert.Equal(3, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Equal(4, CurrentPowerTargetTests.Snapshot(game.State).GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("HOST").GetProperty("effectivePower").GetInt32());
        await Move(game); await Choose(game, "ENEMY1"); await game.Top();
        Assert.Equal(2, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·021")] [InlineData("VEN·021a")]
    public async Task EmpowermentRejectsMissingRedPowerWithoutChangingState(string no)
    {
        var state = Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 20 }), ["P2"] = new(20, 20) } };
        var command = new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("wrong-power", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task RecoveryRejectsForgedMovementAndTargetGenerations()
    {
        var game = new Driver(Position("VEN·021")); await Move(game); await Choose(game, "ENEMY1");
        var item = Assert.Single(game.State.StackItems); var context = item.MovementObserver!;
        foreach (var changed in new[] {
            item with { MovementObserver = context with { SourceGeneration = context.SourceGeneration + 1 } },
            item with { MovementObserver = context with { TargetTokenFactoryCardNo = "FORGED" } },
            item with { TargetGenerations = new Dictionary<string, long> { ["ENEMY1"] = -1 } },
            item with { TargetGenerations = new Dictionary<string, long> { ["ENEMY1"] = game.State.CardObjects["ENEMY1"].ObjectGeneration + 1 } } })
        {
            var forged = game.State with { StackItems = game.State.StackItems.Select(current => current == item ? changed : current).ToArray() };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        }
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·021")] [InlineData("VEN·021a")]
    public async Task ActualChampionPlayPaysThreeMana(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 0) } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Equal(0, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·021")] [InlineData("VEN·021a")]
    public async Task LeavingABattlefieldCanTargetAFriendlyUnitAtTheRecordedOrigin(string no)
    {
        var state = ArmamentMightTests.Position(unit: no);
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        state = OfficialHoldSequenceTests.AddUnit(state, "ALLY", "OGN·001/298", "F", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") } };
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("HOST", "BATTLEFIELD", "BASE", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("ALLY", choice.LegalObjectIds); Assert.DoesNotContain("HOST", choice.LegalObjectIds);
        await Choose(game, "ALLY"); await game.Top(); Assert.Equal(1, game.State.CardObjects["ALLY"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·021", "ALLY")] [InlineData("VEN·021", "ENEMY")]
    [InlineData("VEN·021a", "ALLY")] [InlineData("VEN·021a", "ENEMY")]
    public async Task EffectMovementBetweenBattlefieldsAllowsEitherRecordedEndpoint(string no, string target)
    {
        var state = ArmamentMightTests.Position(unit: no);
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        state = OfficialHoldSequenceTests.AddUnit(state, "ALLY", "OGN·001/298", "F", "P1");
        state = OfficialHoldSequenceTests.AddUnit(state, "ENEMY", "OGN·001/298", "OTHER", "P2");
        state = AttackDamageDistributionTests.Spell(state, "WIND", "OGN·173/298", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Purple] = 2 }) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("WIND", "OGN·173/298", ["HOST"], Destination: "BATTLEFIELD:OTHER"));
        await game.Top(); var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("ALLY", choice.LegalObjectIds); Assert.Contains("ENEMY", choice.LegalObjectIds);
        await Choose(game, target); await game.Top(); Assert.Equal(1, game.State.CardObjects[target].Damage); await game.Replay();
    }

    [Fact]
    public async Task LeavingAnEmptyBattlefieldDiscardsTheOptionalSkillWithoutPausingTheGame()
    {
        var state = ArmamentMightTests.Position(unit: "VEN·021");
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "VEN·021", "P1") } };
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("HOST", "BATTLEFIELD", "BASE", []));
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Contains(game.Events(), e => e.Event.Kind == "TRIGGER_NOT_CONFIRMED"); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·021", false, 2)] [InlineData("VEN·021", true, 1)]
    [InlineData("VEN·021a", false, 2)] [InlineData("VEN·021a", true, 1)]
    public async Task EmpowermentChangesInResponseDetermineTheDamage(string no, bool initiallyEmpowered, int expected)
    {
        var state = AttackDamageDistributionTests.Spell(Position(no, initiallyEmpowered), "STATUS", "VEN·035", "P1");
        var game = new Driver(state); await Move(game); await Choose(game, "ENEMY1");
        await game.Act("P1", new PlayCardCommand("STATUS", "VEN·035", ["HOST"],
            Mode: initiallyEmpowered ? "REMOVE_THEN_EMPOWER" : "EMPOWER_THEN_REMOVE")); await game.Top();
        Assert.Equal(!initiallyEmpowered, game.State.CardObjects["HOST"].IsEmpowered);
        await game.Top(); Assert.Equal(expected, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·021")] [InlineData("VEN·021a")]
    public async Task CopiedRuleTextCreatesTwoIndependentlyConfirmedMovementSkills(string no)
    {
        var state = AttackDamageDistributionTests.Position(no, copied: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1"),
            ["ENEMY1"] = state.CardObjects["ENEMY1"] with { Power = 20 } } };
        var game = new Driver(state); await Move(game);
        var triggers = game.State.TriggerQueue.Where(trigger => trigger.MovementObserver?.Kind == TriggerKinds.UnitMovedDamageAtOriginOrDestination)
            .Select(trigger => trigger.TriggerId).ToArray();
        Assert.Equal(2, triggers.Length);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: triggers));
        await Choose(game, "ENEMY1"); await Choose(game, "ENEMY2");
        await game.Top(); await game.Top();
        Assert.Equal(1, game.State.CardObjects["ENEMY1"].Damage); Assert.Equal(1, game.State.CardObjects["ENEMY2"].Damage);
        await game.Replay();
    }

    [Fact]
    public async Task SourceLeavingTheFieldDoesNotChangeTheRecordedBattlefieldOrRevealItsHandIdentity()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·021", empowered: true), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await Move(game); await Choose(game, "ENEMY1");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        var opponent = ResolutionResult.BuildSnapshots(game.State)["P2"];
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(opponent.Stack));
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(spectator.SpectatorSnapshot.Stack));
        await game.Top(); Assert.Equal(1, game.State.CardObjects["ENEMY1"].Damage);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.Replay();
    }

    [Fact]
    public async Task TargetTokenThatLeavesTheFieldBeforeResolutionNoLongerExists()
    {
        var state = Position("VEN·021");
        var token = Assert.Single(P6TokenFactoryCatalog.GetAll(), definition => definition.CardNo == P6TokenFactoryCatalog.WarhawkTokenCardNo)
            .CreateObject("ENEMY1", "P2", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["ENEMY1"] = token } };
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P2");
        var game = new Driver(state); await Move(game); await Choose(game, "ENEMY1");
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY1"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("ENEMY1")); await game.Top();
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY1"));
        await game.Replay();
    }

    [Fact]
    public async Task EnemyMovedToBaseInResponseIsNoLongerAValidTarget()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·021"), "FLASH", "OGS·011/024", "P2");
        var game = new Driver(state); await Move(game); await Choose(game, "ENEMY1");
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("FLASH", "OGS·011/024", ["ENEMY1"])); await game.Top();
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Base); await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }
}
