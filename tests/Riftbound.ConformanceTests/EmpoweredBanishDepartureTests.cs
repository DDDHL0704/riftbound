using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class EmpoweredBanishBoundaryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BanishAcceptsDualTypeUnitsAndMakesTargetTokensCeaseWithoutDestruction(bool token)
    {
        var initial = Position(token ? "OGN·175/298" : "VEN·058");
        if (token) {
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.SandSoldierTokenCardNo, out var factory));
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["TARGET"] = factory.CreateObject("TARGET", "P2", "P2") } };
        }
        await Run("target-departure-" + token, initial, async game => {
            Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["TARGET"].Tags);
            if (!token) Assert.Contains(CardObjectTags.EquipmentCard, game.State.CardObjects["TARGET"].Tags);
            Assert.True(Might(game.State, "P2", "TARGET") <= 3);
            var generation = game.State.CardObjects["TARGET"].ObjectGeneration;
            await Empower(game); Assert.Contains("TARGET", game.State.PendingCardChoice!.LegalObjectIds);
            await Choose(game, "TARGET"); await game.Top();
            Assert.DoesNotContain("TARGET", game.State.PlayerZones["P2"].Graveyard);
            var banished = Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_BANISHED");
            Assert.False(banished.Event.Payload.ContainsKey("replacementEffectId"));
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_DESTROYED" or "EQUIPMENT_DESTROYED");
            if (token) {
                Assert.False(game.State.CardObjects.ContainsKey("TARGET")); Assert.False(game.State.ObjectLocations.ContainsKey("TARGET"));
                Assert.DoesNotContain("TARGET", game.State.PlayerZones["P2"].Banished);
                Assert.Single(game.Events(), e => e.Event.Kind == "TOKEN_CEASED_TO_EXIST");
            }
            else {
                Assert.Contains("TARGET", game.State.PlayerZones["P2"].Banished);
                Assert.Equal(generation + 1, game.State.CardObjects["TARGET"].ObjectGeneration);
                Assert.Equal("BANISHED", game.State.ObjectLocations["TARGET"].Zone);
            }
        });
    }

    [Fact]
    public async Task BanishingAHostDetachesItsEquipmentWithoutDestroyingOrBanishingTheEquipment()
    {
        var initial = Put(Position("OGN·096/298"), "TARGET-GEAR", "SFD·022/221", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TARGET-GEAR"] = initial.CardObjects["TARGET-GEAR"] with { AttachedToObjectId = "TARGET" } } };
        await Run("host-equipment-departure", initial, async game => {
            Assert.Equal(3, Might(game.State, "P2", "TARGET")); await Empower(game); await Choose(game, "TARGET"); await game.Top();
            Assert.Contains("TARGET", game.State.PlayerZones["P2"].Banished); Assert.Null(game.State.CardObjects["TARGET-GEAR"].AttachedToObjectId);
            Assert.Equal("P2", game.State.CardObjects["TARGET-GEAR"].OwnerId);
            Assert.DoesNotContain("TARGET-GEAR", game.State.PlayerZones["P2"].Banished);
            Assert.DoesNotContain("TARGET-GEAR", game.State.PlayerZones["P2"].Graveyard);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_DESTROYED" or "EQUIPMENT_DESTROYED");
        });
    }
}
