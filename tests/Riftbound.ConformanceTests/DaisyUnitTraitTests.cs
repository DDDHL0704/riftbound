using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DaisyUnitTraitTests
{
    private const string Daisy = "UNL-196/219";
    private static readonly (string Id, string No, string Tag)[] Animals = [
        ("BIRD", "OGN·216/298", "鸟类"), ("CAT", "OGN·135/298", "猫科"),
        ("DOG", "SFD·159/221", "犬形"), ("PORO", "OGN·052/298", "魄罗")];
    private static MatchState Position(int categories)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "DAISY", Daisy, "P1", "HAND");
        foreach (var animal in Animals.Take(categories))
            state = RadGearTestPosition.Put(state, animal.Id, animal.No, "P1", "BASE");
        return state;
    }
    private static PlayCardCommand Play() => new("DAISY", Daisy, []);
    private static PlayCostQuoteDto Quote(MatchState state)
        => new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, Play()));
    private static MatchState AttackPosition(int missing = -1)
    {
        var state = AttackDamageDistributionTests.Position(Daisy);
        foreach (var (animal, index) in Animals.Select((animal, index) => (animal, index)))
            if (index != missing) state = RadGearTestPosition.Put(state, animal.Id, animal.No, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return RadGearTestPosition.Put(state, "OUTSIDE", "OGN·219/298", "P2", "BASE");
    }
    private static Task Attack(Driver game)
        => game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
    private static Task Choose(Driver game, string id)
        => game.Act("P1", new ChooseCardsCommand(game.State.PendingCardChoice!.ChoiceId,
            game.State.PendingCardChoice.ChoiceWindow, [id]));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task EachDistinctFriendlyCategoryReducesBothTheQuoteAndActualManaOnceAndEntryRemainsReady(int categories)
    {
        var state = Position(categories);
        if (categories > 0)
            foreach (var id in new[] { "ANOTHER-BIRD", "THIRD-BIRD" })
                state = RadGearTestPosition.Put(state, id, Animals[0].No, "P1", "BASE");
        var game = new Driver(state); Assert.Equal(9 - categories, Quote(game.State).Cost!.Mana);
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", Play());
        Assert.Equal(mana - (9 - categories), game.State.RunePools["P1"].Mana);
        Assert.False(game.State.CardObjects["DAISY"].IsExhausted);
        Assert.Equal(8, game.State.CardObjects["DAISY"].Power); await game.Replay();
    }

    [Fact]
    public async Task OneCurrentUnitWithEveryCategoryCountsFourCategoriesRatherThanOneUnit()
    {
        var state = Position(1); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BIRD"] = state.CardObjects["BIRD"] with { Tags = [CardObjectTags.UnitCard, "鸟类", "猫科", "犬形", "魄罗"] } } };
        var game = new Driver(state); Assert.Equal(5, Quote(game.State).Cost!.Mana);
        var mana = game.State.RunePools["P1"].Mana; await game.Act("P1", Play());
        Assert.Equal(mana - 5, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Theory]
    [InlineData("P1", "P2", "BASE", false, true)]
    [InlineData("P2", "P1", "BASE", false, false)]
    [InlineData("P1", "P1", "HAND", false, false)]
    [InlineData("P1", "P1", "GRAVEYARD", false, false)]
    public async Task ACategoryRequiresTheCurrentPublicFieldControllerRatherThanThePhysicalOwner(
        string controller, string owner, string zone, bool hidden, bool counts)
    {
        var state = RadGearTestPosition.Put(Position(0), "BIRD", Animals[0].No, controller, zone);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BIRD"] = state.CardObjects["BIRD"] with { OwnerId = owner, IsFaceDown = hidden } } };
        var game = new Driver(state); Assert.Equal(counts ? 8 : 9, Quote(game.State).Cost!.Mana);
        await game.Act("P1", Play()); await game.Replay();
    }

    [Fact]
    public async Task AnActuallyHiddenStandbyCatDoesNotGrantItsPrintedCategoryDiscount()
    {
        var state = LocalDestructionRecallTests.At(Position(0), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "SECRET", "OGN·135/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new HideCardCommand("SECRET", "OGN·135/298", "BATTLEFIELD:BF", ["STANDBY_A"]));
        Assert.True(game.State.CardObjects["SECRET"].IsFaceDown);
        Assert.Equal(9, Quote(game.State).Cost!.Mana);
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", Play());
        Assert.Equal(mana - 9, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Fact]
    public async Task AnActualAttackWithEveryCategoryConfirmsOnlyAnEnemyAtCurrentHereBeforeResponses()
    {
        var game = new Driver(AttackPosition()); await Attack(game);
        var choice = game.State.PendingCardChoice!;
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, choice.LegalObjectIds);
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OUTSIDE"]));
        await Choose(game, "ENEMY1"); Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        await game.Top(); Assert.Contains("STUNNED", game.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY2"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task EachMissingCategoryPreventsTheAttackTriggerAtActualRoleAcquisition(int missing)
    {
        var game = new Driver(AttackPosition(missing)); await Attack(game);
        Assert.True(game.State.CardObjects["HOST"].IsAttacking);
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.State.StackItems, item => item.UnitEvent?.CardNo == Daisy);
        Assert.DoesNotContain(game.State.TriggerQueue, item => item.UnitEvent?.CardNo == Daisy); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LosingACategoryDoesNotRevokeTheEarnedSkillButReturningItsSourceMakesCurrentHereUnavailable(bool returnSource)
    {
        var game = new Driver(AttackPosition()); await Attack(game); await Choose(game, "ENEMY1");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returnSource ? "HOST" : "CAT"])); await game.Top();
        Assert.Single(game.State.StackItems); await game.Top();
        Assert.Equal(!returnSource, game.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects.Contains("STUNNED"));
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryCannotEraseTheOriginallyEarnedConditionalAttackSkill()
    {
        var game = new Driver(AttackPosition()); await Attack(game); await Choose(game, "ENEMY1");
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [] }));
        await game.Top(); await game.Replay();
    }
}
