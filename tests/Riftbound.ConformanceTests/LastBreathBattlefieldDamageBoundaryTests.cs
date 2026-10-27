using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Flow = Riftbound.ConformanceTests.LastBreathDrawBoundaryTests;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathBattlefieldDamageBoundaryTests
{
    private const string Unit = "OGN·190/298";

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 2)] [InlineData(true, 0)] [InlineData(true, 2)]
    public async Task EachPaidKogmawTextEarnsAResponseAndUsesOnlyItsCapturedBattlefieldAndSourceGeneration(bool battlefield, int copies)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await Flow.Tap(game, "P1"); await game.Act("P1", new RecycleRuneCommand("P1-RUNE-6"));
            await game.Act("P1", new PlayCardCommand("UNIT", Unit, [], Destination: battlefield ? "BATTLEFIELD:BF" : "BASE"));
            Assert.Equal(4, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal(Unit, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
            }
            await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
            await BattleWinnerPhaseBoundaryTests.PassFocus(game);
            await game.Act("P1", new EndTurnCommand()); await Flow.Tap(game, "P2");
            if (battlefield)
            {
                await game.Act("P2", new RecycleRuneCommand("P2-RED"));
                await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
                await game.Act("P2", new PlayCardCommand("RAY", "OGN·009/298", ["UNIT"])); await game.Top();
            }
            else
            {
                await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
                await game.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"])); await game.Top();
            }
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(0, game.State.CardObjects["ALLY"].Damage); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
            Assert.Equal(1 + copies, game.State.TriggerQueue.Count + game.State.StackItems.Count);
            if (battlefield && copies == 2)
            {
                Assert.All(game.State.TriggerQueue, t => Assert.Equal(TimingStates.SpellDuelOpen, t.TimingContext));
                AssertAreaBirthBindings(game);
            }
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            if (battlefield && copies == 2)
            {
                Assert.All(game.State.StackItems, t => Assert.Equal(TimingStates.SpellDuelOpen, t.TimingContext));
                Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = game.State.StackItems
                    .Select((item, index) => index == 0 ? item with { LastBreathBattlefieldDamage = null } : item).ToArray() }),
                    e => e.Contains("captured last-breath battlefield damage", StringComparison.Ordinal));
            }
            for (var i = 0; i <= copies; i++)
            {
                var prefix = game.Events().Length; await game.Top(); var events = game.Events().Skip(prefix).Select(e => e.Event).ToArray();
                Assert.Single(events, e => e.Kind == "TRIGGER_RESOLVED");
                if (battlefield && i == 0)
                {
                    var damage = events.Where(e => e.Kind == "DAMAGE_APPLIED").ToArray(); Assert.Equal(2, damage.Length);
                    Assert.All(damage, e => Assert.Equal(4, Convert.ToInt32(e.Payload["damage"])));
                    Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
                    foreach (var death in events.Where(e => e.Kind == "UNIT_DESTROYED"))
                    {
                        var cause = Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]);
                        Assert.Equal(new ObjectBinding("UNIT", generation), cause.Source);
                        Assert.Equal(Unit, cause.CardNo); Assert.Equal("P1", cause.ControllerId); Assert.False(cause.FromSpell);
                    }
                }
                else Assert.DoesNotContain(events, e => e.Kind == "DAMAGE_APPLIED");
                Assert.Equal(0, game.State.CardObjects["OTHER-VICTIM"].Damage); Assert.Equal(0, game.State.CardObjects["BASE-VICTIM"].Damage);
                Assert.Equal(copies - i, game.State.StackItems.Count);
            }
            Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Flow.Save("kogmaw-paid-" + copies + "-battlefield-" + battlefield, initial, game); }
    }

    private static MatchState Position()
    {
        var state = Flow.Add(Flow.Position(Unit), "P1-RUNE-6", "UNL-R05", "P1", "BASE");
        state = Flow.Add(Flow.Add(Flow.Add(state, "P2-RED", "UNL-R01", "P2", "BASE"), "RAY", "OGN·009/298", "P2", "HAND"),
            "BASE-VICTIM", "OGN·219/298", "P1", "BASE");
        state = Flow.Add(state, "OTHER-VICTIM", "OGN·219/298", "P2", "BASE");
        return state with { RoomId = "CAPTURED-KOGMAW-AREA-DAMAGE",
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["OTHER-VICTIM"] = new("P2", "BATTLEFIELD", "OTHER") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with {
                Base = state.PlayerZones["P2"].Base.Where(id => id != "OTHER-VICTIM").ToArray(), Battlefields = [..state.PlayerZones["P2"].Battlefields, "OTHER-VICTIM"] } } };
    }
}
