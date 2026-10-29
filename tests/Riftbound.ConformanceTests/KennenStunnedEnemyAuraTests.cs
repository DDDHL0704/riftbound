using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class KennenStunnedEnemyAuraTests
{
    private static MatchState Position(string cardNo)
    {
        var state = AttackDamageDistributionTests.Position(cardNo);
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Power = 2 } } };
    }

    [Theory]
    [InlineData("VEN·135", 37835)]
    [InlineData("VEN·135a", 37836)]
    public async Task EachPrintingUsesTheSameFixedBonusForAtLeastOneStunnedEnemyHere(string cardNo, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == cardNo);
        Assert.Equal(id, card.Id);
        var aura = Assert.Single(RuleTextParser.Parse(card).StaticAuras,
            a => a.Kind == StaticAuraKinds.SourceObjectPowerWithSameLocationEnemy);
        Assert.Equal(2, aura.PowerDeltaPerParticipant);
        var state = Position(cardNo);
        Assert.Equal(2, ArmamentMightTests.Might(state));
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY1"] = state.CardObjects["ENEMY1"] with { UntilEndOfTurnEffects = ["STUNNED"] } };
        state = state with { CardObjects = cards };
        Assert.Equal(4, ArmamentMightTests.Might(state));
        ArmamentMightTests.PublicRecovery(state);
        cards["ENEMY2"] = cards["ENEMY2"] with { UntilEndOfTurnEffects = ["STUNNED"] };
        state = state with { CardObjects = cards };
        Assert.Equal(4, ArmamentMightTests.Might(state));
        ArmamentMightTests.PublicRecovery(state);
        cards["ENEMY1"] = cards["ENEMY1"] with { UntilEndOfTurnEffects = [] };
        cards["ENEMY2"] = cards["ENEMY2"] with { UntilEndOfTurnEffects = [] };
        state = state with { CardObjects = cards };
        Assert.Equal(2, ArmamentMightTests.Might(state));
    }

    [Fact]
    public void AStunnedEnemyAtAnotherBattlefieldOrAFriendlyUnitHereDoesNotQualify()
    {
        var state = Position("VEN·135");
        state = LocalDestructionRecallTests.At(state, "ENEMY1", "P2", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY1"] = state.CardObjects["ENEMY1"] with { UntilEndOfTurnEffects = ["STUNNED"] },
            ["HOST"] = state.CardObjects["HOST"] with { UntilEndOfTurnEffects = ["STUNNED"] } } };
        Assert.Equal(2, ArmamentMightTests.Might(state));
        ArmamentMightTests.PublicRecovery(state);
    }
    [Fact]
    public void CopiedPrintedTextAddsItsOwnConditionalPowerAndRecomputes()
    {
        var state = AttackDamageDistributionTests.Position("VEN·135", copied: true);
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Power = 2 },
            ["ENEMY1"] = state.CardObjects["ENEMY1"] with { UntilEndOfTurnEffects = ["STUNNED"] } } };
        Assert.Equal(6, ArmamentMightTests.Might(state));
        ArmamentMightTests.PublicRecovery(state);
    }

}
