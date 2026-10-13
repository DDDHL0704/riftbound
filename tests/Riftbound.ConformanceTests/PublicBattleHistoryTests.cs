using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PublicBattleHistoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualBothSideDestructionAndBurnoutRetainPublicBattleHistoryWithoutCurrentHandContents(bool emptyDeck)
        => await Run(false, emptyDeck);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualAttackerBurnoutRetainsItsEarlierPublicBattleIdentity(bool defenderEmptyDeck)
        => await Run(true, defenderEmptyDeck);

    private static async Task Run(bool attackerEmptyDeck, bool emptyDeck)
    {
        var initial = AttackDamageDistributionTests.Position();
        foreach (var id in new[] { "ENEMY2", "ENEMY3" })
            initial = LocalDestructionRecallTests.At(initial, id, "P2", "BASE");
        foreach (var player in new[] { "P1", "P2" })
        {
            var ally = player + "-ALLY";
            initial = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(initial, ally, "OGN·001/298", "F", player), ally, player, "BASE");
            initial = AttackDamageDistributionTests.Spell(initial, player + "-SOUL", "SFD·163/221", player);
        }
        Assert.Empty(initial.PlayerZones["P2"].MainDeck);
        if (attackerEmptyDeck)
        {
            var privateDeck = initial.PlayerZones["P1"].MainDeck;
            initial = initial with {
                CardObjects = initial.CardObjects.Where(p => !privateDeck.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
                ObjectLocations = initial.ObjectLocations.Where(p => !privateDeck.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [] } }
            };
        }
        if (!emptyDeck)
            initial = initial with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["P2-DRAW"] = new("P2-DRAW", cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["P2-DRAW"] = new("P2", "MAIN_DECK") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { MainDeck = ["P2-DRAW"] } }
            };
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_PUBLIC_BATTLE_HISTORY_EVIDENCE");
        var branch = (attackerEmptyDeck ? "attacker-" : "") + (emptyDeck ? "burnout" : "deck");
        var driver = new StandbyDefendObserverTests.Driver(initial, root is null ? null : Path.Combine(root, branch));
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var target = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["ENEMY1"]));
        await driver.Act("P1", new PlayCardCommand("P1-SOUL", "SFD·163/221", ["HOST", "P1-ALLY"]));
        var attackerRemoved = await driver.Top();
        Assert.Equal(attackerEmptyDeck, attackerRemoved.Events.Any(e => e.Kind == "BURNOUT_APPLIED"));
        if (attackerEmptyDeck) Assert.Contains("HOST", driver.State.PlayerZones["P1"].Hand);
        else Assert.Contains("HOST", driver.State.PlayerZones["P1"].Graveyard);
        if (driver.State.PriorityPlayerId != "P2") await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("P2-SOUL", "SFD·163/221", ["ENEMY1", "P2-ALLY"]));
        var destroyed = await driver.Top();
        Assert.Contains(destroyed.Events, e => e.Kind == "UNIT_DESTROYED" && Equals(e.Payload["targetObjectId"], "ENEMY1"));
        Assert.Equal(emptyDeck, destroyed.Events.Any(e => e.Kind == "BURNOUT_APPLIED"));
        if (emptyDeck) Assert.Contains("ENEMY1", driver.State.PlayerZones["P2"].Hand);
        else Assert.Contains("ENEMY1", driver.State.PlayerZones["P2"].Graveyard);
        await driver.Top();
        var closed = await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        if (!closed.Events.Any(e => e.Kind == "BATTLE_CLOSED"))
            closed = await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        Assert.Contains(closed.Events, e => e.Kind == "BATTLE_CLOSED");
        Assert.DoesNotContain(closed.Events, e => e.Kind == "DAMAGE_APPLIED");
        var history = Assert.Single(driver.State.BattleResolutions);
        Assert.Equal("NO_RESULT", history.Kind); Assert.Null(history.WinnerPlayerId);
        Assert.Equal(new[] { "HOST" }, history.AttackerObjectIds);
        Assert.Equal(new[] { "ENEMY1" }, history.DefenderObjectIds);
        Assert.Equal(new[] { "ENEMY1", "HOST" }, history.PublicObjectBindings!.Select(b => b.ObjectId).Order(StringComparer.Ordinal));
        Assert.All(history.PublicObjectBindings!, b => Assert.Equal(0, b.Generation));
        var snapshot = JsonSerializer.SerializeToElement(closed.Snapshots["P1"], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var recorded = snapshot.GetProperty("timing").GetProperty("battleResolutions")[0];
        Assert.Equal("HOST", recorded.GetProperty("attackerObjectIds")[0].GetString());
        Assert.Equal("ENEMY1", recorded.GetProperty("defenderObjectIds")[0].GetString());
        if (emptyDeck) Assert.False(snapshot.GetProperty("players").GetProperty("P2").GetProperty("objects").TryGetProperty("ENEMY1", out _));
        foreach (var corrupt in new[] {
            history with { PublicObjectBindings = history.PublicObjectBindings!.Select(b => b.ObjectId == "ENEMY1" ? b with { Generation = 100 } : b).ToArray() },
            history with { PublicObjectBindings = history.PublicObjectBindings!.Append(history.PublicObjectBindings![0]).ToArray() },
            history with { PublicObjectBindings = history.PublicObjectBindings!.Append(new ObjectBinding("P2-ALLY", 0)).ToArray() },
            history with { ResolutionId = history.ResolutionId + ":PRIVATE-PROBE" } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with { BattleResolutions = [corrupt] }), e => e.Contains("captured public battle history reference"));
        foreach (var id in new[] { "HOST", "ENEMY1" }.Where(id => driver.State.PlayerZones.Values.Any(z => z.Hand.Contains(id))))
        {
            var forgedGeneration = history with { PublicObjectBindings = history.PublicObjectBindings!.Select(b => b.ObjectId == id
                ? b with { Generation = driver.State.CardObjects[id].ObjectGeneration } : b).ToArray() };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with { BattleResolutions = [forgedGeneration] }),
                e => e.Contains("captured public battle history reference"));
        }
        var playerView = closed.Snapshots["P1"];
        var timing = new Dictionary<string, object?>(playerView.Timing);
        var rows = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(timing["battleResolutions"]).Select(r => new Dictionary<string, object?>(r)).ToArray();
        rows[0]["defenderObjectIds"] = new[] { "P2-ALLY" };
        timing["battleResolutions"] = rows;
        var forgedViews = new Dictionary<string, RecoveredPlayerView> { ["P1"] = new("P1", driver.State.Tick, 0,
            playerView with { Timing = timing }, null, null, null) };
        Assert.Contains(MatchRecoveryValidator.Validate(driver.State.RoomId, 0, [], [], forgedViews, driver.State, driver.State.Tick),
            e => e.Contains("captured public battle history"));
        if (emptyDeck)
        {
            var opponent = new Dictionary<string, object?>(Assert.IsType<Dictionary<string, object?>>(playerView.Players["P2"]));
            opponent["objects"] = new Dictionary<string, object?>(Assert.IsType<Dictionary<string, object?>>(opponent["objects"])) {
                ["ENEMY1"] = JsonSerializer.SerializeToElement(driver.State.CardObjects["ENEMY1"], new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
            var players = playerView.Players.ToDictionary(p => p.Key, p => p.Value); players["P2"] = opponent;
            var snapshots = closed.Snapshots.ToDictionary(p => p.Key, p => p.Value);
            snapshots["P1"] = playerView with { Players = players };
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(closed with { Snapshots = snapshots }));
        }
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        driver.Export("steps.json", driver.Steps);
        driver.Export("replay.json", new { nativeCommands = driver.NativeCommands, accepted = driver.Commands().Length, coldRestores = driver.Commands().Length,
            attackerEmptyDeck, emptyDeck, replay, finalHash = MatchStateHasher.Hash(driver.State) });
        if (driver.NativeCommands > 0) Assert.Equal(driver.Commands().Length, driver.NativeCommands);
    }
}
