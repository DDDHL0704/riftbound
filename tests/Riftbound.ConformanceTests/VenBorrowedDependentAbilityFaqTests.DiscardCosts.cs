using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    // VEN FAQ p. 15: inheritance includes every printed cost, including
    // discarding equipment from the borrowing controller's hand.
    [Fact]
    public async Task BorrowedDiscardCostUsesAnEquipmentHandCardBeforeItsDamageEntersTheChain()
    {
        var game = await DiscardCostGame();
        var id = TemplateId(game.State, "4 点伤害");
        var row = (ResolutionResult.BuildPrompts(game.State)["P1"].Candidates ?? [])
            .Where(c => c.Enabled && c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Single(r => Equals(r.GetValueOrDefault("sourceObjectId"), "HOST") && Equals(r.GetValueOrDefault("abilityId"), id));
        Assert.Equal(1, row["requiredAdditionalCostChoiceCount"]);
        Assert.Equal("DISCARD_HAND_CARD:COST", Assert.Single((IReadOnlyList<ActionPromptChoiceDto>)row["additionalCostChoices"]!).Id);
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"], ["DISCARD_HAND_CARD:COST"]));
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain("COST", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", game.State.ObjectLocations["COST"].Zone);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        Assert.Equal(new[] { "ALLY" }, Assert.Single(game.State.StackItems).TargetObjectIds);
        Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
        await game.Top();
        Assert.Equal(4, game.State.CardObjects["ALLY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("spell")]
    [InlineData("field")]
    [InlineData("enemy")]
    [InlineData("multiple")]
    [InlineData("mana")]
    [InlineData("base-target")]
    public async Task BorrowedDiscardCostRejectsIllegalPaymentWithoutDiscardingOrExhausting(string boundary)
    {
        var game = await DiscardCostGame();
        var id = TemplateId(game.State, "4 点伤害");
        var state = game.State;
        if (boundary == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        var costs = boundary switch {
            "missing" => Array.Empty<string>(), "spell" => ["DISCARD_HAND_CARD:READY2"],
            "field" => ["DISCARD_HAND_CARD:GEAR"], "enemy" => ["DISCARD_HAND_CARD:OTHER-COST"],
            "multiple" => ["DISCARD_HAND_CARD:COST", "DISCARD_HAND_CARD:OTHER-COST"],
            _ => new[] { "DISCARD_HAND_CARD:COST" } };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("borrowed-discard-invalid", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, [boundary == "base-target" ? "ENEMY" : "ALLY"], costs), default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static Task<Driver> DiscardCostGame()
        => WithTemplate("VEN·060", "UNIT", adjust: initial => {
            initial = AttackDamageDistributionTests.Spell(initial, "COST", "OGN·017/298", "P1");
            initial = AttackDamageDistributionTests.Spell(initial, "OTHER-COST", "OGN·017/298", "P2");
            return initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["COST"] = initial.CardObjects["COST"] with { Tags = [CardObjectTags.EquipmentCard] },
                ["OTHER-COST"] = initial.CardObjects["OTHER-COST"] with { Tags = [CardObjectTags.EquipmentCard] } } };
        });
}
