using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadHeimerdingerReplacementBoundaryTests
{
    private static MatchState Position(string spell)
    {
        var state = OfficialTokenReplacementTests.Position(0, spell);
        return LocalDestructionRecallTests.At(state with {
            Tick = 0,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HEIMER"] = OfficialGarenConquestBattlefieldCountTests.Printed("HEIMER", "RAD·132/167", "P1") }
        }, "HEIMER", "P1", "BATTLEFIELD", "BFZ");
    }

    private static async Task Choose(Driver game, params string[] selected)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
        for (var step = 0; game.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" } location; step++)
        {
            Assert.True(step < 2);
            Assert.Contains(location.Request.Options, option => option.Id == "BASE");
            await game.Act(location.Request.PlayerId, new PayCostCommand(location.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        }
    }

    private static async Task ResolveHighCostSpellTriggers(Driver game)
    {
        for (var step = 0; game.State.StackItems.LastOrDefault()?.SpellContext is { } trigger; step++)
        {
            Assert.True(step < 2);
            Assert.Equal("LEGEND_HIGH_COST_SPELL_DRAW_ONE", trigger.Kind);
            await game.Top();
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EachEchoExecutionOffersANewReplacementWithoutChangingTheEarlierToken(bool replaceFirst)
    {
        var game = new Driver(Position("SFD·031/221"));
        await game.Act("P1", new PlayCardCommand("C", "SFD·031/221", [], OptionalCosts: ["ECHO"]));
        await game.Top();
        Assert.Empty(OfficialTokenReplacementTests.Tokens(game.State));
        var pool = MatchStateHasher.HashValue(game.State.RunePools);
        await Choose(game, replaceFirst ? ["HEIMER"] : []);
        var first = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State));
        Assert.Equal(replaceFirst ? "SFD·T01" : P6TokenFactoryCatalog.SandSoldierTokenCardNo, first.TokenFactoryCardNo);
        Assert.Equal(replaceFirst ? 3 : 2, first.Power);
        var continuation = Assert.Single(game.State.StackItems);
        Assert.Equal(1, continuation.CompletedRepeatExecutions);
        Assert.Empty(continuation.TokenEntryPlan!.Applied);
        Assert.Equal(0, continuation.TokenEntryPlan.NextToken);
        Assert.Equal(new[] { "HEIMER" }, game.State.PendingCardChoice!.LegalObjectIds);
        var firstHash = MatchStateHasher.HashValue(first);
        await Choose(game, replaceFirst ? [] : ["HEIMER"]);
        var tokens = OfficialTokenReplacementTests.Tokens(game.State);
        Assert.Equal(2, tokens.Length);
        Assert.Single(tokens, token => token.TokenFactoryCardNo == "SFD·T01");
        Assert.Single(tokens, token => token.TokenFactoryCardNo == P6TokenFactoryCatalog.SandSoldierTokenCardNo);
        Assert.Equal(firstHash, MatchStateHasher.HashValue(game.State.CardObjects[first.ObjectId]));
        Assert.All(tokens, token => {
            Assert.True(token.IsExhausted);
            Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base);
            Assert.DoesNotContain(CardObjectTags.Ephemeral, token.Tags);
        });
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Single(game.State.PlayerZones["P1"].Graveyard, id => id == "C");
        Assert.Equal(pool, MatchStateHasher.HashValue(game.State.RunePools));
        await game.Replay();
    }

    [Fact]
    public async Task ReplacingEveryTokenInOneBatchPreservesTheReplacementForTheNextSpell()
    {
        var state = OfficialCopyIdentityTests.AddSpell(Position("UNL-069/219"), "SECOND", "UNL-069/219");
        var game = new Driver(state);
        foreach (var source in new[] { "C", "SECOND" })
        {
            await game.Act("P1", new PlayCardCommand(source, "UNL-069/219", []));
            await game.Top();
            var before = OfficialTokenReplacementTests.Tokens(game.State).Length;
            var pool = MatchStateHasher.HashValue(game.State.RunePools);
            await Choose(game, "HEIMER");
            Assert.Equal(before, OfficialTokenReplacementTests.Tokens(game.State).Length);
            Assert.Equal(1, Assert.Single(game.State.StackItems).TokenEntryPlan!.NextToken);
            await Choose(game, "HEIMER");
            Assert.Equal(before + 2, OfficialTokenReplacementTests.Tokens(game.State).Length);
            Assert.Null(game.State.PendingCardChoice);
            Assert.Equal(pool, MatchStateHasher.HashValue(game.State.RunePools));
            Assert.DoesNotContain("TOKEN_ENTRY_REPLACEMENT_USED", game.State.CardObjects["HEIMER"].UntilEndOfTurnEffects);
            await ResolveHighCostSpellTriggers(game);
            Assert.Empty(game.State.StackItems);
        }
        Assert.Equal(4, OfficialTokenReplacementTests.Tokens(game.State).Length);
        Assert.All(OfficialTokenReplacementTests.Tokens(game.State), token => {
            Assert.Equal("SFD·T01", token.TokenFactoryCardNo);
            Assert.Equal(3, token.Power);
            Assert.True(token.IsExhausted);
            Assert.Contains("机械", token.Tags);
            Assert.DoesNotContain(CardObjectTags.Ephemeral, token.Tags);
        });
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·104/298", "HAND")] [InlineData("SFD·163/221", "GRAVEYARD")]
    public async Task ASourceThatLeavesBeforeCreationCannotReplaceThePendingTokens(string response, string zone)
    {
        var state = OfficialCopyIdentityTests.AddSpell(Position("UNL-069/219"), "LEAVE", response);
        state = LocalDestructionRecallTests.At(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RECEIVER"] = OfficialGarenConquestBattlefieldCountTests.Printed("RECEIVER", "OGN·001/298", "P1") }
        }, "RECEIVER", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("C", "UNL-069/219", []));
        await game.Act("P1", new PlayCardCommand("LEAVE", response,
            response == "SFD·163/221" ? ["HEIMER", "RECEIVER"] : ["HEIMER"]));
        await game.Top();
        Assert.Equal(new ObjectLocationState("P1", zone), game.State.ObjectLocations["HEIMER"]);
        Assert.Equal(1, game.State.CardObjects["HEIMER"].ObjectGeneration);
        await ResolveHighCostSpellTriggers(game);
        Assert.Equal("C", Assert.Single(game.State.StackItems).SourceObjectId);
        Assert.Empty(OfficialTokenReplacementTests.Tokens(game.State));
        var firstEvent = game.Events().Length;
        await game.Top();
        Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(2, OfficialTokenReplacementTests.Tokens(game.State).Length);
        Assert.All(OfficialTokenReplacementTests.Tokens(game.State), token => {
            Assert.NotEqual("SFD·T01", token.TokenFactoryCardNo);
            Assert.Contains(CardObjectTags.Ephemeral, token.Tags);
        });
        Assert.DoesNotContain(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "TOKEN_REPLACEMENT_CHOSEN");
        await ResolveHighCostSpellTriggers(game);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
