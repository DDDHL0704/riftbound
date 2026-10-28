using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedPowerSourceProjectionTests
{
    private const string Field = "unitOrEquipmentPlayOnlyPower";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("EQUIPMENT_ONLY", 0, 1, 1)]
    [InlineData("UNIT_OR_EQUIPMENT_PLAY_ONLY", 1, 0, 1)]
    [InlineData("UNRESTRICTED", 1, 1, 0)]
    public void QuoteAndPromptExposeTheExactChosenSourceWithoutChangingState(string selected, int equipment, int union, int ordinary)
    {
        var state = Position(); var before = MatchStateHasher.Hash(state);
        var command = new PlayCardCommand("CARD", "OGN·063/298", ["HOST"],
            OptionalCosts: ["POWER_SPEND:A:1", "POWER_SOURCE:" + selected + ":1"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(1, quote.Cost!.AvailableWildcardSources!["EQUIPMENT_ONLY"]);
        Assert.Equal(1, quote.Cost.AvailableWildcardSources["UNIT_OR_EQUIPMENT_PLAY_ONLY"]);
        Assert.Equal(1, quote.Cost.AvailableWildcardSources["UNRESTRICTED"]);
        var remaining = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(quote.Cost.RemainingWildcardSources);
        Assert.Equal(equipment, remaining.GetValueOrDefault("EQUIPMENT_ONLY"));
        Assert.Equal(union, remaining.GetValueOrDefault("UNIT_OR_EQUIPMENT_PLAY_ONLY"));
        Assert.Equal(ordinary, remaining.GetValueOrDefault("UNRESTRICTED"));
        Assert.Equal(before, MatchStateHasher.Hash(state));
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements, r => Equals(r["sourceObjectId"], "CARD"));
        var advertised = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(requirement["availableWildcardSources"]);
        Assert.Equal(quote.Cost.AvailableWildcardSources, advertised);
        var grammar = JsonSerializer.SerializeToElement(requirement["powerSpendSelection"], Web);
        Assert.Equal("POWER_SOURCE:", grammar.GetProperty("sourceChoicePrefix").GetString());
        Assert.True(grammar.GetProperty("sourceChoiceRequiresPowerSpend").GetBoolean());
    }

    [Fact]
    public void RecoveryRejectsForgedOrMissingRestrictedPowerInEveryPlayerAndSpectatorView()
    {
        var state = Position();
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id,
            id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot))
        {
            var player = Assert.IsAssignableFrom<IDictionary<string, object?>>(snapshot.Players!["P1"]);
            var pool = Assert.IsAssignableFrom<IDictionary<string, object?>>(player["runePool"]);
            var original = pool[Field]; pool[Field] = 2;
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            pool.Remove(Field);
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            pool[Field] = original;
        }
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
    }

    [Theory]
    [InlineData("\"1\"")] [InlineData("null")] [InlineData("2147483648")]
    public void NewResourceFieldRejectsNonIntegerOrOverflowingJson(string value)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RunePool>(
            "{\"mana\":0,\"power\":0,\"" + Field + "\":" + value + "}", Web));

    private static MatchState Position()
    {
        var state = RestrictedPowerPlayTests.Position("OGN·063/298", false);
        var pool = JsonSerializer.Deserialize<RunePool>(
            "{\"mana\":10,\"power\":1,\"equipmentOnlyPower\":1,\"unitOrEquipmentPlayOnlyPower\":1}", Web)!;
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
    }
}
