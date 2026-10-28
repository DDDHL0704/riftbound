using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedPowerSourceSelectionTests
{
    private const string Equipment = "OGN·063/298";
    private const string UnionField = "unitOrEquipmentPlayOnlyPower";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("EQUIPMENT_ONLY", 1, 0, 1)]
    [InlineData("UNIT_OR_EQUIPMENT_PLAY_ONLY", 1, 1, 0)]
    [InlineData("UNRESTRICTED", 0, 1, 1)]
    public async Task EquipmentPlayDebitsThePlayersChosenWildcardSourceAndReplays(
        string source, int ordinary, int equipment, int union)
    {
        var game = new Driver(Position()); var command = Command(["POWER_SPEND:A:1", "POWER_SOURCE:" + source + ":1"]);
        var before = MatchStateHasher.Hash(game.State);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.IsValid, quote.Message); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.AvailableRainbowPower); Assert.Equal(2, quote.Cost.RemainingRainbowPower);
        Assert.Equal(before, MatchStateHasher.Hash(game.State));
        await Submit(game, command, true);
        var pool = game.State.RunePools["P1"];
        Assert.Equal(ordinary, pool.Power); Assert.Equal(equipment, pool.EquipmentOnlyPower);
        Assert.Equal(union, Union(pool)); Assert.Equal(2, pool.TotalStoredPower);
        var cost = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event;
        var paidSources = JsonSerializer.SerializeToElement(cost.Payload["selectedPowerSources"], Web);
        Assert.Equal(1, paidSources.GetProperty(source).GetInt32());
        await game.Replay();
    }

    [Theory]
    [InlineData("POWER_SOURCE:SPELL_ONLY:1")]
    [InlineData("POWER_SOURCE:UNIT_OR_EQUIPMENT_PLAY_ONLY:2")]
    [InlineData("POWER_SOURCE:UNIT_OR_EQUIPMENT_PLAY_ONLY:0")]
    [InlineData("POWER_SOURCE:UNIT_OR_EQUIPMENT_PLAY_ONLY:1,UNIT_OR_EQUIPMENT_PLAY_ONLY:1")]
    [InlineData("POWER_SOURCE:UNKNOWN:1")]
    public async Task InvalidOrIneligibleSourceCannotCommitAnyPartOfThePlay(string selected)
    {
        var game = new Driver(Position()); var command = Command(["POWER_SPEND:A:1", selected]);
        var before = MatchStateHasher.Hash(game.State); var count = game.Events().Length;
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.False(quote.CanPay); await Submit(game, command, false);
        Assert.Equal(before, MatchStateHasher.Hash(game.State)); Assert.Equal(count, game.Events().Length);
        Assert.Equal(3, game.State.RunePools["P1"].TotalStoredPower); await game.Replay();
    }

    private static PlayCardCommand Command(IReadOnlyList<string> costs)
        => new("CARD", Equipment, ["HOST"], OptionalCosts: costs);

    private static MatchState Position()
    {
        var state = RestrictedPowerPlayTests.Position(Equipment, false);
        var pool = JsonSerializer.Deserialize<RunePool>(
            "{\"mana\":10,\"power\":1,\"equipmentOnlyPower\":1,\"unitOrEquipmentPlayOnlyPower\":1}", Web)!;
        Assert.Equal(3, pool.TotalStoredPower);
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
    }

    private static int Union(RunePool pool) => JsonSerializer.SerializeToElement(pool, Web).TryGetProperty(UnionField, out var value)
        ? value.GetInt32() : 0;

    private static Task Submit(Driver game, PlayCardCommand command, bool accepted)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P1"].PromptId; raw["snapshotTick"] = game.State.Tick;
        return game.ActRetained(new("P1", "chosen-power-source", JsonSerializer.SerializeToElement(raw, Web)), accepted);
    }
}
