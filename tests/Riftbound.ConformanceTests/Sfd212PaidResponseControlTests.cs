using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

// A separate positive control for the identical physical reaction driver.
// It is intentionally not part of the already-running v1 input.
public sealed class Sfd212PaidResponseControlTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task PaidOpponentReactionCompletesBeforeTheKnownQueuedMinefieldEffect(string player)
    {
        var trace = new ThreeBattlefieldTimingProbe.Trace(
            ThreeBattlefieldTimingProbe.Position(player, "SFD·212/221"), "sfd212-control-" + player);
        try
        {
            var conquest = await trace.Conquer(player);
            Assert.Contains(conquest.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.DoesNotContain(trace.Events, e => e.Kind == "CARDS_MILLED");
            Assert.Contains(trace.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerMill);
            await trace.ReactWithPaidGust(player, TriggerKinds.BattlefieldConquerMill);
            Assert.DoesNotContain(trace.Events, e => e.Kind == "CARDS_MILLED");
            var resolved = await trace.Top();
            Assert.Equal(2, resolved.Events.Where(e => e.Kind == "CARDS_MILLED")
                .Sum(e => Assert.IsType<int>(e.Payload["count"])));
        }
        finally { trace.Save(); }
    }
}
