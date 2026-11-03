using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("VEN·151", "战力")]
    [InlineData("VEN·195*", "战力")]
    [InlineData("VEN·153", "单位活跃")]
    [InlineData("VEN·196", "单位活跃")]
    [InlineData("VEN·155", "强攻2")]
    [InlineData("VEN·197", "强攻2")]
    public async Task BorrowedEmpowerRemovalCostsTheBorrowerAndKeepsItsCapturedSkill(string donor, string label)
    {
        var game = await WithTemplate(donor, "LEGEND", empower: true);
        var id = TemplateId(game.State, label);
        var before = game.State;
        var unempowered = before with { CardObjects = new Dictionary<string, CardObjectState>(before.CardObjects) {
            ["HOST"] = before.CardObjects["HOST"] with { IsEmpowered = false } } };
        await RejectBorrowedResource(unempowered, "HOST", id, ["ALLY"]);
        if (label == "战力")
            await RejectBorrowedResource(before, "HOST", id, ["ENEMY"]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(before.CardObjects["DONOR"].IsEmpowered, game.State.CardObjects["DONOR"].IsEmpowered);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        var captured = Assert.Single(game.State.StackItems);
        Assert.True(captured.BorrowedExhaust!.BorrowerWasEmpowered);
        var forged = captured with { BorrowedExhaust = captured.BorrowedExhaust with { BorrowerWasEmpowered = false } };
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        if (label == "单位活跃") Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        if (label == "战力") Assert.Equal(before.CardObjects["ALLY"].Power, game.State.CardObjects["ALLY"].Power);
        await game.Top();
        if (label == "单位活跃") Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        if (label == "战力") Assert.Equal(before.CardObjects["ALLY"].Power - 2, game.State.CardObjects["ALLY"].Power);
        if (label == "强攻2") Assert.Contains("COMBAT_KEYWORD:强攻2:1", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();
    }
}
