using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedFaeriePosition(string donor = "UNL-189/219", int supports = 0, string controller = "P1")
    {
        var state = BorrowedUnitTokenPosition(donor);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.FaerieTokenCardNo, out var factory));
        for (var i = 0; i < supports; i++)
        {
            var id = "FAERIE-" + i;
            state = LocalDestructionRecallTests.At(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = factory.CreateObject(id, controller, controller) } }, id, controller, "BASE");
        }
        return state;
    }

    [Theory]
    [InlineData("UNL-189/219", 0)]
    [InlineData("UNL-230/219", 0)]
    [InlineData("UNL-230*/219", 0)]
    [InlineData("UNL-189/219", 1)]
    [InlineData("UNL-230/219", 1)]
    [InlineData("UNL-230*/219", 1)]
    [InlineData("UNL-189/219", 4)]
    [InlineData("UNL-230/219", 4)]
    [InlineData("UNL-230*/219", 4)]
    [InlineData("UNL-189/219", 5)]
    [InlineData("UNL-230/219", 5)]
    [InlineData("UNL-230*/219", 5)]
    public async Task BorrowedFaerieSkillPricesVisibleFriendlyEphemeralUnitsAndCreatesAReadyFaerie(string donor, int supports)
    {
        var initial = BorrowedFaeriePosition(donor, supports);
        var mana = Math.Max(0, 4 - supports);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(mana, 0) } };
        var id = TemplateId(initial, "战力的精灵");
        var row = ResolutionResult.BuildPrompts(initial)["P1"].Candidates!
            .Where(c => c.Action == CommandTypes.ActivateAbility)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Single(r => Equals(r["abilityId"], id));
        Assert.Equal(mana, row["manaCost"]);
        await RejectBorrowedResource(initial, "HOST", id, ["HOST"]);
        if (mana > 0)
            await RejectBorrowedResource(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(mana - 1, 0) } }, "HOST", id, []);
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        Assert.DoesNotContain(game.State.CardObjects.Keys, key => !initial.CardObjects.ContainsKey(key));
        await game.Top();
        await FinishBorrowedTokenLocations(game);
        var token = Assert.Single(BorrowedCreatedUnits(game.State), c => !initial.CardObjects.ContainsKey(c.ObjectId));
        Assert.Equal(3, token.Power);
        Assert.False(token.IsExhausted);
        Assert.Contains(CardObjectTags.Ephemeral, token.Tags);
        Assert.Contains("仙灵", token.Tags);
        Assert.Equal("P1", token.ControllerId);
        Assert.Equal("BASE", game.State.ObjectLocations[token.ObjectId].Zone);
        await game.Replay();
    }

    [Theory]
    [InlineData("HOST")]
    [InlineData("FAERIE-0")]
    public async Task BorrowedFaerieSkillKeepsItsPaidPriceAndInstructionAfterRealRecall(string returned)
    {
        var initial = BorrowedFaeriePosition(supports: 1);
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "战力的精灵"), []));
        Assert.Equal(initial.RunePools["P1"].Mana - 3, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned]));
        await game.Top();
        var paid = game.State.RunePools["P1"].Mana;
        Assert.Equal(returned == "HOST", game.State.CardObjects.ContainsKey("FAERIE-0"));
        await game.Top();
        await FinishBorrowedTokenLocations(game);
        Assert.Equal(paid, game.State.RunePools["P1"].Mana);
        var token = Assert.Single(BorrowedCreatedUnits(game.State), c => !initial.CardObjects.ContainsKey(c.ObjectId));
        Assert.False(token.IsExhausted);
        Assert.Contains(CardObjectTags.Ephemeral, token.Tags);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedFaerieSkillDoesNotDiscountForEnemyEphemeralUnits()
    {
        var initial = BorrowedFaeriePosition(supports: 4, controller: "P2");
        var id = TemplateId(initial, "战力的精灵");
        await RejectBorrowedResource(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(3, 0) } }, "HOST", id, []);
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Equal(initial.RunePools["P1"].Mana - 4, game.State.RunePools["P1"].Mana);
        await game.Top();
        await FinishBorrowedTokenLocations(game);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedFaerieSkillKeepsReadyAndEphemeralAdjustmentsThroughSharedTokenMultiplication(bool accept)
    {
        var initial = OfficialHoldSequenceTests.AddUnit(BorrowedFaeriePosition(), "DOUBLER", "UNL-086/219", "F", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "战力的精灵"), []));
        await game.Top();
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", game.State.PendingCardChoice!.ChoiceWindow);
        Assert.Empty(BorrowedCreatedUnits(game.State));
        var first = true;
        while (game.State.PendingRuleChoice is null
            && game.State.PendingCardChoice is { ChoiceWindow: "TOKEN_ENTRY_REPLACEMENT" } choice)
        {
            await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DOUBLER"]));
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept && first ? ["DOUBLER"] : []));
            first = false;
        }
        await FinishBorrowedTokenLocations(game);
        var tokens = BorrowedCreatedUnits(game.State);
        Assert.Equal(accept ? 2 : 1, tokens.Length);
        Assert.All(tokens, token => {
            Assert.False(token.IsExhausted);
            Assert.Contains(CardObjectTags.Ephemeral, token.Tags);
            Assert.Equal(3, token.Power);
        });
        Assert.Equal(initial.RunePools["P1"].Mana - 4, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }
}
