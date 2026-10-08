using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RuneBatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BatchCommitsAllRunesAsOneIntentAndReplays(bool recycle)
    {
        var state = Position();
        var payload = JsonSerializer.SerializeToElement(new
        {
            cmdType = recycle ? "RECYCLE_RUNE" : "TAP_RUNE",
            sourceObjectId = "RED", sourceObjectIds = new[] { "RED", "BLUE" }
        });
        var engine = new CoreRuleEngine();
        var command = GameCommandJsonMapper.Map(payload);
        var result = await engine.ResolveAsync(state, new("batch", "P1", command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(state.Tick + 1, result.State.Tick);
        Assert.Equal(2, result.Events.Count(e => e.Kind == (recycle ? "RUNE_RECYCLED" : "RUNE_TAPPED")));
        if (recycle)
        {
            Assert.Empty(result.State.PlayerZones["P1"].Base);
            Assert.Equal(new[] { "RED", "BLUE" }, result.State.PlayerZones["P1"].RuneDeck);
            Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait["red"]);
            Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait["blue"]);
        }
        else
        {
            Assert.Equal(2, result.State.RunePools["P1"].Mana);
            Assert.True(result.State.CardObjects["RED"].IsExhausted);
            Assert.True(result.State.CardObjects["BLUE"].IsExhausted);
        }
        var recovered = new RecoveredCommand("P1", "batch", command.CmdType, payload, state.Tick, result.State.Tick,
            0, result.Events.Count, true, null);
        var events = result.Events.Select((e, i) => new RecoveredEvent(i + 1, result.State.Tick, i, e)).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state))!, [recovered], result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(false, "exhausted")]
    [InlineData(false, "opponent")]
    [InlineData(true, "missing")]
    [InlineData(true, "opponent")]
    [InlineData(true, "no-trait")]
    [InlineData(false, "duplicate")]
    [InlineData(true, "duplicate")]
    [InlineData(false, "mismatched-first")]
    [InlineData(true, "empty")]
    [InlineData(false, "too-many")]
    [InlineData(true, "wrong-player")]
    public async Task OneIllegalRuneRejectsEntireBatchWithoutChangingState(bool recycle, string invalid)
    {
        var state = Position();
        var ids = new[] { "RED", "BLUE" };
        var first = "RED";
        var player = "P1";
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        switch (invalid)
        {
            case "missing": ids = ["RED", "MISSING"]; break;
            case "exhausted": cards["BLUE"] = cards["BLUE"] with { IsExhausted = true }; break;
            case "opponent": cards["BLUE"] = cards["BLUE"] with { ControllerId = "P2", OwnerId = "P2" }; break;
            case "no-trait": cards["BLUE"] = cards["BLUE"] with { Tags = [CardObjectTags.RuneCard] }; break;
            case "duplicate": ids = ["RED", "RED"]; break;
            case "mismatched-first": first = "BLUE"; break;
            case "empty": ids = []; break;
            case "too-many": ids = Enumerable.Range(0, 13).Select(i => "RUNE" + i).ToArray(); first = ids[0]; break;
            case "wrong-player": player = "P2"; break;
        }
        state = state with { CardObjects = cards };
        GameCommand command = recycle ? new RecycleRuneCommand(first, ids) : new TapRuneCommand(first, ids);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-batch", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"BLUE\"")]
    [InlineData("[\"RED\", 2]")]
    [InlineData("[\"RED\", \"\"]")]
    public async Task MalformedBatchNeverFallsBackToSingleRune(string array)
    {
        var state = Position();
        using var json = JsonDocument.Parse("{\"cmdType\":\"TAP_RUNE\",\"sourceObjectId\":\"RED\",\"sourceObjectIds\":" + array + "}");
        var command = GameCommandJsonMapper.Map(json.RootElement);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-json", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ExhaustedRuneCanStillBeRecycledAndLegacySingleCommandStillWorks()
    {
        var state = Position();
        var engine = new CoreRuleEngine();
        var tapped = await engine.ResolveAsync(state, new("tap", "P1", "TAP_RUNE"), new TapRuneCommand("RED"), default);
        Assert.True(tapped.Accepted);
        var recycled = await engine.ResolveAsync(tapped.State, new("recycle", "P1", "RECYCLE_RUNE"), new RecycleRuneCommand("RED", ["RED", "BLUE"]), default);
        Assert.True(recycled.Accepted, recycled.ErrorMessage);
        Assert.Equal(1, recycled.State.RunePools["P1"].Mana);
        Assert.Equal(2, recycled.State.RunePools["P1"].TotalPower);
    }

    [Fact]
    public async Task SessionRejectsStaleBatchAndDeduplicatesRetry()
    {
        var state = Position();
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var command = new TapRuneCommand("RED", ["RED", "BLUE"]);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var raw = JsonSerializer.SerializeToElement(new { cmdType = "TAP_RUNE", sourceObjectId = "RED",
            sourceObjectIds = new[] { "RED", "BLUE" }, promptId = prompt.PromptId, snapshotTick = state.Tick });
        var first = await session.SubmitAsync("P1", "batch-retry", command, raw, default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var retry = await session.SubmitAsync("P1", "batch-retry", command, raw, default);
        Assert.True(retry.Accepted);
        Assert.Equal(first.State.Tick, retry.State.Tick);
        var stale = await session.SubmitAsync("P1", "stale-batch", command, raw, default);
        Assert.False(stale.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode);
        Assert.Equal(first.State.Tick, session.SnapshotFor("P1").Tick);
    }

    internal static MatchState Position()
    {
        var state = PlayCostPreviewTests.Position(RunePool.Empty);
        return state with
        {
            PlayerZones = state.PlayerZones.ToDictionary(x => x.Key, x => x.Value with { Hand = [], Base = x.Key == "P1" ? ["RED", "BLUE"] : [] }),
            CardObjects = new Dictionary<string, CardObjectState>
            {
                ["RED"] = new("RED", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:red"]),
                ["BLUE"] = new("BLUE", cardNo: "OGN·008/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:blue"])
            }
        };
    }
}
