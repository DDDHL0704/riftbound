using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ControlledLegendResolutionTests
{
    [Theory]
    [InlineData("FIRST", false)]
    [InlineData("SECOND", false)]
    [InlineData("SECOND", true)]
    public async Task PaymentChoosesAnyControlledLegendAtResolutionAndSurvivesRecovery(
        string selected, bool secondOwnedByOpponent)
    {
        var game = new Driver(Position(secondOwnedByOpponent: secondOwnedByOpponent)) { RecoverAfterEveryCommand = false };
        await Conquer(game);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.DoesNotContain("FIRST", payment.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("SECOND", payment.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TARGETS_CONFIRMED");
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));

        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("CONTROLLED_LEGEND_SELECTION", choice.Request.Kind);
        Assert.Equal(new[] { "FIRST", "SECOND" }, choice.Request.Options.Select(option => option.Id));
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.True(game.State.CardObjects["SECOND"].IsExhausted);
        var prompt = game.PromptFor("P1");
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PayCost);
        Assert.Equal("选择变为活跃的传奇", candidate.Label);
        Assert.DoesNotContain(game.PromptFor("P2").Candidates!, c => c.Action == CommandTypes.PayCost && c.Enabled);
        await game.ColdCheckpoint();
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [selected]));

        Assert.Null(game.State.PendingRuleChoice);
        Assert.Null(game.State.PendingPayment);
        Assert.False(game.State.CardObjects[selected].IsExhausted);
        Assert.True(game.State.CardObjects[selected == "FIRST" ? "SECOND" : "FIRST"].IsExhausted);
        Assert.True(game.State.CardObjects["OPPONENT"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        var readied = Assert.Single(game.Events(), e => e.Event.Kind == "LEGEND_READIED");
        Assert.Equal(selected, readied.Event.Payload["sourceObjectId"]);
        Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID"
            && e.Event.Payload.GetValueOrDefault("reason") as string == TriggerKinds.BattlefieldConquerPayReadyLegend);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TARGETS_CONFIRMED");
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task DecliningTheCostDoesNotChooseALegendOrSpendMana()
    {
        var game = new Driver(Position()) { RecoverAfterEveryCommand = false };
        await Conquer(game);
        var payment = game.State.PendingPayment!;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Null(game.State.PendingPayment);
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.True(game.State.CardObjects["SECOND"].IsExhausted);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "COST_PAID" or "LEGEND_READIED" or "RULE_CHOICE_REQUESTED");
        await game.Replay();
    }

    [Fact]
    public async Task AnAlreadyActiveLegendRemainsALegalResolutionChoice()
    {
        var game = new Driver(Position(secondExhausted: false)) { RecoverAfterEveryCommand = false };
        await Conquer(game);
        var payment = game.State.PendingPayment!;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
        var choice = game.State.PendingRuleChoice!.Request;
        Assert.Contains(choice.Options, option => option.Id == "SECOND");
        await game.Act("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", ["SECOND"]));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.False(game.State.CardObjects["SECOND"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "LEGEND_READIED");
        var resolved = Assert.Single(game.Events(), e => e.Event.Kind == "BATTLEFIELD_TRIGGER_RESOLVED");
        Assert.Equal("SECOND", resolved.Event.Payload["legendObjectId"]);
        await game.Replay();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ZeroOrOneControlledLegendNeedsNoAdditionalDecision(int controlledLegends)
    {
        var game = new Driver(Position(controlledLegends)) { RecoverAfterEveryCommand = false };
        await Conquer(game);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
        Assert.Null(game.State.PendingPayment);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(controlledLegends, game.Events().Count(e => e.Event.Kind == "LEGEND_READIED"));
        Assert.True(game.State.CardObjects["OPPONENT"].IsExhausted);
        if (controlledLegends == 1) Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task InvalidLegendDecisionsAreAtomicAndForgedRecoveryOptionsAreRejected()
    {
        var game = new Driver(Position()) { RecoverAfterEveryCommand = false };
        await Conquer(game);
        var payment = game.State.PendingPayment!;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
        var state = game.State;
        var pending = Assert.IsType<RuleChoiceContinuation>(state.PendingRuleChoice);
        var choice = pending.Request;
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        foreach (var (player, command) in new[] {
            ("P2", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", ["FIRST"])),
            ("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", ["OPPONENT"])),
            ("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", ["UNIT"])),
            ("P1", new PayCostCommand("expired", "RULE_REPLACEMENT", ["FIRST"])),
            ("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", ["FIRST", "SECOND"])) })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(state,
                new PlayerIntent("invalid-legend", player, command.CmdType), command, default);
            Assert.False(rejected.Accepted);
            Assert.Empty(rejected.Events);
            Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
            Assert.Equal(snapshots, JsonSerializer.Serialize(rejected.Snapshots));
        }

        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var forged = state with { PendingRuleChoice = pending with {
            Request = choice with { Options = [..choice.Options, new("OPPONENT", "非法传奇", ["OPPONENT"])] } } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        await game.Act("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", ["SECOND"]));
        await game.ColdCheckpoint();
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
    }

    private static MatchState Position(int controlledLegends = 2, bool secondExhausted = true,
        bool secondOwnedByOpponent = false)
    {
        var state = ConquestLifecycleRegressionTests.State("SFD·210/221", 0);
        var cards = state.CardObjects.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        Add("OPPONENT", "OGN·253/298", "P2", "P2", true);
        if (controlledLegends >= 1) Add("FIRST", "UNL-181/219", "P1", "P1", true);
        if (controlledLegends >= 2) Add("SECOND", "SFD·181/221", secondOwnedByOpponent ? "P2" : "P1", "P1", secondExhausted);
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(1, 0), ["P2"] = RunePool.Empty } };

        void Add(string id, string cardNo, string owner, string controller, bool exhausted)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, owner)
                with { ControllerId = controller, IsExhausted = exhausted };
            zones[controller] = zones[controller] with { LegendZone = [..zones[controller].LegendZone, id] };
            locations[id] = new(controller, "LEGEND");
        }
    }
}
