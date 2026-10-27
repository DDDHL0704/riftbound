using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Arena = Riftbound.ConformanceTests.Ven159PhysicalBulwarkCombatConsumerTests;
using Common = Riftbound.ConformanceTests.Ven129PhysicalCombatConsumerTests;

namespace Riftbound.ConformanceTests;

public sealed class Ven159SubmittedBattlefieldConsumerTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task LegalSubmittedTempleAndBothPaidArmiesKeepTheSevenPointBulwarkPriorityThroughActualCombat(string defender)
    {
        // Names do not enter the stable opening stream. These valid ordered
        // three-card inputs put Temple in this seat's actually selected slot.
        var fields = defender == "P1" ? new[] { Arena.Temple, "OGN·275/298", "OGN·276/298" }
            : new[] { "OGN·278/298", Arena.Temple, "OGN·276/298" };
        var game = await VenEmpowerCanonicalOpeningTests.Open(defender, "poppy-standby", "UNL-R06",
            [Arena.WallNo, Arena.WallNo, Arena.WallNo, Arena.GuardNo, Arena.GuardNo, Arena.GuardNo], battlefields: fields);
        try
        {
            var field = Assert.Single(game.State.PlayerZones[defender].Battlefields, id => game.State.CardObjects[id].CardNo == Arena.Temple);
            Assert.Contains(game.Events(), e => e.Event.Kind == "OFFICIAL_BATTLEFIELD_SELECTED" && Equals(e.Event.Payload.GetValueOrDefault("battlefieldObjectId"), field));
            var walls = new Dictionary<string, string>(); var guards = new Dictionary<string, string>();
            foreach (var player in new[] { "P1", "P2" })
            {
                await game.WaitFor(player, 11, Arena.WallNo, Arena.GuardNo); var runes = game.Runes(player);
                Assert.Equal(0, game.State.RunePools[player].Mana); Assert.Equal(0, game.State.RunePools[player].TotalPower);
                foreach (var id in runes.Take(10)) await game.Act(player, new TapRuneCommand(id));
                await game.Act(player, new RecycleRuneCommand(runes[0]));
                walls[player] = game.Hand(player, Arena.WallNo).First(); guards[player] = game.Hand(player, Arena.GuardNo).First();
                await game.Act(player, new PlayCardCommand(walls[player], Arena.WallNo, [])); Assert.Equal(4, game.State.RunePools[player].Mana);
                await game.Act(player, new PlayCardCommand(guards[player], Arena.GuardNo, [])); Assert.Equal(0, game.State.RunePools[player].Mana); Assert.Equal(0, game.State.RunePools[player].TotalPower);
                Assert.Equal(6, game.Might(player, walls[player])); Assert.Equal(6, game.Might(player, guards[player])); Common.AssertTwelveEach(game.State); await game.ColdCheckpoint();
            }
            await game.Act("P2", new EndTurnCommand());
            if (defender == "P2") await game.Act("P1", new EndTurnCommand());
            Assert.Equal(defender, game.State.TurnPlayerId); Assert.False(game.State.CardObjects[walls[defender]].IsExhausted);
            await game.Act(defender, new MoveUnitCommand(walls[defender], "BASE", "BATTLEFIELD:" + field, [], SourceObjectIds: [walls[defender], guards[defender]]));
            Assert.Equal(7, game.Might(defender, walls[defender])); Assert.Equal(6, game.Might(defender, guards[defender])); await game.ColdCheckpoint();
            await Common.CompleteMovementWindow(() => game.State, async (p, c) => { await game.Act(p, c); await game.ColdCheckpoint(); });
            await game.Act(defender, new EndTurnCommand()); var attacker = Arena.Other(defender);
            await game.Act(attacker, new MoveUnitCommand(walls[attacker], "BASE", "BATTLEFIELD:" + field, [], SourceObjectIds: [walls[attacker], guards[attacker]]));
            foreach (var player in new[] { "P1", "P2" })
            {
                Assert.Equal(7, game.Might(player, walls[player])); Assert.Equal(6, game.Might(player, guards[player])); Assert.Equal(6, game.State.CardObjects[walls[player]].Power);
                Assert.Equal(player == attacker, game.State.CardObjects[walls[player]].IsAttacking); Assert.Equal(player == defender, game.State.CardObjects[walls[player]].IsDefending);
            }
            await game.ColdCheckpoint(); var prefix = game.Events().Length;
            await Arena.FinishBattle(() => game.State, async (p, c) => { await game.Act(p, c); await game.ColdCheckpoint(); }, game.Events, walls, guards);
            Arena.AssertBattleOutcome(game.State, game.Events().Skip(prefix).ToArray(), field, walls, guards);
            Common.AssertTwelveEach(game.State); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven159-submitted-real-temple-battle-" + defender); }
    }
}
