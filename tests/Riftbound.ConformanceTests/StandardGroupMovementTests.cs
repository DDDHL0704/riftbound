using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandardGroupMovementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistedMovementWindowRequiresBattleOnlyWhenBothSidesHaveUnits(bool defended)
    {
        var state = Position();
        if (!defended)
        {
            var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
            zones["P2"] = zones["P2"] with { Battlefields = ["BF"], Base = ["D"] };
            var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
            locations["D"] = new("P2", "BASE");
            state = state with { PlayerZones = zones, ObjectLocations = locations };
        }
        var result = await Resolve(state, new("A", Destination: "BATTLEFIELD:BF", SourceObjectIds: ["A"]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.SpellDuelState.IsActive);
        Assert.Equal(defended, result.State.PendingTaskQueue.Tasks.Any(t => t.Kind == "START_BATTLE"));
        var views = result.Snapshots.ToDictionary(e => e.Key, e =>
            new RecoveredPlayerView(e.Key, result.State.Tick, 0, e.Value, result.State.Tick, 0, result.Prompts[e.Key]));
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views));

        // An actually defended contest must still reject a lost battle task.
        if (defended)
        {
            var corruptViews = views.ToDictionary(e => e.Key, e =>
            {
                var timing = e.Value.Snapshot.Timing!.ToDictionary(item => item.Key, item => item.Value);
                var tasks = JsonSerializer.SerializeToElement(timing["battlefieldTasks"]).EnumerateArray()
                    .Where(task => task.GetProperty("kind").GetString() != "START_BATTLE").Select(task => task.Clone()).ToArray();
                timing["battlefieldTasks"] = JsonSerializer.SerializeToElement(tasks);
                return e.Value with { Snapshot = e.Value.Snapshot with { Timing = timing } };
            });
            Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], corruptViews),
                error => error.Contains("START_BATTLE is required", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void MovementPromptSuppliesGroupCapabilityAndDestinationCost()
    {
        var original = Position();
        var cards = original.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["D"] = cards["D"] with { CardNo = "UNL-163/219" };
        var session = new MatchSession(original with { CardObjects = cards }, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var move = session.PromptFor("P1").Candidates!.Single(c => c.Action == CommandTypes.MoveUnit);
        Assert.True(move.Enabled);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(move.Metadata);
        Assert.True(Assert.IsType<bool>(metadata["supportsSimultaneousMovement"]));
        var costs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(metadata["powerPerExtraUnitByDestination"]);
        Assert.Equal(1, costs["BATTLEFIELD:BF"]);
    }

    [Fact]
    public async Task AllUnitsMoveAndExhaustBeforeTheSingleContestStarts()
    {
        var state = Position();
        var command = new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", SourceObjectIds: ["A", "B"]);
        var result = await Resolve(state, command);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(state.Tick + 1, result.State.Tick);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.All(new[] { "A", "B" }, id =>
        {
            Assert.True(result.State.CardObjects[id].IsExhausted);
            Assert.Equal("BF", result.State.ObjectLocations[id].BattlefieldObjectId);
        });
        Assert.Equal(2, result.Events.Count(e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
        Assert.Single(result.Events, e => e.Kind == "BATTLEFIELD_CONTESTED");
        var kinds = result.Events.Select(e => e.Kind).ToList();
        Assert.True(kinds.LastIndexOf("UNIT_MOVED_TO_BATTLEFIELD") < kinds.IndexOf("SPELL_DUEL_STARTED"));
        Assert.Equal("P1", result.State.FocusPlayerId);
        var recovered = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(recovered));
    }

    [Theory]
    [InlineData(true, "P1", false)]
    [InlineData(false, "P2", false)]
    [InlineData(false, "P1", true)]
    public async Task OneIllegalSourceRejectsEntireAction(bool exhausted, string controller, bool duplicate)
    {
        var original = Position();
        var cards = original.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["B"] = cards["B"] with { IsExhausted = exhausted, ControllerId = controller };
        var state = original with { CardObjects = cards };
        var result = await Resolve(state, new("A", "BASE", "BATTLEFIELD:BF", SourceObjectIds: duplicate ? ["A", "A"] : ["A", "B"]));
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task PatrolChargesGenericPowerOncePerExtraUnit(int power, bool allowed)
    {
        var original = Position();
        var cards = original.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["D"] = cards["D"] with { CardNo = "UNL-163/219", Power = 4 };
        var state = original with { CardObjects = cards, RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, power), ["P2"] = RunePool.Empty } };
        var result = await Resolve(state, new("A", "BASE", "BATTLEFIELD:BF", SourceObjectIds: ["A", "B"]));
        Assert.Equal(allowed, result.Accepted);
        if (allowed)
        {
            Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
            Assert.Single(result.Events, e => e.Kind == "COST_PAID");
        }
        else Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task DifferentOriginsMoveTogetherUsingEachUnitsPermissions()
    {
        var original = Position();
        var cards = original.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["B"] = cards["B"] with { Tags = [CardObjectTags.UnitCard, "游走"] };
        cards["HOME"] = cards["BF"] with { ObjectId = "HOME", OwnerId = "P1", ControllerId = "P1" };
        var locations = original.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        locations["B"] = new("P1", "BATTLEFIELD", "HOME");
        locations["HOME"] = new("P1", "BATTLEFIELD", "HOME");
        var zones = original.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones["P1"] = PlayerZones.Empty with { Base = ["A"], Battlefields = ["HOME", "B"] };
        var state = original with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
        var result = await Resolve(state, new("A", Destination: "BATTLEFIELD:BF", SourceObjectIds: ["A", "B"]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.All(new[] { "A", "B" }, id => Assert.Equal("BF", result.State.ObjectLocations[id].BattlefieldObjectId));
        Assert.Single(result.Events, e => e.Kind == "SPELL_DUEL_STARTED");
    }

    private static ValueTask<ResolutionResult> Resolve(MatchState state, MoveUnitCommand command) =>
        new CoreRuleEngine().ResolveAsync(state, new PlayerIntent("move-group", "P1", CommandTypes.MoveUnit), command, default);

    private static MatchState Position() => new("GROUP-MOVE", 0, 3, "P1",
        new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
        status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
        runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
        playerZones: new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Base = ["A", "B"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = ["BF", "D"] }
        },
        cardObjects: new Dictionary<string, CardObjectState>
        {
            ["A"] = new("A", power: 2, cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["B"] = new("B", power: 2, cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["D"] = new("D", power: 2, cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2"),
            ["BF"] = new("BF", cardNo: "OGN·275/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2")
        },
        objectLocations: new Dictionary<string, ObjectLocationState>
        {
            ["A"] = new("P1", "BASE"), ["B"] = new("P1", "BASE"),
            ["D"] = new("P2", "BATTLEFIELD", "BF"), ["BF"] = new("P2", "BATTLEFIELD", "BF")
        });
}
