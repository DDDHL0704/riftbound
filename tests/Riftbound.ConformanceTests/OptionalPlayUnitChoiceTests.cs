using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OptionalPlayUnitChoiceTests
{
    private const string Swindler = "SFD·081/221";
    private const string Captain = "SFD·091/221";
    private const string Return = "OGN·104/298";

    private static MatchState Position(string no, string player = "P1", bool multiplayer = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SOURCE", no, player, "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", Return, player, "HAND");
        if (multiplayer)
            state = state with {
                Seats = new Dictionary<string, string>(state.Seats) { ["P3"] = "P3" },
                ReadyPlayerIds = state.ReadyPlayerIds.Append("P3").ToArray(),
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P3"] = PlayerZones.Empty },
                RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P3"] = RunePool.Empty } };
        return state with { TurnPlayerId = player, ActivePlayerId = player };
    }

    private static CardObjectState[] Gold(MatchState state, string player)
        => state.CardObjects.Values.Where(card => card.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币"
            && state.PlayerZones[player].Base.Contains(card.ObjectId)).ToArray();

    private static async Task Decide(Driver game, string player, bool accept)
    {
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal(player, pending.Request.PlayerId);
        Assert.Equal("EACH_PLAYER_OPTIONAL_EQUIPMENT_TOKEN", pending.Request.Kind);
        Assert.True(game.PromptFor(player).Actionable);
        Assert.All(game.State.Seats.Keys.Where(other => other != player), other => Assert.False(game.PromptFor(other).Actionable));
        var selection = accept ? "PLAY" : "DECLINE";
        await RadGearTestPosition.Reject(game.State, game.State.Seats.Keys.First(other => other != player),
            new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [selection]));
        await RadGearTestPosition.Reject(game.State, player,
            new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["INVALID"]));
        await game.Act(player, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [selection]));
    }

    [Theory]
    [InlineData("P1", false, false)] [InlineData("P1", false, true)]
    [InlineData("P1", true, false)] [InlineData("P1", true, true)]
    [InlineData("P2", false, false)] [InlineData("P2", false, true)]
    [InlineData("P2", true, false)] [InlineData("P2", true, true)]
    public async Task EachPlayerDecidesAtResolutionAndOpponentAcceptanceAloneEarnsTheExtraGold(
        string controller, bool own, bool other)
    {
        var game = new Driver(Position(Swindler, controller));
        var opponent = controller == "P1" ? "P2" : "P1";
        await game.Act(controller, new PlayCardCommand("SOURCE", Swindler, []));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.All(game.State.Seats.Keys, player => Assert.Empty(Gold(game.State, player)));
        await game.Top();
        await Decide(game, controller, own);
        await Decide(game, opponent, other);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal((own ? 1 : 0) + (other ? 1 : 0), Gold(game.State, controller).Length);
        Assert.Equal(other ? 1 : 0, Gold(game.State, opponent).Length);
        Assert.All(game.State.Seats.Keys.SelectMany(player => Gold(game.State, player)), gold => {
            Assert.True(gold.IsExhausted); Assert.Equal(gold.OwnerId, gold.ControllerId);
        });
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "PLAYER_EQUIPMENT_TOKEN_CHOICE"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task MultiplayerCountsOnlyAcceptingOpponentsInCurrentTurnOrder(bool first, bool second)
    {
        // Persisted rooms currently use two seats. The two-seat cases above
        // exercise native recovery/retry; this component case checks the shared
        // instruction with additional opponents and replays original commands.
        var origin = Position(Swindler, "P2", multiplayer: true);
        var state = origin;
        var engine = new CoreRuleEngine();
        var transcript = new List<(PlayerIntent Intent, GameCommand Command, string Events, string Hash)>();
        async Task Act(string player, GameCommand command)
        {
            var intent = new PlayerIntent("multi-" + transcript.Count, player, command.CmdType);
            var result = await engine.ResolveAsync(state, intent, command, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            state = result.State;
            transcript.Add((intent, command, JsonSerializer.Serialize(result.Events), MatchStateHasher.Hash(state)));
        }
        await Act("P2", new PlayCardCommand("SOURCE", Swindler, []));
        for (var pass = 0; state.PendingRuleChoice is null && state.StackItems.Count > 0 && pass < 6; pass++)
            await Act(state.PriorityPlayerId!, new PassPriorityCommand());
        foreach (var decision in new[] { ("P2", false), ("P3", first), ("P1", second) })
        {
            var pending = Assert.IsType<RuleChoiceContinuation>(state.PendingRuleChoice);
            Assert.Equal(decision.Item1, pending.Request.PlayerId);
            await Act(decision.Item1, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT",
                [decision.Item2 ? "PLAY" : "DECLINE"]));
        }
        Assert.Null(state.PendingRuleChoice);
        Assert.Equal((first ? 1 : 0) + (second ? 1 : 0), Gold(state, "P2").Length);
        Assert.Equal(first ? 1 : 0, Gold(state, "P3").Length);
        Assert.Equal(second ? 1 : 0, Gold(state, "P1").Length);
        var replay = origin;
        foreach (var accepted in transcript)
        {
            var result = await engine.ResolveAsync(replay, accepted.Intent, accepted.Command, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            Assert.Equal(accepted.Events, JsonSerializer.Serialize(result.Events));
            Assert.Equal(accepted.Hash, MatchStateHasher.Hash(result.State));
            replay = result.State;
        }
    }

    [Fact]
    public async Task ReturningTheSwindlerDuringResponsesPreservesEveryPlayersDecisionAndGold()
    {
        var game = new Driver(Position(Swindler));
        await game.Act("P1", new PlayCardCommand("SOURCE", Swindler, []));
        await game.Act("P1", new PlayCardCommand("RETURN", Return, ["SOURCE"]));
        await game.Top(); Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        await Decide(game, "P1", true); await Decide(game, "P2", true);
        Assert.Equal(2, Gold(game.State, "P1").Length); Assert.Single(Gold(game.State, "P2"));
        await game.Replay();
    }

    [Theory]
    [InlineData("DRAW_1")] [InlineData("SELF_BOON")] [InlineData("")]
    public async Task CaptainPlaysWithoutPreselectionThenChoosesOrDeclinesItsEarnedTrigger(string mode)
    {
        var initial = Position(Captain);
        var beforeHand = initial.PlayerZones["P1"].Hand.Where(id => id != "SOURCE").ToArray();
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SOURCE", Captain, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("UNIT_EVENT_MODE_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(new[] { "DRAW_1", "SELF_BOON" }, choice.LegalObjectIds);
        Assert.Equal(beforeHand, game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["SOURCE"].Tags);
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DRAW_1"]));
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["INVALID"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, mode.Length == 0 ? [] : [mode]));
        Assert.Equal(beforeHand, game.State.PlayerZones["P1"].Hand); Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["SOURCE"].Tags);
        Assert.Null(game.State.PendingPayment); Assert.Null(game.State.PendingCardChoice);
        if (mode.Length > 0) await game.Top();
        Assert.Equal(beforeHand.Length + (mode == "DRAW_1" ? 1 : 0), game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(mode == "SELF_BOON", game.State.CardObjects["SOURCE"].Tags.Contains(CardObjectTags.Boon));
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Theory]
    [InlineData("DRAW_1")] [InlineData("SELF_BOON")]
    public async Task TheCaptainsChosenIndependentInstructionSurvivesSourceReturnWithoutBuffingANewObject(string mode)
    {
        var game = new Driver(Position(Captain));
        await game.Act("P1", new PlayCardCommand("SOURCE", Captain, []));
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [mode]));
        await game.Act("P1", new PlayCardCommand("RETURN", Return, ["SOURCE"]));
        await game.Top(); Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Hand);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Top();
        Assert.Equal(hand + (mode == "DRAW_1" ? 1 : 0), game.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["SOURCE"].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData("SELF_BOON")] [InlineData("")]
    public async Task RecoveryRejectsAChangedOrErasedAcceptedOptionalMode(string mode)
    {
        var game = new Driver(Position(Captain));
        await game.Act("P1", new PlayCardCommand("SOURCE", Captain, []));
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DRAW_1"]));
        var forged = game.State with { StackItems = game.State.StackItems.Select(item =>
            item.UnitEvent?.CardNo == Captain ? item with { UnitEvent = item.UnitEvent with {
                Mode = mode.Length == 0 ? null : mode } } : item).ToArray() };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Replay();
    }

}
