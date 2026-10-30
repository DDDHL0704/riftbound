using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.VenConditionalPlayCostTests;

namespace Riftbound.ConformanceTests;

public sealed class VenOpponentSpellCostTests
{
    [Fact]
    public async Task SuppressionHelmPaysItsPrintedCostAndItsSeparateGreenEmpowerCost()
    {
        var source = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == "VEN·045");
        Assert.Equal(37735, source.Id); Assert.Equal(4, source.Energy); Assert.Equal(1, source.ReturnEnergy);
        var state = VenUnitEntryBasicTests.Position("VEN·045") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(8, 0, new Dictionary<string, int> { [RuneTrait.Green] = 2 }), ["P2"] = new(20, 20) } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·045", []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(4, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(0, game.State.RunePools["P1"].Mana);
        await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
        await Reject(game.State, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Replay();
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(1, true)] [InlineData(2, true)]
    public async Task EachEnemyHelmIncreasesTheSameQuotePromptAndCommittedSpellCost(int count, bool empowered)
    {
        var state = Position(count, empowered); var mana = 4 + count; var power = empowered ? count : 0;
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(mana, power) } };
        var command = new PlayCardCommand("SPELL", "VEN·081", ["ALLY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(mana, quote.Cost!.Mana); Assert.Equal(power, quote.Cost.GenericPower);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        Assert.Equal(mana, Assert.Single(requirements, r => Equals(r["sourceObjectId"], "SPELL"))["minimumManaCost"]);
        if (count > 0) {
            var adjustment = Assert.Single(quote.Cost.Adjustments, a => a.Label == "对手法术增费");
            Assert.Equal(count, adjustment.Mana); Assert.Equal(power, adjustment.Power);
        }
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(mana - 1, power) } }, command);
        if (power > 0) await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(mana, power - 1) } }, command);
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        if (count > 0) {
            var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event;
            Assert.Equal(count, paid.Payload["opponentSpellCostIncreaseMana"]);
            Assert.Equal(power, paid.Payload["opponentSpellCostIncreasePower"]);
        }
        await game.Top(); Assert.Equal(state.CardObjects["ALLY"].Power + 6, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Theory]
    [InlineData("friendly")] [InlineData("hand")] [InlineData("graveyard")] [InlineData("banished")] [InlineData("hidden")]
    public void OnlyEnemyPublicFieldSourcesIncreaseTheCost(string branch)
    {
        var state = Position(0, false);
        state = Place(state, "HELM", "VEN·045", branch == "friendly" ? "P1" : "P2", branch switch {
            "hand" => "HAND", "graveyard" => "GRAVEYARD", "banished" => "BANISHED", "hidden" => "BATTLEFIELD", _ => "BASE" }, branch == "hidden" ? "OTHER" : null);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HELM"] = state.CardObjects["HELM"] with { IsFaceDown = branch == "hidden", IsEmpowered = branch != "hidden" } } };
        var command = new PlayCardCommand("SPELL", "VEN·081", ["ALLY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(4, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower);
    }

    [Theory]
    [InlineData("VEN·096", 5, 0)] [InlineData("VEN·045", 4, 1)]
    public async Task EnemyHelmsDoNotIncreaseUnitsOrEquipment(string no, int mana, int power)
    {
        var state = Place(Position(2, true), "PLAY", no, "P1", "HAND");
        var command = new PlayCardCommand("PLAY", no, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Contains("PLAY", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task TotalReductionsApplyAfterTheHelmsManaAndGenericPowerIncreases()
    {
        var state = Place(Position(1, true), "RESEARCHER", "VEN·055", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RESEARCHER"] = state.CardObjects["RESEARCHER"] with { IsEmpowered = true } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(4, 0) } };
        var command = new PlayCardCommand("SPELL", "VEN·081", ["ALLY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(4, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower);
        var game = new Driver(state); await game.Act("P1", command); await game.Top(); await game.Replay();
    }

    private static MatchState Position(int count, bool empowered)
    {
        var state = Place(VenUnitEntryBasicTests.Position("VEN·045"), "UNIT", "VEN·045", "P1", "BANISHED");
        state = Place(state, "SPELL", "VEN·081", "P1", "HAND");
        for (var i = 0; i < count; i++) {
            var id = "HELM-" + i; state = Place(state, id, "VEN·045", "P2", "BASE");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = state.CardObjects[id] with { IsEmpowered = empowered } } };
        }
        return state;
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-spell-tax", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
