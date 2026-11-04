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
        Assert.Equal(1, replacement.Request.PowerCost);
        Assert.Contains(replacement.Request.Options, option => option.Id == "PAY_RED");
        Assert.Contains(replacement.Request.Options, option => option.Id == "DECLINE");

        await game.ColdCheckpoint();
        await game.Act("P1", new PayCostCommand(replacement.Request.Id, "RULE_REPLACEMENT", ["PAY_RED"]));

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
            && Equals(entry.Event.Payload.GetValueOrDefault("objectId"), "H1"));
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
        var replacement = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        await game.Act("P1", new PayCostCommand(replacement.Request.Id, "RULE_REPLACEMENT", ["DECLINE"]));

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

    [Theory]
    [InlineData("generic")]
    [InlineData("blue")]
    public async Task OnlyRedPowerOffersTheReplacement(string poolKind)
    {
        var pool = poolKind == "generic"
            ? new RunePool(0, 1)
            : new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 });
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
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        var before = MatchStateHasher.Hash(game.State);

        await Reject(game.State, "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["PAY_RED"]));
        await Reject(game.State, "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["FORGED"]));
        Assert.Equal(before, MatchStateHasher.Hash(game.State));

        await game.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["PAY_RED"]));
        Assert.Contains("H1", game.State.PlayerZones["P1"].Base);
        await game.Replay();
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

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new("flame-chompers-invalid", player, command.CmdType),
            command,
            default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
