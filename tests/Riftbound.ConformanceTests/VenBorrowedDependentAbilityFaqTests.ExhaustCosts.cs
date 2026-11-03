using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedStrongUnitPosition(bool aura = false)
    {
        var state = HandDiscardObserverTests.Put(BorrowedRecyclePosition("RAD·035/167"),
            "STRONG", aura ? "OGN·219/298" : "OGN·049/298", "P1", "BASE");
        return aura ? HandDiscardObserverTests.Put(state, "WARD", "RAD·039/167", "P1", "BASE") : state;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BorrowedStrongUnitCostUsesCurrentMightAndPaysBeforeIndependentDraw(bool aura, bool recall)
    {
        var initial = BorrowedStrongUnitPosition(aura);
        if (recall) initial = RadUnitEffectTestFixture.Place(initial, "STRONG", "P1", "BATTLEFIELD", "F");
        var id = TemplateId(initial, "休眠一名友方强力单位");
        var game = new VenBasicFlowSpellTests.Driver(initial);
        Assert.Contains("EXHAUST_FRIENDLY_UNIT:STRONG", JsonSerializer.Serialize(game.PromptFor("P1")));
        Assert.DoesNotContain("EXHAUST_FRIENDLY_UNIT:HOST", JsonSerializer.Serialize(game.PromptFor("P1")));
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [], ["EXHAUST_FRIENDLY_UNIT:STRONG"]));
        Assert.True(game.State.CardObjects["STRONG"].IsExhausted);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        Assert.DoesNotContain("TOP", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(initial.RunePools["P1"], game.State.RunePools["P1"]);
        Assert.Contains(game.Events(), e => e.Event.Kind == "UNIT_EXHAUSTED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "STRONG")
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "ABILITY_COST"));
        if (recall)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
            Assert.Equal("HAND", game.State.ObjectLocations["HOST"].Zone);
        }
        await game.Top();
        Assert.Contains("TOP", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["STRONG"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedStrongUnitCostRejectsWeakExhaustedEnemyAndDuplicateChoicesAtomically()
    {
        var initial = BorrowedStrongUnitPosition();
        var id = TemplateId(initial, "休眠一名友方强力单位");
        async Task Reject(MatchState state, string[] choices, string[]? targets = null)
        {
            var hash = MatchStateHasher.Hash(state);
            var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-borrowed-exhaust", "P1", CommandTypes.ActivateAbility),
                new ActivateAbilityCommand("HOST", id, targets ?? [], choices), default);
            Assert.False(result.Accepted, string.Join(',', choices));
            Assert.Empty(result.Events);
            Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        }
        await Reject(initial, []);
        foreach (var invalid in new[] { "HOST", "GEAR", "DONOR", "G1", "ENEMY-G", "MISSING" })
            await Reject(initial, ["EXHAUST_FRIENDLY_UNIT:" + invalid]);
        await Reject(initial, ["EXHAUST_FRIENDLY_UNIT:STRONG", "EXHAUST_FRIENDLY_UNIT:STRONG"]);
        await Reject(initial, ["EXHAUST_FRIENDLY_UNIT:STRONG"], ["STRONG"]);
        foreach (var exhausted in new[] { false, true })
        {
            var unpayable = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["STRONG"] = initial.CardObjects["STRONG"] with { IsExhausted = exhausted, Power = exhausted ? 5 : 4 } } };
            Assert.DoesNotContain(id, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(unpayable)));
            await Reject(unpayable, ["EXHAUST_FRIENDLY_UNIT:STRONG"]);
        }
    }
}
