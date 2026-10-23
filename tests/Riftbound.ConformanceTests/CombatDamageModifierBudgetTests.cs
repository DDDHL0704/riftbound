using System.Text;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN core effective 2026-10-16, 467.2.c.4.a/c.5: a modified assignment
// consumes its original budget, and its damage modifier is applied once.
public sealed class CombatDamageModifierBudgetTests
{
    private const string DoubleDamage = "DAMAGE_RECEIVED_DOUBLED_THIS_TURN";

    [Theory]
    [InlineData(2, 0, 1)]
    [InlineData(3, 0, 2)]
    [InlineData(4, 1, 2)]
    [InlineData(5, 1, 2)]
    public async Task DoubledLethalAssignmentConsumesTheMinimumOriginalBudget(int power, int marked, int budget)
    {
        var initial = Position(power, marked, budget + 2);
        var driver = Driver(initial, $"minimum-{power}-{marked}");
        var first = await driver.Act("P1", Assignment(new("A1", "D1", budget), new("A1", "D2", 2)));
        Assert.DoesNotContain(first.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.Equal(marked, driver.State.CardObjects["D1"].Damage);
        var thresholds = Thresholds(first);
        Assert.Equal(budget, thresholds["D1"]);
        Assert.Equal(2, thresholds["D2"]);

        var done = await driver.Act("P2", Assignment(new("D1", "A1", power), new("D2", "A1", 2)));
        AssertDamage(done, "D1", budget * 2);
        AssertDamage(done, "D2", 2);
        Assert.All(new[] { "D1", "D2" }, id => Assert.Contains(id, driver.State.PlayerZones["P2"].Graveyard));
        Assert.False(driver.State.BattleState.IsActive);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task CurrentCoreAllowsOneBudgetPointForEachOfTwoDoubledTwoMightTargets()
    {
        var initial = Position(2, 0, 2);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["D2"] = initial.CardObjects["D2"] with { UntilEndOfTurnEffects = [DoubleDamage] }
        } };
        var driver = Driver(initial, "two-doubled-targets");
        await driver.Act("P1", Assignment(new("A1", "D1", 1), new("A1", "D2", 1)));
        var done = await driver.Act("P2", Assignment(new("D1", "A1", 2), new("D2", "A1", 2)));
        AssertDamage(done, "D1", 2);
        AssertDamage(done, "D2", 2);
        Assert.All(new[] { "D1", "D2" }, id => Assert.Contains(id, driver.State.PlayerZones["P2"].Graveyard));
        await Replay(initial, driver);
    }

