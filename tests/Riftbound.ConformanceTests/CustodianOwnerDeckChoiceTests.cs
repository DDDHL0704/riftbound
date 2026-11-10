using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class CustodianOwnerDeckChoiceTests
{
    private const string Spell = "UNL-204/219";
    private static MatchState Position(bool borrowed = false, bool token = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", Spell, "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P2", "HAND");
        var target = state.CardObjects["ENEMY"] with { OwnerId = borrowed ? "P1" : "P2" };
        if (token)
        {
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.ZaunMinionTokenCardNo, out var factory));
            target = factory.CreateObject("ENEMY", borrowed ? "P1" : "P2", "P2");
        }
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["ENEMY"] = target } };
    }
    private static PlayCardCommand Play() => new("SPELL", Spell, ["ENEMY"]);
    private static Task Choose(Driver game, string end)
    {
        var request = game.State.PendingRuleChoice!.Request;
        return game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [end]));
    }

    [Theory]
    [InlineData(false, "TOP")] [InlineData(false, "BOTTOM")]
    [InlineData(true, "TOP")] [InlineData(true, "BOTTOM")]
    public async Task OnlyThePhysicalOwnerChoosesTheDeckEndDuringResolution(bool borrowed, string end)
    {
        var initial = Position(borrowed); var owner = borrowed ? "P1" : "P2"; var other = borrowed ? "P2" : "P1";
        var deck = initial.PlayerZones[owner].MainDeck.ToArray(); var generation = initial.CardObjects["ENEMY"].ObjectGeneration;
        var game = new Driver(initial); await game.Act("P1", Play());
        Assert.Null(game.State.PendingRuleChoice); Assert.Equal(Spell, Assert.Single(game.State.StackItems).CardNo);
        await game.Top(); var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("OWNER_MAIN_DECK_POSITION", request.Kind); Assert.Equal(owner, request.PlayerId);
        Assert.Equal(new[] { "TOP", "BOTTOM" }, request.Options.Select(option => option.Id));
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Battlefields);
        await RadGearTestPosition.Reject(game.State, other, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [end]));
        foreach (var invalid in new[] { "MIDDLE", "OWNER_MAIN_DECK_TOP" })
            await RadGearTestPosition.Reject(game.State, owner, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [invalid]));
        await Choose(game, end);
        Assert.Equal(end == "TOP" ? new[] { "ENEMY" }.Concat(deck) : deck.Append("ENEMY"), game.State.PlayerZones[owner].MainDeck);
        Assert.DoesNotContain("ENEMY", game.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(owner, game.State.CardObjects["ENEMY"].OwnerId);
        Assert.Equal(generation + 1, game.State.CardObjects["ENEMY"].ObjectGeneration);
        var view = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)[other], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(view.GetProperty("players").GetProperty(owner).GetProperty("objects").TryGetProperty("ENEMY", out _));
        await game.Replay();
    }

    [Theory]
    [InlineData("OWNER_MAIN_DECK_TOP")] [InlineData("OWNER_MAIN_DECK_BOTTOM")]
    public async Task TheCasterCannotPredeclareTheOwnersLaterResolutionDecision(string mode)
        => await RadGearTestPosition.Reject(Position(), "P1", Play() with { Mode = mode });

    [Fact]
    public async Task AnActualReturnResponseInvalidatesTheTargetWithoutAskingItsOwnerForADeckDecision()
    {
        var game = new Driver(Position()); await game.Act("P1", Play()); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"])); await game.Top(); await game.Top();
        Assert.Null(game.State.PendingRuleChoice); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("ENEMY", game.State.PlayerZones["P2"].MainDeck); await game.Replay();
    }

    [Theory]
    [InlineData("TOP")] [InlineData("BOTTOM")]
    public async Task ATokenLeavesTheFieldThenCeasesInsteadOfBecomingACardInEitherDeck(string end)
    {
        var initial = Position(token: true); var deck = initial.PlayerZones["P2"].MainDeck.ToArray();
        var game = new Driver(initial); await game.Act("P1", Play()); await game.Top(); await Choose(game, end);
        Assert.False(game.State.CardObjects.ContainsKey("ENEMY")); Assert.Equal(deck, game.State.PlayerZones["P2"].MainDeck);
        Assert.All(game.State.PlayerZones.Values, zones => Assert.DoesNotContain("ENEMY", zones.MainDeck)); await game.Replay();
    }

    [Fact]
    public async Task PlacementIntoADeckIsNotTheBattlefieldOrBaseMovementPreventedByAnArmament()
    {
        var state = RadGearTestPosition.Put(Position(), "CUTLASS", "VEN·073", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["CUTLASS"] = state.CardObjects["CUTLASS"] with { AttachedToObjectId = "ENEMY" } } };
        var game = new Driver(CoreRuleEngine.RecomputeRuleDerivedKeywords(state));
        await game.Act("P1", Play()); await game.Top(); await Choose(game, "TOP");
        Assert.Equal("ENEMY", game.State.PlayerZones["P2"].MainDeck[0]); Assert.Null(game.State.CardObjects["CUTLASS"].AttachedToObjectId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsGivingTheOwnersOutstandingDecisionToTheCaster()
    {
        var game = new Driver(Position()); await game.Act("P1", Play()); await game.Top();
        var pending = game.State.PendingRuleChoice!;
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingRuleChoice = pending with {
            Request = pending.Request with { PlayerId = "P1" } } }));
        await Choose(game, "BOTTOM"); await game.Replay();
    }
}
