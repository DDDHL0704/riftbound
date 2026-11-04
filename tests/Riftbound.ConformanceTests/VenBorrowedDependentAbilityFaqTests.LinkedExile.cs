using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    // 396/398: acquiring only the activated component never links it
    // to the donor's printed play trigger or to arbitrary banished units.
    [Theory]
    [InlineData("empty")]
    [InlineData("other-banish")]
    [InlineData("played")]
    [InlineData("source-return")]
    [InlineData("source-token-return")]
    public async Task BorrowedSarcophagusCannotUseAnotherObjectsLinkedOrUnlinkedExiles(string mode)
    {
        var initial = BorrowedBombPosition("UNL-148/219");
        if (mode.Contains("token", StringComparison.Ordinal))
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = initial.CardObjects["HOST"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                    CopyFaceTransitions = [new(0, initial.CardObjects["HOST"].ObjectGeneration,
                        P6TokenFactoryCatalog.ImageTokenCardNo, initial.CardObjects["HOST"].CardNo!)] } } };
        if (mode == "other-banish") initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BANISHED");
        var actualPlay = mode is "played" or "source-return" or "source-token-return";
        if (actualPlay)
        {
            initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "GRAVEYARD");
            initial = RadUnitEffectTestFixture.Place(initial, "UNIT", "P1", "HAND");
        }
        var game = new Driver(initial);
        if (actualPlay)
        {
            await game.Act("P1", new PlayCardCommand("UNIT", "UNL-148/219", []));
            await game.Top();
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Banished);
            Assert.Contains(game.Events(), e => e.Event.Kind == "CARDS_BANISHED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        }
        var command = new ActivateAbilityCommand("HOST", TemplateId(game.State, "打出以关联技能放逐的单位"), []);
        await RadGearTestPosition.Reject(game.State, "P2", command);
        await RadGearTestPosition.Reject(game.State, "P1", command with { TargetObjectIds = ["ALLY"] });
        var pool = MatchStateHasher.HashValue(game.State.RunePools);
        var banished = game.State.PlayerZones["P1"].Banished.ToArray();
        await game.Act("P1", command);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(pool, MatchStateHasher.HashValue(game.State.RunePools));
        var item = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with {
            BorrowedExhaust = item.BorrowedExhaust! with { AbilityIndex = 999 } }] }));
        if (mode.Contains("return", StringComparison.Ordinal))
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        await game.Top();
        Assert.Equal(banished, game.State.PlayerZones["P1"].Banished);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_PLAYED_TO_BASE");
        if (mode == "source-token-return") Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        await game.Replay();
    }
}
