using Riftbound.Api.Hubs;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CommandReceiptFollowupTests
{
    [Theory]
    [InlineData(true, 2, 1, 1, "ACCEPTED", "events", "已生成 2 条公开事件")]
    [InlineData(true, 0, 1, 0, "ACCEPTED", "snapshot-prompt", "无公开事件，但已生成 1 个快照、0 个提示")]
    [InlineData(true, 0, 0, 1, "ACCEPTED", "snapshot-prompt", "无公开事件，但已生成 0 个快照、1 个提示")]
    [InlineData(true, 0, 0, 0, "ACCEPTED", "silent", "命令已接受，未生成公开事件或广播视图")]
    [InlineData(false, 0, 0, 0, "REJECTED", "rejected", "命令被服务端规则拒绝")]
    [InlineData(false, 0, 0, 0, "FAILED", "failed", "命令未进入服务端规则结算")]
    public void CreateMapsReceiptCountsToStableFollowupState(
        bool accepted,
        int eventCount,
        int snapshotCount,
        int promptCount,
        string receiptState,
        string expectedState,
        string expectedSummaryFragment)
    {
        var followup = CommandReceiptFollowups.Create(
            accepted,
            serverTick: 42,
            eventCount,
            snapshotCount,
            promptCount,
            receiptState);

        Assert.Equal(expectedState, followup.State);
        Assert.Equal(42, followup.ServerTick);
        Assert.Equal(eventCount, followup.EventCount);
        Assert.Equal(snapshotCount, followup.SnapshotCount);
        Assert.Equal(promptCount, followup.PromptCount);
        Assert.Contains(expectedSummaryFragment, followup.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateKeepsEventKindsAndRefsOnlyWhenEventsExist()
    {
        var eventRefs = new[]
        {
            new CommandReceiptEventRefDto(9, 0, "STACK_ITEM_ADDED"),
            new CommandReceiptEventRefDto(9, 1, "BATTLEFIELD_CONTROL_RESOLVED")
        };

        var followup = CommandReceiptFollowups.Create(
            accepted: true,
            serverTick: 9,
            eventCount: 2,
            snapshotCount: 1,
            promptCount: 1,
            receiptState: "ACCEPTED",
            eventKinds: new[] { "STACK_ITEM_ADDED", "BATTLEFIELD_CONTROL_RESOLVED" },
            eventRefs);

        Assert.Equal("events", followup.State);
        Assert.Equal(new[] { "STACK_ITEM_ADDED", "BATTLEFIELD_CONTROL_RESOLVED" }, followup.EventKinds);
        Assert.Equal(eventRefs, followup.EventRefs);

        var silentFollowup = CommandReceiptFollowups.Create(
            accepted: true,
            serverTick: 10,
            eventCount: 0,
            snapshotCount: 0,
            promptCount: 0,
            receiptState: "ACCEPTED",
            eventKinds: Array.Empty<string>(),
            eventRefs: Array.Empty<CommandReceiptEventRefDto>());

        Assert.Equal("silent", silentFollowup.State);
        Assert.Null(silentFollowup.EventKinds);
        Assert.Null(silentFollowup.EventRefs);
    }

    [Fact]
    public void EventRefsUseServerTickAndEventOrder()
    {
        var refs = CommandReceiptFollowups.EventRefs(
            new[]
            {
                new GameEvent("STACK_ITEM_ADDED", "stack item", new Dictionary<string, object?>()),
                new GameEvent("BATTLEFIELD_CONTROL_RESOLVED", "control", new Dictionary<string, object?>())
            },
            serverTick: 12);

        Assert.NotNull(refs);
        Assert.Equal(
            new[]
            {
                new CommandReceiptEventRefDto(12, 0, "STACK_ITEM_ADDED"),
                new CommandReceiptEventRefDto(12, 1, "BATTLEFIELD_CONTROL_RESOLVED")
            },
            refs);
        Assert.Null(CommandReceiptFollowups.EventRefs(Array.Empty<GameEvent>(), serverTick: 12));
    }
}
