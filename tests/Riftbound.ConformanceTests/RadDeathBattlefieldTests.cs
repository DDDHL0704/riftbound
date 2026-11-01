using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadDeathBattlefieldTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task SanctuaryRewardsTheVictimsControllerAfterAResponseWindow(string player)
    {
        var game = new Driver(Position(player));
        await Destroy(game);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal("BF", item.SourceObjectId);
        Assert.Equal(player, item.ControllerId);
        Assert.Equal(TriggerKinds.BattlefieldFirstNonTokenDestroyedCreateMinion, item.DeathObserver!.Kind);
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.MinionTokenFamily));
        await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.MinionTokenFamily));
        Assert.Equal(1, token.Power);
        Assert.True(token.IsExhausted);
        Assert.Equal(player, token.ControllerId);
        Assert.Contains(token.ObjectId, game.State.PlayerZones[player].Base);
        Assert.Contains(game.State.UntilEndOfTurnEffects, e => e.StartsWith("DEATH_OBSERVER_ONCE:"));
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith("DEATH_OBSERVER_ONCE:"));
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·T01")]
    [InlineData("OGN·273/298")]
    public async Task UnitTokensDoNotCount(string tokenCardNo)
    {
        var state = Position("P2");
        var token = P6TokenFactoryCatalog.GetAll().Single(f => f.CardNo == tokenCardNo).CreateObject("D", "P2", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = token with { Damage = token.Power - 1 } } };
        var game = new Driver(state);
        await Destroy(game);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith("DEATH_OBSERVER_ONCE:"));
        await game.Replay();
    }

    [Fact]
    public async Task AnActualImageCopyOfAPrintedUnitIsStillExcluded()
    {
        var state = Position("P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P1" } } };
        state = OfficialCopyIdentityTests.AddSpell(state, "COPY", "UNL-200/219");
        state = OfficialCopyIdentityTests.AddSpell(state, "KILL", "OGN·229/298");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["D"]));
        await game.Top();
        await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal("UNL-008/219", token.CardNo);
        await game.Act("P1", new MoveUnitCommand(token.ObjectId, "BASE", "BATTLEFIELD:BF", []));
        for (var i = 0; i < 4 && game.PromptFor(game.State.ActivePlayerId!).EnabledActions().Contains(CommandTypes.PassFocus); i++)
            await game.Act(game.State.ActivePlayerId!, new PassFocusCommand());
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", [token.ObjectId]));
        await game.Top();
        Assert.DoesNotContain(token.ObjectId, game.State.CardObjects.Keys);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith("DEATH_OBSERVER_ONCE:"));
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")]
    [InlineData("OTHER")]
    public async Task ADeathOutsideTheSanctuaryDoesNotConsumeItsUse(string destination)
    {
        var state = Position("P2");
        if (destination == "OTHER")
        {
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OTHER"] = OfficialGarenConquestBattlefieldCountTests.Printed("OTHER", "OGN·275/298", "P2") } };
            state = RadUnitEffectTestFixture.Place(state, "OTHER", "P2", "BATTLEFIELD", "OTHER");
        }
        state = RadUnitEffectTestFixture.Place(state, "D", "P2", destination == "BASE" ? "BASE" : "BATTLEFIELD", destination == "BASE" ? null : "OTHER");
        var game = new Driver(state);
        await Destroy(game);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith("DEATH_OBSERVER_ONCE:"));
        await game.Replay();
    }

    [Theory]
    [InlineData("D")]
    [InlineData("D2")]
    public async Task SimultaneousDeathsChooseOneOccurrenceAndRejectForgedChoices(string chosen)
    {
        var game = new Driver(Position("P2", true));
        await Destroy(game);
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal(2, request.Options.Count);
        var bad = new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["forged-death"]);
        var result = await new CoreRuleEngine().ResolveAsync(game.State, new("forged", "P2", bad.CmdType), bad, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(result.State));
        var option = request.Options.Single(o => o.ObjectIds![0] == chosen);
        await game.Act("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
        Assert.Equal(chosen, Assert.Single(game.State.StackItems).DeathObserver!.Destroyed.ObjectId);
        await game.Top();
        Assert.Single(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.MinionTokenFamily));
        await game.Replay();
    }

    [Fact]
    public async Task PriorDeathsElsewhereDoNotBlockTheFirstLocalDeath()
    {
        var state = Position("P2") with { DeathLedger = new(5, new Dictionary<string, int> { ["P2"] = 4 }) };
        var game = new Driver(state);
        await Destroy(game);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Single(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.MinionTokenFamily));
        await game.Replay();
    }

    [Fact]
    public async Task EachPlayerEarnsTheirOwnFirstDeathInTheSameBatch()
    {
        var state = Position("P2", true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D2"] = state.CardObjects["D2"] with { ControllerId = "P1", OwnerId = "P1" } } };
        state = RadUnitEffectTestFixture.Place(state, "D2", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await Destroy(game);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.Equal(new[] { "P1", "P2" }, game.State.TriggerQueue.Select(t => t.ControllerId).Order(StringComparer.Ordinal));
        for (var i = 0; i < 8 && (game.State.TriggerQueue.Count > 0 || game.State.StackItems.Count > 0); i++)
        {
            if (game.State.TriggerQueue.Count > 0)
            {
                var player = game.State.ActivePlayerId!;
                await game.Act(player, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            }
            else await game.Top();
        }
        Assert.Single(game.State.PlayerZones["P1"].Base, id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.MinionTokenFamily));
        Assert.Single(game.State.PlayerZones["P2"].Base, id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.MinionTokenFamily));
        await game.Replay();
    }

    [Fact]
    public async Task ASecondLocalDeathDuringTheSameTurnDoesNotEarnAnotherToken()
    {
        var state = Position("P2", true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D2"] = state.CardObjects["D2"] with { Power = 2, Damage = 0, Tags = [CardObjectTags.UnitCard] },
            ["AOE2"] = state.CardObjects["AOE"] with { ObjectId = "AOE2" } } };
        state = RadUnitEffectTestFixture.Place(state, "AOE2", "P1", "HAND");
        var game = new Driver(state);
        await Destroy(game);
        await game.Top();
        Assert.Contains("D2", game.State.PlayerZones["P2"].Battlefields);
        await game.Act("P1", new PlayCardCommand("AOE2", "OGN·133/298", []));
        await game.Top();
        Assert.Contains("D2", game.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Empty(game.State.StackItems);
        Assert.Single(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.MinionTokenFamily));
        await game.Replay();
    }

    [Fact]
    public async Task TheSamePlayerCanTriggerTwoDifferentSanctuariesInOneTurn()
    {
        var state = Position("P2", true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = OfficialGarenConquestBattlefieldCountTests.Printed("OTHER", "RAD·159/167", "P2") } };
        state = RadUnitEffectTestFixture.Place(state, "OTHER", "P2", "BATTLEFIELD", "OTHER");
        state = RadUnitEffectTestFixture.Place(state, "D2", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(state);
        await Destroy(game);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await game.Top();
        await game.Top();
        Assert.Equal(2, game.State.CardObjects.Values.Count(c => c.Tags.Contains(CardObjectTags.MinionTokenFamily)));
        await game.Replay();
    }

    [Fact]
    public async Task SanctuaryUsesTheSharedUnitTokenEntryReplacement()
    {
        var state = DeathObserverAuditTests.Source(Position("P2"), "UNL-086/219", "ZILEAN");
        state = RadUnitEffectTestFixture.Place(state, "ZILEAN", "P2", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await Destroy(game);
        await game.Top();
        var choice = game.State.PendingCardChoice!;
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", choice.ChoiceWindow);
        await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ZILEAN"]));
        Assert.Equal(2, game.State.CardObjects.Values.Count(c => c.Tags.Contains(CardObjectTags.MinionTokenFamily)));
        await game.Replay();
    }

    [Theory]
    [InlineData("token")]
    [InlineData("other-field")]
    [InlineData("other-player")]
    [InlineData("missing-location")]
    public async Task RecoveryRejectsForgedDeathFacts(string boundary)
    {
        var game = new Driver(Position("P2"));
        await Destroy(game);
        var item = Assert.Single(game.State.StackItems);
        var context = item.DeathObserver!;
        var forged = boundary switch {
            "token" => context with { Destroyed = context.Destroyed with { WasToken = true } },
            "other-field" => context with { DestroyedLocation = context.DestroyedLocation! with { BattlefieldObjectId = "OTHER" } },
            "other-player" => context with { Destroyed = context.Destroyed with { ControllerId = "P1" } },
            _ => context with { SourceLocation = null } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { DeathObserver = forged }] }),
            e => e.Contains("invalid captured death observer", StringComparison.Ordinal));
        await game.Replay();
    }

    private static async Task Destroy(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        await game.Top();
    }

    private static MatchState Position(string player, bool two = false)
    {
        var state = DeathObserverAuditTests.NoReplacement(two);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { CardNo = "RAD·159/167" },
            ["D"] = state.CardObjects["D"] with { OwnerId = "P1", ControllerId = player } } };
        state = RadUnitEffectTestFixture.Place(state, "D", player, "BATTLEFIELD", "BF");
        return state;
    }
}
