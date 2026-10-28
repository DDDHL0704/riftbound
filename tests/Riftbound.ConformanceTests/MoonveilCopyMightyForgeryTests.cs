using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MoonveilCopyMightyForgeryTests
{
    [Theory]
    [InlineData("P1", "face")] [InlineData("P1", "prefix")]
    [InlineData("P2", "face")] [InlineData("P2", "prefix")]
    public async Task ForgedEarlierFaceOrTimelinePrefixIsRejectedByRecoveryAndReplay(string player, string tamper)
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
            evidence is null ? null : Path.Combine(evidence, "moonveil-copy-mighty-forgery-" + player + "-" + tamper));
        try
        {
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
        var item = Assert.Single(driver.State.StackItems, i => i.MightySkill is not null);
        var mighty = item.MightySkill!;
        Assert.Equal("HOST", mighty.Unit.ObjectId);
        Assert.Equal(2, mighty.BeforeMight);
        Assert.Equal(5, mighty.AfterMight);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        var oldHost = mighty.Before.CardObjects["HOST"];
        Assert.Equal("UNL-092/219", oldHost.CardNo);
        Assert.NotEmpty(oldHost.CopyFaceTransitions!);
        CardObjectState forgedHost;
        if (tamper == "face") forgedHost = oldHost with { CardNo = "SFD·125/221" };
        else
        {
            var prefix = oldHost.CopyFaceTransitions!.ToArray();
            Assert.True(prefix[0].Tick > 0);
            prefix[0] = prefix[0] with { Tick = prefix[0].Tick - 1 };
            forgedHost = oldHost with { CopyFaceTransitions = prefix };
        }
        var forgedFrame = mighty.Before with { CardObjects = new Dictionary<string, CardObjectState>(mighty.Before.CardObjects) {
            ["HOST"] = forgedHost } };
        var forgedItem = item with { MightySkill = mighty with { Before = forgedFrame } };
        var forged = driver.State with { StackItems = driver.State.StackItems.Select(i =>
            i.StackItemId == item.StackItemId ? forgedItem : i).ToArray() };
        var sequence = driver.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(forged);
        var prompts = ResolutionResult.BuildPrompts(forged);
        var views = forged.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id,
            forged.Tick, sequence, snapshots[id], forged.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
        var errors = MatchRecoveryValidator.ValidateComponentStateAndViews(forged, views, spectator);
        var fullErrors = MatchRecoveryValidator.Validate(forged.RoomId, sequence, driver.Commands(), driver.Events(),
            views, forged, forged.Tick, spectator);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged,
            new CoreRuleEngine(), default, driver.Events());
        driver.Export("forgery-diagnostic.json", new { tamper, original = driver.State, forged, errors, fullErrors, replay });
        Assert.Contains(errors, e => e.Contains("Mighty transition", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fullErrors, e => e.Contains("Mighty transition", StringComparison.OrdinalIgnoreCase));
        Assert.False(replay.IsMatch);
        }
        finally
        {
            driver.Export("terminal-diagnostic.json", new { initial, final = driver.State,
                commands = driver.Commands(), events = driver.Events(), steps = driver.Steps });
        }
    }
}
