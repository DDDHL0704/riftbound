namespace Riftbound.Engine;

internal static class GameEventText
{
    internal static string ObjectName(MatchState state, string? objectId, string fallback = "一张牌")
    {
        if (objectId is null)
            return fallback;

        if (state.CardObjects.TryGetValue(objectId, out var card)
            && !card.IsFaceDown
            && state.ObjectLocations.GetValueOrDefault(objectId)?.Zone == "STACK"
            && state.StackItems.Any(item => item.SourceObjectId == objectId && !StackObjectRules.IsAbility(item))
            && ResolutionResult.VisibleObjectIdForViewer(state, objectId, ProjectionViewers.Spectator) != "HIDDEN")
            return CardName(card, fallback);

        return ObjectName(state.PlayerZones, state.CardObjects, objectId, fallback);
    }

    internal static string ObjectName(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string objectId, string fallback)
    {
        if (!cards.TryGetValue(objectId, out var card))
            return fallback;

        // Event descriptions are shared by both players and spectators. Only
        // name sources in public zones at the step that produced the event.
        if (card.IsFaceDown)
            return card.StandbyPlacement is not null ? "背面待命牌" : fallback;

        return zones.Values.Any(zone => zone.Base.Contains(objectId) || zone.Battlefields.Contains(objectId)
            || zone.Graveyard.Contains(objectId) || zone.Banished.Contains(objectId)
            || zone.LegendZone.Contains(objectId) || zone.ChampionZone.Contains(objectId))
            ? CardName(card, fallback) : fallback;
    }

    internal static string StackAction(MatchState state, StackItemState item)
    {
        var skill = !StackObjectRules.IsSpell(item);
        var name = ObjectName(state, item.SourceObjectId, "");
        if (name.Length == 0 || name == "背面待命牌")
            return skill ? "技能" : "法术";
        return skill ? $"{name}的技能" : name;
    }

    internal static string StatusApplied(string source, string effect)
    {
        if (effect.StartsWith(TemporaryCombatKeywordRules.Prefix, StringComparison.Ordinal))
            return $"{source}赋予{effect[TemporaryCombatKeywordRules.Prefix.Length..]}";

        return effect switch
        {
            "STUNNED" => $"{source}使目标眩晕",
            "ROAM" => $"{source}赋予游走",
            DamageDoublingApplications.EffectId => $"{source}使目标本回合受到的伤害翻倍",
            TemporaryUnitEffectRules.EnemyTargetProtection => $"{source}使目标本回合免受敌方法术与技能指定",
            "PREVENT_NEXT_DAMAGE_THIS_TURN" => $"{source}防止目标本回合下一次受到的伤害",
            "RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN" => $"{source}使目标本回合被摧毁时改为休眠召回",
            _ => $"{source}为目标施加状态"
        };
    }

    private static string CardName(CardObjectState card, string fallback)
        => OfficialCardSourceIdentityGroups.KnownDisplayName(card.CardNo)
            ?? (card.TokenFactoryCardNo is { } factory && P6TokenFactoryCatalog.TryGetByCardNo(factory, out var token)
                ? token.TokenFamilyName : fallback);
}
