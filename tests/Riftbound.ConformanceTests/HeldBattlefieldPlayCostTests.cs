using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class HeldBattlefieldPlayCostTests
{
    private const string Giant = "SFD·055/221";

    internal static MatchState Position(int heldFields, bool opponentTurn = true, string fieldCard = "OGN·296/298")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(fieldCard), "GIANT", Giant, "P1", "HAND");
        for (var i = 0; i < heldFields; i++)
        {
            var field = i == 0 ? "BF" : i == 1 ? "OTHER" : "HELD" + i;
            if (i == 1) state = RadUnitEffectTestFixture.Place(state, field, "P1", "BATTLEFIELD", field);
            if (i > 1) state = RadGearTestPosition.Put(state, field, "OGN·296/298", "P1", "BATTLEFIELD", field);
            state = RadGearTestPosition.Put(state, "HOLDER" + i, "OGN·219/298", "P1", "BATTLEFIELD", field);
        }
        for (var i = 0; i < 12; i++) state = RadGearTestPosition.Put(state, "GREEN" + i, "OGN·042/298", "P1", "BASE");
        return state with {
            TurnPlayerId = opponentTurn ? "P2" : "P1", ActivePlayerId = opponentTurn ? "P2" : "P1",
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty }
        };
    }

    private static PlayCostQuoteDto Quote(MatchState state, PlayCardCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.Equal(before, MatchStateHasher.Hash(state));
        return quote;
    }

    [Theory]
    [InlineData(0, 10, 3)] [InlineData(1, 8, 2)]
    [InlineData(2, 6, 1)] [InlineData(3, 4, 0)]
    public async Task NaturalHoldingReducesTheSameManaAndGreenInQuotesAndRealPayments(int held, int mana, int green)
    {
        var game = new Driver(Position(held, opponentTurn: held > 0));
        if (held > 0) await game.Act("P2", new EndTurnCommand());
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Equal(held, game.State.BattlefieldHolds?.Entries.Count(entry => entry.PlayerId == "P1") ?? 0);
        var costs = Enumerable.Range(0, green).Select(i => "RECYCLE_RUNE:GREEN" + i).ToArray();
        var command = new PlayCardCommand("GIANT", Giant, [], OptionalCosts: costs);
        var underfunded = Quote(game.State, command);
        Assert.False(underfunded.CanPay);
        Assert.Equal(mana, underfunded.Cost!.Mana);
        Assert.Equal(green, underfunded.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Green));
        await RadGearTestPosition.Reject(game.State, "P1", command);
        await game.TapRunes("P1", Enumerable.Range(0, mana).Select(i => "GREEN" + i));
        var quote = Quote(game.State, command);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(green, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Green));
        Assert.Equal(0, quote.Cost.GenericPower);
        await game.Act("P1", command);
        Assert.Contains("GIANT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(10, game.State.CardObjects["GIANT"].ManaCost);
        Assert.Equal(5, game.State.CardObjects["GIANT"].Power);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.All(Enumerable.Range(0, green), i => Assert.Contains("GREEN" + i, game.State.PlayerZones["P1"].RuneDeck));
        await game.Replay();
    }

    [Fact]
    public async Task APreviousTurnsHoldOrAnotherPlayersHoldDoesNotReduceTheCost()
    {
        var game = new Driver(Position(1)); await game.Act("P2", new EndTurnCommand());
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Giant, out var behavior));
        Assert.Equal(8, CoreRuleEngine.CalculatePlayManaCost(game.State, "P1", behavior, sourceObjectId: "GIANT").Total);
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.BattlefieldHolds?.Entries ?? [], entry => entry.PlayerId == "P1");
        Assert.Equal(10, CoreRuleEngine.CalculatePlayManaCost(game.State, "P1", behavior, sourceObjectId: "GIANT").Total);
        await game.Replay();
    }

    [Fact]
    public async Task TheMonumentsEntirelyPreventedScoringActionDoesNotCountAsAHoldForTheDiscount()
    {
        var initial = Position(1, fieldCard: "SFD·209/221") with { TurnNumber = 2, CardPlays = new(2, [], []) };
        var game = new Driver(initial); await game.Act("P2", new EndTurnCommand());
        Assert.Null(game.State.BattlefieldHolds);
        Assert.Equal(0, game.State.PlayerScores["P1"]);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Giant, out var behavior));
        Assert.Equal(10, CoreRuleEngine.CalculatePlayManaCost(game.State, "P1", behavior, sourceObjectId: "GIANT").Total);
        await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("append")] [InlineData("field")] [InlineData("turn")]
    public async Task AReprojectedDiscountStillRequiresTheOriginalAcceptedHoldingHistory(string change)
    {
        var game = new Driver(Position(1)); await game.Act("P2", new EndTurnCommand());
        var holds = Assert.IsType<TurnBattlefieldHolds>(game.State.BattlefieldHolds);
        var forged = game.State with { BattlefieldHolds = change switch {
            "erase" => null,
            "append" => holds with { Entries = [..holds.Entries, new("P2", new("BF", game.State.CardObjects["BF"].ObjectGeneration))] },
            "field" => holds with { Entries = [new("P1", new("OTHER", game.State.CardObjects["OTHER"].ObjectGeneration))] },
            _ => holds with { TurnNumber = holds.TurnNumber + 1 }
        } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Replay();
    }
}
