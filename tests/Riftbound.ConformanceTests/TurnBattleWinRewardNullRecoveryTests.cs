using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class TurnBattleWinRewardNullRecoveryTests
{
    [Theory]
    [InlineData(false, "list")] [InlineData(true, "list")]
    [InlineData(false, "first")] [InlineData(true, "first")]
    [InlineData(false, "last")] [InlineData(true, "last")]
    public async Task ARealRewardRejectsNullEffectsInEitherRecoveryFrameWithoutThrowing(bool inOrigin, string malformed)
    {
        var state = RadGearTestPosition.Put(MovingPerchOpponentTokenTests.Position(),
            "NULL-REWARD-SPELL", "UNL-095/219", "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("NULL-REWARD-SPELL", "UNL-095/219", ["ALLY"]));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("PERCH", "UNL-130/219", []));
        var opponent = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(opponent.ChoiceId, opponent.ChoiceWindow, ["P2"]));
        await game.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        var frame = inOrigin ? pending.Origin : game.State;
        Assert.Contains(frame.UntilEndOfTurnEffects,
            effect => effect.StartsWith("TURN_BATTLE_WIN_REWARD:", StringComparison.Ordinal));
        IReadOnlyList<string> effects = malformed switch {
            "list" => null!, "first" => [null!, .. frame.UntilEndOfTurnEffects],
            _ => [.. frame.UntilEndOfTurnEffects, null!] };
        var corrupted = frame with { UntilEndOfTurnEffects = effects };
        var forged = inOrigin ? game.State with { PendingRuleChoice = pending with { Origin = corrupted } } : corrupted;
        var before = MatchStateHasher.HashValue(game.State);
        var errors = ArmamentGrantedCombatTriggerTests.Errors(game, forged);
        Assert.Contains(errors, error => error.Contains(
            "turn battle-win reward history contains a null effect list or value", StringComparison.Ordinal));
        Assert.Equal(before, MatchStateHasher.HashValue(game.State));
        await game.Replay();
    }
}
