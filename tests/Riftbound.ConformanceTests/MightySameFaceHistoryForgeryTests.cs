using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task SameCurrentFaceStillRequiresTheRealCopyHistoryPrefix(string player)
    {
        var opponent = ThreeBattlefieldTimingProbe.Other(player);
        var initial = ThreeBattlefieldTimingProbe.Position(player, "SFD·221/221", equipment: true);
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
        var game = new StandbyDefendObserverTests.Driver(initial);

        await game.Act(player, new AssembleEquipmentCommand("EQUIP", "HOST", ["ASSEMBLE_YELLOW"]));
        await game.Top();
        var copy = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("ATTACHMENT_COPY_SELECTION", copy.Request.Kind);
        await game.Act(player, new PayCostCommand(copy.Request.Id, "RULE_REPLACEMENT", ["COPY:UNIT"]));
        Assert.Equal("UNL-092/219", game.State.CardObjects["HOST"].CardNo);
        await game.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act(player, new PassFocusCommand());
        await game.Act(opponent, new PassFocusCommand());
        var confirmation = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(player, new ChooseCardsCommand(confirmation.ChoiceId, confirmation.ChoiceWindow, ["EQUIP"]));
        if (game.State.PriorityPlayerId == player) await game.Act(player, new PassPriorityCommand());
        await game.Act(opponent, new TapRuneCommand("REACTION-RUNE"));
        await game.Act(opponent, new PlayCardCommand("GUST", "OGN·169/298", ["UNIT"]));
        await game.Top();
        await game.Top();
        var detach = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var detached = await game.Act(player, new ChooseCardsCommand(detach.ChoiceId, detach.ChoiceWindow, ["EQUIP"]));
        Assert.Single(detached.Events, e => e.Kind == "UNIT_COPY_ENDED");

        var item = Assert.Single(game.State.StackItems, i => i.MightySkill is not null);
        var mighty = item.MightySkill!;
        Assert.Equal(2, mighty.BeforeMight);
        Assert.Equal(5, mighty.AfterMight);
        var current = game.State.CardObjects["HOST"];
        var historical = mighty.After.CardObjects["HOST"];
        Assert.Equal("OGN·001/298", current.CardNo);
        Assert.Equal(current.CardNo, historical.CardNo);
        Assert.Equal(current.ObjectGeneration, historical.ObjectGeneration);
        Assert.Equal(current.CopyFaceTransitions, historical.CopyFaceTransitions);
        var validFrame = VayneRecoveryFrame(initial, game, game.State);
        Assert.Empty(validFrame.ValidationErrors);
        var validReplay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, validFrame.Commands, game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(validFrame.Events));
        Assert.True(validReplay.IsMatch, string.Join("; ", validReplay.Errors));

        var forgedHistory = Assert.IsAssignableFrom<IReadOnlyList<CopyFaceTransition>>(historical.CopyFaceTransitions).ToArray();
        Assert.Equal(2, forgedHistory.Length);
        Assert.True(forgedHistory[0].Tick > 0);
        forgedHistory[0] = forgedHistory[0] with { Tick = forgedHistory[0].Tick - 1 };
        var forgedAfter = mighty.After with { CardObjects = new Dictionary<string, CardObjectState>(mighty.After.CardObjects) {
            ["HOST"] = historical with { CopyFaceTransitions = forgedHistory } } };
        var forgedItem = item with { MightySkill = mighty with { After = forgedAfter } };
        var forged = game.State with { StackItems = game.State.StackItems.Select(i =>
            i.StackItemId == item.StackItemId ? forgedItem : i).ToArray() };
        var frame = VayneRecoveryFrame(initial, game, forged);
        var componentErrors = MatchRecoveryValidator.ValidateComponentStateAndViews(forged, frame.PlayerViews, frame.SpectatorReplayFrame);
        var forgedReplay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, forged,
            new CoreRuleEngine(), default, JournalRoundTrip(frame.Events));
        Assert.False(forgedReplay.IsMatch);
        Assert.Contains(componentErrors, e => e.Contains("Mighty transition", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(frame.ValidationErrors, e => e.Contains("Mighty transition", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
    }
}
