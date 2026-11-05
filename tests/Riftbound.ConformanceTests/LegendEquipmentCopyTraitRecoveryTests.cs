using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class LegendEquipmentCopyTraitRecoveryTests
{
    [Fact]
    public async Task ARealGyroGoldAnimatedByOriannaCanCopyOgn096AndRecoverItsFullHistory()
    {
        var initial = Put(Put(Put(Create(), "GYRO", "RAD·022/167", "P1", "BASE"),
            "COPY-SOURCE", "OGN·096/298", "P1", "HAND"),
            "GLASSES", "VEN·137", "P1", "HAND");
        initial = WithOrianna(initial);
        var game = new Driver(initial);

        await game.Act("P1", new PlayCardCommand("COPY-SOURCE", "OGN·096/298", []));
        await game.Act("P1", new PlayCardCommand("GLASSES", "VEN·137", []));
        await game.Act("P1", new ActivateAbilityCommand("GYRO", "RAD_022_EXHAUST_CREATE_GOLD", []));
        await game.Top();
        var gold = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币");
        Assert.Contains(gold.ObjectId, game.State.PlayerZones["P1"].Base);

        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, gold.ObjectId);
        Assert.NotNull(game.State.CardObjects[gold.ObjectId].LegendEquipmentUnit);

        await game.Act("P1", new AssembleEquipmentCommand("GLASSES", gold.ObjectId, ["ASSEMBLE_YELLOW"]));
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("ATTACHMENT_COPY_SELECTION", choice.Request.Kind);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));

        var copied = game.State.CardObjects[gold.ObjectId];
        Assert.Equal("OGN·096/298", copied.CardNo);
        Assert.NotNull(copied.LegendEquipmentUnit);
        Assert.Contains(CardObjectTags.EquipmentCard, copied.Tags);
        Assert.Contains(CardObjectTags.UnitCard, copied.Tags);
        Assert.NotNull(copied.AttachmentUnitCopies);
        var copiedView = CurrentPowerTargetTests.Snapshot(game.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty(gold.ObjectId);
        Assert.Equal(copied.ManaCost, copiedView.GetProperty("basePower").GetInt32());
        Assert.Equal(copied.ManaCost, copiedView.GetProperty("effectivePower").GetInt32());
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        await game.ColdCheckpoint();
        await game.Replay();
    }

    private static MatchState WithOrianna(MatchState state)
        => OriannaEquipmentConsumerFixture.WithResources(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } });
}
