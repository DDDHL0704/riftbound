using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record ConquestTokenContext(string CardNo, long SourceGeneration, string BattlefieldId);

public sealed partial class CoreRuleEngine
{
    private static TriggerSpec? ConquestTokenSpec(string? cardNo)
        => UnitConquestTriggerSpecRules.TryGetTrigger(cardNo,
            t => t.Kind == TriggerKinds.UnitConquestCreateDormantGold
                && UnitConquestTriggerSpecRules.IsSupportedUnitConquestTrigger(t), out var spec) ? spec : null;

    internal static bool ValidConquestToken(MatchState state, ConquestTokenContext context, string sourceId,
        string effect, string? cardNo = null)
        => effect == TriggerKinds.UnitConquestCreateDormantGold && context.SourceGeneration >= 0
            && !string.IsNullOrWhiteSpace(context.BattlefieldId) && ConquestTokenSpec(context.CardNo) is not null
            && (cardNo is null || cardNo == context.CardNo)
            && state.BattlefieldStates.ContainsKey(context.BattlefieldId)
            && state.CardObjects.TryGetValue(sourceId, out var source) && source.ObjectGeneration >= context.SourceGeneration
            && (source.ObjectGeneration > context.SourceGeneration || source.CardNo == context.CardNo);

    private static IEnumerable<TriggerQueueItemState> CaptureConquestTokens(MatchState state, string player,
        string battlefield, IReadOnlyList<string> units, long tick)
    {
        foreach (var id in units.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || source.ControllerId != player
                || source.IsFaceDown || !source.Tags.Contains(CardObjectTags.UnitCard)
                || BattlefieldLocalRules.AtUnit(state, id)?.ObjectId != battlefield
                || ConquestTokenSpec(source.CardNo) is not { } spec) continue;
            var repeats = ResolveUnitConquestEffectRepeatCount(state.PlayerZones, state.CardObjects,
                state.ObjectLocations, player, battlefield, "BATTLEFIELD_CONQUERED");
            for (var i = 0; i < repeats; i++)
                yield return new TriggerQueueItemState($"conquest-token-{tick}-{battlefield}-{id}-{i}", player, id,
                    spec.Kind, "BATTLEFIELD_CONQUERED") {
                    ConquestToken = new(source.CardNo!, source.ObjectGeneration, battlefield) };
        }
    }

    private static StackResolutionResult ResolveConquestToken(MatchState state, StackItemState item)
    {
        var context = item.ConquestToken!;
        var spec = ConquestTokenSpec(context.CardNo)!;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        AddUnitConquestEffectActivatedEvent(events, item.ControllerId, item.SourceObjectId, context.CardNo,
            spec.Kind, context.BattlefieldId, "BATTLEFIELD_CONQUERED");
        var name = spec.CreatedTokenName ?? "金币";
        var tags = new[] { CardObjectTags.EquipmentCard, name }.Concat(spec.CreatedTokenKeywords ?? [])
            .Distinct(StringComparer.Ordinal).ToArray();
        for (var i = 0; i < spec.CreatedTokenCount.GetValueOrDefault(1); i++)
            CreateLegendEquipmentToken(zones, cards, item.ControllerId, item.SourceObjectId, spec.Kind,
                name, tags, spec.CreatedTokenExhausted.GetValueOrDefault(true), events, context.CardNo);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
