using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class CardBoonObserverTests
{
    private const string Explorer = "SFD·100/221", Dragon = "SFD·101/221";
    private const string Consume = "OGN·146/298", Bulk = "OGN·153/298";

    private static MatchState Position(string observer)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "OBSERVER", observer, "P1", "BASE");
        return RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
    }

    private static MatchState Boon(MatchState state, string id)
    {
        Assert.True(BoonRules.TryGrant(state.CardObjects[id], state.PlayerZones, state.CardObjects, out var granted));
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = granted } };
    }

    private static CardObjectState[] Gold(MatchState state)
        => state.CardObjects.Values.Where(card => card.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币").ToArray();

    private static string[] Costs(MatchState state, string no, string source)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var definition));
        var choices = CoreRuleEngine.BoonPlayCostChoices(state, "P1", definition).Where(choice => choice.ObjectIds?.Contains(source) == true)
            .Select(choice => choice.Id).ToArray();
        Assert.NotEmpty(choices); return choices;
    }

    private static async Task Order(Driver game)
    {
        var prompt = ResolutionResult.BuildPrompts(game.State).Values.FirstOrDefault(p =>
            p.Candidates?.Any(c => c.Action == CommandTypes.OrderTriggers && c.Enabled) == true);
        if (prompt is not null) await game.Act(prompt.PlayerId, new OrderTriggersCommand(OrderedTriggerIds:
            game.State.TriggerQueue.Where(t => t.ControllerId == prompt.PlayerId).Select(t => t.TriggerId).ToArray()));
    }

    [Theory]
    [InlineData("SFD·055/221", true)] [InlineData("OGN·160/298", true)]
    [InlineData(Bulk, true)] [InlineData("OGN·219/298", false)]
    public async Task PrintedPowerCostAcrossUnitsEquipmentAndSpellsControlsTheIndependentDraw(string no, bool draws)
    {
        var game = new Driver(RadGearTestPosition.Put(Position(Explorer), "PLAY", no, "P1", "HAND"));
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        await game.Act("P1", new PlayCardCommand("PLAY", no, []));
        if (no == Bulk)
        {
            Assert.DoesNotContain(game.State.StackItems, item => item.UnitEvent?.CardNo == Explorer);
            await game.Top();
        }
        if (draws)
        {
            var skill = Assert.Single(game.State.StackItems); Assert.Equal(Explorer, skill.UnitEvent!.CardNo);
            Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck.Count);
            await game.Top();
        }
        else Assert.Empty(game.State.StackItems);
        Assert.Equal(deck - (draws ? 1 : 0), game.State.PlayerZones["P1"].MainDeck.Count); await game.Replay();
    }

    [Fact]
    public async Task ACounteredHighPowerSpellDoesNotCompleteAPlayForTheExplorer()
    {
        var state = RadGearTestPosition.Put(Position(Explorer), "PLAY", Bulk, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "COUNTER", "OGN·064/298", "P2", "HAND");
        var game = new Driver(state); var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        await game.Act("P1", new PlayCardCommand("PLAY", Bulk, [])); var spell = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell.StackItemId])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck.Count); await game.Replay();
    }

    [Fact]
    public async Task AnEquipmentResourceSkillIsNotAQualifyingCardPlay()
    {
        var state = RadGearTestPosition.Put(Position(Explorer), "GOLD", "SFD·033/221", "P1", "BASE");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("SFD·T03", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GOLD"] = factory.CreateObject("GOLD", "P1", "P1") } };
        var game = new Driver(state); var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        var ability = P4ActivatedAbilityCatalog.GetAll().Single(a => a.SourceCardNo == "SFD·T03" && a.IsResourceSkill);
        await game.Act("P1", new ActivateAbilityCommand("GOLD", ability.AbilityId, []));
        Assert.Empty(game.State.StackItems); Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck.Count);
        await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsCardIsNotYourQualifyingCardPlay()
    {
        var state = RadGearTestPosition.Put(Position(Explorer), "OPPONENT-PLAY", "SFD·055/221", "P2", "HAND");
        var game = new Driver(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        await game.Act("P2", new PlayCardCommand("OPPONENT-PLAY", "SFD·055/221", []));
        Assert.DoesNotContain(game.State.StackItems, item => item.UnitEvent?.CardNo == Explorer);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck.Count); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheExplorerAfterCaptureLeavesItsEarnedDrawIndependent()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(Explorer), "PLAY", "SFD·055/221", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("PLAY", "SFD·055/221", []));
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["OBSERVER"])); await game.Top();
        Assert.Contains("OBSERVER", game.State.PlayerZones["P1"].Hand); await game.Top();
        Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count); await game.Replay();
    }

    [Fact]
    public async Task AUnitPlayedByAnEffectStillUsesItsPrintedPowerCostWhenTheBaseCostIsWaived()
    {
        var state = RadGearTestPosition.Put(Position(Explorer), "GEAR", "SFD·090/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "ALLY", "SFD·055/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "KILL", "OGN·229/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "ALLY", ["ASSEMBLE_BLUE"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["ALLY"]));
        await game.Top(); await Order(game);
        while (game.State.StackItems.Count > 0) { await game.Top(); await Order(game); }
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Banished);
        await game.Act("P1", new ActivateAbilityCommand("GEAR", "BANISH_SELF_PLAY_ALL_LINKED_UNITS", [])); await game.Top();
        var pool = game.State.RunePools["P1"]; var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        await game.Act("P1", new PlayCardCommand("ALLY", "SFD·055/221", []));
        Assert.Equal(pool, game.State.RunePools["P1"]); Assert.Equal(Explorer, Assert.Single(game.State.StackItems).UnitEvent!.CardNo);
        await game.Top(); Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count); await game.Replay();
    }

    [Theory]
    [InlineData(Consume)] [InlineData("OGN·207/298")]
    public async Task APlayedDragonRetainsItsBoonEntryAndObservesABoonPaidAsASpellCost(string spell)
    {
        var state = RadUnitEffectTestFixture.Place(Position(Dragon), "OBSERVER", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "SPELL", spell, "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("OBSERVER", Dragon, ["ALLY"])); await game.Top();
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["ALLY"]));
        await game.Act("P1", new PlayCardCommand("SPELL", spell, ["ALLY"], OptionalCosts: Costs(game.State, spell, "ALLY")));
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["ALLY"])); Assert.Empty(Gold(game.State));
        Assert.Equal(Dragon, game.State.StackItems[^1].UnitEvent!.CardNo); await game.Top();
        Assert.True(Assert.Single(Gold(game.State)).IsExhausted); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task ConsumingTwoBoonsDuringOneResolutionEarnsTwoIndependentGoldSkills()
    {
        var state = RadGearTestPosition.Put(Position(Dragon), "SECOND", "OGN·219/298", "P1", "BASE");
        state = Boon(Boon(state, "ALLY"), "SECOND"); state = RadGearTestPosition.Put(state, "SPELL", Bulk, "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("SPELL", Bulk, ["ALLY", "SECOND"]));
        Assert.Empty(Gold(game.State)); await game.Top();
        await Order(game);
        Assert.Equal(2, game.State.StackItems.Count(item => item.UnitEvent?.CardNo == Dragon));
        await game.Top(); await game.Top(); Assert.Equal(2, Gold(game.State).Length);
        Assert.All(Gold(game.State), card => Assert.True(card.IsExhausted)); await game.Replay();
    }

    [Fact]
    public async Task TheDragonStillEarnsGoldWhenLosingItsOwnBoonMakesMarkedDamageLethal()
    {
        var state = Boon(Position(Dragon), "OBSERVER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OBSERVER"] = state.CardObjects["OBSERVER"] with { Damage = 7 } } };
        state = RadGearTestPosition.Put(state, "SPELL", Consume, "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", Consume, ["ALLY"], OptionalCosts: Costs(game.State, Consume, "OBSERVER")));
        Assert.Contains("OBSERVER", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(Dragon, game.State.StackItems[^1].UnitEvent!.CardNo);
        await game.Top(); Assert.Single(Gold(game.State)); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheDragonAfterConsumptionDoesNotCancelItsGold()
    {
        var state = Boon(Position(Dragon), "ALLY"); state = RadGearTestPosition.Put(state, "SPELL", Consume, "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", Consume, ["ALLY"], OptionalCosts: Costs(game.State, Consume, "ALLY")));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["OBSERVER"])); await game.Top();
        Assert.Contains("OBSERVER", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Single(Gold(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·157/298", "CONSUME_BOON_READY_SELF")]
    [InlineData("OGN·164/298", "SOURCE-BOON-MIGHT:0")]
    public async Task BothPrintedActivationFamiliesCaptureBoonConsumptionBeforeResponses(string unit, string ability)
    {
        var state = RadGearTestPosition.Put(Position(Dragon), "PAYER", unit, "P1", "BASE"); state = Boon(state, "PAYER");
        var game = new Driver(state); await game.Act("P1", new ActivateAbilityCommand("PAYER", ability, []));
        Assert.Empty(Gold(game.State)); Assert.Equal(Dragon, game.State.StackItems[^1].UnitEvent!.CardNo);
        await game.Top(); Assert.Single(Gold(game.State)); await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsBoonConsumptionDoesNotGiveYourDragonGold()
    {
        var state = Boon(Position(Dragon), "ENEMY"); state = RadGearTestPosition.Put(state, "SPELL", Consume, "P2", "HAND");
        state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Consume, out var behavior));
        var costs = CoreRuleEngine.BoonPlayCostChoices(state, "P2", behavior).Select(choice => choice.Id).ToArray();
        var game = new Driver(state); await game.Act("P2", new PlayCardCommand("SPELL", Consume, ["ENEMY"], OptionalCosts: costs));
        await game.Top(); Assert.Empty(Gold(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData(Explorer, "erase")] [InlineData(Explorer, "capture")] [InlineData(Explorer, "duplicate")]
    [InlineData(Dragon, "erase")] [InlineData(Dragon, "capture")] [InlineData(Dragon, "duplicate")]
    public async Task RecoveryBindsObservedSkillsToTheirOriginalAcceptedCapture(string observer, string change)
    {
        var state = Position(observer);
        if (observer == Explorer) state = RadGearTestPosition.Put(state, "PLAY", "SFD·055/221", "P1", "HAND");
        else { state = Boon(state, "ALLY"); state = RadGearTestPosition.Put(state, "SPELL", Consume, "P1", "HAND"); }
        var game = new Driver(state);
        await game.Act("P1", observer == Explorer ? new PlayCardCommand("PLAY", "SFD·055/221", [])
            : new PlayCardCommand("SPELL", Consume, ["ALLY"], OptionalCosts: Costs(game.State, Consume, "ALLY")));
        var skill = game.State.StackItems.Single(item => item.UnitEvent?.CardNo == observer);
        var others = game.State.StackItems.Where(item => item.StackItemId != skill.StackItemId).ToArray();
        var forged = game.State with { StackItems = change switch {
            "erase" => others,
            "capture" => [..others, skill with { UnitEvent = skill.UnitEvent! with { CapturedTick = skill.UnitEvent.CapturedTick + 1 } }],
            _ => [..game.State.StackItems, skill]
        } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }
}
