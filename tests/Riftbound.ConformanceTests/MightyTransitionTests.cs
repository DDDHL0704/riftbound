using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MightyTransitionTests
{
    internal static async Task<ResolutionResult> OpenUnit(string cardNo = "SFD·180/221")
        => await EquipTestDriver.FinishAsync(await Act(UnitSource(cardNo), "P1",
            new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])));

    internal static MatchState LegendSource(string legend = "SFD·205/221")
    {
        var state = ArmamentMightTests.Position(gear: "SFD·056/221", unit: "SFD·167/221", power: 2);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = new("LEGEND", cardNo: legend, ownerId: "P1", controllerId: "P1", tags: ["CARD_TYPE:LEGEND"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
    }

    internal static MatchState UnitSource(string cardNo = "SFD·180/221", bool alreadyMighty = false)
    {
        var state = ArmamentMightTests.Position(gear: alreadyMighty ? "SFD·022/221" : "SFD·056/221",
            unit: "SFD·167/221", power: alreadyMighty ? 4 : 2, attached: alreadyMighty);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = true },
            ["FIORA"] = new("FIORA", cardNo: cardNo, power: 3, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) } };
        return LocalDestructionRecallTests.At(state, "FIORA", "P1", "BASE");
    }

    [Theory]
    [InlineData("SFD·180/221")] [InlineData("SFD·180a/221")]
    public async Task EquippingAcrossFiveCreatesThePrintedReadyTrigger(string source)
    {
        var state = UnitSource(source); Assert.Equal(2, ArmamentMightTests.Might(state));
        var done = await EquipTestDriver.FinishAsync(await Act(state, "P1",
            new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])));
        Assert.Equal(5, ArmamentMightTests.Might(done.State)); Assert.Equal(2, done.State.CardObjects["HOST"].Power);
        Assert.True(done.State.PendingPayment is not null || done.State.TriggerQueue.Count > 0 || done.State.StackItems.Count > 0);
    }

    [Theory]
    [InlineData("SFD·205/221")] [InlineData("SFD·251/221")]
    public async Task TheLegendObservesBecomingMightyThroughEquipment(string legend)
    {
        var state = LegendSource(legend);
        var done = await EquipTestDriver.FinishAsync(await Act(state, "P1",
            new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])));
        Assert.Equal(5, ArmamentMightTests.Might(done.State)); Assert.False(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(3, done.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.True(done.State.PendingCardChoice is not null || done.State.TriggerQueue.Count > 0 || done.State.StackItems.Count > 0);
    }

    [Theory]
    [InlineData("SFD·180/221", true)] [InlineData("SFD·180/221", false)]
    [InlineData("SFD·180a/221", true)] [InlineData("SFD·180a/221", false)]
    public async Task ReadyCostConfirmsBeforeResponsesAndDecliningLeavesUnitExhausted(string card, bool pay)
    {
        var opened = await OpenUnit(card); var pending = Assert.IsType<PendingPaymentState>(opened.State.PendingPayment);
        Assert.Equal("MIGHTY_SKILL_COST", pending.PaymentWindow);
        Assert.Equal(1, pending.PowerCostByTrait[RuneTrait.Yellow]);
        Restore(opened.State); ArmamentMightTests.PublicRecovery(opened.State);
        var confirmed = await Act(opened.State, "P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, [pay ? "SPEND_POWER:yellow:1" : "DECLINE"]));
        Assert.True(confirmed.State.CardObjects["HOST"].IsExhausted); Assert.Null(confirmed.State.PendingPayment);
        Assert.DoesNotContain(confirmed.Events, e => e.Kind == "UNIT_READIED");
        if (!pay) { Assert.Empty(confirmed.State.StackItems); Assert.DoesNotContain(confirmed.Events, e => e.Kind == "COST_PAID"); }
        else {
            Assert.True(Assert.Single(confirmed.State.StackItems).MightySkill!.Confirmed);
            Assert.Single(confirmed.Events, e => e.Kind == "COST_PAID");
            Restore(confirmed.State); ArmamentMightTests.PublicRecovery(confirmed.State);
            var done = await Top(confirmed.State); Assert.False(done.State.CardObjects["HOST"].IsExhausted);
            Assert.Single(done.Events, e => e.Kind == "UNIT_READIED"); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        }
    }

    [Theory]
    [InlineData("SFD·205/221", true)] [InlineData("SFD·205/221", false)]
    [InlineData("SFD·251/221", true)] [InlineData("SFD·251/221", false)]
    public async Task LegendExhaustionIsOptionalAndChannelingWaitsForResponses(string card, bool accept)
    {
        var opened = await EquipTestDriver.FinishAsync(await Act(LegendSource(card), "P1",
            new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])));
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Restore(opened.State); ArmamentMightTests.PublicRecovery(opened.State);
        var confirmed = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["LEGEND"] : []));
        Assert.Equal(accept, confirmed.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(3, confirmed.State.PlayerZones["P1"].RuneDeck.Count);
        if (!accept) { Assert.Empty(confirmed.State.StackItems); Assert.DoesNotContain(confirmed.Events, e => e.Kind == "RUNES_CALLED"); }
        else {
            Restore(confirmed.State); ArmamentMightTests.PublicRecovery(confirmed.State);
            var done = await Top(confirmed.State); Assert.Equal(2, done.State.PlayerZones["P1"].RuneDeck.Count);
            var rune = Assert.Single(done.Events, e => e.Kind == "RUNES_CALLED");
            var id = Assert.Single(Assert.IsType<string[]>(rune.Payload["runeObjectIds"]));
            Assert.Contains(id, done.State.PlayerZones["P1"].Base); Assert.True(done.State.CardObjects[id].IsExhausted);
            Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        }
    }

    [Theory]
    [InlineData("before")] [InlineData("after")] [InlineData("source-generation")]
    [InlineData("unit-generation")] [InlineData("instance")] [InlineData("private-frame")]
    [InlineData("cost")] [InlineData("choices")]
    public async Task RecoveryRejectsForgedCrossingIdentityInputsOrCosts(string field)
    {
        var opened = await OpenUnit(); var state = opened.State; var item = Assert.Single(state.StackItems); var context = item.MightySkill!;
        var forged = field switch {
            "before" => context with { BeforeMight = 5 }, "after" => context with { AfterMight = 4 },
            "source-generation" => context with { Source = context.Source with { Generation = 1 } },
            "unit-generation" => context with { Unit = context.Unit with { Generation = 1 } },
            "instance" => context with { InstanceId = "FORGED" },
            "private-frame" => context with { After = context.After with { PlayerZones = new Dictionary<string, PlayerZones>(context.After.PlayerZones) {
                ["P1"] = context.After.PlayerZones["P1"] with { Hand = ["PRIVATE"] } } } }, _ => context };
        var pending = state.PendingPayment!;
        var changed = state with { StackItems = [item with { MightySkill = forged }], PendingPayment = field switch {
            "cost" => pending with { PowerCostByTrait = new Dictionary<string, int> { [RuneTrait.Yellow] = 0 } },
            "choices" => pending with { LegalPaymentChoiceIds = ["DECLINE"] }, _ => pending } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(changed), e => e.Contains("Mighty transition", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublicRecoveryRejectsSelfConsistentButUnrecordedHistoricalInput()
    {
        var opened = await OpenUnit(); var state = opened.State; var captured = Assert.Single(state.StackItems).MightySkill!;
        var earlier = captured.Before with { CardObjects = new Dictionary<string, CardObjectState>(captured.Before.CardObjects) {
            ["HOST"] = captured.Before.CardObjects["HOST"] with { Power = 1 } } };
        var forged = captured with { Before = earlier, BeforeMight = 1 };
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        ArmamentMightTests.PublicRecovery(state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var payload = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            payload["mightySkill"] = forged;
            Assert.NotEmpty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator));
            payload["mightySkill"] = captured;
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EveryPrintedOrCopiedSkillInstanceQueuesIndependently(bool copied)
    {
        var state = UnitSource();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECOND"] = copied
                ? new("SECOND", cardNo: "SFD·059/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard],
                    attachedToObjectId: "FIORA") { AttachmentRevision = 1, AttachedRuleText = new(new("FIORA", 0), "SFD·180/221", 1, state.Tick, 0) }
                : new("SECOND", cardNo: "SFD·180a/221", power: 3, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) } };
        state = LocalDestructionRecallTests.At(state, "SECOND", "P1", "BASE");
        var done = await EquipTestDriver.FinishAsync(await Act(state, "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])));
        Assert.Equal(2, done.State.TriggerQueue.Count); Assert.Equal(2, done.State.TriggerQueue.Select(t => t.MightySkill!.InstanceId).Distinct().Count());
        Assert.All(done.State.TriggerQueue, t => { Assert.Equal("HOST", t.MightySkill!.Unit.ObjectId); Assert.Equal(2, t.MightySkill.BeforeMight); Assert.Equal(5, t.MightySkill.AfterMight); });
        Assert.Equal(copied ? "FIORA" : "SECOND", done.State.TriggerQueue[1].SourceObjectId);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("FIORA")] [InlineData("HOST")]
    public async Task ARealReturnResponsePreservesTheSkillAndDoesNotReadyANewIncarnation(string returned)
    {
        var state = OfficialCopyIdentityTests.AddSpell(UnitSource(), "RETURN", "OGN·104/298");
        var opened = await EquipTestDriver.FinishAsync(await Act(state, "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])));
        var pending = opened.State.PendingPayment!;
        var paid = await Act(opened.State, "P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["SPEND_POWER:yellow:1"]));
        var response = await Act(paid.State, "P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned]));
        var departed = await Top(response.State);
        Assert.Contains(returned, departed.State.PlayerZones["P1"].Hand);
        Assert.Equal(1, departed.State.CardObjects[returned].ObjectGeneration);
        Assert.Equal(0, Assert.Single(departed.State.StackItems).MightySkill!.Source.Generation);
        Restore(departed.State); ArmamentMightTests.PublicRecovery(departed.State);
        var done = await Top(departed.State); Assert.Empty(done.State.StackItems);
        if (returned == "FIORA") {
            Assert.False(done.State.CardObjects["HOST"].IsExhausted); Assert.Single(done.Events, e => e.Kind == "UNIT_READIED");
        } else Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_READIED");
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ARealDetachResponseDoesNotRecheckAnEarnedMightyCondition()
    {
        var state = AttackDamageDistributionTests.Spell(UnitSource(), "DETACH", "SFD·011/221", "P2");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(5, 5) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RESPONSE-DRAW"] = new("RESPONSE-DRAW", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { MainDeck = ["RESPONSE-DRAW"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RESPONSE-DRAW"] = new("P2", "MAIN_DECK") } };
        var opened = await EquipTestDriver.FinishAsync(await Act(state, "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])));
        var pending = opened.State.PendingPayment!;
        var paid = await Act(opened.State, "P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["SPEND_POWER:yellow:1"]));
        var pass = await Act(paid.State, "P1", new PassPriorityCommand());
        var response = await Act(pass.State, "P2", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
        var detached = await Top(response.State); Assert.Equal(2, ArmamentMightTests.Might(detached.State));
        Assert.Equal(5, Assert.Single(detached.State.StackItems).MightySkill!.AfterMight);
        Assert.True(detached.State.CardObjects["HOST"].IsExhausted); Restore(detached.State); ArmamentMightTests.PublicRecovery(detached.State);
        var done = await Top(detached.State); Assert.False(done.State.CardObjects["HOST"].IsExhausted);
        Assert.Single(done.Events, e => e.Kind == "UNIT_READIED"); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task AStoredFourAlreadyAtCurrentSixDoesNotBecomeMightyAgainWhenBooned()
    {
        var state = PowerfulConsumerMightTests.Hand(UnitSource(alreadyMighty: true), "OGN·053/298");
        Assert.Equal(6, ArmamentMightTests.Might(state)); Assert.Equal(4, state.CardObjects["HOST"].Power);
        var done = await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·053/298", ["HOST"]))).State);
        Assert.Equal(5, done.State.CardObjects["HOST"].Power);
        Assert.Equal(7, ArmamentMightTests.Might(done.State with { UntilEndOfTurnEffects = [] }));
        Assert.Equal(8, ArmamentMightTests.Might(done.State));
        Assert.Null(done.State.PendingPayment); Assert.Empty(done.State.TriggerQueue); Assert.Empty(done.State.StackItems);
    }
}
