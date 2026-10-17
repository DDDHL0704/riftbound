using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class CardPlayHistoryTests
{
    [Theory]
    [InlineData("OGN·096/298", 1)]
    [InlineData("SFD·059/221", 2)]
    public async Task OrdinaryPermanentConfirmationAndEntryAreOnePlayInEachStage(string card, int types)
    {
        var played = await Act(PermanentPlayTargetTests.Position(card), "P1", new PlayCardCommand("UNIT", card, []));
        var finalized = Assert.Single(played.State.CardPlays.Finalized);
        var resolved = Assert.Single(played.State.CardPlays.Resolved);
        Assert.Equal(finalized.PlayId, resolved.PlayId);
        Assert.Equal(card, resolved.CardNo); Assert.Equal(types, resolved.Types);
        Assert.Equal("P1", finalized.PlayerId); Assert.Equal("P1", resolved.PlayerId);
        Assert.Equal(played.State.TurnNumber, played.State.CardPlays.TurnNumber);
        Assert.Equal(played.State.CardObjects["UNIT"].ObjectGeneration, resolved.Source.Generation);
        NoPrivateEvents(played); Restore(played.State);
    }

    [Fact]
    public async Task ASpellIsFinalizedBeforeItsSuccessfulResolution()
    {
        var played = await Act(Initial(), "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        var finalized = Assert.Single(played.State.CardPlays.Finalized);
        Assert.Equal("P1", finalized.PlayerId); Assert.Equal(4, finalized.Types);
        Assert.Empty(played.State.CardPlays.Resolved); Restore(played.State);
        var done = await Top(played.State);
        var resolved = Assert.Single(done.State.CardPlays.Resolved);
        Assert.Equal(finalized.PlayId, resolved.PlayId); Assert.Equal("P1", resolved.PlayerId);
        Assert.Equal(4, resolved.Types); Assert.True(resolved.Tick > finalized.Tick);
        NoPrivateEvents(done); Restore(done.State);
    }

    [Fact]
    public async Task AResolvingSpellStillRecordsCompletionWhenBurnoutEndsTheMatch()
    {
        var initial = Initial();
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [] } },
            CardObjects = initial.CardObjects.Where(e => e.Key is not ("D1" or "D2")).ToDictionary(e => e.Key, e => e.Value),
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 } };
        var done = await Top((await Act(initial, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []))).State);
        Assert.Equal(MatchStatuses.Finished, done.State.Status); Assert.Equal("P2", done.State.WinnerPlayerId);
        Assert.Equal("SPELL", Assert.Single(done.State.CardPlays.Resolved).Source.ObjectId);
        Assert.Single(done.State.CardPlays.Finalized); NoPrivateEvents(done); Restore(done.State);
    }

    [Fact]
    public async Task ActualCounterKeepsTheOriginalFinalizationButDoesNotInventItsResolution()
    {
        var played = await Act(Initial(), "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        var passed = await Act(played.State, "P1", new PassPriorityCommand());
        var counter = await Act(passed.State, "P2", new PlayCardCommand("COUNTER", "OGN·064/298", [Assert.Single(played.State.StackItems).StackItemId]));
        var done = await Top(counter.State);
        Assert.Equal(2, done.State.CardPlays.Finalized.Count);
        Assert.Equal("P1", Assert.Single(done.State.CardPlays.Finalized, e => e.Source.ObjectId == "SPELL").PlayerId);
        var completed = Assert.Single(done.State.CardPlays.Resolved);
        Assert.Equal("COUNTER", completed.Source.ObjectId); Assert.Equal("P2", completed.PlayerId);
        Assert.Empty(done.State.StackItems); NoPrivateEvents(done); Restore(done.State);
    }

    [Fact]
    public async Task RebuttalPreservesTheFinalizerAndRecordsTheActualResolvingController()
    {
        var state = await RebuttalRulesTests.Open();
        state = await RebuttalRulesTests.Choose(state, "PAY:ANY");
        state = await RebuttalRulesTests.Choose(state, "KEEP_ALL");
        Assert.Equal("P1", Assert.Single(state.CardPlays.Finalized, e => e.Source.ObjectId == "R-SPELL").PlayerId);
        Assert.DoesNotContain(state.CardPlays.Resolved, e => e.Source.ObjectId == "R-SPELL");
        state = (await Top(state)).State;
        var resolved = Assert.Single(state.CardPlays.Resolved, e => e.Source.ObjectId == "R-SPELL");
        Assert.Equal("P2", resolved.PlayerId); Assert.Equal(4, resolved.Types);
        Assert.Equal("P1", state.CardObjects["R-SPELL"].OwnerId); Restore(state);
    }

    [Fact]
    public async Task OwnerDirectedLimitedUnitPlayBelongsToTheOwnerRatherThanTheSpellCaster()
    {
        var initial = TurnUnitPlayGrantsTests.Position("OGN·223/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { CardNo = "OGN·174/298", OwnerId = "P2", Power = 5,
                Tags = [CardObjectTags.UnitCard, "预知"] } } };
        initial = AttackDamageDistributionTests.Spell(initial, "HUNT", "UNL-184/219", "P1");
        var done = await Top((await Act(initial, "P1", new PlayCardCommand("HUNT", "UNL-184/219", ["HOST"]))).State);
        Assert.Equal("P2", done.State.PendingEffectPlay!.PlayerId);
        done = await Act(done.State, "P2", new PlayCardCommand("HOST", "OGN·174/298", [], Destination: "BATTLEFIELD:F"));
        var unit = Assert.Single(done.State.CardPlays.Resolved, e => e.Source.ObjectId == "HOST");
        Assert.Equal("P2", unit.PlayerId); Assert.Equal(1, unit.Types); Assert.StartsWith("LIMITED-", unit.PlayId);
        Assert.Equal("P2", Assert.Single(done.State.CardPlays.Finalized, e => e.PlayId == unit.PlayId).PlayerId);
        Assert.Equal("P1", Assert.Single(done.State.CardPlays.Resolved, e => e.Source.ObjectId == "HUNT").PlayerId);
        NoPrivateEvents(done); Restore(done.State);
    }

    [Fact]
    public async Task RealImageCreationAndCopiedPrintedFacesDoNotBecomeNonTokenCardPlays()
    {
        var copied = await OfficialCopyIdentityTests.Copy(OfficialCopyIdentityTests.Position("OGN·096/298"));
        Assert.DoesNotContain(copied.State.CardPlays.Finalized, e => e.Source.ObjectId == "C-TOKEN-001");
        Assert.DoesNotContain(copied.State.CardPlays.Resolved, e => e.Source.ObjectId == "C-TOKEN-001");
        Assert.Single(copied.State.CardPlays.Resolved); Restore(copied.State);
    }

    [Fact]
    public async Task BothHistoriesExpireAtTheActualNextTurn()
    {
        var done = await Top((await Act(Initial(), "P1", new PlayCardCommand("SPELL", "OGN·048/298", []))).State);
        var oldTurn = done.State.TurnNumber;
        var next = await Act(done.State, "P1", new EndTurnCommand());
        Assert.True(next.State.TurnNumber > oldTurn);
        Assert.Equal(next.State.TurnNumber, next.State.CardPlays.TurnNumber);
        Assert.Empty(next.State.CardPlays.Finalized); Assert.Empty(next.State.CardPlays.Resolved); Restore(next.State);
    }

    [Theory]
    [InlineData("TYPE")]
    [InlineData("ACTOR")]
    [InlineData("GENERATION")]
    [InlineData("TICK")]
    [InlineData("FACE")]
    [InlineData("PLAY_ID")]
    [InlineData("DUPLICATE")]
    [InlineData("TURN")]
    [InlineData("PRINTING")]
    [InlineData("SHORT_ID")]
    [InlineData("EMPTY_CARD")]
    public async Task StructuralHistoryTamperingIsRejectedIndependentlyOnRecovery(string mutation)
    {
        var state = (await Act(PermanentPlayTargetTests.Position("OGN·096/298"), "P1",
            new PlayCardCommand("UNIT", "OGN·096/298", []))).State;
        var history = state.CardPlays; var entry = Assert.Single(history.Resolved);
        var changed = mutation switch {
            "TYPE" => entry with { Types = 7 }, "ACTOR" => entry with { PlayerId = "MISSING" },
            "GENERATION" => entry with { Source = entry.Source with { Generation = 99 } },
            "TICK" => entry with { Tick = state.Tick + 1 }, "FACE" => entry with { FaceRevision = 99 },
            "PRINTING" => entry with { CardNo = "OGN·100/298" }, "SHORT_ID" => entry with { PlayId = "LIMITED-UNIT" },
            "EMPTY_CARD" => entry with { CardNo = null! },
            "PLAY_ID" => entry with { PlayId = "STACK-999999-UNIT" }, _ => entry };
        var bad = history with { Resolved = mutation == "DUPLICATE" ? [changed, changed] : [changed] };
        if (mutation == "TURN") bad = bad with { TurnNumber = state.TurnNumber - 1 };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { CardPlays = bad }),
            e => e.Contains("typed card play history", StringComparison.Ordinal));
        Restore(state);
    }

    [Fact]
    public void CurrentSnapshotsCannotOmitTheRequiredHistoryField()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Initial()))!;
        Assert.True(json.AsObject().Remove("CardPlays"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MatchState>(json.ToJsonString()));
    }

    internal static MatchState Initial() => new("card-play-history", 0, 3, "P1",
        new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
        status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
        timingState: TimingStates.NeutralOpen,
        playerZones: new Dictionary<string, PlayerZones> {
            ["P1"] = PlayerZones.Empty with { Hand = ["SPELL"], MainDeck = ["D1", "D2"] },
            ["P2"] = PlayerZones.Empty with { Hand = ["COUNTER"], MainDeck = ["E1", "E2"] } },
        cardObjects: new Dictionary<string, CardObjectState> {
            ["SPELL"] = new("SPELL", cardNo: "OGN·048/298", tags: [CardObjectTags.SpellCard], ownerId: "P1", controllerId: "P1"),
            ["COUNTER"] = new("COUNTER", cardNo: "OGN·064/298", tags: [CardObjectTags.SpellCard], ownerId: "P2", controllerId: "P2"),
            ["D1"] = new("D1", cardNo: "OGN·048/298", tags: [CardObjectTags.SpellCard], ownerId: "P1", controllerId: "P1"),
            ["D2"] = new("D2", cardNo: "OGN·048/298", tags: [CardObjectTags.SpellCard], ownerId: "P1", controllerId: "P1"),
            ["E1"] = new("E1", cardNo: "OGN·048/298", tags: [CardObjectTags.SpellCard], ownerId: "P2", controllerId: "P2"),
            ["E2"] = new("E2", cardNo: "OGN·048/298", tags: [CardObjectTags.SpellCard], ownerId: "P2", controllerId: "P2") },
        playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
        runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) });

    private static void NoPrivateEvents(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, e => e.Kind == "INTERNAL_CARD_PLAY_COMPLETION");
        Assert.DoesNotContain("cardPlayHistoryObservation", JsonSerializer.Serialize(result.Events));
        Assert.DoesNotContain("historyPlayId", JsonSerializer.Serialize(result.Events));
        Assert.False(JsonSerializer.Serialize(result.Snapshots).Contains("cardPlays", StringComparison.OrdinalIgnoreCase));
    }
}
