using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayCostPreviewTests
{
    [Fact]
    public async Task EachReducerFloorAppliesOnlyToThatReducer_Cn356Example()
    {
        var state = Position(new(0, 2), "OGN·014/298");
        state = state with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            { ["P1"] = state.PlayerZones["P1"] with { Battlefields = ["APPRENTICE", "STRONG"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                ["APPRENTICE"] = new("APPRENTICE", cardNo: "OGN·084/298", ownerId: "P1", controllerId: "P1", power: 3, tags: [CardObjectTags.UnitCard]),
                ["STRONG"] = new("STRONG", cardNo: "OGN·025/298", ownerId: "P1", controllerId: "P1", power: 7, tags: [CardObjectTags.UnitCard])
            }
        };
        var command = new PlayCardCommand("CARD", "OGN·014/298", ["STRONG"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", Request(state, command));
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.True(quote.CanPay, quote.Message);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("floor", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(0, Assert.Single(result.Events, e => e.Kind == "COST_PAID").Payload!["mana"]);
    }

    [Fact]
    public async Task ManaReductionLeavesWardPowerPayable()
    {
        var state = Position(new(0, 1), "OGS·003/024");
        state = state with
        {
            UntilEndOfTurnEffects = ["RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:DRAKE"],
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            { ["P2"] = PlayerZones.Empty with { Battlefields = ["SHIELD"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["SHIELD"] = new("SHIELD", cardNo: "OGN·013/298", ownerId: "P2", controllerId: "P2", power: 3,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield]) }
        };
        var command = new PlayCardCommand("CARD", "OGS·003/024", ["SHIELD"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", Request(state, command));
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.GenericPower);
        Assert.True(quote.CanPay, quote.Message);
        var result = await engine.ResolveAsync(state, new("tax-reduction", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(0, Assert.Single(result.Events, e => e.Kind == "COST_PAID").Payload!["mana"]);
        Assert.DoesNotContain("RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:DRAKE", result.State.UntilEndOfTurnEffects);
    }

    [Theory]
    [InlineData(false, 4, 1)]
    [InlineData(true, 5, 2)]
    public async Task PreviewAndCommitUseTheSameHastePayment(bool haste, int mana, int power)
    {
        var state = Position(new(8, 4));
        var command = Command(haste ? [HasteOptionalCostNames.HasteReady] : []);
        var before = MatchStateHasher.Hash(state);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.PowerByTrait["purple"]);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        var result = await engine.ResolveAsync(state, new("commit", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(quote.Cost.RemainingMana!.Value, quote.Cost.RemainingRainbowPower!.Value,
            quote.Cost.RemainingPowerByTrait), result.State.RunePools["P1"]);
        var paid = Assert.Single(result.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(mana, paid.Payload!["mana"]);
        Assert.Equal(power, paid.Payload["power"]);
    }

    [Fact]
    public void InsufficientQuoteReturnsFinalCostAndSeparateDeficitsWithoutMutation()
    {
        var state = Position(new(3, 1));
        var before = MatchStateHasher.Hash(state);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", Request(state, Command([HasteOptionalCostNames.HasteReady])));
        Assert.True(quote.IsValid);
        Assert.False(quote.CanPay);
        Assert.Equal(5, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.MissingMana);
        Assert.Equal(1, quote.Cost.MissingPower);
        Assert.Null(quote.Cost.RemainingMana);
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData("P2-CARD", "BASE")]
    [InlineData("CARD", "UNKNOWN-BATTLEFIELD")]
    public void InvalidSourcesAndDestinationsNeverReturnAPayableQuote(string source, string destination)
    {
        var state = Position(new(20, 20));
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", Request(state, Command([]) with { SourceObjectId = source, Destination = destination }));
        Assert.False(quote.IsValid);
        Assert.False(quote.CanPay);
        Assert.Null(quote.Cost);
    }

    [Fact]
    public async Task PreviewDoesNotRecycleResourcesOrConsumeTemporaryResource()
    {
        var state = Position(new(4, 0), "SFD·190/221");
        state = state with
        {
            PlayerZones = new Dictionary<string, PlayerZones> { ["P1"] = state.PlayerZones["P1"] with { Base = ["RUNE"] }, ["P2"] = PlayerZones.Empty },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["RUNE"] = new("RUNE", cardNo: "UNL-R03", tags: [CardObjectTags.RuneCard, "COLOR:blue"], ownerId: "P1", controllerId: "P1") },
            TemporaryPaymentResources = [new("TEMP", "P1", "SOURCE", "TEST_RESOURCE", "PLAY_CARD", 1, 1, allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind])]
        };
        var command = Command(["RECYCLE_RUNE:RUNE", "TEMP_PAYMENT_RESOURCE:TEMP", "PRINTED_POWER:blue:1,green:1"]) with { CardNo = "SFD·190/221" };
        var before = MatchStateHasher.Hash(state);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("payment", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Single(result.State.PlayerZones["P1"].RuneDeck);
        Assert.Empty(result.State.TemporaryPaymentResources);
    }

    [Fact]
    public async Task SessionPreviewIsReadOnlyAndCannotAuthorizeAStaleSubmission()
    {
        var state = Position(new(5, 2));
        var journal = new Journal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        var request = Request(state, Command([HasteOptionalCostNames.HasteReady]));
        var first = await session.PreviewPlayCardAsync("P1", request, default);
        var second = await session.PreviewPlayCardAsync("P1", request, default);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.True(first.CanPay, first.Message);
        Assert.Empty(journal.Entries);
        Assert.Equal(state.Tick, session.SnapshotFor("P1").Tick);
        var expired = await session.PreviewPlayCardAsync("P1", request with { SnapshotTick = state.Tick - 1 }, default);
        Assert.Equal(ErrorCodes.PromptExpired, expired.ErrorCode);
        Assert.Null(expired.Cost);
        var commit = await session.SubmitAsync("P1", request.RequestId, request.Command, JsonSerializer.SerializeToElement(new
        { cmdType = "PLAY_CARD", request.PromptId, request.SnapshotTick }), default);
        Assert.True(commit.Accepted, commit.ErrorMessage);
        Assert.Single(journal.Entries);
        var stale = await session.PreviewPlayCardAsync("P1", request, default);
        Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode);
        Assert.Single(journal.Entries);
    }

    internal static MatchState Position(RunePool pool, string cardNo = "SFD·143/221")
        => new("COST-PREVIEW", 1, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = pool, ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["CARD"] }, ["P2"] = PlayerZones.Empty with { Hand = ["P2-CARD"] } },
            cardObjects: new Dictionary<string, CardObjectState>
            {
                ["CARD"] = new("CARD", cardNo: cardNo, ownerId: "P1", controllerId: "P1"),
                ["P2-CARD"] = new("P2-CARD", cardNo: cardNo, ownerId: "P2", controllerId: "P2")
            });

    internal static PlayCardCommand Command(string[] costs) => new("CARD", "SFD·143/221", [], OptionalCosts: costs, Destination: "BASE");
    internal static PlayCostPreviewRequestDto Request(MatchState state, PlayCardCommand command)
        => new("preview-id", ResolutionResult.BuildPrompts(state)["P1"].PromptId!, state.Tick, command);
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
