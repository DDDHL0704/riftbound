using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class KennenLegendOutsideHandTests
{
    private const string Ability = LegendActionAbilityCatalog.KennenLegendAbilityId;
    private const string Keyword = "COMBAT_KEYWORD:强攻2:1";

    private static MatchState WithLegend(MatchState state, string cardNo = "VEN·155", string controller = "P1", bool empowered = false)
    {
        var zones = state.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Value with {
            LegendZone = pair.Value.LegendZone.Where(id => id != "LEGEND").ToArray() });
        zones[controller] = zones[controller] with { LegendZone = ["LEGEND"] };
        return state with {
            PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = new CardObjectState("LEGEND", cardNo: cardNo, ownerId: controller, controllerId: controller, tags: ["传奇"]) with { IsEmpowered = empowered } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new(controller, "LEGEND") } };
    }

    [Theory]
    [InlineData("VEN·155", 37858)]
    [InlineData("VEN·155a", 38299)]
    [InlineData("VEN·197", 37908)]
    [InlineData("VEN·197*", 37909)]
    public async Task EachPrintingObservesARealChampionZonePlayAndEmpowersAfterResponses(string cardNo, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == cardNo);
        Assert.Equal(id, card.Id); Assert.Equal("狂暴之心", card.CardName); Assert.Equal("凯南", card.Hero);
        Assert.Contains("当你从你的手牌以外的位置打出一张卡牌时，强化我。", card.CardEffect);
        var state = WithLegend(VenUnitEntryBasicTests.Position("VEN·019"), cardNo);
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") } };
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·019", [], Destination: "BASE"));
        var play = Assert.Single(game.State.CardPlays.Resolved);
        Assert.True(play.OriginOutsideHands); Assert.Equal("P1", play.PlayerId);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.NotNull(Assert.Single(game.State.StackItems).OutsideHandPlayObserver);
        ArmamentMightTests.PublicRecovery(game.State);
        await game.Top();
        Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ACompletedFlowSpellTriggersButTheSameSpellFromTheControllersHandDoesNot(bool flow)
    {
        var state = WithLegend(VenFlowCombatKeywordTests.Position(flow));
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", "VEN·012", ["ALLY"]));
        Assert.Empty(game.State.CardPlays.Resolved);
        Assert.DoesNotContain(game.State.StackItems, i => i.OutsideHandPlayObserver is not null);
        await game.Top();
        var play = Assert.Single(game.State.CardPlays.Resolved);
        Assert.Equal(flow, play.OriginOutsideHands);
        Assert.Equal(flow, game.State.StackItems.Any(i => i.OutsideHandPlayObserver is not null));
        if (flow) { await game.Top(); Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered); }
        else Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task CounteredFlowSpellDoesNotTriggerAnOutsideHandPlay()
    {
        var state = AttackDamageDistributionTests.Spell(WithLegend(VenFlowCombatKeywordTests.Position(true)), "COUNTER", "OGN·045/298", "P2");
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", "VEN·012", ["ALLY"]));
        var original = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [original]));
        await game.Top();
        Assert.DoesNotContain(game.State.CardPlays.Resolved, play => play.Source.ObjectId == "SPELL");
        Assert.DoesNotContain(game.State.StackItems, item => item.OutsideHandPlayObserver is not null);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task OnlyThePlayerWhoCompletesTheCardPlayObservesIt()
    {
        var state = WithLegend(VenFlowCombatKeywordTests.Position(true), controller: "P2");
        var played = await Act(state, "P1", new PlayCardCommand("SPELL", "VEN·012", ["ALLY"]));
        var resolved = await Top(played.State);
        Assert.DoesNotContain(resolved.State.StackItems, item => item.OutsideHandPlayObserver is not null);
        Assert.False(resolved.State.CardObjects["LEGEND"].IsEmpowered);
    }

    [Theory]
    [InlineData("VEN·155")]
    [InlineData("VEN·155a")]
    [InlineData("VEN·197")]
    [InlineData("VEN·197*")]
    public async Task QuickAbilitySpendsEmpowermentAndExhaustionThenGrantsAssaultTwo(string cardNo)
    {
        var state = WithLegend(VenFlowCombatKeywordTests.Position(false), cardNo, empowered: true);
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new LegendActCommand("LEGEND", Ability, ["ALLY"], []));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.DoesNotContain(Keyword, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Top();
        Assert.Contains(Keyword, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(Keyword, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("unempowered")]
    [InlineData("exhausted")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("hand")]
    [InlineData("wrong-player")]
    public async Task InvalidActivationPaysNothingAndCannotChangeState(string scenario)
    {
        var state = WithLegend(VenFlowCombatKeywordTests.Position(false), empowered: scenario != "unempowered");
        if (scenario == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true } } };
        var targets = scenario switch { "missing" => Array.Empty<string>(), "duplicate" => new[] { "ALLY", "ALLY" },
            "hand" => new[] { "SPELL" }, _ => new[] { "ALLY" } };
        var cmd = new LegendActCommand("LEGEND", Ability, targets, []);
        var rejected = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid", scenario == "wrong-player" ? "P2" : "P1", cmd.CmdType), cmd, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task EnemyWardIsPaidAndTheOpponentCanBeGivenAssault()
    {
        var state = WithLegend(VenFlowCombatKeywordTests.Position(false), empowered: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [..state.CardObjects["ENEMY"].Tags, "法盾2"] } } };
        var before = state.RunePools["P1"].TotalPower;
        var activated = await Act(state, "P1", new LegendActCommand("LEGEND", Ability, ["ENEMY"], []));
        Assert.Equal(before - 2, activated.State.RunePools["P1"].TotalPower);
        var item = Assert.Single(activated.State.StackItems);
        Assert.Equal(2, item.LegendUnitKeyword!.SpellshieldTaxPower);
        var forged = item with { LegendUnitKeyword = item.LegendUnitKeyword with { SpellshieldTaxPower = -1 } };
        Assert.Contains("invalid captured legend unit keyword", OfficialInsightAndSpellLockTests.Errors(activated.State with { StackItems = [forged] }));
        var done = await Top(activated.State);
        Assert.Contains(Keyword, done.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
    }

    [Theory]
    [InlineData("context")]
    [InlineData("card")]
    [InlineData("controller")]
    [InlineData("play")]
    [InlineData("played-generation")]
    public async Task RecoveryRejectsForgedCardPlayObserver(string change)
    {
        var state = WithLegend(VenUnitEntryBasicTests.Position("VEN·019"));
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") } };
        state = (await Act(state, "P1", new PlayCardCommand("UNIT", "VEN·019", [], Destination: "BASE"))).State;
        var item = Assert.Single(state.StackItems); var context = item.OutsideHandPlayObserver!;
        var forged = item with { OutsideHandPlayObserver = change switch {
            "context" => null, "card" => context with { CardNo = "FORGED" },
            "controller" => context with { ControllerId = "P2" }, "play" => context with { PlayId = "FORGED" },
            _ => context with { Played = context.Played with { Generation = context.Played.Generation + 1 } } } };
        Assert.Contains("invalid card play observer stack", OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [forged] }));
    }

    [Fact]
    public async Task WhenTheSpellChangesControllerTheResolvingPlayersOwnHandIsTheReference()
    {
        var initial = WithLegend(RebuttalRulesTests.Position(), controller: "P2");
        var state = await RebuttalRulesTests.Open(initial);
        state = await RebuttalRulesTests.Choose(state, "PAY:ANY");
        state = await RebuttalRulesTests.Choose(state, "KEEP_ALL");
        var finished = await Top(state);
        var confirmed = Assert.Single(finished.State.CardPlays.Finalized, e => e.Source.ObjectId == "R-SPELL");
        var completed = Assert.Single(finished.State.CardPlays.Resolved, e => e.Source.ObjectId == "R-SPELL");
        Assert.Equal("P1", confirmed.PlayerId); Assert.Equal("P2", completed.PlayerId);
        Assert.Null(confirmed.OriginHandPlayerId);
        Assert.NotNull(Assert.Single(finished.State.StackItems).OutsideHandPlayObserver);
        ArmamentMightTests.PublicRecovery(finished.State);
        var done = await Top(finished.State);
        Assert.True(done.State.CardObjects["LEGEND"].IsEmpowered);
    }

    [Fact]
    public async Task AnOpponentDirectedPlayFromTheOwnersOwnHandDoesNotQualify()
    {
        var initial = WithLegend(OfficialRevealedHandPlayTests.Position(), controller: "P2");
        var opened = await OfficialRevealedHandPlayTests.Open(initial);
        var selected = await OfficialRevealedHandPlayTests.Select(opened.State, "U");
        var done = await Act(selected.State, "P2", new PlayCardCommand("U", "OGN·208/298", [], Destination: "BATTLEFIELD:BF"));
        var play = Assert.Single(done.State.CardPlays.Resolved, p => p.Source.ObjectId == "U");
        Assert.Equal("P2", play.PlayerId); Assert.Null(play.OriginHandPlayerId);
        Assert.DoesNotContain(done.State.StackItems, i => i.OutsideHandPlayObserver is not null);
        Assert.False(done.State.CardObjects["LEGEND"].IsEmpowered);
        Restore(done.State);
    }

    [Fact]
    public async Task CreatingAndPlayingATokenIsNotPlayingACard()
    {
        var initial = AttackDamageDistributionTests.Spell(TurnUnitPlayGrantsTests.Position(TurnUnitPlayGrantsTests.Confront),
            "SANDCRAFT", "SFD·031/221", "P1");
        var game = new VenBasicFlowSpellTests.Driver(WithLegend(initial));
        await game.Act("P1", new PlayCardCommand("SANDCRAFT", "SFD·031/221", []));
        await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier));
        Assert.DoesNotContain(game.State.CardPlays.Resolved, p => p.Source.ObjectId == token.ObjectId);
        Assert.DoesNotContain(game.State.StackItems, i => i.OutsideHandPlayObserver is not null);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task AReturnedTargetKeepsItsNewHandIncarnationAndDoesNotRefundTheCosts()
    {
        var initial = EquipChainTests.Response(WithLegend(LegendAttachmentTests.PositionFor("VEN·155"), empowered: true),
            "OGN·169/298", "P2");
        var activated = await Act(initial, "P1", new LegendActCommand("LEGEND", Ability, ["HOST"], []));
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["HOST"]));
        var returned = await Top(response.State);
        Assert.Contains("HOST", returned.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(returned.State)["P2"].Stack));
        var done = await Top(returned.State);
        Assert.True(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.False(done.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.DoesNotContain(Keyword, done.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Contains(done.Events, e => e.Kind == "ABILITY_NO_EFFECT");
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(done.Events));
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ForgingAHandOriginInBothHistoryEntriesCannotValidateTheQueuedObserver()
    {
        var state = WithLegend(VenUnitEntryBasicTests.Position("VEN·019"));
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") } };
        state = (await Act(state, "P1", new PlayCardCommand("UNIT", "VEN·019", [], Destination: "BASE"))).State;
        var history = state.CardPlays;
        var forged = history with {
            Finalized = history.Finalized.Select(e => e with { OriginHandPlayerId = null, OriginOutsideHands = false }).ToArray(),
            Resolved = history.Resolved.Select(e => e with { OriginHandPlayerId = null, OriginOutsideHands = false }).ToArray() };
        Assert.Contains("invalid card play observer stack", OfficialInsightAndSpellLockTests.Errors(state with { CardPlays = forged }));
    }

    [Fact]
    public async Task AnOpponentOwnedCardPlayedFromTheirDeckBelongsToTheActingPlayer()
    {
        var initial = AttackDamageDistributionTests.Spell(TurnUnitPlayGrantsTests.Position(TurnUnitPlayGrantsTests.Confront),
            "IMPULSE", "OGN·025/298", "P1");
        initial = WithLegend(initial);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["OPPONENT_TOP"] = new("OPPONENT_TOP", cardNo: "OGN·096/298", power: 1, manaCost: 2,
                    ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["OPPONENT_TOP"] = new("P2", "MAIN_DECK") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { MainDeck = initial.PlayerZones["P2"].MainDeck.Prepend("OPPONENT_TOP").ToArray() } } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("IMPULSE", "OGN·025/298", []));
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OPPONENT_TOP"]));
        await game.Act("P1", new PlayCardCommand("OPPONENT_TOP", "OGN·096/298", []));
        var play = Assert.Single(game.State.CardPlays.Resolved, p => p.Source.ObjectId == "OPPONENT_TOP");
        Assert.Equal("P1", play.PlayerId); Assert.True(play.OriginOutsideHands);
        Assert.Equal("P2", game.State.CardObjects["OPPONENT_TOP"].OwnerId);
        Assert.NotNull(Assert.Single(game.State.StackItems).OutsideHandPlayObserver);
        await game.Top();
        Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task EndStepDeckPlayResolvesTheEarnedObserverBeforeAdvancingTheTurn()
    {
        var initial = WithLegend(EndDeckPlayTests.Position());
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new EndTurnCommand());
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("END_UNIT", "OGN·096/298", []));
        Assert.Equal("P1", game.State.TurnPlayerId);
        Assert.NotNull(Assert.Single(game.State.StackItems).OutsideHandPlayObserver);
        ArmamentMightTests.PublicRecovery(game.State);
        await game.Top();
        Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.Equal("P2", game.State.TurnPlayerId);
        await game.Replay();
    }

    [Fact]
    public async Task TheGrantedAssaultChangesActualAttackingMightByTwo()
    {
        var initial = WithLegend(AttackDamageDistributionTests.Position("OGN·001/298"), empowered: true);
        var withoutGrant = await AttackDamageDistributionTests.Attack(initial);
        var baselineBattle = await CombatTestDriver.FinishAsync(withoutGrant);
        var baseline = Convert.ToInt32(CombatTestDriver.DamageFrom(baselineBattle, "HOST").Payload["combatPower"]);
        var activated = await Act(initial, "P1", new LegendActCommand("LEGEND", Ability, ["HOST"], []));
        var granted = await Top(activated.State);
        var attack = await AttackDamageDistributionTests.Attack(granted.State);
        Assert.True(attack.State.BattleState.IsActive);
        ArmamentMightTests.PublicRecovery(attack.State);
        var battle = await CombatTestDriver.FinishAsync(attack);
        var damage = CombatTestDriver.DamageFrom(battle, "HOST");
        Assert.Equal(baseline + 2, Convert.ToInt32(damage.Payload["combatPower"]));
    }

    [Fact]
    public async Task AFaceDownStandbyUnitTriggersOnlyWhenItIsActuallyPlayed()
    {
        var initial = WithLegend(StandbySourceRulesTests.Position("OGN·135/298"));
        var hidden = await Act(initial, "P1", new HideCardCommand("SHADOW", "OGN·135/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.Empty(hidden.State.CardPlays.Resolved);
        Assert.DoesNotContain(hidden.State.StackItems, i => i.OutsideHandPlayObserver is not null);
        var ready = await StandbySourceRulesTests.ReadyShadow("OGN·135/298", initial);
        var played = await Act(ready, "P1", new PlayCardCommand("SHADOW", "OGN·135/298", []));
        var play = Assert.Single(played.State.CardPlays.Resolved, p => p.Source.ObjectId == "SHADOW");
        Assert.True(play.OriginOutsideHands);
        Assert.Contains(played.State.StackItems, i => i.OutsideHandPlayObserver is not null);
        ArmamentMightTests.PublicRecovery(played.State);
        var done = await Top(played.State);
        Assert.True(done.State.CardObjects["LEGEND"].IsEmpowered);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task EarnedTriggerDoesNotExposeItsSourceAfterTheSourceReturnsToHand()
    {
        var initial = WithLegend(VenFlowCombatKeywordTests.Position(true));
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = [], Battlefields = [..initial.PlayerZones["P1"].Battlefields, "LEGEND"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LEGEND"] = initial.CardObjects["LEGEND"] with { Tags = [CardObjectTags.UnitCard] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["LEGEND"] = new("P1", "BATTLEFIELD", "F") } };
        initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "OGN·169/298", "P2");
        var cast = await Act(initial, "P1", new PlayCardCommand("SPELL", "VEN·012", ["ALLY"]));
        var completed = await Top(cast.State);
        Assert.NotNull(Assert.Single(completed.State.StackItems).OutsideHandPlayObserver);
        var passed = await Act(completed.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RETURN", "OGN·169/298", ["LEGEND"]));
        var returned = await Top(response.State);
        Assert.Contains("LEGEND", returned.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"LEGEND\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(returned.State)["P2"].Stack));
        ArmamentMightTests.PublicRecovery(returned.State);
        var done = await Top(returned.State);
        Assert.DoesNotContain("\"LEGEND\"", JsonSerializer.Serialize(done.Events));
        Assert.False(done.State.CardObjects["LEGEND"].IsEmpowered);
        ArmamentMightTests.PublicRecovery(done.State);
    }
}
