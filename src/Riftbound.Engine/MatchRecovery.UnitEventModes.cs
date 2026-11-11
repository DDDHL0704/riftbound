using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateUnitEventModeUseHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        ValidateOptionalUnitEventModeHistory(state, commands, events, errors);
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

    private static void ValidateOptionalUnitEventModeHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        IEnumerable<StackItemState> Captured(MatchState value) => value.StackItems.Concat(
            value.PendingEffectPlay?.Parent is { } parent ? [parent] : []);
        var items = Captured(state).Concat(state.PendingRuleChoice is { } pending ? Captured(pending.Origin) : []);
        foreach (var item in items.Where(item => item.UnitEvent is { } context
            && CoreRuleEngine.UnitEventSpecForContext(context) is { Optional: true, Modes.Count: > 0 }))
        {
            var hash = MatchStateHasher.HashValue(item.StackItemId);
            var confirmed = events.Where(entry => entry.Event.Kind == "TRIGGER_MODE_CONFIRMED"
                && TryReadObjectString(entry.Event.Payload, "optionalModeSkillHash", out var recorded)
                && recorded == hash).ToArray();
            var mode = item.UnitEvent!.Mode;
            if (mode is null && confirmed.Length == 0) continue;
            if (mode is null || confirmed.Length != 1
                || !TryReadObjectString(confirmed[0].Event.Payload, "mode", out var selected) || selected != mode
                || !TryReadObjectString(confirmed[0].Event.Payload, "playerId", out var player) || player is null
                || !HasAcceptedBattlefieldChoice(commands, confirmed[0], player,
                    "UNIT-EVENT-MODE:" + item.StackItemId, CoreRuleEngine.UnitEventModeWindow, mode))
                errors.Add("optional unit event mode differs from its accepted confirmation history");
        }
    }

}
