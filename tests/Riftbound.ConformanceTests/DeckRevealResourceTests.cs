using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DeckRevealResourceTests
{
    private const string Beast = GaiBeastGroupMightTests.Beast;

    [Theory]
    [InlineData("如果我从你的主牌堆中被展示，则你{{获得}}{{2}}。", 2)]
    [InlineData("如果我从你的主牌堆中被展示，则你{{获得}}{{3}}。", 3)]
    [InlineData("如果我从你的主牌堆中被查看，则你{{获得}}{{2}}。", 0)]
    [InlineData("如果我从你的手牌中被展示，则你{{获得}}{{2}}。", 0)]
    public void OnlyTheMainDeckRevealTemplateParsesItsPrintedMana(string text, int amount)
    {
        var specs = StaticAbilityParser.Parse(text, []).Where(spec => spec.Kind == StaticAbilityKinds.MainDeckRevealMana).ToArray();
        if (amount == 0) Assert.Empty(specs); else Assert.Equal(amount, Assert.Single(specs).Amount);
    }

    [Theory]
    [InlineData("SFD·187/221")] [InlineData("SFD·243/221")]
    public async Task ARealVoidBurrowerRevealPaysForPlayingThatSameBeast(string legend)
    {
        var game = new Driver(LegendPosition(legend, Beast, "SFD·042/221")); await RevealLegend(game);
        Assert.Equal(6, game.State.RunePools["P1"].Mana); Assert.Single(Gains(game));
        await Select(game, "DECK0");
        var play = new PlayCardCommand("DECK0", Beast, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(6, quote.Cost!.Mana);
        await game.Act("P1", play); Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Contains("DECK0", game.State.PlayerZones["P1"].Base);
        await game.Top(); Assert.Single(Gains(game)); await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsActualRevealGivesOnlyTheDeckOwnerTheMana()
    {
        var initial = DeckPlaySequenceTests.TopCard(DeckPlaySequenceTests.Position(DeckPlaySequenceTests.BlindFury), "P2_TOP", Beast);
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("PARENT", DeckPlaySequenceTests.BlindFury, []));
        var paid = game.State.RunePools["P1"].Mana; await game.Top();
        Assert.Equal(paid, game.State.RunePools["P1"].Mana); Assert.Equal(2, game.State.RunePools["P2"].Mana);
        Assert.Equal("P2", Assert.Single(Gains(game)).Event.Payload["playerId"]);
        await Select(game, "P2_TOP"); await game.Act("P1", new PlayCardCommand("P2_TOP", Beast, [])); await game.Top();
        Assert.Equal("P1", game.State.CardObjects["P2_TOP"].ControllerId);
        Assert.Equal("P2", game.State.CardObjects["P2_TOP"].OwnerId); Assert.Single(Gains(game)); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LookingAtOrNormallyDrawingTheBeastDoesNotGrantRevealMana(bool draw)
    {
        var card = draw ? "OGN·048/298" : DeckPlaySequenceTests.BrightFuture;
        var initial = DeckPlaySequenceTests.TopCard(DeckPlaySequenceTests.Position(card), "P1_TOP", Beast);
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("PARENT", card, []));
        var mana = game.State.RunePools["P1"].Mana; await game.Top();
        Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Empty(Gains(game));
        if (draw) Assert.Contains("P1_TOP", game.State.PlayerZones["P1"].Hand);
        else Assert.NotNull(game.State.PendingCardChoice);
        await game.Replay();
    }

    [Fact]
    public async Task TheMandatoryGainCommitsOnceBeforeAnOptionalObservationPause()
    {
        var game = new Driver(LegendPosition("SFD·187/221", DeckObservationPlayTests.Nocturne, Beast));
        await RevealLegend(game); var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("DECK_OBSERVATION_BANISH", pending.Request.Kind);
        Assert.Equal(6, game.State.RunePools["P1"].Mana); Assert.Equal(6, pending.Request.Preview!.RunePools["P1"].Mana);
        Assert.Single(Gains(game)); Assert.Equal("HIDDEN", Assert.Single(Gains(game)).Event.Payload["sourceObjectId"]);
        var forged = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
            ["P1"] = game.State.RunePools["P1"] with { Mana = 8 } } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["KEEP"]));
        Assert.Equal(6, game.State.RunePools["P1"].Mana); Assert.Single(Gains(game));
        await Select(game, "DECK1"); await game.Act("P1", new PlayCardCommand("DECK1", Beast, [])); await game.Top();
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Single(Gains(game)); await game.Replay();
    }

    [Fact]
    public async Task TwoActuallyRevealedBeastsGrantFourBeforeThePlaySelection()
    {
        var game = new Driver(LegendPosition("SFD·187/221", Beast, Beast)); await RevealLegend(game);
        Assert.Equal(8, game.State.RunePools["P1"].Mana); Assert.Equal(2, Gains(game).Length);
        await Select(game, "DECK0"); await game.Act("P1", new PlayCardCommand("DECK0", Beast, [])); await game.Top();
        Assert.Equal(2, game.State.RunePools["P1"].Mana); Assert.Equal(2, Gains(game).Length); await game.Replay();
    }

    [Fact]
    public async Task SimultaneousRevealGrantsTheOtherOwnersManaBeforeTheFirstOptionalChoice()
    {
        var initial = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·294/298"), "GEAR", "RAD·111/167", "P1", "BASE");
        initial = initial with { Phase = MatchPhases.TurnStart, TimingState = TimingStates.NeutralClosed,
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["P1-D0"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-D0", DeckObservationPlayTests.Nocturne, "P1"),
                ["P2-D0"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-D0", Beast, "P2") } };
        var game = new Driver(initial); await game.Act("P1", new PassPriorityCommand());
        var before = game.State.RunePools["P2"].Mana; await game.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal(before + 2, game.State.RunePools["P2"].Mana);
        Assert.Equal(before + 2, pending.Request.Preview!.RunePools["P2"].Mana); Assert.Single(Gains(game));
        await game.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["KEEP"]));
        // CN167: completing the beginning skill advances to Main and empties
        // every player's pool. The mandatory gain still precedes that cleanup.
        Assert.Equal(MatchPhases.Main, game.State.Phase); Assert.Equal(0, game.State.RunePools["P2"].Mana);
        Assert.Single(Gains(game)); var trace = game.Events();
        var gainIndex = Array.FindIndex(trace, e => e.Event.Kind == "MANA_GAINED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceCardNo"), Beast));
        var clearIndex = Array.FindLastIndex(trace, e => e.Event.Kind == "RUNE_POOL_CLEARED");
        Assert.True(clearIndex > gainIndex); await game.Replay();
    }

    [Fact]
    public async Task ActualMovementRevealGrantsManaBeforeRecyclingTheBeast()
    {
        var game = new Driver(ApprenticeBlacksmithRevealTests.Position(Beast, 2));
        await ApprenticeBlacksmithRevealTests.Move(game); var before = game.State.RunePools["P1"].Mana; await game.Top();
        Assert.Equal(before + 2, game.State.RunePools["P1"].Mana); Assert.Equal("P1-D0", game.State.PlayerZones["P1"].MainDeck[^1]);
        Assert.Single(Gains(game)); await game.Replay();
    }

    [Fact]
    public async Task AnUnsupportedRevealGainRejectsTheWholeCommandWithoutEventsOrMutation()
    {
        var initial = DeckPlaySequenceTests.TopCard(DeckPlaySequenceTests.Position(DeckPlaySequenceTests.BlindFury), "P2_TOP", Beast);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(int.MaxValue, 0) } };
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("PARENT", DeckPlaySequenceTests.BlindFury, []));
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        var before = MatchStateHasher.Hash(game.State);
        var result = await new CoreRuleEngine().ResolveAsync(game.State,
            new("reveal-limit", game.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(before, MatchStateHasher.Hash(game.State)); await game.Replay();
    }

    private static RecoveredEvent[] Gains(Driver game) => game.Events().Where(e => e.Event.Kind == "MANA_GAINED"
        && Equals(e.Event.Payload.GetValueOrDefault("sourceCardNo"), Beast)).ToArray();

    private static MatchState LegendPosition(string legend, params string[] top)
    {
        var initial = OfficialLegendDeckPlayTests.Position(legend, top: top);
        return initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 2 }) } };
    }
    private static async Task RevealLegend(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        var cost = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(cost.PlayerId, new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, ["LEGEND"])); await game.Top();
    }
    private static Task Select(Driver game, string id)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        return game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
    }
}
