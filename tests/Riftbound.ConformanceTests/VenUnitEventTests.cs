using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenUnitEventTests
{
    [Theory]
    [InlineData("VEN·009", 37694, 3, 4, 1)]
    [InlineData("VEN·079", 37773, 5, 5, 0)]
    [InlineData("VEN·080", 37774, 2, 1, 0)]
    [InlineData("VEN·020", 37706, 3, 3, 0)]
    [InlineData("VEN·024", 37712, 3, 3, 0)]
    [InlineData("VEN·028", 37716, 2, 2, 0)]
    [InlineData("VEN·071", 37765, 6, 5, 0)]
    [InlineData("VEN·121", 37821, 4, 3, 1)]
    public async Task EveryOfficialUnitPaysItsCostAndEntersExhaustedWithoutTriggeringItsOwnAbility(string no, long id, int mana, int might, int power)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(might, card.Power); Assert.Equal(power, card.ReturnEnergy ?? 0);
        var state = VenUnitEntryBasicTests.Position(no);
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } },
            "P1", new PlayCardCommand("UNIT", no, []));
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, ["ALLY"]));
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana); Assert.Equal(20 - power, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(might, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·219/298", true)] [InlineData("OGN·023/298", false)]
    public async Task CaptainOnlyReactsToAnotherFriendlyUnitAndItsBonusExpires(string playedNo, bool isUnit)
    {
        var state = AddHandCard(InBase("VEN·121"), "PLAY", playedNo, "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("PLAY", playedNo, []));
        if (isUnit) {
            Assert.Equal("ANOTHER_UNIT_PLAYED", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
            Assert.Equal(3, game.State.CardObjects["UNIT"].Power); await game.Top(); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
            await game.Act("P1", new EndTurnCommand()); Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
        } else Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent is not null);
        await game.Replay();
    }

    [Fact]
    public async Task CaptainDoesNotObserveOpponentsAndDoesNotBuffANewIncarnation()
    {
        var initial = AddHandCard(InBase("VEN·121"), "PLAY", "OGN·219/298", "P2") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var opponent = new Driver(initial); await opponent.Act("P2", new PlayCardCommand("PLAY", "OGN·219/298", []));
        Assert.DoesNotContain(opponent.State.StackItems, i => i.UnitEvent is not null);
        var game = new Driver(AddHandCard(InBase("VEN·121"), "PLAY", "OGN·219/298", "P1"));
        await game.Act("P1", new PlayCardCommand("PLAY", "OGN·219/298", []));
        var changed = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["UNIT"] = game.State.CardObjects["UNIT"] with { ObjectGeneration = game.State.CardObjects["UNIT"].ObjectGeneration + 2 } } };
        var done = await OfficialGraveyardRecastTests.Top(changed); Assert.Equal(3, done.State.CardObjects["UNIT"].Power);
        OfficialGraveyardRecastTests.Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ImpatientCatTriggersOnlyAnActualReadyTransitionAndWaitsForResponses(bool exhausted)
    {
        var state = AddHandCard(InBase("VEN·071"), "READY", "UNL-009/219", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = exhausted } } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"])); await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        if (exhausted) {
            Assert.Equal("BECAME_READY", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
            await game.Top(); Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
            await game.Act("P1", new EndTurnCommand()); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        } else Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task CatAlsoObservesTheSharedBeginningOfTurnReadyInstruction()
    {
        var state = InBase("VEN·071") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = true } } };
        var game = new Driver(state); await game.Act("P2", new EndTurnCommand());
        for (var i = 0; i < 12 && !game.State.StackItems.Any(x => x.UnitEvent is not null); i++) {
            Assert.NotNull(game.State.PriorityPlayerId);
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.Equal("BECAME_READY", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power); await game.Top(); Assert.Equal(7, game.State.CardObjects["UNIT"].Power); await game.Replay();
    }

    [Fact]
    public async Task DancerConfirmsAnotherFriendlyUnitBeforeResponsesAndReadiesItOnResolution()
    {
        var state = BattlePosition("VEN·020", true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true } } };
        var game = new Driver(state); await game.Act("P1", new DeclareBattleCommand("BF", ["UNIT"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        var pending = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { "ALLY" }, pending.LegalObjectIds); Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        foreach (var id in new[] { "UNIT", "ENEMY", "GEAR", "GRAVE" })
            await Reject(game.State, "P1", new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, [id]));
        await Reject(game.State, "P2", new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["ALLY"]));
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["ALLY"]));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted); await game.Top(); Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·024", true)] [InlineData("VEN·024", false)]
    [InlineData("VEN·028", true)] [InlineData("VEN·028", false)]
    public async Task BattleEndAbilitiesTriggerForSurvivorsAfterBattleClosesAndThePoroRequiresNoActualDamage(string no, bool prevented)
    {
        var game = new Driver(BattlePosition(no, prevented));
        await Fight(game);
        for (var i = 0; i < 20 && (game.State.BattleState.IsActive || game.State.PendingBattleResult is not null); i++) {
            Assert.NotNull(game.State.PriorityPlayerId); await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.False(game.State.CardObjects["UNIT"].IsAttacking);
        var expected = no == "VEN·028" || prevented;
        Assert.Equal(expected, game.State.StackItems.Any(i => i.UnitEvent is { Timing: "BATTLE_END" }));
        if (expected) {
            Assert.Empty(game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Top();
            if (no == "VEN·024") Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
            else { Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Might(game.State)); }
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·024")] [InlineData("VEN·028")]
    public async Task AUnitThatDiesBeforeBattleEndsDoesNotEarnItsBattleEndAbility(string no)
    {
        var state = BattlePosition(no, false);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Power = 10 } } };
        var game = new Driver(state); await Fight(game);
        for (var i = 0; i < 20 && (game.State.BattleState.IsActive || game.State.PendingBattleResult is not null); i++)
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent is not null); await game.Replay();
    }

    [Fact]
    public async Task TheWitnessDoesNotGainAnotherBonusWhenAlreadyEmpowered()
    {
        var state = BattlePosition("VEN·028", true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsEmpowered = true } } };
        var game = new Driver(state); await Fight(game);
        for (var i = 0; i < 20 && (game.State.BattleState.IsActive || game.State.PendingBattleResult is not null); i++)
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(4, Might(game.State)); await game.Top(); Assert.Equal(4, Might(game.State)); await game.Replay();
    }

    [Fact]
    public async Task ThePoroKeepsItsEarnedDrawAfterItsSourceLeavesInResponse()
    {
        var game = new Driver(AddHandCard(BattlePosition("VEN·024", true), "LEAVE", "OGN·104/298", "P1"));
        await Fight(game);
        for (var i = 0; i < 20 && (game.State.BattleState.IsActive || game.State.PendingBattleResult is not null); i++)
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Single(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems); await game.Top();
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task TheDancerCannotReadyATargetThatLeftInResponse()
    {
        var state = AddHandCard(BattlePosition("VEN·020", true), "LEAVE", "OGN·104/298", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true } } };
        var game = new Driver(state); await Fight(game);
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Act("P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand); await game.Top();
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_READIED"); await game.Replay();
    }

    [Fact]
    public async Task HealingDoesNotEraseThePorosHistoryOfTakingDamage()
    {
        var state = AddHandCard(InBase("VEN·024"), "DAMAGE", "SFD·080/221", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("DAMAGE", "SFD·080/221", ["UNIT"])); await game.Top();
        Assert.Contains("TOOK_DAMAGE_THIS_TURN", game.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        // Keep a truthful damage history while testing the post-heal boundary.
        var healed = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["UNIT"] = game.State.CardObjects["UNIT"] with { Damage = 0, DamageMarkedByPlayerIds = null } } };
        var battle = BattlePosition("VEN·024", true) with { CardObjects = new Dictionary<string, CardObjectState>(BattlePosition("VEN·024", true).CardObjects) {
            ["UNIT"] = healed.CardObjects["UNIT"] } };
        var fought = new Driver(battle); await Fight(fought);
        for (var i = 0; i < 20 && (fought.State.BattleState.IsActive || fought.State.PendingBattleResult is not null); i++)
            await fought.Act(fought.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.DoesNotContain(fought.State.StackItems, i => i.UnitEvent is not null); await fought.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsForgedOrMissingCapturedUnitEventMetadata()
    {
        var game = new Driver(AddHandCard(InBase("VEN·121"), "PLAY", "OGN·219/298", "P1"));
        await game.Act("P1", new PlayCardCommand("PLAY", "OGN·219/298", [])); var item = Assert.Single(game.State.StackItems);
        foreach (var context in new UnitEventContext?[] { null, item.UnitEvent! with { Timing = "BATTLE_END" },
            item.UnitEvent! with { SourceGeneration = 500 }, item.UnitEvent! with { CapturedTick = game.State.Tick + 1 } }) {
            var bad = game.State with { StackItems = [item with { UnitEvent = context }] };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), e => e.Contains("unit event"));
        }
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReaperMayPayOneRedBeforeResponsesForTemporaryAssault(bool decline)
    {
        var initial = BattlePosition("VEN·009", true);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(0, 0, new Dictionary<string, int> { ["red"] = 1 }) } };
        var game = new Driver(initial); await Fight(game);
        var payment = game.State.PendingPayment!; Assert.Equal("TRIGGER_COST_CONFIRMATION", payment.PaymentWindow);
        Assert.Equal(1, payment.PowerCost); Assert.Equal(1, payment.PowerCostByTrait["red"]);
        await Reject(game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P1"] = RunePool.Empty } },
            "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [decline ? "DECLINE" : "PAY"]));
        Assert.Equal(decline ? 1 : 0, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects, x => x.StartsWith("COMBAT_KEYWORD:"));
        if (decline) { Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent is not null); return; }
        Assert.NotNull(game.State.PriorityPlayerId); await game.Top();
        Assert.Contains("COMBAT_KEYWORD:强攻2:1", game.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        Assert.Equal(6, Might(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData(false, 7, false)] [InlineData(true, 5, false)] [InlineData(true, 7, true)]
    public async Task DameOnlyConfirmsAGreaterUnitHereAfterAnEmpoweredAttack(bool empowered, int enemyPower, bool triggers)
    {
        var initial = BattlePosition("VEN·079", true);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsEmpowered = empowered },
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Power = enemyPower },
            ["ALLY"] = initial.CardObjects["ALLY"] with { Power = 10 } } };
        var game = new Driver(initial); await Fight(game);
        if (!triggers) { Assert.Null(game.State.PendingCardChoice); Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent is not null); return; }
        var choice = game.State.PendingCardChoice!; Assert.Equal(new[] { "ENEMY" }, choice.LegalObjectIds);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        Assert.Equal(5, Might(game.State)); await game.Top(); Assert.Equal(enemyPower + 1, Might(game.State)); await game.Replay();
    }

    [Fact]
    public async Task DameAlsoTriggersWhenDefending()
    {
        var initial = BattlePosition("VEN·079", true) with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsEmpowered = true },
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Power = 7 } } };
        var game = new Driver(initial);
        await game.Act("P2", new DeclareBattleCommand("BF", ["ENEMY"], ["UNIT"], ["COMBAT_ASSIGNMENT"]));
        var choice = game.State.PendingCardChoice!; Assert.Equal(new[] { "ENEMY" }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await game.Top(); Assert.Equal(8, Might(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DemolitionistCanDestroyEitherSidesEquipmentWithinItsCurrentPowerAfterConquest(bool decline)
    {
        var initial = InBase("VEN·080");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { ManaCost = 1 },
            ["EXPENSIVE"] = OfficialGarenConquestBattlefieldCountTests.Printed("EXPENSIVE", "SFD·022/221", "P2") with { ManaCost = 2 } },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { Base = [..initial.PlayerZones["P2"].Base, "EXPENSIVE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["EXPENSIVE"] = new("P2", "BASE") } };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        var choice = game.State.PendingCardChoice!; Assert.Equal(new[] { "GEAR" }, choice.LegalObjectIds);
        foreach (var illegal in new[] { "UNIT", "ENEMY", "EXPENSIVE" })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [illegal]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, decline ? [] : ["GEAR"]));
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        if (!decline) { await game.Top(); Assert.Contains("GEAR", game.State.PlayerZones["P1"].Graveyard); }
        await game.Replay();
    }

    [Fact]
    public async Task DamesConfirmedTargetLeavingInResponseDoesNotGrantPower()
    {
        var initial = AddHandCard(BattlePosition("VEN·079", true), "BOUNCE", "OGN·104/298", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsEmpowered = true },
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Power = 7 } } };
        var game = new Driver(initial); await Fight(game);
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        if (game.State.PriorityPlayerId == "P1") await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("BOUNCE", "OGN·104/298", ["ENEMY"])); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand); await game.Top();
        Assert.Equal(5, Might(game.State)); await game.Replay();
    }

    [Fact]
    public async Task DemolitionistsEarnedAbilityUsesLastKnownPowerWhenItsSourceLeaves()
    {
        var initial = AddHandCard(InBase("VEN·080"), "BOUNCE", "OGN·104/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { ManaCost = 1 } } };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        await game.Act("P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Theory]
    [InlineData(0, false)] [InlineData(4, false)] [InlineData(5, true)] [InlineData(7, true)]
    public async Task DragonEntersReadyOnlyWhenMoreThanThreePointsBelowTheWinningScore(int score, bool exhausted)
    {
        var initial = VenUnitEntryBasicTests.Position("VEN·091");
        initial = initial with { PlayerScores = new Dictionary<string, int>(initial.PlayerScores) { ["P1"] = score } };
        await Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } },
            "P1", new PlayCardCommand("UNIT", "VEN·091", []));
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·091", []));
        Assert.Equal(exhausted, game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(10, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(10, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task DragonChoosesAnyNumberOfEnemyUnitsHereWhoseCurrentPowerDoesNotExceedFive(int count)
    {
        var game = new Driver(DragonBattle()); await Fight(game);
        var choice = game.State.PendingCardChoice!; Assert.Equal(0, choice.RequiredCount); Assert.Equal(2, choice.MaxCount);
        Assert.Equal(new[] { "ENEMY", "SECOND" }, choice.LegalObjectIds);
        foreach (var illegal in new[] { "UNIT", "ALLY", "STRONG", "AWAY", "GRAVE" })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [illegal]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, new[] { "ENEMY", "SECOND" }.Take(count).ToArray()));
        if (count > 0) { Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["ENEMY"].Zone); await game.Top(); }
        Assert.Equal(count > 0 ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.Equal(count == 2 ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["SECOND"].Zone);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["STRONG"].Zone);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["AWAY"].Zone); await game.Replay();
    }

    [Fact]
    public async Task DragonsConfirmedTargetAboveFivePowerInResponseDoesNotMove()
    {
        var game = new Driver(AddHandCard(DragonBattle(), "BUFF", "OGN·058/298", "P2")); await Fight(game);
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY", "SECOND"]));
        if (game.State.PriorityPlayerId == "P1") await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("BUFF", "OGN·058/298", ["SECOND"])); await game.Top(); await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["SECOND"].Zone); await game.Replay();
    }

    [Fact]
    public async Task DragonLeavingInResponseKeepsItsLastKnownHereForTheEarnedSkill()
    {
        var game = new Driver(AddHandCard(DragonBattle(), "BOUNCE", "OGN·104/298", "P1")); await Fight(game);
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY", "SECOND"]));
        await game.Act("P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["UNIT"])); await game.Top(); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("BASE", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.Equal("BASE", game.State.ObjectLocations["SECOND"].Zone); await game.Replay();
    }

    private static MatchState DragonBattle()
    {
        var initial = BattlePosition("VEN·091", true);
        foreach (var (id, power, field) in new[] { ("SECOND", 5, "BF"), ("STRONG", 6, "BF"), ("AWAY", 1, "OTHER") }) {
            initial = AddHandCard(initial, id, "OGN·219/298", "P2");
            initial = LocalDestructionRecallTests.At(initial, id, "P2", "BATTLEFIELD", field);
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [id] = initial.CardObjects[id] with { Power = power } } };
        }
        return initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P2"] = initial.PlayerZones["P2"] with { Hand = [] } } };
    }

    private static async Task Fight(Driver game)
    {
        await game.Act("P1", new DeclareBattleCommand("BF", ["UNIT"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
    }

    private static int Might(MatchState state) => CurrentPowerTargetTests.Snapshot(state).GetProperty("players")
        .GetProperty("P1").GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();

    private static MatchState InBase(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [], Base = [..state.PlayerZones["P1"].Base, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "BASE") } };
    }
    private static MatchState BattlePosition(string no, bool stunnedEnemy)
    {
        var state = InBase(no); var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        cards["ENEMY"] = cards["ENEMY"] with { Power = 1, UntilEndOfTurnEffects = stunnedEnemy ? ["STUNNED"] : [] };
        zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Where(id => id != "UNIT").ToArray(), Battlefields = [..zones["P1"].Battlefields, "UNIT"] };
        zones["P2"] = zones["P2"] with { Base = [], Battlefields = [..zones["P2"].Battlefields, "ENEMY"] };
        locations["UNIT"] = new("P1", "BATTLEFIELD", "BF"); locations["ENEMY"] = new("P2", "BATTLEFIELD", "BF");
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations,
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("BF")] };
    }
    private static MatchState AddHandCard(MatchState state, string id, string no, string player)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [player] = state.PlayerZones[player] with { Hand = [..state.PlayerZones[player].Hand, id] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "HAND") } };
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-unit-event", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
