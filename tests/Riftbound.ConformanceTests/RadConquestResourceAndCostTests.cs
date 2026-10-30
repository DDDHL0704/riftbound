using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadConquestResourceAndCostTests
{
    private const string Fledgling = "RAD·076/167";
    private const string Swarm = "RAD·080/167";
    private static PlayCardCommand Play => new("UNIT", Swarm, [], Destination: "BASE");

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(Swarm);
        state = HandDiscardObserverTests.Put(state, "SECOND", "OGN·219/298", "P1", "BASE");
        return state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key, p => p.Key is "BF" or "OTHER"
            ? p.Value with { ControllerId = null } : p.Value) };
    }

    private static int Quote(MatchState state)
    {
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, Play));
        Assert.NotNull(quote.Cost); return quote.Cost.Mana;
    }

    private static async Task Conquer(Driver game, string unit, string field, string player = "P1")
    {
        await game.Act(player, new MoveUnitCommand(unit, "BASE", "BATTLEFIELD:" + field));
        await game.Act(player, new PassFocusCommand());
        await game.Act(player == "P1" ? "P2" : "P1", new PassFocusCommand());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task SwarmPreviewAndActualPaymentCountThisPlayersDistinctConqueredFields(int conquered)
    {
        var game = new Driver(Position());
        if (conquered > 0) await Conquer(game, "ALLY", "BF");
        if (conquered > 1) await Conquer(game, "SECOND", "OTHER");
        var expected = 8 - 2 * conquered;
        Assert.Equal(conquered, game.State.BattlefieldConquests?.Entries.Count ?? 0);
        Assert.Equal(expected, Quote(game.State));
        var mana = game.State.RunePools["P1"].Mana; var power = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", Play);
        Assert.Equal(mana - expected, game.State.RunePools["P1"].Mana);
        Assert.Equal(power - 2, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task SwarmCannotBorrowTheOpponentsConquests()
    {
        var game = new Driver(Position() with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await Conquer(game, "ENEMY", "OTHER", "P2");
        Assert.Single(game.State.BattlefieldConquests!.Entries);
        // Isolate the price from the separate turn-timing restriction.
        Assert.Equal(8, Quote(game.State with { TurnPlayerId = "P1", ActivePlayerId = "P1" }));
        await game.Replay();
    }

    [Fact]
    public async Task SwarmConquestDiscountExpiresEvenWhileThePlayerStillControlsTheField()
    {
        var game = new Driver(Position()); await Conquer(game, "ALLY", "BF"); Assert.Equal(6, Quote(game.State));
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.CardObjects["BF"].ControllerId);
        Assert.Equal(8, Quote(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData(5, 2)] [InlineData(6, 1)]
    public async Task SwarmStillRequiresBothDiscountedManaAndItsPrintedPower(int mana, int power)
    {
        var game = new Driver(Position()); await Conquer(game, "ALLY", "BF");
        var state = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P1"] = new(mana, power) } };
        Assert.False(new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, Play)).CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("poor-swarm", "P1", Play.CmdType), Play, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task OnlyFledglingsAtTheConqueredFieldGainPowerImmediatelyWithoutResponses(int participants)
    {
        var state = Position();
        foreach (var id in new[] { "ALLY", "SECOND" })
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, Fledgling, "P1") } };
        var game = new Driver(state); var power = state.RunePools["P1"].Power; var mana = state.RunePools["P1"].Mana;
        if (participants == 0)
        {
            state = HandDiscardObserverTests.Put(state, "ORDINARY", "OGN·219/298", "P1", "BASE");
            game = new Driver(state); await Conquer(game, "ORDINARY", "BF");
        }
        else
        {
            var moving = participants == 1 ? new[] { "ALLY" } : new[] { "ALLY", "SECOND" };
            await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", [], moving));
            Assert.Equal(power, game.State.RunePools["P1"].Power);
            await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        }
        Assert.Equal(power + participants, game.State.RunePools["P1"].Power); Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(participants, game.Events().Count(e => e.Event.Kind == "POWER_GAINED"));
        Assert.All(game.Events().Where(e => e.Event.Kind == "POWER_GAINED"), e => Assert.Equal(true, e.Event.Payload["resourceSkill"]));
        await game.Replay();
    }

    [Fact]
    public async Task RejectedFledglingMovementCannotGenerateAResource()
    {
        var state = Position() with { CardObjects = Position().CardObjects.ToDictionary(p => p.Key, p => p.Key == "ALLY"
            ? OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", Fledgling, "P1") with { IsExhausted = true } : p.Value) };
        var command = new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF");
        var result = await new CoreRuleEngine().ResolveAsync(state, new("sleeping-fledgling", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task BothOfficialTextsProduceSharedSpecs()
    {
        var cards = (await OfficialCardCatalog.LoadRuntimeAsync()).Cards;
        var resource = Assert.Single(TriggerParser.Parse(cards.Single(c => c.CardNo == Fledgling).CardEffect));
        Assert.Equal(TriggerKinds.UnitConquestGainResources, resource.Kind); Assert.Equal(1, resource.GeneratedPower); Assert.Equal(0, resource.ManaDelta);
        var discount = Assert.Single(StaticAbilityParser.Parse(cards.Single(c => c.CardNo == Swarm).CardEffect, []));
        Assert.Equal(StaticAbilityKinds.SourceCostReductionPerConqueredBattlefieldThisTurn, discount.Kind); Assert.Equal(2, discount.Amount);
    }
}
