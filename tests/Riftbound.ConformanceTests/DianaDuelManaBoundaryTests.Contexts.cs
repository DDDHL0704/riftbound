using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class DianaDuelManaBoundaryTests
{
    [Theory]
    [InlineData("UNL-197/219")] [InlineData("UNL-234/219")] [InlineData("UNL-234*/219")]
    public async Task ClosedDuelPriorityCanBankManaWithoutPassingAndWaitingOpponentStillRejects(string cardNo)
    {
        var initial = Position(cardNo);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["SECOND-RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND-RUNE", "UNL-R01", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "SECOND-RUNE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SECOND-RUNE"] = new("P1", "BASE") } };
        var game = new Driver(initial);
        try
        {
            await OpenDuel(game); await OrdinaryRune(game);
            await game.Act("P1", new TapRuneCommand("SECOND-RUNE"));
            await game.Act("P1", new PlayCardCommand("GEAR", "SFD·022/221", ["HOST"]));
            Assert.Equal(TimingStates.SpellDuelClosed, game.State.TimingState);
            await game.Act("P2", new PassPriorityCommand());
            Assert.Equal("P1", game.State.PriorityPlayerId);
            var stack = game.State.StackItems.ToArray();
            await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], []));
            Assert.Equal(stack, game.State.StackItems); Assert.Equal("P1", game.State.PriorityPlayerId);
            Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, game.State.RunePools["P1"]);
            await Reject(game, new LegendActCommand("LEGEND", Ability, [], []), "P2", "diana-waiting-player");
            Assert.Equal("P1", game.State.PriorityPlayerId); Assert.Equal(stack, game.State.StackItems);
            await game.Top();
            Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
            Assert.Equal(1, game.State.RunePools["P1"].DuelOnlyMana);
            await game.Replay();
        }
        finally { await Save("closed-duel-resource", cardNo, initial, game, null); }
    }

    [Theory]
    [InlineData("UNL-197/219")] [InlineData("UNL-234/219")] [InlineData("UNL-234*/219")]
    public async Task ManaConversionCannotSpendBankedDuelManaUntilTheRealDuelBegins(string cardNo)
    {
        var ability = P4ActivatedAbilityCatalog.GetAll().Single(a => a.AbilityId == P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId);
        var initial = Position(cardNo);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["CONVERTER"] = OfficialGarenConquestBattlefieldCountTests.Printed("CONVERTER", ability.SourceCardNo, "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "CONVERTER"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["CONVERTER"] = new("P1", "BASE") } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], []));
            var amount = P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix + "1";
            var command = new ActivateAbilityCommand("CONVERTER", ability.AbilityId, [], [amount]);
            Assert.DoesNotContain(amount, JsonSerializer.Serialize(game.PromptFor("P1"), Json), StringComparison.Ordinal);
            var retained = await Reject(game, command);
            Assert.False(game.State.CardObjects["CONVERTER"].IsExhausted);
            Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, game.State.RunePools["P1"]);
            await OpenDuel(game);
            Assert.Contains(amount, JsonSerializer.Serialize(game.PromptFor("P1"), Json), StringComparison.Ordinal);
            await game.ActRetained(retained, false);
            await game.Act("P1", command);
            Assert.True(game.State.CardObjects["CONVERTER"].IsExhausted);
            Assert.Equal(new RunePool(0, 1), game.State.RunePools["P1"]);
            await game.Replay();
        }
        finally { await Save("convert-only-in-duel", cardNo, initial, game, null); }
    }
}
