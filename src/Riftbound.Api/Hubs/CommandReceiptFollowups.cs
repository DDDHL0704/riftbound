using Riftbound.Contracts;

namespace Riftbound.Api.Hubs;

internal static class CommandReceiptFollowups
{
    public static CommandReceiptFollowupDto Create(
        bool accepted,
        long serverTick,
        int eventCount,
        int snapshotCount,
        int promptCount,
        string receiptState,
        IReadOnlyList<string>? eventKinds = null,
        IReadOnlyList<CommandReceiptEventRefDto>? eventRefs = null)
    {
        var state = accepted
            ? eventCount > 0
                ? "events"
                : snapshotCount > 0 || promptCount > 0
                    ? "snapshot-prompt"
                    : "silent"
            : string.Equals(receiptState, "REJECTED", StringComparison.Ordinal)
                ? "rejected"
                : "failed";

        return new CommandReceiptFollowupDto(
            serverTick,
            eventCount,
            snapshotCount,
            promptCount,
            state,
            Summary(state, serverTick, eventCount, snapshotCount, promptCount),
            eventKinds is { Count: > 0 } ? eventKinds : null,
            eventRefs is { Count: > 0 } ? eventRefs : null);
    }

    public static IReadOnlyList<CommandReceiptEventRefDto>? EventRefs(
        IReadOnlyList<GameEvent> events,
        long serverTick)
    {
        if (events.Count == 0)
        {
            return null;
        }

        var refs = new CommandReceiptEventRefDto[events.Count];
        for (var index = 0; index < events.Count; index++)
        {
            refs[index] = new CommandReceiptEventRefDto(serverTick, index, events[index].Kind);
        }

        return refs;
    }

    private static string Summary(
        string state,
        long serverTick,
        int eventCount,
        int snapshotCount,
        int promptCount)
    {
        return state switch
        {
            "events" => $"tick {serverTick} 已生成 {eventCount} 条公开事件、{snapshotCount} 个快照、{promptCount} 个提示。",
            "snapshot-prompt" => $"tick {serverTick} 无公开事件，但已生成 {snapshotCount} 个快照、{promptCount} 个提示。",
            "silent" => $"tick {serverTick} 命令已接受，未生成公开事件或广播视图。",
            "rejected" => $"tick {serverTick} 命令被服务端规则拒绝，未广播事件或快照。",
            _ => "命令未进入服务端规则结算，未广播事件或快照。"
        };
    }
}
