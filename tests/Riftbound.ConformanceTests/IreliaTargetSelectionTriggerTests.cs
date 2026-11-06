using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class IreliaTargetSelectionTriggerTests
{
    internal const string Boost = "SFD·034/221";
    private static readonly string[] Prints = ["SFD·195/221", "SFD·195a/221·P", "SFD·246/221"];
    public static IEnumerable<object[]> Printings() => Prints.Select(card => new object[] { card });

    internal static MatchState Position(string printing = "SFD·195/221")
    {
        var state = LegendAttachmentTests.PositionFor(printing);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "OGN·219/298", "P1") with { IsExhausted = true } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20), ["P2"] = new(20, 20) } };
        return AttackDamageDistributionTests.Spell(state, "SPELL", Boost, "P1");
    }

    internal static Task Cast(Driver game) => game.Act("P1", new PlayCardCommand("SPELL", Boost, ["HOST"]));
    internal static Task Pay(Driver game, bool accept = true)
    {
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        return game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [accept ? "PAY" : "DECLINE"]));
    }

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task EveryPrintingCapturesTheRealTargetThenPaysOnePowerAndExhaustsBeforeAnIndependentResponse(string printing)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, card => card.CardNo == printing);
        Assert.Contains("并支付{{A}}", card.CardEffect);
        var game = new Driver(Position(printing)); await Cast(game);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(0, game.State.PendingPayment!.ManaCost); Assert.Equal(1, game.State.PendingPayment.PowerCost);
        Assert.Equal(2, game.State.StackItems.Count);
        var trigger = game.State.StackItems[^1];
        Assert.Equal("FRIENDLY_UNIT_TARGETED", trigger.UnitEvent!.Timing);
        Assert.Equal("HOST", trigger.UnitEvent.Other!.Binding.ObjectId);
        Assert.Empty(trigger.TargetObjectIds); Assert.Null(trigger.TargetGenerations);
        OfficialInsightAndSpellLockTests.Restore(game.State);
        var mana = game.State.RunePools["P1"].Mana; var power = game.State.RunePools["P1"].TotalPower;
        await Pay(game);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(power - 1, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.NotNull(game.State.StackItems[^1].TriggerCost);
        OfficialInsightAndSpellLockTests.Restore(game.State);
        await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Single(game.State.StackItems);
        OfficialInsightAndSpellLockTests.Restore(game.State);
        await game.Top(); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("decline")] [InlineData("mana")] [InlineData("no-power")]
    public async Task DecliningOrFailingThePrintedCostDoesNotExhaustOrReadyAnything(string branch)
    {
        var initial = Position();
        if (branch == "no-power") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0) } };
        var game = new Driver(initial); await Cast(game);
        if (branch != "decline")
        {
            var payment = game.State.PendingPayment!; var hash = MatchStateHasher.Hash(game.State);
            var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, branch == "mana" ? ["SPEND_MANA:1"] : ["PAY"]);
            var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("irelia-invalid-fee", "P1", CommandTypes.PayCost), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        }
        await Pay(game, false);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Single(game.State.StackItems);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task APrintedUnitActivationAlsoCapturesItsFriendlyTargetBeforeTheSkillResolves()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["FLOCK"] = OfficialGarenConquestBattlefieldCountTests.Printed("FLOCK", "RAD·027/167", "P1") } };
        initial = LocalDestructionRecallTests.At(initial, "FLOCK", "P1", "BASE");
        var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), item => item.SourceCardNo == "RAD·027/167"
            && item.Kind == ActivatedAbilityKinds.GrantUnitBoonOrTemporaryPower);
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("FLOCK", ability.AbilityId, ["HOST"]));
        Assert.Equal("HOST", game.State.StackItems[^1].UnitEvent!.Other!.Binding.ObjectId);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await Pay(game); await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task AnAlreadyCapturedUnitCanBecomeAnEnemyWithoutBeingTargetedOrPayingWardAgain()
    {
        var game = new Driver(Position()); await Cast(game); await Pay(game);
        // Isolate a same-generation control change while the paid trigger is pending.
        // This verifies the implicit reference, not the spell that changes control.
        var changed = LocalDestructionRecallTests.At(game.State, "HOST", "P2", "BATTLEFIELD", "F");
        changed = changed with { CardObjects = new Dictionary<string, CardObjectState>(changed.CardObjects) {
            ["HOST"] = changed.CardObjects["HOST"] with { Tags = changed.CardObjects["HOST"].Tags
                .Append(CardObjectTags.Spellshield).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() } } };
        changed = OfficialInsightAndSpellLockTests.Restore(changed);
        var power = changed.RunePools["P1"].TotalPower;
        var done = await OfficialInsightAndSpellLockTests.ResolveTop(changed);
        Assert.Equal("P2", done.State.CardObjects["HOST"].ControllerId);
        Assert.False(done.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(power, done.State.RunePools["P1"].TotalPower);
        Assert.Null(done.State.PendingPayment);
        Assert.Single(done.State.StackItems);
        OfficialInsightAndSpellLockTests.Restore(done.State);
    }

    [Fact]
    public async Task ReturningTheOriginalUnitInResponseDoesNotReadyTheCardInItsNewGeneration()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1"));
        await Cast(game); await Pay(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        var prefix = game.Events().Length;
        await game.Top();
        Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
        Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind == "UNIT_READIED");
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task OpponentsTargetSelectionDoesNotOfferTheLegendControllersCost()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "THEIR", Boost, "P2");
        initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("THEIR", Boost, ["HOST"]));
        Assert.Null(game.State.PendingPayment); Assert.Single(game.State.StackItems);
        Assert.DoesNotContain(game.State.TriggerQueue, t => t.UnitEvent?.Timing == "FRIENDLY_UNIT_TARGETED");
        await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task AnAlreadyExhaustedLegendDoesNotBlockTheOriginalSpellWithAnUnpayableTrigger()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["LEGEND"] = initial.CardObjects["LEGEND"] with { IsExhausted = true } } };
        var game = new Driver(initial); await Cast(game);
        Assert.Null(game.State.PendingPayment); Assert.Single(game.State.StackItems);
        await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task TheRemovedManualReactionCannotBeActivatedWithoutTheTargetEvent(string printing)
    {
        var initial = Position(printing); var hash = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("irelia-obsolete-activation", "P1", CommandTypes.LegendAct),
            new LegendActCommand("LEGEND", LegendActionAbilityCatalog.IreliaLegendAbilityId, ["HOST"], ["SPEND_MANA:1"]), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
    [Theory]
    [InlineData("origin")] [InlineData("actor")] [InlineData("null-actor")] [InlineData("cause")]
    [InlineData("unit")] [InlineData("unit-generation")] [InlineData("unit-face")]
    [InlineData("source-generation")] [InlineData("future-tick")]
    [InlineData("target")] [InlineData("repeat")] [InlineData("amount")] [InlineData("optional-cost")]
    public async Task RecoveryRejectsForgedTargetEventsOrExtraStackInstructions(string branch)
    {
        var game = new Driver(Position()); await Cast(game);
        var state = game.State; Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var item = state.StackItems[^1]; var context = item.UnitEvent!;
        if (branch == "origin") context = context with { TargetSelection = null };
        if (branch == "actor") context = context with { TargetSelection = context.TargetSelection! with { Actor = "P2" } };
        if (branch == "null-actor") context = context with { TargetSelection = context.TargetSelection! with { Actor = null! } };
        if (branch == "cause") context = context with { TargetSelection = context.TargetSelection! with { CauseId = "" } };
        if (branch == "unit") context = context with { Other = context.Other! with { Binding = new("UNKNOWN", 0) } };
        if (branch == "unit-generation") context = context with { Other = context.Other! with { Binding = new("HOST", 99) } };
        if (branch == "unit-face") context = context with { Other = context.Other! with { CardNo = "OGN·001/298" } };
        if (branch == "source-generation") context = context with { SourceGeneration = 99 };
        if (branch == "future-tick") context = context with { CapturedTick = state.Tick + 1 };
        item = item with { UnitEvent = context };
        if (branch == "target") item = item with { TargetObjectIds = ["HOST"] };
        if (branch == "repeat") item = item with { EffectRepeatCount = 2 };
        if (branch == "amount") item = item with { DamageAmount = 99 };
        if (branch == "optional-cost") item = item with { OptionalCosts = ["SPEND_MANA:1"] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [state.StackItems[0], item] }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARepeatedSpellOffersEachTargetEventButTheExhaustionCostCanOnlyBePaidOnce(bool differentTargets)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SECOND"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND", "OGN·219/298", "P1") with { IsExhausted = true } } };
        initial = LocalDestructionRecallTests.At(initial, "SECOND", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", Boost, ["HOST"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", [differentTargets ? "SECOND" : "HOST"])]));
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.All(game.State.TriggerQueue, t => Assert.Equal("FRIENDLY_UNIT_TARGETED", t.UnitEvent!.Timing));
        // The first ID is placed first and the last ID resolves first.
        var ordered = game.State.TriggerQueue.OrderBy(t => t.UnitEvent!.Other!.Binding.ObjectId, StringComparer.Ordinal)
            .Select(t => t.TriggerId).ToArray();
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ordered));
        var selected = game.State.StackItems.Single(item => item.StackItemId == game.State.PendingPayment!.ResolvingStackItemId)
            .UnitEvent!.Other!.Binding.ObjectId;
        var power = game.State.RunePools["P1"].TotalPower;
        await Pay(game); await game.Top();
        Assert.False(game.State.CardObjects[selected].IsExhausted);
        Assert.Null(game.State.PendingPayment);
        Assert.Single(game.State.StackItems);
        Assert.Equal(power - 1, game.State.RunePools["P1"].TotalPower);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task DifferentFriendlyTargetsRetainTheirSeparateOriginalUnitBeforeCostsAreChosen()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "PAIR", "SFD·151/221", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SECOND"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND", "OGN·219/298", "P1") with { IsExhausted = true } } };
        initial = LocalDestructionRecallTests.At(initial, "SECOND", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("PAIR", "SFD·151/221", ["HOST", "SECOND"]));
        Assert.Equal(new[] { "HOST", "SECOND" }, game.State.TriggerQueue.Select(t => t.UnitEvent!.Other!.Binding.ObjectId)
            .Order(StringComparer.Ordinal).ToArray());
        var metadata = Assert.Single(game.PromptFor("P1").Candidates!, candidate =>
            candidate.Action == CommandTypes.OrderTriggers).Metadata!;
        var choices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(metadata["triggerChoices"]);
        Assert.Equal(new[] { "HOST", "SECOND" }, choices.SelectMany(choice => choice.ObjectIds!)
            .Order(StringComparer.Ordinal).ToArray());
        Assert.All(choices, choice => Assert.StartsWith("活跃先前选中的", choice.Label));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var selected = game.State.StackItems.Single(item => item.StackItemId == game.State.PendingPayment!.ResolvingStackItemId)
            .UnitEvent!.Other!.Binding.ObjectId;
        await Pay(game); await game.Top();
        Assert.False(game.State.CardObjects[selected].IsExhausted);
        Assert.True(game.State.CardObjects[selected == "HOST" ? "SECOND" : "HOST"].IsExhausted);
        Assert.Single(game.State.StackItems);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task RemakingTheSameFriendlyTargetEarnsANewTriggerAfterDecliningTheFirstOne()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "MIRROR", "VEN·152", "P1"));
        await Cast(game); await Pay(game, false);
        var spell = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PlayCardCommand("MIRROR", "VEN·152", [spell])); await game.Top();
        async Task Choose(string option)
        {
            var request = game.State.PendingRuleChoice!.Request;
            await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        }
        Assert.Equal("OPTIONAL_SPELL_CONTROL_PAYMENT", game.State.PendingRuleChoice!.Request.Kind);
        await Choose("PAY:ANY");
        Assert.Equal("SPELL_CHOICE_REMAKE", game.State.PendingRuleChoice!.Request.Kind);
        // CONFIRM explicitly remakes all current choices; KEEP_ALL would decline to remake.
        await Choose("CONFIRM");
        Assert.Equal(1, game.State.PendingPayment!.PowerCost);
        Assert.Equal("HOST", game.State.StackItems[^1].UnitEvent!.Other!.Binding.ObjectId);
        await Pay(game); await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await game.Top(); await game.Replay();
    }

}
