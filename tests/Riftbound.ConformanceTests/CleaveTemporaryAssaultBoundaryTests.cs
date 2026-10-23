using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class CleaveTemporaryAssaultBoundaryTests
{
    private const string No = CleaveTemporaryAssaultTests.No;
    private const string Grant = "COMBAT_KEYWORD:强攻3:";

    [Theory]
    [InlineData("ALLY", false)] [InlineData("ALLY", true)]
    [InlineData("ENEMY", false)] [InlineData("ENEMY", true)]
    public async Task EitherPlayersNonattackingUnitReceivesTheGrantWithoutReadyingOrChangingMight(string target, bool exhausted)
    {
        var initial = Neutral(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [target] = initial.CardObjects[target] with { IsExhausted = exhausted } } };
        var driver = new Driver(initial); var power = initial.CardObjects[target].Power;
        await driver.Act("P1", new PlayCardCommand("SPELL", No, [target])); await driver.Top();
        Assert.Equal(exhausted, driver.State.CardObjects[target].IsExhausted);
        Assert.Equal(power, driver.State.CardObjects[target].Power);
        Assert.Equal(power, CleaveTemporaryAssaultTests.Might(driver.State, target == "ALLY" ? "P1" : "P2", target));
        Assert.Single(driver.State.CardObjects[target].UntilEndOfTurnEffects, e => e.StartsWith(Grant, StringComparison.Ordinal));
        Assert.Empty(driver.State.CardObjects[target].UntilEndOfTurnPowerModifiers);
        Assert.Equal(19, driver.State.RunePools["P1"].Mana); Assert.Equal(initial.RunePools["P1"].TotalPower, driver.State.RunePools["P1"].TotalPower);
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard);
        await driver.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(driver.State.CardObjects[target].UntilEndOfTurnEffects, e => e.StartsWith(Grant, StringComparison.Ordinal));
        Assert.Equal(power, driver.State.CardObjects[target].Power); await driver.Replay();
        CleaveTemporaryAssaultTests.Export($"expiry-{target}-{exhausted}", initial, driver);
    }

    [Theory]
    [InlineData("ALLY", 4)] [InlineData("ENEMY", 7)]
    public async Task SwiftAlsoWorksDuringTheOpponentsRealMovementDuelWithTheCastersFocus(string target, int expected)
    {
        var initial = Neutral() with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { IsExhausted = false } } };
        var driver = new Driver(initial);
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P2", new PassFocusCommand()); Assert.Equal("P1", driver.State.FocusPlayerId);
        await driver.Act("P1", new PlayCardCommand("SPELL", No, [target])); await driver.Top();
        Assert.Equal(4, driver.State.CardObjects[target].Power);
        Assert.Equal(expected, CleaveTemporaryAssaultTests.Might(driver.State, target == "ALLY" ? "P1" : "P2", target));
        Assert.Single(driver.State.CardObjects[target].UntilEndOfTurnEffects, e => e.StartsWith(Grant, StringComparison.Ordinal));
        await driver.Replay(); CleaveTemporaryAssaultTests.Export("offturn-duel-" + target, initial, driver, expected);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RealReturnBeforeOrAfterResolutionSeparatesTheNewIncarnationFromTheOldGrant(bool before)
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(Neutral(), "RETURN", "OGN·104/298");
        var generation = initial.CardObjects["ALLY"].ObjectGeneration; var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"]));
        if (!before) { await driver.Top(); Assert.Single(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects); }
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await driver.Top();
        if (before) await driver.Top();
        Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Hand); Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard);
        Assert.True(driver.State.CardObjects["ALLY"].ObjectGeneration > generation);
        Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects); Assert.Equal(4, driver.State.CardObjects["ALLY"].Power);
        await driver.Replay(); CleaveTemporaryAssaultTests.Export("return-" + before, initial, driver);
    }

    [Fact]
    public async Task CounteringTheRealSpellPreventsTheGrantAndRetainsItsManaPayment()
    {
        var initial = AttackDamageDistributionTests.Spell(Neutral(), "COUNTER", "OGN·045/298", "P2"); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"])); var stack = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [stack])); await driver.Top();
        Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects); Assert.Equal(4, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(19, driver.State.RunePools["P1"].Mana); Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(driver.State.StackItems); await driver.Replay(); CleaveTemporaryAssaultTests.Export("countered", initial, driver);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task ActualCombatUsesEachGrantThenNeutralMightFallsBackBeforeTheTurnEnds(int copies)
    {
        var initial = Neutral(); var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        foreach (var id in new[] { "ENEMY", "ENEMY2" }) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·271/298", "P2"); locations[id] = new("P2", "BATTLEFIELD", "OTHER");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P2" ? p.Value with { Battlefields = [..p.Value.Battlefields, "ENEMY2"] } : p.Value) };
        initial = OptionalEntryTargetConfirmationTests.AddSpell(initial, "READY", "UNL-009/219");
        if (copies == 2) initial = OptionalEntryTargetConfirmationTests.AddSpell(initial, "SECOND", No);
        var driver = new Driver(initial);
        foreach (var id in copies == 1 ? new[] { "SPELL" } : ["SPELL", "SECOND"]) {
            await driver.Act("P1", new PlayCardCommand(id, No, ["ALLY"])); await driver.Top();
        }
        await driver.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["ALLY"])); await driver.Top();
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand()); await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act("P1", new DeclareBattleCommand("OTHER", ["ALLY"], ["ENEMY", "ENEMY2"], ["COMBAT_ASSIGNMENT"]));
        var expected = 4 + 3 * copies; Assert.Equal(expected, CleaveTemporaryAssaultTests.Might(driver.State, "P1", "ALLY"));
        var assignments = 0;
        while (driver.State.BattleState.IsActive && assignments < 4) {
            var prompt = ResolutionResult.BuildPrompts(driver.State).Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            await driver.Act(prompt.PlayerId, new AssignCombatDamageCommand(driver.State.BattleState.BattleId!, driver.State.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(prompt)));
            assignments++;
        }
        Assert.Equal(2, assignments); Assert.False(driver.State.BattleState.IsActive);
        Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Battlefields); Assert.False(driver.State.CardObjects["ALLY"].IsAttacking);
        Assert.Equal(4, driver.State.CardObjects["ALLY"].Power); Assert.Equal(4, CleaveTemporaryAssaultTests.Might(driver.State, "P1", "ALLY"));
        Assert.Equal(copies, driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Count(e => e.StartsWith(Grant, StringComparison.Ordinal)));
        var damage = driver.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "ALLY")).ToArray();
        Assert.NotEmpty(damage); Assert.All(damage, e => Assert.Equal(expected, Convert.ToInt32(e.Event.Payload["combatPower"])));
        Assert.Equal(expected, damage.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
        await driver.Act("P1", new EndTurnCommand()); Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await driver.Replay(); CleaveTemporaryAssaultTests.Export("actual-combat-" + copies, initial, driver, expected);
    }

    [Theory]
    [InlineData("mana")] [InlineData("wrong-turn")] [InlineData("response")] [InlineData("duel-focus")]
    [InlineData("source-graveyard")] [InlineData("source-banished")]
    [InlineData("equipment")] [InlineData("battlefield")] [InlineData("graveyard")] [InlineData("deck")]
    [InlineData("missing")] [InlineData("extra")]
    public async Task SwiftIsNotReactionAndInvalidSourcesCostsOrTargetsPreserveEveryView(string branch)
    {
        var initial = Neutral(); string[] targets = ["ALLY"];
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } };
        if (branch == "wrong-turn") initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "duel-focus") initial = initial with { TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "response") {
            initial = OptionalEntryTargetConfirmationTests.AddSpell(initial, "PENDING", "OGN·048/298");
            var opened = await new CoreRuleEngine().ResolveAsync(initial, new("open-response", "P1", CommandTypes.PlayCard), new PlayCardCommand("PENDING", "OGN·048/298", []), default);
            Assert.True(opened.Accepted, opened.ErrorMessage); initial = opened.State;
        }
        if (branch.StartsWith("source-", StringComparison.Ordinal)) {
            var banished = branch == "source-banished";
            initial = initial with { ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SPELL"] = new("P1", banished ? "BANISHED" : "GRAVEYARD") },
                PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [],
                    Graveyard = banished ? p.Value.Graveyard : [..p.Value.Graveyard, "SPELL"], Banished = banished ? ["SPELL"] : p.Value.Banished } : p.Value) };
        }
        if (branch == "equipment") targets = ["GEAR"];
        if (branch == "battlefield") targets = ["BF"];
        if (branch == "graveyard") targets = ["GRAVE"];
        if (branch == "deck") targets = ["P1-DRAW-1"];
        if (branch == "missing") targets = [];
        if (branch == "extra") targets = ["ALLY", "ENEMY"];
        await OptionalEntryTargetBoundaryTests.Reject(initial, "P1", new PlayCardCommand("SPELL", No, targets));
    }

    private static MatchState Neutral()
    {
        var initial = VenFlowCombatKeywordTests.Position(false);
        return initial with { RoomId = "CLEAVE-ASSAULT-BOUNDARY", CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", No, "P1") } };
    }
}
