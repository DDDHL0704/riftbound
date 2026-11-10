using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string PowerSpentEvent = "RUNE_POWER_SPENT";
    internal const string PowerPaymentReceiptKey = "powerPaymentReceipt";

    internal static bool RequiresMightyPowerPaymentHistory(MightySkillContext? context)
        => context is { Before.PaidPowerThisTurn: { } before, After.PaidPowerThisTurn: { } after }
            && before.Values.Concat(after.Values).Any(value => value != 0);

    internal static string MightyPowerPaymentHistoryHash(MightySkillContext context)
        => MatchStateHasher.HashValue(new { Before = context.Before.PaidPowerThisTurn, After = context.After.PaidPowerThisTurn });

    private static ResolutionResult RecordPowerPayments(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        // Series completion may already have created the next game's empty
        // state. Keep the final payment with its actual terminal game.
        var paidState = result.State.LastCompletedSeriesGame is { } terminal
            && terminal.GameNumber != before.LastCompletedSeriesGame?.GameNumber
                ? terminal.State : result.State;
        var events = result.Events.ToList();
        foreach (var player in paidState.Seats.Keys)
        {
            var total = paidState.RunePools.GetValueOrDefault(player, RunePool.Empty).PaidPowerThisTurn;
            var previous = before.TurnNumber == paidState.TurnNumber
                ? before.RunePools.GetValueOrDefault(player, RunePool.Empty).PaidPowerThisTurn : 0;
            if (total < previous) throw new ResourceNumericLimitException("符能支付记录回退");
            if (total == previous) continue;
            var receipt = new Dictionary<string, object?> {
                ["playerId"] = player, ["turnNumber"] = paidState.TurnNumber,
                ["amount"] = total - previous, ["paidPowerTotal"] = total };
            // Enrich only this newly accepted result. Ordinary fees already
            // have a cost receipt; resource conversions have their own result.
            // Keep those public event sequences and descriptions intact.
            var index = events.FindLastIndex(item => item.Kind == "COST_PAID"
                && Equals(item.Payload.GetValueOrDefault("playerId"), player));
            if (index < 0) index = events.FindLastIndex(item => item.Kind == "RESOURCE_SKILL_RESOLVED"
                && Equals(item.Payload.GetValueOrDefault("playerId"), player));
            if (index >= 0)
            {
                var payload = new Dictionary<string, object?>(events[index].Payload) { [PowerPaymentReceiptKey] = receipt };
                events[index] = events[index] with { Payload = payload };
            }
            else
            {
                var boundary = events.FindIndex(item => item.Kind == "SERIES_GAME_COMPLETED");
                events.Insert(boundary < 0 ? events.Count : boundary,
                    new(PowerSpentEvent, "本回合实际支付符能", new Dictionary<string, object?> {
                        ["playerId"] = player, [PowerPaymentReceiptKey] = receipt }));
            }
        }
        return result with { Events = events };
    }
}
