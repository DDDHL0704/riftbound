using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedBombPosition(string donor)
    {
        var state = RadUnitEffectTestFixture.Place(VenUnitEntryBasicTests.Position(donor), "UNIT", "P1", "BASE");
        state = HandDiscardObserverTests.Put(state, "HOST", "OGN·111/298", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = true } } };
        if (state.CardObjects["UNIT"].Tags.Contains("CARD_TYPE:LEGEND"))
            state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Where(id => id != "UNIT").ToArray(), LegendZone = ["UNIT"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "LEGEND") } };
        return AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
    }

    [Theory]
    [InlineData("RAD·112/167", "BF", 0)]
    [InlineData("RAD·112a/167", "OTHER", 0)]
    [InlineData("RAD·141/167", "BF", 1)]
    [InlineData("RAD·141/167", "OTHER", 1)]
    [InlineData("RAD·169*/167", "BF", 1)]
    [InlineData("RAD·169*/167", "OTHER", 1)]
    [InlineData("RAD·169/167", "BF", 1)]
    [InlineData("RAD·169/167", "OTHER", 1)]
    public async Task BorrowedBombSkillsPayThePrintedCostAndCreateARealDeployedEquipment(string donor, string battlefield, int power)
    {
        var initial = BorrowedBombPosition(donor);
        var id = TemplateId(initial, "炸弹装备指示物");
        foreach (var targets in new[] { Array.Empty<string>(), ["ALLY"], ["UNIT"], ["MISSING"], ["BF", "OTHER"] })
            await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", id, targets));
        await RadGearTestPosition.Reject(initial, "P2", new ActivateAbilityCommand("HOST", id, [battlefield]));
        await RadGearTestPosition.Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, 20) } },
            "P1", new ActivateAbilityCommand("HOST", id, [battlefield]));
        if (power > 0) await RadGearTestPosition.Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 0) } },
            "P1", new ActivateAbilityCommand("HOST", id, [battlefield]));
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [battlefield]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(initial.RunePools["P1"].Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(initial.RunePools["P1"].TotalPower - power, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        await RejectBorrowedResource(game.State, "HOST", id, [battlefield]);
        await game.Top();
        var bomb = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", battlefield), game.State.ObjectLocations[bomb.ObjectId]);
        Assert.Equal("P1", bomb.OwnerId);
        Assert.Contains(CardObjectTags.EquipmentCard, bomb.Tags);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, bomb.Tags);
        Assert.False(bomb.IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·112/167")]
    [InlineData("RAD·141/167")]
    public async Task BorrowedBombCreationSurvivesTheBorrowersReturnToAHiddenZone(string donor)
    {
        var game = new VenBasicFlowSpellTests.Driver(BorrowedBombPosition(donor));
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "炸弹装备指示物"), ["OTHER"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedBombUsesTheSharedDestructionDamageAndCeasedSourceRecovery()
    {
        var initial = BorrowedBombPosition("RAD·169*/167");
        initial = HandDiscardObserverTests.Put(initial, "DESTROYER", "RAD·142/167", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "炸弹装备指示物"), ["OTHER"]));
        await game.Top();
        var bomb = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        await game.Act("P1", new PlayCardCommand("DESTROYER", "RAD·142/167", [bomb.ObjectId]));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(bomb.ObjectId));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }
}
