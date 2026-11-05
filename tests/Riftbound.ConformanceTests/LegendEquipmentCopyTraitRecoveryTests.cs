using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class LegendEquipmentCopyTraitRecoveryTests
{
    [Theory]
    [InlineData("RAD·145/167")]
    [InlineData("RAD·171*/167")]
    [InlineData("RAD·171/167")]
    public async Task ARealGyroGoldAnimatedByOriannaCanCopyOgn096AndRecoverItsFullHistory(string legend)
    {
        var (game, goldId) = await CopyGold(legend);
        var copied = game.State.CardObjects[goldId];
        Assert.Equal("OGN·096/298", copied.CardNo);
        Assert.NotNull(copied.LegendEquipmentUnit);
        Assert.Contains(CardObjectTags.EquipmentCard, copied.Tags);
        Assert.Contains(CardObjectTags.UnitCard, copied.Tags);
        Assert.NotNull(copied.AttachmentUnitCopies);
        AssertMight(game, goldId, copied.ManaCost);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        await game.ColdCheckpoint();
        await game.Replay();
    }

    [Fact]
    public async Task DetachingTheCopyRestoresTheGoldFaceAndKeepsTheLegendTransformation()
    {
        var (game, goldId) = await CopyGold(followup: "SFD·011/221");
        var copied = game.State.CardObjects[goldId];
        var transformation = copied.LegendEquipmentUnit;
        var goldFace = copied.AttachmentUnitCopies!.Basis.CardNo;

        await game.Act("P1", new PlayCardCommand("FOLLOWUP", "SFD·011/221", [goldId, "GLASSES"]));
        await game.Top();

        var restored = game.State.CardObjects[goldId];
        Assert.Equal(goldFace, restored.CardNo);
        Assert.Equal(transformation, restored.LegendEquipmentUnit);
        Assert.Equal(copied.ObjectGeneration, restored.ObjectGeneration);
        Assert.Null(restored.AttachmentUnitCopies);
        Assert.Null(game.State.CardObjects["GLASSES"].AttachedToObjectId);
        Assert.Contains(CardObjectTags.EquipmentCard, restored.Tags);
        Assert.Contains(CardObjectTags.UnitCard, restored.Tags);
        AssertMight(game, goldId, restored.ManaCost);
        await game.ColdCheckpoint();
        await game.Replay();
    }

    [Fact]
    public async Task SelectingAnotherEquipmentEndsOnlyTheTransformationWhileTheCopiedUnitRemains()
    {
        var (game, goldId) = await CopyGold();
        var source = game.State.CardObjects["COPY-SOURCE"];
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GYRO"]));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.TapRunes("P1", ["CONVERT-MANA-0"]);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await game.Top();

        var copied = game.State.CardObjects[goldId];
        Assert.Equal(source.CardNo, copied.CardNo);
        Assert.Null(copied.LegendEquipmentUnit);
        Assert.NotNull(copied.AttachmentUnitCopies);
        Assert.Equal(goldId, game.State.CardObjects["GLASSES"].AttachedToObjectId);
        Assert.Contains(CardObjectTags.UnitCard, copied.Tags);
        Assert.DoesNotContain(CardObjectTags.EquipmentCard, copied.Tags);
        AssertMight(game, goldId, source.Power);
        await game.ColdCheckpoint();
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheCopiedGoldRemovesTheTokenAndBothTemporaryLedgers()
    {
        var (game, goldId) = await CopyGold(followup: "OGN·104/298");
        await game.Act("P1", new PlayCardCommand("FOLLOWUP", "OGN·104/298", [goldId]));
        await game.Top();

        Assert.DoesNotContain(goldId, game.State.CardObjects.Keys);
        Assert.Null(game.State.CardObjects["GLASSES"].AttachedToObjectId);
        Assert.Contains("GLASSES", game.State.PlayerZones["P1"].Base);
        await game.ColdCheckpoint();
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopiedGoldStillRejectsForgedTransformationBindings(bool targetGeneration)
    {
        var (game, goldId) = await CopyGold();
        var copied = game.State.CardObjects[goldId];
        var transformation = copied.LegendEquipmentUnit!;
        var forged = transformation with {
            TargetGeneration = targetGeneration ? copied.ObjectGeneration + 1 : transformation.TargetGeneration,
            SourceLegend = targetGeneration ? transformation.SourceLegend : new("MISSING", 0) };
        var bad = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            [goldId] = copied with { LegendEquipmentUnit = forged } } };

        Assert.Contains("invalid legend equipment unit state", OfficialInsightAndSpellLockTests.Errors(bad));
        await game.ColdCheckpoint();
        await game.Replay();
    }

    private static async Task<(Driver Game, string GoldId)> CopyGold(string legend = "RAD·145/167", string? followup = null)
    {
        var initial = Put(Put(Put(Create(), "GYRO", "RAD·022/167", "P1", "BASE"),
            "COPY-SOURCE", "OGN·096/298", "P1", "HAND"),
            "GLASSES", "VEN·137", "P1", "HAND");
        if (followup is not null) initial = Put(initial, "FOLLOWUP", followup, "P1", "HAND");
        initial = WithOrianna(initial, legend);
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

        Assert.Null(game.State.PendingRuleChoice);
        return (game, gold.ObjectId);
    }

    private static void AssertMight(Driver game, string goldId, int expected)
    {
        var copiedView = CurrentPowerTargetTests.Snapshot(game.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty(goldId);
        Assert.Equal(expected, copiedView.GetProperty("basePower").GetInt32());
        Assert.Equal(expected, copiedView.GetProperty("effectivePower").GetInt32());
    }

    private static MatchState WithOrianna(MatchState state, string legend)
        => OriannaEquipmentConsumerFixture.WithResources(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", legend, "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } });
}