    [Fact]
    public async Task PooledSourcesShareTheModifiedLethalBudgetWithoutRoundingEachContribution()
    {
        var initial = Position(4, 0, 1, singleAttacker: false);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["D2"] = initial.CardObjects["D2"] with { Power = 1 }
        } };
        var driver = Driver(initial, "pooled-sources");
        await driver.Act("P1", Assignment(new("A1", "D1", 1), new("A2", "D1", 1), new("A2", "D2", 1)));
        var done = await driver.Act("P2", Assignment(new("D1", "A1", 1), new("D1", "A2", 3), new("D2", "A2", 1)));
        AssertDamage(done, "D1", 4);
        AssertDamage(done, "D2", 1);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public async Task UnmodifiedLethalAmountCannotOverfillDoubledBulwarkBeforeTheOtherTarget(int power, int assigned)
    {
        var initial = Position(power, 0, assigned + 2);
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new("old-lethal-is-overfill", "P1", CommandTypes.AssignCombatDamage),
            Assignment(new("A1", "D1", assigned), new("A1", "D2", 2)), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public void PromptAndSnapshotPublishTheSameOriginalBudgetThresholdAndLegalSuggestion()
    {
        var initial = Position(3, 0, 4);
        var before = MatchStateHasher.Hash(initial);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"].View!.Metadata!;
        var threshold = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(prompt["lethalDamageThreshold"]);
        Assert.Equal(2, threshold["D1"]);
        var suggestion = Assert.IsAssignableFrom<IReadOnlyDictionary<string, long>>(prompt["suggestedDamageByTarget"]);
        Assert.Equal(2, suggestion["D1"]);
        Assert.Equal(2, suggestion["D2"]);
        var snapshot = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(initial)["P1"], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var timing = snapshot.GetProperty("timing").GetProperty("battle").GetProperty("damageAssignment");
        Assert.Equal(2, timing.GetProperty("lethalDamageThreshold").GetProperty("D1").GetInt32());
        Assert.Equal(before, MatchStateHasher.Hash(initial));
    }

    [Theory]
    [InlineData("overfilled-priority")]
    [InlineData("zero-entry")]
    [InlineData("opponent-source")]
    [InlineData("duplicate-marker")]
    [InlineData("invalid-encoding")]
    public async Task ColdValidationRejectsForgedPartialBudgetLedgers(string mutation)
    {
        var initial = Position(2, 0, 3);
        var driver = Driver(initial, "forged-" + mutation);
        await driver.Act("P1", Assignment(new("A1", "D1", 1), new("A1", "D2", 2)));
        const string prefix = "BATTLE_DAMAGE_ASSIGNMENT_LEDGER:";
        var original = Assert.Single(driver.State.UntilEndOfTurnEffects, e => e.StartsWith(prefix, StringComparison.Ordinal));
        var assignments = new List<CombatDamageAssignmentDto> { new("A1", "D1", 1), new("A1", "D2", 2) };
        if (mutation == "overfilled-priority") assignments = [new("A1", "D1", 2), new("A1", "D2", 1)];
        if (mutation == "zero-entry") assignments.Add(new("A1", "D1", 0));
        if (mutation == "opponent-source") assignments.Add(new("D1", "A1", 2));
        var forgedMarker = mutation == "invalid-encoding" ? prefix + "invalid-base64"
            : prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new { BattleId = "battle:BF", BattlefieldId = "BF", Assignments = assignments },
                new JsonSerializerOptions { WriteIndented = mutation == "duplicate-marker" })));
        var markers = driver.State.UntilEndOfTurnEffects.Where(e => e != original).Append(forgedMarker).ToList();
        if (mutation == "duplicate-marker") markers.Add(original);
        var forged = driver.State with { UntilEndOfTurnEffects = markers };
        forged = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(forged))!;
        Assert.Contains("invalid combat damage assignment ledger", OfficialInsightAndSpellLockTests.Errors(forged));

        await driver.Act("P2", Assignment(new("D1", "A1", 2), new("D2", "A1", 2)));
        await Replay(initial, driver);
    }

    [Fact]
    public async Task BudgetThresholdDoesNotRedefineTheExistingExcessLedger()
    {
        var initial = Position(3, 0, 8);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["D1"] = initial.CardObjects["D1"] with { Tags = [CardObjectTags.UnitCard] },
            ["D2"] = initial.CardObjects["D2"] with { Power = 1 }
        } };
        var driver = Driver(initial, "existing-excess-input");
        await driver.Act("P1", Assignment(new("A1", "D2", 1), new("A1", "D1", 7)));
        var done = await driver.Act("P2", Assignment(new("D1", "A1", 3), new("D2", "A1", 1)));
        AssertDamage(done, "D1", 14);
        // Preserve the pre-C3 ledger input (7 - 3), rather than accidentally
        // substituting the budget threshold (7 - 2). This is compatibility
        // evidence, not a ruling on excess after damage modifiers.
        Assert.Equal(4, Assert.Single(done.Events, e => e.Kind == "BATTLEFIELD_CONQUERED").Payload["assignedOverkillDamageToEnemyUnits"]);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task ActualLotusSpellDoublesAutomaticCombatDamageOnce()
    {
        var initial = AttackDamageDistributionTests.Spell(
            OfficialLegendConquestTests.CombatPosition("OGN·269/298", power: 8, exhausted: true),
            "LOTUS", "UNL-013/219", "P1");
        initial = initial with { Tick = 0, CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["D"] = initial.CardObjects["D"] with { Power = 3 }
        } };
        var driver = Driver(initial, "actual-spell-automatic-combat");
        await driver.Act("P1", new PlayCardCommand("LOTUS", "UNL-013/219", ["D"]));
        await driver.Top();
        Assert.Contains(DoubleDamage, driver.State.CardObjects["D"].UntilEndOfTurnEffects);
        await driver.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        var done = await driver.Act("P1", new DeclareBattleCommand("BF", ["A"], ["D"], ["COMBAT_ASSIGNMENT"]));
        AssertDamage(done, "D", 16);
        Assert.Contains("D", driver.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(1, driver.State.PlayerScores["P1"]);
        Assert.True(driver.State.CardObjects["LEGEND"].IsExhausted);
        await driver.Top();
        Assert.False(driver.State.CardObjects["LEGEND"].IsExhausted);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task EitherPlayerCanSurrenderAfterTheFirstSideCommitsItsBudget(string player)
    {
        var initial = Position(2, 0, 3);
        var driver = Driver(initial, "surrender-partial-" + player);
        await driver.Act("P1", Assignment(new("A1", "D1", 1), new("A1", "D2", 2)));
        var done = await driver.Act(player, new SurrenderCommand());
        Assert.Equal(MatchStatuses.Finished, driver.State.Status);
        Assert.Equal(player == "P1" ? "P2" : "P1", driver.State.WinnerPlayerId);
        Assert.DoesNotContain(driver.State.UntilEndOfTurnEffects,
            e => e.StartsWith("BATTLE_DAMAGE_ASSIGNMENT_LEDGER:", StringComparison.Ordinal));
        Assert.DoesNotContain(done.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.Equal("SURRENDER", Assert.Single(done.Events, e => e.Kind == "MATCH_WON").Payload["reason"]);
        await Replay(initial, driver);
    }

    private static MatchState Position(int power, int marked, int attackerPower, bool singleAttacker = true)
    {
        var state = OfficialPooledCombatDamageTests.Position(singleAttacker, power, 2, bulwark: true);
        return state with
        {
            Tick = 0,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                ["A1"] = state.CardObjects["A1"] with { Power = attackerPower },
                ["D1"] = state.CardObjects["D1"] with { Damage = marked, UntilEndOfTurnEffects = [DoubleDamage] }
            }
        };
    }

    private static AssignCombatDamageCommand Assignment(params CombatDamageAssignmentDto[] assignments)
        => new("battle:BF", "BF", assignments);

    private static IReadOnlyDictionary<string, int> Thresholds(ResolutionResult result)
        => Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(result.Prompts["P2"].View!.Metadata!["lethalDamageThreshold"]);

    private static void AssertDamage(ResolutionResult result, string target, int expected)
        => Assert.Equal(expected, result.Events.Where(e => e.Kind == "DAMAGE_APPLIED"
            && Equals(e.Payload.GetValueOrDefault("targetObjectId"), target)).Sum(e => Convert.ToInt32(e.Payload["damage"])));

    private static StandbyDefendObserverTests.Driver Driver(MatchState initial, string scenario)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_COMBAT_MODIFIER_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, scenario));
        driver.Export("initial.json", initial);
        return driver;
    }

    private static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var events = driver.Events().Select(e => e with
        {
            Event = JsonSerializer.Deserialize<GameEvent>(JsonSerializer.Serialize(e.Event, options), options)!
        }).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        driver.Export("commands.json", driver.Commands());
        driver.Export("events.json", events);
        driver.Export("final.json", driver.State);
        driver.Export("receipt.json", new { commands = driver.Commands().Length, events = events.Length, replay,
            engineAssembly = typeof(CoreRuleEngine).Assembly.Location, engineMvid = typeof(CoreRuleEngine).Assembly.ManifestModule.ModuleVersionId,
            scope = "Existing single doubling effect combat fixture; real MatchSession intents, per-command cold restoration, duplicate intents and JSON journal replay. No SQL/API or whole-card certification." });
    }
}
