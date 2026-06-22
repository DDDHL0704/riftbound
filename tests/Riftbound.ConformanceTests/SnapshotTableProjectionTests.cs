using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SnapshotTableProjectionTests
{
    [Fact]
    public void SnapshotForViewerProjectsTabletopAuthorityFields()
    {
        var snapshot = ResolutionResult.BuildSnapshots(TableProjectionState())["P1"];

        Assert.NotNull(snapshot.Table);
        var table = snapshot.Table!;
        Assert.Equal("server-snapshot", table.Source);
        Assert.Equal("P1", table.ViewerPlayerId);
        Assert.Equal(OfficialDeckValidator.RuneDeckCount, table.RuneDeckSize);
        Assert.Equal(2, table.Players.Count);

        var tableP1 = table.Players.Single(player => player.PlayerId == "P1");
        Assert.Equal("self", tableP1.Perspective);
        Assert.True(tableP1.IsViewer);
        Assert.Equal(["P1-HAND"], tableP1.Zones.Hand);
        Assert.Equal(0, tableP1.Zones.HandHidden);
        Assert.Equal(["P1-BASE-UNIT"], tableP1.Zones.BaseCards);
        Assert.Equal(["P1-RUNE-1"], tableP1.Zones.BaseRunes);

        var tableP2 = table.Players.Single(player => player.PlayerId == "P2");
        Assert.Equal("opponent", tableP2.Perspective);
        Assert.False(tableP2.IsViewer);
        Assert.Empty(tableP2.Zones.Hand);
        Assert.Equal(1, tableP2.Zones.HandHidden);
        Assert.Equal(["P2-BASE-UNIT"], tableP2.Zones.BaseCards);
        Assert.Equal(["P2-RUNE-1"], tableP2.Zones.BaseRunes);

        var tableLeftBattlefield = table.Battlefields.Single(field => field.BattlefieldObjectId == "BF-LEFT");
        Assert.Equal(0, tableLeftBattlefield.Index);
        Assert.Equal(["P1-LEFT-UNIT", "P2-LEFT-UNIT"], tableLeftBattlefield.OccupantObjectIds);
        Assert.Equal(["P1-LEFT-UNIT"], tableLeftBattlefield.UnitsBySide["P1"]);
        Assert.Equal(["P2-LEFT-UNIT"], tableLeftBattlefield.UnitsBySide["P2"]);
        Assert.Equal(["P1-STANDBY"], tableLeftBattlefield.StandbyObjectIds);
        Assert.Equal(2, tableLeftBattlefield.StandbySlotCount);
        Assert.Equal(1, tableLeftBattlefield.HiddenStandbyCount);
        Assert.Equal(2, tableLeftBattlefield.StandbySlots.Count);
        Assert.Equal("VISIBLE", tableLeftBattlefield.StandbySlots[0].State);
        Assert.Equal("P1-STANDBY", tableLeftBattlefield.StandbySlots[0].ObjectId);
        Assert.Equal("P1", tableLeftBattlefield.StandbySlots[0].SidePlayerId);
        Assert.Equal("HIDDEN", tableLeftBattlefield.StandbySlots[1].State);
        Assert.Null(tableLeftBattlefield.StandbySlots[1].ObjectId);
        Assert.Equal("P2", tableLeftBattlefield.StandbySlots[1].SidePlayerId);

        var p1Zones = Zones(snapshot, "P1");
        Assert.Equal(["P1-HAND"], StringList(p1Zones["hand"]));
        Assert.Equal(0, IntValue(p1Zones["handHidden"]));
        Assert.Equal(["P1-BASE-UNIT"], StringList(p1Zones["baseCards"]));
        Assert.Equal(["P1-RUNE-1"], StringList(p1Zones["baseRunes"]));

        var p2Zones = Zones(snapshot, "P2");
        Assert.Empty(StringList(p2Zones["hand"]));
        Assert.Equal(1, IntValue(p2Zones["handHidden"]));
        Assert.Equal(["P2-BASE-UNIT"], StringList(p2Zones["baseCards"]));
        Assert.Equal(["P2-RUNE-1"], StringList(p2Zones["baseRunes"]));

        var leftBattlefield = Battlefield(snapshot, "BF-LEFT");
        Assert.Equal(["P1-LEFT-UNIT", "P2-LEFT-UNIT"], StringList(leftBattlefield["occupantObjectIds"]));
        Assert.Equal(["P1-LEFT-UNIT"], StringListMap(leftBattlefield["unitsBySide"])["P1"]);
        Assert.Equal(["P2-LEFT-UNIT"], StringListMap(leftBattlefield["unitsBySide"])["P2"]);
        Assert.Equal(["P1-STANDBY"], StringList(leftBattlefield["standbyObjectIds"]));
        Assert.Equal(2, IntValue(leftBattlefield["standbySlotCount"]));
        Assert.Equal(1, IntValue(leftBattlefield["hiddenStandbyCount"]));

        var standbySlots = ObjectList(leftBattlefield["standbySlots"]);
        Assert.Equal(2, standbySlots.Count);
        Assert.Equal("VISIBLE", StringValue(standbySlots[0]["state"]));
        Assert.Equal("P1-STANDBY", StringValue(standbySlots[0]["objectId"]));
        Assert.Equal("P1", StringValue(standbySlots[0]["sidePlayerId"]));
        Assert.Equal("HIDDEN", StringValue(standbySlots[1]["state"]));
        Assert.False(standbySlots[1].ContainsKey("objectId"));
        Assert.Equal("P2", StringValue(standbySlots[1]["sidePlayerId"]));

        var p2Objects = Objects(snapshot, "P2");
        Assert.DoesNotContain("P2-HAND", p2Objects.Keys);
        Assert.DoesNotContain("P2-HIDDEN-STANDBY", p2Objects.Keys);
    }

    private static MatchState TableProjectionState()
    {
        return new MatchState(
            "snapshot-table-projection",
            7,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "bottom",
                ["P2"] = "top"
            },
            status: MatchStatuses.InProgress,
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    MainDeck: ["P1-DECK"],
                    RuneDeck: ["P1-RUNE-DECK"],
                    Hand: ["P1-HAND"],
                    Base: ["P1-BASE-UNIT", "P1-RUNE-1"],
                    Battlefields: ["BF-LEFT", "P1-LEFT-UNIT", "P1-STANDBY"],
                    Graveyard: [],
                    Banished: [],
                    LegendZone: ["P1-LEGEND"],
                    ChampionZone: ["P1-HERO"]),
                ["P2"] = new(
                    MainDeck: ["P2-DECK"],
                    RuneDeck: ["P2-RUNE-DECK"],
                    Hand: ["P2-HAND"],
                    Base: ["P2-BASE-UNIT", "P2-RUNE-1"],
                    Battlefields: ["BF-RIGHT", "P2-LEFT-UNIT", "P2-HIDDEN-STANDBY"],
                    Graveyard: [],
                    Banished: [],
                    LegendZone: ["P2-LEGEND"],
                    ChampionZone: ["P2-HERO"])
            },
            cardObjects: CardObjects(),
            objectLocations: ObjectLocations());
    }

    private static IReadOnlyDictionary<string, CardObjectState> CardObjects()
    {
        return new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-HAND"] = Unit("P1-HAND", "P1"),
            ["P2-HAND"] = Unit("P2-HAND", "P2"),
            ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT", "P1"),
            ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", "P2"),
            ["P1-RUNE-1"] = Rune("P1-RUNE-1", "P1"),
            ["P2-RUNE-1"] = Rune("P2-RUNE-1", "P2"),
            ["P1-LEGEND"] = new("P1-LEGEND", cardNo: "UNL-181/219", ownerId: "P1", controllerId: "P1"),
            ["P2-LEGEND"] = new("P2-LEGEND", cardNo: "UNL-187/219", ownerId: "P2", controllerId: "P2"),
            ["P1-HERO"] = Unit("P1-HERO", "P1", "UNL-022/219"),
            ["P2-HERO"] = Unit("P2-HERO", "P2", "UNL-030/219"),
            ["BF-LEFT"] = Battlefield("BF-LEFT", "P1", "UNL-205/219"),
            ["BF-RIGHT"] = Battlefield("BF-RIGHT", "P2", "UNL-206/219"),
            ["P1-LEFT-UNIT"] = Unit("P1-LEFT-UNIT", "P1"),
            ["P2-LEFT-UNIT"] = Unit("P2-LEFT-UNIT", "P2"),
            ["P1-STANDBY"] = Standby("P1-STANDBY", "P1", isFaceDown: true),
            ["P2-HIDDEN-STANDBY"] = Standby("P2-HIDDEN-STANDBY", "P2", isFaceDown: true)
        };
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> ObjectLocations()
    {
        return new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-HAND"] = new("P1", "HAND"),
            ["P2-HAND"] = new("P2", "HAND"),
            ["P1-BASE-UNIT"] = new("P1", "BASE"),
            ["P2-BASE-UNIT"] = new("P2", "BASE"),
            ["P1-RUNE-1"] = new("P1", "BASE"),
            ["P2-RUNE-1"] = new("P2", "BASE"),
            ["P1-LEGEND"] = new("P1", "LEGEND"),
            ["P2-LEGEND"] = new("P2", "LEGEND"),
            ["P1-HERO"] = new("P1", "CHAMPION"),
            ["P2-HERO"] = new("P2", "CHAMPION"),
            ["BF-LEFT"] = new("P1", "BATTLEFIELD"),
            ["BF-RIGHT"] = new("P2", "BATTLEFIELD"),
            ["P1-LEFT-UNIT"] = new("P1", "BATTLEFIELD", "BF-LEFT"),
            ["P2-LEFT-UNIT"] = new("P2", "BATTLEFIELD", "BF-LEFT"),
            ["P1-STANDBY"] = new("P1", "BATTLEFIELD", "BF-LEFT"),
            ["P2-HIDDEN-STANDBY"] = new("P2", "BATTLEFIELD", "BF-LEFT")
        };
    }

    private static CardObjectState Battlefield(string objectId, string ownerId, string cardNo)
    {
        return new(
            objectId,
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            cardNo: cardNo,
            ownerId: ownerId,
            controllerId: ownerId);
    }

    private static CardObjectState Rune(string objectId, string ownerId)
    {
        return new(
            objectId,
            tags: [CardObjectTags.RuneCard],
            cardNo: "UNL-R01",
            ownerId: ownerId,
            controllerId: ownerId);
    }

    private static CardObjectState Standby(string objectId, string ownerId, bool isFaceDown)
    {
        return new(
            objectId,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.Standby],
            cardNo: "UNL-011/219",
            ownerId: ownerId,
            controllerId: ownerId);
    }

    private static CardObjectState Unit(string objectId, string ownerId, string cardNo = "UNL-001/219")
    {
        return new(
            objectId,
            power: 3,
            tags: [CardObjectTags.UnitCard],
            cardNo: cardNo,
            ownerId: ownerId,
            controllerId: ownerId);
    }

    private static IReadOnlyDictionary<string, object?> Zones(SnapshotDto snapshot, string playerId)
    {
        return Dict(Player(snapshot, playerId)["zones"]);
    }

    private static IReadOnlyDictionary<string, object?> Objects(SnapshotDto snapshot, string playerId)
    {
        return Dict(Player(snapshot, playerId)["objects"]);
    }

    private static IReadOnlyDictionary<string, object?> Player(SnapshotDto snapshot, string playerId)
    {
        return Dict(snapshot.Players[playerId]);
    }

    private static IReadOnlyDictionary<string, object?> Battlefield(SnapshotDto snapshot, string battlefieldObjectId)
    {
        return ObjectList(Dict(snapshot.Lanes)["battlefields"])
            .Single(entry => string.Equals(StringValue(entry["battlefieldObjectId"]), battlefieldObjectId, StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, object?> Dict(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(value);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ObjectList(object? value)
    {
        return Assert.IsAssignableFrom<IEnumerable<object?>>(value)
            .Select(Dict)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> StringListMap(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<string>>>(value);
    }

    private static IReadOnlyList<string> StringList(object? value)
    {
        return Assert.IsAssignableFrom<IEnumerable<string>>(value).ToArray();
    }

    private static int IntValue(object? value)
    {
        return Assert.IsType<int>(value);
    }

    private static string StringValue(object? value)
    {
        return Assert.IsType<string>(value);
    }
}
