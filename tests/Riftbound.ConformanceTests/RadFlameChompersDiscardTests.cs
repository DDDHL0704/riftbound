using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadFlameChompersDiscardTests
{
    private const string FlameChompers = "OGN·006/298";
    private const string RedRune = "OGN·007/298";
    private const string Chempunk = "OGN·003/298";
    private const string Chompers = "H1";

    [Fact]
    public async Task RedPowerMayReplaceTheMonasteryDiscardWithARealBasePlay()
    {
        var initial = Position(new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }));
        var game = new Driver(initial);
        await OpenDiscard(game);

        var pending = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
        await game.Act("P1", new ChooseHandCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["H1"]));
        var replacement = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("FLAME_CHOMPERS_DISCARD_REPLACEMENT", replacement.Request.Kind);
        Assert.Contains(replacement.Request.Options, option => option.Id == "PAY_RED");
        Assert.Contains(replacement.Request.Options, option => option.Id == "DECLINE");

        await game.ColdCheckpoint();
        await game.Act("P1", Payment(game, "P1", "PAY_RED"));

        Assert.Null(game.State.PendingHandChoice);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("H1", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("H1", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("H1", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Assert.Equal(1, game.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "CARD_PLAYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "H1")
            && Equals(entry.Event.Payload.GetValueOrDefault("mode"), "DISCARD_REPLACEMENT"));
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "COST_PAID"
            && Equals(entry.Event.Payload.GetValueOrDefault("reason"), "OGN_FLAME_CHOMPERS_DISCARD_ALT_PLAY_UNIT")
            && Equals(entry.Event.Payload.GetValueOrDefault("powerByTrait") is IReadOnlyDictionary<string, int> byTrait
                ? byTrait.GetValueOrDefault(RuneTrait.Red) : null, 1));
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "UNIT_PLAYED_TO_BASE"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "H1"));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DISCARDED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "H1"));
        var resolved = Assert.Single(game.State.CardPlays.Resolved, play => play.Source.ObjectId == "H1");
        Assert.Equal(FlameChompers, resolved.CardNo);
        Assert.Equal(1, resolved.Source.Generation);
        await game.Replay();
    }

    [Fact]
    public async Task DecliningTheReplacementKeepsTheOrdinaryDiscardAndCallRuneFlow()
    {
        var initial = Position(new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }));
        var game = new Driver(initial);
        await OpenDiscard(game);

        var pending = game.State.PendingHandChoice!;
        await game.Act("P1", new ChooseHandCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["H1"]));
        await game.Act("P1", Payment(game, "P1", "DECLINE"));

        Assert.Null(game.State.PendingHandChoice);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("H1", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("H1", game.State.PlayerZones["P1"].Base);
        Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "CARD_DISCARDED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "H1"));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_PLAYED_TO_BASE"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "H1"));
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "RUNES_CALLED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "BF"));
        await game.Replay();
    }

    [Fact]
    public async Task UnrelatedColoredPowerDoesNotOfferTheReplacement()
    {
        var pool = new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 });
        var initial = Position(pool);
        var game = new Driver(initial);
        await OpenDiscard(game);

        var pending = game.State.PendingHandChoice!;
        await game.Act("P1", new ChooseHandCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["H1"]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("H1", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("H1", game.State.PlayerZones["P1"].Base);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "CARD_DISCARDED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "H1"));
        await game.Replay();
    }

    [Fact]
    public async Task ReplacementChoiceRejectsWrongPlayerAndForgedAnswerWithoutMutation()
    {
        var initial = Position(new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }));
        var game = new Driver(initial);
        await OpenDiscard(game);
        var hand = game.State.PendingHandChoice!;
        await game.Act("P1", new ChooseHandCardsCommand(hand.ChoiceId, hand.ChoiceWindow, ["H1"]));
        var pay = Payment(game, "P1", "PAY_RED");
        await Reject(game, "P2", pay);
        await Reject(game, "P1", pay with { PaymentChoiceIds = ["FORGED"] });
        await game.Act("P1", pay);
        Assert.Contains("H1", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ChempunkOffersPaymentFromAnAvailableRedRuneWithAnEmptyPool(bool pay, bool exhausted)
    {
        var state = ChempunkPosition();
        state = Put(Put(state, "RED-1", RedRune, "P1", "BASE"), "RED-2", RedRune, "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RED-2"] = state.CardObjects["RED-2"] with { IsExhausted = exhausted } } };
        var game = new Driver(state);
        await PlayChempunk(game);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Contains(Chompers, game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingHandChoice);
        Assert.Equal("FLAME_CHOMPERS_DISCARD_REPLACEMENT", game.State.PendingRuleChoice?.Request.Kind);
        Assert.False(game.PromptFor("P2").Actionable);
        Assert.DoesNotContain(Chompers, JsonSerializer.Serialize(game.PromptFor("P2")));
        Assert.DoesNotContain("嚼火者", JsonSerializer.Serialize(game.PromptFor("P2")));

        var candidate = PayCandidate(game, "P1");
        var choices = Assert.Single(candidate.SelectionSteps!).Choices;
        Assert.Equal(new[] { "PAY_RED:RED-1", "PAY_RED:RED-2", "DECLINE" }, choices.Select(choice => choice.Id));
        Assert.Equal("RED-2", Assert.Single(choices[1].ObjectIds));
        await game.ColdCheckpoint();
        await game.Act("P1", Payment(game, "P1", pay ? "PAY_RED:RED-2" : "DECLINE"));

        Assert.Null(game.State.PendingRuleChoice);
        Assert.DoesNotContain(Chompers, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(pay, game.State.PlayerZones["P1"].Base.Contains(Chompers));
        Assert.Equal(!pay, game.State.PlayerZones["P1"].Graveyard.Contains(Chompers));
        Assert.Equal(pay, game.State.PlayerZones["P1"].RuneDeck.Contains("RED-2"));
        Assert.Contains("RED-1", game.State.PlayerZones["P1"].Base);
        Assert.Equal(pay ? 2 : 1, game.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(pay ? 1 : 0, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.Equal(pay ? 1 : 0, game.Events().Count(e => e.Event.Kind == "RUNE_RECYCLED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "RED-2")));
        Assert.Equal(!pay, game.Events().Any(IsChompersDiscard));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ChempunkDiscardsImmediatelyWithoutRedPowerOrARedRune()
    {
        var game = new Driver(ChempunkPosition());
        await PlayChempunk(game);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.DoesNotContain(CommandTypes.PayCost, game.PromptFor("P1").EnabledActions());
        Assert.Contains(Chompers, game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.Events(), IsChompersDiscard);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUnitPlayPowerSourceCanPayTheRedCost(bool restricted)
    {
        var pool = restricted ? RunePool.Empty with { UnitOrEquipmentPlayOnlyPower = 1 } : new RunePool(0, 1);
        var game = new Driver(ChempunkPosition(pool));
        await PlayChempunk(game);
        await game.Act("P1", Payment(game, "P1", "PAY_RED"));
        Assert.Contains(Chompers, game.State.PlayerZones["P1"].Base);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnOpponentsRevealAndDiscardLetsTheHandOwnerChooseAndPay()
    {
        const string dragon = "OGN·192/298";
        var state = VenUnitEntryBasicTests.Position(dragon);
        state = Put(Put(state, Chompers, FlameChompers, "P2", "HAND"), "P2-RED", RedRune, "P2", "BASE");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = RunePool.Empty } };
        var game = new Driver(state);
        await game.PlayQuoted("P1", new PlayCardCommand("UNIT", dragon, []));
        await game.Top();
        await Choose(game, "P1", Chompers);

        Assert.False(game.PromptFor("P1").Actionable);
        var pay = Payment(game, "P2", "PAY_RED:P2-RED");
        await Reject(game, "P1", pay);
        await game.Act("P2", pay);
        Assert.Contains(Chompers, game.State.PlayerZones["P2"].Base);
        Assert.Contains("P2-RED", game.State.PlayerZones["P2"].RuneDeck);
        Assert.DoesNotContain(game.Events(), IsChompersDiscard);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AReplacedDiscardStillPaysTheTriggerCostBeforeResponses()
    {
        const string mordekaiser = "RAD·137/167";
        var state = VenUnitEntryBasicTests.Position(mordekaiser);
        state = Put(Put(state, Chompers, FlameChompers, "P1", "HAND"), "RED", RedRune, "P1", "BASE");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 2) } };
        var game = new Driver(state);
        await game.PlayQuoted("P1", new PlayCardCommand("UNIT", mordekaiser, []));
        await Choose(game, "P1", "ENEMY");
        await Choose(game, "P1", Chompers);
        await game.Act("P1", Payment(game, "P1", "PAY_RED:RED"));

        Assert.True(Assert.Single(game.State.StackItems).FieldContext!.PaidUnitTrigger!.CostPaid);
        Assert.Contains(Chompers, game.State.PlayerZones["P1"].Base);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(game.Events(), IsChompersDiscard);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheOriginalPlayKeepsItsDiscardDiscountWhenTheCardIsPlayedInstead()
    {
        const string pirate = "OGN·002/298";
        var state = Put(Put(Create(), "PIRATE", pirate, "P1", "HAND"), Chompers, FlameChompers, "P1", "HAND");
        state = Put(state, "RED", RedRune, "P1", "BASE");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(4, 0) } };
        var game = new Driver(state);
        await game.PlayQuoted("P1", new PlayCardCommand("PIRATE", pirate, [], OptionalCosts: ["DISCARD_HAND_CARD:H1"]));
        await game.Act("P1", Payment(game, "P1", "PAY_RED:RED"));
        Assert.Contains("PIRATE", game.State.PlayerZones["P1"].Base);
        Assert.Contains(Chompers, game.State.PlayerZones["P1"].Base);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(1, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.DoesNotContain(game.Events(), IsChompersDiscard);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AGraveyardConquestCostRetainsItsAcceptedDiscardSelectionAcrossRecovery()
    {
        var state = Put(Put(Create(), "ROCKET", "OGN·252/298", "P1", "GRAVEYARD"), Chompers, FlameChompers, "P1", "HAND");
        state = Put(state, "P1-FURY-RUNE-COST", RedRune, "P1", "BASE");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        await Choose(game, "P1", Chompers);
        await game.Act("P1", Payment(game, "P1", "PAY_RED:P1-FURY-RUNE-COST"));
        await game.ColdCheckpoint();
        Assert.Contains(Chompers, game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(game.Events(), IsChompersDiscard);
        await game.Top();
        Assert.Contains("ROCKET", game.State.PlayerZones["P1"].Hand);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task SimultaneousDiscardsLetThePlayerChooseWhichCopyUsesTheOnlyRedRune()
    {
        const string rewind = "OGN·201/298";
        var state = Put(Put(Create(), "REWIND", rewind, "P1", "HAND"), Chompers, FlameChompers, "P1", "HAND");
        state = Put(Put(state, "SECOND", FlameChompers, "P1", "HAND"), "RED", RedRune, "P1", "BASE");
        state = Put(state, "OPPONENT-HAND", "OGN·019/298", "P2", "HAND");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 1) } };
        var game = new Driver(state);
        await game.PlayQuoted("P1", new PlayCardCommand("REWIND", rewind, []));
        await game.Top();
        await game.Act("P1", Payment(game, "P1", "SECOND"));
        await game.Act("P1", Payment(game, "P1", "PAY_RED:RED"));

        Assert.Contains("SECOND", game.State.PlayerZones["P1"].Base);
        Assert.Contains(Chompers, game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("OPPONENT-HAND", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(4, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(4, game.State.PlayerZones["P2"].Hand.Count);
        await game.ColdCheckpoint();
    }

    private static MatchState ChempunkPosition(RunePool? pool = null)
    {
        var state = Put(Put(Create(), "CHEMPUNK", Chempunk, "P1", "HAND"), Chompers, FlameChompers, "P1", "HAND");
        state = Put(Put(state, "BLUE-1", "OGN·089/298", "P1", "BASE"), "BLUE-2", "OGN·089/298", "P1", "BASE");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool ?? RunePool.Empty } };
    }

    private static async Task PlayChempunk(Driver game)
    {
        await game.PlayQuoted("P1", new PlayCardCommand("CHEMPUNK", Chempunk, [Chompers],
            OptionalCosts: ["TAP_RUNE:BLUE-1", "TAP_RUNE:BLUE-2"]));
        await game.Top();
    }

    private static bool IsChompersDiscard(RecoveredEvent e)
        => e.Event.Kind == "CARD_DISCARDED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), Chompers);

    private static ActionPromptCandidateDto PayCandidate(Driver game, string player)
        => Assert.Single(game.PromptFor(player).Candidates!, candidate => candidate.Enabled && candidate.Action == CommandTypes.PayCost);

    private static PayCostCommand Payment(Driver game, string player, string id)
    {
        var candidate = PayCandidate(game, player);
        Assert.Contains(Assert.Single(candidate.SelectionSteps!).Choices, choice => choice.Id == id);
        var metadata = candidate.Metadata!;
        Assert.Equal("RULE_REPLACEMENT", metadata["paymentWindow"]);
        return new(Assert.IsType<string>(metadata["paymentId"]), "RULE_REPLACEMENT", [id]);
    }

    private static Task Choose(Driver game, string player, string id)
    {
        var candidate = Assert.Single(game.PromptFor(player).Candidates!,
            option => option.Enabled && option.Action == CommandTypes.ChooseCards);
        var metadata = candidate.Metadata!;
        return game.Act(player, new ChooseCardsCommand(Assert.IsType<string>(metadata["choiceId"]),
            Assert.IsType<string>(metadata["choiceWindow"]), [id]));
    }

    private static async Task OpenDiscard(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        await game.Top();
        Assert.NotNull(game.State.PendingHandChoice);
        Assert.Equal("BATTLEFIELD_CONQUER_DISCARD_CALL_RUNE", game.State.PendingHandChoice!.EffectKind);
    }

    private static MatchState Position(RunePool pool)
    {
        var state = ConquestLifecycleRegressionTests.State("RAD·166/167", 1);
        var cards = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cards["H1"] = OfficialGarenConquestBattlefieldCountTests.Printed("H1", FlameChompers, "P1");
        state = state with {
            CardObjects = cards,
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool }
        };
        return Put(state, "P1-MONASTERY-RUNE", "OGN·007/298", "P1", "RUNE_DECK");
    }

    private static Task Reject(Driver game, string player, PayCostCommand command)
    {
        var prompt = game.PromptFor(player);
        var raw = JsonSerializer.SerializeToElement(new { command.CmdType, command.PaymentId, command.PaymentWindow,
            command.PaymentChoiceIds, prompt.PromptId, prompt.SnapshotTick }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return game.ActRetained(new(player, "invalid-discard-replacement-" + game.Commands().Length, raw), expectedAccepted: false);
    }
}
