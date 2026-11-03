using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateUnitEventModeUseHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var actual = (state.RuleTextModeUses?.Uses ?? []).Where(u => u is not null
            && CardBehaviorRegistry.TryGetByCardNo(u.CardNo, out var b) && b.UnitEventAbility?.DistinctModesPerTurn == true).ToArray();
        if (actual.Length == 0 && !events.Any(e => e.Event.Kind == "UNIT_EVENT_MODE_USED")) return;
        var accepted = commands.Where(c => c.Accepted).ToLookup(c => c.CompletedTick);
        var history = events.Where(e => accepted[e.Tick].Any(c => c.StartedEventSequence < e.Sequence
            && e.Sequence <= c.CompletedEventSequence)).ToArray();
        var start = history.Where(e => e.Event.Kind == "TURN_PLAYER_ADVANCED"
                && TryReadObjectInt(e.Event.Payload, "turnNumber", out var turn) && turn == state.TurnNumber)
            .Select(e => e.Sequence).DefaultIfEmpty(0).Max();
        var expected = new HashSet<RuleTextModeUse>();
        foreach (var entry in history.Where(e => e.Sequence > start && e.Event.Kind == "UNIT_EVENT_MODE_USED"))
        {
            if (!TryReadLocation<RuleTextModeUse>(entry.Event.Payload.GetValueOrDefault("modeUse"), out var use)
                || use is null || !TryReadObjectString(entry.Event.Payload, "playerId", out var player)
                || !accepted[entry.Tick].Any(c => c.CommandType == CommandTypes.ChooseCards && c.PlayerId == player
                    && c.StartedEventSequence < entry.Sequence && entry.Sequence <= c.CompletedEventSequence)
                || !expected.Add(use)) errors.Add("invalid accepted unit event mode-use history");
        }
        if (actual.Distinct().Count() != actual.Length || !expected.SetEquals(actual))
            errors.Add("unit event mode-use ledger differs from accepted confirmation history");
    }
}
