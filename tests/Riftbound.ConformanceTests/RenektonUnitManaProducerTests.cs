using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Exact reviewed CN API keys. These controlled positions are not normal
// submitted-deck openings or a complete certification of the legend.
public sealed class RenektonUnitManaProducerTests
{
    private const string Ability = "LEGEND_REACTION_PAY_2_POWER_EXHAUST_GAIN_2_UNIT_MANA";

    [Theory]
    [InlineData("VEN·141")]
    [InlineData("VEN·190")]
    [InlineData("VEN·190*")]
    public async Task ExactLegendPaysPowerAndBanksManaForARealUnitPlay(string cardNo)
    {
        var initial = AddLegend(RestrictedPowerPlayTests.Position("SFD·143/221", false), cardNo);
        initial = Pool(initial, new(2, 3));
        var game = new Driver(initial);
        var command = new LegendActCommand("LEGEND", Ability, []);
        Assert.Contains(ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [],
            candidate => candidate.Action == CommandTypes.LegendAct && (candidate.Sources ?? []).Any(source => source.Id == "LEGEND"));
        await game.Act("P1", command);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(1, game.State.RunePools["P1"].Power);
        Assert.Equal(2, game.State.RunePools["P1"].Mana);
        Assert.Equal(2, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(initial.PriorityPlayerId, game.State.PriorityPlayerId);
        Assert.Single(game.Events(), e => e.Event.Kind == "MANA_GAINED");

        var play = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts:
            ["MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2,UNRESTRICTED:2"], Destination: "BASE");
        Assert.True(new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play)).CanPay);
        await game.Act("P1", play);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredMana);
        Assert.Equal(0, game.State.RunePools["P1"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, int.MaxValue - 1)]
    public async Task InsufficientPowerOrUnrepresentableGainDoesNotSpendOrExhaust(int power, int banked)
    {
        var initial = Pool(AddLegend(RestrictedManaLifecycleTests.Position(), "VEN·141"),
            new(0, power, unitPlayOrUnitAbilityOnlyMana: banked));
        var command = new LegendActCommand("LEGEND", Ability, []);
        var before = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("renekton-atomic-reject", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.False(result.State.CardObjects["LEGEND"].IsExhausted);
    }

    [Fact]
    public async Task LegendPowerCostUsesThePlayersSelectedActualResources()
    {
        var initial = Pool(AddLegend(RestrictedManaLifecycleTests.Position(), "VEN·141"),
            new RunePool(0, 1, new Dictionary<string, int> { ["green"] = 1, ["red"] = 1 }));
        var game = new Driver(initial);
        await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], ["POWER_SPEND:A:1,red:1"]));
        var pool = game.State.RunePools["P1"];
        Assert.Equal(0, pool.Power); Assert.Equal(0, pool.PowerByTrait.GetValueOrDefault("red"));
        Assert.Equal(1, pool.PowerByTrait["green"]); Assert.Equal(2, pool.UnitPlayOrUnitAbilityOnlyMana);
        await game.Replay();
    }

    [Fact]
    public async Task RestrictedPlayOnlyPowerCannotPayTheLegendAbilityCost()
    {
        var initial = Pool(AddLegend(RestrictedManaLifecycleTests.Position(), "VEN·141"),
            new RunePool(0, 0, unitOrEquipmentPlayOnlyPower: 2));
        var command = new LegendActCommand("LEGEND", Ability, []);
        var before = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("renekton-ineligible-power", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ReactionResourceCanBeAcquiredDuringAnInstructionWithoutPayingThatInstruction()
    {
        var initial = Pool(AddLegend(RestrictedManaLifecycleTests.Position(), "VEN·141"), new(1, 2));
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"]));
        await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        var resource = Assert.Single(request.Options, option => option.Id == $"LEGEND_RESOURCE:LEGEND:{Ability}");
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [resource.Id]));
        request = game.State.PendingRuleChoice!.Request;
        Assert.Equal(2, request.Preview!.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.Equal(0, request.Preview.RunePools["P1"].Power);
        Assert.DoesNotContain(request.Options, option => option.Id.StartsWith("SPEND_MANA:") || option.Id == "SPEND_POWER:1");
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:0"]));
        Assert.Equal(2, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Single(game.Events(), e => e.Event.Kind == "MANA_GAINED");
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Fact]
    public async Task PaymentWindowOffersDistinctLegalLegendPowerAllocations()
    {
        var initial = Pool(AddLegend(RestrictedManaLifecycleTests.Position(), "VEN·141"),
            new RunePool(1, 1, new Dictionary<string, int> { ["green"] = 1, ["red"] = 1 }));
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"]));
        await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        var prefix = $"LEGEND_RESOURCE:LEGEND:{Ability}:POWER_SPEND:";
        var choices = request.Options.Where(option => option.Id.StartsWith(prefix, StringComparison.Ordinal)).Select(option => option.Id).ToArray();
        Assert.Equal(3, choices.Length);
        var selected = Assert.Single(choices, choice => choice == prefix + "A:1,red:1");
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected]));
        var pool = game.State.PendingRuleChoice!.Request.Preview!.RunePools["P1"];
        Assert.Equal(0, pool.Power); Assert.Equal(0, pool.PowerByTrait.GetValueOrDefault("red"));
        Assert.Equal(1, pool.PowerByTrait["green"]); Assert.Equal(2, pool.UnitPlayOrUnitAbilityOnlyMana);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    private static MatchState AddLegend(MatchState state, string cardNo)
    {
        state = OfficialHoldSequenceTests.AddLegend(state, cardNo);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", cardNo, "P1") } };
    }

    private static MatchState Pool(MatchState state, RunePool pool)
        => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
}
