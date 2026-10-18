using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class MultiControllerTriggerOrderingTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AControllerCannotChooseTheOtherControllersSimultaneousSkillOrder(bool reverseOwn)
    {
        var (initial, driver) = await Open();
        await BanishEffectPlayTests.Replay(initial, driver);
        var state = driver.State;
        var ids = DefaultOrder(state, "P1");
        if (reverseOwn) ReverseController(state, ids, "P1");
        ReverseController(state, ids, "P2");
        var command = new OrderTriggersCommand(ids);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("change-other-controller-order", "P1", command.CmdType), command, default);
        output.WriteLine("MULTI_CONTROLLER_ORDER_RED " + JsonSerializer.Serialize(new {
            reverseOwn, actualRuleInputs = state.RuleInputs, originalPrefixReplayPassed = true,
            explicitMidgameNotBlankOpening = true, otherControllerOrderAccepted = result.Accepted,
            controllers = state.TriggerQueue.Select(t => new { t.ControllerId, t.TriggerId, t.EnqueuedAtTick })
        }));
        Assert.False(result.Accepted, "P1 cannot choose the order of P2's simultaneous skills.");
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EachControllerMustChooseBeforeTheCombinedQueueMovesToTheStack(bool reverseFirst)
        => CompleteBoth("P1", reverseFirst);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TheOtherTurnPlayerAlsoChoosesBeforeTheNonTurnController(bool reverseFirst)
        => CompleteBoth("P2", reverseFirst);

    private async Task CompleteBoth(string firstPlayer, bool reverseFirst)
    {
        var secondPlayer = firstPlayer == "P1" ? "P2" : "P1";
        var branch = firstPlayer + "-" + reverseFirst;
        var (initial, driver) = await Open(firstPlayer);
        await BanishEffectPlayTests.Replay(initial, driver);
        ExportPrompt(driver.State, firstPlayer, branch + "-first-prompt.json");
        var firstOrder = DefaultOrder(driver.State, firstPlayer);
        if (reverseFirst) ReverseController(driver.State, firstOrder, firstPlayer);
        await driver.Act(firstPlayer, NativeOrder(driver.State, firstPlayer, firstOrder, branch + "-first-command.json"));
        output.WriteLine("MULTI_CONTROLLER_HANDOFF_RED " + JsonSerializer.Serialize(new {
            reverseFirst, actualRuleInputs = driver.State.RuleInputs,
            queuedAfterFirstChoice = driver.State.TriggerQueue.Count,
            stackAfterFirstChoice = driver.State.StackItems.Count
        }));
        Assert.Equal(4, driver.State.TriggerQueue.Count);
        Assert.Empty(driver.State.StackItems);
        Assert.False(HasEnabledOrder(driver.State, firstPlayer));
        Assert.True(HasEnabledOrder(driver.State, secondPlayer));
        Assert.All(driver.State.TriggerQueue, trigger =>
            Assert.Equal(trigger.ControllerId == firstPlayer, trigger.ControllerOrderConfirmed));
        var pending = driver.State;
        ExportPrompt(pending, secondPlayer, branch + "-second-prompt.json");
        var secondOrder = DefaultOrder(driver.State, secondPlayer);
        var foreignOrder = secondOrder.ToArray();
        ReverseController(pending, foreignOrder, firstPlayer);
        foreach (var (player, ids) in new[] { (firstPlayer, secondOrder), (secondPlayer, foreignOrder) })
        {
            var command = new OrderTriggersCommand(ids);
            var rejected = await new CoreRuleEngine().ResolveAsync(pending,
                new("cannot-replace-confirmed-order", player, command.CmdType), command, default);
            Assert.False(rejected.Accepted);
            Assert.Equal(MatchStateHasher.Hash(pending), MatchStateHasher.Hash(rejected.State));
        }
        ReverseController(driver.State, secondOrder, secondPlayer);
        await driver.Act(secondPlayer, NativeOrder(driver.State, secondPlayer, secondOrder, branch + "-second-command.json"));
        Assert.Empty(driver.State.TriggerQueue);
        Assert.Equal(secondOrder.Select(id => "ordered-" + id), driver.State.StackItems.Reverse().Select(i => i.StackItemId));
        while (driver.State.StackItems.Count > 0) await driver.Top();
        foreach (var player in new[] { "P1", "P2" })
        {
            Assert.Equal(1, driver.State.PlayerExperience[player]);
            Assert.Single(driver.State.PlayerZones[player].Base,
                id => driver.State.CardObjects[id].TokenFactoryCardNo is not null);
        }
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("partial-controller")]
    [InlineData("wrong-first-controller")]
    [InlineData("all-confirmed")]
    [InlineData("lost-simultaneous-partner")]
    [InlineData("confirmation-before-enqueue")]
    public async Task IndependentRecoveryRejectsForgedControllerConfirmationProgress(string mutation)
    {
        var (_, driver) = await Open();
        await driver.Act("P1", new OrderTriggersCommand(DefaultOrder(driver.State, "P1")));
        var state = driver.State;
        var first = state.TriggerQueue.First(t => t.ControllerId == "P1").TriggerId;
        var queue = state.TriggerQueue.Select(t => mutation switch {
            "partial-controller" when t.TriggerId == first => t with { ControllerOrderConfirmed = false },
            "wrong-first-controller" => t with { ControllerOrderConfirmed = t.ControllerId == "P2" },
            "all-confirmed" => t with { ControllerOrderConfirmed = true },
            "confirmation-before-enqueue" => t with { EnqueuedAtTick = state.Tick },
            _ => t
        }).Where(t => mutation != "lost-simultaneous-partner" || t.TriggerId != first).ToArray();
        var forged = state with { TriggerQueue = queue };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged),
            error => error.Contains("trigger controller order confirmations"));
        var command = new OrderTriggersCommand(queue.Select(t => t.TriggerId).ToArray());
        var rejected = await new CoreRuleEngine().ResolveAsync(forged,
            new("forged-confirmed-order", "P2", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(forged), MatchStateHasher.Hash(rejected.State));
    }

    private static bool HasEnabledOrder(MatchState state, string player) =>
        ResolutionResult.BuildPrompts(state)[player].Candidates!
            .Any(c => c.Action == CommandTypes.OrderTriggers && c.Enabled);

    private static string[] DefaultOrder(MatchState state, string player)
    {
        var prompt = JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(state)[player],
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var metadata = prompt.GetProperty("candidates").EnumerateArray()
            .Single(c => c.GetProperty("action").GetString() == CommandTypes.OrderTriggers && c.GetProperty("enabled").GetBoolean())
            .GetProperty("metadata");
        Assert.Equal(state.TriggerQueue.Where(t => t.ControllerId == player).Select(t => t.TriggerId).Order(),
            metadata.GetProperty("reorderableTriggerIds").EnumerateArray().Select(v => v.GetString()!).Order());
        return metadata.GetProperty("orderedTriggerIds").EnumerateArray()
            .Select(id => id.GetString()!).ToArray();
    }

    private static void ExportPrompt(MatchState state, string player, string name)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_MULTI_TRIGGER_NATIVE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state)[player],
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private static OrderTriggersCommand NativeOrder(MatchState state, string player, string[] ids, string name)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_MULTI_TRIGGER_NATIVE") is not { Length: > 0 } root
            || !File.Exists(Path.Combine(root, name))) return new(ids);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name)));
        Assert.Equal(state.Tick, document.RootElement.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(ResolutionResult.BuildPrompts(state)[player].PromptId, document.RootElement.GetProperty("promptId").GetString());
        var command = Assert.IsType<OrderTriggersCommand>(GameCommandJsonMapper.Map(document.RootElement));
        Assert.Equal(ids, command.OrderedTriggerIds);
        Assert.Equal(ids, command.TriggerIds);
        return command;
    }

    private static void ReverseController(MatchState state, string[] ids, string controller)
    {
        var own = state.TriggerQueue.Where(t => t.ControllerId == controller).Select(t => t.TriggerId).ToHashSet();
        var positions = Enumerable.Range(0, ids.Length).Where(i => own.Contains(ids[i])).ToArray();
        Assert.Equal(2, positions.Length);
        (ids[positions[0]], ids[positions[1]]) = (ids[positions[1]], ids[positions[0]]);
    }

    private static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Driver)> Open(string firstPlayer = "P1")
    {
        var state = DeathObserverAuditTests.NoReplacement(two: true);
        foreach (var player in new[] { "P1", "P2" })
        {
            state = DeathObserverAuditTests.Source(state, "ARC-006/006", player + "-VIKTOR", player);
            state = DeathObserverAuditTests.Source(state, "UNL-129/219", player + "-JAW", player);
        }
        state = state with {
            RoomId = "multi-controller-trigger-order", Tick = 0,
            CardObjects = state.CardObjects.Where(p => p.Key != "LEGEND").ToDictionary(p => p.Key, p => p.Value)
                .Concat(new Dictionary<string, CardObjectState> {
                    ["P1BF"] = new("P1BF", cardNo: "OGN·296/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1", controllerId: "P1")
                }).ToDictionary(p => p.Key, p => p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Battlefields = ["P1BF", "D2"] },
                ["P2"] = state.PlayerZones["P2"] with { LegendZone = [], Battlefields = ["BF", "D"] }
            },
            ObjectLocations = state.ObjectLocations.Where(p => p.Key != "LEGEND").ToDictionary(p => p.Key, p => p.Value)
        };
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["D2"] = state.CardObjects["D2"] with { OwnerId = "P1", ControllerId = "P1" }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["P1BF"] = new("P1", "BATTLEFIELD", "P1BF"), ["D2"] = new("P1", "BATTLEFIELD", "P1BF")
            }
        };
        if (firstPlayer == "P2") state = state with {
            ActivePlayerId = "P2", TurnPlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["AOE"] = state.CardObjects["AOE"] with { OwnerId = "P2", ControllerId = "P2" }
            },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [] },
                ["P2"] = state.PlayerZones["P2"] with { Hand = [..state.PlayerZones["P2"].Hand, "AOE"] }
            },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(20, 20) }
        };
        OfficialGraveyardRecastTests.Restore(state);
        var driver = new StandbyDefendObserverTests.Driver(state);
        await driver.Act(firstPlayer, new PlayCardCommand("AOE", "OGN·133/298", []));
        await driver.Top();
        Assert.Equal(4, driver.State.TriggerQueue.Count);
        Assert.Single(driver.State.TriggerQueue.Select(t => t.EnqueuedAtTick).Distinct());
        Assert.All(new[] { "P1", "P2" }, player => Assert.Equal(2, driver.State.TriggerQueue.Count(t => t.ControllerId == player)));
        return (state, driver);
    }
}
