using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool ValidLegendBattlefieldEquipmentToken(MatchState state, StackItemState item)
        => item.LegendUnitEffect is { } context
            && TryGetLegendAbility(context.AbilityId, out var ability)
            && ability.EffectKind == LegendAbilityEffectKinds.CreateBattlefieldEquipmentToken
            && ability.EffectKind == item.EffectKind
            && P6TokenFactoryCatalog.TryGetByCardNo(ability.CreatedBattlefieldEquipmentTokenCardNo, out var factory)
            && factory.Tags.Contains(CardObjectTags.EquipmentCard)
            && LegendActionAbilityCatalog.IsSourceCardNoForAbility(context.AbilityId, item.CardNo)
            && context.SourceGeneration >= 0 && context.SpellshieldTaxPower == 0
            && state.Seats.ContainsKey(item.ControllerId)
            && state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration >= context.SourceGeneration
            && (source.ObjectGeneration != context.SourceGeneration || source.CardNo == item.CardNo)
            && item.TargetObjectIds is { Count: 1 } && item.TargetObjectIds[0] != item.SourceObjectId
            && item.TargetGenerations is { Count: 1 }
            && item.TargetGenerations.TryGetValue(item.TargetObjectIds[0], out var generation) && generation >= 0
            && state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target)
            && target.ObjectGeneration >= generation
            && target.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag)
            && item.OptionalCosts.SequenceEqual(new[] { "SPEND_MANA:1" }, StringComparer.Ordinal)
            && item.TimingContext == TimingStates.NeutralOpen && !item.SourceConfirmed
            && item.EffectRepeatCount == 1 && item.DamageAmount == 0 && item.Destination.Length == 0;

    private static StackResolutionResult ResolveLegendBattlefieldEquipmentToken(MatchState state, StackItemState item)
    {
        TryGetLegendAbility(item.LegendUnitEffect!.AbilityId, out var ability);
        var battlefield = item.TargetObjectIds.Single();
        if (BattlefieldLocalRules.Battlefield(state, battlefield) is not { } target
            || target.ObjectGeneration != item.TargetGenerations![battlefield])
            return NoopStackResolutionResult(state);
        P6TokenFactoryCatalog.TryGetByCardNo(ability.CreatedBattlefieldEquipmentTokenCardNo, out var factory);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var locations = CreateBattlefieldEquipmentTokenBatch(zones, cards, item.ControllerId, item.SourceObjectId,
            item.CardNo!, ability.DisplayName, factory.TokenFamilyName, factory.Tags, 1, false, battlefield,
            events, ReconcileObjectLocations(state.ObjectLocations, zones));
        return NoopStackResolutionResult(state) with {
            PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, Events = events
        };
    }
}
