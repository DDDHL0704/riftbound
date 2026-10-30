using System.Security.Cryptography;
using System.Text;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string SourceSacrificeRecallKind = "SACRIFICE_RECALL";
    private sealed record SacrificeRecallCost(ObjectBinding Unit, string TurnMarker);

    private static IEnumerable<DestructionOption> SourceSacrificeRecallOptions(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState source, string controller)
    {
        foreach (var rule in AttachedRuleTextRules.Instances(zones, cards, source)) {
            if (!CardReplacementSpecRules.TryGetReplacement(rule.CardNo,
                CardReplacementSpecRules.IsSourceDestroyedSacrificeFriendlyRecallReplacement, out _)) continue;
            var key = $"{SourceSacrificeRecallKind}:{source.ObjectId}:{source.ObjectGeneration}:{rule.CardNo}:{rule.Id}:"
                + (rule.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0);
            // Mark the exact ability instance, without publishing any attached
            // equipment's identity after that equipment changes zones.
            var marker = "REPLACEMENT_ONCE:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            if (source.UntilEndOfTurnEffects.Contains(marker, StringComparer.Ordinal)) continue;
            foreach (var ally in cards.Values.Where(ally => ally.Tags.Contains(CardObjectTags.UnitCard)
                && FieldObjectTypeRules.IsVisibleUnit(ally) && IsObjectOnField(zones, ally.ObjectId)
                && EffectiveFieldControllerId(zones, ally.ObjectId, ally) == controller)
                .OrderBy(ally => ally.ObjectId, StringComparer.Ordinal)) {
                // CN 355.10.c: a replacement's sacrifice is a reference, not a
                // skill target. Protection and ward do not restrict this cost.
                var id = $"{SourceSacrificeRecallKind}:{source.ObjectId}:{ally.ObjectId}"
                    + (rule.Origin is null ? "" : ":" + rule.Id);
                yield return new(new(id, $"摧毁友方单位 {ally.ObjectId}，移除 {source.ObjectId} 的伤害并休眠召回（此技能每回合限一次）",
                    new[] { source.ObjectId, ally.ObjectId }.Distinct(StringComparer.Ordinal).ToArray()),
                    source.ObjectId, source.ObjectId, SourceSacrificeRecallKind, key, rule.CardNo, Optional: true,
                    SacrificeCost: new(new(ally.ObjectId, ally.ObjectGeneration), marker));
            }
        }
    }

    private static void ApplySourceSacrificeRecallCost(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string controller,
        DestructionOption option, StackItemState stack, List<GameEvent> events)
    {
        var cost = option.SacrificeCost ?? throw new InvalidOperationException("Missing destruction replacement cost.");
        if (!cards.TryGetValue(cost.Unit.ObjectId, out var ally) || ally.ObjectGeneration != cost.Unit.Generation
            || !ally.Tags.Contains(CardObjectTags.UnitCard) || !FieldObjectTypeRules.IsVisibleUnit(ally)
            || !IsObjectOnField(zones, ally.ObjectId) || EffectiveFieldControllerId(zones, ally.ObjectId, ally) != controller)
            throw new InvalidOperationException("The replacement's friendly sacrifice is no longer available.");
        var source = cards[option.Target];
        cards[option.Target] = source with { UntilEndOfTurnEffects = source.UntilEndOfTurnEffects
            .Append(cost.TurnMarker).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
        var nested = ResolveFieldDestructions(state, zones, cards, stack, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), pools,
            objectLocations: ReconcileObjectLocations(state.ObjectLocations, zones),
            explicitDestroyObjectIds: new HashSet<string> { ally.ObjectId }, explicitDestroyReason: "REQUIRED_COST");
        events.AddRange(nested.Events);
        foreach (var pool in nested.RunePools) pools[pool.Key] = pool.Value;
        RuleChoices.Value!.NestedTriggers.AddRange(nested.TriggerQueue);
        RuleChoices.Value.NestedDestroyedOwners.UnionWith(nested.DestroyedUnitOwnerIds);
        // CN 203.2: a replacement of the sacrifice still pays its cost. If the
        // sacrifice removes this source incarnation, the caller cannot recall it.
        events.Add(new("COST_PAID", "支付摧毁替换的友方单位费用", new Dictionary<string, object?> {
            ["playerId"] = controller, ["sourceObjectId"] = option.Source,
            ["sacrificedObjectId"] = ally.ObjectId,
            ["reason"] = ReplacementKinds.SourceUnitDestroyedDestroyFriendlyRecallExhaustedOncePerTurn }));
    }
}
