using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.StandbyDefendObserverTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DeckPlayBaseCostWaiverTests
{
    private const string HasteUnit = "UNL-006/219";

    private static async Task<(MatchState Initial, Driver Game, string Id)> Open(bool hook,
        string unit = HasteUnit, bool sacrifice = true)
    {
        var id = hook ? "HOOK_TOP" : "END_UNIT";
        var initial = DeckPlaySequenceTests.TopCard(hook ? HookDeckPlayTests.Position("OGN·208/298")
            : EndDeckPlayTests.Position(), id, unit);
        if (sacrifice)
            initial = HandDiscardObserverTests.Put(initial, "SACRIFICE", "UNL-166/219", "P1", "BASE");
        initial = HandDiscardObserverTests.Put(initial, "FEE-RUNE", "OGN·007/298", "P1", "BASE");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = hook ? new(1, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }) : RunePool.Empty } };
        var game = new Driver(initial);
        if (hook) await HookDeckPlayTests.Activate(game);
        else await game.Act("P1", new EndTurnCommand());
        await game.Top();
        if (hook) await DeckPlaySequenceTests.Select(game, id);
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower); Assert.False(pending.IgnoreAllCosts);
        Assert.Contains("额外费用仍需支付", ResolutionResult.BuildPrompts(game.State)["P1"].Reason);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        return (initial, game, id);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task RealDeckPlaysQuoteFundAndPayTheOptionalHasteFee(bool hook, bool paid)
    {
        var (initial, game, id) = await Open(hook);
        var play = new PlayCardCommand(id, HasteUnit, [],
            OptionalCosts: paid ? [HasteOptionalCostNames.HasteReady] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
        Assert.NotNull(quote.Cost); Assert.Equal(paid ? 1 : 0, quote.Cost.Mana);
        Assert.Equal(paid ? 1 : 0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.Equal(!paid, quote.CanPay);
        if (paid)
        {
            await RadGearTestPosition.Reject(game.State, "P1", play);
            await game.Act("P1", new TapRuneCommand("FEE-RUNE"));
            await game.Act("P1", new RecycleRuneCommand("FEE-RUNE"));
        }
        await game.Act("P1", play);
        Assert.Contains(id, game.State.PlayerZones["P1"].Base);
        Assert.Equal(!paid, game.State.CardObjects[id].IsExhausted);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Theory]
    [InlineData(false, "OGN·208/298", "DESTROY_FRIENDLY_UNIT:SACRIFICE")]
    [InlineData(true, "OGN·208/298", "DESTROY_FRIENDLY_UNIT:SACRIFICE")]
    [InlineData(false, "SFD·044/221", "RETURN_FRIENDLY_EQUIPMENT:GEAR")]
    [InlineData(true, "SFD·044/221", "RETURN_FRIENDLY_EQUIPMENT:GEAR")]
    [InlineData(false, "UNL-166/219", "DESTROY_FRIENDLY_TRAIT_UNIT:SACRIFICE")]
    [InlineData(true, "UNL-166/219", "DESTROY_FRIENDLY_TRAIT_UNIT:SACRIFICE")]
    public async Task RealDeckPlaysStillPayMandatoryDestructionTraitAndEquipmentReturnCosts(bool hook, string unit, string fee)
    {
        var (initial, game, id) = await Open(hook, unit);
        var play = new PlayCardCommand(id, unit, []);
        await RadGearTestPosition.Reject(game.State, "P1", play);
        await game.Act("P1", play with { OptionalCosts = [fee] });
        Assert.Contains(id, game.State.PlayerZones["P1"].Base);
        if (unit == "SFD·044/221") Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        else Assert.Contains("SACRIFICE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AnImpossibleNonresourceCostLeavesTheSelectedCardBanishedAndAllowsTheParentToFinish(bool hook)
    {
        const string unit = "UNL-166/219";
        var (initial, game, id) = await Open(hook, unit, sacrifice: false);
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand(id, unit, []));
        var pending = game.State.PendingEffectPlay!;
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains(id, game.State.PlayerZones["P1"].Banished);
        Assert.DoesNotContain(id, game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(id, game.State.PlayerZones["P1"].Base);
        if (!hook) Assert.Equal("P2", game.State.TurnPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task RefusingANonzeroOptionalFeeKeepsTheCardBanishedWithoutRefundingTheParentCost(bool hook)
    {
        var (initial, game, id) = await Open(hook);
        await RadGearTestPosition.Reject(game.State, "P1", new RefusePlayPaymentCommand(id, HasteUnit, []));
        await game.Act("P1", new RefusePlayPaymentCommand(id, HasteUnit, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains(id, game.State.PlayerZones["P1"].Banished);
        Assert.DoesNotContain(id, game.State.PlayerZones["P1"].Base);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        if (hook) { Assert.True(game.State.CardObjects["GEAR"].IsExhausted); Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard); }
        else Assert.Equal("P2", game.State.TurnPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AControlledTaxBoundaryStillRequiresManaAfterBothBaseComponentsAreWaived(bool hook)
    {
        var (_, game, id) = await Open(hook);
        // Payment component boundary only; this does not claim a real held event.
        var state = game.State with { UntilEndOfTurnEffects = [..game.State.UntilEndOfTurnEffects,
            "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P1"] };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var play = new PlayCardCommand(id, HasteUnit, []);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, play));
        Assert.False(quote.CanPay); Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await RadGearTestPosition.Reject(state, "P1", play);
        var tapped = await engine.ResolveAsync(state, new("deck-waiver-tax-rune", "P1", CommandTypes.TapRune), new TapRuneCommand("FEE-RUNE"), default);
        Assert.True(tapped.Accepted, tapped.ErrorMessage);
        var played = await engine.ResolveAsync(tapped.State, new("deck-waiver-tax-play", "P1", play.CmdType), play, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Contains(id, played.State.PlayerZones["P1"].Base);
        Assert.Equal(RunePool.Empty, played.State.RunePools["P1"]);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(played);
    }

    [Theory]
    [InlineData(false, "all")] [InlineData(false, "mana")] [InlineData(false, "power")]
    [InlineData(true, "all")] [InlineData(true, "mana")] [InlineData(true, "power")]
    public async Task OriginalHistoryRejectsPromotionOrRemovalOfTheExactBaseCostPermission(bool hook, string mutation)
    {
        var (initial, game, id) = await Open(hook);
        var pending = game.State.PendingEffectPlay!;
        var forged = game.State with { PendingEffectPlay = mutation switch {
            "all" => pending with { IgnoreAllCosts = true },
            "mana" => pending with { IgnoreBaseMana = false },
            _ => pending with { IgnoreBasePower = false } } };
        var commands = game.Commands(); var events = game.Events(); var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
        var views = forged.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, forged.Tick, sequence, snapshots[player], forged.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
        Assert.NotEmpty(MatchRecoveryValidator.Validate(forged.RoomId, sequence, commands, events, views, forged, forged.Tick, spectator));
        await game.Act("P1", new PlayCardCommand(id, HasteUnit, []));
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }
}
