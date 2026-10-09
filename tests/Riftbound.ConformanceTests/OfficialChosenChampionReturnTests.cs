using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialChosenChampionReturnTests
{
    internal static MatchState Position()
    {
        var s = OfficialHoldSequenceTests.State("OGN·281/298");
        var cards = s.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var locations = s.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        foreach (var (id, no) in new[] { ("HERO", "OGN·039/298"), ("ALT", "OGN·039a/298"), ("OTHER-HERO", "OGN·112/298"), ("SECOND", "OGN·039/298") })
        {
            cards[id] = new(id, cardNo: no, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard, "CARD_CATEGORY:英雄单位"], power: 4);
            locations[id] = new("P1", "GRAVEYARD");
        }
        return s with { CardObjects = cards, ObjectLocations = locations,
            PlayerDecklists = new Dictionary<string, OfficialDecklist> { ["P1"] = new("OGN·247/298", "OGN·039/298", ["OGN·039/298"], [], []) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Graveyard = ["HERO", "ALT", "OTHER-HERO", "SECOND"] } } };
    }

    internal static MatchState ToChampionZone(MatchState s, string id)
        => s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with {
            Graveyard = s.PlayerZones["P1"].Graveyard.Where(x => x != id).ToArray(), ChampionZone = [id] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { [id] = new("P1", "CHAMPION") } };

    [Fact]
    public async Task EmptyHeroZoneConditionIsCheckedWhenHoldHappens()
    {
        var opened = await Act(ToChampionZone(Position(), "HERO"), "P1", new PassPriorityCommand());
        Assert.DoesNotContain(opened.Events, e => e.Kind == "TRIGGER_QUEUED" && e.Payload.GetValueOrDefault("effectKind") as string == "HOLD_RETURN_HERO");
        Assert.Empty(opened.State.StackItems); Assert.Null(opened.State.PendingCardChoice); Restore(opened.State);
    }

    [Theory]
    [InlineData("HERO")]
    [InlineData("SECOND")]
    [InlineData("ALT")]
    public async Task SameNamedHeroesAndAlternateArtAreChosenBeforeResponses(string target)
    {
        var opened = await Act(Position(), "P1", new PassPriorityCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(["ALT", "HERO", "SECOND"], choice.LegalObjectIds); Restore(opened.State);
        var confirmed = await OfficialHeldConfirmationTests.Choose(opened.State, target);
        Assert.Empty(confirmed.State.PlayerZones["P1"].ChampionZone); Assert.NotNull(confirmed.State.PriorityPlayerId);
        Assert.Equal([target], confirmed.State.StackItems.Single().TargetObjectIds); Restore(confirmed.State);
        var done = await Top(confirmed.State);
        Assert.Equal([target], done.State.PlayerZones["P1"].ChampionZone);
        Assert.DoesNotContain(target, done.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("CHAMPION", done.State.ObjectLocations[target].Zone);
        Assert.Equal(confirmed.State.CardObjects[target].ObjectGeneration + 1, done.State.CardObjects[target].ObjectGeneration);
        Restore(done.State);
    }
    [Fact]
    public async Task DeclineDoesNotOpenResponseOrMoveAnyHero()
    {
        var opened = await Act(Position(), "P1", new PassPriorityCommand());
        var done = await OfficialHeldConfirmationTests.Choose(opened.State);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.Empty(done.State.PlayerZones["P1"].ChampionZone); Assert.Equal(4, done.State.PlayerZones["P1"].Graveyard.Count); Restore(done.State);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("wrong-name")]
    [InlineData("opponent-graveyard")]
    public async Task NoLegalHeroSkipsConfirmationAndDoesNotStallTurn(string kind)
    {
        var s = Position(); var zones = s.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        var moved = zones["P1"].Graveyard.Where(id => kind != "wrong-name" || id != "OTHER-HERO").ToArray();
        zones["P1"] = zones["P1"] with { Graveyard = zones["P1"].Graveyard.Except(moved).ToArray(), Banished = kind == "opponent-graveyard" ? [] : moved };
        if (kind == "opponent-graveyard") zones["P2"] = zones["P2"] with { Graveyard = moved };
        var positions = s.ObjectLocations.ToDictionary(e => e.Key, e => moved.Contains(e.Key)
            ? new ObjectLocationState(kind == "opponent-graveyard" ? "P2" : "P1", kind == "opponent-graveyard" ? "GRAVEYARD" : "BANISHED") : e.Value);
        s = s with { PlayerZones = zones, ObjectLocations = positions };
        var done = await Act(s, "P1", new PassPriorityCommand());
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.Equal(MatchPhases.Main, done.State.Phase); Restore(done.State);
    }

    [Theory]
    [InlineData("new-object")]
    [InlineData("left-graveyard")]
    [InlineData("occupied-zone")]
    [InlineData("changed-name")]
    public async Task ResponseInvalidationCannotReturnOrRetargetHero(string change)
    {
        var opened = await Act(Position(), "P1", new PassPriorityCommand());
        var paid = await OfficialHeldConfirmationTests.Choose(opened.State, "HERO"); var s = paid.State;
        if (change == "new-object") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HERO"] = s.CardObjects["HERO"] with { ObjectGeneration = s.CardObjects["HERO"].ObjectGeneration + 2 } } };
        if (change == "changed-name") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HERO"] = s.CardObjects["HERO"] with { CardNo = "OGN·112/298" } } };
        if (change == "occupied-zone") s = ToChampionZone(s, "SECOND");
        if (change == "left-graveyard") s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with {
            Graveyard = s.PlayerZones["P1"].Graveyard.Where(id => id != "HERO").ToArray(), Hand = ["HERO"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["HERO"] = new("P1", "HAND") } };
        Restore(s); var done = await Top(s);
        Assert.DoesNotContain(done.Events, e => e.Kind == "HERO_RETURNED_TO_CHAMPION_ZONE");
        Assert.Equal(change == "occupied-zone" ? new[] { "SECOND" } : [], done.State.PlayerZones["P1"].ChampionZone);
        Assert.Contains("ALT", done.State.PlayerZones["P1"].Graveyard); Restore(done.State);
    }

    [Fact]
    public async Task BattlefieldControlChangeDoesNotChangeCapturedPlayer()
    {
        var opened = await Act(Position(), "P1", new PassPriorityCommand());
        var paid = await OfficialHeldConfirmationTests.Choose(opened.State, "HERO");
        var s = paid.State with { CardObjects = new Dictionary<string, CardObjectState>(paid.State.CardObjects) {
            ["F"] = paid.State.CardObjects["F"] with { ControllerId = "P2" } } };
        var done = await Top(s); Assert.Equal(["HERO"], done.State.PlayerZones["P1"].ChampionZone);
        Assert.Empty(done.State.PlayerZones["P2"].ChampionZone); Restore(done.State);
    }

    [Fact]
    public async Task AmplifiedHoldConfirmsBothTargetsBeforeEitherCanReturn()
    {
        var s = OfficialHoldSequenceTests.AddUnit(Position(), "BLUE", "UNL-087/219", "F", "P1");
        var opened = await Act(s, "P1", new PassPriorityCommand()); Assert.Equal(2, opened.State.TriggerQueue.Count);
        var ordered = await Act(opened.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: opened.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var first = await OfficialHeldConfirmationTests.Choose(ordered.State, "HERO");
        Assert.Null(first.State.PriorityPlayerId); Assert.Empty(first.State.PlayerZones["P1"].ChampionZone);
        var second = await OfficialHeldConfirmationTests.Choose(first.State, "ALT"); Restore(second.State);
        var expected = second.State.StackItems.Last().TargetObjectIds.Single();
        var one = await Top(second.State); Assert.Equal([expected], one.State.PlayerZones["P1"].ChampionZone);
        var two = await Top(one.State); Assert.Equal([expected], two.State.PlayerZones["P1"].ChampionZone);
        Assert.DoesNotContain(two.Events, e => e.Kind == "HERO_RETURNED_TO_CHAMPION_ZONE"); Restore(two.State);
    }

    [Theory]
    [InlineData("OGN·202/298")]
    [InlineData("OGN·202a/298")]
    [InlineData("ARC-005/006")]
    public async Task ChosenNameIncludesReprintsButNotAnotherSubtitle(string no)
    {
        var s = Position();
        s = s with { PlayerDecklists = new Dictionary<string, OfficialDecklist>(s.PlayerDecklists) { ["P1"] = s.PlayerDecklists["P1"] with { ChampionCardNo = "OGN·202/298" } },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["HERO"] = s.CardObjects["HERO"] with { CardNo = no }, ["SECOND"] = s.CardObjects["SECOND"] with { CardNo = "OGN·030/298" } } };
        var opened = await Act(s, "P1", new PassPriorityCommand()); Assert.Equal(["HERO"], opened.State.PendingCardChoice!.LegalObjectIds);
        var done = await Top((await OfficialHeldConfirmationTests.Choose(opened.State, "HERO")).State);
        Assert.Equal(["HERO"], done.State.PlayerZones["P1"].ChampionZone); Restore(done.State);
    }

    [Fact]
    public async Task ReturnedHeroUsesOrdinaryChampionPlayAndPayment()
    {
        var s = Position(); var cards = s.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var positions = s.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        foreach (var id in new[] { "R4", "R5" }) {
            cards[id] = cards["R1"] with { ObjectId = id }; positions[id] = new("P1", "BASE");
        }
        s = s with { CardObjects = cards, ObjectLocations = positions,
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Base = ["R4", "R5"] } } };
        var opened = await Act(s, "P1", new PassPriorityCommand());
        var done = await Top((await OfficialHeldConfirmationTests.Choose(opened.State, "HERO")).State);
        foreach (var id in new[] { "R1", "R2", "R4", "R5" }) done = await Act(done.State, "P1", new TapRuneCommand(id));
        var played = await Act(done.State, "P1", new PlayCardCommand("HERO", "OGN·039/298", [], Destination: "BASE"));
        Assert.Empty(played.State.PlayerZones["P1"].ChampionZone); Assert.Contains("HERO", played.State.PlayerZones["P1"].Base);
        Assert.True(played.State.CardObjects["HERO"].IsExhausted); Assert.Equal(0, played.State.RunePools["P1"].Mana); Restore(played.State);
    }

    [Fact]
    public async Task RecoveryRejectsMissingBindingsAndObsoleteHeldChoice()
    {
        var opened = await Act(Position(), "P1", new PassPriorityCommand());
        var paid = await OfficialHeldConfirmationTests.Choose(opened.State, "HERO");
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(paid.State with { StackItems = [paid.State.StackItems.Single() with { TargetGenerations = null }] }),
            e => e.Contains("held target confirmation"));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened.State with { PendingCardChoice = opened.State.PendingCardChoice! with { ChoiceWindow = "HOLD_EFFECT" } }),
            e => e.Contains("obsolete held resolution choice"));
    }

}
