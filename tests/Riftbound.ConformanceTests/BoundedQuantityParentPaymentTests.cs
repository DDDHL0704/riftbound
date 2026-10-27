using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ImposedMovementContinuationTests;

namespace Riftbound.ConformanceTests;

// Explicit payment-boundary fixtures. The parent is opened by real commands;
// balances are then fixed for the numerical boundary, not claimed turn-start
// acquisitions. These are state/command recovery tests, not SQL/restart proof.
public sealed class BoundedQuantityParentPaymentTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("held", "stele", true)] [InlineData("held", "stele", false)]
    [InlineData("held", "anomaly", true)] [InlineData("held", "anomaly", false)]
    [InlineData("ward", "stele", true)] [InlineData("ward", "stele", false)]
    [InlineData("ward", "anomaly", true)] [InlineData("ward", "anomaly", false)]
    public async Task AConversionRangePreservesTheUnpaidParentAndReturnsToItsExactDecision(string parentKind, string conversion, bool pay)
    {
        var state = await Parent(parentKind);
        var parent = Assert.IsType<PendingPaymentState>(state.PendingPayment);
        Assert.Equal(parentKind == "held" ? "TRIGGER_COST_CONFIRMATION" : "TRIGGER_TARGET_COST", parent.PaymentWindow);
        Assert.Equal(parentKind == "held" ? 4 : 1, parent.PowerCost);
        Assert.Equal("P1", parent.PlayerId); Assert.Null(state.PriorityPlayerId);
        state = WithPool(state, conversion == "stele" ? new(65, 0, spellOnlyMana: 7) : new(0, 65, spellOnlyMana: 7));
        var no = conversion == "stele" ? P4ActivatedAbilityCatalog.AncientSteleCardNo : P4ActivatedAbilityCatalog.HextechAnomalyCardNo;
        state = AddBase(state, "CONVERTER", no);
        var ability = conversion == "stele" ? P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId : P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId;
        var prefix = conversion == "stele" ? P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix : P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix;
        var parentHash = MatchStateHasher.HashValue(parent); var initialPool = state.RunePools["P1"];
        var events = new List<GameEvent>(); var engine = new CoreRuleEngine();
        var session = new MatchSession(state, engine, NoopMatchJournal.Instance);
        await Act(new ActivateAbilityCommand("CONVERTER", ability, [], [prefix + "CHOOSE_AMOUNT"]));
        var count = 0;
        while (state.PendingRuleChoice is { Request: var request })
        {
            Assert.Equal("RESOURCE_CONVERSION_AMOUNT", request.Kind); Assert.Equal("P1", request.PlayerId);
            Assert.Equal(parentHash, MatchStateHasher.HashValue(state.PendingPayment));
            Assert.Equal(parentHash, MatchStateHasher.HashValue(state.PendingRuleChoice.Origin.PendingPayment));
            Assert.Equal(initialPool, state.RunePools["P1"]); Assert.Equal(initialPool, request.Preview!.RunePools["P1"]);
            Assert.False(state.CardObjects["CONVERTER"].IsExhausted); Assert.False(request.Preview.CardObjects["CONVERTER"].IsExhausted);
            Assert.DoesNotContain(events, e => e.Kind is "COST_PAID" or "RESOURCE_SKILL_RESOLVED" or "MANA_GAINED" or "POWER_GAINED");
            Assert.DoesNotContain(events, e => e.Kind == "RUNE_POOL_CLEARED");
            var before = MatchStateHasher.Hash(state);
            var premature = await engine.ResolveAsync(state, new("premature-parent", "P1", CommandTypes.PayCost),
                new PayCostCommand(parent.PaymentId, parent.PaymentWindow, ["PAY"]), default);
            Assert.False(premature.Accepted); Assert.Empty(premature.Events); Assert.Equal(before, MatchStateHasher.Hash(premature.State));
            var option = request.Options.Single(o => { var pieces = o.Id.Split(':'); return int.Parse(pieces[^2]) <= 4 && 4 <= int.Parse(pieces[^1]); });
            Assert.True(++count <= 31); await Act(new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
        }
        Assert.InRange(count, 1, 31); Assert.Equal(parentHash, MatchStateHasher.HashValue(state.PendingPayment));
        Assert.True(state.CardObjects["CONVERTER"].IsExhausted); Assert.Single(events, e => e.Kind == "RESOURCE_SKILL_RESOLVED");
        Assert.DoesNotContain(events, e => e.Kind == "COST_PAID");
        Assert.Equal(conversion == "stele" ? 61 : 4, state.RunePools["P1"].Mana);
        Assert.Equal(conversion == "stele" ? 4 : 61, state.RunePools["P1"].TotalPower);
        if (parentKind == "ward") Assert.Equal(["ENEMY"], Assert.Single(state.StackItems).TargetObjectIds);
        await Act(new PayCostCommand(parent.PaymentId, parent.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.Null(state.PendingPayment); Assert.Null(state.PendingRuleChoice);
        Assert.Equal(pay ? 1 : 0, events.Count(e => e.Kind == "COST_PAID"));
        if (pay)
        {
            Assert.Equal((conversion == "stele" ? 4 : 61) - parent.PowerCost, state.RunePools["P1"].TotalPower);
            Assert.Equal(conversion == "stele" ? 61 : 4, state.RunePools["P1"].Mana); Assert.Equal(7, state.RunePools["P1"].SpellOnlyMana);
            Assert.Contains(state.StackItems, item => item.StackItemId == parent.ResolvingStackItemId);
            Assert.DoesNotContain(events, e => e.Kind == "RUNE_POOL_CLEARED");
            await Act(new PassPriorityCommand(), state.PriorityPlayerId!);
            await Act(new PassPriorityCommand(), state.PriorityPlayerId!);
            if (parentKind == "held") Assert.Equal(2, state.PlayerScores["P1"]);
            else Assert.Contains(CardObjectTags.Boon, state.CardObjects["ENEMY"].Tags);
        }
        else
        {
            Assert.DoesNotContain(state.StackItems, item => item.StackItemId == parent.ResolvingStackItemId);
            var declined = events.FindIndex(e => e.Kind == (parentKind == "held" ? "TRIGGER_PAYMENT_DECLINED" : "TRIGGER_NOT_CONFIRMED"));
            Assert.True(declined >= 0); Assert.True(declined < events.FindIndex(e => e.Kind == "RUNE_POOL_CLEARED"));
            if (parentKind == "held") Assert.Equal(1, state.PlayerScores["P1"]);
            else Assert.DoesNotContain(CardObjectTags.Boon, state.CardObjects["ENEMY"].Tags);
        }
        Assert.Equal(MatchPhases.Main, state.Phase); Assert.All(state.RunePools.Values, pool => Assert.Equal(RunePool.Empty, pool));
        Assert.Single(events, e => e.Kind == "RUNE_POOL_CLEARED"); Assert.Single(events, e => e.Kind == "RESOURCE_SKILL_RESOLVED");
        Assert.True(events.FindIndex(e => e.Kind == "RESOURCE_SKILL_RESOLVED") < events.FindIndex(e => e.Kind == "RUNE_POOL_CLEARED"));
        Assert.True(events.FindIndex(e => e.Kind == "RUNE_POOL_CLEARED") < events.FindIndex(e => e.Kind == "MAIN_PHASE_BEGAN"));
        Assert.Equal(pay ? 1 : 0, events.Count(e => e.Kind == "COST_PAID"));

        async Task Act(GameCommand command, string player = "P1")
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(node); command = GameCommandJsonMapper.Map(raw);
            var result = await session.SubmitAsync(player, "parent-range-" + state.Tick, command, raw, default);
            Assert.True(result.Accepted, result.ErrorCode + ": " + result.ErrorMessage); events.AddRange(result.Events); state = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, Web), Web)!;
            Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored));
            ArmamentMightTests.PublicRecovery(restored); Assert.Empty(OfficialInsightAndSpellLockTests.Errors(restored));
            session = new MatchSession(restored, engine, NoopMatchJournal.Instance); state = restored;
        }
    }

    private static async Task<MatchState> Parent(string kind)
    {
        var state = kind == "held" ? OfficialHoldSequenceTests.State("SFD·214/221") : OfficialHeldConfirmationTests.WardPosition();
        var engine = new CoreRuleEngine();
        var opened = await engine.ResolveAsync(state, new("parent-open", "P1", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        if (kind == "held") return opened.State;
        var choice = opened.State.PendingCardChoice!;
        var selected = await engine.ResolveAsync(opened.State, new("parent-target", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]), default);
        Assert.True(selected.Accepted, selected.ErrorMessage); return selected.State;
    }
}
