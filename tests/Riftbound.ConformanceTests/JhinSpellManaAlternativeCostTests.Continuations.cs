using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class JhinSpellManaAlternativeCostTests
{
    [Theory]
    [InlineData("UNL-089/219", "RAD·020/167")]
    [InlineData("UNL-089a/219", "RAD·020a/167")]
    public async Task ASpellConfirmsItsActualPaymentAfterAnAdditionalCostReplacement(string jhin, string mordekaiser)
    {
        var initial = ReplacementPosition(jhin, mordekaiser);
        var game = await ConfirmReplacement(initial);
        var play = Assert.Single(game.State.CardPlays.Finalized);
        Assert.Equal(4, play.PaidMana); Assert.Equal(game.State.Tick, play.Tick);
        Assert.Equal($"STACK-{game.Commands()[0].CompletedTick}-SACRIFICE", play.PlayId);
        Assert.NotEqual($"STACK-{play.Tick}-SACRIFICE", play.PlayId);
        Assert.Equal(CommandTypes.PayCost, game.Commands()[^1].CommandType);
        Assert.True(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        Assert.DoesNotContain("MINION", game.State.CardObjects.Keys);
        Assert.Contains("MORDEKAISER", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["MORDEKAISER"].IsExhausted);
        await game.Top();
        Assert.True(Quote(game.State, Play(jhin, true)).CanPay);
        await game.Act("P1", Play(jhin, true)); await game.Top();
        var predict = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(predict.ChoiceId, predict.ChoiceWindow, []));
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("missing-origin")]
    [InlineData("origin-actor")]
    [InlineData("origin-type")]
    [InlineData("origin-source")]
    [InlineData("answer-id")]
    [InlineData("answer-window")]
    [InlineData("answer-actor")]
    [InlineData("answer-type-null")]
    [InlineData("answer-id-null")]
    [InlineData("missing-request")]
    [InlineData("request-id")]
    [InlineData("request-actor")]
    public async Task RecoveryBindsTheCompletedPaymentToItsOriginalPlayAndEveryDecision(string mutation)
    {
        var initial = ReplacementPosition("UNL-089/219", "RAD·020/167");
        var game = await ConfirmReplacement(initial); var state = game.State;
        var commands = game.Commands(); var events = game.Events();
        commands = mutation switch {
            "missing-origin" => commands[1..],
            "origin-actor" => [commands[0] with { PlayerId = "P2" }, ..commands[1..]],
            "origin-type" => [commands[0] with { CommandType = CommandTypes.PassPriority }, ..commands[1..]],
            "origin-source" => [RewriteCommand(commands[0], "sourceObjectId", "JHIN"), ..commands[1..]],
            "answer-id" => [..commands[..^1], RewriteCommand(commands[^1], "paymentId", "REPLACEMENT:0:99")],
            "answer-window" => [..commands[..^1], RewriteCommand(commands[^1], "paymentWindow", "OTHER")],
            "answer-actor" => [..commands[..^1], commands[^1] with { PlayerId = "P2" }], _ => commands };
        if (mutation == "answer-type-null") commands[^1] = commands[^1] with { CommandType = null! };
        if (mutation == "answer-id-null") commands[^1] = RewriteCommand(commands[^1], "paymentId", null);
        if (mutation == "missing-request") events = events.Where(e => e.Event.Kind != "RULE_CHOICE_REQUESTED").ToArray();
        if (mutation is "request-id" or "request-actor")
            events = events.Select(e => e.Event.Kind == "RULE_CHOICE_REQUESTED" ? e with {
                Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) {
                    [mutation == "request-id" ? "choiceId" : "playerId"] = mutation == "request-id" ? "other" : "P2" } } } : e).ToArray();
        var sequence = game.Events()[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, commands, events, views, [],
            state, initial, spectator) { OpeningSeed = initial.Seed };
        frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
        Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, sequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame),
            error => error.Contains("confirmed spell mana history", StringComparison.Ordinal));
        Assert.Contains(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default),
            error => error.Contains("confirmed spell mana history", StringComparison.Ordinal));
        await game.ColdCheckpoint();
    }

    private static RecoveredCommand RewriteCommand(RecoveredCommand command, string property, string? value)
    {
        var raw = JsonSerializer.SerializeToNode(command.RawCommand!.Value, Web)!;
        raw[property] = value;
        return command with { RawCommand = JsonSerializer.SerializeToElement(raw, Web) };
    }

    private static MatchState ReplacementPosition(string jhin, string mordekaiser)
    {
        var state = RadGearTestPosition.Put(Position(jhin, 8), "SACRIFICE", "UNL-173/219", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "MORDEKAISER", mordekaiser, "P1", "BASE");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("OGN·271/298", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["MINION"] = factory.CreateObject("MINION", "P1", "P1") } };
        state = RadUnitEffectTestFixture.Place(state, "MINION", "P1", "BASE");
        foreach (var tax in new[] { "TAX1", "TAX2", "TAX3" })
            state = RadGearTestPosition.Put(state, tax, "VEN·045", "P2", "BASE");
        return state;
    }

    private static async Task<Driver> ConfirmReplacement(MatchState initial)
    {
        var game = new Driver(initial);
        var spell = new PlayCardCommand("SACRIFICE", "UNL-173/219", [],
            OptionalCosts: ["DESTROY_FRIENDLY_POWERFUL_UNIT:MORDEKAISER"]);
        Assert.Equal(4, Quote(initial, spell).Cost!.Mana);
        await game.Act("P1", spell);
        Assert.Empty(game.State.CardPlays.Finalized);
        Assert.False(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        await game.ColdCheckpoint();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SACRIFICE_RECALL:MORDEKAISER:MINION"]));
        return game;
    }
}
