using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 430.2: gain-resource skills confirm and resolve immediately without
    // changing focus. The first role event identifies the start of this battle;
    // gaining a role later or resuming combat never repeats its start effects.
    private static ResolutionResult ResolveBattlefieldStartResourceTriggers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = result.State;
        var events = result.Events.ToList();
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var started in result.Events.Where(e => e.Kind == "BATTLE_ROLES_ESTABLISHED"
            || e.Kind == "BATTLE_DECLARED" && !IsInitialBattleDuel(before, out _)
                && !TryReadBattleResponseDeclarationContext(before, string.Empty, out _)))
        {
            var field = started.Payload.GetValueOrDefault("battlefieldId") as string;
            if (BattlefieldLocalRules.Battlefield(state, field) is not { } source
                || !BattlefieldTriggerSpecRules.TryGetTrigger(source.CardNo,
                    BattlefieldTriggerSpecRules.IsBattlefieldBattleStartManaTrigger, out var spec)) continue;
            var id = $"battle-start-resource-{state.Tick}-{field}";
            if (events.Any(e => e.Kind == "TRIGGER_RESOLVED" && Equals(e.Payload.GetValueOrDefault("triggerId"), id))) continue;
            var attacker = started.Payload.GetValueOrDefault("attackingPlayerId") as string
                ?? started.Payload.GetValueOrDefault("playerId") as string;
            if (attacker is null || !state.Seats.ContainsKey(attacker)) continue;
            var defender = NextPlayerIdAfter(state, attacker);
            var trigger = new TriggerQueueItemState(id, source.ControllerId ?? state.TurnPlayerId, source.ObjectId,
                spec.Kind, started.Kind) { SourceCardNo = source.CardNo };
            events.Add(BuildTriggerQueuedEvent(trigger));
            foreach (var player in new[] { attacker, defender })
            {
                var pool = pools.GetValueOrDefault(player, RunePool.Empty); var mana = spec.ManaDelta!.Value;
                if (!CanGainSpellOnlyMana(pool, mana))
                    return RejectWithCorePrompts(before, "战斗开始技能获得的法力超出支持范围。", ErrorCodes.UnsupportedCardBehavior);
                pools[player] = pool with { Mana = pool.Mana + mana };
                events.Add(new("MANA_GAINED", "战斗开始，攻防双方获得法力", new Dictionary<string, object?> {
                    ["playerId"] = player, ["sourceObjectId"] = source.ObjectId, ["cardNo"] = source.CardNo,
                    ["triggerId"] = id, ["generatedMana"] = mana, ["mana"] = mana,
                    ["manaAfter"] = pools[player].Mana, ["resourceLifecycle"] = "rune-pool", ["resourceSkill"] = true }));
            }
            events.Add(BuildTriggerResolvedEvent(trigger));
        }
        state = state with { RunePools = pools };
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
