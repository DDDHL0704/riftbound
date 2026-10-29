using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class MordekaiserLegendResourceTests
{
    private const string Ability = LegendActionAbilityCatalog.MordekaiserLegendAbilityId;
    private const string Prefix = "BANISH_GRAVEYARD_CARD:";
    public static IEnumerable<object[]> Prints() => new[] { "RAD·143/167", "RAD·170*/167", "RAD·170/167" }.Select(no => new object[] { no });
    public static IEnumerable<object[]> Costs() => Prints().SelectMany(row => new[] { "UNIT_COST", "GEAR_COST" }.Select(id => new object[] { row[0], id }));
    public static IEnumerable<object[]> InvalidCosts() => Prints().SelectMany(row => new[] {
        "none", "duplicate", "two", "spell", "enemy", "hand", "forged", "target", "exhausted", "capacity", "source"
    }.Select(kind => new object[] { row[0], kind }));

    [Theory, MemberData(nameof(Costs))]
    public async Task EachOfficialPrintingBanishesEitherPermanentAsACostAndImmediatelyBanksOneRestrictedPower(string no, string id)
    {
        var initial = Position(no);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var candidate = Assert.Single(prompt.Candidates ?? [], c => c.Action == CommandTypes.LegendAct);
        Assert.Contains(candidate.OptionalCosts ?? [], c => c.Id == Prefix + id);
        Assert.DoesNotContain(candidate.OptionalCosts ?? [], c => c.Id == Prefix + "SPELL_COST" || c.Id == Prefix + "ENEMY_COST");
        var game = new Driver(initial);
        await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], [Prefix + id]));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.DoesNotContain(id, game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(id, game.State.PlayerZones["P1"].Banished);
        Assert.Equal(initial.CardObjects[id].ObjectGeneration + 1, game.State.CardObjects[id].ObjectGeneration);
        Assert.Equal(1, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(initial.FocusPlayerId, game.State.FocusPlayerId);
        Assert.Equal(initial.PriorityPlayerId, game.State.PriorityPlayerId);
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_BANISHED");
        Assert.Single(game.Events(), e => e.Event.Kind == "POWER_GAINED");
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TARGET_SELECTED");
        OfficialGraveyardRecastTests.Restore(game.State);
        await game.Replay();

        var before = MatchStateHasher.Hash(game.State);
        var repeated = await new CoreRuleEngine().ResolveAsync(game.State, new("morde-repeat", "P1", CommandTypes.LegendAct),
            new LegendActCommand("LEGEND", Ability, [], [Prefix + "UNIT_COST"]), default);
        Assert.False(repeated.Accepted); Assert.Empty(repeated.Events);
        Assert.Equal(before, MatchStateHasher.Hash(repeated.State));
    }

    [Theory, MemberData(nameof(InvalidCosts))]
    public async Task InvalidCostSourceAndUnrepresentableGainRejectWithoutBanishmentOrExhaustion(string no, string kind)
    {
        var state = Position(no);
        string[] costs = kind switch {
            "none" => [], "duplicate" => [Prefix + "UNIT_COST", Prefix + "UNIT_COST"],
            "two" => [Prefix + "UNIT_COST", Prefix + "GEAR_COST"],
            "spell" => [Prefix + "SPELL_COST"], "enemy" => [Prefix + "ENEMY_COST"],
            "hand" => [Prefix + "CARD"], "forged" => [Prefix + "INVENTED"], _ => [Prefix + "UNIT_COST"] };
        if (kind is "exhausted" or "source") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = kind == "exhausted", CardNo = kind == "source" ? "SFD·189/221" : no } } };
        if (kind == "capacity") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(10, 0, unitOrEquipmentPlayOnlyPower: int.MaxValue) } };
        var hash = MatchStateHasher.Hash(state); var view = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var command = new LegendActCommand("LEGEND", Ability, kind == "target" ? ["HOST"] : [], costs);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("morde-invalid", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(view, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }

    [Theory, InlineData("unit"), InlineData("equipment"), InlineData("spell")]
    public async Task TheActuallyProducedPowerPaysOnlyAUnitOrEquipmentPlay(string kind)
    {
        var card = kind switch { "unit" => "SFD·143/221", "equipment" => "OGN·063/298", _ => "UNL-010/219" };
        var game = new Driver(Position("RAD·143/167", card));
        await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], [Prefix + "UNIT_COST"]));
        var command = new PlayCardCommand("CARD", card, kind == "unit" ? [] : ["HOST"],
            OptionalCosts: ["POWER_SPEND:A:1", "POWER_SOURCE:UNIT_OR_EQUIPMENT_PLAY_ONLY:1"], Destination: kind == "unit" ? "BASE" : "");
        var before = MatchStateHasher.Hash(game.State);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.Equal(kind != "spell", quote.CanPay);
        if (kind == "spell") {
            var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("morde-cannot-pay-spell", "P1", command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
            Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        } else {
            await game.Act("P1", command);
            Assert.Equal(0, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        }
        await game.Replay();
    }

    [Fact]
    public async Task AResourceSkillCanBeUsedDuringTheOpponentsPriorityWithoutOfferingAResponseToIt()
    {
        var initial = Position("RAD·170/167") with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed, PriorityPlayerId = "P1" };
        var game = new Driver(initial);
        await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], [Prefix + "GEAR_COST"]));
        Assert.Empty(game.State.StackItems); Assert.Equal("P1", game.State.PriorityPlayerId);
        Assert.Equal("P2", game.State.ActivePlayerId);
        await game.Replay();
    }

    [Fact]
    public async Task AnInstructionPaymentOffersEachEligibleGraveyardCostAndPreservesTheRestriction()
    {
        var initial = AddGraveyard(OfficialHoldSequenceTests.AddLegend(RestrictedManaLifecycleTests.Position(), "RAD·143/167"));
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(1, 0) } };
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"]));
        await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        var prefix = "LEGEND_RESOURCE:LEGEND:" + Ability + ":";
        var options = request.Options.Where(option => option.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, options.Length);
        var chosen = Assert.Single(options, option => option.Id == prefix + Prefix + "GEAR_COST");
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [chosen.Id]));
        request = game.State.PendingRuleChoice!.Request;
        Assert.Equal(1, request.Preview!.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        Assert.DoesNotContain(request.Options, option => option.Id == "SPEND_POWER:1");
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:0"]));
        Assert.Contains("GEAR_COST", game.State.PlayerZones["P1"].Banished);
        Assert.Equal(1, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Fact]
    public void AnEmptyOrIneligibleGraveyardNeverAdvertisesTheResourceAction()
    {
        var state = Position("RAD·143/167");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Graveyard = ["SPELL_COST"] } } };
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [],
            c => c.Action == CommandTypes.LegendAct && (c.Sources ?? []).Any(s => s.Id == "LEGEND"));
    }

    internal static MatchState Position(string no, string card = "SFD·143/221")
        => AddGraveyard(OfficialHoldSequenceTests.AddLegend(RestrictedPowerPlayTests.Position(card, false), no));

    private static MatchState AddGraveyard(MatchState state)
    {
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
        cards["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", cards["LEGEND"].CardNo!, "P1");
        foreach (var (id, no, player) in new[] { ("UNIT_COST", "SFD·143/221", "P1"), ("GEAR_COST", "OGN·063/298", "P1"),
            ("SPELL_COST", "UNL-010/219", "P1"), ("ENEMY_COST", "SFD·143/221", "P2") }) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
            locations[id] = new(player, "GRAVEYARD");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Graveyard = ["UNIT_COST", "GEAR_COST", "SPELL_COST"] },
                ["P2"] = state.PlayerZones["P2"] with { Graveyard = ["ENEMY_COST"] } } };
    }
}
