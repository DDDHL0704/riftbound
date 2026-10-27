namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateResourcePoolViews(RecoveredPlayerView view, MatchState state, List<string> errors, MatchState paymentPromptState)
    {
        ValidatePaymentResourceCapacityViews(view, paymentPromptState, errors);
        if (view.Snapshot.Players is null) return;
        foreach (var (player, payload) in view.Snapshot.Players)
        {
            var expected = state.RunePools.GetValueOrDefault(player, RunePool.Empty);
            if (!TryReadObjectValue(payload, "runePool", out var pool))
            {
                if (expected.SpellOnlyMana != 0) errors.Add($"snapshot for {view.PlayerId} player {player} is missing its restricted resource pool");
                continue;
            }
            var label = $"snapshot for {view.PlayerId} player {player} rune pool";
            if (!TryReadObjectInt(pool, "mana", out var mana) || mana != expected.Mana)
                errors.Add(label + " ordinary mana does not match authoritative state");
            var hasRestricted = TryReadObjectInt(pool, "spellOnlyMana", out var restricted);
            if (hasRestricted ? restricted != expected.SpellOnlyMana : expected.SpellOnlyMana != 0)
                errors.Add(label + " spell-only mana does not match authoritative state");
            if (!TryReadObjectInt(pool, "power", out var power) || power != expected.TotalPower)
                errors.Add(label + " total power does not match authoritative state");
            if (!TryReadObjectInt(pool, "untypedPower", out var untyped) || untyped != expected.Power)
                errors.Add(label + " untyped power does not match authoritative state");
            if (!TryReadObjectIntDictionary(pool, "powerByTrait", out var typed) || !IntDictionariesEqual(typed, expected.PowerByTrait))
                errors.Add(label + " typed power does not match authoritative state");
        }
    }
}
