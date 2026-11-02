using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadLastBreathAmplifierPhysicalTests
{
    private const string Amplifier = "RAD·017/167";

    [Fact]
    public async Task ReviewedAndAdmittedIdentityAndPrintedResourcesMatchTheOfficialPrinting()
    {
        var source = Assert.Single(RadiantCardSources.GetReviewedPhysicalPrints(), c => c.CardNo == Amplifier);
        Assert.Equal(39507, source.Id); Assert.Equal(4, source.Energy); Assert.Equal(1, source.ReturnEnergy);
        Assert.Equal(new[] { "red" }, source.CardColorList);
        Assert.Contains("此处所有友方的", source.CardEffect);
        Assert.Contains("额外触发一次", source.CardEffect);
        var printed = OfficialGarenConquestBattlefieldCountTests.Printed("AMP", Amplifier, "P1");
        Assert.Equal(Amplifier, printed.CardNo); Assert.Equal(4, printed.ManaCost); Assert.Equal(0, printed.Power);
        Assert.Contains(CardObjectTags.EquipmentCard, printed.Tags); Assert.Contains("COLOR:red", printed.Tags);
        var cost = PrintedPowerCostRules.ForCard(Amplifier);
        Assert.Equal(1, cost.Amount); Assert.Equal(new[] { RuneTrait.Red }, cost.Traits);
        Assert.Equal(source.Id, Assert.Single(RadiantCardSources.GetAdmittedCards(), c => c.CardNo == Amplifier).Id);
        var runtime = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.Equal(1437, runtime.Cards.Count);
        Assert.Equal(source.Id, Assert.Single(runtime.Cards, c => c.CardNo == Amplifier).Id);
    }

    [Theory]
    [InlineData("A-AMP")] [InlineData("Z-AMP")]
    public async Task SimultaneouslyDyingAmplifierKeepsItsPhysicalCardAndAddsAnEquipmentDraw(string amplifierId)
    {
        // Explicit public equipment position; the mass destruction and each
        // earned skill use accepted commands, cold recovery and original replay.
        var initial = Put(Put(Put(Create(), "GEAR", "RAD·062/167", "P1", "BATTLEFIELD", "BF"),
            amplifierId, Amplifier, "P1", "BATTLEFIELD", "BF"), "MASS", "OGN·022/298", "P1", "HAND");
        initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "GEAR" || p.Key == amplifierId
                ? p.Value with { Tags = [..p.Value.Tags, CardEquipmentKeywordNames.Deploy] } : p.Value) };
        var removalOrder = amplifierId == "A-AMP" ? new[] { amplifierId, "GEAR" } : new[] { "GEAR", amplifierId };
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Battlefields = initial.PlayerZones["P1"].Battlefields
                .Where(id => id != "GEAR" && id != amplifierId).Concat(removalOrder).ToArray() } } };
        var generation = initial.CardObjects[amplifierId].ObjectGeneration;
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("MASS", "OGN·022/298", [])); await game.Top();
        Assert.Equal(removalOrder, game.Events().Where(e => e.Event.Kind == "EQUIPMENT_DESTROYED")
            .Select(e => (string)e.Event.Payload["targetObjectId"]!).ToArray());
        Assert.Contains(amplifierId, game.State.PlayerZones["P1"].Graveyard);
        var physical = game.State.CardObjects[amplifierId];
        Assert.Equal(Amplifier, physical.CardNo); Assert.Equal(generation + 1, physical.ObjectGeneration);
        Assert.Equal(4, physical.ManaCost); Assert.Null(physical.TokenFactoryCardNo);
        Assert.Contains(CardObjectTags.EquipmentCard, physical.Tags);
        Assert.Empty(game.State.DeathLedger.Counts);
        var draws = game.State.TriggerQueue.Select(t => t.LastBreathDraw!).ToArray();
        Assert.Equal(2, draws.Length); Assert.All(draws, c => Assert.Equal("GEAR", c.Source.ObjectId));
        Assert.Equal(new[] { 0, 1 }, draws.Select(c => c.Repetition!.Occurrence).Order().ToArray());
        Assert.All(draws, c => Assert.Equal(generation, c.Repetition!.Frame.CardObjects[amplifierId].ObjectGeneration));
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await game.Top(); Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
        await game.Top(); Assert.Equal(hand + 2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }
}
