using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class KenkenConditionalEntryTests
{
    private const string Unit = "UNL-035/219", Stun = "OGN·050/298";
    private static MatchState Position()
    {
        var s = RadGearTestPosition.Put(RadGearTestPosition.Create(), "UNIT", Unit, "P1", "HAND");
        s = RadGearTestPosition.Put(s, "STUN", Stun, "P1", "HAND");
        return RadGearTestPosition.Put(s, "RETURN", "OGN·104/298", "P1", "HAND");
    }
    private static PlayCardCommand Play(string destination = "BASE") => new("UNIT", Unit, [], Destination: destination);
    private static PlayCostQuoteDto Quote(MatchState state, PlayCardCommand? command = null)
    {
        command ??= Play();
        return new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
    }
    private static async Task StunEnemy(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("STUN", Stun, ["ENEMY"])); await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task ARealEnemyStunReducesTheQuoteAndActualManaByTwoAndEntersReady(string destination)
    {
        var s = Position();
        if (destination != "BASE") s = RadUnitEffectTestFixture.Place(s, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(s); Assert.Equal(6, Quote(game.State).Cost!.Mana);
        await StunEnemy(game); var play = Play(destination);
        Assert.Equal(4, Quote(game.State, play).Cost!.Mana);
        var mana = game.State.RunePools["P1"].Mana; await game.Act("P1", play);
        Assert.Equal(mana - 4, game.State.RunePools["P1"].Mana);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", "P2", true)] [InlineData("P2", "P1", true)] [InlineData("P2", "P2", false)]
    public async Task TheStunnedUnitsCurrentControllerRatherThanOwnerSuppliesBothConditions(string controller,
        string owner, bool stunned)
    {
        var s = RadGearTestPosition.Put(Position(), "COUNTED", "OGN·219/298", controller, "BASE");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["COUNTED"] = s.CardObjects["COUNTED"] with { OwnerId = owner,
                UntilEndOfTurnEffects = stunned ? ["STUNNED"] : [] } } };
        var applies = controller == "P2" && stunned; var game = new Driver(s);
        Assert.Equal(applies ? 4 : 6, Quote(game.State).Cost!.Mana);
        var mana = game.State.RunePools["P1"].Mana; await game.Act("P1", Play());
        Assert.Equal(mana - (applies ? 4 : 6), game.State.RunePools["P1"].Mana);
        Assert.Equal(!applies, game.State.CardObjects["UNIT"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task SeveralStunnedEnemiesStillGiveOnlyOneTwoManaReduction()
    {
        var s = RadGearTestPosition.Put(Position(), "SECOND-STUN", Stun, "P1", "HAND");
        s = RadGearTestPosition.Put(s, "SECOND-ENEMY", "OGN·219/298", "P2", "BASE");
        var game = new Driver(s); await StunEnemy(game);
        await game.Act("P1", new PlayCardCommand("SECOND-STUN", Stun, ["SECOND-ENEMY"])); await game.Top();
        Assert.Equal(4, Quote(game.State).Cost!.Mana); await game.Act("P1", Play());
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheOnlyStunnedEnemyInvalidatesTheOldPriceAndReadyEntry()
    {
        var s = RadGearTestPosition.Put(Position(), "RETURN", "UNL-128/219", "P1", "HAND");
        var game = new Driver(s); await StunEnemy(game); Assert.Equal(4, Quote(game.State).Cost!.Mana);
        await game.Act("P1", new PlayCardCommand("RETURN", "UNL-128/219", ["ALLY", "ENEMY"])); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand); Assert.Equal(6, Quote(game.State).Cost!.Mana);
        var mana = game.State.RunePools["P1"].Mana; await game.Act("P1", Play());
        Assert.Equal(mana - 6, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task ExpiringTheRealStunRestoresTheSixManaPrice()
    {
        var game = new Driver(Position()); await StunEnemy(game); Assert.Equal(4, Quote(game.State).Cost!.Mana);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Equal(6, Quote(game.State).Cost!.Mana); await game.Replay();
    }

    [Fact]
    public async Task HavingTheConditionDoesNotPermitPayingLessThanTheDiscountedCost()
    {
        var s = Position(); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY"] = s.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = ["STUNNED"] } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(3, 30) } };
        Assert.Equal(4, Quote(s).Cost!.Mana); Assert.False(Quote(s).CanPay);
        await RadGearTestPosition.Reject(s, "P1", Play());
    }

    [Fact]
    public async Task ARealGraveyardEffectPlayUsesTheSameDiscountAndReadyEntry()
    {
        var s = RadUnitEffectTestFixture.Place(Position(), "UNIT", "P1", "GRAVEYARD");
        s = RadGearTestPosition.Put(s, "RITUAL", "SFD·150/221", "P1", "BASE");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["RITUAL"] = s.CardObjects["RITUAL"] with { AttachedToObjectId = "ALLY" } } };
        var game = new Driver(s); await StunEnemy(game);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        await game.Top(); Assert.NotNull(game.State.PendingEffectPlay);
        Assert.Equal(4, Quote(game.State).Cost!.Mana);
        var mana = game.State.RunePools["P1"].Mana; await game.Act("P1", Play());
        Assert.Equal(mana - 4, game.State.RunePools["P1"].Mana);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted); await game.Replay();
    }
}
