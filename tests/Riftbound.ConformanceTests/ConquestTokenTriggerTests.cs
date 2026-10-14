using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ConquestTokenTriggerTests
{
    [Theory]
    [InlineData("UNL-222/219")]
    [InlineData("SFD·069/221")]
    public async Task NaturalConquestWaitsForBothResponsesBeforePlayingDormantGold(string card)
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position(card));
        Assert.Equal(1, opened.State.PlayerScores["P1"]);
        Assert.Empty(Gold(opened.State));
        Assert.Single(opened.State.StackItems);
        Assert.DoesNotContain(opened.Events, e => e.Kind == "EQUIPMENT_TOKEN_CREATED");
        Restore(opened.State);
        var first = await Act(opened.State, opened.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Empty(Gold(first.State)); Assert.Single(first.State.StackItems); Restore(first.State);
        var done = await Act(first.State, first.State.PriorityPlayerId!, new PassPriorityCommand());
        var gold = done.State.CardObjects[Assert.Single(Gold(done.State))];
        Assert.True(gold.IsExhausted); Assert.Equal("P1", gold.OwnerId); Assert.Equal("P1", gold.ControllerId);
        Assert.Contains(gold.ObjectId, done.State.PlayerZones["P1"].Base);
        Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Theory]
    [InlineData("UNL-222/219")]
    [InlineData("SFD·069/221")]
    public async Task CombatConquestAlsoQueuesGoldInsteadOfCreatingItDuringCombat(string card)
    {
        var initial = OfficialLegendConquestTests.CombatPosition("OGN·269/298", power: 5);
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = [] } },
            CardObjects = initial.CardObjects.Where(p => p.Key != "LEGEND").ToDictionary(p => p.Key,
                p => p.Key == "A" ? p.Value with { CardNo = card } : p.Value),
            ObjectLocations = initial.ObjectLocations.Where(p => p.Key != "LEGEND").ToDictionary(p => p.Key, p => p.Value) };
        var opened = await OfficialLegendConquestTests.CombatConquer(initial);
        Assert.Empty(Gold(opened.State)); Assert.Single(opened.State.StackItems); Restore(opened.State);
        var done = await Top(opened.State); Assert.Single(Gold(done.State)); Restore(done.State);
    }

    [Theory]
    [InlineData("UNL-222/219")]
    [InlineData("SFD·069/221")]
    public async Task RealReactionDestroysSourceWithoutCancellingItsCapturedGoldSkill(string card)
    {
        var initial = ReactionPosition(card);
        var opened = await ConquestLifecycleRegressionTests.Conquer(initial);
        var first = await Act(opened.State, "P1", new PassPriorityCommand());
        var burned = await Act(first.State, "P2", new PlayCardCommand("COUNTER", "OGN·133/298", []));
        var removed = await Top(burned.State);
        Assert.Contains("UNIT", removed.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(Gold(removed.State)); Assert.Single(removed.State.StackItems); Restore(removed.State);
        var done = await Top(removed.State); Assert.Single(Gold(done.State)); Restore(done.State);
    }

    [Fact]
    public async Task RecoveryRejectsMissingOrForgedCapturedPrintingGenerationAndBattlefield()
    {
        var opened = (await ConquestLifecycleRegressionTests.Conquer(Position())).State;
        var item = Assert.Single(opened.StackItems); var context = Assert.IsType<ConquestTokenContext>(item.ConquestToken);
        foreach (var changed in new[] { item with { ConquestToken = null },
            item with { ConquestToken = context with { CardNo = "OGN·096/298" } },
            item with { ConquestToken = context with { SourceGeneration = context.SourceGeneration + 1 } },
            item with { ConquestToken = context with { BattlefieldId = "missing" } },
            item with { CardNo = "UNL-222/219" } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened with { StackItems = [changed] }),
                e => e.Contains("conquest token", StringComparison.Ordinal));
        Restore(opened);
    }

    internal static MatchState Position(string card = "SFD·069/221")
    {
        var state = ConquestLifecycleRegressionTests.State("OGN·296/298", 0);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { CardNo = card, Power = 2 } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["BF"] = new("P2", "BATTLEFIELD", "BF"), ["UNIT"] = new("P1", "BASE") } };
    }

    internal static MatchState ReactionPosition(string card)
    {
        var initial = AddCounter(Position(card), "OGN·133/298");
        return initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["UNIT"] = initial.CardObjects["UNIT"] with { Damage = 1 } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = new(20,20) } };
    }

    internal static string[] Gold(MatchState state) => state.CardObjects.Values
        .Where(c => c.Tags.Contains(CardObjectTags.EquipmentCard) && c.Tags.Contains("金币"))
        .Select(c => c.ObjectId).ToArray();
}
