using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ArmamentMovementTokenTests
{
    private const string Gear = "SFD·153/221", Wind = "OGN·173/298";

    private static MatchState Position(bool attached = true, bool field = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "GEAR", Gear, "P1", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "WIND", Wind, "P1");
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "BLINK", "OGS·011/024", "P1");
        if (field) state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        return attached ? state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } } : state;
    }

    private static CardObjectState[] Minions(MatchState state)
        => state.CardObjects.Values.Where(card => card.TokenFactoryCardNo == "OGN·271/298").ToArray();

    private static async Task Move(Driver game, string destination = "BATTLEFIELD:BF")
    {
        await game.Act("P1", new PlayCardCommand("WIND", Wind, ["ALLY"], Destination: destination)); await game.Top();
    }

    [Theory]
    [InlineData(false, "BATTLEFIELD:BF")] [InlineData(true, "BASE:P1")] [InlineData(true, "BATTLEFIELD:OTHER")]
    public async Task ActualEffectMovementEarnsAnIndependentOneMightMinionAtTheHostsCurrentLocation(bool field, string destination)
    {
        var game = new Driver(Position(field: field)); await Move(game, destination);
        Assert.Empty(Minions(game.State)); Assert.Equal(Gear, Assert.Single(game.State.StackItems).UnitEvent!.CardNo);
        await game.Top(); var token = Assert.Single(Minions(game.State));
        Assert.Equal(1, token.Power); Assert.True(token.IsExhausted); Assert.Equal("P1", token.OwnerId);
        Assert.Equal(game.State.ObjectLocations["ALLY"], game.State.ObjectLocations[token.ObjectId]); await game.Replay();
    }

    [Fact]
    public async Task AnotherPlayersEffectMovingTheHostStillEarnsItsControllersSkill()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(field: true), "THEIR-MOVE", "OGN·043/298", "P2");
        var game = new Driver(initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new PlayCardCommand("THEIR-MOVE", "OGN·043/298", ["ALLY"], Destination: "BASE:P1"));
        await game.Top(); Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        await game.Top(); var token = Assert.Single(Minions(game.State));
        Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheHostInResponseMakesItsCurrentHereUnavailableWithoutLeakingItsPrivateIncarnation()
    {
        var game = new Driver(Position()); await Move(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.DoesNotContain("ALLY", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Empty(Minions(game.State)); await game.Replay();
    }

    [Fact]
    public async Task AnUnavailableCurrentHereDoesNotOfferOrConsumeATokenReplacement()
    {
        var initial = RadGearTestPosition.Put(Position(), "ZILEAN", "UNL-086/219", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await Move(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top();
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); Assert.Empty(Minions(game.State));
        Assert.DoesNotContain("TOKEN_ENTRY_REPLACEMENT_USED", game.State.CardObjects["ZILEAN"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Fact]
    public async Task AnAvailableCurrentHereUsesTheSharedTokenReplacementBeforeAnyEntry()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "ZILEAN", "UNL-086/219", "P1", "BATTLEFIELD", "BF"));
        await Move(game); await game.Top(); Assert.Empty(Minions(game.State));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", choice.ChoiceWindow); Assert.Contains("ZILEAN", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ZILEAN"]));
        Assert.Equal(2, Minions(game.State).Length);
        Assert.All(Minions(game.State), token => Assert.Equal("BF", game.State.ObjectLocations[token.ObjectId].BattlefieldObjectId));
        Assert.Contains("TOKEN_ENTRY_REPLACEMENT_USED", game.State.CardObjects["ZILEAN"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Fact]
    public async Task ARealBlinkResponseMovesTheHostAndBothEarnedSkillsReadItsCurrentBase()
    {
        var game = new Driver(Position()); await Move(game);
        await game.Act("P1", new PlayCardCommand("BLINK", "OGS·011/024", ["ALLY"])); await game.Top();
        await game.Top(); await game.Top();
        var tokens = Minions(game.State); Assert.Equal(2, tokens.Length);
        Assert.All(tokens, token => Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base)); await game.Replay();
    }

    [Fact]
    public async Task AnUnattachedEquipmentDoesNotGrantItsLowerBoxToAMovingUnit()
    {
        var game = new Driver(Position(false)); await Move(game);
        Assert.Empty(game.State.StackItems); Assert.Empty(Minions(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("destination")] [InlineData("duplicate")]
    public async Task RecoveryRejectsErasedDuplicatedOrRewrittenMovementCaptures(string branch)
    {
        var game = new Driver(Position()); await Move(game); var item = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = branch switch {
            "erase" => [], "duplicate" => [item, item],
            _ => [item with { UnitEvent = item.UnitEvent! with { Here = new("P1", "BASE") } }] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
    }
}
