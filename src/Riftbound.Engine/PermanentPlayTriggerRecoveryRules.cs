using System.Text.RegularExpressions;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

internal static class PermanentPlayTriggerRecoveryRules
{
    // Rebuild the restricted source family from immutable printed text rather
    // than trusting the producer's decision or any reconstructed player view.
    private static readonly Lazy<IReadOnlySet<string>> BattlefieldOnlySources = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(card => Regex.IsMatch(card.CardEffect,
                    @"(?:^|[。\n])\s*每?当你将我打出到一处战场时", RegexOptions.CultureInvariant)
                && !Regex.IsMatch(card.CardEffect,
                    @"(?:^|[。\n])\s*每?(?:当你打出(?:我|此牌|此装备)|当此牌被打出|当你将我打出)时",
                    RegexOptions.CultureInvariant))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));

    private static readonly Lazy<IReadOnlySet<string>> HereDamageSources = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(card => BattlefieldOnlySources.Value.Contains(card.CardNo)
                && card.CardEffect.Contains("对此处的一名敌方单位造成2点伤害", StringComparison.Ordinal))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));

    internal static bool Valid(MatchState state, StackItemState item)
    {
        if (!CoreRuleEngine.ValidShowcase(state, item)) return false;
        if (!CoreRuleEngine.ValidRadShowcaseRuneTargets(state, item)) return false;
        if (item.SourceConfirmed && item.TargetGenerations is { } targets
            && targets.Any(binding => binding.Value < 0 || state.CardObjects.TryGetValue(binding.Key, out var target)
                && binding.Value > target.ObjectGeneration)) return false;
        if (item.SourceConfirmed && CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var sacrificed)
            && sacrificed.PlayAbilityRequiresPermanentDestructionCost && !CoreRuleEngine.OptionalPermanentDestructionWasPaid(item)) return false;
        if (item.SourceConfirmed && CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var returnedEquipment)
            && returnedEquipment.ReadiesSourceAndAddsReturnedEquipmentManaPower && item.ReturnedEquipmentManaCost is null) return false;
        if (item.ReturnedEquipmentManaCost is { } mana
            && (mana < 0 || !CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var equipmentCost)
                || !equipmentCost.MayReturnFriendlyEquipmentAdditionalCost
                || item.OptionalCosts.Count(cost => cost.StartsWith("RETURN_FRIENDLY_EQUIPMENT:", StringComparison.Ordinal)) != 1
                || item.SourceConfirmed && !state.CardPlays.Resolved.Any(play => play.PlayId == item.StackItemId
                    && play.CardNo == item.CardNo && play.PlayerId == item.ControllerId
                    && play.Source.ObjectId == item.SourceObjectId && play.ReturnedEquipmentManaCost == mana))) return false;
        if (item.SourceConfirmed && CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var conditional)
            && conditional.CreatedBaseUnitTokenConditionKind == CardTokenCreationConditionKinds.DiscardHandCardOptionalCostPaid
            && (item.EffectKind != conditional.EffectKind || !item.OptionalCosts.Any(cost =>
                cost.StartsWith("DISCARD_HAND_CARD:", StringComparison.Ordinal) && cost.Length > "DISCARD_HAND_CARD:".Length)))
            return false;
        var battlefield = item.Destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            && item.Destination.Length > "BATTLEFIELD:".Length;
        var here = item.SourceConfirmed && battlefield && HereDamageSources.Value.Contains(item.CardNo)
            && CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var hereBehavior) && item.EffectKind == hereBehavior.EffectKind;
        var declares = item.SourceConfirmed && CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var named)
            && item.EffectKind == named.EffectKind && named.SourceStaticLimits?.DeclaresSpellNameAndProhibitsItOnEnemyBattlefield == true;
        var sourceInstruction = item.SourceConfirmed && CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var instruction)
            && item.EffectKind == instruction.EffectKind
            && (instruction.RecyclesSourceIfPlayedFromStandby || instruction.GrantsSourceEphemeralIfOpponentTurn
                || instruction.PlayTargetsAtSourceLocation && battlefield);
        if (sourceInstruction && (CoreRuleEngine.PlayInstructionSource(state, item) is not { } receipt
            || !state.CardObjects.TryGetValue(receipt.ObjectId, out var entered) || entered.ObjectGeneration < receipt.Generation
            || item.PlayedFromStandby is { } shadow && receipt.Generation != shadow.SourceGeneration + 1)) return false;
        var graveyardCost = item.SourceConfirmed && CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var paid)
            && paid.PlayTriggerBanishesGraveyardCardAsCost && item.EffectKind == paid.EffectKind;
        if (item.PermanentPlaySource is { } binding
            ? !(here || graveyardCost || declares) || binding.ObjectId != item.SourceObjectId || binding.Generation < 0
                || !state.CardObjects.TryGetValue(binding.ObjectId, out var source) || source.ObjectGeneration < binding.Generation
                || item.PlayedFromStandby is { } standby && binding.Generation != standby.SourceGeneration + 1
                || !state.CardPlays.Resolved.Any(play => play.Source == binding && play.CardNo == item.CardNo)
            : here || graveyardCost || declares) return false;

        if (!item.SourceConfirmed || !BattlefieldOnlySources.Value.Contains(item.CardNo)
            || !CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var behavior)
            || item.EffectKind != behavior.EffectKind
            || !(behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment)) return true;

        // This is the recorded play destination, not the source's current
        // location: a response can move the permanent after its ability triggers.
        if (battlefield) return true;
        if (item.DamageAmount != 0) return false;

        return false;
    }

    internal static bool ValidQueued(MatchState state, TriggerQueueItemState trigger)
    {
        if (trigger.PermanentPlayStackItemId is not { } id)
            return !trigger.TriggerId.StartsWith("permanent-play-order-", StringComparison.Ordinal);
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == id);
        return item is { SourceConfirmed: true, UnitPlayMight: null } && state.StackItems.Count(i => i.StackItemId == id) == 1
            && trigger.TriggerId == "permanent-play-order-" + id && trigger.SourceObjectId == item.SourceObjectId
            && trigger.ControllerId == item.ControllerId && trigger.EffectKind == item.EffectKind
            && trigger.SourceCardNo == item.CardNo && trigger.UnitPlayMight is null && Valid(state, item)
            && state.TriggerQueue.Any(t => t.UnitPlayMight is { } legend
                && t.ControllerId == item.ControllerId && t.TriggeredByEventKind == trigger.TriggeredByEventKind
                && trigger.EnqueuedAtTick == legend.CapturedTick
                && RengarPlayRecoveryRules.Valid(state, legend, t.EffectKind, t.SourceObjectId, t.ControllerId)
                && legend.PlayedUnit.ObjectId == item.SourceObjectId
                && state.CardPlays.Resolved.Any(p => p.PlayId == id && p.Source == legend.PlayedUnit
                    && p.CardNo == item.CardNo && p.PlayerId == item.ControllerId && p.Tick == legend.CapturedTick));
    }
}
