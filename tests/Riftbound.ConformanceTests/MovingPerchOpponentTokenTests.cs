using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 355.9/383: confirm the opponent before responses; the instructed player
// actually plays the token and owns its replacement and destination decisions.
public sealed class MovingPerchOpponentTokenTests
{
    private const string Perch = "UNL-130/219";
    internal static MatchState Position()
        => RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "PERCH", Perch, "P1", "HAND");
    private static CardObjectState[] Tokens(MatchState state)
        => state.CardObjects.Values.Where(card => card.TokenFactoryCardNo is { } factory
            && P6TokenFactoryCatalog.IsUnitTokenFactory(factory)).ToArray();
    private static Task Choose(Driver game, params string[] ids)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        return game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }
    private static async Task Open(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("PERCH", Perch, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("UNIT_EVENT_CHOSEN_OPPONENT", choice.ChoiceWindow);
        Assert.Equal("P1", choice.PlayerId); Assert.Equal(["P2"], choice.LegalObjectIds);
        Assert.Empty(Tokens(game.State)); Assert.Null(game.State.PriorityPlayerId);
        await Choose(game, "P2");
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("P1", skill.ControllerId); Assert.Equal("P2", skill.UnitEvent!.ChosenOpponentId);
        Assert.Empty(skill.TargetObjectIds); Assert.NotNull(game.State.PriorityPlayerId);
    }
    private static async Task FinishDestination(Driver game, string destination = "BASE")
    {
        if (game.State.PendingRuleChoice is { } choice)
        {
            Assert.Equal("P2", choice.Request.PlayerId);
            await game.Act("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [destination]));
        }
    }

    [Fact]
    public async Task NativePlayConfirmsTheOpponentThenPlaysItsExhaustedOneMightSpellshieldWarhawk()
    {
        var game = new Driver(Position()); await Open(game); await game.Top(); await FinishDestination(game);
        var token = Assert.Single(Tokens(game.State));
        Assert.Equal(P6TokenFactoryCatalog.WarhawkTokenCardNo, token.TokenFactoryCardNo);
        Assert.Equal("P2", token.OwnerId); Assert.Equal("P2", token.ControllerId);
        Assert.Equal(1, token.Power); Assert.True(token.IsExhausted); Assert.Contains("法盾", token.Tags);
        Assert.Contains(token.ObjectId, game.State.PlayerZones["P2"].Base);
        var created = Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_TOKEN_CREATED");
        Assert.Equal("P2", created.Event.Payload["playerId"]); await game.Replay();
    }

    [Fact]
    public async Task TheChosenOpponentMakesItsOwnControlledBattlefieldDestinationChoice()
    {
        var game = new Driver(RadUnitEffectTestFixture.Place(Position(), "ENEMY", "P2", "BATTLEFIELD", "OTHER"));
        await Open(game); await game.Top();
        Assert.NotNull(game.State.PendingRuleChoice); Assert.Empty(Tokens(game.State));
        await FinishDestination(game, "BATTLEFIELD:OTHER");
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "OTHER"),
            game.State.ObjectLocations[Assert.Single(Tokens(game.State)).ObjectId]); await game.Replay();
    }

    [Theory]
    [InlineData("RAD·132/167", false, 1)] [InlineData("UNL-086/219", false, 2)]
    [InlineData("RAD·132/167", true, 1)]
    public async Task OnlyTheTokenPlayersSourcesOfferReplacementAndThatPlayerCanDecline(string source, bool decline, int count)
    {
        var state = RadGearTestPosition.Put(Position(), "OPPONENT-REPLACE", source, "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "CASTER-REPLACE", "RAD·132/167", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state); await Open(game); await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", choice.ChoiceWindow); Assert.Equal("P2", choice.PlayerId);
        Assert.Contains("OPPONENT-REPLACE", choice.LegalObjectIds); Assert.DoesNotContain("CASTER-REPLACE", choice.LegalObjectIds);
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OPPONENT-REPLACE"]));
        await Choose(game, decline ? [] : ["OPPONENT-REPLACE"]);
        for (var i = 0; game.State.PendingRuleChoice is null
            && game.State.PendingCardChoice is { ChoiceWindow: "TOKEN_ENTRY_REPLACEMENT" }; i++)
        {
            Assert.InRange(i, 0, 3); await Choose(game);
        }
        for (var i = 0; game.State.PendingRuleChoice is not null; i++)
        {
            Assert.InRange(i, 0, 3); await FinishDestination(game);
        }
        Assert.Equal(count, Tokens(game.State).Length);
        Assert.All(Tokens(game.State), token => {
            Assert.Equal("P2", token.OwnerId); Assert.Equal("P2", token.ControllerId);
            Assert.Equal(source == "RAD·132/167" && !decline ? "SFD·T01" : P6TokenFactoryCatalog.WarhawkTokenCardNo, token.TokenFactoryCardNo);
        }); await game.Replay();
    }

    [Fact]
    public async Task TheSkillControllersReplacementSourceCannotReplaceAnOpponentsToken()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "HEIMER", "RAD·132/167", "P1", "BATTLEFIELD", "BF"));
        await Open(game); await game.Top(); await FinishDestination(game);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(P6TokenFactoryCatalog.WarhawkTokenCardNo, Assert.Single(Tokens(game.State)).TokenFactoryCardNo); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceInResponseKeepsTheChosenOpponentAndRedactsItsPrivateSource()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await Open(game); await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["PERCH"])); await game.Top();
        Assert.Contains("PERCH", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"PERCH\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); await FinishDestination(game);
        Assert.Equal("P2", Assert.Single(Tokens(game.State)).ControllerId);
        Assert.Equal("HIDDEN", Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_TOKEN_CREATED").Event.Payload["sourceObjectId"]);
        await game.Replay();
    }

    [Theory]
    [InlineData("self")] [InlineData("empty")] [InlineData("actor")]
    public async Task OpponentConfirmationRejectsSelfEmptyAndWrongActorAtomically(string mode)
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("PERCH", Perch, []));
        var choice = game.State.PendingCardChoice!;
        await RadGearTestPosition.Reject(game.State, mode == "actor" ? "P2" : "P1",
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, mode == "empty" ? [] : [mode == "self" ? "P1" : "P2"]));
        await Choose(game, "P2"); await game.Top(); await FinishDestination(game); await game.Replay();
    }

    [Theory]
    [InlineData("opponent")] [InlineData("tick")] [InlineData("erase")]
    public async Task RecoveryRequiresTheOriginalAcceptedOpponentConfirmation(string mode)
    {
        var game = new Driver(Position()); await Open(game);
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!;
        var forged = context with {
            ChosenOpponentId = mode == "opponent" ? "P1" : mode == "erase" ? null : context.ChosenOpponentId,
            OpponentConfirmedAtTick = mode == "tick" ? context.OpponentConfirmedAtTick - 1 : mode == "erase" ? null : context.OpponentConfirmedAtTick };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game,
            game.State with { StackItems = [item with { UnitEvent = forged }] }));
    }

    [Fact]
    public async Task RecoveryRejectsASelfSeatInTheOpponentChoiceInsteadOfAcceptingArbitrarySeatIdentifiers()
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("PERCH", Perch, []));
        var choice = game.State.PendingCardChoice!;
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game,
            game.State with { PendingCardChoice = choice with { LegalObjectIds = ["P1"] } }));
    }

    [Fact]
    public async Task ActuallyPlayingTheOpponentsTokenEarnsThatPlayersFriendlyTokenPlayObserver()
    {
        var state = RadGearTestPosition.Put(Position(), "LILLIA", "UNL-058/219", "P2", "BASE");
        var game = new Driver(state); var before = game.State.CardObjects["LILLIA"].Power;
        await Open(game); await game.Top(); await FinishDestination(game);
        Assert.Equal(before, game.State.CardObjects["LILLIA"].Power);
        if (game.State.TriggerQueue.Count > 0)
        {
            Assert.All(game.State.TriggerQueue, trigger => Assert.Equal("P2", trigger.ControllerId));
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        }
        var observer = Assert.Single(game.State.StackItems);
        Assert.Equal("LILLIA", observer.SourceObjectId); Assert.Equal("P2", observer.ControllerId);
        await game.Top(); Assert.Equal(before + 1, game.State.CardObjects["LILLIA"].Power); await game.Replay();
    }
}
