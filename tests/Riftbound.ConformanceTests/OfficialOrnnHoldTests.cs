using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialOrnnHoldTests
{
    [Theory]
    [InlineData("SFD·058/221", true, false)]
    [InlineData("SFD·058a/221", true, false)]
    [InlineData("SFD·058/221", false, false)]
    [InlineData("SFD·058/221", true, true)]
    public async Task OrnnHoldPrivatelyLooksAfterResponsesThenResumesTheStartPhase(string card, bool choose, bool sourceLeaves)
    {
        var state = Position(card);
        var result = await Act(state, new PassPriorityCommand());
        Assert.Single(result.State.StackItems);
        Assert.Equal("HOLD_LOOK_EQUIPMENT", result.State.StackItems[0].EffectKind);
        Assert.Null(result.State.PendingCardChoice);
        Assert.DoesNotContain("\"E1\"", JsonSerializer.Serialize(result.Prompts));
        if (sourceLeaves)
        {
            var cards = new Dictionary<string, CardObjectState>(result.State.CardObjects)
                { ["ORNN"] = result.State.CardObjects["ORNN"] with { ObjectGeneration = 1 } };
            result = result with { State = result.State with { CardObjects = cards,
                PlayerZones = new Dictionary<string, PlayerZones>(result.State.PlayerZones)
                    { ["P1"] = result.State.PlayerZones["P1"] with { Battlefields = ["F"], Graveyard = ["ORNN"] } } } };
        }
        result = await Drain(result.State);
        var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        Assert.Equal(["E1","U1","U2","E2"], choice.ContextObjectIds);
        Assert.Equal(["E1","E2"], choice.LegalObjectIds);
        Assert.Equal(MatchPhases.TurnStart, result.State.Phase);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, result.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.DoesNotContain("\"E1\"", JsonSerializer.Serialize(result.Snapshots["P2"]));
        Assert.DoesNotContain("\"E1\"", JsonSerializer.Serialize(result.Prompts["P2"]));
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored));
        Assert.Empty(MatchRecoveryValidator.Validate(restored.RoomId, 0, [], [], new Dictionary<string, RecoveredPlayerView>(), restored, restored.Tick));
        var invalid = await new CoreRuleEngine().ResolveAsync(restored, new("invalid", "P1", CommandTypes.ChooseCards), new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["U1"]), default);
        Assert.False(invalid.Accepted);
        Assert.Equal(MatchStateHasher.Hash(restored), MatchStateHasher.Hash(invalid.State));
        result = await Act(restored, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, choose ? ["E2"] : []));
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Null(result.State.TurnStartStep);
        Assert.Empty(result.State.StackItems);
        Assert.Single(result.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains("TAIL", result.State.PlayerZones["P1"].Hand);
        Assert.Equal(choose, result.State.PlayerZones["P1"].Hand.Contains("E2"));
        Assert.Equal(choose, result.Events.Any(e => e.Kind == "CARD_REVEALED"));
        Assert.DoesNotContain("\"E1\"", JsonSerializer.Serialize(result.Events));
        Assert.Equal(sourceLeaves, result.State.PlayerZones["P1"].Graveyard.Contains("ORNN"));
        Assert.DoesNotContain("ORNN", result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task AUnitAtAnotherPlayersBattlefieldCannotTriggerFromYourHold()
    {
        var state = Position("SFD·058/221");
        state = state with { ObjectLocations = new Dictionary<string,ObjectLocationState>(state.ObjectLocations)
            { ["ORNN"] = new("P1", "BATTLEFIELD", "OTHER") } };
        var result = await Act(state, new PassPriorityCommand());
        Assert.DoesNotContain(result.State.StackItems, s => s.EffectKind == "HOLD_LOOK_EQUIPMENT");
        Assert.Null(result.State.PendingCardChoice);
    }

    private static async Task<ResolutionResult> Act(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("ornn-"+state.Tick, state.PriorityPlayerId ?? "P1", command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage); return result;
    }
    private static async Task<ResolutionResult> Drain(MatchState state)
    {
        var first = await Act(state, new PassPriorityCommand());
        return await Act(first.State, new PassPriorityCommand());
    }
    internal static MatchState Position(string no)
    {
        var cards = new Dictionary<string,CardObjectState> {
            ["F"] = new("F", cardNo: "OGN·294/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1", controllerId: "P1"),
            ["OTHER"] = new("OTHER", cardNo: "OGN·294/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2"),
            ["ORNN"] = new("ORNN", cardNo: no, tags: [CardObjectTags.UnitCard], power: 5, ownerId: "P1", controllerId: "P1") };
        var deck = new[] { "E1","U1","U2","E2","TAIL","TAIL2" };
        foreach (var id in deck) cards[id] = new(id, cardNo: id.StartsWith("E") ? "SFD·022/221" : "SFD·125/221",
            tags: [id.StartsWith("E") ? CardObjectTags.EquipmentCard : CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1");
        var runes = new[] { "R1","R2","R3" };
        foreach (var id in runes) cards[id] = new(id, cardNo: "OGN·007/298", tags: [CardObjectTags.RuneCard,"COLOR:red"], ownerId: "P1", controllerId: "P1");
        return new("ORNN-HOLD", 0, 5, "P1", new Dictionary<string,string> { ["P1"]="P1", ["P2"]="P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.TurnStart, timingState: TimingStates.NeutralClosed,
            cardObjects: cards, objectLocations: new Dictionary<string,ObjectLocationState> { ["F"] = new("P1","BATTLEFIELD"), ["OTHER"] = new("P2","BATTLEFIELD"), ["ORNN"] = new("P1","BATTLEFIELD","F") },
            playerZones: new Dictionary<string,PlayerZones> { ["P1"] = PlayerZones.Empty with { MainDeck = deck, RuneDeck = runes, Battlefields = ["F","ORNN"] }, ["P2"] = PlayerZones.Empty with { Battlefields = ["OTHER"] } });
    }
}
