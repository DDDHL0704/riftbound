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
                ["SECOND-RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND-RUNE", "UNL-R01", "P1"),
                ["THIRD-RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("THIRD-RUNE", "UNL-R01", "P1"),
                ["SECOND-GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND-GEAR", "SFD·022/221", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "SECOND-RUNE", "THIRD-RUNE"],
                    Hand = [..initial.PlayerZones["P1"].Hand, "SECOND-GEAR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["SECOND-RUNE"] = new("P1", "BASE"), ["THIRD-RUNE"] = new("P1", "BASE"), ["SECOND-GEAR"] = new("P1", "HAND") } };
        var game = new Driver(initial); PlayCostQuoteDto? quote = null;
        try
        {
            await OpenDuel(game); await OrdinaryRune(game);
            await game.Act("P1", new TapRuneCommand("SECOND-RUNE"));
            await game.Act("P1", new PlayCardCommand("GEAR", "SFD·022/221", ["HOST"]));
            Assert.True(game.State.SpellDuelState.IsActive); Assert.True(game.State.SpellDuelState.IsClosed);
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal("P1", game.State.PriorityPlayerId);
            var stack = game.State.StackItems.ToArray();
            await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], []));
            Assert.Equal(stack, game.State.StackItems); Assert.Equal("P1", game.State.PriorityPlayerId);
            Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, game.State.RunePools["P1"]);
            await Reject(game, new LegendActCommand("LEGEND", Ability, [], []), "P2", "diana-waiting-player");
            Assert.Equal("P1", game.State.PriorityPlayerId); Assert.Equal(stack, game.State.StackItems);
            await game.Act("P1", new TapRuneCommand("THIRD-RUNE"));
            await game.Act("P1", new RecycleRuneCommand("SECOND-RUNE"));
            var second = new PlayCardCommand("SECOND-GEAR", "SFD·022/221", ["HOST"]);
            quote = Quote(game, second);
            await game.Act("P1", second);
            Assert.True(quote.CanPay); Assert.Equal(1, quote.Cost!.AvailableDuelOnlyMana);
            Assert.Equal(2, game.State.StackItems.Count);
            await game.Top(); await game.Top();
            Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
            Assert.Equal("HOST", game.State.CardObjects["SECOND-GEAR"].AttachedToObjectId);
            Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
            await game.Replay();
        }
        finally { await Save("closed-duel-resource", cardNo, initial, game, quote); }
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

    [Theory]
    [InlineData("UNL-197/219")] [InlineData("UNL-234/219")] [InlineData("UNL-234*/219")]
    public async Task TheActualDuelStartInsightPaymentCanAcquireAndConsumeItsControllersDuelMana(string cardNo)
    {
        var initial = OfficialInsightSourceTests.Diana() with { Tick = 0 };
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["DIANA"] = OfficialGarenConquestBattlefieldCountTests.Printed("DIANA", "UNL-079/219", "P2"),
            ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", cardNo, "P2"),
            ["D1"] = OfficialGarenConquestBattlefieldCountTests.Printed("D1", "SFD·125/221", "P2"),
            ["D2"] = OfficialGarenConquestBattlefieldCountTests.Printed("D2", "OGN·083/298", "P2") };
        cards.Remove("R");
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
            ["LEGEND"] = new("P2", "LEGEND"), ["D1"] = new("P2", "MAIN_DECK"), ["D2"] = new("P2", "MAIN_DECK") };
        locations.Remove("R");
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Base = [], LegendZone = ["LEGEND"] } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new MoveUnitCommand("UNIT", Destination: "BATTLEFIELD:HILL", SourceObjectIds: ["UNIT"]));
            var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
            Assert.Equal("INSIGHT_EFFECT", payment.PaymentWindow); Assert.Equal("P2", payment.PlayerId); Assert.Equal(1, payment.ManaCost);
            Assert.True(game.State.TurnWindow.IsSpellDuel);
            await game.Act("P2", new LegendActCommand("LEGEND", Ability, [], []));
            Assert.Equal(payment, game.State.PendingPayment);
            Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, game.State.RunePools["P2"]);
            await game.Act("P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            Assert.Null(game.State.PendingPayment); Assert.Equal(RunePool.Empty, game.State.RunePools["P2"]);
            await game.Top();
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("P2", choice.PlayerId); Assert.Equal("INSIGHT", choice.ChoiceWindow);
            await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
            Assert.Equal("P1", game.State.FocusPlayerId); Assert.Equal(RunePool.Empty, game.State.RunePools["P2"]);
            Assert.True(game.State.CardObjects["LEGEND"].IsExhausted); await game.Replay();
        }
        finally { await Save("actual-insight-duel-payment", cardNo, initial, game, null); }
    }
}
