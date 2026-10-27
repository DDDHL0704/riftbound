using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven030ConditionalWardConsumerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(false, 0, true)] [InlineData(true, 0, false)] [InlineData(true, 1, true)]
    public async Task ActualEnemySpellPaysOneWardOnlyAfterRealNativeEmpower(bool empowered, int power, bool accepted)
    {
        var initial = Ven014And030EmpoweredCombatConsumerTests.Position("VEN·030", true);
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary(); var added = new List<string>();
        for (var i = 1; i <= 8; i++)
        {
            var id = "WARD-OPPONENT-RUNE-" + i; added.Add(id);
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R03", "P2"); locations[id] = new("P2", "BASE");
        }
        cards["NEGATIVE"] = OfficialGarenConquestBattlefieldCountTests.Printed("NEGATIVE", "UNL-066/219", "P2"); locations["NEGATIVE"] = new("P2", "HAND");
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with {
                Base = [..initial.PlayerZones["P2"].Base, ..added], Hand = [..initial.PlayerZones["P2"].Hand, "NEGATIVE"] } } };
        var game = new Driver(initial);
        try
        {
            for (var i = 0; i < 6; i++) await game.Act("P1", new TapRuneCommand("EMPOWER-COMBAT-RUNE-" + i));
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·030", [])); Assert.Equal(3, game.State.RunePools["P1"].Mana);
            if (empowered)
            {
                await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
                Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Top();
            }
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].Tags.Contains(CardObjectTags.Spellshield));
            await game.Act("P1", new EndTurnCommand());
            for (var i = 1; i <= 7; i++) await game.Act("P2", new TapRuneCommand("WARD-OPPONENT-RUNE-" + i));
            if (power == 1) await game.Act("P2", new RecycleRuneCommand("WARD-OPPONENT-RUNE-8"));
            Assert.Equal(7, game.State.RunePools["P2"].Mana); Assert.Equal(power, game.State.RunePools["P2"].TotalPower);
            var command = new PlayCardCommand("NEGATIVE", "UNL-066/219", ["UNIT"]);
            var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P2", PlayCostPreviewTests.Request(game.State, command));
            Assert.NotNull(quote.Cost); Assert.Equal(7, quote.Cost.Mana); Assert.Equal(empowered ? 1 : 0, quote.Cost.GenericPower);
            if (!accepted)
            {
                var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
                raw["promptId"] = game.PromptFor("P2").PromptId; raw["snapshotTick"] = game.State.Tick;
                await game.ActRetained(new("P2", "unpaid-conditional-ward", JsonSerializer.SerializeToElement(raw, Json)), false);
                Assert.Equal(7, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
                Assert.Contains("NEGATIVE", game.State.PlayerZones["P2"].Hand); Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
            }
            else
            {
                await game.Act("P2", command); Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
                Assert.Equal(empowered ? 1 : 0, game.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
                await game.Top(); Assert.Equal(-7, game.State.CardObjects["UNIT"].Power);
                await game.Act("P2", new EndTurnCommand()); Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
                Assert.Empty(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
            }
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_EMPOWER_COMBAT_EVIDENCE") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, "VEN030-conditional-ward-" + empowered + "-" + power + ".json"), JsonSerializer.Serialize(new {
                    scope = "Existing No030 explicit turn-five fixture, zero pools and physical runes, actual three-mana play/native three-mana Empower, enemy real seven-mana spell and conditional one-power Ward. No forced empowered/tag/source state, ordinary opening, SQL/network, applicability, whole-card or complete L2/L3 claim. Original per-command privacy/recovery/dedup/action replay retained.",
                    initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
            }
        }
    }
}
