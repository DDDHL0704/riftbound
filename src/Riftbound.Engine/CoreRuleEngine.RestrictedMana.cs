using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool CanGainSpellOnlyMana(RunePool pool, long amount)
        => pool.HasSupportedAmounts && amount >= 0 && (long)pool.TotalStoredMana + amount <= int.MaxValue;

    private static void GainLuxSpellOnlyMana(Dictionary<string, RunePool> pools,
        Dictionary<string, CardObjectState> cards, string player, string source,
        List<GameEvent> events, string? paymentId = null)
    {
        var card = cards[source]; var pool = pools.GetValueOrDefault(player, RunePool.Empty);
        if (!CanGainSpellOnlyMana(pool, P4ActivatedAbilityCatalog.LuxGeneratedMana))
            throw new InvalidOperationException("Unrepresentable prevalidated spell-only mana gain.");
        cards[source] = card with { IsExhausted = true };
        pools[player] = pool with { SpellOnlyMana = pool.SpellOnlyMana + P4ActivatedAbilityCatalog.LuxGeneratedMana };
        var payload = new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = source, ["cardNo"] = card.CardNo,
            ["sourceGeneration"] = card.ObjectGeneration,
            ["abilityId"] = P4ActivatedAbilityCatalog.LuxResourceAbilityId,
            ["generatedMana"] = P4ActivatedAbilityCatalog.LuxGeneratedMana,
            ["mana"] = P4ActivatedAbilityCatalog.LuxGeneratedMana, ["manaAfter"] = pool.Mana,
            ["spellOnlyManaAfter"] = pools[player].SpellOnlyMana,
            ["resourceRestriction"] = P4ActivatedAbilityCatalog.LuxSpellOnlyResourceRestriction,
            ["resourceLifecycle"] = "rune-pool", ["paymentOnly"] = false, ["spellOnly"] = true, ["resourceSkill"] = true,
            ["reactionSpeed"] = true, ["stackPolicy"] = "no-ordinary-stack-item",
            ["generatedResourceCannotBeTargetedAsResponse"] = true };
        if (paymentId is not null) { payload["paymentId"] = paymentId; payload["paymentWindow"] = "PLAY_CARD"; }
        events.Add(new("ABILITY_ACTIVATED", "横置拉克丝获得只能打出法术的法力", payload));
        events.Add(new("UNIT_EXHAUSTED", "支付拉克丝资源技能的横置费用", payload));
        events.Add(new("MANA_GAINED", "受限法力加入符文池并保留用途限制", payload));
    }

    private static ResolutionResult ResolveLuxRestrictedMana(MatchState state, PlayerIntent intent, ActivateAbilityCommand command)
    {
        if (!CanActivateReactionResourceSkill(state, intent.PlayerId))
            return RejectWithCorePrompts(state, "当前没有使用反应资源技能的行动权。", ErrorCodes.PhaseNotAllowed);
        if (command.TargetObjectIds.Count != 0 || command.OptionalCosts is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(command.AbilityInstanceId)
            || !CanUseLuxSpellOnlyResourceSource(state, intent.PlayerId, command.SourceObjectId))
            return RejectWithCorePrompts(state, "请选择具有此技能的己方活跃场上拉克丝。", ErrorCodes.InvalidTarget);
        if (!CanGainSpellOnlyMana(state.RunePools.GetValueOrDefault(intent.PlayerId, RunePool.Empty), P4ActivatedAbilityCatalog.LuxGeneratedMana))
            return RejectWithCorePrompts(state, "资源数量超出服务端支持范围，未获取资源或支付费用。", ErrorCodes.UnsupportedCardBehavior);
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        GainLuxSpellOnlyMana(pools, cards, intent.PlayerId, command.SourceObjectId, events);
        var next = state with { Tick = state.Tick + 1, RunePools = pools, CardObjects = cards };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }
}
