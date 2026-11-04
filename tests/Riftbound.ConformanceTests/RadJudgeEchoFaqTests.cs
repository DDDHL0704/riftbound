using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadJudgeEchoFaqTests
{
    [Fact]
    public async Task TimeGateEchoAndMeditationsOptionalExhaustDrawExactlyThreeCards()
    {
        var state = Put(Put(Create(), "GATE", "SFD·078/221", "P1", "BASE"),
            "SPELL", "OGN·048/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("GATE", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, []));
        Assert.True(game.State.CardObjects["GATE"].IsExhausted);
        await game.Top();
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·048/298", out var meditation));
        var echo = Assert.Single(EchoCostRules.Available(game.State, "P1", meditation));
        var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", [],
            OptionalCosts: [echo.Id, "EXHAUST_FRIENDLY_UNIT:ALLY"]));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(deck.Take(3), game.State.PlayerZones["P1"].Hand);
        Assert.Equal(deck.Skip(3), game.State.PlayerZones["P1"].MainDeck);
        Assert.Single(game.State.CardPlays.Finalized);
        Assert.Single(game.State.CardPlays.Resolved);
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("duplicate-echo")]
    [InlineData("duplicate-exhaust")]
    [InlineData("forged-echo")]
    public async Task CombinedEchoAndExhaustRejectsRepeatedOrForgedCostsAtomically(string invalid)
    {
        var state = Put(Put(Create(), "GATE", "SFD·078/221", "P1", "BASE"),
            "SPELL", "OGN·048/298", "P1", "HAND");
        var game = new Driver(state) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new ActivateAbilityCommand("GATE", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, []));
        await game.Top();
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·048/298", out var meditation));
        var echo = Assert.Single(EchoCostRules.Available(game.State, "P1", meditation)).Id;
        const string exhaust = "EXHAUST_FRIENDLY_UNIT:ALLY";
        string[] costs = invalid switch
        {
            "duplicate-echo" => [echo, echo, exhaust],
            "duplicate-exhaust" => [echo, exhaust, exhaust],
            _ => ["ECHO:GRANTED:99", exhaust]
        };
        var hash = MatchStateHasher.Hash(game.State);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State));
        var result = await new CoreRuleEngine().ResolveAsync(game.State,
            new("invalid-combined-echo-cost", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", "OGN·048/298", [], OptionalCosts: costs), default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
        await game.ColdCheckpoint();
    }
}
