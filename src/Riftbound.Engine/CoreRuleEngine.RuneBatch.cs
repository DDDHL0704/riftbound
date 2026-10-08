using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static ResolutionResult ResolveRuneBatch(MatchState state, PlayerIntent intent,
        string firstSource, IReadOnlyList<string> sources, bool recycle)
    {
        if (sources.Count is < 1 or > 12 || sources.Any(string.IsNullOrWhiteSpace)
            || sources.Distinct(StringComparer.Ordinal).Count() != sources.Count
            || (!string.IsNullOrWhiteSpace(firstSource) && firstSource != sources[0]))
            return RejectWithCorePrompts(state, "请选择 1 至 12 枚不同的己方合法符文。", ErrorCodes.InvalidTarget);

        // Reuse each authoritative resource primitive on immutable intermediate states.
        // Nothing is committed unless every rune succeeds; one intent advances one tick.
        var next = state;
        var events = new List<GameEvent>();
        foreach (var source in sources)
        {
            var result = recycle
                ? ResolveRecycleRune(next, intent, new RecycleRuneCommand(source))
                : ResolveTapRune(next, intent, new TapRuneCommand(source));
            if (!result.Accepted)
                return RejectWithCorePrompts(state, result.ErrorMessage ?? "所选符文已失效，请重新选择。", result.ErrorCode ?? ErrorCodes.InvalidTarget);
            next = result.State;
            events.AddRange(result.Events);
        }
        next = next with { Tick = state.Tick + 1 };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }
}
