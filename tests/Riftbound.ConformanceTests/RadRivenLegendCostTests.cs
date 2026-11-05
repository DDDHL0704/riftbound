using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// RAD 114 / 151, CN203: paying the units' exhaustion costs earns Riven's
// triggers independently of the discounted spell's later resolution.
public sealed class RadRivenLegendCostTests
{
    private const string Riven = "RAD·114/167", Singer = "RAD·151/167", Draw = "VEN·049";

    private static MatchState Position(string spell = Draw)
    {
        var state = OfficialHoldSequenceTests.State("OGN·294/298") with {
            Phase = MatchPhases.Main, TimingState = TimingStates.NeutralOpen, TurnPlayerId = "P1",
            PriorityPlayerId = null, CardPlays = new(5, [], []) };
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SINGER"] = new("SINGER", cardNo: Singer, tags: ["CARD_TYPE:LEGEND"], ownerId: "P1", controllerId: "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SINGER"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["SINGER"] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(30, 30), ["P2"] = new(30, 30) } };
        state = HandDiscardObserverTests.Put(state, "RIVEN", Riven, "P1", "BASE");
        for (var i = 0; i < 4; i++)
            state = HandDiscardObserverTests.Put(state, "U" + i, "OGN·001/298", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "U0", "P1", "BATTLEFIELD", "F");
        state = HandDiscardObserverTests.Put(state, "SPELL", spell, "P1", "HAND");
        state = HandDiscardObserverTests.Put(state, "COUNTER", "OGN·064/298", "P2", "HAND");
        return HandDiscardObserverTests.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
    }

    private static PlayCardCommand DiscountedPlay(MatchState state, string spell = Draw, bool includeRiven = false)
    {
        var prompt = JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(state)["P1"],
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var requirement = prompt.GetProperty("candidates").EnumerateArray()
            .Where(candidate => candidate.GetProperty("action").GetString() == "PLAY_CARD")
            .SelectMany(candidate => candidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray())
            .Single(requirement => requirement.GetProperty("sourceObjectId").GetString() == "SPELL");
        var group = Assert.Single(requirement.GetProperty("optionalPlayCostReductionGroups").EnumerateArray(),
            group => group.GetProperty("sourceObjectId").GetString() == "SINGER");
        Assert.Equal(3, group.GetProperty("requiredUnitCount").GetInt32());
        var legal = group.GetProperty("unitChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray();
        var supporters = includeRiven ? new[] { "U0", "U1", "RIVEN" } : new[] { "U0", "U1", "U2" };
        var costs = new[] { group.GetProperty("choiceId").GetString()! }.Concat(supporters.Select(unit =>
            Assert.Single(legal, choice => choice.EndsWith(":" + Uri.EscapeDataString(unit), StringComparison.Ordinal)))).ToArray();
        if (spell == "OGN·048/298") costs = [..costs, "EXHAUST_FRIENDLY_UNIT:U3"];
        return new("SPELL", spell, [], OptionalCosts: costs);
    }

    private static int Earned(MatchState state)
        => state.TriggerQueue.Count(trigger => trigger.SourceObjectId == "RIVEN" && trigger.UnitEvent?.Timing == "ANOTHER_FRIENDLY_UNIT_EXHAUSTED")
            + state.StackItems.Count(item => item.SourceObjectId == "RIVEN" && item.UnitEvent?.Timing == "ANOTHER_FRIENDLY_UNIT_EXHAUSTED");

    private static async Task OrderIfNeeded(Driver game)
    {
        for (var i = 0; game.State.TriggerQueue.Count > 0; i++)
        {
            Assert.True(i < 4);
            var prompt = ResolutionResult.BuildPrompts(game.State).Single(pair => pair.Value.Actions.Contains("ORDER_TRIGGERS"));
            var candidate = Assert.Single(prompt.Value.Candidates!, candidate => candidate.Action == "ORDER_TRIGGERS");
            var ids = JsonSerializer.SerializeToElement(candidate.Metadata).GetProperty("orderedTriggerIds")
                .EnumerateArray().Select(id => id.GetString()!).ToArray();
            await game.Act(prompt.Key, new OrderTriggersCommand(OrderedTriggerIds: ids));
        }
    }

    private static async Task Drain(Driver game)
    {
        for (var i = 0; game.State.TriggerQueue.Count > 0 || game.State.StackItems.Count > 0; i++)
        {
            Assert.True(i < 10);
            await OrderIfNeeded(game);
            if (game.State.StackItems.Count > 0) await game.Top();
        }
        Assert.Null(game.State.PendingPayment);
        Assert.Null(game.State.PendingCardChoice);
        await game.Replay();
    }

    private static void AssertPaid(Driver game, int expectedTriggers, bool includesRiven = false)
    {
        Assert.Equal(expectedTriggers, Earned(game.State));
        Assert.Equal(5, game.State.CardObjects["RIVEN"].Power);
        Assert.Equal(includesRiven, game.State.CardObjects["RIVEN"].IsExhausted);
        Assert.True(game.State.CardObjects["SINGER"].IsExhausted);
        Assert.All(includesRiven ? new[] { "U0", "U1" } : new[] { "U0", "U1", "U2" },
            id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.Single(game.Events(), entry => entry.Event.Kind == "LEGEND_EXHAUSTED");
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_EXHAUSTED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "SINGER"));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        Assert.Equal(30, game.State.RunePools["P1"].Mana);
        Assert.Equal(30, game.State.RunePools["P1"].TotalPower);
    }

