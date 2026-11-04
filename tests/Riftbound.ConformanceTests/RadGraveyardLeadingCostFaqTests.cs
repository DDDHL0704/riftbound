using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadGraveyardLeadingCostFaqTests
{
    // DOC-17 p17 Q1/Q3: the leading cost is part of playing the original unit.
    [Theory]
    [InlineData(null)]
    [InlineData("RAD·143/167")]
    [InlineData("RAD·170*/167")]
    [InlineData("RAD·170/167")]
    public async Task PhoenixLeadingCostUsesRedOrActuallyProducedLegendPower(string? legend)
    {
        var initial = Position(legend);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("RAY", DestructionGraveyardFamilyTests.Ray, ["TARGET"]));
        await game.Top();
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.ManaCost); Assert.Equal(1, payment.PowerCostByTrait["red"]);
        if (legend is not null)
        {
            await Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.MordekaiserLegendAbilityId,
                [], ["BANISH_GRAVEYARD_CARD:FUEL"]));
            Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(1, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
            Assert.Contains("FUEL", game.State.PlayerZones["P1"].Banished);
            Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
            Assert.Equal(payment.PaymentId, game.State.PendingPayment!.PaymentId);
        }
        await game.ColdCheckpoint();
        var before = game.State.RunePools["P1"];
        var pay = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]);
        await Reject(game.State, "P2", pay);
        await game.Act("P1", pay);
        Assert.Equal(before.Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        Assert.NotNull(Assert.Single(game.State.StackItems).TriggerCost);
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.ColdCheckpoint();
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("PHOENIX", DestructionGraveyardFamilyTests.Phoenix, [], Destination: "BASE"));
        Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Contains("PHOENIX", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("PHOENIX", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(1, game.State.CardObjects["PHOENIX"].ObjectGeneration);
        Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData("spell")]
    [InlineData("equipment")]
    public async Task UnrelatedRestrictedPowerCannotPayTheLeadingUnitPlayCost(string kind)
    {
        var initial = Position(null);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(30, 0, new Dictionary<string, int> { ["red"] = 1 }, spellOnlyPower: kind == "spell" ? 1 : 0,
                equipmentOnlyPower: kind == "equipment" ? 1 : 0) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("RAY", DestructionGraveyardFamilyTests.Ray, ["TARGET"]));
        await game.Top();
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.Contains("PHOENIX", game.State.PlayerZones["P1"].Graveyard);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Equal(kind == "spell" ? 1 : 0, game.State.RunePools["P1"].SpellOnlyPower);
        Assert.Equal(kind == "equipment" ? 1 : 0, game.State.RunePools["P1"].EquipmentOnlyPower);
        await game.Replay();
    }

    private static MatchState Position(string? legend)
    {
        var state = DestructionGraveyardFamilyTests.Position(smallTarget: true);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(30, 0, new Dictionary<string, int> { ["red"] = legend is null ? 2 : 1 }) } };
        if (legend is null) return state;
        state = OfficialHoldSequenceTests.AddLegend(state, legend);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", legend, "P1") } };
        return RadGearTestPosition.Put(state, "FUEL", "OGN·219/298", "P1", "GRAVEYARD");
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-faq-leading-cost", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
