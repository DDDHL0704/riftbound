using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Fixture = Riftbound.ConformanceTests.Ven001PhysicalThresholdConsumerTests;

namespace Riftbound.ConformanceTests;

public sealed class Ven001ConditionalWardConsumerTests
{
    [Theory]
    [InlineData(false, 0, true)] [InlineData(true, 0, false)] [InlineData(true, 1, true)]
    public async Task ActualEnemySpellPaysOneWardOnlyAfterTheRealDiscountedEmpower(bool empowered, int power, bool accepted)
    {
        var initial = Fixture.Position(true); var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary(); var zones = initial.PlayerZones.ToDictionary();
        for (var i = 0; i < 12; i++)
        {
            var id = "WARD-THRESHOLD-RUNE-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R03", "P2"); locations[id] = new("P2", i < 8 ? "BASE" : "RUNE_DECK");
            zones["P2"] = i < 8 ? zones["P2"] with { Base = [..zones["P2"].Base, id] } : zones["P2"] with { RuneDeck = [..zones["P2"].RuneDeck, id] };
        }
        cards["NEGATIVE"] = OfficialGarenConquestBattlefieldCountTests.Printed("NEGATIVE", "UNL-066/219", "P2"); locations["NEGATIVE"] = new("P2", "HAND");
        zones["P2"] = zones["P2"] with { Hand = [..zones["P2"].Hand, "NEGATIVE"] };
        initial = initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
        // Keep per-command acceptance and duplicate checks while sharing one recovery
        // at the boundary where the enemy's paid ward action becomes testable.
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await Fixture.Prepare(game); var eight = Fixture.Runes(game.State);
            await game.TapRunes("P1", eight);
            foreach (var id in eight.Skip(4)) await game.Act("P1", new RecycleRuneCommand(id));
            if (empowered)
            {
                await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
                Assert.Equal(2, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Top();
            }
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(empowered, game.State.CardObjects["UNIT"].Tags.Contains(CardObjectTags.Spellshield));
            await game.Act("P1", new EndTurnCommand());
            await game.TapRunes("P2", Enumerable.Range(0, 7).Select(i => "WARD-THRESHOLD-RUNE-" + i));
            if (power == 1) await game.Act("P2", new RecycleRuneCommand("WARD-THRESHOLD-RUNE-7"));
            Assert.Equal(7, game.State.RunePools["P2"].Mana); Assert.Equal(power, game.State.RunePools["P2"].TotalPower);
            await game.ColdCheckpoint();
            var command = new PlayCardCommand("NEGATIVE", "UNL-066/219", ["UNIT"]);
            var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P2", PlayCostPreviewTests.Request(game.State, command));
            Assert.NotNull(quote.Cost); Assert.Equal(7, quote.Cost.Mana); Assert.Equal(empowered ? 1 : 0, quote.Cost.GenericPower);
            if (accepted)
            {
                await game.Act("P2", command); Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
                Assert.Equal(empowered ? 1 : 0, game.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
                await game.Top(); Assert.Equal(-4, game.State.CardObjects["UNIT"].Power); await game.Act("P2", new EndTurnCommand());
                Assert.Equal(6, game.State.CardObjects["UNIT"].Power); Assert.Empty(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
            }
            else
            {
                await Fixture.Reject(game, "P2", command, "ven001-unpaid-ward");
                Assert.Equal(7, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
                Assert.Contains("NEGATIVE", game.State.PlayerZones["P2"].Hand); Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
            }
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered);
            // ColdCheckpoint already replays the complete original journal.
            await game.ColdCheckpoint();
        }
        finally { await Fixture.Save("conditional-ward-" + empowered + "-" + power, initial, game); }
    }
}
