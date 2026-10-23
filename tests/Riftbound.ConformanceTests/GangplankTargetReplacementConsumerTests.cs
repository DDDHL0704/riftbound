using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class GangplankTargetReplacementConsumerTests
{
    public static IEnumerable<object[]> ChosenActions() => new[] { "VEN·086", "VEN·181" }
        .SelectMany(no => new[] { "P1", "P2" }.SelectMany(caster => new[] { "stun", "negative", "return" }
            .SelectMany(action => new[] { false, true }.Select(empowered => new object[] { no, caster, action, empowered }))));

    [Theory]
    [MemberData(nameof(ChosenActions))]
    public async Task ActualFriendlyAndEnemySelectedSpellsReplaceOnlyOnTheRealEmpoweredIncarnation(
        string no, string caster, string action, bool empowered)
    {
        var initial = Position(no, caster, action); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        if (empowered) {
            await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await driver.Top();
        }
        if (caster == "P2") {
            await driver.Act("P1", new EndTurnCommand());
            var runes = Enumerable.Range(1, 8).Select(i => "P2-MANA-" + i).ToArray();
            await driver.Act("P2", new TapRuneCommand(runes[0], runes));
            if (action is "stun" or "return") await driver.Act("P2", new RecycleRuneCommand(runes[0]));
            if (action == "return") await driver.Act("P2", new RecycleRuneCommand(runes[1]));
        }
        Assert.Equal(empowered, driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        var runeDeckBefore = driver.State.PlayerZones["P1"].RuneDeck.ToArray();
        var eventCount = driver.Events().Length;
        var spell = Spell(caster, action);
        await driver.Act(caster, new PlayCardCommand("ACTION", spell, ["UNIT"])); await driver.Top();
        await driver.Replay(); Export($"chosen-{no}-{caster}-{action}-{empowered}", initial, driver, new {
            no, caster, action, empowered, expectedMight = empowered ? 9 : action == "negative" ? -4 : 6,
            expectedReturned = !empowered && action == "return", generation, runeDeckBefore
        });

        var unit = driver.State.CardObjects["UNIT"];
        if (!empowered) {
            Assert.Equal(action == "negative" ? -4 : 6, unit.Power);
            Assert.Equal(action == "stun", unit.UntilEndOfTurnEffects.Contains("STUNNED"));
            if (action == "return") {
                Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand);
                Assert.True(unit.ObjectGeneration > generation); Assert.False(unit.IsEmpowered);
                Assert.Equal(caster == "P1" ? 0 : runeDeckBefore.Length, driver.State.PlayerZones["P1"].RuneDeck.Count);
            } else Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Battlefields);
            return;
        }

        // The same literal +3 replaces each eligible chosen action, irrespective
        // of whether the spell's controller is friendly or enemy.
        Assert.Equal(9, unit.Power); Assert.True(unit.IsEmpowered);
        Assert.Equal(generation, unit.ObjectGeneration); Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("BATTLEFIELD", driver.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal("BF", driver.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.DoesNotContain("STUNNED", unit.UntilEndOfTurnEffects);
        Assert.Equal(3, unit.UntilEndOfTurnPowerModifier);
        var entry = Assert.Single(unit.UntilEndOfTurnPowerModifiers);
        Assert.Equal(3, entry.PowerDelta); Assert.Equal(3, entry.RequestedPowerDelta);
        Assert.Equal(0, entry.MinimumPower); Assert.Null(entry.SnapshotMight); Assert.Null(entry.ChosenNegativeMight);
        // CN359.3.e.14.b: OGN104's follow-up refers to the unit's owner,
        // not to successful completion of its replaced return action.
        var independentRuneCall = action == "return" && caster == "P1";
        Assert.Equal(runeDeckBefore.Skip(independentRuneCall ? 1 : 0), driver.State.PlayerZones["P1"].RuneDeck);
        var actions = driver.Events().Skip(eventCount).Select(e => e.Event).ToArray();
        Assert.DoesNotContain(actions, e => e.Kind == "STATUS_EFFECT_APPLIED" && Equals(e.Payload.GetValueOrDefault("effectId"), "STUNNED"));
        if (independentRuneCall) {
            var call = Assert.Single(actions, e => e.Kind == "RUNES_CALLED");
            Assert.Equal("P1", call.Payload["playerId"]); Assert.Equal(1, call.Payload["count"]);
            Assert.Contains(runeDeckBefore[0], driver.State.PlayerZones["P1"].Base);
            Assert.True(driver.State.CardObjects[runeDeckBefore[0]].IsExhausted);
        } else Assert.DoesNotContain(actions, e => e.Kind == "RUNES_CALLED");
        Assert.Single(actions, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "UNIT"));
        foreach (var view in ResolutionResult.BuildSnapshots(driver.State).Values)
            Assert.False(HasPrivateReplacementField(JsonSerializer.SerializeToElement(view, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        await driver.Act(caster, new EndTurnCommand());
        Assert.Equal(6, driver.State.CardObjects["UNIT"].Power); Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers); await driver.Replay();
    }

    internal static string Spell(string caster, string action) => action switch {
        "stun" => "OGN·050/298", "negative" => "UNL-066/219", _ => caster == "P1" ? "OGN·104/298" : "OGN·172/298"
    };

    internal static MatchState Position(string no, string caster, string action)
    {
        var initial = AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position(no), "ACTION", Spell(caster, action), caster);
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary(); var zones = initial.PlayerZones.ToDictionary();
        for (var i = 1; i <= 8; i++) {
            var id = "P2-MANA-" + i;
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, action == "return" ? "UNL-R05" : "UNL-R02", "P2");
            locations[id] = new("P2", "BASE"); zones["P2"] = zones["P2"] with { Base = [..zones["P2"].Base, id] };
        }
        cards["P1-RETURN-RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-RETURN-RUNE", "UNL-R02", "P1");
        locations["P1-RETURN-RUNE"] = new("P1", "RUNE_DECK"); zones["P1"] = zones["P1"] with { RuneDeck = ["P1-RETURN-RUNE"] };
        return initial with { RoomId = "GANGPLANK-C7-CHOSEN-CONSUMER", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(30, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 4, [RuneTrait.Blue] = 4, [RuneTrait.Green] = 4 }) } };
    }

    internal static void Export(string name, MatchState initial, Driver driver, object expected)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_GANGPLANK_C7_CONSUMER_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, name.Replace('·', '-') + ".json"),
            JsonSerializer.Serialize(new { initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), expected },
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
    private static bool HasPrivateReplacementField(JsonElement value)
        => value.ValueKind switch {
            JsonValueKind.Object => value.EnumerateObject().Any(p =>
                string.Equals(p.Name, "chosenActionReplacement", StringComparison.OrdinalIgnoreCase)
                    || HasPrivateReplacementField(p.Value)),
            JsonValueKind.Array => value.EnumerateArray().Any(HasPrivateReplacementField),
            _ => false
        };

}
