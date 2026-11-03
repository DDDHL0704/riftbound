using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private const string WaymarkAbility = "FORGOTTEN_SIGNPOST_EXHAUST_MOVE_FRIENDLY_UNIT";

    private static MatchState WaymarkPosition(bool borrowed)
    {
        var state = BorrowedBombPosition("UNL-045/219");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = borrowed } } };
        return RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
    }

    private static ActivateAbilityCommand Waymark(MatchState state, bool borrowed, string target = "HOST", string cost = "ALLY")
        => new(borrowed ? "HOST" : "UNIT", borrowed ? TemplateId(state, "因此技能而休眠") : WaymarkAbility,
            [target], [CoreRuleEngine.ExhaustFriendlyUnitOptionalCostPrefix + cost]);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WaymarkPaysASeparateUnitCostThenMovesAnotherUnitAfterResponses(bool borrowed)
    {
        var initial = WaymarkPosition(borrowed);
        var game = new Driver(initial);
        var command = Waymark(initial, borrowed);
        await RadGearTestPosition.Reject(initial, "P1", Waymark(initial, borrowed, "ALLY"));
        await RadGearTestPosition.Reject(initial, "P1", Waymark(initial, borrowed, cost: "ENEMY"));
        await RadGearTestPosition.Reject(initial, "P1", command with { OptionalCosts = [] });
        await RadGearTestPosition.Reject(initial, "P2", command);
        await game.Act("P1", command);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.True(game.State.CardObjects[borrowed ? "HOST" : "UNIT"].IsExhausted);
        Assert.Equal(new ObjectLocationState("P1", "BASE"), game.State.ObjectLocations["HOST"]);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(["HOST"], item.TargetObjectIds);
        Assert.Equal(new ObjectBinding("ALLY", initial.CardObjects["ALLY"].ObjectGeneration),
            (borrowed ? item.BorrowedExhaust!.ExhaustedCostUnit : item.SimpleActivation!.ExhaustedCostUnit)!.Unit);
        var forged = borrowed ? item with { BorrowedExhaust = item.BorrowedExhaust! with { ExhaustedCostUnit = null } }
            : item with { SimpleActivation = item.SimpleActivation! with { ExhaustedCostUnit = null } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        await game.Top();
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF"), game.State.ObjectLocations["HOST"]);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, "ALLY")] [InlineData(true, "ALLY")]
    [InlineData(false, "HOST")] [InlineData(true, "HOST")]
    public async Task WaymarkKeepsItsPaidReferenceAndTargetGenerationsAcrossARealReturn(bool borrowed, string returned)
    {
        var game = new Driver(WaymarkPosition(borrowed));
        await game.Act("P1", Waymark(game.State, borrowed));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned]));
        await game.Top();
        Assert.Contains(returned, game.State.PlayerZones["P1"].Hand);
        await game.Top();
        if (returned == "ALLY") Assert.Equal(new ObjectLocationState("P1", "BASE"), game.State.ObjectLocations["HOST"]);
        else Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WaymarkUsesSwiftDuelFocusButCannotBeActivatedInsideAResponseChain(bool borrowed)
    {
        var initial = HandDiscardObserverTests.Put(WaymarkPosition(borrowed), "STARTER", "OGN·219/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("STARTER", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
        Assert.Equal("P1", game.State.FocusPlayerId);
        await game.Act("P1", Waymark(game.State, borrowed));
        await RadGearTestPosition.Reject(game.State, "P1", Waymark(game.State, borrowed));
        await game.Top();
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF"), game.State.ObjectLocations["HOST"]);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedWaymarkSurvivesItsBorrowersReturnWithAnIndependentMovementTarget()
    {
        var initial = HandDiscardObserverTests.Put(WaymarkPosition(true), "MOVER", "OGN·219/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", Waymark(game.State, true, "MOVER"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF"), game.State.ObjectLocations["MOVER"]);
        await game.Replay();
    }

}
