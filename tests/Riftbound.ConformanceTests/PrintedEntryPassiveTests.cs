using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class PrintedEntryPassiveTests
{
    private static MatchState Position(string cardNo)
    {
        var state = VenUnitEntryBasicTests.Position(cardNo);
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 20, new Dictionary<string, int> { ["green"] = 10, ["yellow"] = 10, ["blue"] = 10 }) } };
    }

    [Theory]
    [InlineData("OGN·079/298", 4, true)] [InlineData("OGN·079a/298", 4, true)]
    [InlineData("OGN·079/298", 5, false)] [InlineData("OGN·079a/298", 5, false)]
    [InlineData("OGN·079/298", 7, false)] [InlineData("OGN·079a/298", 7, false)]
    public async Task LeonaChecksTheOpponentsScoreAtEntry(string cardNo, int opponentScore, bool exhausted)
    {
        var initial = Position(cardNo) with { PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = opponentScore } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        Assert.Equal(exhausted, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·079/298")] [InlineData("OGN·079a/298")]
    public async Task LeonaDoesNotUseHerControllersScoreForTheOpponentsCondition(string cardNo)
    {
        var game = new Driver(Position(cardNo) with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 7, ["P2"] = 0 } });
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·079/298", 5, true)] [InlineData("OGN·079a/298", 5, true)]
    [InlineData("OGN·079/298", 6, false)] [InlineData("OGN·079a/298", 6, false)]
    public async Task LeonaUsesTheCurrentWinningScoreIncludingBattlefieldModifiers(string cardNo, int opponentScore, bool exhausted)
    {
        var initial = Position(cardNo);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = Printed("BF", "OGN·276/298", "P1") with { ControllerId = "P1" } },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = opponentScore } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        Assert.Equal(exhausted, game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    private static MatchState OpenFieldPosition(string cardNo)
    {
        var state = Position(cardNo);
        state = MightyExperienceFrameTests.HandUnit(state, "NEXT", "OGN·219/298");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = null } } };
    }

    private static MatchState FieldSourcePosition(string controller)
    {
        var state = OpenFieldPosition("OGN·193/298");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray() } } };
        return LocalDestructionRecallTests.At(state, "UNIT", controller, "BASE");
    }

    [Theory]
    [InlineData("OGN·193/298")] [InlineData("OGN·193a/298")] [InlineData("OGN·193b/298")]
    public async Task EveryMissFortunePrintingCanBePlayedDirectlyToAnOpenBattlefield(string cardNo)
    {
        var initial = OpenFieldPosition(cardNo);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BATTLEFIELD:OTHER"));
        Assert.Equal("OTHER", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        for (var i = 0; i < 4 && game.State.FocusPlayerId is { } focus; i++)
            await game.Act(focus, new PassFocusCommand());
        Assert.Equal("P1", game.State.CardObjects["OTHER"].ControllerId);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·193/298")] [InlineData("OGN·193a/298")] [InlineData("OGN·193b/298")]
    public async Task AFieldSourceImmediatelyGrantsItsControllerTheOpenBattlefieldPlayPermission(string cardNo)
    {
        var game = new Driver(OpenFieldPosition(cardNo));
        var next = new PlayCardCommand("NEXT", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER");
        await Reject(game.State, "P1", next);
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", new("next", "prompt", game.State.Tick, next));
        Assert.True(quote.IsValid, quote.Message);
        await game.Act("P1", next);
        Assert.Equal("OTHER", game.State.ObjectLocations["NEXT"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["NEXT"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task TheContinuousPlayPermissionEndsWhenItsSourceLeavesTheField()
    {
        var initial = AttackDamageDistributionTests.Spell(OpenFieldPosition("OGN·193/298"), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·193/298", [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await Reject(game.State, "P1", new PlayCardCommand("NEXT", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER"));
        await game.Replay();
    }

    [Fact]
    public async Task ThePermissionDoesNotIncludeAnEnemyOccupiedBattlefield()
    {
        var initial = LocalDestructionRecallTests.At(OpenFieldPosition("OGN·193/298"), "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·193/298", [], Destination: "BASE"));
        await Reject(game.State, "P1", new PlayCardCommand("NEXT", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER"));
        await game.Replay();
    }

    [Fact]
    public async Task AnUnoccupiedBattlefieldThatStillHasAControllerIsNotOpen()
    {
        var initial = FieldSourcePosition("P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = "P2" } } };
        Assert.False(CoreRuleEngine.IsOpenBattlefield(initial, "OTHER"));
        var command = new PlayCardCommand("NEXT", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER");
        await Reject(initial, "P1", command);
        var released = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = null } } };
        var accepted = await new CoreRuleEngine().ResolveAsync(released, new("released-field", "P1", command.CmdType), command, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
    }

    [Theory]
    [InlineData("OGN·193/298")] [InlineData("OGN·193a/298")] [InlineData("OGN·193b/298")]
    public async Task TheFieldPermissionAlsoAppliesToAnUnrestrictedUnitTokenPlay(string cardNo)
    {
        var initial = AttackDamageDistributionTests.Spell(OpenFieldPosition(cardNo), "SAND", "SFD·031/221", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("SAND", "SFD·031/221", []));
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("TOKEN_PLAY_LOCATION", choice.Request.Kind);
        Assert.Contains(choice.Request.Options, option => option.Id == "BATTLEFIELD:OTHER");
        await game.Act(choice.Request.PlayerId,
            new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:OTHER"]));
        var token = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo is not null);
        Assert.Equal("OTHER", game.State.ObjectLocations[token.ObjectId].BattlefieldObjectId);
        await game.Replay();
    }

    [Fact]
    public async Task ExplicitBaseTokenInstructionsRetainTheirLocationRestriction()
    {
        var initial = OpenFieldPosition("OGN·193/298");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["FURNACE"] = Printed("FURNACE", "OGN·212/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = [..initial.PlayerZones["P1"].Hand, "FURNACE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["FURNACE"] = new("P1", "HAND") } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·193/298", [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("FURNACE", "OGN·212/298", [], Destination: "BASE"));
        await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo is not null);
        Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.PendingRuleChoice);
        await game.Replay();
    }

    [Fact]
    public async Task AnOpponentControlledSourceDoesNotGrantYouItsPlayPermission()
    {
        var command = new PlayCardCommand("NEXT", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER");
        await Reject(FieldSourcePosition("P2"), "P1", command);
        var accepted = await new CoreRuleEngine().ResolveAsync(FieldSourcePosition("P1"),
            new("friendly-source", "P1", command.CmdType), command, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
    }

    [Fact]
    public async Task AnOpenDestinationDoesNotGrantResponseTiming()
    {
        var initial = AttackDamageDistributionTests.Spell(OpenFieldPosition("OGN·193/298"), "OLDER", "OGN·048/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·193/298", [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("OLDER", "OGN·048/298", []));
        await Reject(game.State, "P1", new PlayCardCommand("NEXT", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER"));
        await game.Replay();
    }

    private static async Task Reject(MatchState initial, string actor, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("reject-entry", actor, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
    }
}
