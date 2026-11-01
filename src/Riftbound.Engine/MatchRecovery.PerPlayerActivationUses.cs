using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static readonly IReadOnlyDictionary<string, P4ActivatedAbilityDefinition> PerPlayerActivationAbilities =
        P4ActivatedAbilityCatalog.GetAll()
            .Where(a => a.SimpleEffect is { OncePerTurn: true, OncePerTurnPerPlayer: true })
            .ToDictionary(a => a.AbilityId, StringComparer.Ordinal);

    private static void ValidatePerPlayerActivationUseHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var abilities = PerPlayerActivationAbilities;
        bool Tracked(GameEvent ev) => ev.Kind == "ABILITY_ACTIVATED"
            && TryReadObjectString(ev.Payload, "abilityId", out var ability) && abilities.ContainsKey(ability!);
        var actual = (state.UntilEndOfTurnEffects ?? []).Where(effect =>
            effect?.StartsWith(CoreRuleEngine.SimpleActivationUsePrefix, StringComparison.Ordinal) == true
                && effect.Contains(":PLAYER:", StringComparison.Ordinal)).ToArray();
        if (actual.Length == 0 && !events.Any(e => Tracked(e.Event))) return;

        var accepted = commands.Where(c => c.Accepted).ToLookup(c => c.CompletedTick);
        var history = events.Where(e => accepted[e.Tick].Any(c =>
                c.StartedEventSequence < e.Sequence && e.Sequence <= c.CompletedEventSequence))
            .OrderBy(e => e.Sequence).ToArray();
        var start = history.Where(e => e.Event.Kind == "TURN_PLAYER_ADVANCED"
                && TryReadObjectInt(e.Event.Payload, "turnNumber", out var turn) && turn == state.TurnNumber)
            .Select(e => e.Sequence).DefaultIfEmpty(0).Max();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in history.Where(e => e.Sequence > start && Tracked(e.Event)))
        {
            var payload = entry.Event.Payload;
            TryReadObjectString(payload, "abilityId", out var abilityId);
            var ability = abilities[abilityId!];
            if (!TryReadObjectString(payload, "playerId", out var player) || !state.Seats.ContainsKey(player!)
                || !TryReadObjectString(payload, "sourceObjectId", out var source) || string.IsNullOrWhiteSpace(source)
                || !TryReadObjectLong(payload, "sourceGeneration", out var generation) || generation < 0
                || !accepted[entry.Tick].Any(c => c.CommandType == CommandTypes.ActivateAbility && c.PlayerId == player
                    && c.StartedEventSequence < entry.Sequence && entry.Sequence <= c.CompletedEventSequence)
                || !expected.Add(CoreRuleEngine.SimpleActivationUse(new ObjectBinding(source!, generation), ability, player!)))
                errors.Add("per-player activation use history has an invalid accepted activation");
        }
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length || !expected.SetEquals(actual))
            errors.Add("per-player activation use history differs from current uses");
    }
}
