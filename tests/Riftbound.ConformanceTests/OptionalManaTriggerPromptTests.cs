using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OptionalManaTriggerPromptTests
{
    [Theory]
    [InlineData("vayne", false)] [InlineData("vayne", true)]
    [InlineData("gold", false)] [InlineData("gold", true)]
    [InlineData("legend", false)] [InlineData("legend", true)]
    [InlineData("sand", false)] [InlineData("sand", true)]
    [InlineData("icevale", false)] [InlineData("icevale", true)]
    public async Task ActualOptionalManaTriggerChoicesColdRecoverAndReplay(string branch, bool decline)
    {
        var initial = Position(branch);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_OPTIONAL_MANA_TRIGGER_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch + (decline ? "-decline" : "-pay"));
        var driver = await Open(initial, branch, dir);
        var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
        Assert.Equal("TRIGGER_PAYMENT", payment.PaymentWindow);
        Assert.Equal(new[] { "SPEND_MANA:1", "DECLINE" }, payment.LegalPaymentChoiceIds);
        var prompt = ResolutionResult.BuildPrompts(driver.State)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PayCost);
        var step = Assert.Single(candidate.SelectionSteps ?? []);
        Assert.Equal("target", step.Role); Assert.True(step.Required);
        Assert.Equal(new[] { "SPEND_MANA:1", "DECLINE" }, step.Choices.Select(c => c.Id));
        Assert.Equal(new[] { "支付 1 法力", "放弃此效果" }, step.Choices.Select(c => c.Label));
        var binding = Assert.Single(candidate.CommandTemplate!.Bindings, b => b.Field == "paymentChoiceIds");
        Assert.Equal("selectedTargets", binding.Source); Assert.True(binding.Required);
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(driver.State)["P2"].Candidates ?? [],
            c => c.Action == CommandTypes.PayCost && c.Enabled);
        var payStep = "defend-step-" + driver.Commands().Length;
        var done = await driver.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
            [decline ? "DECLINE" : "SPEND_MANA:1"]));
        Assert.Null(driver.State.PendingPayment);
        Assert.Equal(decline ? 1 : 0, driver.State.RunePools["P1"].Mana);
        Assert.Contains(done.Events, e => e.Kind == (decline ? "TRIGGER_PAYMENT_DECLINED" : "COST_PAID"));
        if (branch == "vayne" && !decline)
        {
            Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Battlefields);
            Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_RETURNED_TO_HAND");
            Assert.NotNull(driver.State.PriorityPlayerId);
            await driver.Top();
        }
        if (branch is "vayne" or "sand")
            Assert.Contains("UNIT", decline ? driver.State.PlayerZones["P1"].Battlefields : driver.State.PlayerZones["P1"].Hand);
        if (branch == "legend") Assert.Equal(decline, driver.State.CardObjects["LEGEND"].IsExhausted);
        if (branch == "gold") Assert.Equal(decline ? 0 : 1,
            driver.State.PlayerZones["P1"].Base.Count(id => driver.State.CardObjects[id].Tags.Contains("金币")));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.True(driver.NativeCommands is 0 or 1);
        driver.Export("steps.json", new[] { payStep });
        driver.Export("receipt.json", new { branch, decline, nativeCommands = driver.NativeCommands,
            commands = driver.Commands().Length, events = driver.Events().Length, coldRecoveries = driver.Commands().Length, replay });
    }

    [Theory]
    [InlineData("payment-id")] [InlineData("window")] [InlineData("other-player")]
    [InlineData("alias-pay")] [InlineData("duplicate")] [InlineData("both-branches")]
    public async Task OptionalManaTriggerStillRejectsForgedDecisionsWithoutMutation(string change)
    {
        var driver = await Open(Position("vayne"), "vayne", null);
        var before = driver.State;
        var payment = before.PendingPayment!;
        var choices = change switch { "alias-pay" => new[] { "PAY" },
            "duplicate" => new[] { "SPEND_MANA:1", "SPEND_MANA:1" },
            "both-branches" => new[] { "SPEND_MANA:1", "DECLINE" }, _ => new[] { "SPEND_MANA:1" } };
        var command = new PayCostCommand(change == "payment-id" ? "FORGED" : payment.PaymentId,
            change == "window" ? "FORGED" : payment.PaymentWindow, choices);
        var rejected = await new CoreRuleEngine().ResolveAsync(before,
            new("forged-decision", change == "other-player" ? "P2" : "P1", CommandTypes.PayCost), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
    }

    private static async Task<StandbyDefendObserverTests.Driver> Open(MatchState initial, string branch, string? dir)
    {
        var driver = new StandbyDefendObserverTests.Driver(initial, dir);
        driver.Export("explicit-fixture-initial.json", initial);
        if (branch == "icevale")
            await driver.Act("P1", new DeclareBattleCommand("BF", ["UNIT"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"], ["DEFENDER"]));
        else
        {
            await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            await driver.Act("P1", new PassFocusCommand());
            await driver.Act("P2", new PassFocusCommand());
        }
        if (branch == "vayne")
        {
            Assert.Empty(driver.State.TriggerQueue);
            var trigger = Assert.Single(driver.State.StackItems);
            Assert.Equal(TriggerKinds.UnitConquestPayReturnSelfToHand, trigger.EffectKind);
            var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
            Assert.Equal(trigger.StackItemId, payment.ResolvingStackItemId);
        }
        return driver;
    }

    private static MatchState Position(string branch)
    {
        var state = ConquestLifecycleRegressionTests.State(branch switch {
            "gold" => "SFD·220/221", "legend" => "SFD·210/221", "sand" => "SFD·207/221", _ => "OGN·278/298" }, 0);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        locations["BF"] = new("P2", "BATTLEFIELD", "BF");
        cards["UNIT"] = cards["UNIT"] with { CardNo = branch == "vayne" ? "OGN·035/298" : branch == "icevale" ? "UNL-065/219" : "SFD·125/221" };
        if (branch == "legend")
        {
            cards["LEGEND"] = new("LEGEND", cardNo: "UNL-197/219", ownerId: "P1", controllerId: "P1", isExhausted: true);
            zones["P1"] = zones["P1"] with { LegendZone = ["LEGEND"] };
            locations["LEGEND"] = new("P1", "LEGEND");
        }
        if (branch == "icevale")
        {
            cards["BF"] = cards["BF"] with { ControllerId = "P2" };
            cards["DEFENDER"] = new("DEFENDER", cardNo: "SFD·125/221", power: 1, tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2");
            zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Where(id => id != "UNIT").ToArray(), Battlefields = ["UNIT"] };
            zones["P2"] = zones["P2"] with { Battlefields = ["BF", "DEFENDER"] };
            locations["UNIT"] = new("P1", "BATTLEFIELD", "BF"); locations["DEFENDER"] = new("P2", "BATTLEFIELD", "BF");
        }
        return state with { RoomId = "optional-mana-prompt-" + branch, CardObjects = cards, PlayerZones = zones,
            ObjectLocations = locations, UntilEndOfTurnEffects = branch == "icevale" ? [BattlefieldTaskMarkers.SpellDuelCompleted("BF")] : [],
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(1, 0), ["P2"] = RunePool.Empty } };
    }
}
