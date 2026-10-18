using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AttachedPredictGrantTests
{
    [Theory]
    [InlineData("SFD·065/221", 0)]
    [InlineData("SFD·065/221", 1)]
    [InlineData("SFD·065/221", 2)]
    [InlineData("OGN·100/298", 0)]
    [InlineData("OGN·100/298", 1)]
    [InlineData("OGN·100/298", 2)]
    public async Task EachAttachedPrintedAuraCreatesItsOwnPredictInstance(string host, int songs)
    {
        // CN719.1 adds each attached effect text to the host; CN817.2 triggers
        // each Predict separately. These are controlled inputs, not legal openings.
        var initial = Position(host);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        for (var i = 0; i < songs; i++)
        {
            var id = i == 0 ? "GEAR" : "SONG2";
            await driver.Act("P1", new AssembleEquipmentCommand(id, "HOST", ["ASSEMBLE_GREEN"]));
            await driver.Top();
            Assert.Equal("HOST", driver.State.CardObjects[id].AttachedToObjectId);
            Assert.NotNull(driver.State.CardObjects[id].AttachedRuleText);
        }
        await driver.Act("P1", new PlayCardCommand("MECH", "SFD·075/221", []));
        var instances = driver.State.TriggerQueue.Select(t => t.InsightContext)
            .Concat(driver.State.StackItems.Select(t => t.InsightContext)).ToArray();
        Assert.Equal(1 + songs, instances.Length);
        Assert.All(instances, context => Assert.Equal("GRANTED_PREDICT", context!.Kind));
        Assert.Null(driver.State.PendingCardChoice);
        if (driver.State.TriggerQueue.Count > 0)
            await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; i <= songs; i++)
        {
            await driver.Top();
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal([initial.PlayerZones["P1"].MainDeck[0]], choice.LegalObjectIds);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        }
        Assert.Empty(driver.State.StackItems);
        Assert.Empty(driver.State.TriggerQueue);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, driver.State.PlayerZones["P1"].MainDeck);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("CARD")] [InlineData("SOURCE")] [InlineData("SOURCE_GENERATION")]
    [InlineData("EQUIPMENT")] [InlineData("EQUIPMENT_GENERATION")] [InlineData("REVISION")]
    [InlineData("ORIGIN_TIME")] [InlineData("FACE_REVISION")] [InlineData("SOURCE_FACE_REVISION")]
    [InlineData("CAPTURE_ORDER")] [InlineData("EQUIPMENT_SELF")] [InlineData("TOKEN")]
    public async Task RecoveryRejectsForgedCapturedCopiedAuraBindings(string mutation)
    {
        var (_, driver) = await EnterWithCopy("SFD·065/221");
        var trigger = Assert.Single(driver.State.TriggerQueue, t => t.InsightContext?.PredictGrant?.Origin is not null);
        var context = trigger.InsightContext!; var grant = context.PredictGrant!; var origin = grant.Origin!;
        var forged = mutation switch {
            "CARD" => grant with { CardNo = "SFD·068/221" },
            "SOURCE" => grant with { Origin = origin with { Unit = origin.Unit with { ObjectId = "MECH" } } },
            "SOURCE_GENERATION" => grant with { Source = grant.Source with { Generation = 99 } },
            "EQUIPMENT" => grant with { Origin = origin with { Equipment = origin.Equipment with { ObjectId = "UNKNOWN" } } },
            "EQUIPMENT_GENERATION" => grant with { Origin = origin with { Equipment = origin.Equipment with { Generation = 99 } } },
            "REVISION" => grant with { Origin = origin with { Revision = 99 } },
            "ORIGIN_TIME" => grant with { Origin = origin with { CapturedTick = -1 } },
            "FACE_REVISION" => grant with { Origin = origin with { FaceRevision = 99 } },
            "SOURCE_FACE_REVISION" => grant with { SourceFaceRevision = 99 },
            "CAPTURE_ORDER" => grant with { Origin = origin with { CapturedTick = grant.CapturedTick + 1 } },
            "EQUIPMENT_SELF" => grant with { Origin = origin with { Equipment = grant.Source } },
            _ => grant with { SourceTokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo } };
        var state = driver.State with { TriggerQueue = driver.State.TriggerQueue.Select(t => t.TriggerId == trigger.TriggerId
            ? t with { InsightContext = context with { PredictGrant = forged } } : t).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), error => error.Contains("Insight", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SFD·065/221")]
    [InlineData("OGN·100/298")]
    public async Task ActualDetachAfterCaptureKeepsBothIndependentPredicts(string host)
    {
        var (initial, driver) = await EnterWithCopy(host, detachSpell: true);
        await Order(driver);
        await driver.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
        await driver.Top();
        Assert.Null(driver.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Null(driver.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal(2, driver.State.StackItems.Count);
        Assert.Single(driver.State.StackItems, item => item.InsightContext?.PredictGrant?.Origin is not null);
        await Resolve(driver, 2);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·065/221")]
    [InlineData("OGN·100/298")]
    public async Task ActualDetachBeforeEntryStopsTheCopiedGrant(string host)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position(host), "DETACH", "SFD·011/221");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Attach(driver);
        await driver.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
        await driver.Top();
        await driver.Act("P1", new PlayCardCommand("MECH", "SFD·075/221", []));
        var item = Assert.Single(driver.State.StackItems);
        Assert.Null(item.InsightContext!.PredictGrant!.Origin);
        await Resolve(driver, 1);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·065/221")]
    [InlineData("OGN·100/298")]
    public async Task AddingCopiedAuraDoesNotRetroactivelyTriggerExistingUnits(string host)
    {
        var initial = LocalDestructionRecallTests.At(Position(host), "MECH", "P1", "BASE");
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "MECH").ToArray() } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Attach(driver);
        Assert.Empty(driver.State.TriggerQueue);
        Assert.Empty(driver.State.StackItems);
        Assert.Null(driver.State.PendingCardChoice);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·065/221")]
    [InlineData("OGN·100/298")]
    public async Task SongKeepsItsCapturedAuraAfterAnotherEquipmentChangesTheHostsFace(string host)
    {
        var initial = Position(host);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SHADY"] = new("SHADY", cardNo: "VEN·137", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]),
            ["COPY-TARGET"] = new("COPY-TARGET", cardNo: "OGN·096/298", ownerId: "P1", controllerId: "P1",
                power: 1, tags: [CardObjectTags.UnitCard]) } };
        initial = LocalDestructionRecallTests.At(LocalDestructionRecallTests.At(initial, "SHADY", "P1", "BASE"), "COPY-TARGET", "P1", "BASE");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Attach(driver);
        await driver.Act("P1", new AssembleEquipmentCommand("SHADY", "HOST", ["ASSEMBLE_YELLOW"]));
        await driver.Top();
        var copy = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        await driver.Act("P1", new PayCostCommand(copy.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-TARGET"]));
        Assert.Equal("OGN·096/298", driver.State.CardObjects["HOST"].CardNo);
        Assert.Equal(host, driver.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        await driver.Act("P1", new PlayCardCommand("MECH", "SFD·075/221", []));
        var grant = Assert.Single(driver.State.StackItems).InsightContext!.PredictGrant!;
        Assert.Equal(host, grant.CardNo);
        Assert.NotNull(grant.Origin);
        Assert.Equal(grant.Origin.FaceRevision, grant.SourceFaceRevision);
        await Resolve(driver, 1);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·065/221")]
    [InlineData("OGN·100/298")]
    public async Task BorrowedAuraKeepsItsOriginalEquipmentBindingAfterActualDetach(string face)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(ShadySpectaclesTests.Position(face), "MECH", "SFD·075/221");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "DETACH", "SFD·011/221");
        initial = initial with { Tick = 0, CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["MECH"] = initial.CardObjects["MECH"] with { Power = 3, Tags = [CardObjectTags.UnitCard, "机械"] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"]));
        await driver.Top();
        var copy = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        await driver.Act("P1", new PayCostCommand(copy.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal(face, driver.State.CardObjects["HOST"].CardNo);
        await driver.Act("P1", new PlayCardCommand("MECH", "SFD·075/221", []));
        Assert.Equal(2, driver.State.TriggerQueue.Count);
        var grant = Assert.Single(driver.State.TriggerQueue, t => t.InsightContext?.PredictGrant?.Origin is not null).InsightContext!.PredictGrant!;
        Assert.Equal("HOST", grant.Source.ObjectId);
        Assert.Equal("GEAR", grant.Origin!.Equipment.ObjectId);
        await Order(driver);
        await driver.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
        await driver.Top();
        Assert.Equal("OGN·096/298", driver.State.CardObjects["HOST"].CardNo);
        Assert.Equal(2, driver.State.StackItems.Count);
        await Resolve(driver, 2);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·065/221")]
    [InlineData("OGN·100/298")]
    public async Task OriginalJournalRejectsCoordinatedRemovalOfTheCopiedOrigin(string host)
    {
        var run = await AttachedPredictOpeningTests.CaptureAsync(host, OfficialDeckFormat.CoreRules);
        var original = run.Frame();
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(original, new CoreRuleEngine(), default));
        var state = run.State with { TriggerQueue = run.State.TriggerQueue.Select(t => t.InsightContext?.PredictGrant?.Origin is not null
            ? t with { InsightContext = t.InsightContext with { PredictGrant = t.InsightContext.PredictGrant with { Origin = null } } } : t).ToArray() };
        // This shape also describes an ordinary printed aura. The immutable
        // accepted journal must still reject the coordinated forged views/state.
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(run.Frame(state), new CoreRuleEngine(), default));
    }

    internal static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Driver)> EnterWithCopy(string host, bool detachSpell = false)
    {
        var initial = Position(host);
        if (detachSpell) initial = OfficialCopyIdentityTests.AddSpell(initial, "DETACH", "SFD·011/221");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Attach(driver);
        await driver.Act("P1", new PlayCardCommand("MECH", "SFD·075/221", []));
        return (initial, driver);
    }

    private static async Task Attach(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        await driver.Top();
        Assert.NotNull(driver.State.CardObjects["GEAR"].AttachedRuleText);
    }

    private static async Task Order(StandbyDefendObserverTests.Driver driver)
    {
        if (driver.State.TriggerQueue.Count > 0)
            await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    private static async Task Resolve(StandbyDefendObserverTests.Driver driver, int count)
    {
        await Order(driver);
        for (var i = 0; i < count; i++)
        {
            var top = driver.State.PlayerZones["P1"].MainDeck[0];
            await driver.Top();
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal([top], choice.LegalObjectIds);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        }
        Assert.Empty(driver.State.StackItems);
        Assert.Empty(driver.State.TriggerQueue);
    }

    internal static MatchState Position(string host)
    {
        var state = AttachedRuleTextTests.AddSong(AttachedRuleTextTests.Position(host), "SONG2");
        state = OfficialCopyIdentityTests.AddSpell(state, "MECH", "SFD·075/221");
        return state with { Tick = 0, CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["MECH"] = state.CardObjects["MECH"] with { Power = 3, Tags = [CardObjectTags.UnitCard, "机械"] } } };
    }
}
