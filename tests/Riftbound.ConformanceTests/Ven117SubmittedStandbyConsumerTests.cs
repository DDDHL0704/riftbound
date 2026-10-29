using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Arena = Riftbound.ConformanceTests.Ven117PaidConditionalSteadfastConsumerTests;
using Common = Riftbound.ConformanceTests.Ven129PhysicalCombatConsumerTests;

namespace Riftbound.ConformanceTests;

public sealed class Ven117SubmittedStandbyConsumerTests
{
    [Theory]
    [InlineData("P1", false)] [InlineData("P2", false)]
    [InlineData("P1", true)] [InlineData("P2", true)]
    public async Task LegalSubmittedDeckNaturalDrawAndRealNormalOrStandbyPaymentGiveOnlyTheCurrentDefenderFourMight(string actor, bool standby)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "poppy-standby", "UNL-R06",
            [Arena.No, Arena.No, Arena.No, Arena.FriendNo, Arena.FriendNo, Arena.FriendNo, Arena.EnemyNo, Arena.EnemyNo, Arena.EnemyNo]);
        try
        {
            var field = Assert.Single(game.State.PlayerZones[actor].Battlefields, id => game.State.CardObjects[id].Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag));
            string host = "", friend = "", enemy = ""; var other = Arena.Other(actor);
            foreach (var player in new[] { "P1", "P2" })
            {
                await game.WaitFor(player, 11, player == actor ? [Arena.No, Arena.FriendNo] : [Arena.EnemyNo]);
                Assert.Equal(0, game.State.RunePools[player].Mana); Assert.Equal(0, game.State.RunePools[player].TotalPower);
                var mana = player == actor ? standby ? 3 : 5 : 6;
                await game.Tap(player, mana);
                if (player == actor)
                {
                    host = game.Hand(actor, Arena.No).First(); friend = game.Hand(actor, Arena.FriendNo).First();
                    if (!standby)
                    { var before = game.Events().Length; await game.Act(actor, new PlayCardCommand(host, Arena.No, [])); Arena.AssertCost(game.Events().Skip(before), 2, 0); Assert.Equal(1, game.Might(actor, host)); }
                    var prefix = game.Events().Length; await game.Act(actor, new PlayCardCommand(friend, Arena.FriendNo, [])); Arena.AssertCost(game.Events().Skip(prefix), 3, 0);
                }
                else
                { enemy = game.Hand(other, Arena.EnemyNo).First(); var prefix = game.Events().Length; await game.Act(other, new PlayCardCommand(enemy, Arena.EnemyNo, [])); Arena.AssertCost(game.Events().Skip(prefix), 6, 0); }
                Assert.Equal(0, game.State.RunePools[player].Mana); Assert.Equal(0, game.State.RunePools[player].TotalPower);
                Common.AssertTwelveEach(game.State); await game.ColdCheckpoint();
            }
            await game.Act("P2", new EndTurnCommand()); if (actor == "P2") await game.Act("P1", new EndTurnCommand());
            Assert.Equal(actor, game.State.TurnPlayerId);
            var members = standby ? new[] { friend } : new[] { host, friend };
            await game.Act(actor, new MoveUnitCommand(members[0], "BASE", "BATTLEFIELD:" + field, [], SourceObjectIds: members)); await game.ColdCheckpoint();
            await Common.CompleteMovementWindow(() => game.State, async (p, c) => { await game.Act(p, c); await game.ColdCheckpoint(); });
            if (standby)
            {
                Assert.Equal(actor, game.State.CardObjects[field].ControllerId);
                await game.Act(actor, new RecycleRuneCommand(game.Runes(actor)[0])); var prefix = game.Events().Length;
                await game.Act(actor, new HideCardCommand(host, Arena.No, "BATTLEFIELD:" + field, ["STANDBY_A"])); Arena.AssertCost(game.Events().Skip(prefix), 0, 1);
                Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
                Arena.AssertShadowPrivate(game.State, actor, host); await game.ColdCheckpoint();
            }
            await game.Act(actor, new EndTurnCommand());
            await game.Act(other, new MoveUnitCommand(enemy, "BASE", "BATTLEFIELD:" + field, [])); await game.ColdCheckpoint();
            if (standby) await Arena.RevealDuringRealBattle(() => game.State, async (p, c) => { await game.Act(p, c); await game.ColdCheckpoint(); }, game.Events, actor, host, field);
            Arena.AssertCondition(game.State, actor, host, field, 1, false, 4); await game.ColdCheckpoint();
            var beforeCombat = game.Events().Length; var generation = game.State.CardObjects[host].ObjectGeneration;
            await Arena.FinishBattle(() => game.State, async (p, c) => { await game.Act(p, c); await game.ColdCheckpoint(); }, game.Events, actor, host, [friend], enemy, 4);
            Arena.AssertOutcome(game.State, game.Events().Skip(beforeCombat).ToArray(), host, 4, false, generation);
            Common.AssertTwelveEach(game.State); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven117-real-submitted-normal-or-standby-" + actor + "-" + standby); }
    }
}
