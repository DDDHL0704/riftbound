using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SpriteQueenBeginningTests
{
    private const string Queen = "UNL-084/219";
    private static MatchState Position(string zone = "BASE", string controller = "P1", bool stolen = false)
    {
        var state = RadGearTestPosition.Put(OfficialHoldSequenceTests.State("OGN·294/298"), "QUEEN", Queen, controller,
            zone, zone == "BATTLEFIELD" ? "F" : null);
        for (var i = 0; i < 6; i++) state = RadGearTestPosition.Put(state, "P2-DECK-" + i, "OGN·219/298", "P2", "MAIN_DECK");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["QUEEN"] = state.CardObjects["QUEEN"] with { OwnerId = stolen ? "P2" : controller } } };
    }
    private static CardObjectState[] Sprites(MatchState state)
        => state.CardObjects.Values.Where(card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.FaerieTokenCardNo).ToArray();
    private static async Task Open(Driver game) { await game.Act("P1", new PassPriorityCommand()); }

    [Theory]
    [InlineData("BASE", "P1", false, true)] [InlineData("BATTLEFIELD", "P1", false, true)]
    [InlineData("BASE", "P1", true, true)] [InlineData("BASE", "P2", false, false)]
    [InlineData("HAND", "P1", false, false)] [InlineData("GRAVEYARD", "P1", false, false)]
    public async Task OnlyAPublicSourceControlledByTheStartingPlayerEarnsTheBeforeScoringSkill(string zone, string controller, bool stolen, bool earned)
    {
        var game = new Driver(Position(zone, controller, stolen)); await Open(game);
        Assert.Empty(Sprites(game.State));
        if (earned) {
            var skill = Assert.Single(game.State.StackItems); Assert.Equal(Queen, skill.CardNo); Assert.Equal("P1", skill.ControllerId);
            Assert.Equal("SCORE", game.State.TurnStartStep); Assert.Equal(0, game.State.PlayerScores.GetValueOrDefault("P1"));
            await game.Top(); var sprite = Assert.Single(Sprites(game.State));
            Assert.Equal(3, sprite.Power); Assert.False(sprite.IsExhausted); Assert.Contains(CardObjectTags.Ephemeral, sprite.Tags);
            Assert.Contains(sprite.ObjectId, game.State.PlayerZones["P1"].Base);
        } else Assert.Empty(game.State.StackItems);
        Assert.Equal(MatchPhases.Main, game.State.Phase); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceBeforeResolutionPreservesTheEarnedRewardAndHidesItsIdentity()
    {
        var game = new Driver(Position()); await Open(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["QUEEN"])); await game.Top();
        Assert.Contains("QUEEN", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("QUEEN", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var prefix = game.Events().Length; await game.Top(); var sprite = Assert.Single(Sprites(game.State));
        Assert.DoesNotContain("QUEEN", sprite.ObjectId);
        Assert.DoesNotContain("QUEEN", JsonSerializer.Serialize(game.Events().Skip(prefix).Where(entry => entry.Event.Kind == "UNIT_TOKEN_CREATED")));
        await game.Replay();
    }

    [Fact]
    public async Task TheNextActualOwnStartDestroysTheOldEphemeralBeforeScoringAndEarnsAFreshReadySprite()
    {
        var game = new Driver(Position()); await Open(game); await game.Top();
        var old = Assert.Single(Sprites(game.State)).ObjectId;
        ResolutionResult Current() => new(true, null, game.State, [],
            ResolutionResult.BuildSnapshots(game.State), ResolutionResult.BuildPrompts(game.State));
        async ValueTask<ResolutionResult> Submit(string player, GameCommand command) {
            await game.Act(player, command); return Current();
        }
        await game.Act("P1", new EndTurnCommand()); await TurnSequenceTestDriver.Complete(Current(), Submit);
        Assert.Contains(old, game.State.CardObjects.Keys);
        await game.Act("P2", new EndTurnCommand()); await TurnSequenceTestDriver.Complete(Current(), Submit);
        Assert.DoesNotContain(old, game.State.CardObjects.Keys);
        var fresh = Assert.Single(Sprites(game.State)); Assert.NotEqual(old, fresh.ObjectId); Assert.False(fresh.IsExhausted);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "UNIT_TOKEN_CREATED"));
        Assert.Equal(MatchPhases.Main, game.State.Phase); Assert.Equal("P1", game.State.TurnPlayerId); await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("controller")]
    public async Task RecoveryRequiresTheOriginalAcceptedBeginningCapture(string change)
    {
        var game = new Driver(Position()); await Open(game); var skill = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = change == "erase" ? [] : [skill with { ControllerId = "P2" }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Top(); await game.Replay();
    }
}
