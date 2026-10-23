using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenFlowAssaultBattleTests
{
    [Fact]
    public Task TheRetainedPrintedAndAuraControlDoesNotUseTheNewVenConsumer()
        => PrintedAssaultAndFarronsIndependentAuraMustAddAlongsideAnyTemporaryGrant(false, false, 4);

    [Theory]
    [InlineData(false, false, 4)]
    [InlineData(false, true, 7)]
    [InlineData(true, true, 7)]
    public async Task PrintedAssaultAndFarronsIndependentAuraMustAddAlongsideAnyTemporaryGrant(bool flow, bool grant, int expected)
    {
        var initial = grant ? VenFlowCombatKeywordTests.Position(flow) : OptionalEntryTargetConfirmationTests.Position("UNL-009/219");
        initial = OptionalEntryTargetConfirmationTests.AddSpell(initial, "CONTROL-READY", "UNL-009/219");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·210/298", "P1"),
            ["FARRON"] = OfficialGarenConquestBattlefieldCountTests.Printed("FARRON", "OGN·015/298", "P1"),
            ["ENEMY2"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY2", "OGN·219/298", "P2") },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1"
                ? p.Value with { Base = ["GEAR", "FARRON"], Hand = [..p.Value.Hand, "ALLY"] }
                : p.Value with { Battlefields = [..p.Value.Battlefields, "ENEMY2"] }),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["ALLY"] = new("P1", "HAND"), ["FARRON"] = new("P1", "BASE"), ["ENEMY2"] = new("P2", "BATTLEFIELD", "OTHER") } };
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("ALLY", "OGN·210/298", [], Destination: "BASE"));
        Assert.Equal(2, driver.State.CardObjects["ALLY"].Power);
        Assert.Contains(CardCombatKeywordNames.Assault, driver.State.CardObjects["ALLY"].Tags);
        await driver.Act("P1", new PlayCardCommand(grant ? "SPELL" : "CONTROL-READY", grant ? "VEN·012" : "UNL-009/219", ["ALLY"])); await driver.Top();
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [], ["ALLY", "FARRON"]));
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act("P1", new DeclareBattleCommand("OTHER", ["ALLY", "FARRON"], ["ENEMY", "ENEMY2"], ["COMBAT_ASSIGNMENT"]));
        var actual = Might(driver.State);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_FLOW_KEYWORD_EVIDENCE") is { Length: > 0 } root) {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, $"printed-plus-aura-{flow}-{grant}.json"), JsonSerializer.Serialize(new {
                initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), expected, actual, replay
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task RealAttackDeclarationUsesEachAssaultGrantWhileTheEarlierNeutralMightStaysUnchanged(bool flow, int copies)
    {
        var initial = VenFlowCombatKeywordTests.Position(flow); var cards = initial.CardObjects.ToDictionary();
        var zones = initial.PlayerZones.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        cards["ENEMY2"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY2", "OGN·219/298", "P2");
        locations["ENEMY2"] = new("P2", "BATTLEFIELD", "OTHER");
        zones["P2"] = zones["P2"] with { Battlefields = [..zones["P2"].Battlefields, "ENEMY2"] };
        if (copies == 2) {
            cards["SECOND"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND", "VEN·012", "P1");
            locations["SECOND"] = new("P1", flow ? "GRAVEYARD" : "HAND");
            zones["P1"] = zones["P1"] with { Hand = flow ? zones["P1"].Hand : [..zones["P1"].Hand, "SECOND"],
                Graveyard = flow ? [..zones["P1"].Graveyard, "SECOND"] : zones["P1"].Graveyard };
        }
        initial = initial with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        var driver = new Driver(initial);
        foreach (var spell in copies == 2 ? new[] { "SPELL", "SECOND" } : ["SPELL"]) {
            await driver.Act("P1", new PlayCardCommand(spell, "VEN·012", ["ALLY"])); await driver.Top();
            Assert.Equal(4, driver.State.CardObjects["ALLY"].Power);
        }
        Assert.Equal(copies, driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Count(e => e.StartsWith("COMBAT_KEYWORD:强攻3:", StringComparison.Ordinal)));
        Assert.Equal(4, Might(driver.State));
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act("P1", new DeclareBattleCommand("OTHER", ["ALLY"], ["ENEMY", "ENEMY2"], ["COMBAT_ASSIGNMENT"]));
        Assert.True(driver.State.BattleState.IsActive); Assert.True(driver.State.CardObjects["ALLY"].IsAttacking);
        Assert.Equal(4, driver.State.CardObjects["ALLY"].Power); Assert.Equal(4 + copies * 3, Might(driver.State));
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!, c => c.Action == CommandTypes.AssignCombatDamage);
        var damage = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(candidate.Metadata!["damagePool"]);
        var lethal = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(candidate.Metadata!["lethalDamageThreshold"]);
        Assert.Equal(4 + copies * 3, damage["ALLY"]); Assert.Equal(4 + copies * 3, lethal["ALLY"]);
        // This is a legal, cold-restored combat-assignment prefix. Expiry and
        // physical spell exits are checked by the separate complete lifecycles.
        await driver.Replay();
        if (Environment.GetEnvironmentVariable("RIFTBOUND_FLOW_KEYWORD_EVIDENCE") is { Length: > 0 } root) {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, $"assault-battle-{flow}-{copies}.json"), JsonSerializer.Serialize(new {
                initial, final = driver.State, commands = driver.Commands(), events = driver.Events()
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
    }

    private static int Might(MatchState state) => CurrentPowerTargetTests.Snapshot(state)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("ALLY").GetProperty("effectivePower").GetInt32();
}
