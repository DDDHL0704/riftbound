using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenZedSignatureSpellTests
{
    private const string CardNo = "VEN·144";

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HandAndFlowPayTheirIndependentCostsThenBurnBeforeCreatingOneShadow(bool flow)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == CardNo);
        Assert.Equal(37847, card.Id); Assert.Equal(2, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal(new[] { RuneTrait.Red, RuneTrait.Purple }.Order(), PrintedPowerCostRules.ForCard(CardNo).Traits.Order());
        var state = Position(flow);
        var command = Play(flow);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(flow ? 1 : 2, quote.Cost!.Mana);
        Assert.Equal(flow ? 2 : 0, quote.Cost.GenericPower);
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Equal(flow, Assert.Single(game.State.StackItems).FlowPlay is not null);
        Assert.Equal(flow ? 9 : 8, game.State.RunePools["P1"].Mana);
        Assert.Equal(flow ? 3 : 4, game.State.RunePools["P1"].PowerByTrait.Values.Sum());
        Assert.Equal(4, game.State.PlayerZones["P1"].MainDeck.Count); Assert.Empty(Shadows(game.State));
        await game.Top();
        Assert.Equal(new[] { "P1-D4" }, game.State.PlayerZones["P1"].MainDeck);
        Assert.All(new[] { "P1-D1", "P1-D2", "P1-D3" }, id => {
            Assert.Contains(id, game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(1, game.State.CardObjects[id].ObjectGeneration); });
        var shadow = Assert.Single(Shadows(game.State));
        Assert.Equal(0, shadow.Power); Assert.True(shadow.IsExhausted);
        Assert.Contains(shadow.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Contains("SPELL", flow ? game.State.PlayerZones["P1"].Banished : game.State.PlayerZones["P1"].Graveyard);
        var events = game.Events().Select(e => e.Event).ToArray();
        Assert.Equal(3, events.Where(e => e.Kind == "CARDS_BURNED").Sum(e => Convert.ToInt32(e.Payload["count"])));
        Assert.True(Array.FindLastIndex(events, e => e.Kind == "CARDS_BURNED") < Array.FindIndex(events, e => e.Kind == "UNIT_TOKEN_CREATED"));
        Assert.DoesNotContain(events, e => e.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BurnoutCanBurnTheSameCardThreeTimesAndNeverRecycleTheResolvingSpell(bool flow)
    {
        var game = new Driver(Position(flow, deckCount: 1));
        await game.Act("P1", Play(flow)); await game.Top();
        Assert.Equal(2, game.State.PlayerScores["P2"]);
        Assert.Equal(5, game.State.CardObjects["P1-D1"].ObjectGeneration);
        Assert.Contains("P1-D1", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.PlayerZones["P1"].MainDeck); Assert.Single(Shadows(game.State));
        Assert.Contains("SPELL", flow ? game.State.PlayerZones["P1"].Banished : game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ImmediateBurnoutVictoryStopsTheLaterTokenInstruction(bool flow)
    {
        var game = new Driver(Position(flow, deckCount: 0));
        await game.Act("P1", Play(flow)); await game.Top();
        Assert.Equal("P2", game.State.WinnerPlayerId); Assert.Equal(MatchStatuses.Finished, game.State.Status);
        Assert.Empty(Shadows(game.State));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_TOKEN_CREATED" or "CARDS_BURNED" or "CARD_DRAWN");
        Assert.Contains("SPELL", flow ? game.State.PlayerZones["P1"].Banished : game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, 2, 8, 3, true)] [InlineData(true, 2, 8, 3, true)]
    [InlineData(false, 1, 9, 2, false)] [InlineData(true, 1, 9, 2, false)]
    public async Task FirstBurnoutDefersVictoryUntilCleanupButASecondOneWinsImmediately(
        bool flow, int deckCount, int expectedScore, int burnedCount, bool createsShadow)
    {
        var state = Position(flow, deckCount) with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 } };
        var game = new Driver(state);
        await game.Act("P1", Play(flow)); await game.Top();
        Assert.Equal("P2", game.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.Finished, game.State.Status);
        Assert.Equal(expectedScore, game.State.PlayerScores["P2"]);
        Assert.Equal(createsShadow ? 1 : 0, Shadows(game.State).Length);
        Assert.Equal(burnedCount, game.Events().Where(e => e.Event.Kind == "CARDS_BURNED")
            .Sum(e => Convert.ToInt32(e.Event.Payload["count"])));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CounteringSkipsBothEffectsAndFlowStillBanishesTheSpell(bool flow)
    {
        var state = AttackDamageDistributionTests.Spell(Position(flow), "COUNTER", "OGN·045/298", "P2");
        var game = new Driver(state); await game.Act("P1", Play(flow));
        var spell = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [spell.StackItemId]));
        await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Empty(Shadows(game.State));
        Assert.Equal(4, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARDS_BURNED");
        Assert.Contains("SPELL", flow ? game.State.PlayerZones["P1"].Banished : game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, "mana")] [InlineData(true, "mana")]
    [InlineData(false, "power")] [InlineData(true, "power")]
    [InlineData(false, "target")] [InlineData(true, "target")]
    [InlineData(false, "turn")] [InlineData(true, "turn")]
    [InlineData(false, "color")]
    public async Task IllegalPlaysCannotSpendBurnOrCreateAnything(bool flow, string kind)
    {
        var state = Position(flow);
        if (kind is "mana" or "power" or "color") state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(kind == "mana" ? 0 : 10, 0,
                new Dictionary<string, int> { [kind == "color" || flow ? RuneTrait.Blue : RuneTrait.Red] = kind == "power" ? 0 : 5 }) } };
        if (kind == "turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var command = kind == "target" ? Play(flow) with { TargetObjectIds = ["P1-D1"] } : Play(flow);
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-zed-signature", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    private static PlayCardCommand Play(bool flow)
        => new("SPELL", CardNo, [], OptionalCosts: flow ? ["FLOW_COST:1:2:"] : []);
    private static CardObjectState[] Shadows(MatchState state)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo == "VEN·T05").ToArray();

    private static MatchState Position(bool flow, int deckCount = 4)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(1, player == "P1" ? deckCount : 4).Select(i => player + "-D" + i).ToArray();
            foreach (var id in deck) {
                cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·219/298", player);
                locations[id] = new(player, "MAIN_DECK"); }
            zones[player] = PlayerZones.Empty with { MainDeck = deck,
                Hand = player == "P1" && !flow ? ["SPELL"] : [], Graveyard = player == "P1" && flow ? ["SPELL"] : [] };
        }
        cards["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", CardNo, "P1");
        locations["SPELL"] = new("P1", flow ? "GRAVEYARD" : "HAND");
        return new MatchState("VEN-ZED-SIGNATURE", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> {
                ["P1"] = new(10, 0, new Dictionary<string, int> { [flow ? RuneTrait.Blue : RuneTrait.Red] = 5 }),
                ["P2"] = new(20, 20) }, seed: 424242) with { CardPlays = new(5, [], []) };
    }
}
