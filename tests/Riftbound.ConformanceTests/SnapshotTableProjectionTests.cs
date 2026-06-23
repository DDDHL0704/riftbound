using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SnapshotTableProjectionTests
{
    [Fact]
    public void SnapshotForViewerProjectsTabletopAuthorityFields()
    {
        var snapshots = ResolutionResult.BuildSnapshots(TableProjectionState());
        var snapshot = snapshots["P1"];

        Assert.NotNull(snapshot.Table);
        var table = snapshot.Table!;
        Assert.Equal("server-snapshot", table.Source);
        Assert.Equal("P1", table.ViewerPlayerId);
        Assert.Equal(OfficialDeckValidator.RuneDeckCount, table.RuneDeckSize);
        Assert.Equal(2, table.Players.Count);

        var tableP1 = table.Players.Single(player => player.PlayerId == "P1");
        Assert.Equal("self", tableP1.Perspective);
        Assert.True(tableP1.IsViewer);
        Assert.Equal(1, tableP1.Zones.MainDeckCount);
        Assert.Equal(1, tableP1.Zones.RuneDeckCount);
        Assert.Equal(["P1-HAND"], tableP1.Zones.Hand);
        Assert.Equal(0, tableP1.Zones.HandHidden);
        Assert.Equal(["P1-BASE-UNIT"], tableP1.Zones.BaseCards);
        Assert.Equal(["P1-RUNE-1"], tableP1.Zones.BaseRunes);
        Assert.Equal(["P1-GRAVEYARD"], tableP1.Zones.Graveyard);
        Assert.Equal(["P1-BANISHED"], tableP1.Zones.Banished);
        Assert.Equal(["P1-LEGEND"], tableP1.Zones.LegendZone);
        Assert.Equal(["P1-HERO"], tableP1.Zones.ChampionZone);

        var tableP2 = table.Players.Single(player => player.PlayerId == "P2");
        Assert.Equal("opponent", tableP2.Perspective);
        Assert.False(tableP2.IsViewer);
        Assert.Equal(1, tableP2.Zones.MainDeckCount);
        Assert.Equal(1, tableP2.Zones.RuneDeckCount);
        Assert.Empty(tableP2.Zones.Hand);
        Assert.Equal(1, tableP2.Zones.HandHidden);
        Assert.Equal(["P2-BASE-UNIT"], tableP2.Zones.BaseCards);
        Assert.Equal(["P2-RUNE-1"], tableP2.Zones.BaseRunes);
        Assert.Equal(["P2-GRAVEYARD"], tableP2.Zones.Graveyard);
        Assert.Equal(["P2-BANISHED"], tableP2.Zones.Banished);
        Assert.Equal(["P2-LEGEND"], tableP2.Zones.LegendZone);
        Assert.Equal(["P2-HERO"], tableP2.Zones.ChampionZone);

        Assert.Equal(["BF-LEFT", "BF-RIGHT"], table.Battlefields.Select(field => field.BattlefieldObjectId).ToArray());
        var tableLeftBattlefield = table.Battlefields.Single(field => field.BattlefieldObjectId == "BF-LEFT");
        Assert.Equal(0, tableLeftBattlefield.Index);
        Assert.Equal("P1", tableLeftBattlefield.ZonePlayerId);
        Assert.Equal("UNL-205/219", tableLeftBattlefield.CardNo);
        Assert.Equal("P1", tableLeftBattlefield.ControllerId);
        Assert.Equal("CONTESTED", tableLeftBattlefield.Status);
        Assert.True(tableLeftBattlefield.Contested);
        Assert.Equal(["P1-LEFT-UNIT", "P2-LEFT-UNIT"], tableLeftBattlefield.OccupantObjectIds);
        Assert.Equal(["P1", "P2"], tableLeftBattlefield.OccupantControllerIds);
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

        var tableRightBattlefield = table.Battlefields.Single(field => field.BattlefieldObjectId == "BF-RIGHT");
        Assert.Equal(1, tableRightBattlefield.Index);
        Assert.Equal("P2", tableRightBattlefield.ZonePlayerId);
        Assert.Equal("UNL-206/219", tableRightBattlefield.CardNo);
        Assert.Equal("P2", tableRightBattlefield.ControllerId);
        Assert.Equal("CONTESTED", tableRightBattlefield.Status);
        Assert.True(tableRightBattlefield.Contested);
        Assert.Equal(["P1-RIGHT-UNIT", "P2-RIGHT-UNIT"], tableRightBattlefield.OccupantObjectIds);
        Assert.Equal(["P1", "P2"], tableRightBattlefield.OccupantControllerIds);
        Assert.Equal(["P1-RIGHT-UNIT"], tableRightBattlefield.UnitsBySide["P1"]);
        Assert.Equal(["P2-RIGHT-UNIT"], tableRightBattlefield.UnitsBySide["P2"]);
        Assert.Equal(["P2-RIGHT-STANDBY"], tableRightBattlefield.StandbyObjectIds);
        Assert.Equal(1, tableRightBattlefield.StandbySlotCount);
        Assert.Equal(0, tableRightBattlefield.HiddenStandbyCount);
        Assert.Single(tableRightBattlefield.StandbySlots);
        Assert.Equal("VISIBLE", tableRightBattlefield.StandbySlots[0].State);
        Assert.Equal("P2-RIGHT-STANDBY", tableRightBattlefield.StandbySlots[0].ObjectId);
        Assert.Equal("P2", tableRightBattlefield.StandbySlots[0].SidePlayerId);

        var p1Zones = Zones(snapshot, "P1");
        Assert.Equal(1, IntValue(p1Zones["mainDeckCount"]));
        Assert.Equal(1, IntValue(p1Zones["runeDeckCount"]));
        Assert.Equal(["P1-HAND"], StringList(p1Zones["hand"]));
        Assert.Equal(0, IntValue(p1Zones["handHidden"]));
        Assert.Equal(["P1-BASE-UNIT"], StringList(p1Zones["baseCards"]));
        Assert.Equal(["P1-RUNE-1"], StringList(p1Zones["baseRunes"]));
        Assert.Equal(["P1-GRAVEYARD"], StringList(p1Zones["graveyard"]));
        Assert.Equal(["P1-BANISHED"], StringList(p1Zones["banished"]));
        Assert.Equal(["P1-LEGEND"], StringList(p1Zones["legendZone"]));
        Assert.Equal(["P1-HERO"], StringList(p1Zones["championZone"]));
        var p1Objects = Objects(snapshot, "P1");
        Assert.Contains("P1-GRAVEYARD", p1Objects.Keys);
        Assert.Contains("P1-BANISHED", p1Objects.Keys);
        Assert.Contains("P1-LEGEND", p1Objects.Keys);
        Assert.Contains("P1-HERO", p1Objects.Keys);
        var p1Standby = Dict(p1Objects["P1-STANDBY"]);
        Assert.Equal("P1-STANDBY", StringValue(p1Standby["objectId"]));
        Assert.Equal("UNL-011/219", StringValue(p1Standby["cardNo"]));

        var p2Zones = Zones(snapshot, "P2");
        Assert.Equal(1, IntValue(p2Zones["mainDeckCount"]));
        Assert.Equal(1, IntValue(p2Zones["runeDeckCount"]));
        Assert.Empty(StringList(p2Zones["hand"]));
        Assert.Equal(1, IntValue(p2Zones["handHidden"]));
        Assert.Equal(["P2-BASE-UNIT"], StringList(p2Zones["baseCards"]));
        Assert.Equal(["P2-RUNE-1"], StringList(p2Zones["baseRunes"]));
        Assert.Equal(["P2-GRAVEYARD"], StringList(p2Zones["graveyard"]));
        Assert.Equal(["P2-BANISHED"], StringList(p2Zones["banished"]));
        Assert.Equal(["P2-LEGEND"], StringList(p2Zones["legendZone"]));
        Assert.Equal(["P2-HERO"], StringList(p2Zones["championZone"]));

        var leftBattlefield = Battlefield(snapshot, "BF-LEFT");
        Assert.Equal("P1", StringValue(leftBattlefield["zonePlayerId"]));
        Assert.Equal("UNL-205/219", StringValue(leftBattlefield["cardNo"]));
        Assert.Equal("P1", StringValue(leftBattlefield["controllerId"]));
        Assert.Equal("CONTESTED", StringValue(leftBattlefield["status"]));
        Assert.True(BoolValue(leftBattlefield["contested"]));
        Assert.Equal(["P1-LEFT-UNIT", "P2-LEFT-UNIT"], StringList(leftBattlefield["occupantObjectIds"]));
        Assert.Equal(["P1", "P2"], StringList(leftBattlefield["occupantControllerIds"]));
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

        var rightBattlefield = Battlefield(snapshot, "BF-RIGHT");
        Assert.Equal("P2", StringValue(rightBattlefield["zonePlayerId"]));
        Assert.Equal("UNL-206/219", StringValue(rightBattlefield["cardNo"]));
        Assert.Equal("P2", StringValue(rightBattlefield["controllerId"]));
        Assert.Equal("CONTESTED", StringValue(rightBattlefield["status"]));
        Assert.True(BoolValue(rightBattlefield["contested"]));
        Assert.Equal(["P1-RIGHT-UNIT", "P2-RIGHT-UNIT"], StringList(rightBattlefield["occupantObjectIds"]));
        Assert.Equal(["P1", "P2"], StringList(rightBattlefield["occupantControllerIds"]));
        Assert.Equal(["P1-RIGHT-UNIT"], StringListMap(rightBattlefield["unitsBySide"])["P1"]);
        Assert.Equal(["P2-RIGHT-UNIT"], StringListMap(rightBattlefield["unitsBySide"])["P2"]);
        Assert.Equal(["P2-RIGHT-STANDBY"], StringList(rightBattlefield["standbyObjectIds"]));
        Assert.Equal(1, IntValue(rightBattlefield["standbySlotCount"]));
        Assert.Equal(0, IntValue(rightBattlefield["hiddenStandbyCount"]));
        var rightStandbySlots = ObjectList(rightBattlefield["standbySlots"]);
        Assert.Single(rightStandbySlots);
        Assert.Equal("VISIBLE", StringValue(rightStandbySlots[0]["state"]));
        Assert.Equal("P2-RIGHT-STANDBY", StringValue(rightStandbySlots[0]["objectId"]));
        Assert.Equal("P2", StringValue(rightStandbySlots[0]["sidePlayerId"]));

        var p2Objects = Objects(snapshot, "P2");
        Assert.DoesNotContain("P2-HAND", p2Objects.Keys);
        Assert.DoesNotContain("P2-HIDDEN-STANDBY", p2Objects.Keys);
        Assert.Contains("P2-GRAVEYARD", p2Objects.Keys);
        Assert.Contains("P2-BANISHED", p2Objects.Keys);
        Assert.Contains("P2-LEGEND", p2Objects.Keys);
        Assert.Contains("P2-HERO", p2Objects.Keys);
        Assert.Contains("P2-RIGHT-STANDBY", p2Objects.Keys);
        AssertSerializedSnapshotDoesNotContain(
            snapshot,
            "P1-DECK",
            "P1-RUNE-DECK",
            "P2-HAND",
            "P2-DECK",
            "P2-RUNE-DECK",
            "P2-HIDDEN-STANDBY");

        var p2Snapshot = snapshots["P2"];
        var p2Table = p2Snapshot.Table!;
        Assert.Equal("P2", p2Table.ViewerPlayerId);
        Assert.Equal("opponent", p2Table.Players.Single(player => player.PlayerId == "P1").Perspective);
        Assert.Equal("self", p2Table.Players.Single(player => player.PlayerId == "P2").Perspective);

        var p2LeftBattlefield = p2Table.Battlefields.Single(field => field.BattlefieldObjectId == "BF-LEFT");
        Assert.Equal(2, p2LeftBattlefield.StandbySlots.Count);
        Assert.Equal("HIDDEN", p2LeftBattlefield.StandbySlots[0].State);
        Assert.Null(p2LeftBattlefield.StandbySlots[0].ObjectId);
        Assert.Equal("P1", p2LeftBattlefield.StandbySlots[0].SidePlayerId);
        Assert.Equal("VISIBLE", p2LeftBattlefield.StandbySlots[1].State);
        Assert.Equal("P2-HIDDEN-STANDBY", p2LeftBattlefield.StandbySlots[1].ObjectId);
        Assert.Equal("P2", p2LeftBattlefield.StandbySlots[1].SidePlayerId);

        var p2RightBattlefield = p2Table.Battlefields.Single(field => field.BattlefieldObjectId == "BF-RIGHT");
        Assert.Equal(["P1-RIGHT-UNIT"], p2RightBattlefield.UnitsBySide["P1"]);
        Assert.Equal(["P2-RIGHT-UNIT"], p2RightBattlefield.UnitsBySide["P2"]);
        Assert.Single(p2RightBattlefield.StandbySlots);
        Assert.Equal("P2-RIGHT-STANDBY", p2RightBattlefield.StandbySlots[0].ObjectId);

        var p1ObjectsFromP2 = Objects(p2Snapshot, "P1");
        Assert.DoesNotContain("P1-HAND", p1ObjectsFromP2.Keys);
        Assert.DoesNotContain("P1-STANDBY", p1ObjectsFromP2.Keys);
        Assert.Contains("P1-GRAVEYARD", p1ObjectsFromP2.Keys);
        Assert.Contains("P1-BANISHED", p1ObjectsFromP2.Keys);
        var p2ObjectsFromP2 = Objects(p2Snapshot, "P2");
        Assert.Contains("P2-HIDDEN-STANDBY", p2ObjectsFromP2.Keys);
        Assert.Contains("P2-GRAVEYARD", p2ObjectsFromP2.Keys);
        Assert.Contains("P2-BANISHED", p2ObjectsFromP2.Keys);
        AssertSerializedSnapshotDoesNotContain(
            p2Snapshot,
            "P1-HAND",
            "P1-DECK",
            "P1-RUNE-DECK",
            "P1-STANDBY",
            "P2-DECK",
            "P2-RUNE-DECK");
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
                    Battlefields: ["BF-LEFT", "P1-LEFT-UNIT", "P1-STANDBY", "P1-RIGHT-UNIT"],
                    Graveyard: ["P1-GRAVEYARD"],
                    Banished: ["P1-BANISHED"],
                    LegendZone: ["P1-LEGEND"],
                    ChampionZone: ["P1-HERO"]),
                ["P2"] = new(
                    MainDeck: ["P2-DECK"],
                    RuneDeck: ["P2-RUNE-DECK"],
                    Hand: ["P2-HAND"],
                    Base: ["P2-BASE-UNIT", "P2-RUNE-1"],
                    Battlefields: ["BF-RIGHT", "P2-LEFT-UNIT", "P2-HIDDEN-STANDBY", "P2-RIGHT-UNIT", "P2-RIGHT-STANDBY"],
                    Graveyard: ["P2-GRAVEYARD"],
                    Banished: ["P2-BANISHED"],
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
            ["P1-GRAVEYARD"] = Unit("P1-GRAVEYARD", "P1"),
            ["P2-GRAVEYARD"] = Unit("P2-GRAVEYARD", "P2"),
            ["P1-BANISHED"] = Unit("P1-BANISHED", "P1"),
            ["P2-BANISHED"] = Unit("P2-BANISHED", "P2"),
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
            ["P1-RIGHT-UNIT"] = Unit("P1-RIGHT-UNIT", "P1"),
            ["P2-RIGHT-UNIT"] = Unit("P2-RIGHT-UNIT", "P2"),
            ["P1-STANDBY"] = Standby("P1-STANDBY", "P1", isFaceDown: true),
            ["P2-HIDDEN-STANDBY"] = Standby("P2-HIDDEN-STANDBY", "P2", isFaceDown: true),
            ["P2-RIGHT-STANDBY"] = Standby("P2-RIGHT-STANDBY", "P2", isFaceDown: false)
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
            ["P1-GRAVEYARD"] = new("P1", "GRAVEYARD"),
            ["P2-GRAVEYARD"] = new("P2", "GRAVEYARD"),
            ["P1-BANISHED"] = new("P1", "BANISHED"),
            ["P2-BANISHED"] = new("P2", "BANISHED"),
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
            ["P2-HIDDEN-STANDBY"] = new("P2", "BATTLEFIELD", "BF-LEFT"),
            ["P1-RIGHT-UNIT"] = new("P1", "BATTLEFIELD", "BF-RIGHT"),
            ["P2-RIGHT-UNIT"] = new("P2", "BATTLEFIELD", "BF-RIGHT"),
            ["P2-RIGHT-STANDBY"] = new("P2", "BATTLEFIELD", "BF-RIGHT")
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

    private static bool BoolValue(object? value)
    {
        return Assert.IsType<bool>(value);
    }

    private static void AssertSerializedSnapshotDoesNotContain(SnapshotDto snapshot, params string[] objectIds)
    {
        var serializedSnapshot = JsonSerializer.Serialize(snapshot);
        foreach (var objectId in objectIds)
        {
            Assert.DoesNotContain(objectId, serializedSnapshot, StringComparison.Ordinal);
        }
    }
}
