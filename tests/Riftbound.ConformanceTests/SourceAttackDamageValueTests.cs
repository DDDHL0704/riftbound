using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.PrintedAttackClauseTests;

namespace Riftbound.ConformanceTests;

public sealed class SourceAttackDamageValueTests
{
    private static readonly string[] Printings = ["SFD·028/221", "SFD·028a/221", "OGN·130/298"];
    public static IEnumerable<object[]> Cards() => Printings.Select(no => new object[] { no });

    [Theory]
    [InlineData("{{强攻}}", "强攻")]
    [InlineData("强攻", "强攻")]
    [InlineData("{{坚守}}", "坚守")]
    [InlineData("坚守", "坚守")]
    public void PrintedKeywordMarkersAndBareNumericNamesHaveTheSameExactAttackSpecification(string printed, string keyword)
    {
        var spec = Assert.Single(TriggerParser.Parse($"当我进攻时，对此处的一名敌方单位造成等同于我{printed}数值的伤害。"));
        Assert.Equal(TriggerKinds.UnitAttackSourceKeywordDamage, spec.Kind);
        Assert.Equal(TriggerTimings.UnitAttack, spec.Timing);
        Assert.Equal(TriggerTargetScopes.EnemyUnitHere, spec.TargetScope);
        Assert.Equal(keyword, spec.DamageSourceKeyword);
        Assert.Null(spec.DamageAmount);
        Assert.NotEqual(true, spec.Optional);
    }

    [Theory, InlineData("{{后排}}"), InlineData("{{强攻")]
    public void NonNumericOrBrokenKeywordMarkersCannotDeclareTheDamageRoute(string printed)
        => Assert.DoesNotContain(TriggerParser.Parse($"当我进攻时，对此处的一名敌方单位造成等同于我{printed}数值的伤害。"),
            spec => spec.Kind == TriggerKinds.UnitAttackSourceKeywordDamage);

