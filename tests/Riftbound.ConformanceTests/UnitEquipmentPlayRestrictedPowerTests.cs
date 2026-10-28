using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// The current regional Mordekaiser print says its [A] may be used to play a
// unit OR equipment. This is one synthetic resource in an explicit midgame
// position; it is not a claim that the new print can be played or activated.
public sealed class UnitEquipmentPlayRestrictedPowerTests
{
    private const string Field = "unitOrEquipmentPlayOnlyPower";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("unit", false, true)] [InlineData("unit", true, true)]
    [InlineData("equipment", false, true)] [InlineData("equipment", true, true)]
    [InlineData("spell", false, false)] [InlineData("spell", true, false)]
    public async Task OneRestrictedPowerCanBeSpentOnceOnlyToPlayAUnitOrEquipment(
        string kind, bool explicitSpend, bool expected)
    {
        var no = kind switch { "spell" => "UNL-010/219", "equipment" => "OGN·063/298", _ => "SFD·143/221" };
        var initial = WithPool(RestrictedPowerPlayTests.Position(no, false), Pool(10, 1));
        Assert.Equal(1, initial.RunePools["P1"].TotalStoredPower);
        Assert.Equal(0, initial.RunePools["P1"].TotalPower);
        var game = new Driver(initial);
        var command = new PlayCardCommand("CARD", no, kind == "unit" ? [] : ["HOST"],
            OptionalCosts: explicitSpend ? ["POWER_SPEND:A:1"] : [], Destination: kind == "unit" ? "BASE" : "");
        var before = MatchStateHasher.Hash(game.State); var count = game.Events().Length;
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.Equal(expected, quote.CanPay); Assert.Equal(before, MatchStateHasher.Hash(game.State));
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P1"].PromptId; raw["snapshotTick"] = game.State.Tick;
        await game.ActRetained(new("P1", "permanent-restricted-play", JsonSerializer.SerializeToElement(raw, Web)), expected);
        if (expected)
        {
            Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
            Assert.Single(game.Events().Skip(count), e => e.Event.Kind == "COST_PAID");
        }
        else
        {
            Assert.Equal(before, MatchStateHasher.Hash(game.State));
            Assert.Equal(1, Bucket(game.State.RunePools["P1"]));
        }
        await game.Replay();
    }

    [Fact]
    public async Task ItCannotPayEquipmentAssemblyEvenThoughEquipmentOnlyPowerCan()
    {
        var initial = WithPool(ArmamentMightTests.Position() with { Tick = 0 }, Pool(10, 1));
        Assert.Equal(1, initial.RunePools["P1"].TotalStoredPower);
        var before = MatchStateHasher.Hash(initial);
        var command = new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_RED"]);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("restricted-assembly", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State)); Assert.Equal(1, Bucket(result.State.RunePools["P1"]));
    }

    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)] [InlineData(int.MaxValue, true)]
    public void TheSingleBucketSurvivesStrictJsonAndCapacityChecks(int amount, bool supported)
    {
        var pool = Pool(0, amount);
        Assert.Equal(amount, Bucket(pool)); Assert.Equal(supported, pool.HasSupportedAmounts);
        Assert.Equal(supported ? amount : -1, pool.TotalStoredPower);
        Assert.Equal(pool, JsonSerializer.Deserialize<RunePool>(JsonSerializer.Serialize(pool, Web), Web));
    }

    private static RunePool Pool(int mana, int amount) => JsonSerializer.Deserialize<RunePool>(
        $"{{\"mana\":{mana},\"power\":0,\"{Field}\":{amount}}}", Web)!;
    private static int Bucket(RunePool pool) => JsonSerializer.SerializeToElement(pool, Web).GetProperty(Field).GetInt32();
    private static MatchState WithPool(MatchState state, RunePool pool) => state with {
        RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
}
