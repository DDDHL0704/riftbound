using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool IsBorrowedReturnPlay(StackItemState item)
        => item.BorrowedExhaust is { Donor: { } donor } c
            && BorrowedExhaustRules.Spec(donor.CardNo, c.AbilityIndex)?.Kind == "RETURN_UNIT_FREE_PLAY";

    private static bool ValidReplacedReturnPlayTarget(MatchState state, PendingEffectPlayState pending, string targetId)
        => StackObjectRules.ReferencesCurrentObject(state, pending.Parent, targetId)
            && state.CardObjects.TryGetValue(targetId, out var target) && target.IsEmpowered && !target.IsFaceDown
            && target.Tags.Contains(CardObjectTags.UnitCard)
            && IsObjectOnField(state.PlayerZones, targetId)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, pending.PlayerId, targetId)
            && EffectiveFieldControllerId(state.PlayerZones, targetId, target) == pending.PlayerId
            && AttachedRuleTextRules.Instances(state, target).Any(text =>
                EmpoweredTargetReplacements.Value.GetValueOrDefault(text.CardNo, []).Any(spec => spec.ReturnToHand))
            && pending.MaximumUnitPower == ResolveCurrentFieldUnitPower(state, target) - 1;

    private static StackResolutionResult ResolveReturnUnitThenFreePlay(MatchState state, StackItemState item,
        string displayName, string abilityId, Func<string, bool> legalTarget)
    {
        if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var result = NoopStackResolutionResult(state);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", displayName + "技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["abilityId"] = abilityId }) };
        var targetId = item.TargetObjectIds.SingleOrDefault();
        var targetGeneration = targetId is null ? -1 : item.TargetGenerations?.GetValueOrDefault(targetId, -1) ?? -1;
        if (targetId is null
            || targetGeneration < 0
            || !cards.TryGetValue(targetId, out var target)
            || target.ObjectGeneration != targetGeneration
            || !legalTarget(targetId))
        {
            events.Add(new("ABILITY_NO_EFFECT", "易位法师结算时目标已不再合法",
                new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                    ["abilityId"] = abilityId, ["stackItemId"] = item.StackItemId,
                    ["reason"] = "TARGET_NO_LONGER_LEGAL"
                }));
            return result with { Events = events };
        }

        var capturedPower = ResolveCurrentFieldUnitPower(state, target);
        if (TryReplaceEmpoweredTargetEvent(state with { PlayerZones = zones, CardObjects = cards }, item, target,
            "RETURN_TO_HAND", out var returnReplacement, out var returnReplacementEvent))
        {
            cards[targetId] = returnReplacement;
            events.Add(returnReplacementEvent);
            capturedPower = ResolveCurrentFieldUnitPower(state with { PlayerZones = zones, CardObjects = cards }, returnReplacement);
        }
        else
        {
            if (!TryReturnTargetToHand(zones, cards, targetId, out var ownerPlayerId, out var wasEquipment))
            {
                events.Add(new("ABILITY_NO_EFFECT", "易位法师结算时无法返回目标",
                    new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                        ["abilityId"] = abilityId, ["stackItemId"] = item.StackItemId,
                        ["reason"] = "TARGET_RETURN_FAILED"
                    }));
                return result with { Events = events };
            }
            events.Add(BuildReturnedToHandEvent(displayName, item, targetId, ownerPlayerId, wasEquipment));
        }

        var eligible = zones[item.ControllerId].Hand
            .Where(id => cards.TryGetValue(id, out var candidate)
                && candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && CardBehaviorRegistry.TryGetByCardNo(candidate.CardNo ?? "", out var behavior)
                && behavior.PlaysSourceToBaseAsUnit
                && ResolveCurrentFieldUnitPower(state with { PlayerZones = zones, CardObjects = cards }, candidate) < capturedPower)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (eligible.Length == 0)
        {
            events.Add(new("ABILITY_NO_EFFECT", "易位法师没有符合战力限制的手牌单位",
                new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                    ["abilityId"] = abilityId, ["capturedPower"] = capturedPower,
                    ["reason"] = "NO_ELIGIBLE_HAND_UNIT"
                }));
            return result with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
        }

        var pending = new PendingEffectPlayState(
            $"EFFECT-PLAY:{state.Tick + 1}:{item.StackItemId}", item.ControllerId, item, "HAND",
            eligible.ToDictionary(id => id, id => cards[id].ObjectGeneration, StringComparer.Ordinal),
            IgnoreBaseMana: true, IgnoreBasePower: true, ManaReduction: 0,
            DestinationPolicy: "", Optional: true,
            MaximumUnitPower: capturedPower - 1);
        return result with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), PendingEffectPlay = pending,
            Events = events };
    }
}
