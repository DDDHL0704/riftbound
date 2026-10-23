using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LocalDestructionReplacementOrderingTests
{
    [Theory]
    [InlineData("SFD·173/221")]
    [InlineData("SFD·239/221")]
    [InlineData("SFD·239*/221")]
    public async Task RecallingSourceBeforeItsLocalGroupUsesItsNewLocation(string printing)
    {
        var initial = ReplacementGroupState(printing);
        var driver = await Open(initial, $"source-recalled-first-{printing}");
        await Select(driver, "GEAR:S:G1");
        var request = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice).Request;
        Assert.Contains(request.Options, o => o.Id == "LOCAL_RECALL:BASE-1:S");
        Assert.Contains(request.Options, o => o.Id == "LOCAL_RECALL:BASE-2:S");
        Assert.DoesNotContain(request.Options, o => o.Id is "LOCAL_RECALL:D:S" or "LOCAL_RECALL:D2:S");

        var before = MatchStateHasher.Hash(driver.State);
        foreach (var (player, option) in new[] { ("P2", "LOCAL_RECALL:D:S"), ("P2", "DECLINE"), ("P1", "LOCAL_RECALL:BASE-1:S") })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(driver.State,
                new("illegal-old-location", player, CommandTypes.PayCost),
                new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]), default);
            Assert.False(rejected.Accepted);
            Assert.Empty(rejected.Events);
            Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        }

        var result = await Select(driver, "LOCAL_RECALL:BASE-2:S");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.All(new[] { "S", "BASE-1", "BASE-2" }, id => Assert.Contains(id, driver.State.PlayerZones["P2"].Base));
        Assert.All(new[] { "D", "D2", "G1" }, id => Assert.Contains(id, driver.State.PlayerZones["P2"].Graveyard));
        Assert.Equal(2, driver.State.DeathLedger.Counts["P2"]);
        Assert.True(Index(result.Events, "UNIT_RECALLED_TO_BASE", "BASE-1") < Index(result.Events, "UNIT_DESTROYED", "D"));
        Assert.True(Index(result.Events, "UNIT_RECALLED_TO_BASE", "BASE-2") < Index(result.Events, "UNIT_DESTROYED", "D2"));
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·173/221")]
    [InlineData("SFD·239/221")]
    [InlineData("SFD·239*/221")]
    public async Task StartedLocalGroupFinishesBeforeAnotherReplacementAndCannotRestartAtBase(string printing)
    {
        var initial = ReplacementGroupState(printing);
        var driver = await Open(initial, $"local-group-first-{printing}");
        await Select(driver, "LOCAL_RECALL:D:S");
        var request = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice).Request;
        Assert.Contains(request.Options, o => o.Id == "GEAR:S:G1");
        Assert.DoesNotContain(request.Options, o => o.Id.StartsWith("LOCAL_RECALL:", StringComparison.Ordinal));

        var result = await Select(driver, "GEAR:S:G1");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.All(new[] { "S", "D", "D2" }, id => Assert.Contains(id, driver.State.PlayerZones["P2"].Base));
        Assert.All(new[] { "BASE-1", "BASE-2", "G1" }, id => Assert.Contains(id, driver.State.PlayerZones["P2"].Graveyard));
        Assert.Equal(2, driver.State.DeathLedger.Counts["P2"]);
        Assert.True(Index(result.Events, "UNIT_RECALLED_TO_BASE", "D") < Index(result.Events, "EQUIPMENT_DESTROYED", "G1"));
        Assert.True(Index(result.Events, "UNIT_RECALLED_TO_BASE", "D2") < Index(result.Events, "EQUIPMENT_DESTROYED", "G1"));
        await Replay(initial, driver);
    }

    [Fact]
    public async Task RecallCanEnableAnUnstartedLocalGroupWithoutOriginalEligibleTargets()
    {
        var initial = ReplacementGroupState(onlySourceAtBattlefield: true);
        var driver = await Open(initial, "no-original-eligible-target");
        await Select(driver, "GEAR:S:G1");
        var request = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice).Request;
        Assert.Equal(new[] { "LOCAL_RECALL:BASE-1:S", "LOCAL_RECALL:BASE-2:S" }, request.Options.Select(o => o.Id).ToArray());
        await Select(driver, "LOCAL_RECALL:BASE-1:S");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.All(new[] { "S", "BASE-1", "BASE-2" }, id => Assert.Contains(id, driver.State.PlayerZones["P2"].Base));
        Assert.DoesNotContain("P2", driver.State.DeathLedger.Counts.Keys);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task ColdValidationRejectsForgedOldLocationReplacementOptions()
    {
        var initial = ReplacementGroupState();
        var driver = await Open(initial, "forged-old-location");
        await Select(driver, "GEAR:S:G1");
        var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.DoesNotContain(pending.Request.Options, o => o.Id == "LOCAL_RECALL:D:S");
        var forged = driver.State with
        {
            PendingRuleChoice = pending with
            {
                Request = pending.Request with
                {
                    Options = [..pending.Request.Options, new("LOCAL_RECALL:D:S", "S recalls D at its old battlefield", ["D", "S"])]
                }
            }
        };
        var roundTrip = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(forged))!;
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(roundTrip), e => e == "invalid replacement choice continuation");
        await Select(driver, "LOCAL_RECALL:BASE-1:S");
        await Replay(initial, driver);
    }

    private static MatchState ReplacementGroupState(string printing = "SFD·173/221", bool onlySourceAtBattlefield = false)
    {
        var state = DestructionOrderTests.Gear(LocalDestructionRecallTests.PositionFor(true, printing));
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        cards["AOE"] = cards["AOE"] with { CardNo = "UNL-180/219" };
        foreach (var id in new[] { "BASE-1", "BASE-2" })
        {
            cards[id] = new(id, power: 1, cardNo: "UNL-008/219", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]);
            locations[id] = new("P2", "BASE");
        }
        if (onlySourceAtBattlefield)
            foreach (var id in new[] { "D", "D2" }) { cards.Remove(id); locations.Remove(id); }
        return state with
        {
            Tick = 0, Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P2"] = state.PlayerZones["P2"] with
                {
                    Base = [..state.PlayerZones["P2"].Base, "BASE-1", "BASE-2"],
                    Battlefields = state.PlayerZones["P2"].Battlefields.Where(id => !onlySourceAtBattlefield || id is not ("D" or "D2")).ToArray()
                }
            }
        };
    }

    private static async Task<StandbyDefendObserverTests.Driver> Open(MatchState initial, string scenario)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_LOCAL_REPLACEMENT_ORDER_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial,
            root is null ? null : Path.Combine(root, scenario.Replace('/', '-').Replace('*', 's')));
        driver.Export("initial.json", initial);
        await driver.Act("P1", new PlayCardCommand("AOE", "UNL-180/219", []));
        await driver.Top();
        Assert.NotNull(driver.State.PendingRuleChoice);
        return driver;
    }

    private static Task<ResolutionResult> Select(StandbyDefendObserverTests.Driver driver, string option)
    {
        var request = driver.State.PendingRuleChoice!.Request;
        return driver.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
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
        Assert.Equal(MatchStateHasher.Hash(driver.State), replay.ReplayedStateHash);
        driver.Export("commands.json", driver.Commands());
        driver.Export("events.json", events);
        driver.Export("final.json", driver.State);
        driver.Export("receipt.json", new { commands = driver.Commands().Length, events = events.Length, replay,
            finalHash = MatchStateHasher.Hash(driver.State), engineAssembly = typeof(CoreRuleEngine).Assembly.Location,
            engineMvid = typeof(CoreRuleEngine).Assembly.ManifestModule.ModuleVersionId,
            scope = "Explicit simultaneous destruction fixture; actual serialized intents, cold restores, duplicate intents and original journal JSON replay. Not SQL/API or whole-card certification." });
    }

    private static int Index(IReadOnlyList<GameEvent> events, string kind, string target)
    {
        for (var i = 0; i < events.Count; i++)
            if (events[i].Kind == kind && Equals(events[i].Payload.GetValueOrDefault("targetObjectId"), target)) return i;
        Assert.Fail($"Missing {kind} for {target}.");
        return -1;
    }
}
