using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class BeginningRuneConditionTests
{
    internal static MatchState Position(string cardNo, int ownRunes, int opposingRunes)
    {
        var state = OfficialHoldSequenceTests.AddUnit(
            OfficialHoldSequenceTests.State("OGN·294/298"), "SOURCE", cardNo, "F", "P1");
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var positions = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        cards["SOURCE"] = cards["SOURCE"] with { Power = cardNo == "VEN·005" ? 2 : 4 };
        foreach (var (player, count) in new[] { ("P1", ownRunes), ("P2", opposingRunes) })
        {
            var ids = Enumerable.Range(0, count).Select(i => $"{player}-CONTROLLED-RUNE-{i}").ToArray();
            foreach (var id in ids)
            {
                cards[id] = new(id, cardNo: "OGN·007/298", ownerId: player, controllerId: player,
                    isExhausted: true, tags: [CardObjectTags.RuneCard, "COLOR:red"]);
                positions[id] = new(player, "BASE");
            }
            zones[player] = zones[player] with { Base = zones[player].Base.Concat(ids).ToArray() };
        }
        return state with { CardObjects = cards, ObjectLocations = positions, PlayerZones = zones };
    }

    [Theory]
    [InlineData("VEN·005", 2, 1, false)]
    [InlineData("VEN·006", 4, 2, true)]
    public async Task FewerControlledRunesAtBeginningQueuesBeforeChannelAndGrantsUntilTurnEnd(
        string cardNo, int printedMight, int bonus, bool ganking)
    {
        var start = await Act(Position(cardNo, 0, 1), "P1", new PassPriorityCommand());
        Assert.Equal(MatchPhases.TurnStart, start.State.Phase);
        Assert.Equal("SCORE", start.State.TurnStartStep);
        Assert.Equal(3, start.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Single(start.State.StackItems);
        Assert.Equal(printedMight, start.State.CardObjects["SOURCE"].Power);
        Restore(start.State);

        var resolved = await Top(start.State);
        Assert.Equal(printedMight + bonus, resolved.State.CardObjects["SOURCE"].Power);
        Assert.Equal(ganking, resolved.State.CardObjects["SOURCE"].UntilEndOfTurnEffects.Contains("ROAM"));
        Assert.Equal(MatchPhases.Main, resolved.State.Phase);
        Restore(resolved.State);
    }

    [Theory]
    [InlineData("VEN·005", 1, 1)]
    [InlineData("VEN·006", 1, 1)]
    [InlineData("VEN·005", 2, 1)]
    [InlineData("VEN·006", 2, 1)]
    public async Task EqualOrMoreControlledRunesDoesNotCreateTheBeginningTrigger(string cardNo, int own, int opposing)
    {
        var result = await Act(Position(cardNo, own, opposing), "P1", new PassPriorityCommand());
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.TriggerQueue);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(cardNo == "VEN·005" ? 2 : 4, result.State.CardObjects["SOURCE"].Power);
        Restore(result.State);
    }

    [Theory]
    [InlineData("VEN·005", 3)]
    [InlineData("VEN·006", 6)]
    public async Task TriggerConditionIsCapturedEvenIfTheRuneCountsLaterBecomeEqual(string cardNo, int expectedMight)
    {
        var opened = await Act(Position(cardNo, 1, 2), "P1", new PassPriorityCommand());
        Assert.Single(opened.State.StackItems);
        var state = opened.State;
        const string moved = "P2-CONTROLLED-RUNE-1";
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with {
                    Base = state.PlayerZones["P2"].Base.Where(id => id != moved).ToArray(),
                    RuneDeck = state.PlayerZones["P2"].RuneDeck.Append(moved).ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [moved] = new("P2", "RUNE_DECK") },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [moved] = state.CardObjects[moved] with { ObjectGeneration = state.CardObjects[moved].ObjectGeneration + 1 } } };
        Restore(state);
        var resolved = await Top(state);
        Assert.Equal(expectedMight, resolved.State.CardObjects["SOURCE"].Power);
        Restore(resolved.State);
    }

    [Theory]
    [InlineData("VEN·005", 2)]
    [InlineData("VEN·006", 4)]
    public async Task DepartedSourceCannotGiveItsNewIncarnationTheEarnedModifier(string cardNo, int printedMight)
    {
        var opened = await Act(Position(cardNo, 0, 1), "P1", new PassPriorityCommand());
        Assert.Single(opened.State.StackItems);
        var state = opened.State;
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with {
                    Battlefields = state.PlayerZones["P1"].Battlefields.Where(id => id != "SOURCE").ToArray(),
                    Graveyard = state.PlayerZones["P1"].Graveyard.Append("SOURCE").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SOURCE"] = new("P1", "GRAVEYARD") },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SOURCE"] = state.CardObjects["SOURCE"] with { ObjectGeneration = state.CardObjects["SOURCE"].ObjectGeneration + 1 } } };
        Restore(state);
        var resolved = await Top(state);
        Assert.Equal(printedMight, resolved.State.CardObjects["SOURCE"].Power);
        Assert.DoesNotContain("ROAM", resolved.State.CardObjects["SOURCE"].UntilEndOfTurnEffects);
        Restore(resolved.State);
    }

    [Theory]
    [InlineData("VEN·005")]
    [InlineData("VEN·006")]
    public async Task EarnedAbilityDoesNotExposeTheSourcesNewPrivateIncarnation(string cardNo)
    {
        var opened = await Act(Position(cardNo, 0, 1), "P1", new PassPriorityCommand());
        Assert.Single(opened.State.StackItems);
        var state = opened.State;
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with {
                    Battlefields = state.PlayerZones["P1"].Battlefields.Where(id => id != "SOURCE").ToArray(),
                    Hand = state.PlayerZones["P1"].Hand.Append("SOURCE").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SOURCE"] = new("P1", "HAND") },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SOURCE"] = state.CardObjects["SOURCE"] with { ObjectGeneration = state.CardObjects["SOURCE"].ObjectGeneration + 1 } } };
        var opponent = ResolutionResult.BuildSnapshots(state)["P2"];
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state).SpectatorSnapshot;
        foreach (var view in new[] { opponent, spectator }) {
            var stack = JsonSerializer.SerializeToElement(Assert.Single(view.Stack));
            Assert.Equal("HIDDEN", stack.GetProperty("sourceObjectId").GetString());
            Assert.DoesNotContain("SOURCE", JsonSerializer.Serialize(view));
        }
        Restore(state);
        await Top(state);
    }
}
