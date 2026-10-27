using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class DianaDuelManaBoundaryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const string Ability = LegendActionAbilityCatalog.DianaLegendAbilityId;

    [Theory]
    [InlineData("UNL-197/219")] [InlineData("UNL-234/219")] [InlineData("UNL-234*/219")]
    public async Task ReactionResourceCanBeBankedInMainButCannotPayItsOrdinaryEquipment(string cardNo)
    {
        var initial = Position(cardNo); var game = new Driver(initial); PlayCostQuoteDto? quote = null;
        try
        {
            await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], []));
            Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
            Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, game.State.RunePools["P1"]);
            await OrdinaryRune(game);
            var command = new PlayCardCommand("GEAR", "SFD·022/221", ["HOST"]);
            quote = Quote(game, command);
            var retained = await Reject(game, command);
            Assert.True(quote.IsValid); Assert.False(quote.CanPay); Assert.Equal(1, quote.Cost!.MissingMana);
            Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
            Assert.Equal(1, game.State.RunePools["P1"].DuelOnlyMana);
            await OpenDuel(game);
            await game.ActRetained(retained, false);
            Assert.True(Quote(game, command).CanPay);
            await game.Act("P1", command); await game.Top();
            Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
            Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
            await game.Replay();
        }
        finally { await Save("bank-in-main", cardNo, initial, game, quote); }
    }

    [Theory]
    [InlineData("UNL-197/219")] [InlineData("UNL-234/219")] [InlineData("UNL-234*/219")]
    public async Task ManaGainedInTheActualDuelCannotLeakIntoAPlayAfterBothPlayersPassFocus(string cardNo)
    {
        var initial = Position(cardNo); var game = new Driver(initial); PlayCostQuoteDto? quote = null;
        try
        {
            await OpenDuel(game);
            await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], []));
            for (var i = 0; game.State.TimingState == TimingStates.SpellDuelOpen && i < 4; i++)
                await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
            Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState);
            Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, game.State.RunePools["P1"]);
            await OrdinaryRune(game);
            var command = new PlayCardCommand("GEAR", "SFD·022/221", ["HOST"]);
            quote = Quote(game, command);
            await Reject(game, command);
            Assert.True(quote.IsValid); Assert.False(quote.CanPay); Assert.Equal(1, quote.Cost!.MissingMana);
            Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
            Assert.Equal(1, game.State.RunePools["P1"].DuelOnlyMana);
            await game.Act("P1", new EndTurnCommand());
            Assert.All(game.State.RunePools.Values, pool => Assert.Equal(RunePool.Empty, pool));
            await game.Replay();
        }
        finally { await Save("no-leak-after-duel", cardNo, initial, game, quote); }
    }

    [Theory]
    [InlineData("UNL-197/219")] [InlineData("UNL-234/219")] [InlineData("UNL-234*/219")]
    public async Task TheSameActualDuelManaCanPayAReactionEquipmentWhileTheDuelIsStillOpen(string cardNo)
    {
        var initial = Position(cardNo); var game = new Driver(initial); PlayCostQuoteDto? quote = null;
        try
        {
            await OpenDuel(game);
            await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], []));
            await OrdinaryRune(game);
            var command = new PlayCardCommand("GEAR", "SFD·022/221", ["HOST"]);
            quote = Quote(game, command);
            Assert.True(quote.CanPay, quote.Message);
            Assert.Equal(1, quote.Cost!.AvailableDuelOnlyMana);
            Assert.Equal(0, quote.Cost.RemainingDuelOnlyMana);
            await game.Act("P1", command); await game.Top();
            Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
            Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
            var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event;
            Assert.Equal(2, paid.Payload["totalManaCost"]);
            Assert.Equal(1, paid.Payload["duelOnlyManaSpent"]);
            Assert.Equal(1, paid.Payload["ordinaryManaSpent"]);
            await game.Replay();
        }
        finally { await Save("pay-equipment-in-duel", cardNo, initial, game, quote); }
    }

    private static async Task OpenDuel(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
        Assert.Equal("P1", game.State.FocusPlayerId);
    }

    private static async Task OrdinaryRune(Driver game)
    {
        await game.Act("P1", new TapRuneCommand("RUNE"));
        await game.Act("P1", new RecycleRuneCommand("RUNE"));
    }

    private static PlayCostQuoteDto Quote(Driver game, PlayCardCommand command)
        => new CoreRuleEngine().PreviewPlayCard(game.State, "P1", new("diana-duel-mana", Assert.IsType<string>(game.PromptFor("P1").PromptId), game.State.Tick, command));

    private static async Task<ConformanceCommand> Reject(Driver game, GameCommand command, string player = "P1", string intent = "diana-outside-duel-payment")
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = game.PromptFor(player).PromptId; raw["snapshotTick"] = game.State.Tick;
        var retained = new ConformanceCommand(player, intent, JsonSerializer.SerializeToElement(raw, Json));
        await game.ActRetained(retained, false);
        return retained;
    }

    private static MatchState Position(string cardNo)
    {
        var state = ArmamentMightTests.Position() with { Tick = 0 };
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["F"] = state.CardObjects["F"] with { ControllerId = null },
            ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", cardNo, "P1"),
            ["RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("RUNE", "UNL-R01", "P1"),
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·022/221", "P1") };
        return state with { CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = ["HOST", "RUNE"], Hand = ["GEAR"], LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND"), ["RUNE"] = new("P1", "BASE"), ["GEAR"] = new("P1", "HAND") },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }

    private static async Task Save(string scenario, string cardNo, MatchState initial, Driver game, PlayCostQuoteDto? quote)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_DIANA_DUEL_MANA_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, scenario + "-" + cardNo.Replace('/', '_').Replace('*', 'a') + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit normal-card midgame state, zero initial pools, actual move/focus/legend/tap/recycle/play commands. Driver checks privacy/recovery/dedup/original replay. A failed retained expected-reject assertion may leave driverState at the prior boundary; actualRetainedResult retains the true accepted result. No complete opening/L2/L3 claim.",
            initial, driverState = game.State, actualRetainedResult = game.LastRetainedResult, quote, commands = game.Commands(), events = game.Events()
        }, Json));
    }
}
