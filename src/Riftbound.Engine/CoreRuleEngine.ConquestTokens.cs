using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record ConquestTokenContext(string CardNo, long SourceGeneration, string BattlefieldId,
    long CapturedTick, long FaceRevision, string? TokenFactoryCardNo = null, RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    private static TriggerSpec? ConquestTokenSpec(string? cardNo)
        => UnitConquestTriggerSpecRules.TryGetTrigger(cardNo,
            t => (t.Kind is TriggerKinds.UnitConquestCreateDormantGold or TriggerKinds.UnitConquestCreateUnitToken)
                && UnitConquestTriggerSpecRules.IsSupportedUnitConquestTrigger(t)
                && (t.Kind == TriggerKinds.UnitConquestCreateDormantGold || ConquestUnitTokenFactory(t) is not null), out var spec) ? spec : null;

    private static P6TokenFactoryDefinition? ConquestUnitTokenFactory(TriggerSpec spec)
        => P6TokenFactoryCatalog.GetAll().FirstOrDefault(f => f.TokenFamilyName == spec.CreatedTokenName
            && f.DefaultPower == spec.CreatedTokenPower && !f.RequiresCopySource
            && f.Tags.Contains(CardObjectTags.UnitCard) && (spec.CreatedTokenKeywords ?? []).All(f.Tags.Contains));

    internal static bool ValidConquestToken(MatchState state, ConquestTokenContext context, string sourceId,
        string effect, string? cardNo = null)
        => ConquestTokenSpec(context.CardNo) is { } spec && effect == spec.Kind && context.SourceGeneration >= 0
            && !string.IsNullOrWhiteSpace(context.BattlefieldId)
            && context.CapturedTick > 0 && context.CapturedTick <= state.Tick && context.FaceRevision >= 0
            && (cardNo is null || cardNo == context.CardNo)
            && state.BattlefieldStates.ContainsKey(context.BattlefieldId)
            && (context.Origin is { } origin
                ? AttachedRuleTextRules.ValidCaptured(state, origin, sourceId, context.SourceGeneration, context.CardNo)
                : PredictBindingMatches(state, new(sourceId, context.SourceGeneration), context.CardNo,
                    context.FaceRevision, context.CapturedTick, context.TokenFactoryCardNo));

    private static IEnumerable<TriggerQueueItemState> CaptureConquestTokens(MatchState state, string player,
        string battlefield, IReadOnlyList<string> units, long tick)
    {
        foreach (var id in units.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || source.ControllerId != player
                || source.IsFaceDown || !source.Tags.Contains(CardObjectTags.UnitCard)
                || BattlefieldLocalRules.AtUnit(state, id)?.ObjectId != battlefield) continue;
            var repeats = ResolveUnitConquestEffectRepeatCount(state.PlayerZones, state.CardObjects,
                state.ObjectLocations, player, battlefield, "BATTLEFIELD_CONQUERED");
            var instances = AttachedRuleTextRules.Instances(state, source).ToArray();
            for (var ordinal = 0; ordinal < instances.Length; ordinal++)
            {
                var instance = instances[ordinal];
                if (ConquestTokenSpec(instance.CardNo) is not { } spec) continue;
                for (var i = 0; i < repeats; i++)
                    yield return new TriggerQueueItemState($"conquest-token-{tick}-{battlefield}-{id}-{i}"
                        + (ordinal == 0 ? "" : $"-instance-{ordinal}"), player, id, spec.Kind, "BATTLEFIELD_CONQUERED") {
                        ConquestToken = new(instance.CardNo, source.ObjectGeneration, battlefield, tick,
                            source.CopyFaceTransitions?.Count ?? 0, source.TokenFactoryCardNo, instance.Origin) };
            }
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
        if (spec.Kind == TriggerKinds.UnitConquestCreateUnitToken)
        {
            var factory = ConquestUnitTokenFactory(spec)!;
            var result = CreateUnitTokenBatch(state, item, factory.CardNo, spec.CreatedTokenCount!.Value,
                entersReady: spec.CreatedTokenExhausted != true);
            return result with { Events = events.Concat(result.Events).ToArray() };
        }
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
