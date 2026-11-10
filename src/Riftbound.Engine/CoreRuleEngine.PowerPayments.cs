using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string PowerSpentEvent = "RUNE_POWER_SPENT";

    private static ResolutionResult RecordPowerPayments(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        // Series completion may already have created the next game's empty
        // state. Keep the final payment with its actual terminal game.
        var paidState = result.State.LastCompletedSeriesGame is { } terminal
            && terminal.GameNumber != before.LastCompletedSeriesGame?.GameNumber
                ? terminal.State : result.State;
        var paidEvents = new List<GameEvent>();
        foreach (var player in paidState.Seats.Keys)
        {
            var total = paidState.RunePools.GetValueOrDefault(player, RunePool.Empty).PaidPowerThisTurn;
            var previous = before.TurnNumber == paidState.TurnNumber
                ? before.RunePools.GetValueOrDefault(player, RunePool.Empty).PaidPowerThisTurn : 0;
            if (total < previous) throw new ResourceNumericLimitException("符能支付记录回退");
            if (total == previous) continue;
            paidEvents.Add(new(PowerSpentEvent, "本回合实际支付符能", new Dictionary<string, object?> {
                ["playerId"] = player, ["turnNumber"] = paidState.TurnNumber,
                ["amount"] = total - previous, ["paidPowerTotal"] = total }));
        }
        if (paidEvents.Count == 0) return result;
        var events = result.Events.ToList();
        var boundary = events.FindIndex(item => item.Kind == "SERIES_GAME_COMPLETED");
        events.InsertRange(boundary < 0 ? events.Count : boundary, paidEvents);
        return result with { Events = events };
    }
}
