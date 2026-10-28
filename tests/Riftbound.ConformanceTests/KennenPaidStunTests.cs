using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class KennenPaidStunTests
{
    private static async Task Select(VenBasicFlowSpellTests.Driver game, params string[] targets)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
    }
    private static async Task Pay(VenBasicFlowSpellTests.Driver game, string answer)
    {
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act(payment.PlayerId, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [answer]));
    }

    [Theory]
    [InlineData("VEN·135", 37835)] [InlineData("VEN·135a", 37836)]
    public async Task EachHeroPrintingPlaysThenOptionallyPaysToStunAChosenUnit(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("御衡之心", card.SubTitle);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers, t => t.Kind == TriggerKinds.UnitPlayedOrAttacksOptionalPaidStun);
        Assert.Equal(2, spec.ManaCost); Assert.Equal(TriggerTargetScopes.AnyUnitOnField, spec.TargetScope);
        var game = new VenBasicFlowSpellTests.Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(17, game.State.RunePools["P1"].Mana);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount);
        Assert.All(new[] { "UNIT", "ALLY", "ENEMY" }, id => Assert.Contains(id, choice.LegalObjectIds));
        Assert.DoesNotContain("GEAR", choice.LegalObjectIds); Assert.DoesNotContain("GRAVE", choice.LegalObjectIds);
        await Select(game, "ENEMY");
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(2, payment.ManaCost); Assert.Equal(0, payment.PowerCost);
        Assert.Equal(17, game.State.RunePools["P1"].Mana);
        await Pay(game, "PAY");
        Assert.Equal(15, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·135")] [InlineData("VEN·135a")]
    public async Task AttackingOffersTheSamePaidStunBeforeResponses(string no)
    {
        var attacked = await AttackDamageDistributionTests.Attack(AttackDamageDistributionTests.Position(no));
        var choice = Assert.IsType<PendingCardChoiceState>(attacked.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount);
        var selected = await Act(attacked.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        var payment = Assert.IsType<PendingPaymentState>(selected.State.PendingPayment);
        var paid = await Act(selected.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.DoesNotContain("STUNNED", paid.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        var done = await AttackDamageDistributionTests.Top(paid.State);
        Assert.Contains("STUNNED", done.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("PAY")] [InlineData("DECLINE")]
    public async Task WardAndOptionalManaArePaidTogetherOrNotAtAll(string answer)
    {
        var state = VenUnitEntryBasicTests.Position("VEN·135");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·135", [], Destination: "BASE"));
        await Select(game, "ENEMY"); var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(2, payment.ManaCost); Assert.Equal(2, payment.PowerCost);
        var mana = game.State.RunePools["P1"].Mana; var power = game.State.RunePools["P1"].TotalPower;
        await Pay(game, answer);
        Assert.Equal(mana - (answer == "PAY" ? 2 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(power - (answer == "PAY" ? 2 : 0), game.State.RunePools["P1"].TotalPower);
        if (answer == "PAY") await game.Top();
        Assert.Equal(answer == "PAY", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        await game.Replay();
    }

    [Fact]
    public async Task DecliningBeforeTargetSelectionSpendsNothing()
    {
        var game = new VenBasicFlowSpellTests.Driver(VenUnitEntryBasicTests.Position("VEN·135"));
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·135", [], Destination: "BASE"));
        var mana = game.State.RunePools["P1"].Mana;
        await Select(game);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingPayment);
        await game.Replay();
    }

    [Theory]
    [InlineData("bad-target")] [InlineData("two-targets")] [InlineData("wrong-player")]
    public async Task IllegalChoiceCannotChangeTheState(string kind)
    {
        var initial = VenUnitEntryBasicTests.Position("VEN·135");
        var played = await Act(initial, "P1", new PlayCardCommand("UNIT", "VEN·135", [], Destination: "BASE"));
        var choice = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            kind == "bad-target" ? ["GEAR"] : kind == "two-targets" ? ["ALLY", "ENEMY"] : ["ENEMY"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(played.State, new("bad", kind == "wrong-player" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(played.State), MatchStateHasher.Hash(rejected.State));
    }
    [Fact]
    public async Task AnUnfundedPaymentIsRejectedWithoutChangingStateAndMayStillBeDeclined()
    {
        var state = VenUnitEntryBasicTests.Position("VEN·135");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 0) } };
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", "VEN·135", [], Destination: "BASE"));
        var choice = played.State.PendingCardChoice!;
        var selected = await Act(played.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var payment = selected.State.PendingPayment!;
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(selected.State, new("unfunded", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(selected.State), MatchStateHasher.Hash(rejected.State));
        var declined = await Act(selected.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.Empty(declined.State.StackItems); Assert.Contains("UNIT", declined.State.PlayerZones["P1"].Base);
        ArmamentMightTests.PublicRecovery(declined.State);
    }

    [Fact]
    public async Task TriggeredSkillCannotSpendManaRestrictedToUnitActivatedAbilities()
    {
        var state = AttackDamageDistributionTests.Position("VEN·135");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new RunePool(0, 0, unitPlayOrUnitAbilityOnlyMana: 2) } };
        var attacked = await AttackDamageDistributionTests.Attack(state);
        var choice = attacked.State.PendingCardChoice!;
        var selected = await Act(attacked.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        var payment = selected.State.PendingPayment!;
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(selected.State, new("restricted-mana", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(selected.State), MatchStateHasher.Hash(rejected.State));
        var declined = await Act(selected.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.Equal(2, declined.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.DoesNotContain("STUNNED", declined.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        ArmamentMightTests.PublicRecovery(declined.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AStunObserverFiresOnlyWhenAnEnemyActuallyBecomesStunned(bool alreadyStunned)
    {
        var state = VenUnitEntryBasicTests.Position("VEN·135");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { CardNo = "OGN·059/298", IsExhausted = true },
            ["ENEMY"] = state.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = alreadyStunned ? ["STUNNED"] : [] } } };
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·135", [], Destination: "BASE"));
        await Select(game, "ENEMY"); await Pay(game, "PAY"); await game.Top();
        Assert.Equal(alreadyStunned, game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(state.CardObjects["ALLY"].Power + (alreadyStunned ? 0 : 1), game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task StandbyPlacementDoesNotTriggerButLaterPlayingDoes()
    {
        var initial = StandbySourceRulesTests.Position("VEN·135");
        var hidden = await Act(initial, "P1", new HideCardCommand("SHADOW", "VEN·135", "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.DoesNotContain(hidden.State.StackItems, item => item.FieldContext?.Kind == "PLAY_OR_ATTACK_OPTIONAL_PAID_STUN");
        var ready = await StandbySourceRulesTests.ReadyShadow("VEN·135", initial);
        var played = await Act(ready, "P1", new PlayCardCommand("SHADOW", "VEN·135", []));
        Assert.Contains(played.State.StackItems, item => item.FieldContext?.Kind == "PLAY_OR_ATTACK_OPTIONAL_PAID_STUN");
        Assert.NotNull(played.State.PendingCardChoice);
        ArmamentMightTests.PublicRecovery(played.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheCapturedSkillSurvivesSourceReturnAndDoesNotExposePrivateObjects(bool targetIsSource)
    {
        var initial = AttackDamageDistributionTests.Spell(AttackDamageDistributionTests.Position("VEN·135"), "RETURN", "OGN·169/298", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Power = 2 } } };
        var attacked = await AttackDamageDistributionTests.Attack(initial);
        var choice = attacked.State.PendingCardChoice!;
        var selected = await Act(attacked.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            [targetIsSource ? "HOST" : "ENEMY1"]));
        var payment = selected.State.PendingPayment!;
        var paid = await Act(selected.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        var passed = await Act(paid.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RETURN", "OGN·169/298", ["HOST"]));
        var returned = await AttackDamageDistributionTests.Top(response.State);
        Assert.Contains("HOST", returned.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(returned.State)["P2"].Stack));
        ArmamentMightTests.PublicRecovery(returned.State);
        var done = await AttackDamageDistributionTests.Top(returned.State);
        Assert.Equal(!targetIsSource, done.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.DoesNotContain("STUNNED", done.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(done.Events));
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("paid-too-early")] [InlineData("unpaid-after-confirmation")] [InlineData("forged-mana")]
    public async Task RecoveryRejectsForgedPaymentAndTriggerContexts(string change)
    {
        var played = await Act(VenUnitEntryBasicTests.Position("VEN·135"), "P1", new PlayCardCommand("UNIT", "VEN·135", [], Destination: "BASE"));
        var choice = played.State.PendingCardChoice!;
        var selected = await Act(played.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var payment = selected.State.PendingPayment!; var state = selected.State;
        if (change == "unpaid-after-confirmation") state = (await Act(state, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]))).State;
        var item = Assert.Single(state.StackItems); var context = item.FieldContext!;
        if (change == "forged-mana") state = state with { PendingPayment = payment with { ManaCost = 1 } };
        else state = state with { StackItems = [item with { FieldContext = context with {
            PaidUnitTrigger = context.PaidUnitTrigger! with { CostPaid = change == "paid-too-early" } } }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state));
    }

    [Fact]
    public async Task CopiedPrintedTextCreatesTwoIndependentPaidAttackTriggers()
    {
        var attack = await AttackDamageDistributionTests.Attack(AttackDamageDistributionTests.Position("VEN·135", copied: true));
        Assert.Equal(2, attack.State.TriggerQueue.Count);
        var ids = attack.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray();
        Assert.Equal(2, ids.Distinct().Count());
        var state = (await Act(attack.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: ids))).State;
        foreach (var target in new[] { "ENEMY1", "ENEMY2" }) {
            var choice = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice);
            state = (await Act(state, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]))).State;
            var payment = Assert.IsType<PendingPaymentState>(state.PendingPayment);
            state = (await Act(state, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]))).State;
        }
        Assert.Equal(2, state.StackItems.Count);
        Assert.Single(state.StackItems, item => item.FieldContext!.Origin is not null);
        ArmamentMightTests.PublicRecovery(state);
        state = (await AttackDamageDistributionTests.Top(state)).State;
        state = (await AttackDamageDistributionTests.Top(state)).State;
        Assert.All(new[] { "ENEMY1", "ENEMY2" }, id => Assert.Contains("STUNNED", state.CardObjects[id].UntilEndOfTurnEffects));
        ArmamentMightTests.PublicRecovery(state);
    }

}
