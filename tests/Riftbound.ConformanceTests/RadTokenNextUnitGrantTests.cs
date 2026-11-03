using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadTokenNextUnitGrantTests
{
    private const string Create = "SFD·031/221";

    private static MatchState Position(bool heimer = false)
    {
        var state = OfficialTokenReplacementTests.Position(0, Create);
        state = state with {
            Tick = 0,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·139/167", "P1") with { IsEmpowered = true } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } };
        if (heimer)
            state = LocalDestructionRecallTests.At(state with {
                CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                    ["HEIMER"] = OfficialGarenConquestBattlefieldCountTests.Printed("HEIMER", "RAD·132/167", "P1") }
            }, "HEIMER", "P1", "BATTLEFIELD", "BFZ");
        return state;
    }

    private static async Task Grant(Driver game)
    {
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, [], []));
        await game.Top();
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
    }

    private static async Task Cast(Driver game, bool echo = false)
    {
        await game.Act("P1", new PlayCardCommand("C", Create, [], OptionalCosts: echo ? ["ECHO"] : []));
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await game.Top();
    }

    private static async Task Choose(Driver game, bool replace)
    {
        var pending = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(pending.PlayerId, new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, replace ? ["HEIMER"] : []));
        await ChooseBase(game);
    }

    private static async Task ChooseBase(Driver game)
    {
        for (var step = 0; game.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" } pending; step++)
        {
            Assert.True(step < 2);
            await game.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        }
    }

    private static string[] Markers(CardObjectState card)
        => card.UntilEndOfTurnEffects.Where(effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal)).ToArray();

    private static void AssertMarked(Driver game, CardObjectState token)
    {
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        var marker = CoreRuleEngine.ReadTimeAssassinDestructionMarker(Assert.Single(Markers(token)))!;
        Assert.Equal(token.ObjectId, marker.Target.ObjectId);
        Assert.Equal(token.ObjectGeneration, marker.Target.Generation);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TIME_ASSASSIN_UNIT_MARKED_FOR_DESTRUCTION"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), token.ObjectId));
    }

    private static async Task DestroyAtEnd(Driver game, string id)
    {
        await game.Act("P1", new EndTurnCommand());
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(CoreRuleEngine.TimeAssassinEndDestructionKind, skill.EndAbility!.Kind);
        Assert.Equal(id, skill.SourceObjectId);
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(id));
        Assert.False(game.State.ObjectLocations.ContainsKey(id));
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), id));
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TOKEN_CEASED_TO_EXIST"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), id));
        await game.Replay();
    }

    [Fact]
    public async Task ASpellCreatedUnitTokenConsumesTheGrantAndActuallyDiesAtTurnEnd()
    {
        var game = new Driver(Position());
        await Grant(game);
        await Cast(game);
        await ChooseBase(game);
        var token = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State));
        Assert.Equal(P6TokenFactoryCatalog.SandSoldierTokenCardNo, token.TokenFactoryCardNo);
        AssertMarked(game, token);
        await DestroyAtEnd(game, token.ObjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptingOrDecliningHeimerGrantsTheSkillToTheTokenThatActuallyEnters(bool replace)
    {
        var game = new Driver(Position(heimer: true));
        await Grant(game);
        await Cast(game);
        Assert.Empty(OfficialTokenReplacementTests.Tokens(game.State));
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await Choose(game, replace);
        var token = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State));
        Assert.Equal(replace ? "SFD·T01" : P6TokenFactoryCatalog.SandSoldierTokenCardNo, token.TokenFactoryCardNo);
        AssertMarked(game, token);
        await DestroyAtEnd(game, token.ObjectId);
    }

    [Fact]
    public async Task ASecondEchoExecutionCannotReuseTheFirstTokensConsumedGrant()
    {
        var game = new Driver(Position(heimer: true));
        await Grant(game);
        await Cast(game, echo: true);
        await Choose(game, replace: true);
        var first = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State));
        Assert.Equal("SFD·T01", first.TokenFactoryCardNo);
        AssertMarked(game, first);
        await Choose(game, replace: false);
        var second = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State), token => token.ObjectId != first.ObjectId);
        Assert.Empty(Markers(second));
        Assert.Equal(P6TokenFactoryCatalog.SandSoldierTokenCardNo, second.TokenFactoryCardNo);
        AssertMarked(game, game.State.CardObjects[first.ObjectId]);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "UNIT_TOKEN_CREATED"));
        await DestroyAtEnd(game, first.ObjectId);
        Assert.Contains(second.ObjectId, game.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task AnInvalidReplacementDoesNotConsumeTheGrantOrChangeThePendingPlay()
    {
        var game = new Driver(Position(heimer: true));
        await Grant(game);
        await Cast(game);
        var pending = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var before = MatchStateHasher.Hash(game.State);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State));
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("forged-token-replacement", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["TARGET"]), default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(views, JsonSerializer.Serialize(rejected.Snapshots));
        Assert.Equal(prompts, JsonSerializer.Serialize(rejected.Prompts));
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await Choose(game, replace: true);
        var token = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State));
        AssertMarked(game, token);
        await DestroyAtEnd(game, token.ObjectId);
    }
}
