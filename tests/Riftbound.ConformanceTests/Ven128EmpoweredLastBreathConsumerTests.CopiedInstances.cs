using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven128EmpoweredLastBreathConsumerTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public async Task EachActuallyAttachedSongTextEarnsItsOwnDependentLastBreathOnlyWhenTheHostWasEmpowered(int copies, bool empower)
    {
        var initial = Position("P1"); var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        foreach (var i in new[] { 1, 2 })
        {
            cards["P1-RUNE-" + i] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-RUNE-" + i, "UNL-R02", "P1");
            cards["SONG-" + i] = OfficialGarenConquestBattlefieldCountTests.Printed("SONG-" + i, "SFD·059/221", "P1");
            locations["SONG-" + i] = new("P1", "BASE");
        }
        foreach (var i in new[] { 5, 6 })
        {
            cards["P1-RUNE-" + i] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-RUNE-" + i, "UNL-R06", "P1");
            locations["P1-RUNE-" + i] = new("P1", "BASE");
        }
        initial = initial with { RoomId = "VEN128-ACTUAL-ATTACHED-SONGS", CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = [..initial.PlayerZones["P1"].Base, "P1-RUNE-5", "P1-RUNE-6", "SONG-1", "SONG-2"] } } };
        // The Songs are pre-existing, unattached equipment in this explicit midgame
        // fixture. Their attachment and copied-text instances are earned by commands.
        var game = new Driver(initial);
        try
        {
            await game.TapRunes("P1", Enumerable.Range(1, 6).Select(i => "P1-RUNE-" + i));
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); Assert.Equal(4, game.State.RunePools["P1"].Mana);
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            if (empower)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-3"));
                await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
                Assert.Equal(3, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            }
            for (var i = 1; i <= copies; i++)
            {
                Assert.Null(game.State.CardObjects["SONG-" + i].AttachedRuleText);
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i)); var mana = game.State.RunePools["P1"].Mana;
                Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Green]);
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"]));
                Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                await game.Top(); var gear = game.State.CardObjects["SONG-" + i];
                Assert.Equal("UNIT", gear.AttachedToObjectId); Assert.Equal(No, gear.AttachedRuleText!.CardNo);
                Assert.Equal("UNIT", gear.AttachedRuleText.Unit.ObjectId); Assert.Equal(generation, gear.AttachedRuleText.Unit.Generation);
                Assert.True(gear.AttachmentRevision > 0); JsonCold(initial, game);
            }
            Assert.Equal(empower, game.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(Minions(game.State));
            await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-2"));
            await game.Act("P2", new PlayCardCommand("KILL", Destroy, ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.Empty(Minions(game.State));
            var queued = game.State.TriggerQueue.Where(t => t.EffectKind == TriggerKinds.UnitLastBreathCreateMinions).ToArray();
            var stacked = game.State.StackItems.Where(t => t.EffectKind == TriggerKinds.UnitLastBreathCreateMinions).ToArray();
            var expected = empower ? 1 + copies : 0; Assert.Equal(expected, queued.Length + stacked.Length);
            if (queued.Length > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: queued.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(expected, game.State.StackItems.Count); Assert.Empty(game.State.TriggerQueue); JsonCold(initial, game);
            for (var i = 1; i <= expected; i++)
            {
                Assert.Equal("P1", game.State.StackItems[^1].ControllerId); Assert.Empty(game.State.StackItems[^1].TargetObjectIds);
                await game.Top(); Assert.Equal(2 * i, Minions(game.State).Length); JsonCold(initial, game);
            }
            Assert.All(Minions(game.State), m => { Assert.Equal("P1", m.OwnerId); Assert.Equal("P1", m.ControllerId); Assert.Equal(1, m.Power);
                Assert.True(m.IsExhausted); Assert.Contains(m.ObjectId, game.State.PlayerZones["P1"].Base); Assert.False(string.IsNullOrEmpty(m.TokenFactoryCardNo)); });
            Assert.Empty(game.State.StackItems); await game.Replay();
        }
        finally { await Export("attached-songs-" + copies + "-" + empower, initial, game); }
    }
}
