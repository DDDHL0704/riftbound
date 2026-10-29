using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class FrontendRuleFeedbackTests
{
    [Fact]
    public async Task TargetlessQuoteReturnsKnownReductionsAndMarksTheRemainingTargetChoice()
    {
        var state = SpellPosition(new(0, 1)) with { UntilEndOfTurnEffects = ["RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:DRAKE"] };
        var command = new PlayCardCommand("CARD", "OGS·003/024", []);
        var engine = new CoreRuleEngine();
        var before = MatchStateHasher.Hash(state);
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.IsValid, quote.Message);
        Assert.False(quote.CanPay);
        Assert.True(quote.RequiresTargetSelection);
        Assert.True(quote.TargetDependentCosts);
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower);
        Assert.Null(quote.Cost.RemainingMana);
        Assert.Contains(quote.Cost.Adjustments, a => a.Mana < 0);
        var selected = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command with { TargetObjectIds = ["SHIELD"] }));
        Assert.True(selected.CanPay, selected.Message);
        Assert.False(selected.RequiresTargetSelection);
        Assert.Equal(1, selected.Cost!.GenericPower);
        var rejected = await engine.ResolveAsync(state, new("missing-target", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Fact]
    public async Task InvalidQuotePreservesTheCommandRejectionReason()
    {
        var state = SpellPosition(new(20, 20));
        var command = new PlayCardCommand("CARD", "OGS·003/024", ["UNKNOWN"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        var rejected = await engine.ResolveAsync(state, new("bad-target", "P1", command.CmdType), command, default);
        Assert.False(quote.IsValid);
        Assert.Null(quote.Cost);
        Assert.Equal(rejected.ErrorCode, quote.ErrorCode);
        Assert.Equal(rejected.ErrorMessage, quote.Message);
        Assert.Contains("合法目标", quote.Message);
    }

    [Theory]
    [InlineData("mana", "法力不足")]
    [InlineData("power", "符能不足")]
    [InlineData("experience", "经验不足")]
    public async Task QuoteAndCommitExplainTheSpecificInsufficientResourceInChinese(string resource, string reason)
    {
        var state = PlayCostPreviewTests.Position(resource == "mana" ? new(0, 1) : new(4, 0));
        var command = PlayCostPreviewTests.Command([]);
        if (resource == "experience")
        {
            state = AmbushPermissionTests.Position() with { PlayerExperience = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 } };
            command = AmbushPermissionTests.Poppy();
        }
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        var rejected = await engine.ResolveAsync(state, new("insufficient", "P1", command.CmdType), command, default);
        Assert.True(quote.IsValid, quote.Message);
        Assert.False(quote.CanPay);
        Assert.Contains(reason, quote.Message);
        Assert.Equal(rejected.ErrorMessage, quote.Message);
        Assert.Equal(ErrorCodes.InsufficientCost, rejected.ErrorCode);
    }

    [Fact]
    public async Task UnofferedCommandWithTheCurrentPromptIsRejectedWithoutChangingStateAndDeduplicates()
    {
        var state = PlayCostPreviewTests.Position(new(20, 20)) with { Tick = 0 };
        var journal = new Journal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        var prompt = session.PromptFor("P2");
        Assert.DoesNotContain(prompt.Candidates ?? [], c => c.Action == CommandTypes.MoveUnit && c.Enabled);
        var command = new MoveUnitCommand("P2-CARD", "BASE", "BATTLEFIELD:UNKNOWN", []);
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = state.Tick;
        var wire = JsonSerializer.SerializeToElement(raw);
        var before = MatchStateHasher.Hash(state);
        var result = await session.SubmitAsync("P2", "unoffered", GameCommandJsonMapper.Map(wire), wire, default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, result.ErrorCode);
        Assert.Contains("未提供此操作", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        var rows = journal.Entries.Count;
        var duplicate = await session.SubmitAsync("P2", "unoffered", GameCommandJsonMapper.Map(wire), wire, default);
        Assert.Equal(result.ErrorCode, duplicate.ErrorCode);
        Assert.Equal(rows, journal.Entries.Count);
        Assert.Equal(before, MatchStateHasher.Hash(duplicate.State));
    }

    private static MatchState SpellPosition(RunePool pool)
    {
        var state = PlayCostPreviewTests.Position(pool, "OGS·003/024");
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Battlefields = ["SHIELD"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SHIELD"] = new("SHIELD", cardNo: "OGN·013/298", ownerId: "P2", controllerId: "P2", power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SHIELD"] = new("P2", "BATTLEFIELD", "F") } };
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
