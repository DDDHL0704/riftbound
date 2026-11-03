using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedRecyclePosition(string donor)
    {
        var state = BorrowedConditionPosition(donor);
        foreach (var (id, card, player, zone) in new[] {
            ("G1", "OGN·001/298", "P1", "GRAVEYARD"),
            ("G2", "SFD·022/221", "P1", "GRAVEYARD"),
            ("G3", "OGN·104/298", "P1", "GRAVEYARD"),
            ("G4", "OGN·001/298", "P1", "GRAVEYARD"),
            ("ENEMY-G", "OGN·001/298", "P2", "GRAVEYARD"),
            ("TOP", "OGN·001/298", "P1", "MAIN_DECK") })
        {
            state = HandDiscardObserverTests.Put(state, id, card, player, "HAND");
            var zones = state.PlayerZones[player];
            state = state with {
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, zone) },
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = zones with {
                    Hand = zones.Hand.Where(c => c != id).ToArray(),
                    Graveyard = zone == "GRAVEYARD" ? [..zones.Graveyard, id] : zones.Graveyard,
                    MainDeck = zone == "MAIN_DECK" ? [id, ..zones.MainDeck] : zones.MainDeck } } };
        }
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(8, 0, new Dictionary<string, int> { ["red"] = 4 }) } };
    }

    private static IReadOnlyDictionary<string, object?> BorrowedRecycleRequirement(MatchState state)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
            .Where(c => c.Enabled && c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Single(r => Equals(r.GetValueOrDefault("sourceObjectId"), "HOST")
                && r.GetValueOrDefault("abilityLabel") is string label && label.Contains("回收", StringComparison.Ordinal));

    [Theory]
    [InlineData("OGN·099/298", "none")]
    [InlineData("OGN·099/298", "source")]
    [InlineData("SFD·019/221", "none")]
    [InlineData("SFD·019/221", "source")]
    public async Task BorrowedRecycleCostsPayBeforeResponsesAndKeepTheIndependentEffect(string donor, string response)
    {
        var initial = BorrowedRecyclePosition(donor);
        var draw = donor == "OGN·099/298";
        var ids = draw ? new[] { "G1", "G2", "G3" } : ["G1"];
        var row = BorrowedRecycleRequirement(initial);
        var id = (string)row["abilityId"]!;
        Assert.Equal(ids.Length, row["requiredAdditionalCostChoiceCount"]);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]);
        Assert.Equal(draw ? new[] { "RECYCLE_GRAVEYARD_CARD:G1", "RECYCLE_GRAVEYARD_CARD:G2", "RECYCLE_GRAVEYARD_CARD:G3", "RECYCLE_GRAVEYARD_CARD:G4" }
            : ["RECYCLE_GRAVEYARD_CARD:G1", "RECYCLE_GRAVEYARD_CARD:G4"], choices.Select(c => c.Id));
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [],
            OptionalCosts: ids.Select(c => "RECYCLE_GRAVEYARD_CARD:" + c).ToArray()));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        Assert.Equal(initial.RunePools["P1"].Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(initial.RunePools["P1"].PowerByTrait["red"] - (draw ? 0 : 1), game.State.RunePools["P1"].PowerByTrait["red"]);
        Assert.All(ids, card => {
            Assert.DoesNotContain(card, game.State.PlayerZones["P1"].Graveyard);
            Assert.Contains(card, game.State.PlayerZones["P1"].MainDeck);
            Assert.Equal("MAIN_DECK", game.State.ObjectLocations[card].Zone);
        });
        Assert.Equal(initial.RngCursor + (draw ? 1 : 0), game.State.RngCursor);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck.Take(initial.PlayerZones["P1"].MainDeck.Count));
        Assert.Empty(BorrowedCreatedUnits(game.State));
        Assert.DoesNotContain("TOP", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.Events(), e => e.Event.Kind == "CARDS_RECYCLED");
        if (response != "none")
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [response == "source" ? "HOST" : "DONOR"]));
            await game.Top();
            Assert.Equal("HAND", game.State.ObjectLocations[response == "source" ? "HOST" : "DONOR"].Zone);
        }
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Empty(game.State.StackItems);
        if (draw) Assert.Contains("TOP", game.State.PlayerZones["P1"].Hand);
        else
        {
            var robot = Assert.Single(BorrowedCreatedUnits(game.State));
            Assert.Equal(3, robot.Power);
            Assert.Equal("BASE", game.State.ObjectLocations[robot.ObjectId].Zone);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·099/298")]
    [InlineData("SFD·019/221")]
    public async Task BorrowedRecycleCostsRejectWrongQuantityOwnershipTypeAndResourcesWithoutMutation(string donor)
    {
        var initial = BorrowedRecyclePosition(donor);
        var draw = donor == "OGN·099/298";
        var id = (string)BorrowedRecycleRequirement(initial)["abilityId"]!;
        var good = draw ? new[] { "G1", "G2", "G3" } : ["G1"];
        var invalid = draw ? new[] { Array.Empty<string>(), ["G1", "G2"], ["G1", "G1", "G3"],
                ["G1", "G2", "ENEMY-G"], ["G1", "G2", "HOST"], ["G1", "G2", "MISSING"], ["G1", "G2", "G3", "G4"] }
            : new[] { Array.Empty<string>(), ["G1", "G4"], ["G2"], ["G3"], ["ENEMY-G"], ["HOST"], ["MISSING"] };
        async Task Reject(MatchState state, string player, string[] selected)
        {
            var hash = MatchStateHasher.Hash(state);
            var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-borrowed-recycle", player, CommandTypes.ActivateAbility),
                new ActivateAbilityCommand("HOST", id, [], OptionalCosts: selected.Select(c => "RECYCLE_GRAVEYARD_CARD:" + c).ToArray()), default);
            Assert.False(result.Accepted, $"{donor}: {player} paid {string.Join(',', selected)}");
            Assert.Empty(result.Events);
            Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        }
        foreach (var selected in invalid) await Reject(initial, "P1", selected);
        await Reject(initial, "P2", good);
        await Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, 20) } }, "P1", good);
        if (!draw) await Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(8, 0, new Dictionary<string, int> { ["blue"] = 4 }) } }, "P1", good);
        var exhausted = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = true } } };
        await Reject(exhausted, "P1", good);
    }
}
