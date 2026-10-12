using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record LegendAttachmentContext(string AbilityId, long SourceGeneration,
    string? BattlefieldObjectId = null, string? BattlefieldCardNo = null);

public sealed partial class CoreRuleEngine
{
    private static readonly Lazy<HashSet<string>> OfficialLegendCardNos = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Where(c => c.CardCategoryList.Contains("legendary", StringComparer.Ordinal))
            .Select(c => c.CardNo).ToHashSet(StringComparer.Ordinal));

    internal static bool IsLegendAttachmentEffect(string effect)
        => effect is LegendAbilityEffectKinds.AttachArmament or LegendAbilityEffectKinds.ReattachArmament;

    internal static bool ValidLegendAttachment(MatchState state, StackItemState item)
    {
        if (item.LegendAttachment is not { } context) return !IsLegendAttachmentEffect(item.EffectKind);
        if (!IsLegendAttachmentEffect(item.EffectKind) || !TryGetLegendAbility(context.AbilityId, out var ability)
            || ability.EffectKind != item.EffectKind || context.SourceGeneration < 0
            || item.SourceConfirmed || item.EffectRepeatCount != 1 || item.RepeatExecutions is not null
            || string.IsNullOrWhiteSpace(item.SourceObjectId) || !state.Seats.ContainsKey(item.ControllerId)
            || item.TargetObjectIds.Count != 2 || item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() != 2
            || item.TargetObjectIds.Contains(item.SourceObjectId, StringComparer.Ordinal)
            || item.TargetGenerations is not { Count: 2 } bindings
            || item.TargetObjectIds.Any(id => !bindings.TryGetValue(id, out var generation) || generation < 0
                || state.CardObjects.TryGetValue(id, out var card) && generation > card.ObjectGeneration)
            || state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                && (context.SourceGeneration > source.ObjectGeneration
                    || context.SourceGeneration == source.ObjectGeneration && source.CardNo != item.CardNo))
            return false;
        var costs = ability.ManaCost > 0 ? new[] { $"SPEND_MANA:{ability.ManaCost}" } : [];
        if (!item.OptionalCosts.SequenceEqual(costs, StringComparer.Ordinal)) return false;
        if (string.IsNullOrWhiteSpace(ability.RequiredControlledBattlefieldStaticAbilityKind))
            return context.BattlefieldObjectId is null && context.BattlefieldCardNo is null
                && LegendActionAbilityCatalog.IsSourceCardNoForAbility(context.AbilityId, item.CardNo);
        return OfficialLegendCardNos.Value.Contains(item.CardNo ?? string.Empty)
            && context.BattlefieldObjectId is { Length: > 0 } grantId
            && grantId != item.SourceObjectId && !item.TargetObjectIds.Contains(grantId, StringComparer.Ordinal)
            && state.CardObjects.TryGetValue(grantId, out var grant) && grant.CardNo == context.BattlefieldCardNo
            && BattlefieldStaticAbilitySpecRules.TryGetAbility(context.BattlefieldCardNo,
                a => a.Kind == ability.RequiredControlledBattlefieldStaticAbilityKind, out _);
    }

    private static ResolutionResult QueueLegendAttachment(MatchState state, string player, string sourceId,
        string cardNo, LegendAbilityDefinition ability, IReadOnlyList<string> targets,
        IReadOnlyList<string> optionalCosts, List<GameEvent> events)
    {
        string? grantId = null, grantCardNo = null;
        if (!string.IsNullOrWhiteSpace(ability.RequiredControlledBattlefieldStaticAbilityKind))
        {
            if (!TryGetControlledBattlefieldStaticAbilityObject(state, player,
                ability.RequiredControlledBattlefieldStaticAbilityKind, out var id, out var grant))
                throw new InvalidOperationException("A granted ability must have an authorized granting source at activation.");
            grantId = id; grantCardNo = grant.CardNo;
        }
        var item = new StackItemState($"STACK-{state.Tick + 1}-{sourceId}-ATTACH", player, sourceId,
            ability.EffectKind, cardNo, targets, optionalCosts: optionalCosts) {
            LegendAttachment = new(ability.AbilityId, state.CardObjects[sourceId].ObjectGeneration, grantId, grantCardNo),
            TargetGenerations = targets.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal)
        };
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = player, TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = player, PassedPriorityPlayerIds = [], StackItems = [..state.StackItems, item] };
        events.Add(new("STACK_ITEM_ADDED", $"{ability.DisplayName}加入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["controllerId"] = player, ["sourceObjectId"] = sourceId,
            ["cardNo"] = cardNo, ["effectKind"] = ability.EffectKind, ["abilityId"] = ability.AbilityId,
            ["targetObjectIds"] = targets.ToArray() }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveLegendAttachment(MatchState state, StackItemState item)
    {
        if (!ValidLegendAttachment(state, item)) return NoopStackResolutionResult(state);
        var context = item.LegendAttachment!;
        TryGetLegendAbility(context.AbilityId, out var ability);
        var unitId = item.TargetObjectIds[0]; var gearId = item.TargetObjectIds[1];
        var events = new List<GameEvent> { new("LEGEND_ABILITY_RESOLVED", "传奇贴附技能结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["cardNo"] = item.CardNo, ["abilityId"] = context.AbilityId, ["stackItemId"] = item.StackItemId }) };
        if (!IsValidLegendAbilityTarget(state, item.ControllerId, unitId, ability)
            || !state.CardObjects.TryGetValue(unitId, out var host) || !IsFaceUpNonStandbyUnit(host)
            || host.ObjectGeneration != item.TargetGenerations![unitId]
            || !state.CardObjects.TryGetValue(gearId, out var gear) || gear.ObjectGeneration != item.TargetGenerations[gearId]
            || !IsValidLegendArmamentSecondTarget(state, item.ControllerId, unitId, gearId, ability))
        {
            events.Add(new("ABILITY_NO_EFFECT", "贴附目标不再合法，已支付的费用不退回", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["abilityId"] = context.AbilityId,
                ["stackItemId"] = item.StackItemId, ["reason"] = "TARGET_NO_LONGER_LEGAL" }));
            return NoopStackResolutionResult(state) with { Events = events };
        }
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var applied = TryAttachEquipment(state, zones, cards, state.ObjectLocations, item.ControllerId, item.SourceObjectId,
            gearId, unitId, ability.EffectKind, events, out var locations, new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["abilityId"] = context.AbilityId, ["stackItemId"] = item.StackItemId });
        // The granted skill was captured at activation. Losing control of the
        // granting battlefield in response does not erase the pending skill.
        if (applied && context.BattlefieldObjectId is { } battlefield)
            events.Add(new("BATTLEFIELD_TRIGGER_RESOLVED", "战场授予的传奇贴附技能结算", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["battlefieldObjectId"] = battlefield,
                ["battlefieldCardNo"] = context.BattlefieldCardNo,
                ["trigger"] = "BATTLEFIELD_CONTROLLED_LEGEND_ATTACH_ARMAMENT",
                ["sourceObjectId"] = item.SourceObjectId, ["unitObjectId"] = unitId,
                ["equipmentObjectId"] = gearId, ["abilityId"] = context.AbilityId }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, Events = events };
    }
}
