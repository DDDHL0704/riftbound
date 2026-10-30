using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DestructionGraveyardFamilyTests;
using static Riftbound.ConformanceTests.DestructionGraveyardLifecycleTests;

namespace Riftbound.ConformanceTests;

public sealed class DestructionGraveyardSourceOccupationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SiblingEarnedSkillsAllowOriginalPlayAndCannotBorrowLaterGraveyardEntry(bool reverse, bool reenter)
    {
        var initial = Add(Add(Add(Position(), "RUIN", "UNL-180/219", "P1", "HAND"),
            "REVIVE", "UNL-142/219", "P1", "HAND"), "OWN", "OGN·210/298", "P1", "BATTLEFIELD", "F");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["TARGET"] = HweiMoveDiscardTypesTests.Card("TARGET", "OGN·210/298", "P2") },
            RunePools = initial.RunePools.ToDictionary(p => p.Key, p => p.Key == "P1"
                ? new RunePool(40, 40, new Dictionary<string, int> { ["red"] = 20, ["yellow"] = 10, ["purple"] = 10 }) : p.Value) };
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("RUIN", "UNL-180/219", [])); await run.Top();
        Assert.Equal(2, run.State.TriggerQueue.Count);
        Assert.All(run.State.TriggerQueue, t => Assert.Equal(new ObjectBinding("PHOENIX", 0), t.GraveyardPlay!.Source));
        var order = run.State.TriggerQueue.Select(t => t.TriggerId).Order(StringComparer.Ordinal).ToArray();
        if (reverse) Array.Reverse(order);
        await run.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        for (var i = 0; i < 2; i++)
        {
            var payment = Assert.IsType<PendingPaymentState>(run.State.PendingPayment);
            Assert.Equal("TRIGGER_COST_CONFIRMATION", payment.PaymentWindow);
            Assert.Equal("P1", payment.PlayerId);
            Assert.Equal(1, payment.ManaCost); Assert.Equal(1, payment.PowerCostByTrait["red"]);
            var before = run.State.RunePools["P1"];
            await run.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            Assert.Equal(before.Mana - 1, run.State.RunePools["P1"].Mana);
            Assert.Equal(before.PowerByTrait["red"] - 1, run.State.RunePools["P1"].PowerByTrait["red"]);
        }
        Assert.Null(run.State.PendingPayment); Assert.Equal(2, run.State.StackItems.Count);
        await run.Top();
        Assert.Equal(new ObjectBinding("PHOENIX", 0), run.State.PendingEffectPlay!.Parent.GraveyardPlay!.Source);
        Assert.Equal(new ObjectBinding("PHOENIX", 0), Assert.Single(run.State.StackItems).GraveyardPlay!.Source);
        await run.Act("P1", new PlayCardCommand("PHOENIX", Phoenix, []));
        Assert.Contains("PHOENIX", run.State.PlayerZones["P1"].Base);
        Assert.Equal(1, run.State.CardObjects["PHOENIX"].ObjectGeneration);
        var remaining = Assert.Single(run.State.StackItems).StackItemId;
        if (reenter)
        {
            if (run.State.PriorityPlayerId != "P1") await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
            await run.Act("P1", new PlayCardCommand("REVIVE", "UNL-142/219", ["OWN"], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:PHOENIX"]));
            Assert.Contains("PHOENIX", run.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(2, run.State.CardObjects["PHOENIX"].ObjectGeneration);
            Trace($"revive-cost-{reverse}", new { run.State, prompts = ResolutionResult.BuildPrompts(run.State), commands = run.Commands(), events = run.Events() });
            var newCost = Assert.IsType<PendingPaymentState>(run.State.PendingPayment);
            var newSkill = Assert.Single(run.State.StackItems, i => i.StackItemId == newCost.ResolvingStackItemId);
            Assert.Equal(new ObjectBinding("PHOENIX", 2), newSkill.GraveyardPlay!.Source);
            Assert.Equal("UNL-142/219", newSkill.GraveyardPlay.Attribution.CardNo);
            Assert.True(newSkill.GraveyardPlay.Attribution.FromSpell);
            Assert.Equal(1, newCost.ManaCost); Assert.Equal(1, newCost.PowerCostByTrait["red"]);
            var beforeDecline = run.State.RunePools["P1"];
            await run.Act("P1", new PayCostCommand(newCost.PaymentId, newCost.PaymentWindow, ["DECLINE"]));
            Assert.Equal(beforeDecline, run.State.RunePools["P1"]);
            await run.Top();
            Assert.Equal(new[] { "OWN" }, run.State.PendingEffectPlay!.Sources.Keys);
            await run.Act("P1", new PlayCardCommand("OWN", "OGN·210/298", []));
        }
        var old = Assert.Single(run.State.StackItems);
        Assert.Equal(remaining, old.StackItemId);
        Assert.Equal(new ObjectBinding("PHOENIX", 0), old.GraveyardPlay!.Source);
        var pool = run.State.RunePools["P1"];
        var done = await run.Top();
        Assert.Contains(done.Events, e => e.Kind == "TRIGGER_RESOLVED");
        Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue); Assert.Null(run.State.PendingEffectPlay);
        Assert.Equal(pool, run.State.RunePools["P1"]);
        Assert.Equal(reenter ? 2 : 1, run.State.CardObjects["PHOENIX"].ObjectGeneration);
        Assert.Contains("PHOENIX", reenter ? run.State.PlayerZones["P1"].Graveyard : run.State.PlayerZones["P1"].Base);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Fact]
    public async Task ActualSpellCardOccupyingStackStillRejectsDuplicatePlayWithoutStateOrEventChange()
    {
        var initial = Position(); var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("RAY", Ray, ["TARGET"]));
        if (run.State.PriorityPlayerId != "P1") await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal("RAY", Assert.Single(run.State.StackItems).SourceObjectId);
        Assert.Equal("STACK", run.State.ObjectLocations["RAY"].Zone);
        var state = run.State; var command = new PlayCardCommand("RAY", Ray, ["TARGET"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("duplicate-real-stack-card", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("该出牌来源已在结算链中。", rejected.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypedAutomaticSkillsRemainUnavailableToSpellOnlyAndChosenUnitCounters(bool delayed)
    {
        var initial = Position(delayed ? "GRAVEYARD" : "BATTLEFIELD");
        foreach (var (id, no) in new[] { ("WINDWALL", "OGN·064/298"), ("REPULSE", "UNL-106/219"), ("QUICK", "SFD·045/221") })
            initial = Add(initial, id, no, "P2", "HAND");
        initial = initial with { RunePools = initial.RunePools.ToDictionary(p => p.Key,
            p => p.Key == "P2" ? new RunePool(30, 30, new Dictionary<string, int> { ["blue"] = 20 }) : p.Value) };
        var run = new StandbyDefendObserverTests.Driver(initial);
        if (delayed) { await run.Act("P1", new PlayCardCommand("DECREE", Decree, [])); await run.Top(); }
        await run.Act("P1", new PlayCardCommand("RAY", Ray, [delayed ? "TARGET" : "PHOENIX"])); await run.Top();
        if (!delayed) await PayPhoenix(run);
        if (run.State.PriorityPlayerId != "P2") await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        var state = run.State; var skill = Assert.Single(state.StackItems);
        if (delayed) Assert.NotNull(skill.DamageDestruction); else Assert.NotNull(skill.GraveyardPlay);
        Assert.Empty(skill.TargetObjectIds);
        var candidates = ResolutionResult.BuildPrompts(state)["P2"].Candidates!.Where(c => c.Action == CommandTypes.PlayCard).ToArray();
        Trace($"counter-options-{delayed}", new { state, candidates });
        var descriptor = Assert.Single(candidates.SelectMany(c => c.Targets ?? []), choice => choice.Id == skill.StackItemId);
        Assert.Contains("技能", descriptor.Label);
        foreach (var command in new[] {
            new PlayCardCommand("WINDWALL", "OGN·064/298", [skill.StackItemId]),
            new PlayCardCommand("REPULSE", "UNL-106/219", ["TARGET", skill.StackItemId]),
            new PlayCardCommand("QUICK", "SFD·045/221", [skill.StackItemId]) })
        {
            var quote = new CoreRuleEngine().PreviewPlayCard(state, "P2", PlayCostPreviewTests.Request(state, command));
            Assert.False(quote.CanPay);
            var rejected = await new CoreRuleEngine().ResolveAsync(state, new("invalid-automatic-skill-counter-" + command.SourceObjectId,
                "P2", command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
            Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        }
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    private static void Trace(string name, object value)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_SOURCE_OCCUPATION_TRACE") is not { } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name + ".json"), System.Text.Json.JsonSerializer.Serialize(value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
