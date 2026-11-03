using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// Card play observations use the same admitted printing as the threshold skill.
public sealed class RadSingularityCardPlayDrawTests
{
    private const string Card = "RAD·069/167";
    private const string Effect = TriggerKinds.ControllerCardPlayDraw;

    [Fact]
    public async Task ACompletedUnitPlayEarnsOneDrawSkillThatWaitsForResponses()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card, out var behavior));
        Assert.Equal(2, behavior.ControllerCardPlayDrawCount);
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: "BASE"));
        var skill = Assert.Single(game.State.StackItems, i => i.EffectKind == Effect);
        Assert.Equal("PLAY", skill.OutsideHandPlayObserver!.Played.ObjectId);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(game.State.CardPlays.Resolved);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task ASpellDeclarationDoesNotEarnTheDrawUntilTheSpellResolves()
    {
        var game = new Driver(Put(Position(), "SPELL", "OGN·048/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == Effect);
        await game.Top();
        Assert.Single(game.State.StackItems, i => i.EffectKind == Effect);
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count); // Original unit plus the spell's one draw.
        await game.Top();
        Assert.Equal(4, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task AnotherPlayersCompletedPlayDoesNotUseYourEquipment()
    {
        var state = Put(Position(), "THEIR_PLAY", "OGN·219/298", "P2", "HAND") with {
            TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("THEIR_PLAY", "OGN·219/298", [], Destination: "BASE"));
        Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == Effect);
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task ACompletedEquipmentPlayAlsoEarnsItsOwnDrawSkill()
    {
        var game = new Driver(Put(Position(), "GEAR", "OGN·077/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("GEAR", "OGN·077/298", [], Destination: "BASE"));
        Assert.Single(game.State.StackItems, i => i.EffectKind == Effect);
        await game.Top();
        Assert.Equal(3, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task ARealChampionZonePlayNeedsNoHandOriginCondition()
    {
        var state = Source(VenUnitEntryBasicTests.Position("VEN·019"), "SOURCE");
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·019", [], Destination: "BASE"));
        Assert.True(Assert.Single(game.State.CardPlays.Resolved).OriginOutsideHands);
        Assert.Single(game.State.StackItems, i => i.EffectKind == Effect);
        await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task AnActuallyCounteredSpellDoesNotCompleteOrEarnADraw()
    {
        var game = new Driver(Put(Put(Position(), "SPELL", "OGN·048/298", "P1", "HAND"),
            "COUNTER", "OGN·045/298", "P2", "HAND"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        var original = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [original]));
        await game.Top();
        Assert.DoesNotContain(game.State.CardPlays.Resolved, i => i.Source.ObjectId == "SPELL");
        Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == Effect);
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task ARealReactionDuringTheOpponentsTurnStillEarnsTheControllersDraw()
    {
        var state = Put(Put(Position(), "OPEN", "OGN·048/298", "P2", "HAND"),
            "ANSWER", "OGN·104/298", "P1", "HAND") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("OPEN", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("ANSWER", "OGN·104/298", ["ALLY"]));
        await game.Top();
        Assert.Single(game.State.StackItems, i => i.EffectKind == Effect);
        await game.Top();
        Assert.Equal(4, game.State.PlayerZones["P1"].Hand.Count);
        await game.Top();
        Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == Effect);
        await game.Replay();
    }

    [Fact]
    public async Task ASourceOwnedByTheOpponentStillObservesItsActualControllersPlay()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SOURCE"] = state.CardObjects["SOURCE"] with { OwnerId = "P2" } } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: "BASE"));
        await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.PlayerZones["P2"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task AnIllegalPlayCannotChangeThePositionOrEarnADraw()
        => await Reject(Position(), "P1", new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: "BATTLEFIELD:missing"));

    [Fact]
    public async Task MultipleSourcesEarnIndependentSkillsWithoutObservingTheirOwnDraws()
    {
        var game = new Driver(Source(Position(), "SECOND"));
        await game.Act("P1", new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: "BASE"));
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(i => i.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count);
        await game.Top(); await game.Top();
        Assert.Equal(4, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("GRAVEYARD")] [InlineData("CONTROLLER")]
    public async Task DerivedSourceChangesPreserveTheAlreadyCapturedControllerAndDraw(string branch)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: "BASE"));
        var state = branch == "GRAVEYARD" ? RadUnitEffectTestFixture.Place(game.State, "SOURCE", "P1", "GRAVEYARD")
            : RadUnitEffectTestFixture.Place(game.State, "SOURCE", "P2", "BASE");
        OfficialInsightAndSpellLockTests.Restore(state);
        var result = await OfficialGraveyardRecastTests.Top(state);
        Assert.Equal(2, result.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        OfficialInsightAndSpellLockTests.Restore(result.State);
    }

    [Theory]
    [InlineData("played-generation")] [InlineData("future-tick")] [InlineData("opponent-turn")]
    public async Task RestoreRejectsForgedCompletedPlayWitnesses(string branch)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: "BASE"));
        var skill = Assert.Single(game.State.StackItems); var context = skill.OutsideHandPlayObserver!;
        var forged = branch switch {
            "played-generation" => context with { Played = context.Played with { Generation = context.Played.Generation + 1 } },
            "future-tick" => context with { CapturedTick = game.State.Tick + 1 },
            _ => context with { TurnPlayerId = "P2" }
        };
        Assert.Contains("invalid card play observer stack", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [skill with { OutsideHandPlayObserver = forged }] }));
        await game.Replay();
    }

    [Fact]
    public async Task TheImplementedObserverHasAnAdmittedOfficialPrinting()
        => Assert.Contains((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);

    private static MatchState Position() => Source(Put(Create(), "PLAY", "OGN·219/298", "P1", "HAND"), "SOURCE");
    private static MatchState Source(MatchState state, string id)
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: Card, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) } };
        return RadUnitEffectTestFixture.Place(state, id, "P1", "BASE");
    }
}
