using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven078EmpoweredRuneLastBreathConsumerTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public async Task EachActuallyPaidSongCopiesTheSelfMightAndEarnsAnIndependentConditionalTwoRuneSkill(int copies, bool empower)
    {
        var initial = Position("P1", 8); var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        for (var i = 1; i <= 2; i++)
        {
            cards["P1-RUNE-" + i] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-RUNE-" + i, "UNL-R02", "P1");
            cards["SONG-" + i] = OfficialGarenConquestBattlefieldCountTests.Printed("SONG-" + i, "SFD·059/221", "P1");
            locations["SONG-" + i] = new("P1", "BASE");
        }
        // Pre-existing, unattached Songs in an explicit midgame fixture. Neither
        // copied text nor the host's empowered status is written into the fixture.
        initial = initial with { RoomId = "VEN078-PAID-COPIED-TEXT", CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = [..initial.PlayerZones["P1"].Base, "SONG-1", "SONG-2"] } } };
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P1"); await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            Assert.Equal(4, game.State.RunePools["P1"].Mana); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            if (empower)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-3")); await game.Act("P1", new RecycleRuneCommand("P1-RUNE-4"));
                Assert.Equal(2, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
                await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
                Assert.Equal(3, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, Might(game.State, "P1"));
            }
            for (var i = 1; i <= copies; i++)
            {
                Assert.Null(game.State.CardObjects["SONG-" + i].AttachedRuleText);
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i)); var mana = game.State.RunePools["P1"].Mana;
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"]));
                Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                await game.Top(); var gear = game.State.CardObjects["SONG-" + i];
                Assert.Equal("UNIT", gear.AttachedToObjectId); Assert.Equal(No, gear.AttachedRuleText!.CardNo);
                Assert.Equal("UNIT", gear.AttachedRuleText.Unit.ObjectId); Assert.Equal(generation, gear.AttachedRuleText.Unit.Generation);
                Assert.True(gear.AttachmentRevision > 0); Assert.Equal(empower ? 4 + 2 * (1 + i) : 4, Might(game.State, "P1")); Cold(initial, game);
            }
            Assert.Equal(empower ? 4 + 2 * (1 + copies) : 4, Might(game.State, "P1")); Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
            await game.Act("P1", new EndTurnCommand()); await Kill(game, "P2");
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            var queued = game.State.TriggerQueue.Where(t => t.EffectKind == TriggerKinds.UnitLastBreathCallRuneOne).ToArray();
            var stacked = game.State.StackItems.Where(t => t.EffectKind == TriggerKinds.UnitLastBreathCallRuneOne).ToArray();
            var expected = empower ? 1 + copies : 0; Assert.Equal(expected, queued.Length + stacked.Length);
            var before = game.State.PlayerZones["P1"].RuneDeck.ToArray(); Assert.True(before.Length >= 2 * expected);
            if (queued.Length > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: queued.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(expected, game.State.StackItems.Count); Assert.Empty(game.State.TriggerQueue); Cold(initial, game);
            for (var i = 1; i <= expected; i++)
            {
                Assert.Equal("P1", game.State.StackItems[^1].ControllerId); Assert.Empty(game.State.StackItems[^1].TargetObjectIds);
                await game.Top(); Assert.Equal(before.Skip(2 * i), game.State.PlayerZones["P1"].RuneDeck);
                CalledRunes(game.State, "P1", before.Skip(2 * (i - 1)).Take(2)); Cold(initial, game);
            }
            Assert.Equal(expected, game.Events().Count(e => e.Event.Kind == "RUNES_CALLED" && Equals(e.Event.Payload.GetValueOrDefault("reason"), TriggerKinds.UnitLastBreathCallRuneOne)));
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Export("paid-song-copies-" + copies + "-" + empower, initial, game); }
    }
}
