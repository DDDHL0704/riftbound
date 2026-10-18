using System.Text.Json;

namespace Riftbound.Engine;

internal static class TriggerQueueRecoveryRules
{
    // Validate recorded facts independently of the producer and ordering builder.
    // No absent timestamp is reconstructed when loading an old development game.
    internal static bool Valid(MatchState state)
    {
        var ticks = state.TriggerQueue.Where(t => t.EnqueuedAtTick is not null)
            .Select(t => t.EnqueuedAtTick!.Value).ToArray();
        return ticks.All(t => t > 0 && t <= state.Tick)
            && ticks.SequenceEqual(ticks.Order());
    }

    internal static bool ValidControllerOrderConfirmations(MatchState state)
    {
        if (!state.TriggerQueue.Any(t => t.ControllerOrderConfirmed)) return true;
        var choices = state.TriggerQueue.GroupBy(t => (t.EnqueuedAtTick, t.ControllerId))
            .Where(group => group.Count() > 1).Select(group => group.Key.ControllerId).Distinct(StringComparer.Ordinal).ToHashSet();
        var confirmed = state.TriggerQueue.Where(t => t.ControllerOrderConfirmed)
            .Select(t => t.ControllerId).Distinct(StringComparer.Ordinal).ToHashSet();
        if (!confirmed.IsSubsetOf(choices) || confirmed.Count >= choices.Count
            || state.TriggerQueue.Any(t => !state.Seats.ContainsKey(t.ControllerId) || t.EnqueuedAtTick >= state.Tick
                || t.ControllerOrderConfirmed != confirmed.Contains(t.ControllerId))) return false;

        var first = state.TurnPlayerId;
        if (state.TriggerQueue.Any(t => t.TriggeredByEventKind == "BATTLE_ROLES_ESTABLISHED"))
        {
            const string prefix = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
            var markers = state.UntilEndOfTurnEffects.Where(s => s.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (markers.Length != 1) return false;
            try
            {
                using var document = JsonDocument.Parse(Convert.FromBase64String(markers[0][prefix.Length..]));
                if (document.RootElement.TryGetProperty("InitialAttackingPlayerId", out var player)
                    && player.ValueKind == JsonValueKind.String) first = player.GetString()!;
            }
            catch (Exception error) when (error is FormatException or JsonException) { return false; }
        }
        var apnap = new[] { first }.Concat(state.Seats.OrderBy(p => p.Value, StringComparer.Ordinal).Select(p => p.Key))
            .Distinct(StringComparer.Ordinal).Where(choices.Contains).ToArray();
        return confirmed.SetEquals(apnap.Take(confirmed.Count));
    }
}
