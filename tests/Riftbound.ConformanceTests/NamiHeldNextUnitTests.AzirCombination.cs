using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    [Fact]
    public async Task AUnitReadiedByAnActualNamiHoldCanAttackAndIndependentlyGatherItsOwnTokens()
    {
        var initial = RadGearTestPosition.Put(Position(), "FIRST", "SFD·177/221", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "MAKE-SAND", "SFD·031/221", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "OTHER", "OGN·296/298", "P2", "BATTLEFIELD", "OTHER");
        initial = RadGearTestPosition.Put(initial, "ENEMY", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await EarnHold(game); await game.Top(); await Fund(game);
        await game.Act("P1", new PlayCardCommand("FIRST", "SFD·177/221", []));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Single(game.State.StackItems, item => item.TurnUnitPlayBoon?.Unit.ObjectId == "FIRST");
        await game.Top();
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["FIRST"]));

        await game.Act("P1", new PlayCardCommand("MAKE-SAND", "SFD·031/221", [])); await game.Top();
        var placement = game.State.PendingRuleChoice!;
        Assert.NotNull(placement);
        Assert.Equal(CoreRuleEngine.UnitTokenDestinationChoiceKind, placement.Request.Kind);
        Assert.Contains(placement.Request.Options, option => option.Id == "BASE");
        await game.Act("P1", new PayCostCommand(placement.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        var token = Assert.Single(game.State.CardObjects.Values, card => card.Tags.Contains(CardObjectTags.SandSoldier));
        Assert.True(token.IsExhausted); Assert.Equal(0, BoonRules.Count(token));
        var use = Assert.Single(CoreRuleEngine.UnitEventTurnPlayGrantUses(game.State));
        Assert.Equal("HOST", use.Grant.Source.ObjectId); Assert.Equal("FIRST", use.Unit.ObjectId);
        await game.Act("P1", new MoveUnitCommand("FIRST", "BASE", "BATTLEFIELD:OTHER", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { token.ObjectId }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [token.ObjectId]));
        Assert.Equal("BASE", game.State.ObjectLocations[token.ObjectId].Zone);
        Assert.Single(game.State.StackItems, item => item.UnitEvent?.CardNo == "SFD·177/221");
        await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations[token.ObjectId].BattlefieldObjectId);
        Assert.True(game.State.CardObjects[token.ObjectId].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects[token.ObjectId]));
        Assert.Single(CoreRuleEngine.UnitEventTurnPlayGrantUses(game.State));
        await game.ColdCheckpoint();
    }
}
