using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ObservedDeckPlayWindowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleObservedOfferReturnsToOpenMainAndAllowsTheNextEndStep(bool play)
    {
        var initial = DeckObservationPlayTests.Position();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await CardTrick(driver);
        await CompleteOffer(driver, play);
        AssertOpenMain(driver.State);
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(MatchPhases.TurnEnd, driver.State.Phase);
        Assert.NotEmpty(driver.State.StackItems);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservedOfferKeepsTheOlderSpellClosedAndRestoresItsPriority(bool play)
    {
        var initial = AttackDamageDistributionTests.Spell(DeckObservationPlayTests.Position(), "INSIGHT", "UNL-063/219", "P1");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("TRICK", "OGN·183/298", []));
        await driver.Act("P1", new PlayCardCommand("INSIGHT", "UNL-063/219", ["HOST"]));
        await driver.Top(); await DeckObservationPlayTests.Decide(driver, true);
        await CompleteOffer(driver, play);
        Assert.Equal("TRICK", Assert.Single(driver.State.StackItems).SourceObjectId);
        Assert.Equal(TimingStates.NeutralClosed, driver.State.TimingState);
        Assert.Equal("P1", driver.State.PriorityPlayerId); Assert.Equal("P1", driver.State.ActivePlayerId);
        await RejectEndTurn(driver);
        await driver.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["END_PREFIX"]));
        AssertOpenMain(driver.State);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedBrightFutureOriginalQueueStaysMandatoryUntilBothOriginalPlaysFinish(bool play)
    {
        var initial = DeckPlaySequenceTests.TopCard(DeckPlaySequenceTests.TopCard(
            DeckPlaySequenceTests.Position(DeckPlaySequenceTests.BrightFuture), "P1_TOP", DeckObservationPlayTests.Nocturne),
            "P2_TOP", DeckObservationPlayTests.Nocturne);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(0, 1) } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", DeckPlaySequenceTests.BrightFuture, [])); await driver.Top();
        await DeckObservationPlayTests.Decide(driver, true);
        await SelectFirst(); await DeckObservationPlayTests.Decide(driver, true);
        while (driver.State.PendingCardChoice is not null) await SelectFirst();
        foreach (var player in new[] { "P1", "P2" })
        {
            Assert.Equal(player, driver.State.PendingEffectPlay!.PlayerId);
            await CompleteOffer(driver, play);
            Assert.NotNull(driver.State.PendingEffectPlay);
            await RejectEndTurn(driver);
        }
        foreach (var player in new[] { "P2", "P1" })
        {
            var original = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
            Assert.Null(original.Parent.ObservedPlayPermission); Assert.Equal(player, original.PlayerId);
            var id = original.Sources.Keys.Single();
            await driver.Act(player, new PlayCardCommand(id, driver.State.CardObjects[id].CardNo!, []));
        }
        AssertOpenMain(driver.State);
        await driver.Act("P1", new EndTurnCommand()); Assert.Equal("P2", driver.State.TurnPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);

        async Task SelectFirst()
        {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.LegalObjectIds.First()]));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservedOfferInDianasTriggeredDuelKeepsItsOriginalFocusContinuation(bool play)
    {
        var initial = DeckObservationPlayTests.WithNocturne(OfficialInsightSourceTests.Diana(), "D1", "P2");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", Destination: "BATTLEFIELD:HILL", SourceObjectIds: ["UNIT"]));
        var payment = driver.State.PendingPayment!;
        await driver.Act("P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"])); await driver.Top();
        await DeckObservationPlayTests.Decide(driver, false);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await DeckObservationPlayTests.Decide(driver, true); await CompleteOffer(driver, play);
        Assert.Equal(TimingStates.SpellDuelOpen, driver.State.TimingState); Assert.Equal("P1", driver.State.FocusPlayerId);
        Assert.Equal("P1", driver.State.ActivePlayerId); Assert.Null(driver.State.PriorityPlayerId);
        await RejectEndTurn(driver);
        await driver.Act("P1", new PassFocusCommand()); Assert.Equal("P2", driver.State.FocusPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BattleDuelAttackObservedOfferKeepsTheOriginalAttackerFocus(bool play)
    {
        var initial = DeckObservationPlayTests.WithNocturne(AttackDeckPlayTests.Position(), "ATK_TOP", "P1");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await AttackDeckPlayTests.Attack(driver); await driver.Top(); await AttackDeckPlayTests.Reveal(driver);
        await DeckObservationPlayTests.Decide(driver, true);
        if (driver.State.PendingCardChoice is { } choice)
            await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await CompleteOffer(driver, play);
        Assert.True(driver.State.BattleState.IsActive); Assert.Empty(driver.State.StackItems);
        Assert.Equal(TimingStates.SpellDuelOpen, driver.State.TimingState);
        Assert.Equal("P1", driver.State.FocusPlayerId); Assert.Null(driver.State.PriorityPlayerId);
        await RejectEndTurn(driver);
        await driver.Act("P1", new PassFocusCommand()); Assert.Equal("P2", driver.State.FocusPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndStepObservedOfferFinishesTheOriginalTurnBeforeOpeningTheNextPlayersMain(bool play)
    {
        var initial = DeckObservationPlayTests.Position(); var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new EndTurnCommand()); await driver.Top(); await DeckObservationPlayTests.Decide(driver, true);
        await CompleteOffer(driver, play);
        Assert.Equal("P2", driver.State.TurnPlayerId); Assert.Equal("P2", driver.State.ActivePlayerId);
        Assert.Equal(MatchPhases.Main, driver.State.Phase); Assert.Equal(TimingStates.NeutralOpen, driver.State.TimingState);
        await driver.Act("P2", new EndTurnCommand());
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task CoherentlyClosedFinalViewsStillFailIndependentOriginalActionReplay()
    {
        var initial = DeckObservationPlayTests.Position(); var driver = new StandbyDefendObserverTests.Driver(initial);
        await CardTrick(driver); await CompleteOffer(driver, false); AssertOpenMain(driver.State);
        var forged = driver.State with { TimingState = TimingStates.NeutralClosed };
        var sequence = driver.Events().Last().Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
        var views = forged.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            forged.Tick, sequence, snapshots[player], forged.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
        var errors = MatchRecoveryValidator.Validate(forged.RoomId, sequence, driver.Commands(), driver.Events(), views, forged, forged.Tick, spectator);
        Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(forged.RoomId, forged.Tick, sequence, driver.Commands(), driver.Events(), views, errors, forged, initial, spectator);
        var hash = MatchStateHasher.Hash(forged);
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        Assert.Equal(hash, MatchStateHasher.Hash(forged));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static async Task CardTrick(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1", new PlayCardCommand("TRICK", "OGN·183/298", [])); await driver.Top();
        await DeckObservationPlayTests.Decide(driver, true);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["END_PREFIX"]));
    }
    private static async Task CompleteOffer(StandbyDefendObserverTests.Driver driver, bool play)
    {
        var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        Assert.NotNull(pending.Parent.ObservedPlayPermission);
        if (play) await driver.Act(pending.PlayerId, new PlayCardCommand(pending.Sources.Keys.Single(), DeckObservationPlayTests.Nocturne, []));
        else await driver.Act(pending.PlayerId, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
    }
    private static void AssertOpenMain(MatchState state)
    {
        Assert.Null(state.PendingEffectPlay); Assert.Null(state.ObservedDeckPlays); Assert.Empty(state.StackItems);
        Assert.Equal(MatchPhases.Main, state.Phase); Assert.Equal(TimingStates.NeutralOpen, state.TimingState);
        Assert.Equal(state.TurnPlayerId, state.ActivePlayerId); Assert.Null(state.PriorityPlayerId); Assert.Null(state.FocusPlayerId);
    }
    private static async Task RejectEndTurn(StandbyDefendObserverTests.Driver driver)
    {
        var state = driver.State; var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("blocked-end-turn", "P1", CommandTypes.EndTurn), new EndTurnCommand(), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
