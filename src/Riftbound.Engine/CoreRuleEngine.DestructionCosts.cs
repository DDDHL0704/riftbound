namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool ValidPermanentDestructionCost(PermanentDestructionCostSpec? cost)
        => cost is null || cost.Count > 0 && (cost.MaximumMana is null or >= 0)
            && (cost.RequiredTag is null or CardObjectTags.EquipmentCard or CardObjectTags.UnitCard);

    private static bool IsDestructionChoice(MatchState state, string player, string id,
        PermanentDestructionCostSpec cost, string? source)
        => (cost.AllowsSource || id != source) && IsObjectOnField(state.PlayerZones, id)
            && state.CardObjects.TryGetValue(id, out var card) && !card.IsFaceDown
            && (cost.AnyController || EffectiveFieldControllerId(state.PlayerZones, id, card) == player)
            && (card.Tags.Contains(CardObjectTags.UnitCard) || card.Tags.Contains(CardObjectTags.EquipmentCard))
            && (cost.RequiredTag is null || card.Tags.Contains(cost.RequiredTag))
            && (cost.MaximumMana is not { } maximum || card.ManaCost <= maximum);

    private static CardObjectState[] DestructionChoices(MatchState state, string player,
        PermanentDestructionCostSpec cost, string? source)
        => state.CardObjects.Values.Where(card => IsDestructionChoice(state, player, card.ObjectId, cost, source))
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal).ToArray();

    private static string DestructionCostLabel(LeadingCost cost)
    {
        if (!cost.DestructionUsesLastKnownMana)
            return $"选择摧毁另外 {cost.DestroyPermanents} 个友方{(cost.DestroyPermanentsRequiredTag == CardObjectTags.EquipmentCard ? "装备" : "单位或装备")}作为费用；不选则放弃。支付后双方可以响应。";
        var other = cost.DestructionAllowsSource ? "" : "其他";
        var friendly = cost.DestructionAnyController ? "" : "友方";
        var maximum = cost.DestructionMaximumMana is { } mana ? $"法力费用不高于 {mana} 的" : "";
        var kind = cost.DestroyPermanentsRequiredTag == CardObjectTags.EquipmentCard ? "装备" : "单位或装备";
        return $"选择摧毁 {cost.DestroyPermanents} 个{other}{friendly}{maximum}{kind}作为费用；不选则放弃。支付后双方可以响应。";
    }

    internal static bool RequiresInstructionCostHistory(UnitEventContext? context)
        => context is not null && UnitEventSpec(context.CardNo) is { } spec
            && (spec.Timing == UnitEventTimings.OpponentTurnSpellPlayed || spec.LeadingDestruction is not null);

    internal static string InstructionCostCaptureHash(UnitEventContext context, string source, string controller)
        => MatchStateHasher.HashValue(new { Source = source, Controller = controller, Context = context with { Mode = null } });

    internal static string DestructionCostReceiptHash(TriggerCostReceipt receipt)
        => MatchStateHasher.HashValue(receipt);
}
