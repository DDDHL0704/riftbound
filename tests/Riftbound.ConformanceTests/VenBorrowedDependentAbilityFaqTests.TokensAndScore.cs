using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("P1", false)]
    [InlineData("P2", false)]
    [InlineData("P1", true)]
    [InlineData("P2", true)]
    public async Task BorrowedGoldIsCreatedDormantForTheActivatorAfterResponses(string owner, bool returns)
    {
        var initial = BorrowedConditionPosition("RAD·022/167");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = owner } } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "金币");
        await RejectBorrowedResource(initial, "HOST", id, ["ALLY"]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        await RejectBorrowedResource(game.State, "HOST", id, []);
        Assert.DoesNotContain(game.State.CardObjects.Values, IsBorrowedGold);
        if (returns)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        await game.Top();
        var gold = Assert.Single(game.State.CardObjects.Values, IsBorrowedGold);
        Assert.Equal("P1", gold.OwnerId);
        Assert.Equal("P1", gold.ControllerId);
        Assert.True(gold.IsExhausted);
        Assert.Contains(gold.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Equal(initial.CardObjects["DONOR"], game.State.CardObjects["DONOR"]);
        var resource = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), a => P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(a.AbilityId)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(a, gold.CardNo));
        await RejectBorrowedResource(game.State, gold.ObjectId, resource.AbilityId, []);
        await game.Replay();
    }

    private static bool IsBorrowedGold(CardObjectState card)
        => card.TokenFactoryCardNo is { } no && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory)
            && factory.TokenFamilyName == "金币";

    [Theory]
    [InlineData("SFD·088/221", 0, false)]
    [InlineData("SFD·088a/221", 0, false)]
    [InlineData("SFD·088/221", 7, false)]
    [InlineData("SFD·088a/221", 7, false)]
    [InlineData("SFD·088/221", 0, true)]
    [InlineData("SFD·088a/221", 0, true)]
    [InlineData("SFD·088/221", 7, true)]
    [InlineData("SFD·088a/221", 7, true)]
    public async Task BorrowedScoreChecksTheBorrowersBattlefieldAndKeepsDirectVictoryAfterRecall(string donor, int score, bool returns)
    {
        var initial = LocalDestructionRecallTests.At(BorrowedConditionPosition(donor), "HOST", "P1", "BATTLEFIELD", "F");
        initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(12, 0, new Dictionary<string, int> { ["blue"] = 4 }) },
            PlayerScores = new Dictionary<string, int>(initial.PlayerScores) { ["P1"] = score, ["P2"] = 0 } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "获得 1 分");
        await RejectBorrowedResource(initial, "HOST", id, ["ALLY"]);
        await RejectBorrowedResource(LocalDestructionRecallTests.At(initial, "HOST", "P1", "BASE"), "HOST", id, []);
        foreach (var pool in new[] { new RunePool(3, 0, new Dictionary<string, int> { ["blue"] = 4 }),
            new RunePool(12, 0, new Dictionary<string, int> { ["blue"] = 3, ["red"] = 1 }) })
            await RejectBorrowedResource(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = pool } }, "HOST", id, []);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Equal(score, game.State.PlayerScores["P1"]);
        Assert.Equal(8, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.True(Assert.Single(game.State.StackItems).BorrowedExhaust!.BorrowerWasOnBattlefield);
        if (returns)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        await game.Top();
        Assert.Equal(score + 1, game.State.PlayerScores["P1"]);
        Assert.Equal(score == 7 ? "P1" : null, game.State.WinnerPlayerId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_SCORE_REPLACED_BY_DRAW");
        Assert.DoesNotContain(BattlefieldTaskMarkers.ScoreGainedThisTurn("F", "P1"), game.State.UntilEndOfTurnEffects);
        Assert.Single(game.Events(), e => e.Event.Kind == "SCORE_GAINED");
        await game.Replay();
    }
}