    [Fact]
    public async Task ThreeUnitCostsEarnThreeBonusesWhileTheLegendItselfDoesNotCountAsAUnit()
    {
        var game = new Driver(Position());
        await game.Act("P1", DiscountedPlay(game.State));
        AssertPaid(game, 3);
        await Drain(game);
        Assert.Equal(8, game.State.CardObjects["RIVEN"].Power);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task RivenMayPayOneOfTheThreeCostsButOnlyTheOtherTwoUnitsEarnBonuses()
    {
        var game = new Driver(Position());
        await game.Act("P1", DiscountedPlay(game.State, includeRiven: true));
        AssertPaid(game, 2, includesRiven: true);
        Assert.False(game.State.CardObjects["U2"].IsExhausted);
        await Drain(game);
        Assert.Equal(7, game.State.CardObjects["RIVEN"].Power);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
    }

    [Fact]
    public async Task MeditationPaysItsSeparateUnitCostAndEarnsAFourthIndependentBonus()
    {
        const string meditation = "OGN·048/298";
        var game = new Driver(Position(meditation));
        await game.Act("P1", DiscountedPlay(game.State, meditation));
        AssertPaid(game, 4);
        Assert.True(game.State.CardObjects["U3"].IsExhausted);
        await Drain(game);
        Assert.Equal(9, game.State.CardObjects["RIVEN"].Power);
        Assert.Equal(2, game.Events().Where(entry => entry.Event.Kind == "CARD_DRAWN")
            .Sum(entry => Convert.ToInt32(entry.Event.Payload["count"])));
    }

    [Fact]
    public async Task CounteringTheDiscountedSpellDoesNotRefundItsUnitCostsOrRivensEarnedBonuses()
    {
        var game = new Driver(Position());
        await game.Act("P1", DiscountedPlay(game.State));
        AssertPaid(game, 3);
        await OrderIfNeeded(game);
        if (game.State.PriorityPlayerId == "P1") await game.Act("P1", new PassPriorityCommand());
        var spell = Assert.Single(game.State.StackItems, item => item.SourceObjectId == "SPELL");
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell.StackItemId]));
        await Drain(game);
        Assert.Equal(8, game.State.CardObjects["RIVEN"].Power);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        Assert.All(new[] { "SINGER", "U0", "U1", "U2" }, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task ARejectedRepeatedUnitCostCannotPayAnythingOrEarnAnyRivenTriggers()
    {
        var state = Position();
        var command = DiscountedPlay(state);
        var supporters = command.OptionalCosts!.Where(cost => cost.StartsWith("PLAY_COST_REDUCTION_UNIT:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, supporters.Length);
        var invalid = command with { OptionalCosts = [..command.OptionalCosts!, supporters[0]] };
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("duplicate-singer-supporter", "P1", invalid.CmdType), invalid, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(result.Snapshots));
        Assert.Equal(prompts, JsonSerializer.Serialize(result.Prompts));
        Assert.Equal(0, Earned(result.State));
        var game = new Driver(result.State);
        await game.Act("P1", DiscountedPlay(game.State));
        AssertPaid(game, 3);
        await Drain(game);
        Assert.Equal(8, game.State.CardObjects["RIVEN"].Power);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
    }
}
