using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN Radiance judge FAQ p9: Gate-granted Echo plus Meditation's paid
// additional cost draws three cards, not two or four. The grant is earned
// through the printed equipment ability, not a fabricated turn marker.
public sealed class MeditationEchoAdditionalCostTests
{
    private const string ExhaustAlly = "EXHAUST_FRIENDLY_UNIT:ALLY";

    [Theory]
    [InlineData(false, false, false, 1)]
    [InlineData(true, false, false, 2)]
    [InlineData(false, true, false, 2)]
    [InlineData(true, true, false, 3)]
    [InlineData(true, true, true, 3)]
    public async Task GateEchoAndMeditationAdditionalCostAreIndependent(
        bool echo, bool exhaust, bool reverse, int draws)
    {
        var initial = Position();
        var game = new StandbyDefendObserverTests.Driver(initial);
        await Grant(game);
        var choices = new List<string>();
        if (echo) choices.Add("ECHO");
        if (exhaust) choices.Add(ExhaustAlly);
        if (reverse) choices.Reverse();
        var command = new PlayCardCommand("UNIT", "OGN·048/298", [], OptionalCosts: choices);
        var before = game.State;
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(before, "P1", PlayCostPreviewTests.Request(before, command));
        var direct = await engine.ResolveAsync(before, new("meditation-direct", "P1", CommandTypes.PlayCard), command, default);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(game.State));
        Assert.True(direct.Accepted, $"Core rejected: {direct.ErrorCode}: {direct.ErrorMessage}; preview: {quote.ErrorCode}: {quote.Message}");
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2 + (echo ? 2 : 0), quote.Cost!.Mana);
        var played = await game.Act("P1", command);
        Assert.Equal(20 - 2 - (echo ? 2 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower); // Gate costs one power.
        Assert.Equal(exhaust, game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(exhaust ? 1 : 0, played.Events.Count(e => e.Kind == "UNIT_EXHAUSTED"));
        if (echo && exhaust) Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount);
        var completed = await game.Top();
        Assert.Equal(Enumerable.Range(1, draws).Select(i => "P1-DRAW-" + i), game.State.PlayerZones["P1"].Hand);
        Assert.Equal(3 - draws, game.State.PlayerZones["P1"].MainDeck.Count);
        if (echo && exhaust) Assert.Equal(2, completed.Events.Count(e => e.Kind == "SPELL_EXECUTION_COMPLETED"));
        Assert.Empty(game.State.StackItems);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Theory]
    [InlineData("enemy")]
    [InlineData("exhausted")]
    [InlineData("duplicate-exhaust")]
    [InlineData("duplicate-echo")]
    [InlineData("unknown-echo")]
    [InlineData("underfunded")]
    public async Task InvalidCompoundCostNeverChargesOrChangesState(string branch)
    {
        var initial = Position();
        if (branch == "exhausted") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ALLY"] = initial.CardObjects["ALLY"] with { IsExhausted = true } } };
        if (branch == "underfunded") initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(3, 20) } };
        var game = new StandbyDefendObserverTests.Driver(initial);
        await Grant(game);
        var choices = new List<string> { "ECHO", branch == "enemy" ? "EXHAUST_FRIENDLY_UNIT:ENEMY" : ExhaustAlly };
        if (branch == "duplicate-exhaust") choices.Add(ExhaustAlly);
        if (branch == "duplicate-echo") choices.Add("ECHO");
        if (branch == "unknown-echo") choices[0] = "ECHO:GRANTED:99";
        var command = new PlayCardCommand("UNIT", "OGN·048/298", [], OptionalCosts: choices);
        var before = game.State;
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(before, "P1", PlayCostPreviewTests.Request(before, command));
        var rejected = await engine.ResolveAsync(before, new("invalid-meditation", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(quote.CanPay);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(game.State));
    }

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position("OGN·048/298");
        return state with { RoomId = "MEDITATION-ECHO-ADDITIONAL", CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·078/221", "P1") } };
    }

    private static async Task Grant(StandbyDefendObserverTests.Driver game)
    {
        await game.Act("P1", new ActivateAbilityCommand("GEAR", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, []));
        await game.Top();
        Assert.Single(game.State.UntilEndOfTurnEffects, e => EchoCostRules.IsGrant(e, "P1"));
    }
}
