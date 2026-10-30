using Riftbound.Contracts;
using Riftbound.CardCatalog;
using System.Text.RegularExpressions;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string ReadyRestrictedEntryChoice = "ENTER_READY_CANNOT_MOVE";
    internal const string DestroyFriendlyPermanentAdditionalCostPrefix = "DESTROY_FRIENDLY_PERMANENT:";

    private static bool CanDestroyFriendlyPermanentAsAdditionalCost(MatchState state, string player, string id)
        => IsPlayerControlledFieldObject(state, player, id) && state.CardObjects.TryGetValue(id, out var card)
            && !card.IsFaceDown && (card.Tags.Contains(CardObjectTags.UnitCard) || card.Tags.Contains(CardObjectTags.EquipmentCard));

    internal static IReadOnlyList<string> OptionalPermanentDestructionChoices(MatchState state, string player)
        => state.CardObjects.Keys.Order(StringComparer.Ordinal).Where(id => CanDestroyFriendlyPermanentAsAdditionalCost(state, player, id))
            .Select(id => DestroyFriendlyPermanentAdditionalCostPrefix + id).ToArray();

    internal static bool OptionalPermanentDestructionWasPaid(StackItemState item)
        => item.OptionalCosts.Count(cost => cost.StartsWith(DestroyFriendlyPermanentAdditionalCostPrefix, StringComparison.Ordinal)
            && cost.Length > DestroyFriendlyPermanentAdditionalCostPrefix.Length) == 1;

    private static bool FriendlyPermanentDestroyedThisTurn(string controller, int turnNumber)
    {
        if (RuleChoices.Value is not { } frame) return false;
        var ledger = frame.ResourceDeathLedger ?? frame.Origin.DeathLedger;
        return frame.DestroyedPermanentControllers.Contains((turnNumber, controller))
            || ledger.TurnNumber == turnNumber && ledger.PermanentControllers.Contains(controller);
    }

    internal static IReadOnlyList<string> OptionalReturnEquipmentChoices(MatchState state, string player, CardBehaviorDefinition behavior)
        => behavior.MayReturnFriendlyEquipmentAdditionalCost ? state.CardObjects.Keys.Order(StringComparer.Ordinal)
            .Where(id => CanReturnFriendlyEquipmentAsAdditionalCost(state, player, id))
            .Select(id => ReturnFriendlyEquipmentAdditionalCostPrefix + id).ToArray() : [];

    private static int? ReturnedEquipmentManaCost(MatchState state, PlayCardPlan plan)
        => plan.Behavior.MayReturnFriendlyEquipmentAdditionalCost && plan.ReturnedAdditionalCostTargetObjectIds.Count == 1
            && state.CardObjects.TryGetValue(plan.ReturnedAdditionalCostTargetObjectIds[0], out var equipment)
                ? equipment.ManaCost : null;

    private static void ResolveReturnedEquipmentSourceInstruction(MatchState state, CardBehaviorDefinition behavior,
        StackItemState item, Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        if (!behavior.ReadiesSourceAndAddsReturnedEquipmentManaPower || item.ReturnedEquipmentManaCost is not { } mana) return;
        var binding = state.CardPlays.Resolved.SingleOrDefault(play => play.PlayId == item.StackItemId
            && play.PlayerId == item.ControllerId && play.CardNo == item.CardNo && play.Source.ObjectId == item.SourceObjectId)?.Source;
        if (binding is null || !cards.TryGetValue(binding.ObjectId, out var source)
            || source.ObjectGeneration != binding.Generation || !IsObjectOnField(zones, source.ObjectId)) return;
        var wasExhausted = source.IsExhausted;
        AddReadiedObservation(zones, cards, source, events);
        var next = mana == 0 ? source : ApplyDirectUntilEndPowerModifier(source, source.ObjectId, source.ObjectId,
            item.CardNo, "RETURNED_EQUIPMENT_MANA_POWER", "CoreRuleEngine.ResolveReturnedEquipmentSourceInstruction", mana, source.Power + mana);
        cards[source.ObjectId] = next with { IsExhausted = false };
        events.Add(CaptureSelfReady(new GameEvent("UNIT_READIED", "返回装备费用使单位变为活跃状态",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId,
                ["targetObjectId"] = source.ObjectId, ["wasExhausted"] = wasExhausted, ["isExhausted"] = false }),
            source, item.ControllerId, cards: cards));
        if (mana != 0) events.Add(new GameEvent("POWER_MODIFIED_UNTIL_END_OF_TURN", "返回装备费用临时修正战力",
            new Dictionary<string, object?> { ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = source.ObjectId,
                ["powerDelta"] = mana, ["appliedPowerDelta"] = mana, ["resultingPower"] = next.Power,
                ["reason"] = "RETURNED_EQUIPMENT_MANA_POWER" }));
    }

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlySet<string>>> PrintedEquipmentKeywordLines = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(card => card.CardCategoryNameList.Contains("装备")).DistinctBy(card => card.CardNo)
            .ToDictionary(card => card.CardNo, card => (IReadOnlySet<string>)Regex.Matches(card.CardEffect,
                @"(?:^|\n)\s*\{\{([^}]+)\}\}", RegexOptions.CultureInvariant)
                .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal));

    private static bool EquipmentMatchesAuraKeyword(StaticAuraSpec aura, CardObjectState equipment)
        => StaticAuraSpecRules.TargetMatchesFilter(aura, equipment)
            || aura.TargetFilter is { } filter && filter.StartsWith(StaticAuraTargetFilters.TagPrefix, StringComparison.Ordinal)
                && equipment.CardNo is { } cardNo && PrintedEquipmentKeywordLines.Value.TryGetValue(cardNo, out var keywords)
                && keywords.Contains(filter[StaticAuraTargetFilters.TagPrefix.Length..]);

    internal static IReadOnlyList<string> SameBattlefieldFriendlyEquipmentParticipants(MatchState state, string sourceId,
        string controller, StaticAuraSpec aura)
    {
        if (!state.ObjectLocations.TryGetValue(sourceId, out var at) || at.Zone != "BATTLEFIELD"
            || string.IsNullOrWhiteSpace(at.BattlefieldObjectId)) return [];
        return state.CardObjects.Values.Where(card => !card.IsFaceDown && card.Tags.Contains(CardObjectTags.EquipmentCard)
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) == controller
            && EquipmentMatchesAuraKeyword(aura, card)
            && state.ObjectLocations.TryGetValue(card.ObjectId, out var other) && other.Zone == "BATTLEFIELD"
            && other.BattlefieldObjectId == at.BattlefieldObjectId).Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }
}
