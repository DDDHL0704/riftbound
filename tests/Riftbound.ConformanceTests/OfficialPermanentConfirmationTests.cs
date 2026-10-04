using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 359.2 / 359.3: permanent confirmation and its triggered ability are distinct.
public sealed class OfficialPermanentConfirmationTests
{
    [Theory]
    [InlineData("SFD·125/221", true, true)]
    [InlineData("SFD·006/221", true, false)]
    [InlineData("OGN·021/298", false, false)]
    [InlineData("OGN·017/298", false, true)]
    public async Task PlainPermanentEntersWithoutOpponentPassing(string cardNo, bool unit, bool exhausted)
    {
        var result = await Play(Position(cardNo, unit));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains("CARD", result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(exhausted, result.State.CardObjects["CARD"].IsExhausted);
        Assert.Equal(2, result.State.Tick);
        Assert.DoesNotContain(result.Events, e => e.Kind == "PRIORITY_PASSED");
    }

    [Fact]
    public async Task UnitEntersBeforeItsPlayTriggerAndCannotReenterOnTriggerResolution()
    {
        var result = await Play(Position("OGN·087/298", true));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains("CARD", result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("DRAW", result.State.PlayerZones["P1"].Hand);
        Assert.Single(result.State.StackItems);
        var state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(state));
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["CARD"] = cards["CARD"] with { IsExhausted = false, Damage = 1 };
        state = state with { CardObjects = cards };
        var engine = new CoreRuleEngine();
        for (var i = 0; i < 2; i++)
        {
            result = await engine.ResolveAsync(state, new($"pass{i}", state.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
        }
        Assert.Contains("DRAW", state.PlayerZones["P1"].Hand);
        Assert.Empty(state.StackItems);
        Assert.False(state.CardObjects["CARD"].IsExhausted);
        Assert.Equal(1, state.CardObjects["CARD"].Damage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_PLAYED_TO_BASE");
    }

    [Fact]
    public async Task PlayAbilityRemainsAfterItsUnitLeavesTheField()
    {
        var result = await Play(Position("OGN·087/298", true));
        Assert.True(result.Accepted, result.ErrorMessage);
        var zones = result.State.PlayerZones.ToDictionary(x => x.Key, x => x.Value);
        zones["P1"] = zones["P1"] with { Base = [], Graveyard = ["CARD"] };
        result = result with { State = result.State with { PlayerZones = zones } };
        var resolved = await PermanentConfirmationAssert.ResolveAfterPlayAsync(new CoreRuleEngine(), result);
        Assert.Contains("DRAW", resolved.State.PlayerZones["P1"].Hand);
        Assert.Contains("CARD", resolved.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("CARD", resolved.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task SpellRetainsResponseWindow()
    {
        var state = Position("OGN·009/298", false);
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["CARD"] = cards["CARD"] with { Tags = [CardObjectTags.SpellCard] };
        cards["TARGET"] = new("TARGET", cardNo: "SFD·125/221", power: 4, tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2");
        state = state with { CardObjects = cards, PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
        { ["P2"] = PlayerZones.Empty with { Battlefields = ["TARGET"] } } };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("play", "P1", CommandTypes.PlayCard), new PlayCardCommand("CARD", "OGN·009/298", ["TARGET"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Single(result.State.StackItems);
        Assert.Equal(0, result.State.CardObjects["TARGET"].Damage);
        Assert.Equal("P1", result.State.PriorityPlayerId);
    }

    internal static MatchState Position(string cardNo, bool unit) => new("CONFIRM", 1, 2, "P1",
        new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
        phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
        runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) },
        playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["CARD"], MainDeck = ["DRAW"] }, ["P2"] = PlayerZones.Empty },
        cardObjects: new Dictionary<string, CardObjectState> {
            ["CARD"] = new("CARD", cardNo: cardNo, power: 4, tags: [unit ? CardObjectTags.UnitCard : CardObjectTags.EquipmentCard], ownerId: "P1", controllerId: "P1"),
            ["DRAW"] = new("DRAW", cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1") });
    private static ValueTask<ResolutionResult> Play(MatchState state) => new CoreRuleEngine().ResolveAsync(state,
        new("play", "P1", CommandTypes.PlayCard), new PlayCardCommand("CARD", state.CardObjects["CARD"].CardNo!, []), default);
}
