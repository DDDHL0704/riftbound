using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenConquestScoreHeroesTests
{
    private static MatchState Position(string no, bool empowered = false)
    {
        var state = ConquestTokenTriggerTests.Position(no);
        return state with { RoomId = "VEN-CONQUEST-SCORE-" + no,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", no, "P1") with { IsEmpowered = empowered } },
            RunePools = new Dictionary<string, RunePool> {
                ["P1"] = new(40, 20, new Dictionary<string, int> { [RuneTrait.Green] = 8, [RuneTrait.Yellow] = 8, [RuneTrait.Blue] = 8 }),
                ["P2"] = new(40, 20, new Dictionary<string, int> { [RuneTrait.Green] = 8, [RuneTrait.Yellow] = 8, [RuneTrait.Blue] = 8 }) } };
    }
    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
    }

    [Theory]
    [InlineData("VEN·046", 37736)] [InlineData("VEN·046a", 37737)]
    public async Task NasusCanPayEightToEmpowerAndItsConquestScoresAfterResponses(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("飞升者", card.SubTitle); Assert.Equal(8, card.Energy); Assert.Equal(8, card.Power);
        Assert.Contains("当我征服一处战场时，你获得1分", card.CardEffect);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitConquestEmpoweredGainScore);
        Assert.True(spec.RequiresSourceEmpowered); Assert.Equal(1, spec.ScoreAmount);
        var game = new Driver(Position(no));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.Equal(32, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
        await Conquer(game); Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Single(game.State.StackItems, i => i.ConquestScore is not null);
        await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·046")] [InlineData("VEN·046a")]
    public async Task NasusCanBePlayedFromHandWithPrintedWardAndThenEmpowered(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(16, 20, new Dictionary<string, int> { [RuneTrait.Green] = 3 }), ["P2"] = new(20, 20) } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Equal(8, game.State.RunePools["P1"].Mana);
        Assert.Contains("法盾2", game.State.CardObjects["UNIT"].Tags);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
        Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(0, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Fact]
    public async Task PlayingNasusTriggersItsLegendAndTheExhaustedLegendCannotPayAgain()
    {
        var state = VenUnitEntryBasicTests.Position("VEN·046");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = new("LEGEND", cardNo: "VEN·145", ownerId: "P1", controllerId: "P1", tags: ["传奇"]),
            ["RUNE-A"] = OfficialGarenConquestBattlefieldCountTests.Printed("RUNE-A", "UNL-R02", "P1") with { IsExhausted = true } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"], Base = [..state.PlayerZones["P1"].Base, "RUNE-A"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND"), ["RUNE-A"] = new("P1", "BASE") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(16, 20, new Dictionary<string, int> { [RuneTrait.Green] = 3 }) } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·046", [], Destination: "BASE"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["RUNE-A"]));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"])); await game.Top();
        Assert.False(game.State.CardObjects["RUNE-A"].IsExhausted); Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
        Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.State.StackItems, item => item.HighCostPlayObserver is not null); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·046")] [InlineData("VEN·046a")]
    public async Task NasusWithoutEmpowermentCannotEarnTheConquestSkill(string no)
    {
        var game = new Driver(Position(no)); await Conquer(game);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.DoesNotContain(game.State.StackItems, i => i.ConquestScore is not null); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·046")] [InlineData("VEN·046a")]
    public async Task InsufficientManaAndRepeatedEmpowerAreRejectedAtomically(string no)
    {
        var state = Position(no) with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(7, 20), ["P2"] = RunePool.Empty } };
        var cmd = new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("unpaid", "P1", cmd.CmdType), cmd, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsEmpowered = true } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(8, 20), ["P2"] = RunePool.Empty } };
        rejected = await new CoreRuleEngine().ResolveAsync(state, new("already", "P1", cmd.CmdType), cmd, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task NasusSkillSurvivesRemovingEmpowermentInResponse()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·046", empowered: true), "REVERSE", "VEN·035", "P1");
        var game = new Driver(state); await Conquer(game);
        Assert.Single(game.State.StackItems, i => i.ConquestScore is not null);
        await game.Act("P1", new PlayCardCommand("REVERSE", "VEN·035", ["UNIT"], Mode: "REMOVE_THEN_EMPOWER"));
        await game.Top(); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ReckonerArenaActivatesNasusConquestEffectOnlyWhileEmpowered(bool empowered)
    {
        var state = OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·286/298"), "UNIT", "VEN·046", "F", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsEmpowered = empowered } } };
        var game = new Driver(state); await game.Act("P1", new PassPriorityCommand());
        Assert.Single(game.State.StackItems, item => item.HeldContext?.Kind == "ACTIVATE_CONQUEST");
        await game.Top();
        Assert.Equal(empowered, game.State.StackItems.Any(item => item.ConquestScore?.Reason == "BATTLEFIELD_HELD"));
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        if (empowered) { await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]); }
        await game.Replay();
    }

    [Fact]
    public async Task NasusSkillCanWinAfterTheLastConquestPointWasReplacedByADraw()
    {
        var state = Position("VEN·046", empowered: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = new("OTHER", cardNo: "OGN·294/298", ownerId: "P1", controllerId: "P1",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Battlefields = ["OTHER"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["OTHER"] = new("P1", "BATTLEFIELD", "OTHER") },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 7, ["P2"] = 0 } };
        var game = new Driver(state); await Conquer(game);
        Assert.Equal(7, game.State.PlayerScores["P1"]); Assert.Contains("DRAW1", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems, item => item.ConquestScore is not null);
        await game.Top(); Assert.Equal(8, game.State.PlayerScores["P1"]);
        Assert.Equal(MatchStatuses.Finished, game.State.Status); Assert.Equal("P1", game.State.WinnerPlayerId);
        await game.Replay();
    }

    [Fact]
    public async Task AdditionalConquestActivationCreatesTwoIndependentlyRespondableScoreSkills()
    {
        var state = Position("VEN·046", empowered: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TREANT"] = OfficialGarenConquestBattlefieldCountTests.Printed("TREANT", "UNL-029/219", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "TREANT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["TREANT"] = new("P1", "BASE") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(40, 20, new Dictionary<string, int> { [RuneTrait.Green] = 8, [RuneTrait.Purple] = 8 }) } };
        state = AttackDamageDistributionTests.Spell(state, "WIND", "OGN·173/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PlayCardCommand("WIND", "OGN·173/298", ["TREANT"], Destination: "BATTLEFIELD:BF"));
        await game.Top();
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        var earned = game.State.TriggerQueue.Where(t => t.ConquestScore is not null).ToArray();
        Assert.Equal(2, earned.Length);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: earned.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count(item => item.ConquestScore is not null));
        await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]);
        await game.Top(); Assert.Equal(3, game.State.PlayerScores["P1"]); await game.Replay();
    }

    private static MatchState WithPlays(string no, bool tokenSpell = false)
    {
        var state = Position(no); var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        foreach (var (id, cardNo, player) in new[] { ("BODY", "OGN·088/298", "P1"), ("GEAR", "SFD·168/221", "P1"),
            ("SPELL", tokenSpell ? "UNL-044/219" : "OGN·048/298", "P1"), ("COUNTER", "OGN·064/298", "P2") })
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, player);
            locations[id] = new(player, "HAND"); zones[player] = zones[player] with { Hand = [..zones[player].Hand, id] };
        }
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }
    private static async Task PlayTypes(Driver game, int mask, bool counter = false, bool tokenSpell = false)
    {
        if ((mask & 1) != 0) await game.Act("P1", new PlayCardCommand("BODY", "OGN·088/298", [], Destination: "BASE"));
        if ((mask & 2) != 0) await game.Act("P1", new PlayCardCommand("GEAR", "SFD·168/221", [], Destination: "BASE"));
        if ((mask & 4) == 0) return;
        await game.Act("P1", tokenSpell
            ? new PlayCardCommand("SPELL", "UNL-044/219", [], Mode: "CREATE_WARHAWKS")
            : new PlayCardCommand("SPELL", "OGN·048/298", []));
        if (counter)
        {
            var target = Assert.Single(game.State.StackItems).StackItemId;
            await game.Act("P1", new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [target]));
        }
        await game.Top();
    }

    [Theory]
    [InlineData("VEN·065", 37937)] [InlineData("VEN·173", 37876)]
    public async Task SwainScoresAfterTheControllerHasPlayedAUnitEquipmentAndSpell(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("远谋卓识", card.SubTitle);
        var game = new Driver(WithPlays(no)); await PlayTypes(game, 7); await Conquer(game);
        Assert.Equal(1, game.State.PlayerScores["P1"]); Assert.Single(game.State.StackItems, i => i.ConquestScore is not null);
        await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·065", 3)] [InlineData("VEN·065", 5)] [InlineData("VEN·065", 6)]
    [InlineData("VEN·173", 3)] [InlineData("VEN·173", 5)] [InlineData("VEN·173", 6)]
    public async Task SwainRequiresAllThreeCardTypesInTheCurrentTurn(string no, int mask)
    {
        var game = new Driver(WithPlays(no)); await PlayTypes(game, mask); await Conquer(game);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.DoesNotContain(game.State.StackItems, i => i.ConquestScore is not null); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·065")] [InlineData("VEN·173")]
    public async Task SwainCountsAConfirmedSpellEvenWhenTheOpponentCountersIt(string no)
    {
        var game = new Driver(WithPlays(no)); await PlayTypes(game, 7, counter: true);
        Assert.Contains(game.State.CardPlays.Finalized, p => p.Source.ObjectId == "SPELL");
        Assert.DoesNotContain(game.State.CardPlays.Resolved, p => p.Source.ObjectId == "SPELL");
        await Conquer(game); Assert.Single(game.State.StackItems, i => i.ConquestScore is not null);
        await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·065")] [InlineData("VEN·173")]
    public async Task ReturnedCounteredSpellCountsWithoutRevealingItsNewHiddenHandIdentity(string no)
    {
        var state = WithPlays(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["COUNTER"] = OfficialGarenConquestBattlefieldCountTests.Printed("COUNTER", "UNL-131/219", "P2") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("BODY", "OGN·088/298", [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("GEAR", "SFD·168/221", [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        var target = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "UNL-131/219", [target])); await game.Top();
        if (game.State.PendingCardChoice is { } choice)
            await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Hand);
        Assert.Contains(game.State.CardPlays.Finalized, play => play.Source.ObjectId == "SPELL");
        await Conquer(game); Assert.Single(game.State.StackItems, i => i.ConquestScore is not null);
        await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·065")] [InlineData("VEN·173")]
    public async Task OneNonTokenUnitEquipmentCardSatisfiesBothOfThoseTypes(string no)
    {
        var state = WithPlays(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BODY"] = OfficialGarenConquestBattlefieldCountTests.Printed("BODY", "VEN·058", "P1") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("BODY", "VEN·058", [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", [])); await game.Top();
        Assert.Equal(3, Assert.Single(game.State.CardPlays.Finalized, p => p.Source.ObjectId == "BODY").Types & 3);
        await Conquer(game); Assert.Single(game.State.StackItems, i => i.ConquestScore is not null);
        await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·065")] [InlineData("VEN·173")]
    public async Task PlayedTokenUnitsDoNotSatisfyTheNonTokenUnitCondition(string no)
    {
        var game = new Driver(WithPlays(no, tokenSpell: true)); await PlayTypes(game, 6, tokenSpell: true);
        Assert.True(game.State.CardObjects.Values.Count(card => card.CardNo == "UNL·T02") >= 4);
        Assert.DoesNotContain(game.State.CardPlays.Finalized, p => (p.Types & 1) != 0);
        await Conquer(game); Assert.DoesNotContain(game.State.StackItems, i => i.ConquestScore is not null);
        Assert.Equal(1, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·065")] [InlineData("VEN·173")]
    public async Task SwainPrintedPredictWorksWhenActuallyPlayed(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Blue] = 3 }), ["P2"] = new(20, 20) } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Single(game.State.StackItems, i => i.InsightContext?.Kind == "PRINTED_PREDICT");
        await game.Top(); var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsAlteredScoreSourceAndConquestConditions()
    {
        var game = new Driver(Position("VEN·046", empowered: true)); await Conquer(game);
        var state = game.State; var item = Assert.Single(state.StackItems); var context = item.ConquestScore!;
        foreach (var changed in new[] { item with { ConquestScore = null },
            item with { ConquestScore = context with { SourceWasEmpowered = false } },
            item with { ConquestScore = context with { Source = context.Source with { Generation = context.Source.Generation + 1 } } },
            item with { ConquestScore = context with { BattlefieldId = "MISSING" } },
            item with { ConquestScore = context with { ControllerId = "P2" } } })
            Assert.Contains("invalid conquest score stack", OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [changed] }));
    }
}
