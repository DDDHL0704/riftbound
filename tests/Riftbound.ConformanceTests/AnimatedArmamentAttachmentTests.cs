using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AnimatedArmamentAttachmentTests
{
    [Theory]
    [InlineData("ASSEMBLE")]
    [InlineData("SPELL")]
    public async Task AnAnimatedArmamentCanActivateEquipButCannotAttachThroughAnySharedRoute(string route)
    {
        const string gearNo = "SFD·022/221";
        var initial = VenUnitEntryBasicTests.Position(gearNo);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        if (route == "SPELL") initial = AttackDamageDistributionTests.Spell(initial, "ATTACH", "SFD·011/221", "P1");
        initial = WithMainPhaseResources(initial);
        var g = new Driver(initial);
        await g.Act("P1", new PlayCardCommand("UNIT", gearNo, ["ALLY"], Destination: "BASE"));
        await g.Top();
        Assert.Equal("ALLY", g.State.CardObjects["UNIT"].AttachedToObjectId);
        await TransformAtNextOwnMain(g, "UNIT");
        Assert.Contains(CardObjectTags.UnitCard, g.State.CardObjects["UNIT"].Tags);
        Assert.Null(g.State.CardObjects["UNIT"].AttachedToObjectId);
        var at = g.State.ObjectLocations["UNIT"];
        var before = g.State.RunePools["P1"];
        var attachments = g.Events().Count(e => e.Event.Kind == "EQUIPMENT_ATTACHED");
        var draws = g.Events().Count(e => e.Event.Kind == "CARD_DRAWN");
        if (route == "ASSEMBLE")
        {
            await g.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY", ["ASSEMBLE_RED"]));
            Assert.Equal(before.TotalPower - 1, g.State.RunePools["P1"].TotalPower);
            Assert.NotNull(Assert.Single(g.State.StackItems).Equip);
        }
        else
        {
            await g.Act("P1", new PlayCardCommand("ATTACH", "SFD·011/221", ["ALLY", "UNIT"]));
            Assert.Equal(before.Mana - 2, g.State.RunePools["P1"].Mana);
        }
        var paid = g.State.RunePools["P1"];
        await g.Top();
        Assert.Null(g.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.Contains(CardObjectTags.UnitCard, g.State.CardObjects["UNIT"].Tags);
        Assert.NotNull(g.State.CardObjects["UNIT"].LegendEquipmentUnit);
        Assert.Equal(at, g.State.ObjectLocations["UNIT"]);
        Assert.Equal(paid, g.State.RunePools["P1"]);
        Assert.Equal(attachments, g.Events().Count(e => e.Event.Kind == "EQUIPMENT_ATTACHED"));
        Assert.Equal(draws + (route == "SPELL" ? 1 : 0), g.Events().Count(e => e.Event.Kind == "CARD_DRAWN"));
        Assert.Empty(g.State.StackItems);
    }

    internal static MatchState WithMainPhaseResources(MatchState initial)
    {
        for (var i = 0; i < 6; i++)
            initial = RadGearTestPosition.Put(initial, "ORI-MANA-" + i, "UNL-R03", "P1", "BASE");
        for (var i = 0; i < 4; i++)
            initial = RadGearTestPosition.Put(initial, "ORI-POWER-" + i, "UNL-R01", "P1", "BASE");
        return initial;
    }

    internal static async Task TransformAtNextOwnMain(Driver game, string equipment)
    {
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        var selection = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("MAIN", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        await game.Act("P1", new ChooseCardsCommand(selection.ChoiceId, selection.ChoiceWindow, [equipment]));
        await game.TapRunes("P1", Enumerable.Range(0, 6).Select(i => "ORI-MANA-" + i));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await game.Top();
        var runes = Enumerable.Range(0, 4).Select(i => "ORI-POWER-" + i).ToArray();
        await game.Act("P1", new RecycleRuneCommand(runes[0], runes));
    }
}
