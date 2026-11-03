using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private const string BorrowedBanishPrefix = "BANISH_GRAVEYARD_CARD:";

    private static MatchState BorrowedBanishPosition(string donor)
    {
        var state = BorrowedRecyclePosition(donor);
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with {
                    Base = state.PlayerZones["P1"].Base.Where(id => id != "DONOR").ToArray(), LegendZone = ["DONOR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["DONOR"] = new("P1", "LEGEND") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(8, 0) } };
    }

    [Theory]
    [InlineData("RAD·143/167", "G1")]
    [InlineData("RAD·143/167", "G2")]
    [InlineData("RAD·170*/167", "G1")]
    [InlineData("RAD·170*/167", "G2")]
    [InlineData("RAD·170/167", "G1")]
    [InlineData("RAD·170/167", "G2")]
    public async Task BorrowedGraveyardBanishPaysEitherPermanentAndImmediatelyBanksRestrictedPower(string donor, string cost)
    {
        var initial = BorrowedBanishPosition(donor);
        var row = Assert.Single(ResourceRows(initial));
        var ability = (string)row["abilityId"]!;
        Assert.Equal(1, row["requiredAdditionalCostChoiceCount"]);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]);
        Assert.Equal(new[] { "G1", "G2", "G4" }.Select(id => BorrowedBanishPrefix + id), choices.Select(c => c.Id));
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", ability, [], [BorrowedBanishPrefix + cost]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        Assert.Contains(cost, game.State.PlayerZones["P1"].Banished);
        Assert.DoesNotContain(cost, game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("BANISHED", game.State.ObjectLocations[cost].Zone);
        Assert.Equal(initial.CardObjects[cost].ObjectGeneration + 1, game.State.CardObjects[cost].ObjectGeneration);
        Assert.Equal(1, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(initial.TimingState, game.State.TimingState);
        Assert.Equal(initial.PriorityPlayerId, game.State.PriorityPlayerId);
        Assert.Equal(initial.FocusPlayerId, game.State.FocusPlayerId);
        Assert.Equal(initial.RunePools["P1"].Mana, game.State.RunePools["P1"].Mana);
        var banished = Assert.Single(game.Events(), e => e.Event.Kind == "CARD_BANISHED");
        Assert.Equal(true, banished.Event.Payload["isCost"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TARGET_SELECTED");
        await RejectBorrowedResource(game.State, "HOST", ability, []);
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·143/167")]
    [InlineData("RAD·170*/167")]
    [InlineData("RAD·170/167")]
    public async Task BorrowedGraveyardBanishRejectsIllegalCostsSourcesAndOverflowWithoutMutation(string donor)
    {
        var initial = BorrowedBanishPosition(donor);
        var ability = (string)Assert.Single(ResourceRows(initial))["abilityId"]!;
        async Task Reject(MatchState state, string player, string[] costs, string[]? targets = null)
        {
            var hash = MatchStateHasher.Hash(state);
            var result = await new CoreRuleEngine().ResolveAsync(state,
                new("borrowed-banish-rejected", player, CommandTypes.ActivateAbility),
                new ActivateAbilityCommand("HOST", ability, targets ?? [], costs), default);
            Assert.False(result.Accepted);
            Assert.Empty(result.Events);
            Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        }
        foreach (var costs in new[] { Array.Empty<string>(), ["G1", "G1"], ["G1", "G2"],
            ["G3"], ["ENEMY-G"], ["HOST"], ["MISSING"] })
            await Reject(initial, "P1", costs.Select(id => BorrowedBanishPrefix + id).ToArray());
        await Reject(initial, "P1", [BorrowedBanishPrefix + "G1"], ["ALLY"]);
        await Reject(initial, "P2", [BorrowedBanishPrefix + "G1"]);
        var full = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(8, 0, equipmentOnlyPower: int.MaxValue) } };
        await Reject(full, "P1", [BorrowedBanishPrefix + "G1"]);
        Assert.Empty(ResourceRows(full));
        var empty = initial;
        foreach (var id in new[] { "G1", "G2", "G4" }) empty = RadUnitEffectTestFixture.Place(empty, id, "P1", "HAND");
        await Reject(empty, "P1", [BorrowedBanishPrefix + "G1"]);
        Assert.Empty(ResourceRows(empty));
        var exhausted = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = true } } };
        await Reject(exhausted, "P1", [BorrowedBanishPrefix + "G1"]);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("equipment")]
    [InlineData("spell")]
    public async Task BorrowedGraveyardBanishPowerPaysOnlyActualUnitOrEquipmentPlays(string kind)
    {
        var no = kind switch { "unit" => "SFD·143/221", "equipment" => "OGN·063/298", _ => "UNL-010/219" };
        var initial = HandDiscardObserverTests.Put(BorrowedBanishPosition("RAD·143/167"), "CARD", no, "P1", "HAND");
        var ability = (string)Assert.Single(ResourceRows(initial))["abilityId"]!;
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", ability, [], [BorrowedBanishPrefix + "G1"]));
        var command = new PlayCardCommand("CARD", no, kind == "unit" ? [] : ["HOST"],
            OptionalCosts: ["POWER_SPEND:A:1", "POWER_SOURCE:UNIT_OR_EQUIPMENT_PLAY_ONLY:1"], Destination: kind == "unit" ? "BASE" : "");
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.Equal(kind != "spell", quote.CanPay);
        if (kind == "spell") await RadGearTestPosition.Reject(game.State, "P1", command);
        else
        {
            await game.Act("P1", command);
            Assert.Equal(0, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        }
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedGraveyardBanishAppearsDuringInstructionPaymentEvenWhenItsPowerCannotPayIt()
    {
        var costs = BorrowedBanishPosition("RAD·143/167");
        var initial = OfficialHoldSequenceTests.AddLegend(RestrictedManaLifecycleTests.Position(), "RAD·143/167");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["LUX"] = OfficialGarenConquestBattlefieldCountTests.Printed("LUX", "OGN·111/298", "P1"),
            ["LEGEND"] = initial.CardObjects["LEGEND"] with { IsExhausted = true },
            ["G1"] = costs.CardObjects["G1"], ["G2"] = costs.CardObjects["G2"] };
        initial = initial with { CardObjects = cards,
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(1, 0) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["G1"] = new("P1", "GRAVEYARD"), ["G2"] = new("P1", "GRAVEYARD") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Graveyard = ["G1", "G2"] } } };
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"]));
        await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        var options = request.Options.Where(o => o.Id.StartsWith("RESOURCE:LUX:BORROWED:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, options.Length);
        var option = Assert.Single(options, o => o.Id.EndsWith(BorrowedBanishPrefix + "G2", StringComparison.Ordinal));
        Assert.Contains("G2", option.ObjectIds ?? []);
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
        request = game.State.PendingRuleChoice!.Request;
        Assert.Equal(1, request.Preview!.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        Assert.DoesNotContain(request.Options, o => o.Id == "SPEND_POWER:1");
        Assert.DoesNotContain(request.Options, o => o.Id.StartsWith("RESOURCE:LUX:", StringComparison.Ordinal));
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:0"]));
        Assert.Contains("G2", game.State.PlayerZones["P1"].Banished);
        Assert.Equal(1, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        Assert.True(game.State.CardObjects["LUX"].IsExhausted);
        Assert.Single(game.Events(), e => e.Event.Kind == "RESOURCE_SKILL_RESOLVED");
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }
}
