using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Explicit synthetic pool in a midgame position. This does not activate the
// newly reviewed legend or establish its card/format certification.
public sealed class UnitPlayOrAbilityRestrictedManaTests
{
    private const string Field = "unitPlayOrUnitAbilityOnlyMana";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("unit", true)] [InlineData("equipment", false)] [InlineData("spell", false)]
    public async Task BankedManaCanPayUnitPlayButNotEquipmentOrSpellPlay(string kind, bool expected)
    {
        var no = kind switch { "unit" => "SFD·143/221", "equipment" => "OGN·063/298", _ => "UNL-010/219" };
        var initial = WithPool(RestrictedPowerPlayTests.Position(no, false), 4);
        Assert.Equal(4, initial.RunePools["P1"].TotalStoredMana); Assert.Equal(0, initial.RunePools["P1"].Mana);
        var game = new Driver(initial); var before = MatchStateHasher.Hash(initial);
        var command = new PlayCardCommand("CARD", no, kind == "unit" ? [] : ["HOST"], Destination: kind == "unit" ? "BASE" : "");
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.Equal(expected, quote.CanPay); Assert.Equal(before, MatchStateHasher.Hash(initial));
        await Submit(game, command, expected);
        if (expected) { Assert.Equal(0, game.State.RunePools["P1"].TotalStoredMana); Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID"); }
        else { Assert.Equal(before, MatchStateHasher.Hash(game.State)); Assert.Equal(4, Bucket(game.State.RunePools["P1"])); Assert.Empty(game.Events()); }
        await game.Replay();
    }

    [Fact]
    public async Task UnitActivatedAbilityCanPayItsManaFromTheSameSingleBucket()
    {
        var state = ArmamentMightTests.Position(unit: P4ActivatedAbilityCatalog.ViCardNo, power: 3) with { Tick = 0 };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", P4ActivatedAbilityCatalog.ViCardNo, "P1") } };
        var initial = WithPool(state, 2); Assert.Equal(2, initial.RunePools["P1"].TotalStoredMana);
        var command = new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, []);
        Assert.True(Offered(initial, "HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId));
        var game = new Driver(initial); await Submit(game, command, true);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredMana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Single(game.State.StackItems); await game.Top(); Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task EquipmentActivatedAbilityCannotSpendTheUnitOnlyMana()
    {
        var state = ArmamentMightTests.Position("UNL-136/219") with { Tick = 0 };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "UNL-136/219", "P1") } };
        var initial = WithPool(state, 2); Assert.Equal(2, initial.RunePools["P1"].TotalStoredMana);
        var before = MatchStateHasher.Hash(initial); var game = new Driver(initial);
        var command = new ActivateAbilityCommand("GEAR", P4ActivatedAbilityCatalog.ScryingBlossomAbilityId, []);
        Assert.False(Offered(initial, "GEAR", P4ActivatedAbilityCatalog.ScryingBlossomAbilityId));
        await Submit(game, command, false); Assert.Equal(before, MatchStateHasher.Hash(game.State));
        Assert.Equal(2, Bucket(game.State.RunePools["P1"])); Assert.Empty(game.Events()); await game.Replay();
    }

    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)] [InlineData(int.MaxValue, true)]
    public void TheSingleManaBucketSurvivesJsonAndValidityChecks(int amount, bool valid)
    {
        var pool = Pool(amount); Assert.Equal(amount, Bucket(pool)); Assert.Equal(valid, pool.HasSupportedAmounts);
        Assert.Equal(pool, JsonSerializer.Deserialize<RunePool>(JsonSerializer.Serialize(pool, Web), Web));
    }

    private static bool Offered(MatchState state, string source, string ability)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? []).Where(c => c.Action == CommandTypes.ActivateAbility)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Any(r => Equals(r["sourceObjectId"], source) && Equals(r["abilityId"], ability));
    private static RunePool Pool(int amount) => JsonSerializer.Deserialize<RunePool>(
        $"{{\"mana\":0,\"power\":1,\"{Field}\":{amount}}}", Web)!;
    private static int Bucket(RunePool pool) => JsonSerializer.SerializeToElement(pool, Web).TryGetProperty(Field, out var value) ? value.GetInt32() : 0;
    private static MatchState WithPool(MatchState state, int amount) => state with {
        RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = Pool(amount) } };
    private static Task Submit(Driver game, GameCommand command, bool expected)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P1"].PromptId; raw["snapshotTick"] = game.State.Tick;
        return game.ActRetained(new("P1", "unit-mana-boundary-" + game.Commands().Length, JsonSerializer.SerializeToElement(raw, Web)), expected);
    }
}
