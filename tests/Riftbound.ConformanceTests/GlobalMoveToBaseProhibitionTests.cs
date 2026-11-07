using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// SFD·014 official CN text; core 421 / 448-451 movement and 458.3 recall.
public sealed class GlobalMoveToBaseProhibitionTests
{
    private const string Reckoner = "SFD·014/221";

    [Fact]
    public async Task PlayingReckonerAppliesTheRestrictionAndReturningItToHandImmediatelyRemovesIt()
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Reckoner, [], Destination: "BASE"));
        Assert.Equal(15, game.State.RunePools["P1"].Mana);
        Assert.Empty(game.State.StackItems); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        await RejectMove(game.State, "P1", "ALLY");
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates ?? [], c =>
            c.Action == CommandTypes.MoveUnit && c.Enabled && c.Sources?.Any(s => s.Id == "ALLY") == true);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["UNIT"].Zone);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BATTLEFIELD", "BASE", []));
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone); await game.Replay();
    }

    [Theory]
    [InlineData("P1", "BASE")] [InlineData("P1", "BATTLEFIELD")]
    [InlineData("P2", "BASE")] [InlineData("P2", "BATTLEFIELD")]
    public async Task AVisibleSourceAtAnyLocationRestrictsUnitsOfEitherController(string controller, string zone)
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "UNIT", controller, zone, zone == "BATTLEFIELD" ? "BF" : null);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { OwnerId = controller, ControllerId = controller } } };
        await RejectMove(state, "P1", "ALLY");
        await RejectMove(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" }, "P2", "ENEMY");
    }

    [Theory]
    [InlineData("HAND")] [InlineData("GRAVEYARD")] [InlineData("FACE_DOWN")]
    public async Task ASourceOutsideTheFieldOrFaceDownDoesNotProhibitMovement(string zone)
    {
        var state = Position();
        if (zone == "GRAVEYARD") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "GRAVEYARD");
        if (zone == "FACE_DOWN") {
            state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "BATTLEFIELD", "BF");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = state.CardObjects["UNIT"] with { IsFaceDown = true } } };
        }
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BATTLEFIELD", "BASE", []));
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MoveEffectsKeepTheirLegalTargetButObserveTheCurrentProhibitionOnResolution(bool removeSource)
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "UNIT", "P1", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "MOVE", "OGN·168/298", "P1");
        if (removeSource) state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("MOVE", "OGN·168/298", ["ALLY"]));
        if (removeSource) {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        }
        await game.Top();
        Assert.Equal(removeSource ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(removeSource ? 1 : 0, game.Events().Count(e => e.Event.Kind == "UNIT_MOVED_TO_BASE"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY")));
        await game.Replay();
    }

    [Fact]
    public async Task ARecallStillTransfersAUnitToTheNewControllersBase()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "UNIT", "P1", "BASE");
        var game = new Driver(AttackDamageDistributionTests.Spell(state, "RECALL", "OGN·203/298", "P1"));
        await game.Act("P1", new PlayCardCommand("RECALL", "OGN·203/298", ["ENEMY"])); await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.Equal("P1", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.Contains("ENEMY", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BASE");
        await game.Replay();
    }

    private static MatchState Position()
    {
        var state = RadUnitEffectTestFixture.Place(VenUnitEntryBasicTests.Position(Reckoner), "ALLY", "P1", "BATTLEFIELD", "BF");
        return RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
    }
    private static async Task RejectMove(MatchState state, string player, string source)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("blocked-global-base", player, CommandTypes.MoveUnit),
            new MoveUnitCommand(source, "BATTLEFIELD", "BASE", []), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
