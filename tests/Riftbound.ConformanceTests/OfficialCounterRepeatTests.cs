using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialCounterRepeatTests
{
    [Theory]
    [InlineData("OGN·064/298", "S2", "GRAVEYARD", 2)]
    [InlineData("OGN·064/298", "S1", "GRAVEYARD", 1)]
    public async Task CountersRepeatInOrderAndOnlyLiveStackItemsCanBeCountered(string card, string second, string destination, int count)
    {
        var state = Position(card);
        var command = new PlayCardCommand("C", card, ["S1"], OptionalCosts: ["ECHO"], RepeatChoices: [new("", [second])]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        var played = await Submit(state, command);
        Assert.Equal(2, played.State.StackItems.Last().RepeatExecutions!.Count);
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(played.State))!;
        Assert.Empty(MatchRecoveryValidator.Validate(restored.RoomId, 0, [], [], new Dictionary<string, RecoveredPlayerView>(), restored, restored.Tick));
        var result = await ResolveTop(restored);
        var countered = result.Events.Where(e => e.Kind == "STACK_ITEM_COUNTERED").ToArray();
        Assert.Equal(count, countered.Length);
        Assert.Equal("S1", countered[0].Payload["stackItemId"]);
        if (count == 2) Assert.Equal("S2", countered[1].Payload["stackItemId"]);
        Assert.DoesNotContain(result.State.StackItems, s => s.StackItemId == "S1");
        Assert.Equal(count == 1, result.State.StackItems.Any(s => s.StackItemId == "S2"));
        Assert.Single(result.State.PlayerZones["P1"].Graveyard, id => id == "C");
        var returned = destination == "HAND" ? result.State.PlayerZones["P2"].Hand : result.State.PlayerZones["P2"].Graveyard;
        Assert.Equal(count, returned.Count);
        Assert.All(returned, id => Assert.Equal("P2", result.State.CardObjects[id].ControllerId));
        Assert.Equal(1, result.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(2, result.Events.Count(e => e.Kind == "SPELL_EXECUTION_COMPLETED"));
    }

    [Theory]
    [InlineData("UNL-131/219")]
    public async Task InsightCannotChargeForUnsupportedRepeat(string card)
    {
        var state = Position(card);
        var command = new PlayCardCommand("C", card, ["S1"], OptionalCosts: ["ECHO"]);
        var engine = new CoreRuleEngine();
        var candidate = ResolutionResult.BuildPrompts(state)["P1"].Candidates!.Single(c => c.Action == CommandTypes.PlayCard);
        Assert.DoesNotContain(candidate.OptionalCosts ?? [], cost => EchoCostRules.IsEcho(cost.Id));
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay);
        var result = await engine.ResolveAsync(state, new("unsupported-echo", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("OGN·064/298", false)]
    [InlineData("UNL-131/219", true)]
    public async Task CounteredStolenSpellReturnsToOwnerNotItsController(string card, bool hand)
    {
        var state = Position(card);
        state = state with { StackItems = [state.StackItems[0] with { ControllerId = "P1" }],
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
                { ["SPELL1"] = state.CardObjects["SPELL1"] with { ControllerId = "P1" } } };
        var result = await Submit(state, new PlayCardCommand("C", card, ["S1"]));
        result = await ResolveTop(result.State);
        Assert.Contains("SPELL1", hand ? result.State.PlayerZones["P2"].Hand : result.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("SPELL1", result.State.PlayerZones["P1"].Hand.Concat(result.State.PlayerZones["P1"].Graveyard));
        Assert.Equal("P2", result.State.CardObjects["SPELL1"].ControllerId);
        Assert.Equal("P2", result.State.CardObjects["SPELL1"].OwnerId);
    }

    [Fact]
    public async Task StolenSpellResolvesForControllerThenGoesToOwnersGraveyard()
    {
        var state = Position("OGN·064/298");
        state = state with { StackItems = [state.StackItems[0] with { ControllerId = "P1" }] };
        var result = await ResolveTop(state);
        Assert.Single(result.State.PlayerZones["P1"].Hand, id => id == "D1");
        Assert.Contains("SPELL1", result.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("SPELL1", result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("P2", result.State.CardObjects["SPELL1"].ControllerId);
    }

    [Fact]
    public async Task InvalidRepeatStackTargetCannotSpendResourcesOrConsumeTheGrant()
    {
        var state = Position("OGN·064/298");
        var command = new PlayCardCommand("C", "OGN·064/298", ["S1"], OptionalCosts: ["ECHO"], RepeatChoices: [new("", ["MISSING"])]);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static async Task<ResolutionResult> Submit(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("counter-" + state.Tick, "P1", command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }
    private static async Task<ResolutionResult> ResolveTop(MatchState state)
    {
        ResolutionResult result = null!;
        for (var i = 0; i < 2; i++)
        {
            result = await new CoreRuleEngine().ResolveAsync(state, new("pass-" + state.Tick, state.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
        }
        return result;
    }
    internal static MatchState Position(string card)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-061/219", out var draw));
        return new("COUNTER-REPEAT", 1, 3, "P1", new Dictionary<string,string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralClosed,
            priorityPlayerId: "P1", untilEndOfTurnEffects: [EchoCostRules.GrantPrefix + "P1"],
            runePools: new Dictionary<string,RunePool> { ["P1"] = new(20,20), ["P2"] = new(20,20) },
            playerZones: new Dictionary<string,PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["C"], MainDeck = ["D1","D2"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string,CardObjectState> {
                ["C"] = new("C", cardNo: card, ownerId: "P1", controllerId: "P1"),
                ["SPELL1"] = new("SPELL1", cardNo: "UNL-061/219", ownerId: "P2", controllerId: "P2"),
                ["SPELL2"] = new("SPELL2", cardNo: "UNL-061/219", ownerId: "P2", controllerId: "P2"),
                ["D1"] = new("D1", cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1"),
                ["D2"] = new("D2", cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1") },
            stackItems: [new("S1", "P2", "SPELL1", draw.EffectKind, draw.CardNo, [], 0, 1),
                new("S2", "P2", "SPELL2", draw.EffectKind, draw.CardNo, [], 0, 1)]);
    }
}
