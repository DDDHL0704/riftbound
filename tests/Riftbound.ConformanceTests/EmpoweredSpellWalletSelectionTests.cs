using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EmpoweredSpellWalletSelectionTests
{
    private const string Aura = "VEN·055";
    private const string Spell = "SFD·182/221";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("blue", false)] [InlineData("red", false)]
    [InlineData("blue", true)] [InlineData("red", true)]
    public async Task TheSelectedWalletFindsItsAffordableNetPriceAcrossIndependentPrintedEchoAndDiscountChoices(
        string trait, bool explicitDiscount)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await Empower(game);
            var costs = new List<string> { "PRINTED_POWER:red:1", "ECHO:GRANTED:1", "POWER_SPEND:" + trait + ":1" };
            if (explicitDiscount) costs.Add("SPELL_POWER_REDUCTION:" + (trait == "blue" ? "red" : "blue") + ":1");
            var command = new PlayCardCommand("SPELL", Spell, [], OptionalCosts: costs);
            var before = game.State; var hash = MatchStateHasher.Hash(before);
            var quote = new CoreRuleEngine().PreviewPlayCard(before, "P1", PlayCostPreviewTests.Request(before, command));
            Assert.Equal(hash, MatchStateHasher.Hash(before)); Assert.True(quote.IsValid && quote.CanPay, quote.Message);
            Assert.Equal(0, quote.Cost!.GenericPower);
            Assert.Equal(new Dictionary<string, int> { [trait] = 1 }, quote.Cost.PowerByTrait);
            await game.Act("P1", command);
            Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount);
            Assert.Equal(before.RunePools["P1"].Mana - quote.Cost.Mana, game.State.RunePools["P1"].Mana);
            Assert.Equal(new Dictionary<string, int> { ["blue"] = trait == "blue" ? 1 : 2,
                ["red"] = trait == "red" ? 1 : 2 }, game.State.RunePools["P1"].PowerByTrait);
            Assert.Equal(quote.Cost.RemainingPowerByTrait!.OrderBy(p => p.Key),
                game.State.RunePools["P1"].PowerByTrait.OrderBy(p => p.Key));
            var paid = Assert.Single(game.Events().Where(e => e.Tick > before.Tick), e => e.Event.Kind == "COST_PAID").Event;
            Assert.Equal(1, Convert.ToInt32(paid.Payload["totalPowerCost"]));
            Assert.Equal(1, Convert.ToInt32(paid.Payload["empoweredSpellCostReductionPower"]));
            Assert.Equal(quote.Cost.PowerByTrait.OrderBy(p => p.Key),
                JsonSerializer.SerializeToElement(paid.Payload["powerByTrait"], Json)
                    .Deserialize<Dictionary<string, int>>(Json)!.OrderBy(p => p.Key));
            await game.Top(); await game.Replay();
        }
        finally { await Save("net-price-" + trait + "-" + explicitDiscount, initial, game); }
    }

    [Theory]
    [InlineData("blue")] [InlineData("red")]
    public async Task ADiscountChoiceCannotAlsoStandInForAnIncompatibleActualWalletDebit(string trait)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await Empower(game);
            var command = new PlayCardCommand("SPELL", Spell, [], OptionalCosts: ["PRINTED_POWER:red:1", "ECHO:GRANTED:1",
                "SPELL_POWER_REDUCTION:blue:1", trait == "blue" ? "POWER_SPEND:blue:1" : "POWER_SPEND:red:2"]);
            var state = game.State; var hash = MatchStateHasher.Hash(state);
            var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state), Json);
            var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state), Json);
            var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
            Assert.False(quote.CanPay); Assert.Equal(hash, MatchStateHasher.Hash(state));
            var rejected = await new CoreRuleEngine().ResolveAsync(state, new("bad-discount-wallet", "P1", command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode); Assert.Empty(rejected.Events);
            Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
            Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State), Json));
            Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(rejected.State), Json));
            await game.Replay();
        }
        finally { await Save("incompatible-wallet-" + trait, initial, game); }
    }

    private static async Task Empower(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("UNIT", Aura, []));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
    }

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(Aura);
        return state with {
            RoomId = "empowered-wallet-choice", Tick = 0,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", Spell, "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SPELL"] = new("P1", "HAND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [..state.PlayerZones["P1"].Hand, "SPELL"] } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 0, new Dictionary<string, int> { ["blue"] = 2, ["red"] = 2 }), ["P2"] = new(20, 20) },
            UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(state.UntilEndOfTurnEffects, "P1") };
    }

    private static async Task Save(string name, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_EMPOWERED_PAYMENT_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit midgame with real VEN055 play and Empower, printed allocation, granted Echo, discount and wallet choices; not canonical opening or SQL.",
            initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
    }
}
