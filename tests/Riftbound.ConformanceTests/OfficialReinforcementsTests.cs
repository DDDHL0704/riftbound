using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialReinforcementsTests
{
    [Theory]
    [InlineData("SFD·125/221", 0)]
    [InlineData("OGN·142/298", 4)]
    public async Task SelectUnitFromTopFivePaysReducedManaAndEntersExhausted(string unit, int remainingMana)
    {
        var opened = await Open(unit);
        var state = opened.State;
        var pending = Assert.IsType<PendingEffectPlayState>(state.PendingEffectPlay);
        Assert.Equal("MAIN_DECK", pending.SourceZone);
        Assert.Equal(["U", "N1", "N2", "N3", "N4"], pending.ViewedCardIds);
        Assert.Equal(["U"], pending.Sources.Keys);
        Assert.DoesNotContain("\"U\"", JsonSerializer.Serialize(opened.Prompts["P2"]));
        Assert.DoesNotContain("\"N1\"", JsonSerializer.Serialize(opened.Snapshots["P2"]));
        Assert.DoesNotContain("\"N1\"", JsonSerializer.Serialize(opened.Events));
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored));
        Assert.Empty(MatchRecoveryValidator.Validate(restored.RoomId, 0, [], [], new Dictionary<string, RecoveredPlayerView>(), restored, restored.Tick));
        var command = new PlayCardCommand("U", unit, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(remainingMana, quote.Cost!.Mana);
        var result = await Act(restored, command);
        Assert.Null(result.State.PendingEffectPlay);
        Assert.Contains("U", result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("U", result.State.PlayerZones["P1"].MainDeck);
        Assert.True(result.State.CardObjects["U"].IsExhausted);
        Assert.Equal(state.RunePools["P1"].Mana - remainingMana, result.State.RunePools["P1"].Mana);
        Assert.Equal("TAIL", result.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal(5, result.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(4, Assert.Single(result.Events, e => e.Kind == "CARDS_RECYCLED").Payload["count"]);
        Assert.Contains("CARD", result.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("\"N1\"", JsonSerializer.Serialize(result.Events));
    }

    [Fact]
    public async Task MayDeclineAndCannotPickBelowTopFiveOrSkipPrintedPower()
    {
        var opened = await Open("SFD·143/221");
        var state = opened.State with { RunePools = new Dictionary<string,RunePool>(opened.State.RunePools) { ["P1"] = new(20,0) } };
        var engine = new CoreRuleEngine();
        foreach (var command in new[] { new PlayCardCommand("TAIL", "SFD·125/221", []), new PlayCardCommand("N1", "SFD·022/221", []), new PlayCardCommand("U", "SFD·143/221", []) })
        {
            var result = await engine.ResolveAsync(state, new("bad", "P1", CommandTypes.PlayCard), command, default);
            Assert.False(result.Accepted);
            Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        }
        var pending = state.PendingEffectPlay!;
        var forged = state with { PendingEffectPlay = pending with { ManaReduction = 100 } };
        Assert.Contains(MatchRecoveryValidator.Validate(forged.RoomId, 0, [], [], new Dictionary<string, RecoveredPlayerView>(), forged, forged.Tick), e => e.Contains("deck effect-play"));
        var declined = await Act(state, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Null(declined.State.PendingEffectPlay);
        Assert.Empty(declined.State.PlayerZones["P1"].Base);
        Assert.Equal(6, declined.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal("TAIL", declined.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal(5, Assert.Single(declined.Events, e => e.Kind == "CARDS_RECYCLED").Payload["count"]);
    }

    [Fact]
    public async Task UnitPlayedFromDeckStillQueuesItsOwnPlayAbility()
    {
        var opened = await Open("SFD·058/221");
        var played = await Act(opened.State, new PlayCardCommand("U", "SFD·058/221", []));
        Assert.Null(played.State.PendingEffectPlay);
        Assert.Null(played.State.PendingCardChoice);
        Assert.Contains("CARD", played.State.PlayerZones["P1"].Graveyard);
        Assert.Single(played.State.StackItems);
        Assert.True(played.State.StackItems[0].SourceConfirmed);
        var first = await Pass(played.State);
        var second = await Pass(first.State);
        Assert.NotNull(second.State.PendingCardChoice);
        Assert.Equal(played.State.PlayerZones["P1"].MainDeck.Take(4), second.State.PendingCardChoice!.ContextObjectIds);
    }

    private static async Task<ResolutionResult> Act(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("reinforce-"+state.Tick, "P1", command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage); return result;
    }
    private static async Task<ResolutionResult> Pass(MatchState state)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("pass-"+state.Tick, state.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(result.Accepted, result.ErrorMessage); return result;
    }
    internal static async Task<ResolutionResult> Open(string unit)
    {
        var initial = Position(unit);
        Assert.DoesNotContain("\"N1\"", JsonSerializer.Serialize(ResolutionResult.BuildPrompts(initial)));
        var played = await Act(initial, new PlayCardCommand("CARD", "OGN·062/298", []));
        Assert.Null(played.State.PendingEffectPlay);
        var first = await Pass(played.State);
        return await Pass(first.State);
    }
    internal static MatchState Position(string unit)
    {
        var state = OfficialCounterRepeatTests.Position("OGN·062/298");
        var cards = new Dictionary<string,CardObjectState> { ["CARD"] = new("CARD", cardNo: "OGN·062/298", ownerId:"P1", controllerId:"P1"),
            ["U"] = new("U", cardNo: unit, tags: [CardObjectTags.UnitCard], ownerId:"P1", controllerId:"P1"),
            ["TAIL"] = new("TAIL", cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId:"P1", controllerId:"P1") };
        foreach (var id in new[] { "N1","N2","N3","N4" }) cards[id] = new(id, cardNo:"SFD·022/221", tags:[CardObjectTags.EquipmentCard], ownerId:"P1", controllerId:"P1");
        return state with { StackItems=[], PriorityPlayerId=null, TimingState=TimingStates.NeutralOpen, UntilEndOfTurnEffects=[], CardObjects=cards,
            PlayerZones=new Dictionary<string,PlayerZones> { ["P1"]=PlayerZones.Empty with { Hand=["CARD"], MainDeck=["U","N1","N2","N3","N4","TAIL"] }, ["P2"]=PlayerZones.Empty } };
    }
}
