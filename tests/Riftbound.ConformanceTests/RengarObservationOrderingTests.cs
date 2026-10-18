using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class RengarObservationOrderingTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SequentialObservedNocturnesFinishTheirPermissionsBeforeLegendSkillsConfirmInEarnedOrder()
    {
        var initial = RengarUnitPlayConfirmationTests.WithLegend(DeckObservationPlayTests.WithNocturne(
            DeckObservationPlayTests.WithNocturne(CandlelitSanctumTests.Position(), "DRAW1", "P1"), "DRAW2", "P1")) with { Tick = 0 };
        var driver = Driver(initial, "sanctum-two-nocturnes");
        await CandlelitSanctumTests.Conquer(driver);
        await driver.Top();
        await DeckObservationPlayTests.Decide(driver, true);
        await DeckObservationPlayTests.Decide(driver, true);
        foreach (var id in new[] { "DRAW1", "DRAW2" })
        {
            Assert.Equal(id, driver.State.PendingEffectPlay!.Sources.Keys.Single());
            Assert.Null(driver.State.PendingCardChoice);
            await driver.Act("P1", new PlayCardCommand(id, DeckObservationPlayTests.Nocturne, []));
            Assert.Contains(id, driver.State.PlayerZones["P1"].Base);
        }
        Assert.Equal(8, driver.State.RunePools["P1"].Power);
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARDS_REVEALED");
        await AssertSequentialLegendConfirmations(initial, driver, ["DRAW1", "DRAW2"]);
    }

    [Fact]
    public async Task BrightFutureKeepsAllOriginalPendingCardsBeforeLegendSkillsEarnedByDifferentConfirmations()
    {
        var (initial, driver, selectedFirst, _) = await BrightFuturePrefix();
        await AssertSequentialLegendConfirmations(initial, driver, ["P1_TOP", selectedFirst]);
    }

    [Fact]
    public async Task BothPlayersLegendSkillsKeepEarnedChronologyInsteadOfOneGlobalApnapBlock()
    {
        var (initial, driver, first, second) = await BrightFuturePrefix(bothLegends: true);
        await AssertSequentialLegendConfirmations(initial, driver,
            ["P1_TOP", "P2_TOP", second, first], ["P1", "P2", "P2", "P1"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ALaterSimultaneousOwnAndLegendGroupCannotMoveBeforeAnEarlierLegendSkill(bool legendFirst)
    {
        var (initial, driver, selected, _) = await BrightFuturePrefix(merfolk: true, branch: "mixed-late-" + legendFirst);
        await BanishEffectPlayTests.Replay(initial, driver);
        var state = driver.State;
        var early = state.TriggerQueue.Single(t => t.UnitPlayMight?.PlayedUnit.ObjectId == "P1_TOP");
        var late = state.TriggerQueue.Where(t => t.TriggerId != early.TriggerId)
            .OrderBy(t => (t.UnitPlayMight is not null) == legendFirst ? 0 : 1).ToArray();
        Assert.Equal(2, late.Length);
        Assert.All(late, t => Assert.True(t.EnqueuedAtTick > early.EnqueuedAtTick));
        var wrong = new OrderTriggersCommand(new[] { early.TriggerId }.Concat(late.Select(t => t.TriggerId)).ToArray());
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("bad-cross-time-block", "P1", wrong.CmdType), wrong, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        driver.Export("actual-chronology-mixed-prompt.json", ResolutionResult.BuildPrompts(state)["P1"]);
        var prompt = JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(state)["P1"],
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var metadata = prompt.GetProperty("candidates").EnumerateArray()
            .Single(c => c.GetProperty("action").GetString() == "ORDER_TRIGGERS").GetProperty("metadata");
        var defaults = metadata.GetProperty("orderedTriggerIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
        var defaultCommand = new OrderTriggersCommand(defaults);
        var defaultResult = await new CoreRuleEngine().ResolveAsync(state,
            new("server-default-chronology", "P1", defaultCommand.CmdType), defaultCommand, default);
        output.WriteLine("CHRONOLOGY_PROMPT_DEFAULT " + JsonSerializer.Serialize(new {
            legendFirst, defaultAccepted = defaultResult.Accepted, defaults,
            blocks = metadata.GetProperty("legalOrderingConstraints").GetProperty("legalResolutionControllerBlockOrder")
        }));
        Assert.True(defaultResult.Accepted, "The server-provided default trigger order must obey its own chronology checks.");
        Assert.Equal(early.TriggerId, defaults[^1]);
        var blocks = metadata.GetProperty("legalOrderingConstraints").GetProperty("legalResolutionControllerBlockOrder");
        Assert.Equal(2, blocks.GetArrayLength());
        Assert.Equal(2, blocks[0].GetProperty("triggerIds").GetArrayLength());
        Assert.Equal(early.TriggerId, blocks[1].GetProperty("triggerIds")[0].GetString());
        var correct = new OrderTriggersCommand(late.Select(t => t.TriggerId).Append(early.TriggerId).ToArray());
        await driver.Act("P1", correct);
        var firstChoice = driver.State.PendingCardChoice!;
        Assert.Equal("P1_TOP", driver.State.StackItems.Single(i => i.StackItemId == firstChoice.ResolvingStackItemId)
            .UnitPlayMight!.PlayedUnit.ObjectId);
        var powers = new[] { "P1_TOP", selected }.ToDictionary(id => id, id => state.CardObjects[id].Power);
        while (driver.State.PendingCardChoice is { } choice)
        {
            var item = driver.State.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
            Assert.NotNull(item.UnitPlayMight);
            await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                [item.UnitPlayMight!.PlayedUnit.ObjectId]));
        }
        while (driver.State.StackItems.Count > 0) await driver.Top();
        foreach (var id in powers.Keys) Assert.Equal(powers[id] + 1, driver.State.CardObjects[id].Power);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyTheControllerWithASimultaneousChoiceMayOrderItsSkills(bool legendFirst)
    {
        var (initial, driver, _, second) = await BrightFuturePrefix(bothLegends: true, merfolk: true,
            branch: "p2-choice-owner-" + legendFirst, merfolkPlayer: "P2");
        await BanishEffectPlayTests.Replay(initial, driver);
        var state = driver.State;
        var pair = state.TriggerQueue.Where(t => t.ControllerId == "P2" && t.EnqueuedAtTick == 10).ToArray();
        Assert.Equal(2, pair.Length);
        Assert.All(state.TriggerQueue.Where(t => t.ControllerId == "P1"),
            t => Assert.Single(state.TriggerQueue, other => other.EnqueuedAtTick == t.EnqueuedAtTick));
        var prompts = ResolutionResult.BuildPrompts(state);
        var projected = JsonSerializer.SerializeToElement(prompts, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var candidate = projected.EnumerateObject().SelectMany(p => p.Value.GetProperty("candidates").EnumerateArray())
            .First(c => c.GetProperty("action").GetString() == "ORDER_TRIGGERS" && c.GetProperty("enabled").GetBoolean());
        var ids = candidate.GetProperty("metadata").GetProperty("orderedTriggerIds").EnumerateArray()
            .Select(v => v.GetString()!).ToArray();
        var orderedPair = pair.OrderBy(t => (t.UnitPlayMight is not null) == legendFirst ? 0 : 1).ToArray();
        var start = Array.FindIndex(ids, id => pair.Any(t => t.TriggerId == id));
        ids[start] = orderedPair[0].TriggerId; ids[start + 1] = orderedPair[1].TriggerId;
        var command = new OrderTriggersCommand(ids);
        var engine = new CoreRuleEngine();
        var wrong = await engine.ResolveAsync(state, new("opponent-chooses-p2-order", "P1", command.CmdType), command, default);
        var owned = await engine.ResolveAsync(state, new("owner-chooses-p2-order", "P2", command.CmdType), command, default);
        output.WriteLine("TRIGGER_CHOICE_OWNER " + JsonSerializer.Serialize(new {
            legendFirst, originalPrefixFullReplayPassed = true, explicitMidgameReachabilityNotCertified = true,
            turnPlayer = state.TurnPlayerId, choosingController = "P2", pairTicks = pair.Select(t => t.EnqueuedAtTick),
            wrongActorAccepted = wrong.Accepted, ownerAccepted = owned.Accepted, ownerError = owned.ErrorMessage,
            actualRuleInputs = state.RuleInputs
        }));
        driver.Export("p2-choice-owner-prefix.json", new { initial, state, commands = driver.Commands(), events = driver.Events() });
        Assert.False(wrong.Accepted, "P1 has no simultaneous choice and cannot choose P2's own/legend order.");
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(wrong.State));
        Assert.True(owned.Accepted, owned.ErrorMessage);
        Assert.DoesNotContain(projected.GetProperty("P1").GetProperty("candidates").EnumerateArray(),
            c => c.GetProperty("action").GetString() == "ORDER_TRIGGERS" && c.GetProperty("enabled").GetBoolean());
        var ownedCandidate = Assert.Single(projected.GetProperty("P2").GetProperty("candidates").EnumerateArray(),
            c => c.GetProperty("action").GetString() == "ORDER_TRIGGERS" && c.GetProperty("enabled").GetBoolean());
        Assert.Equal("P2", ownedCandidate.GetProperty("metadata").GetProperty("orderingPlayerId").GetString());
        await driver.Act("P2", command);
        while (driver.State.PendingCardChoice is { } choice)
        {
            var item = driver.State.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
            Assert.NotNull(item.UnitPlayMight);
            await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                [item.UnitPlayMight!.PlayedUnit.ObjectId]));
        }
        while (driver.State.StackItems.Count > 0) await driver.Top();
        Assert.Equal(state.CardObjects[second].Power + 1, driver.State.CardObjects[second].Power);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    private static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Driver, string First, string Second)>
        BrightFuturePrefix(bool bothLegends = false, bool merfolk = false, string? branch = null, string merfolkPlayer = "P1")
    {
        var initial = RengarUnitPlayConfirmationTests.WithLegend(DeckPlaySequenceTests.TopCard(
            DeckPlaySequenceTests.TopCard(DeckPlaySequenceTests.Position(DeckPlaySequenceTests.BrightFuture),
                "P1_TOP", DeckObservationPlayTests.Nocturne), "P2_TOP", DeckObservationPlayTests.Nocturne)) with { Tick = 0 };
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(0, 1) } };
        if (bothLegends)
        {
            Assert.Empty(initial.PlayerZones["P2"].LegendZone);
            initial = initial with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["OTHER_LEGEND"] = new("OTHER_LEGEND", cardNo: "UNL-183/219", ownerId: "P2", controllerId: "P2") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P2"] = initial.PlayerZones["P2"] with { LegendZone = ["OTHER_LEGEND"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                    ["OTHER_LEGEND"] = new("P2", "LEGEND") }
            };
        }
        if (merfolk)
        {
            var merfolkId = merfolkPlayer == "P1" ? "D0"
                : initial.PlayerZones["P2"].MainDeck.First(id => id != "P2_TOP");
            initial = DeckPlaySequenceTests.TopCard(initial, merfolkId, "UNL-003/219");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["F"] = initial.CardObjects["F"] with { ControllerId = merfolkPlayer } } };
        }
        var driver = Driver(initial, branch ?? (bothLegends ? "bright-future-both-legends" : "bright-future-one-legend"));
        await driver.Act("P1", new PlayCardCommand("PARENT", DeckPlaySequenceTests.BrightFuture, []));
        await driver.Top();
        await DeckObservationPlayTests.Decide(driver, true);
        var first = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("P1", first.PlayerId);
        var selectedFirst = first.LegalObjectIds.First();
        await driver.Act("P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, [selectedFirst]));
        Assert.Equal("P2", driver.State.PendingRuleChoice!.Request.PlayerId);
        await DeckObservationPlayTests.Decide(driver, true);
        var selectedSecond = "";
        while (driver.State.PendingCardChoice is { } choice)
        {
            Assert.Null(driver.State.PendingEffectPlay);
            selectedSecond = choice.LegalObjectIds.First();
            await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [selectedSecond]));
        }
        Assert.Contains("PARENT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new[] { "P2", "P1" }, driver.State.PendingEffectPlay!.Parent.ObservedPlayPermission!.Suspended!
            .Parent.DeckPlaySequence!.Queue.Select(c => c.OwnerId));
        foreach (var player in new[] { "P1", "P2" })
        {
            Assert.Equal(player, driver.State.PendingEffectPlay!.PlayerId);
            await driver.Act(player, new PlayCardCommand(player + "_TOP", DeckObservationPlayTests.Nocturne, []));
            await RejectOrderingWhileOriginalPermissionRemains(driver);
        }
        foreach (var player in new[] { "P2", "P1" })
        {
            var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
            Assert.Equal(player, pending.PlayerId);
            var id = pending.Sources.Keys.Single();
            await driver.Act(player, new PlayCardCommand(id, driver.State.CardObjects[id].CardNo!, [],
                Destination: merfolk && player == merfolkPlayer ? "BATTLEFIELD:F" : "BASE"));
            await RejectOrderingWhileOriginalPermissionRemains(driver);
        }
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Contains(CardObjectTags.UnitCard, driver.State.CardObjects[selectedFirst].Tags);
        return (initial, driver, selectedFirst, selectedSecond);
    }

    private static async Task RejectOrderingWhileOriginalPermissionRemains(StandbyDefendObserverTests.Driver driver)
    {
        if (driver.State.PendingEffectPlay is null) return;
        var state = driver.State;
        var command = new OrderTriggersCommand(state.TriggerQueue.Select(t => t.TriggerId).ToArray());
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-before-pending-finished", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private async Task AssertSequentialLegendConfirmations(MatchState initial, StandbyDefendObserverTests.Driver driver,
        string[] playedOrder, string[]? controllers = null)
    {
        await BanishEffectPlayTests.Replay(initial, driver);
        var state = driver.State;
        var earned = state.TriggerQueue.Where(t => t.UnitPlayMight is not null).Select(t => t.UnitPlayMight!)
            .Concat(state.StackItems.Where(i => i.UnitPlayMight is not null).Select(i => i.UnitPlayMight!))
            .OrderBy(c => c.CapturedTick).ToArray();
        Assert.Equal(playedOrder, earned.Select(c => c.PlayedUnit.ObjectId));
        Assert.True(earned.Zip(earned.Skip(1)).All(pair => pair.First.CapturedTick < pair.Second.CapturedTick));
        var ids = driver.Events().Where(e => e.Event.Kind == "TRIGGER_QUEUED"
                && e.Event.Payload.GetValueOrDefault("effectKind") as string == "UNIT_PLAYED_POWER_PLUS_1")
            .Select(e => (string)e.Event.Payload["triggerId"]!).ToArray();
        Assert.Equal(playedOrder.Length, ids.Length);
        // ORDER_TRIGGERS lists resolution order. Keeping capture order here
        // would reverse the required confirmation chronology.
        var command = new OrderTriggersCommand(ids);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("bad-distinct-trigger-order", "P1", command.CmdType), command, default);
        output.WriteLine("DISTINCT_TRIGGER_CHRONOLOGY " + JsonSerializer.Serialize(new {
            playedOrder, capturedTicks = earned.Select(c => c.CapturedTick),
            actualReorderingAccepted = rejected.Accepted,
            queuedLegendCount = state.TriggerQueue.Count(t => t.UnitPlayMight is not null),
            stackLegendCount = state.StackItems.Count(i => i.UnitPlayMight is not null),
            pendingChoice = state.PendingCardChoice?.ChoiceWindow,
            attemptedNextConfirmationPlayedUnit = rejected.State.PendingCardChoice is { } attemptedChoice
                ? rejected.State.StackItems.Single(i => i.StackItemId == attemptedChoice.ResolvingStackItemId).UnitPlayMight?.PlayedUnit.ObjectId : null,
            actualRuleInputs = state.RuleInputs,
            originalPrefixFullReplayPassed = true
        }));
        driver.Export("distinct-trigger-prefix.json", new { initial, state, commands = driver.Commands(), events = driver.Events() });
        Assert.False(rejected.Accepted, "Different accepted unit confirmations earned these skills at distinct times; they are not a simultaneous ordering choice.");
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        var powers = playedOrder.ToDictionary(id => id, id => state.CardObjects[id].Power);
        controllers ??= playedOrder.Select(_ => "P1").ToArray();
        foreach (var (played, controller) in playedOrder.Zip(controllers))
        {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            var item = driver.State.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
            Assert.Equal(played, item.UnitPlayMight!.PlayedUnit.ObjectId);
            Assert.Equal(controller, choice.PlayerId);
            Assert.Equal(1, choice.RequiredCount);
            Assert.Null(driver.State.PriorityPlayerId);
            await driver.Act(controller, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [played]));
        }
        while (driver.State.StackItems.Count > 0) await driver.Top();
        foreach (var played in playedOrder) Assert.Equal(powers[played] + 1, driver.State.CardObjects[played].Power);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    private static StandbyDefendObserverTests.Driver Driver(MatchState initial, string branch)
    {
        var directory = Environment.GetEnvironmentVariable("RIFTBOUND_OBSERVED_RENGAR_EVIDENCE");
        return new(initial, string.IsNullOrWhiteSpace(directory) ? null : Path.Combine(directory, branch));
    }
}
