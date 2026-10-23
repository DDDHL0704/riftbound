using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 433.1.b / 370.2 and Unleashed judge FAQ 260511: two Lotus
// applications make Hextech Ray deal 12, with each replacement applied once.
public sealed class DamageDoublingApplicationTests
{
    private const string Double = "DAMAGE_RECEIVED_DOUBLED_THIS_TURN";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 12)]
    [InlineData(3, 24)]
    public async Task ActualSeparateLotusSpellsMultiplyEachDamageEvent(int applications, int expected)
    {
        var initial = Position();
        var driver = Driver(initial, "separate-" + applications);
        for (var i = 1; i <= applications; i++) await Cast(driver, "LOTUS" + i, "UNL-013/219");
        Assert.Equal(applications, Effects(driver.State, "D").Length);
        Assert.Equal(80, driver.State.CardObjects["D"].Power);
        var first = await Cast(driver, "RAY1", "OGN·009/298");
        AssertDamage(first, "D", expected);
        var second = await Cast(driver, "RAY2", "OGN·009/298");
        AssertDamage(second, "D", expected);
        Assert.Equal(expected * 2, driver.State.CardObjects["D"].Damage);
        Assert.Equal(applications, Effects(driver.State, "D").Length);
        if (applications == 1) Assert.Equal(new[] { Double }, Effects(driver.State, "D"));
        await Replay(initial, driver);
    }

    [Fact]
    public async Task EchoCreatesTwoApplicationsEvenWithTheSameSourceAndStackItem()
    {
        var initial = Position() with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = Driver(initial, "echo");
        await driver.Act("P1", new PlayCardCommand("LOTUS1", "UNL-013/219", ["D"],
            OptionalCosts: ["ECHO"], RepeatChoices: [new("", ["D"])]));
        await driver.Top();
        Assert.Equal(2, Effects(driver.State, "D").Length);
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "STATUS_EFFECT_APPLIED"));
        AssertDamage(await Cast(driver, "RAY1", "OGN·009/298"), "D", 12);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task OpposingControllersApplicationsCombineOnTheSameTarget()
    {
        var initial = Position();
        var driver = Driver(initial, "opposing-controllers");
        await driver.Act("P1", new PlayCardCommand("LOTUS1", "UNL-013/219", ["D"]));
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("P2-LOTUS", "UNL-013/219", ["D"]));
        await driver.Top();
        await driver.Top();
        Assert.Equal(2, Effects(driver.State, "D").Length);
        AssertDamage(await Cast(driver, "RAY1", "OGN·009/298"), "D", 12);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task ApplicationsOnDifferentUnitsDoNotShareTheirMultiplier()
    {
        var initial = AddDefender(Position(), 80);
        var driver = Driver(initial, "independent-targets");
        await Cast(driver, "LOTUS1", "UNL-013/219");
        await Cast(driver, "LOTUS2", "UNL-013/219", "D2");
        AssertDamage(await Cast(driver, "RAY1", "OGN·009/298"), "D", 6);
        AssertDamage(await Cast(driver, "RAY2", "OGN·009/298", "D2"), "D2", 6);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CombatUsesOneSharedOriginalBudgetAndDoesNotApplyDoublingTwice(bool pooledSources)
    {
        var initial = AddDefender(Position(), 2);
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["A"] = initial.CardObjects["A"] with { Power = pooledSources ? 1 : 4 },
            ["D"] = initial.CardObjects["D"] with { Power = 5, Tags = [CardObjectTags.UnitCard, CardCombatKeywordNames.Bulwark] }
        };
        if (pooledSources) cards["A2"] = cards["A"] with { ObjectId = "A2", Power = 3 };
        initial = initial with { CardObjects = cards };
        if (pooledSources) initial = initial with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "A2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["A2"] = new("P1", "BASE") }
        };
        var driver = Driver(initial, "combat-" + pooledSources);
        await Cast(driver, "LOTUS1", "UNL-013/219");
        await Cast(driver, "LOTUS2", "UNL-013/219");
        await driver.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", [],
            SourceObjectIds: pooledSources ? ["A", "A2"] : null));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        await driver.Act("P1", new DeclareBattleCommand("BF", pooledSources ? ["A", "A2"] : ["A"], ["D", "D2"], ["COMBAT_ASSIGNMENT"]));
        var metadata = ResolutionResult.BuildPrompts(driver.State)["P1"].View!.Metadata!;
        Assert.Equal(2, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(metadata["lethalDamageThreshold"])["D"]);
        var attackers = pooledSources
            ? new CombatDamageAssignmentDto[] { new("A", "D", 1), new("A2", "D", 1), new("A2", "D2", 2) }
            : [new("A", "D", 2), new("A", "D2", 2)];
        await driver.Act("P1", new AssignCombatDamageCommand("battle:BF", "BF", attackers));
        var defenders = pooledSources
            ? new CombatDamageAssignmentDto[] { new("D", "A", 1), new("D", "A2", 4), new("D2", "A2", 2) }
            : [new("D", "A", 5), new("D2", "A", 2)];
        var done = await driver.Act("P2", new AssignCombatDamageCommand("battle:BF", "BF", defenders));
        AssertDamage(done, "D", 8);
        AssertDamage(done, "D2", 2);
        Assert.False(driver.State.BattleState.IsActive);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplicationsExpireAtTurnEndOrWhenTheTargetBecomesANewObject(bool returnToHand)
    {
        var initial = Position();
        if (returnToHand) initial = LocalDestructionRecallTests.At(initial, "D", "P1", "BASE");
        var driver = Driver(initial, returnToHand ? "return-clears" : "turn-end-clears");
        await Cast(driver, "LOTUS1", "UNL-013/219");
        await Cast(driver, "LOTUS2", "UNL-013/219");
        if (returnToHand)
        {
            await Cast(driver, "RETURN", "OGN·104/298");
            Assert.Contains("D", driver.State.PlayerZones["P2"].Hand);
            Assert.True(driver.State.CardObjects["D"].ObjectGeneration > 0);
        }
        else
        {
            await driver.Act("P1", new EndTurnCommand());
            Assert.True(driver.State.TurnNumber > initial.TurnNumber);
        }
        Assert.Empty(Effects(driver.State, "D"));
        await Replay(initial, driver);
    }

    [Fact]
    public async Task PreventedDamageDoesNotConsumeTheDoublingApplications()
    {
        var initial = Position();
        var driver = Driver(initial, "next-damage-prevention");
        await Cast(driver, "LOTUS1", "UNL-013/219");
        await Cast(driver, "LOTUS2", "UNL-013/219");
        await Cast(driver, "PREVENT", "SFD·194/221");
        AssertDamage(await Cast(driver, "RAY1", "OGN·009/298"), "D", 0);
        Assert.DoesNotContain("PREVENT_NEXT_DAMAGE_THIS_TURN", driver.State.CardObjects["D"].UntilEndOfTurnEffects);
        Assert.Equal(2, Effects(driver.State, "D").Length);
        AssertDamage(await Cast(driver, "RAY2", "OGN·009/298"), "D", 12);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task RecallWithinTheFieldPreservesBothApplicationsAndTheIncarnation()
    {
        var initial = AttackDamageDistributionTests.Spell(DestructionOrderTests.Gear(Position()), "DESTROY", "OGN·229/298", "P1");
        var driver = Driver(initial, "recall-preserves");
        await Cast(driver, "LOTUS1", "UNL-013/219");
        await Cast(driver, "LOTUS2", "UNL-013/219");
        await Cast(driver, "DESTROY", "OGN·229/298");
        Assert.Contains("D", driver.State.PlayerZones["P2"].Base);
        Assert.Equal(0, driver.State.CardObjects["D"].ObjectGeneration);
        Assert.Equal(2, Effects(driver.State, "D").Length);
        Assert.Contains("G1", driver.State.PlayerZones["P2"].Graveyard);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task CanonicalButUnrecordedApplicationStillFailsOriginalJournalReplay()
    {
        var initial = Position();
        var driver = Driver(initial, "forged-birth");
        await Cast(driver, "LOTUS1", "UNL-013/219");
        await Cast(driver, "LOTUS2", "UNL-013/219");
        var forged = driver.State with { CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects)
        {
            ["D"] = driver.State.CardObjects["D"] with { UntilEndOfTurnEffects = CanonicalEffects(3) }
        } };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(forged));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged,
            new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch);
        Assert.Contains(replay.Errors, e => e.Contains("final state hash", StringComparison.Ordinal));
        driver.Export("forged-birth.json", new { forged, replay });
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("missing-base")]
    [InlineData("sparse")]
    [InlineData("zero")]
    [InlineData("one")]
    [InlineData("negative")]
    [InlineData("text")]
    [InlineData("leading-zero")]
    [InlineData("too-large")]
    public void ColdValidationRejectsNonCanonicalApplicationInstances(string mutation)
    {
        var state = Position();
        var effects = mutation switch
        {
            "missing-base" => new[] { Double + ":2" },
            "sparse" => [Double, Double + ":3"],
            "zero" => [Double, Double + ":0"],
            "one" => [Double, Double + ":1"],
            "negative" => [Double, Double + ":-1"],
            "text" => [Double, Double + ":abc"],
            "leading-zero" => [Double, Double + ":02"],
            _ => [Double, Double + ":2147483648"]
        };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["D"] = state.CardObjects["D"] with { UntilEndOfTurnEffects = effects }
        } };
        state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state))!;
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), e => e.Contains("invalid damage doubling applications", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DuplicateAndUnorderedInputNormalizesWithoutInventingAnotherApplication()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["D"] = initial.CardObjects["D"] with { UntilEndOfTurnEffects = [Double + ":2", Double, Double + ":2", Double] }
        } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(initial), e => e.Contains("invalid damage doubling applications", StringComparison.Ordinal));
        initial = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(initial))!;
        Assert.Equal(new[] { Double, Double + ":2" }, Effects(initial, "D"));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(initial));
        var driver = Driver(initial, "normalized-duplicate-order");
        AssertDamage(await Cast(driver, "RAY1", "OGN·009/298"), "D", 12);
        await Replay(initial, driver);
    }

    [Fact]
    public void LargeApplicationCountUsesSafeRepeatedCeilingForTheBudget()
    {
        var initial = OfficialPooledCombatDamageTests.Position(true, int.MaxValue, 1, true);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["D1"] = initial.CardObjects["D1"] with { UntilEndOfTurnEffects = CanonicalEffects(32) }
        } };
        var metadata = ResolutionResult.BuildPrompts(initial)["P1"].View!.Metadata!;
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(metadata["lethalDamageThreshold"])["D1"]);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(initial));
    }

    [Theory]
    [InlineData(536870911, 0, false)]
    [InlineData(536870911, 3, false)]
    [InlineData(536870912, 0, true)]
    [InlineData(536870911, 4, true)]
    public async Task ExistingIntDamageBoundaryNeverWrapsAndAnUnsupportedResultRollsBack(int budget, int marked, bool unsupported)
    {
        var initial = BoundaryPosition(budget, marked, 2);
        var driver = Driver(initial, $"numeric-{budget}-{marked}");
        await driver.Act("P1", new AssignCombatDamageCommand("battle:BF", "BF", [new("A1", "D1", budget)]));
        var command = new AssignCombatDamageCommand("battle:BF", "BF", [new("D1", "A1", int.MaxValue)]);
        if (!unsupported)
        {
            var done = await driver.Act("P2", command);
            AssertDamage(done, "D1", 2147483644);
        }
        else
        {
            var before = driver.State;
            var session = new MatchSession(before, new CoreRuleEngine(), NoopMatchJournal.Instance);
            var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            raw["promptId"] = session.PromptFor("P2").PromptId; raw["snapshotTick"] = before.Tick;
            var rejected = await session.SubmitAsync("P2", "numeric-unsupported", command, JsonSerializer.SerializeToElement(raw, Json), default);
            Assert.False(rejected.Accepted);
            Assert.Equal(ErrorCodes.UnsupportedCardBehavior, rejected.ErrorCode);
            Assert.Contains("当前服务端数值范围尚未支持", rejected.ErrorMessage);
            Assert.Empty(rejected.Events);
            Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
            Assert.Equal(before.RngCursor, rejected.State.RngCursor);
            Assert.Equal(before.PriorityPlayerId, rejected.State.PriorityPlayerId);
            Assert.Equal(before.ActivePlayerId, rejected.State.ActivePlayerId);
            Assert.Equal(JsonSerializer.Serialize(before.RunePools, Json), JsonSerializer.Serialize(rejected.State.RunePools, Json));
            Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(before), Json), JsonSerializer.Serialize(rejected.Snapshots, Json));
            Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildPrompts(before), Json), JsonSerializer.Serialize(rejected.Prompts, Json));
            driver.Export("unsupported.json", rejected);
            // The failed computation must not poison a later legal resolution.
            await driver.Act("P2", new SurrenderCommand());
        }
        await Replay(initial, driver);
    }

    [Fact]
    public async Task FullyPreventedDamageDoesNotRequireAnUnrepresentableIntermediateMultiplier()
    {
        var initial = BoundaryPosition(3, 0, 32, defenderPower: 2);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["D1"] = initial.CardObjects["D1"] with { UntilEndOfTurnEffects = [..CanonicalEffects(32), "PREVENT_NEXT_DAMAGE_THIS_TURN"] }
        } };
        var driver = Driver(initial, "numeric-prevented-zero");
        await driver.Act("P1", new AssignCombatDamageCommand("battle:BF", "BF", [new("A1", "D1", 3)]));
        var done = await driver.Act("P2", new AssignCombatDamageCommand("battle:BF", "BF", [new("D1", "A1", 2)]));
        AssertDamage(done, "D1", 0);
        await Replay(initial, driver);
    }

    private static MatchState BoundaryPosition(int budget, int marked, int applications, int defenderPower = int.MaxValue)
    {
        var state = OfficialPooledCombatDamageTests.Position(true, defenderPower);
        var cards = state.CardObjects.Where(e => e.Key != "D2").ToDictionary(e => e.Key, e => e.Value);
        cards["A1"] = cards["A1"] with { Power = budget };
        cards["D1"] = cards["D1"] with { Damage = marked, UntilEndOfTurnEffects = CanonicalEffects(applications) };
        return state with { Tick = 0, CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Battlefields = ["BF", "D1"] } },
            ObjectLocations = state.ObjectLocations.Where(e => e.Key != "D2").ToDictionary(e => e.Key, e => e.Value) };
    }

    private static string[] CanonicalEffects(int applications)
        => applications == 0 ? [] : new[] { Double }.Concat(Enumerable.Range(2, applications - 1).Select(i => Double + ":" + i))
            .Order(StringComparer.Ordinal).ToArray();

    private static MatchState Position()
    {
        var state = OfficialLegendConquestTests.CombatPosition("OGN·269/298");
        state = state with { Tick = 0, CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["BF"] = state.CardObjects["BF"] with { CardNo = "OGN·275/298" },
            ["D"] = state.CardObjects["D"] with { Power = 80 }
        }, RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(30, 30), ["P2"] = new(30, 30) } };
        foreach (var (id, no) in new[] { ("LOTUS1", "UNL-013/219"), ("LOTUS2", "UNL-013/219"), ("LOTUS3", "UNL-013/219"),
            ("RAY1", "OGN·009/298"), ("RAY2", "OGN·009/298"), ("RETURN", "OGN·104/298"), ("PREVENT", "SFD·194/221") })
            state = AttackDamageDistributionTests.Spell(state, id, no, "P1");
        state = AttackDamageDistributionTests.Spell(state, "P2-LOTUS", "UNL-013/219", "P2");
        foreach (var player in new[] { "P1", "P2" })
        {
            var id = player + "-DRAW";
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                [id] = new(id, cardNo: "SFD·125/221", ownerId: player, controllerId: player, power: 2, tags: [CardObjectTags.UnitCard])
            }, PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = state.PlayerZones[player] with { MainDeck = [id] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "MAIN_DECK") } };
        }
        return state;
    }

    private static MatchState AddDefender(MatchState state, int power)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["D2"] = state.CardObjects["D"] with { ObjectId = "D2", Power = power }
        }, PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Battlefields = [..state.PlayerZones["P2"].Battlefields, "D2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["D2"] = new("P2", "BATTLEFIELD", "BF") } };

    private static string[] Effects(MatchState state, string id)
        => state.CardObjects[id].UntilEndOfTurnEffects.Where(e => e == Double || e.StartsWith(Double + ":", StringComparison.Ordinal)).ToArray();

    private static async Task<ResolutionResult> Cast(StandbyDefendObserverTests.Driver driver, string id, string no, string target = "D")
    {
        await driver.Act("P1", new PlayCardCommand(id, no, [target]));
        return await driver.Top();
    }

    private static void AssertDamage(ResolutionResult result, string target, int expected)
        => Assert.Equal(expected, result.Events.Where(e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), target))
            .Sum(e => Convert.ToInt32(e.Payload["damage"])));

    private static StandbyDefendObserverTests.Driver Driver(MatchState initial, string scenario)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_DAMAGE_DOUBLING_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial, root is null ? null : Path.Combine(root, scenario));
        driver.Export("initial.json", initial); return driver;
    }

    private static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var events = driver.Events().Select(e => e with { Event = JsonSerializer.Deserialize<GameEvent>(JsonSerializer.Serialize(e.Event, Json), Json)! }).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        driver.Export("commands.json", driver.Commands()); driver.Export("events.json", events); driver.Export("final.json", driver.State);
        driver.Export("receipt.json", new { commands = driver.Commands().Length, events = events.Length, replay,
            engineAssembly = typeof(CoreRuleEngine).Assembly.Location, engineMvid = typeof(CoreRuleEngine).Assembly.ManifestModule.ModuleVersionId,
            scope = "Explicit damage-replacement fixtures; actual serialized MatchSession intents, cold restore, duplicate intents and JSON journal replay. Numeric model limits remain OPEN; no SQL/API or whole-card certification." });
    }
}
