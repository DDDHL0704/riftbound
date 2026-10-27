using Riftbound.Contracts;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Flow = Riftbound.ConformanceTests.LastBreathDrawBoundaryTests;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathBattlefieldDamageBoundaryTests
{
    [Fact]
    public async Task ARealPaidFaceDownStandbyPrintingIsNotAnAreaDamageUnit()
    {
        var initial = Flow.Add(Position(), "SECRET", "UNL-003/219", "P1", "HAND"); var game = new Driver(initial);
        try
        {
            await Flow.Tap(game, "P1"); await game.Act("P1", new RecycleRuneCommand("P1-RUNE-6"));
            await game.Act("P1", new PlayCardCommand("UNIT", Unit, [], Destination: "BATTLEFIELD:BF"));
            await game.Act("P1", new RecycleRuneCommand("P1-RUNE-2"));
            await game.Act("P1", new HideCardCommand("SECRET", "UNL-003/219", "BATTLEFIELD:BF"));
            Assert.True(game.State.CardObjects["SECRET"].IsFaceDown); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
            await BattleWinnerPhaseBoundaryTests.PassFocus(game); await game.Act("P1", new EndTurnCommand()); await Flow.Tap(game, "P2");
            await game.Act("P2", new RecycleRuneCommand("P2-RED"));
            await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
            await game.Act("P2", new PlayCardCommand("RAY", "OGN·009/298", ["UNIT"])); await game.Top();
            Assert.True(game.State.CardObjects["SECRET"].IsFaceDown);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            var prefix = game.Events().Length; await game.Top();
            Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "SECRET"));
            Assert.Equal(0, game.State.CardObjects["SECRET"].Damage); await game.Replay();
        }
        finally { await Flow.Save("kogmaw-actual-paid-standby-not-a-unit", initial, game); }
    }
}
