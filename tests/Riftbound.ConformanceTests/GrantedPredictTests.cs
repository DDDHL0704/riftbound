using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class GrantedPredictTests
{
    private const string Mech = "SFD·065/221";
    private const string Seer = "OGN·100/298";
    private const string Mechanical = "SFD·075/221";
    [Fact]
    public async Task PrescientMechsOwnMechanicalTraitReceivesItsPredictGrantOnPlay()
    {
        var state = OfficialCopyIdentityTests.AddSpell(OfficialCopyIdentityTests.Position(), "MECH", "SFD·065/221");
        state = state with { CardObjects = new Dictionary<string,CardObjectState>(state.CardObjects) {
            ["MECH"] = state.CardObjects["MECH"] with { Power=2,Tags=[CardObjectTags.UnitCard,"约德尔人","机械"] } } };
        var played = await Act(state,"P1",new PlayCardCommand("MECH","SFD·065/221",[]));
        Assert.Contains("MECH",played.State.PlayerZones["P1"].Base); Assert.Null(played.State.PendingCardChoice);
        var opened = await Top(played.State);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal([state.PlayerZones["P1"].MainDeck[0]],choice.LegalObjectIds);
        Assert.Equal(0,choice.RequiredCount); Assert.Equal(1,choice.MaxCount); Restore(opened.State);
        var done = await Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
        Assert.Equal(state.PlayerZones["P1"].MainDeck,done.State.PlayerZones["P1"].MainDeck); Restore(done.State);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EachGrantHasItsOwnResponseWindowAndReadsTheCurrentTop(bool self, bool recycleFirst)
    {
        var initial = AddAura(AddAura(Hand(self ? Mech : Mechanical), "A", Mech), "B", Seer);
        var played = await Act(initial, "P1", new PlayCardCommand("MECH", self ? Mech : Mechanical, []));
        Assert.Equal(self ? 3 : 2, played.State.TriggerQueue.Count);
        Assert.Equal(played.State.TriggerQueue.Count, played.State.TriggerQueue.Select(t => t.TriggerId).Distinct().Count());
        Restore(played.State);
        var ordered = await Act(played.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: played.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        var current = ordered.State;
        for (var i = 0; i < (self ? 3 : 2); i++) {
            var opened = await Top(current);
            var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
            Assert.Equal([current.PlayerZones["P1"].MainDeck[0]], choice.LegalObjectIds);
            Assert.Equal(initial.PlayerZones["P1"].MainDeck[recycleFirst && i > 0 ? 1 : 0], choice.LegalObjectIds[0]);
            Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount); Restore(opened.State);
            current = (await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                recycleFirst && i == 0 ? choice.LegalObjectIds : []))).State;
            Restore(current);
        }
        Assert.Empty(current.StackItems);
        Assert.Equal(recycleFirst ? initial.PlayerZones["P1"].MainDeck.Skip(1).Append(initial.PlayerZones["P1"].MainDeck[0]) : initial.PlayerZones["P1"].MainDeck,
            current.PlayerZones["P1"].MainDeck);
    }

    [Theory]
    [InlineData("ENEMY")]
    [InlineData("HIDDEN")]
    [InlineData("NOT_MECHANICAL")]
    public async Task InapplicableAurasDoNotCreatePredict(string kind)
    {
        var initial = AddAura(Hand(kind == "NOT_MECHANICAL" ? "SFD·068/221" : Mechanical), "A", Mech, kind == "ENEMY" ? "P2" : "P1");
        if (kind == "HIDDEN") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["A"] = initial.CardObjects["A"] with { IsFaceDown = true } } };
        var played = await Act(initial, "P1", new PlayCardCommand("MECH", initial.CardObjects["MECH"].CardNo!, []));
        Assert.DoesNotContain(played.State.StackItems, s => s.InsightContext?.Kind == "GRANTED_PREDICT");
        Assert.DoesNotContain(played.State.TriggerQueue, t => t.InsightContext?.Kind == "GRANTED_PREDICT");
        Assert.Null(played.State.PendingCardChoice);
    }

    [Theory]
    [InlineData("LEFT")]
    [InlineData("CONTROL")]
    [InlineData("COPIED")]
    public async Task CapturedGrantSurvivesLaterSourceChanges(string change)
    {
        var played = await Act(AddAura(Hand(Mechanical), "A", Mech), "P1", new PlayCardCommand("MECH", Mechanical, []));
        var state = played.State;
        Assert.Single(state.StackItems);
        var aura = state.CardObjects["A"];
        if (change == "LEFT") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["A"] = aura with { ObjectGeneration = aura.ObjectGeneration + 1 } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Where(id => id != "A").ToArray(), Graveyard = ["A"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["A"] = new("P1", "GRAVEYARD") } };
        else if (change == "CONTROL") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["A"] = aura with { ControllerId = "P2" } } };
        else state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["A"] = aura with { CardNo = "SFD·068/221", CopyFaceTransitions = [new(state.Tick, aura.ObjectGeneration, Mech, "SFD·068/221")] } } };
        Restore(state);
        var opened = await Top(state);
        Assert.Equal("P1", opened.State.PendingCardChoice!.PlayerId); Restore(opened.State);
        var done = await Act(opened.State, "P1", new ChooseCardsCommand(opened.State.PendingCardChoice.ChoiceId, "INSIGHT", []));
        Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Fact]
    public async Task AuraEnteringLaterDoesNotRetroactivelyTriggerForExistingMechanicalUnit()
    {
        var initial = AddAura(Hand(Mech), "OLD", Mechanical);
        var played = await Act(initial, "P1", new PlayCardCommand("MECH", Mech, []));
        var item = Assert.Single(played.State.StackItems);
        Assert.Equal("MECH", item.SourceObjectId);
        Assert.Equal("MECH", item.InsightContext!.PredictGrant!.Source.ObjectId);
        Assert.Equal(played.State.CardObjects["MECH"].ObjectGeneration, item.InsightContext.PredictGrant.Source.Generation);
        Restore(played.State);
    }

    [Fact]
    public async Task EmptyDeckCompletesPredictWithoutBurningOut()
    {
        var initial = Hand(Mech);
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Graveyard = initial.PlayerZones["P1"].MainDeck } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => initial.PlayerZones["P1"].MainDeck.Contains(p.Key) ? new ObjectLocationState("P1", "GRAVEYARD") : p.Value) };
        var done = await Top((await Act(initial, "P1", new PlayCardCommand("MECH", Mech, []))).State);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.DoesNotContain(done.Events, e => e.Kind.Contains("BURN", StringComparison.Ordinal)); Restore(done.State);
    }

    [Theory]
    [InlineData("MISSING")]
    [InlineData("CARD")]
    [InlineData("ORIGIN")]
    [InlineData("GENERATION")]
    [InlineData("ENTRY_GENERATION")]
    [InlineData("REVISION")]
    [InlineData("TICK")]
    [InlineData("COUNT")]
    [InlineData("SELF_GENERATION")]
    public async Task IndependentRecoveryRejectsForgedPredictProvenance(string mutation)
    {
        var played = await Act(Hand(Mech), "P1", new PlayCardCommand("MECH", Mech, []));
        var item = Assert.Single(played.State.StackItems); var context = item.InsightContext!; var grant = context.PredictGrant!;
        var forged = mutation switch {
            "MISSING" => context with { PredictGrant = null },
            "CARD" => context with { PredictGrant = grant with { CardNo = "SFD·068/221" } },
            "ORIGIN" => context with { PredictGrant = grant with { Source = grant.Source with { ObjectId = "UNKNOWN" } } },
            "GENERATION" => context with { PredictGrant = grant with { Source = grant.Source with { Generation = 99 } } },
            "ENTRY_GENERATION" => context with { SourceGeneration = 99 },
            "REVISION" => context with { PredictGrant = grant with { SourceFaceRevision = 99 } },
            "TICK" => context with { PredictGrant = grant with { CapturedTick = played.State.Tick + 1 } },
            "COUNT" => context with { Count = 2 },
            _ => context with { PredictGrant = grant with { Source = grant.Source with { Generation = 0 } } } };
        var bad = played.State with { StackItems = [item with { InsightContext = forged }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), e => e.Contains("Insight", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrivateChoiceRejectsOpponentAndStaleOrOutsideTopSelections()
    {
        var opened = await Top((await Act(Hand(Mech), "P1", new PlayCardCommand("MECH", Mech, []))).State);
        var choice = opened.State.PendingCardChoice!;
        Assert.Equal([opened.State.PlayerZones["P1"].MainDeck[0]], choice.ContextObjectIds);
        var opponent = JsonSerializer.Serialize(opened.Snapshots["P2"]);
        Assert.DoesNotContain(JsonSerializer.Serialize(choice.ContextObjectIds[0]), opponent);
        foreach (var (player, command) in new[] {
            ("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])),
            ("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [opened.State.PlayerZones["P1"].MainDeck[1]])),
            ("P1", new ChooseCardsCommand("STALE", choice.ChoiceWindow, [])) }) {
            var rejected = await new CoreRuleEngine().ResolveAsync(opened.State, new("bad", player, command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(opened.State), MatchStateHasher.Hash(rejected.State));
        }
        Restore(opened.State);
    }

    [Fact]
    public async Task PrintedAndGrantedPredictAreSeparateInstances()
    {
        var initial = AddAura(Hand(Seer), "A", Seer);
        var played = await Act(initial, "P1", new PlayCardCommand("MECH", Seer, []));
        Assert.Equal(2, played.State.TriggerQueue.Count); Restore(played.State);
        played = await Act(played.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: played.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var first = await Top(played.State); var firstChoice = first.State.PendingCardChoice!;
        Assert.Equal("INSIGHT", firstChoice.ChoiceWindow); Assert.Equal([initial.PlayerZones["P1"].MainDeck[0]], firstChoice.LegalObjectIds);
        var kept = await Act(first.State, "P1", new ChooseCardsCommand(firstChoice.ChoiceId, firstChoice.ChoiceWindow, [])); Restore(kept.State);
        var second = await Top(kept.State); var secondChoice = second.State.PendingCardChoice!;
        Assert.Equal("INSIGHT", secondChoice.ChoiceWindow); Assert.Equal(firstChoice.LegalObjectIds, secondChoice.LegalObjectIds);
        var done = await Act(second.State, "P1", new ChooseCardsCommand(secondChoice.ChoiceId, secondChoice.ChoiceWindow, []));
        Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Fact]
    public async Task NewlyPlayedTokensReceiveSeparateEntryGrantsWithoutRetriggeringWhenCopied()
    {
        var initial = AddAura(MaskedAttendantTests.Position(), "A", Seer);
        var played = await Act(initial, "P1", new PlayCardCommand("MASK", MaskedAttendantTests.Card, []));
        // First resolve Masked Attendant's own granted instance, then its play ability.
        var first = await Top(played.State);
        var kept = await Act(first.State, "P1", new ChooseCardsCommand(first.State.PendingCardChoice!.ChoiceId, "INSIGHT", []));
        var created = await Top(kept.State);
        Assert.Equal(3, created.State.TriggerQueue.Count);
        Assert.Equal(2, created.State.TriggerQueue.Count(t => t.InsightContext?.Kind == "GRANTED_PREDICT")); Restore(created.State);
        var ordered = await Act(created.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: created.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var current = ordered.State; var observed = 0;
        while (current.StackItems.Count > 0) {
            var opened = await Top(current); current = opened.State;
            if (current.PendingCardChoice is { } choice) {
                observed++;
                current = (await Act(current, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []))).State;
            }
            Restore(current);
        }
        Assert.Equal(2, observed); Assert.Empty(current.TriggerQueue);
        Assert.Equal(2, MaskedAttendantTests.Images(current).Length);
    }

    [Fact]
    public async Task ReentryBindsTheNewIncarnationRatherThanThePreviousOne()
    {
        var initial = Hand(Mech);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["MECH"] = initial.CardObjects["MECH"] with { ObjectGeneration = 4 } } };
        var played = await Act(initial, "P1", new PlayCardCommand("MECH", Mech, []));
        var context = Assert.Single(played.State.StackItems).InsightContext!;
        Assert.Equal(5, context.SourceGeneration); Assert.Equal(5, context.PredictGrant!.Source.Generation); Restore(played.State);
    }

    [Fact]
    public async Task ColdRecoveryAndFullActionEventReplayKeepMultipleInstancesAndChoices()
    {
        var initial = AddAura(Hand(Mech), "A", Seer) with { Tick = 0 };
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var result = await Submit("P1", new PlayCardCommand("MECH", Mech, [], OptionalCosts: []));
        Assert.Equal(2, result.State.TriggerQueue.Count);
        result = await Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: result.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        for (var instance = 0; instance < 2; instance++) {
            for (var pass = 0; pass < 2; pass++) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
            var choice = result.State.PendingCardChoice!;
            result = await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, instance == 0 ? choice.LegalObjectIds : []));
        }
        Assert.Empty(result.State.StackItems); Assert.Empty(result.State.TriggerQueue);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), result.State, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        async Task<ResolutionResult> Submit(string player, GameCommand command) {
            var accepted = await session.SubmitAsync(player, "predict-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
            Assert.True(accepted.Accepted, accepted.ErrorMessage);
            var restored = OfficialInsightAndSpellLockTests.Restore(accepted.State);
            var frame = new MatchRecoveryFrame(initial.RoomId, restored.Tick, journal.Entries[^1].CompletedEventSequence,
                Commands(), Events(), new Dictionary<string, RecoveredPlayerView>(), [], restored, initial);
            session = MatchSession.Restore(frame, engine, journal); return accepted;
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    internal static MatchState Hand(string card)
    {
        var state = OfficialCopyIdentityTests.AddSpell(OfficialCopyIdentityTests.Position(), "MECH", card);
        var traits = card == Mech ? new[] { CardObjectTags.UnitCard, "约德尔人", "机械" }
            : card == Mechanical ? new[] { CardObjectTags.UnitCard, "机械" } : new[] { CardObjectTags.UnitCard };
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["MECH"] = state.CardObjects["MECH"] with { Power = card == Mech ? 2 : 3, Tags = traits } } };
    }

    private static MatchState AddAura(MatchState state, string id, string card, string player = "P1") => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = new(id, cardNo: card, ownerId: player, controllerId: player,
            power: card == Mech ? 2 : 3, tags: card == Mech ? [CardObjectTags.UnitCard, "约德尔人", "机械"] : [CardObjectTags.UnitCard]) },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = state.PlayerZones[player] with { Base = state.PlayerZones[player].Base.Append(id).ToArray() } },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BASE") } };
}
