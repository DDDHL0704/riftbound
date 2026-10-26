using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PrintedPredictEntryTests
{
    [Theory]
    [InlineData("UNL-089/219")]
    [InlineData("UNL-089a/219")]
    [InlineData("UNL-161/219")]
    [InlineData("UNL-224/219")]
    [InlineData("SFD·237/221")]
    [InlineData("SFD·237*/221")]
    [InlineData("OGN·086/298")]
    [InlineData("OGN·100/298")]
    [InlineData("OGN·171/298")]
    [InlineData("OGN·174/298")]
    [InlineData("OGN·235/298")]
    public async Task EveryRuntimePrintedPredictFaceUsesAnIndependentOptionalEntryTrigger(string card)
    {
        var initial = GrantedPredictTests.Hand(card);
        if (card == "UNL-161/219") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["MECH"] = initial.CardObjects["MECH"] with { Tags = [CardObjectTags.EquipmentCard] } } };
        var played = await Act(initial, "P1", new PlayCardCommand("MECH", card, []));
        Assert.Contains("MECH", played.State.PlayerZones["P1"].Base); Assert.Null(played.State.PendingCardChoice);
        var trigger = Assert.Single(played.State.StackItems);
        Assert.Equal("PRINTED_PREDICT", trigger.InsightContext!.Kind);
        Assert.Equal(1, trigger.InsightContext.SourceGeneration); Restore(played.State);
        var opened = await Top(played.State); var choice = opened.State.PendingCardChoice!;
        Assert.Equal("INSIGHT", choice.ChoiceWindow); Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal([initial.PlayerZones["P1"].MainDeck[0]], choice.LegalObjectIds); Restore(opened.State);
        var done = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, done.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Fact]
    public async Task BanishThenPlayDaseScoutCapturesItsPrintedPredictAtNewEntry()
    {
        var initial = TurnUnitPlayGrantsTests.Position("OGN·223/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { CardNo = "OGN·174/298", Power = 5, Tags = [CardObjectTags.UnitCard, "预知"] } } };
        initial = AttackDamageDistributionTests.Spell(initial, "HUNT", "UNL-184/219", "P1");
        var cast = await Act(initial, "P1", new PlayCardCommand("HUNT", "UNL-184/219", ["HOST"]));
        var entered = await Top(cast.State);
        Assert.NotNull(entered.State.PendingEffectPlay);
        entered = await Act(entered.State, "P1", new PlayCardCommand("HOST", "OGN·174/298", [], Destination: "BATTLEFIELD:F"));
        Assert.Equal("BATTLEFIELD", entered.State.ObjectLocations["HOST"].Zone);
        Assert.True(entered.State.CardObjects["HOST"].ObjectGeneration > initial.CardObjects["HOST"].ObjectGeneration);
        var trigger = Assert.Single(entered.State.StackItems, i => i.InsightContext?.Kind == "PRINTED_PREDICT");
        Assert.Equal("HOST", trigger.SourceObjectId); Restore(entered.State);
        var opened = await Top(entered.State);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal([initial.PlayerZones["P1"].MainDeck[0]], choice.LegalObjectIds);
        var done = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Theory]
    [InlineData("UNL-089/219")]
    [InlineData("UNL-089a/219")]
    [InlineData("UNL-224/219")]
    [InlineData("SFD·237/221")]
    [InlineData("SFD·237*/221")]
    [InlineData("OGN·086/298")]
    [InlineData("OGN·100/298")]
    [InlineData("OGN·171/298")]
    [InlineData("OGN·174/298")]
    [InlineData("OGN·235/298")]
    public async Task ReplayedRuntimeUnitFacesCapturePredictAtTheirNewIncarnation(string card)
    {
        var initial = ReplayPosition(card);
        var cast = await Act(initial, "P1", new PlayCardCommand("HUNT", "UNL-184/219", ["HOST"]));
        var entered = await Top(cast.State);
        Assert.NotNull(entered.State.PendingEffectPlay);
        entered = await Act(entered.State, "P1", new PlayCardCommand("HOST", card, [], Destination: "BATTLEFIELD:F"));
        var trigger = Assert.Single(entered.State.StackItems);
        Assert.Equal("PRINTED_PREDICT", trigger.InsightContext!.Kind);
        Assert.Equal(entered.State.CardObjects["HOST"].ObjectGeneration, trigger.InsightContext.SourceGeneration);
        Restore(entered.State);
        var opened = await Top(entered.State); var choice = opened.State.PendingCardChoice!;
        var done = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, done.State.PlayerZones["P1"].MainDeck); Restore(done.State);
    }

    [Fact]
    public async Task OwnerDirectedReplayPredictUsesTheOwnerRatherThanTheSpellCaster()
    {
        var initial = ReplayPosition("OGN·174/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = "P2", ControllerId = "P1" } } };
        var entered = await Top((await Act(initial, "P1", new PlayCardCommand("HUNT", "UNL-184/219", ["HOST"]))).State);
        Assert.Equal("P2", entered.State.PendingEffectPlay!.PlayerId);
        entered = await Act(entered.State, "P2", new PlayCardCommand("HOST", "OGN·174/298", [], Destination: "BATTLEFIELD:F"));
        var trigger = Assert.Single(entered.State.StackItems);
        Assert.Equal("P2", trigger.ControllerId); Assert.Equal("P2", trigger.InsightContext!.ControllerId); Restore(entered.State);
        var opened = await Top(entered.State);
        Assert.Equal([initial.PlayerZones["P2"].MainDeck[0]], opened.State.PendingCardChoice!.LegalObjectIds);
        var done = await Act(opened.State, "P2", new ChooseCardsCommand(opened.State.PendingCardChoice.ChoiceId, "INSIGHT", []));
        Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Fact]
    public async Task ActualResponseReturningSourceToHandKeepsItsCapturedPredict()
    {
        var initial = GrantedPredictTests.Hand("OGN·171/298");
        var played = await Act(initial, "P1", new PlayCardCommand("MECH", "OGN·171/298", []));
        var response = AttackDamageDistributionTests.Spell(played.State, "BOUNCE", "OGN·104/298", "P1");
        var cast = await Act(response, "P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["MECH"]));
        var left = await Top(cast.State);
        Assert.Contains("MECH", left.State.PlayerZones["P1"].Hand); Assert.Single(left.State.StackItems); Restore(left.State);
        var opened = await Top(left.State); var choice = opened.State.PendingCardChoice!;
        Assert.Equal([initial.PlayerZones["P1"].MainDeck[0]], choice.LegalObjectIds);
        var done = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Fact]
    public async Task CopyingPrintedPredictOntoAnAlreadyEnteredImageDoesNotTriggerEntryAgain()
    {
        var copied = await OfficialCopyIdentityTests.Copy(OfficialCopyIdentityTests.Position("OGN·174/298"));
        Assert.Equal("OGN·174/298", copied.State.CardObjects["C-TOKEN-001"].CardNo);
        Assert.Empty(copied.State.StackItems); Assert.Empty(copied.State.TriggerQueue); Restore(copied.State);
    }

    [Theory]
    [InlineData("MISSING")]
    [InlineData("FACE")]
    [InlineData("SOURCE")]
    [InlineData("GENERATION")]
    [InlineData("REVISION")]
    [InlineData("TICK")]
    [InlineData("COUNT")]
    [InlineData("TOKEN")]
    public async Task RecoveryRejectsForgedPrintedPredictProvenance(string mutation)
    {
        var played = await Act(GrantedPredictTests.Hand("OGN·171/298"), "P1", new PlayCardCommand("MECH", "OGN·171/298", []));
        var item = Assert.Single(played.State.StackItems); var context = item.InsightContext!; var origin = context.PrintedPredict!;
        var forged = mutation switch {
            "MISSING" => context with { PrintedPredict = null },
            "FACE" => context with { CardNo = "SFD·065/221" },
            "GENERATION" => context with { SourceGeneration = 99 },
            "REVISION" => context with { PrintedPredict = origin with { FaceRevision = 99 } },
            "TICK" => context with { PrintedPredict = origin with { CapturedTick = played.State.Tick + 1 } },
            "COUNT" => context with { Count = 2 },
            "TOKEN" => context with { PrintedPredict = origin with { TokenFactoryCardNo = "NOT_A_FACTORY" } },
            _ => context };
        var bad = played.State with { StackItems = [item with { InsightContext = forged, SourceObjectId = mutation == "SOURCE" ? "UNKNOWN" : item.SourceObjectId }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), e => e.Contains("Insight", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MixedPrintedAndGrantedInstancesColdRecoverAndReplayEveryResponseAndChoice()
    {
        var initial = GrantedPredictTests.Hand("OGN·100/298") with { Tick = 0 };
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["AURA"] = new("AURA", cardNo: "OGN·100/298", ownerId: "P1", controllerId: "P1", power: 3, tags: [CardObjectTags.UnitCard, "预知"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Base = ["AURA"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["AURA"] = new("P1", "BASE") } };
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var result = await Submit("P1", new PlayCardCommand("MECH", "OGN·100/298", [], OptionalCosts: []));
        Assert.Equal(2, result.State.TriggerQueue.Count);
        Assert.Equal(["GRANTED_PREDICT", "PRINTED_PREDICT"], result.State.TriggerQueue.Select(t => t.InsightContext!.Kind).Order().ToArray());
        result = await Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: result.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        for (var instance = 0; instance < 2; instance++) {
            for (var pass = 0; pass < 2; pass++) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
            var choice = result.State.PendingCardChoice!;
            Assert.Equal([initial.PlayerZones["P1"].MainDeck[instance == 0 ? 0 : 1]], choice.LegalObjectIds);
            result = await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, instance == 0 ? choice.LegalObjectIds : []));
        }
        Assert.Empty(result.State.StackItems); Assert.Empty(result.State.TriggerQueue);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), result.State, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        async Task<ResolutionResult> Submit(string actor, GameCommand command) {
            var accepted = await session.SubmitAsync(actor, "printed-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
            Assert.True(accepted.Accepted, accepted.ErrorMessage);
            var restored = OfficialInsightAndSpellLockTests.Restore(accepted.State);
            session = MatchSession.Restore(new(initial.RoomId, restored.Tick, journal.Entries[^1].CompletedEventSequence,
                Commands(), Events(), new Dictionary<string, RecoveredPlayerView>(), [], restored, initial), engine, journal);
            return accepted;
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private static MatchState ReplayPosition(string card)
    {
        var initial = TurnUnitPlayGrantsTests.Position("OGN·223/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { CardNo = card, Power = 5, Tags = [CardObjectTags.UnitCard, "预知"] } } };
        return AttackDamageDistributionTests.Spell(initial, "HUNT", "UNL-184/219", "P1");
    }
}
