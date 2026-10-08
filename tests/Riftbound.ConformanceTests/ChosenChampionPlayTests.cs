using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ChosenChampionPlayTests
{
    private const string Jhin = "UNL-022/219";

    [Fact]
    public void ChampionZoneIsAnOrdinaryPlaySource_Cn108_3dAnd419_1a()
    {
        var state = Position();
        var play = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(play.Sources!, source => source.Id == "CARD");
        var metadata = JsonSerializer.SerializeToElement(play.Metadata);
        Assert.Contains(metadata.GetProperty("sourceRequirements").EnumerateArray(), r => r.GetProperty("sourceObjectId").GetString() == "CARD");
    }

    [Fact]
    public async Task ChampionUsesNormalCostAndEntersBaseExactlyOnce()
    {
        var state = Position();
        var command = new PlayCardCommand("CARD", Jhin, [], Destination: "BASE");
        var engine = new CoreRuleEngine();
        var before = MatchStateHasher.Hash(state);
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(4, quote.Cost!.Mana);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        var result = await engine.ResolveAsync(state, new("hero", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.PlayerZones["P1"].ChampionZone);
        Assert.Equal("CARD", Assert.Single(result.State.PlayerZones["P1"].Base));
        Assert.True(result.State.CardObjects["CARD"].IsExhausted);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Equal("BASE", result.State.ObjectLocations["CARD"].Zone);
        var duplicate = await engine.ResolveAsync(result.State, new("again", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(duplicate.Accepted);
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(duplicate.State));
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("trait")]
    [InlineData("opponent")]
    [InlineData("timing")]
    public async Task IllegalChampionPlayPreservesState(string reason)
    {
        var state = Position();
        if (reason == "mana") state = state with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(3, 0, new Dictionary<string, int> { ["red"] = 1 }) } };
        if (reason == "trait") state = state with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(4, 0, new Dictionary<string, int> { ["blue"] = 1 }) } };
        if (reason == "timing") state = state with { TimingState = TimingStates.NeutralClosed };
        var command = new PlayCardCommand(reason == "opponent" ? "P2-CARD" : "CARD", Jhin, [], Destination: "BASE");
        var engine = new CoreRuleEngine();
        Assert.False(engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command)).CanPay);
        var result = await engine.ResolveAsync(state, new("illegal", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    internal static MatchState Position()
    {
        var state = PlayCostPreviewTests.Position(new(4, 0, new Dictionary<string, int> { ["red"] = 1 }), Jhin);
        return state with
        {
            PlayerZones = state.PlayerZones.ToDictionary(x => x.Key, x => x.Value with { Hand = [], ChampionZone = x.Value.Hand }),
            CardObjects = state.CardObjects.ToDictionary(x => x.Key, x => x.Value with { Tags = [CardObjectTags.UnitCard, "CARD_CATEGORY:英雄单位"] })
        };
    }
}
