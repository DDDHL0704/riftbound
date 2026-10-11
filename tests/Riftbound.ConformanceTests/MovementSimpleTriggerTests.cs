using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ChosenMovementTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MovementSimpleTriggerTests
{
    internal static MatchState Position(string cardNo, string origin = "BATTLEFIELD")
    {
        var s = MovementObserverAuditTests.Position(cardNo);
        s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["DRAW-TOP"] = new("DRAW-TOP", cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]),
                ["DRAW-NEXT"] = new("DRAW-NEXT", cardNo: "OGN·002/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P2"] = s.PlayerZones["P2"] with { MainDeck = ["DRAW-TOP", "DRAW-NEXT"] } }
        };
        return origin == "BASE" ? At(s, "D", "P2", "BASE") : s;
    }

    public static IEnumerable<object[]> Moves()
    {
        foreach (var card in new[] { "SFD·048/221", "SFD·137/221", "UNL-115/219", "UNL-127/219" })
        foreach (var route in new[] { ("BASE", "BATTLEFIELD:BF2"), ("BATTLEFIELD", "BASE:P2"), ("BATTLEFIELD", "BATTLEFIELD:BF2") })
            yield return [card, route.Item1, route.Item2];
    }

    [Theory]
    [MemberData(nameof(Moves))]
    public async Task OfficialMovementBenefitsUseCapturedControllerAndExactRoute(string card, string origin, string destination)
    {
        var s = Position(card, origin);
        var moved = await MovementObserverAuditTests.Move(s, destination);
        var expected = card != "SFD·137/221" || origin == "BATTLEFIELD";
        expected &= card != "UNL-127/219" || destination.StartsWith("BATTLEFIELD:");
        Assert.Equal(s.PlayerZones["P2"].Hand.Count, moved.State.PlayerZones["P2"].Hand.Count);
        Assert.Equal(s.PlayerExperience.GetValueOrDefault("P2"), moved.State.PlayerExperience.GetValueOrDefault("P2"));
        Assert.Equal(s.CardObjects["D"].Power, moved.State.CardObjects["D"].Power);
        Restore(moved.State);
        if (!expected) { Assert.Empty(moved.State.StackItems); return; }
        var skill = Assert.Single(moved.State.StackItems);
        Assert.Equal("P2", skill.ControllerId);
        Assert.Equal("P1", skill.MovementObserver!.Unit.MovingPlayerId);
        var done = await Top(moved.State);
        if (card == "SFD·048/221") {
            Assert.Contains("DRAW-TOP", done.State.PlayerZones["P2"].Hand);
            Assert.False(ShowsCard(done.Snapshots["P1"], "OGN·001/298"));
            Assert.True(ShowsCard(done.Snapshots["P2"], "OGN·001/298"));
        }
        else if (card == "SFD·137/221") Assert.Equal(s.CardObjects["D"].Power + 2, done.State.CardObjects["D"].Power);
        else Assert.Equal(s.PlayerExperience.GetValueOrDefault("P2") + (card == "UNL-115/219" ? 1 : 2), done.State.PlayerExperience["P2"]);
        Assert.Equal(s.PlayerExperience.GetValueOrDefault("P1"), done.State.PlayerExperience.GetValueOrDefault("P1"));
        Restore(done.State);
    }
    [Theory]
    [InlineData("SFD·048/221")]
    [InlineData("UNL-115/219")]
    [InlineData("UNL-127/219")]
    public async Task DrawAndExperienceSurviveTheSourceLeaving(string card)
    {
        var moved = await MovementObserverAuditTests.Move(Position(card));
        var s = moved.State;
        s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with {
                Battlefields = s.PlayerZones["P2"].Battlefields.Where(id => id != "D").ToArray() }, ["P1"] = s.PlayerZones["P1"] with { Graveyard = [..s.PlayerZones["P1"].Graveyard, "D"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["D"] = new("P1", "GRAVEYARD") },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["D"] = s.CardObjects["D"] with { ObjectGeneration = 1, ControllerId = "P1" } } };
        Restore(s); var done = await Top(s);
        if (card == "SFD·048/221") Assert.Contains("DRAW-TOP", done.State.PlayerZones["P2"].Hand);
        else Assert.Equal(card == "UNL-115/219" ? 1 : 2, done.State.PlayerExperience["P2"]);
        Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SelfBuffFollowsAControlChangeButNeverANewIncarnation(bool newIncarnation)
    {
        var moved = await MovementObserverAuditTests.Move(Position("SFD·137/221"), "BASE:P2");
        var s = At(moved.State, "D", "P1", "BASE");
        if (newIncarnation) s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["D"] = s.CardObjects["D"] with { ObjectGeneration = 1 } } };
        Restore(s); var done = await Top(s);
        Assert.Equal(newIncarnation ? 2 : 4, done.State.CardObjects["D"].Power);
        Restore(done.State);
    }

    [Fact]
    public async Task MovementDrawFeedsTheSecondDrawObserverAndPreservesPrivateCards()
    {
        var s = Position("SFD·048/221");
        s = s with { DrawLedger = new(s.TurnNumber, new Dictionary<string, int> { ["P2"] = 1 }),
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Base = [..s.PlayerZones["P2"].Base, "G"] } },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["G"] = new("G", cardNo: "UNL-074/219", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.EquipmentCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["G"] = new("P2", "BASE") } };
        var moved = await MovementObserverAuditTests.Move(s, "BASE:P2");
        var drawn = await Top(moved.State);
        Assert.Equal(2, drawn.State.DrawLedger.Counts["P2"]);
        Assert.Equal("TRIGGER_CONFIRMATION", drawn.State.PendingCardChoice!.ChoiceWindow);
        var selected = await Act(drawn.State, "P2", new ChooseCardsCommand(drawn.State.PendingCardChoice.ChoiceId, "TRIGGER_CONFIRMATION", ["D"]));
        var done = await Top(selected.State); Assert.Equal(4, done.State.CardObjects["D"].Power);
        Assert.False(ShowsCard(done.Snapshots["P1"], "OGN·001/298")); Restore(done.State);
    }

    [Fact]
    public async Task MovementDrawUsesTheSharedBurnoutAndRecycleFlow()
    {
        var s = Position("SFD·048/221");
        s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { MainDeck = [], Graveyard = ["DRAW-TOP", "DRAW-NEXT"] } } };
        var moved = await MovementObserverAuditTests.Move(s, "BASE:P2"); var done = await Top(moved.State);
        Assert.Equal(s.PlayerScores.GetValueOrDefault("P1") + 1, done.State.PlayerScores["P1"]);
        Assert.Single(done.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Equal(s.PlayerZones["P2"].Hand.Count + 1, done.State.PlayerZones["P2"].Hand.Count); Assert.Single(done.State.PlayerZones["P2"].MainDeck); Assert.Empty(done.State.PlayerZones["P2"].Graveyard);
        Restore(done.State);
    }

    private static bool ShowsCard(SnapshotDto snapshot, string cardNo)
    {
        bool Search(JsonElement element) => element.ValueKind switch {
            JsonValueKind.Object => element.EnumerateObject().Any(p => p.Name == "cardNo" && p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == cardNo || Search(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().Any(Search),
            _ => false };
        return Search(JsonSerializer.SerializeToElement(snapshot));
    }

    [Theory]
    [InlineData("SFD·137/221")]
    [InlineData("UNL-127/219")]
    public async Task RecoveryRejectsMovementFactsThatViolateThePrintedRoute(string card)
    {
        var moved = await MovementObserverAuditTests.Move(Position(card));
        var skill = Assert.Single(moved.State.StackItems); var context = skill.MovementObserver!;
        var unit = card == "SFD·137/221" ? context.Unit with { Origin = new("P2", "BASE") }
            : context.Unit with { Destination = new("P2", "BASE") };
        var forged = moved.State with { StackItems = [skill with { MovementObserver = context with { Unit = unit } }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("movement observer"));
    }

    [Theory]
    [InlineData("UNL-115/219", 4, 4, 1)]
    [InlineData("UNL-127/219", 3, 1, 2)]
    public async Task RealAcceleratedPlayThenStandardMoveEarnsExperience(string card, int manaCost, int power, int experience)
    {
        var s = Position(card);
        s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["PLAY"] = new("PLAY", cardNo: card, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Hand = [..s.PlayerZones["P1"].Hand, "PLAY"] } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20, 20) } };
        var played = await Act(s, "P1", new PlayCardCommand("PLAY", card, [], OptionalCosts: ["HASTE_READY"]));
        var entered = played;
        Assert.Empty(entered.State.StackItems);
        Assert.Contains("PLAY", entered.State.PlayerZones["P1"].Base);
        Assert.False(entered.State.CardObjects["PLAY"].IsExhausted);
        Assert.Equal(power, entered.State.CardObjects["PLAY"].Power);
        Assert.Equal(20 - manaCost, entered.State.RunePools["P1"].Mana);
        Assert.Equal(card == "UNL-115/219" ? 18 : 19, entered.State.RunePools["P1"].TotalPower);
        var moved = await Act(entered.State, "P1", new MoveUnitCommand("PLAY", "BASE", "BATTLEFIELD:BF2", []));
        Assert.Equal(0, moved.State.PlayerExperience.GetValueOrDefault("P1"));
        var done = await Top(moved.State); Assert.Equal(experience, done.State.PlayerExperience["P1"]);
        Assert.Equal("BF2", done.State.ObjectLocations["PLAY"].BattlefieldObjectId);
        Assert.True(done.State.CardObjects["PLAY"].IsExhausted); Restore(done.State);
    }

    [Theory]
    [InlineData("SFD·048/221")]
    [InlineData("SFD·137/221")]
    [InlineData("UNL-115/219")]
    [InlineData("UNL-127/219")]
    public async Task SeparateMovesInTheSameTurnTriggerIndependently(string card)
    {
        var s = Position(card);
        s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["AOE2"] = s.CardObjects["AOE"] with { ObjectId = "AOE2" } },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Hand = [..s.PlayerZones["P1"].Hand, "AOE2"] } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20, 20) } };
        var first = await Top((await MovementObserverAuditTests.Move(s)).State);
        var secondSpell = await Act(first.State, "P1", new PlayCardCommand("AOE2", Spell, ["D"], Destination: "BASE:P2"));
        var second = await Top(secondSpell.State);
        if (card != "UNL-127/219") second = await Top(second.State);
        else Assert.Empty(second.State.StackItems);
        Assert.Equal(s.TurnNumber, second.State.TurnNumber);
        if (card == "SFD·048/221") {
            Assert.Contains("DRAW-TOP", second.State.PlayerZones["P2"].Hand);
            Assert.Contains("DRAW-NEXT", second.State.PlayerZones["P2"].Hand);
            Assert.Equal(2, second.State.DrawLedger.Counts["P2"]);
        }
        else if (card == "SFD·137/221") Assert.Equal(6, second.State.CardObjects["D"].Power);
        else Assert.Equal(2, second.State.PlayerExperience["P2"]);
        Restore(second.State);
    }

}