    private static async Task Choose(Driver game, string target = "ENEMY")
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(1, choice.RequiredCount);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
    }

    [Theory, MemberData(nameof(Cards))]
    public async Task EachPrintingSelectsAnEnemyHereAndWaitsForResponsesBeforeDealingOne(string no)
    {
        var game = new Driver(Position(no));
        await Attack(game, no);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { "ENEMY" }, choice.LegalObjectIds);
        await Choose(game);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("COMBAT_SOURCE_MIGHT_DAMAGE", skill.FieldContext!.Kind);
        Assert.Equal(no, skill.FieldContext.CardNo);
        OfficialInsightAndSpellLockTests.Restore(game.State);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory, MemberData(nameof(Cards))]
    public async Task MandatoryTargetingRejectsFriendlyDistantPrivateDuplicateAndWrongPlayerChoices(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(Position(no), "DISTANT", "OGN·088/298", "P2", "BASE");
        initial = VenConditionalPlayCostTests.Place(initial, "PRIVATE", "OGN·088/298", "P2", "HAND");
        var game = new Driver(initial);
        await Attack(game, no);
        var choice = game.State.PendingCardChoice!;
        foreach (var targets in new string[][] { [], ["UNIT"], ["ALLY"], ["GEAR"], ["DISTANT"], ["PRIVATE"], ["ENEMY", "ENEMY"] })
            await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await Choose(game); await game.Top();
        Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Fact]
    public async Task PirateMayChooseAnEnemyOtherThanTheFirstDefender()
    {
        const string no = "OGN·130/298";
        var initial = VenConditionalPlayCostTests.Place(Position(no), "SECOND", "OGN·088/298", "P2", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "SECOND", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await Attack(game, no);
        Assert.Contains("SECOND", game.State.PendingCardChoice!.LegalObjectIds);
        await Choose(game, "SECOND"); await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(1, game.State.CardObjects["SECOND"].Damage);
        await game.Replay();
    }

    [Theory, InlineData("SFD·028/221"), InlineData("SFD·028a/221")]
    public async Task LucianUsesAssaultRatherThanHisMightAfterARealPowerResponse(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "BUFF", "SFD·034/221", "P1"));
        await Attack(game, no); await Choose(game);
        await game.Act("P1", new PlayCardCommand("BUFF", "SFD·034/221", ["UNIT"]));
        await game.Top();
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory, InlineData("SFD·028/221"), InlineData("SFD·028a/221")]
    public async Task ARealTemporaryAssaultGrantAddsToThePrintedKeywordAndDamage(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "GRANT", "VEN·012", "P1"));
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        await game.Act("P1", new PlayCardCommand("GRANT", "VEN·012", ["UNIT"]));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
        Assert.Contains("COMBAT_KEYWORD:强攻3:1", game.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER", []));
        await Choose(game); await game.Top();
        Assert.Equal(4, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory, InlineData("SFD·028/221"), InlineData("SFD·028a/221")]
    public async Task TheSameIncarnationReadsAKeywordChangeAtResolutionInsteadOfCachingItAtAttack(string no)
    {
        var game = new Driver(Position(no)); await Attack(game, no); await Choose(game);
        // Explicit same-generation instruction boundary, not a claim that a
        // main-phase-only grant spell can be cast during this response window.
        var source = game.State.CardObjects["UNIT"];
        var changed = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["UNIT"] = source with { UntilEndOfTurnEffects = [..source.UntilEndOfTurnEffects,
                TemporaryCombatKeywordRules.AppliedEffectId(source, "COMBAT_KEYWORD:强攻3")] } } };
        OfficialInsightAndSpellLockTests.Restore(changed);
        var result = await OfficialGraveyardRecastTests.Top(changed);
        Assert.Equal(4, result.State.CardObjects["ENEMY"].Damage);
        OfficialInsightAndSpellLockTests.Restore(result.State);
    }

    [Theory, MemberData(nameof(Cards))]
    public async Task ARealSourceReturnLeavesNoLocationOrCurrentValueForItsOldSkill(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "RETURN", "OGN·104/298", "P1"));
        await Attack(game, no); await Choose(game);
        var skill = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(skill.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
        await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory, MemberData(nameof(Cards))]
    public async Task ARealMovementResponseChangesHereBeforeDamageResolves(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "MOVE", "OGS·011/024", "P1"));
        await Attack(game, no); await Choose(game);
        await game.Act("P1", new PlayCardCommand("MOVE", "OGS·011/024", ["UNIT"])); await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["UNIT"].Zone);
        await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory, MemberData(nameof(Cards))]
    public async Task ARealTargetReturnCannotDamageItsNewHandIncarnation(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "RETURN", "OGN·104/298", "P2"));
        await Attack(game, no); await Choose(game);
        var generation = game.State.CardObjects["ENEMY"].ObjectGeneration;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"])); await game.Top();
        Assert.True(game.State.CardObjects["ENEMY"].ObjectGeneration > generation);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory, MemberData(nameof(Cards))]
    public async Task EachSkillPaysTheSharedWardTaxBeforeItsResponseWindow(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(Position(no), "ENEMY", "OGN·013/298", "P2", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·013/298", out var ward));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Power = 9,
                Tags = initial.CardObjects["ENEMY"].Tags.Concat(ward.SourceUnitTags.Split('|')).Distinct().Order(StringComparer.Ordinal).ToArray() } } };
        var game = new Driver(initial); await Attack(game, no); var before = game.State.RunePools["P1"].TotalPower;
        await Choose(game);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.PowerCost);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.Equal(before - 1, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Top(); Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory, MemberData(nameof(Cards))]
    public async Task AnActualImageInheritsTheAttackClauseAndUsesItsCapturedCopiedFace(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(Position(no), "ENEMY", no, "P2", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Power = 15 } } };
        var game = new Driver(AttackDamageDistributionTests.Spell(initial, "COPY", "UNL-200/219", "P1"));
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["ENEMY"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        await game.Act("P1", new MoveUnitCommand(image.ObjectId, "BASE", "BATTLEFIELD:OTHER", []));
        Assert.Equal(image.ObjectId, game.State.PendingCardChoice!.SourceObjectId);
        await Choose(game); await game.Top();
        Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory, MemberData(nameof(Cards))]
    public async Task DefendingWithTheSamePrintedClauseDoesNotProduceAnAttackSkill(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(Position("OGN·219/298"), "ENEMY", no, "P2", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial); await Attack(game, "OGN·219/298");
        Assert.DoesNotContain(game.State.StackItems, item => item.SourceObjectId == "ENEMY" && item.FieldContext is not null);
        Assert.DoesNotContain(game.State.TriggerQueue, item => item.SourceObjectId == "ENEMY" && item.FieldContext is not null);
        await game.Replay();
    }
}
