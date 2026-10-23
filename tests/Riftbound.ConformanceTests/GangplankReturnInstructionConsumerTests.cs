using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class GangplankReturnInstructionConsumerTests
{
    [Theory]
    [InlineData("VEN·086", false)] [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)] [InlineData("VEN·181", true)]
    public async Task Ogn104IndependentRuneInstructionRemainsButGhostBayRequiresTheActualReturnEvent(string no, bool empowered)
    {
        var initial = Position(no); var bay = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "UNL-214/219", "P1") with { ControllerId = "P1" };
        var second = OfficialGarenConquestBattlefieldCountTests.Printed("P1-SECOND-RUNE", "UNL-R02", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["BF"] = bay, ["P1-SECOND-RUNE"] = second },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["P1-SECOND-RUNE"] = new("P1", "RUNE_DECK") },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { RuneDeck = [..p.Value.RuneDeck, "P1-SECOND-RUNE"] } : p.Value) };
        var driver = await Play(initial, no, empowered); var before = driver.Events().Length;
        await driver.Act("P1", new PlayCardCommand("ACTION", "OGN·104/298", ["UNIT"])); await driver.Top(); await driver.Replay();
        var actions = driver.Events().Skip(before).Select(e => e.Event).ToArray();
        GangplankTargetReplacementConsumerTests.Export($"ogn104-and-return-trigger-{no}-{empowered}", initial, driver, new {
            expectedMight = empowered ? 9 : 6, expectedRuneDeckCount = empowered ? 1 : 0,
            independentRuneInstruction = 1, actualReturnEventTriggers = empowered ? 0 : 1
        });
        // No 'if you do' in OGN104: this is an object-linked second instruction.
        var independent = Assert.Single(actions, e => e.Kind == "RUNES_CALLED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "ACTION"));
        Assert.Equal(1, independent.Payload["count"]); Assert.Equal("P1", independent.Payload["playerId"]);
        Assert.Equal(empowered ? 1 : 0, driver.State.PlayerZones["P1"].RuneDeck.Count);
        if (empowered) {
            Assert.Equal(9, driver.State.CardObjects["UNIT"].Power); Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Battlefields);
            Assert.DoesNotContain(actions, e => e.Kind == "UNIT_RETURNED_TO_HAND");
            Assert.DoesNotContain(actions, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED");
            Assert.DoesNotContain(actions, e => e.Kind == "RUNES_CALLED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "BF"));
        } else {
            Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand);
            Assert.Single(actions, e => e.Kind == "UNIT_RETURNED_TO_HAND");
            Assert.Single(actions, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED" && Equals(e.Payload.GetValueOrDefault("battlefieldObjectId"), "BF"));
            Assert.Single(actions, e => e.Kind == "RUNES_CALLED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "BF"));
        }
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task ARealCounterOfTheWholeOgn104AlsoCancelsItsIndependentInstruction(string no)
    {
        var initial = Add(Position(no), "COUNTER", "OGN·045/298", "P2");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P2"] = initial.RunePools["P2"] with { PowerByTrait = new Dictionary<string, int>(initial.RunePools["P2"].PowerByTrait) { [RuneTrait.Green] = 4 } } } };
        var driver = await Play(initial, no, true); var before = driver.Events().Length;
        await driver.Act("P1", new PlayCardCommand("ACTION", "OGN·104/298", ["UNIT"]));
        var item = Assert.Single(driver.State.StackItems).StackItemId; await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [item])); await driver.Top();
        Assert.Empty(driver.State.StackItems); Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Single(driver.State.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain(driver.Events().Skip(before), e => e.Event.Kind == "RUNES_CALLED");
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export("ogn104-whole-counter-" + no, initial, driver,
            new { expectedMight = 6, independentRuneInstructions = 0 });
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task ARealReactionDestructionInvalidatesTheOldTargetRatherThanReplacingItsReturn(string no)
    {
        var initial = Add(Position(no), "REMOVE", "SFD·163/221", "P1"); var driver = await Play(initial, no, true);
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration; var before = driver.Events().Length;
        await driver.Act("P1", new PlayCardCommand("ACTION", "OGN·104/298", ["UNIT"]));
        await driver.Act("P1", new PlayCardCommand("REMOVE", "SFD·163/221", ["UNIT", "ALLY"])); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Graveyard); Assert.True(driver.State.CardObjects["UNIT"].ObjectGeneration > generation);
        await driver.Top(); Assert.Single(driver.State.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain(driver.Events().Skip(before), e => e.Event.Kind == "RUNES_CALLED");
        Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export("ogn104-target-left-" + no, initial, driver,
            new { expectedOldTargetIgnored = true, independentRuneInstructions = 0 });
    }

    private static MatchState Position(string no) => GangplankTargetReplacementConsumerTests.Position(no, "P1", "return");
    private static MatchState Add(MatchState state, string id, string no, string player)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "HAND") },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == player ? p.Value with { Hand = [..p.Value.Hand, id] } : p.Value) };
    private static async Task<Driver> Play(MatchState initial, string no, bool empowered)
    {
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        if (empowered) { await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await driver.Top(); } return driver;
    }
}
