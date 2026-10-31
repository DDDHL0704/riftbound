using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MoonveilCopyMightyObserverTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task DetachingARealCopiedArmamentRestoresTheFaceAndOpensOneMightyTrigger(string player)
    {
        // The starting position is controlled; the resource pool is a fixture.
        // Assembly, the copy, conquest, the paid response and detachment are real commands.
        var initial = ThreeBattlefieldTimingProbe.Position(player, "SFD·221/221", equipment: true);
        var opponent = ThreeBattlefieldTimingProbe.Other(player);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = new("HOST", cardNo: "OGN·001/298", power: 5, isExhausted: true,
                    tags: [CardObjectTags.UnitCard, "龙"], ownerId: player, controllerId: player),
                ["EQUIP"] = new("EQUIP", cardNo: "VEN·137", isExhausted: true,
                    tags: [CardObjectTags.EquipmentCard, "武装"], ownerId: player, controllerId: player),
                ["FIORA"] = new("FIORA", cardNo: "SFD·180/221", power: 3,
                    tags: [CardObjectTags.UnitCard], ownerId: player, controllerId: player) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = initial.PlayerZones[player] with { Base = initial.PlayerZones[player].Base.Concat(["FIORA"]).ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["FIORA"] = new(player, "BASE") },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                [player] = new(1, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 2 }) } };
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(initial));
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_THREE_FIELD_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial,
            evidence is null ? null : Path.Combine(evidence, "moonveil-copy-mighty-" + player));

        await driver.Act(player, new AssembleEquipmentCommand("EQUIP", "HOST", ["ASSEMBLE_YELLOW"]));
        await driver.Top();
        var copy = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("ATTACHMENT_COPY_SELECTION", copy.Request.Kind);
        var copied = await driver.Act(player, new PayCostCommand(copy.Request.Id, "RULE_REPLACEMENT", ["COPY:UNIT"]));
        Assert.Single(copied.Events, e => e.Kind == "UNIT_COPY_APPLIED");
        Assert.Equal("UNL-092/219", driver.State.CardObjects["HOST"].CardNo);
        Assert.NotNull(driver.State.CardObjects["HOST"].AttachmentUnitCopies);
        Assert.Equal(2, ThreeBattlefieldTimingProbe.EffectivePower(driver.State, player, "HOST"));

        await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act(player, new PassFocusCommand());
        await driver.Act(opponent, new PassFocusCommand());
        var confirmation = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Contains("EQUIP", confirmation.LegalObjectIds);
        await driver.Act(player, new ChooseCardsCommand(confirmation.ChoiceId, confirmation.ChoiceWindow, ["EQUIP"]));
        var moonveil = Assert.Single(driver.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerReadyEquipment);
        Assert.Equal(initial.CardObjects["EQUIP"].ObjectGeneration, moonveil.TargetGenerations!["EQUIP"]);
        if (driver.State.PriorityPlayerId == player) await driver.Act(player, new PassPriorityCommand());
        Assert.Equal(opponent, driver.State.PriorityPlayerId);
        await driver.Act(opponent, new TapRuneCommand("REACTION-RUNE"));
        var gust = await driver.Act(opponent, new PlayCardCommand("GUST", "OGN·169/298", ["UNIT"]));
        Assert.Equal(1, Assert.Single(gust.Events, e => e.Kind == "COST_PAID").Payload["totalManaCost"]);
        await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones[player].Hand);
        Assert.Equal(2, ThreeBattlefieldTimingProbe.EffectivePower(driver.State, player, "HOST"));
        Assert.True(driver.State.CardObjects["EQUIP"].IsExhausted);

        var ready = await driver.Top();
        Assert.Single(ready.Events, e => e.Kind == "EQUIPMENT_READIED");
        var detach = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(0, detach.RequiredCount);
        var detached = await driver.Act(player, new ChooseCardsCommand(detach.ChoiceId, detach.ChoiceWindow, ["EQUIP"]));
        Assert.Single(detached.Events, e => e.Kind == "EQUIPMENT_DETACHED");
        Assert.Single(detached.Events, e => e.Kind == "UNIT_COPY_ENDED");
        Assert.Equal("OGN·001/298", driver.State.CardObjects["HOST"].CardNo);
        Assert.Null(driver.State.CardObjects["HOST"].AttachmentUnitCopies);
        Assert.Null(driver.State.CardObjects["EQUIP"].AttachedToObjectId);
        Assert.Equal(5, ThreeBattlefieldTimingProbe.EffectivePower(driver.State, player, "HOST"));
        var mighty = Assert.Single(driver.State.StackItems, i => i.MightySkill is not null).MightySkill!;
        Assert.Equal("FIORA", mighty.Source.ObjectId);
        Assert.Equal("HOST", mighty.Unit.ObjectId);
        Assert.Equal(2, mighty.BeforeMight);
        Assert.Equal(5, mighty.AfterMight);
        var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
        Assert.Equal("MIGHTY_SKILL_COST", payment.PaymentWindow);
        Assert.True(driver.State.CardObjects["HOST"].IsExhausted);
        await driver.Act(player, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_POWER:yellow:1"]));
        Assert.True(driver.State.CardObjects["HOST"].IsExhausted);
        await driver.Top();
        Assert.False(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_COPY_ENDED");
        Assert.Single(driver.Events(), e => e.Event.Kind == "EQUIPMENT_DETACHED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        driver.Export("steps.json", driver.Steps);
    }
}
