using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven035DelayedEmpowerConsumerTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task ChosenInverseOrderChecksConditionalDeathAfterActualGuardianRecall(bool auraOnBattlefield, bool auraFirst)
    {
        // Controlled initial field and base equipment, not a submitted-deck or
        // acquisition claim. Empowerment, assembly, damage, ordering and draws
        // below all use ordinary commands and physical rune payments.
        var initial = LethalAuraPosition(auraOnBattlefield); var game = new Driver(initial);
        try
        {
            await TapRunes(game);
            await game.Act("P1", new RecycleRuneCommand("LETHAL-P1-RUNE-0"));
            await game.Act("P1", new AssembleEquipmentCommand("GUARDIAN", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
            Assert.Equal(12, game.State.RunePools["P1"].Mana);
            var hostGeneration = game.State.CardObjects["HOST"].ObjectGeneration;
            var poroGeneration = game.State.CardObjects["UNIT"].ObjectGeneration;
            var targets = new[] { "HOST", "UNIT", "AURA" };
            for (var i = 0; i < targets.Length; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("LETHAL-P1-RUNE-" + (i + 1)));
                await game.Act("P1", new PlayCardCommand("STATUS-" + i, No, [targets[i]], Mode: Empower)); await game.Top();
                Assert.True(game.State.CardObjects[targets[i]].IsEmpowered);
            }
            Assert.Equal(4, StatusMight(game.State, "P1", "HOST"));
            Assert.Equal(5, StatusMight(game.State, "P1", "UNIT"));
            Assert.Equal(7, StatusMight(game.State, "P1", "AURA"));
            Assert.Equal(3, game.State.UntilEndOfTurnEffects.Count(e => e.StartsWith("DELAYED_EMPOWER:", StringComparison.Ordinal)));
            var damageStart = game.Events().Length;
            for (var i = 0; i < 2; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("LETHAL-P1-RUNE-" + (i + 4)));
                await game.Act("P1", new PlayCardCommand("RAY-" + i, "OGN·009/298", [targets[i]])); await game.Top();
                Assert.Equal(3, game.State.CardObjects[targets[i]].Damage);
                Assert.Contains(targets[i], game.State.PlayerZones["P1"].Battlefields);
            }
            var damage = game.Events().Skip(damageStart).Where(e => e.Event.Kind == "DAMAGE_APPLIED").ToArray();
            Assert.Equal(2, damage.Length);
            Assert.All(damage, e => Assert.Equal(3, Convert.ToInt32(e.Event.Payload["damage"])));
            Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            var hand = game.State.PlayerZones["P1"].Hand.ToArray(); var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
            var scores = game.State.PlayerScores.ToDictionary();
            await game.Act("P1", new EndTurnCommand());
            Assert.Equal(3, game.State.TriggerQueue.Count);
            var byUnit = game.State.TriggerQueue.ToDictionary(t => Assert.IsType<DelayedEmpowerContext>(t.DelayedEmpower).Unit.ObjectId);
            var order = auraFirst ? new[] { "AURA", "HOST", "UNIT" } : new[] { "UNIT", "AURA", "HOST" };
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order.Select(id => byUnit[id].TriggerId).ToArray()));
            Assert.Equal(order[0], game.State.StackItems[^1].DelayedEmpower!.Unit.ObjectId);
            var firstStart = game.Events().Length; await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(poroGeneration + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            var first = game.Events().Skip(firstStart).ToArray();
            var death = Assert.Single(first, e => e.Event.Kind == "UNIT_DESTROYED" && LethalTarget(e.Event) == "UNIT");
            Assert.DoesNotContain(first, e => e.Event.Kind is "DAMAGE_APPLIED" or "CARD_DRAWN");
            if (auraFirst)
            {
                var consumed = Assert.Single(first, e => e.Event.Kind == "EQUIPMENT_DESTROYED" && LethalTarget(e.Event) == "GUARDIAN");
                var recalled = Assert.Single(first, e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && LethalTarget(e.Event) == "HOST");
                Assert.True(consumed.Sequence < recalled.Sequence && recalled.Sequence < death.Sequence);
                Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
                Assert.Equal(0, game.State.CardObjects["HOST"].Damage);
                Assert.True(game.State.CardObjects["HOST"].IsExhausted);
            }
            else
            {
                Assert.Contains("HOST", game.State.PlayerZones["P1"].Battlefields);
                Assert.Equal(3, game.State.CardObjects["HOST"].Damage);
                Assert.Equal("HOST", game.State.CardObjects["GUARDIAN"].AttachedToObjectId);
                Assert.DoesNotContain(first, e => e.Event.Kind is "EQUIPMENT_DESTROYED" or "UNIT_RECALLED_TO_BASE");
            }
            var earnsDraw = !auraFirst || auraOnBattlefield;
            Assert.Equal(2 + (earnsDraw ? 1 : 0), game.State.StackItems.Count);
            if (earnsDraw)
            {
                var draw = Assert.IsType<LastBreathDrawContext>(game.State.StackItems[^1].LastBreathDraw);
                Assert.Equal(new ObjectBinding("UNIT", poroGeneration), draw.Source); Assert.Equal("P1", draw.ControllerId);
                Assert.NotEmpty(deck); await game.Top();
                Assert.Equal(hand.Concat(deck.Take(1)), game.State.PlayerZones["P1"].Hand);
                Assert.Equal(deck.Skip(1), game.State.PlayerZones["P1"].MainDeck);
            }
            else
            {
                Assert.All(game.State.StackItems, i => Assert.NotNull(i.DelayedEmpower));
                Assert.Equal(hand, game.State.PlayerZones["P1"].Hand); Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck);
            }
            Assert.Equal(2, game.State.StackItems.Count);
            for (var i = 1; i < order.Length; i++)
            {
                var inverse = Assert.IsType<DelayedEmpowerContext>(game.State.StackItems[^1].DelayedEmpower);
                Assert.Equal(order[i], inverse.Unit.ObjectId);
                if (order[i] == "UNIT") Assert.Equal(poroGeneration, inverse.Unit.Generation);
                await game.Top();
            }
            Assert.Equal("P2", game.State.TurnPlayerId); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            Assert.Null(game.State.EndStep); Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
            Assert.Equal(hostGeneration, game.State.CardObjects["HOST"].ObjectGeneration);
            Assert.Equal(0, game.State.CardObjects["HOST"].Damage); Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.False(game.State.CardObjects["AURA"].IsEmpowered); Assert.Contains("GUARDIAN", game.State.PlayerZones["P1"].Graveyard);
            var after = game.Events().Skip(firstStart).ToArray();
            Assert.Single(after, e => e.Event.Kind == "UNIT_DESTROYED" && LethalTarget(e.Event) == "UNIT");
            Assert.DoesNotContain(after, e => e.Event.Kind == "UNIT_DESTROYED" && LethalTarget(e.Event) == "HOST");
            Assert.Equal(earnsDraw ? 1 : 0, after.Count(e => e.Event.Kind == "CARD_DRAWN" && Equals(e.Event.Payload.GetValueOrDefault("playerId"), "P1")));
            Assert.DoesNotContain(after, e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD" or "BATTLEFIELD_CONQUERED" or "SCORE_GAINED");
            Assert.Equal(scores, game.State.PlayerScores);
            foreach (var player in new[] { "P1", "P2" })
                Assert.Equal(12, game.State.CardObjects.Values.Count(c => c.OwnerId == player && c.Tags.Contains(CardObjectTags.RuneCard)));
            await game.Replay(); Restore(game.State);
        }
        finally { await Save($"delayed-aura-lethal-field-{auraOnBattlefield}-aura-first-{auraFirst}", initial, game); }
    }

    private static string? LethalTarget(GameEvent value) => value.Payload.GetValueOrDefault("targetObjectId") as string;

    private static MatchState LethalAuraPosition(bool auraOnBattlefield)
    {
        var state = VenUnitEntryBasicTests.Position("UNL-156/219") with { RoomId = "DELAYED-AURA-LETHAL",
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        state = state with {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "BATTLEFIELD", "BF") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Hand = [], Battlefields = [..state.PlayerZones["P1"].Battlefields, "UNIT"] } } };
        state = StatusBattlefield(Add(state, "HOST", "OGN·096/298", zone: "BASE"), "HOST", "P1");
        state = Add(state, "AURA", "VEN·130", zone: "BASE");
        if (auraOnBattlefield) state = StatusBattlefield(state, "AURA", "P1");
        state = Add(state, "GUARDIAN", "SFD·051/221", zone: "BASE");
        for (var i = 0; i < 3; i++) state = Add(state, "STATUS-" + i, No);
        for (var i = 0; i < 2; i++) state = Add(state, "RAY-" + i, "OGN·009/298");
        foreach (var player in new[] { "P1", "P2" })
            for (var i = 0; i < 12; i++)
                state = Add(state, "LETHAL-" + player + "-RUNE-" + i, i < 4 ? "UNL-R02" : i < 6 ? "UNL-R01" : "UNL-R03", player, "BASE");
        return state;
    }
}
