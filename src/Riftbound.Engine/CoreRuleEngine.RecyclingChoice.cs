using Riftbound.Contracts;

namespace Riftbound.Engine;

// Last-known information survives the cost's zone transition, including token removal.
public sealed record RecycledUnitReceipt(string ObjectId, long Generation, int Power);

public sealed partial class CoreRuleEngine
{
    private static string[] RecyclingChoices(MatchState state, StackItemState parent)
        => state.CardObjects.Values.Where(card => card.ObjectId != parent.SourceObjectId
            && card.ControllerId == parent.ControllerId && IsFaceUpNonStandbyUnit(card)
            && IsObjectOnField(state.PlayerZones, card.ObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static StackResolutionResult BeginRecyclingChoice(MatchState state, StackItemState parent)
    {
        var legal = RecyclingChoices(state, parent);
        if (legal.Length == 0) return NoopStackResolutionResult(state);
        var choice = new PendingCardChoiceState($"RECYCLE-PLAY:{parent.StackItemId}", "RECYCLE_FOR_EFFECT_PLAY",
            parent.ControllerId, 0, 1, legal, legal,
            "可选择回收另一名友方单位，再从你的废牌堆打出机械单位；按回收前战力减法力，符能仍需支付。不选则放弃。",
            parent.SourceObjectId, parent.EffectKind) { ResolvingStackItemId = parent.StackItemId };
        return NoopStackResolutionResult(state) with { StackItems = state.StackItems, PendingCardChoice = choice };
    }

    internal static bool ValidRecycledUnit(StackItemState parent)
        => parent.RecastContext is { Kind: TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit } context
            && ValidRecastContext(context, parent.EffectKind, parent.CardNo)
            && parent.RecycledUnit is { Power: >= 0, Generation: >= 0 } receipt
            && !string.IsNullOrWhiteSpace(receipt.ObjectId) && receipt.ObjectId != parent.SourceObjectId;

    internal static bool ValidRecyclingChoice(MatchState state, PendingCardChoiceState choice)
    {
        var parent = state.StackItems.LastOrDefault();
        return parent is { RecycledUnit: null, EffectPlayCompleted: false,
                RecastContext.Kind: TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit }
            && ValidRecastContext(parent.RecastContext, parent.EffectKind, parent.CardNo)
            && choice.ChoiceWindow == "RECYCLE_FOR_EFFECT_PLAY" && choice.ResolvingStackItemId == parent.StackItemId
            && choice.PlayerId == parent.ControllerId && choice.SourceObjectId == parent.SourceObjectId
            && choice.EffectKind == parent.EffectKind && choice.RequiredCount == 0 && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(RecyclingChoices(state, parent))
            && choice.ContextObjectIds.SequenceEqual(choice.LegalObjectIds);
    }

    private static ResolutionResult ResolveRecyclingChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidRecyclingChoice(state, choice))
            return RejectWithCorePrompts(state, "回收费用选择已失效。", ErrorCodes.InvalidTarget);
        var parent = state.StackItems[^1];
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        var events = new List<GameEvent>();
        if (selected.Count == 0) parent = parent with { EffectPlayCompleted = true };
        else
        {
            var id = selected[0];
            var card = cards[id];
            var power = Math.Max(0, ResolutionResult.SnapshotEffectivePower(state, card, state.ObjectLocations.GetValueOrDefault(id)));
            if (!TryMoveTargetToOwnerMainDeck(zones, cards, id, "BOTTOM", out var owner, out _))
                return RejectWithCorePrompts(state, "无法支付回收单位费用。", ErrorCodes.InvalidTarget);
            parent = parent with { RecycledUnit = new(id, card.ObjectGeneration, power) };
            events.Add(new("CARDS_RECYCLED", "已回收所选单位，按离场前战力减少再次打出的法力费用", new Dictionary<string, object?>
            {
                ["playerId"] = parent.ControllerId, ["ownerPlayerId"] = owner,
                ["sourceObjectId"] = parent.SourceObjectId, ["cardIds"] = selected.ToArray(),
                ["count"] = 1, ["recycledUnitPower"] = power, ["destinationZone"] = "MAIN_DECK",
                ["reason"] = parent.RecastContext!.Kind
            }));
        }
        events.Add(new("CARD_CHOICE_RESOLVED", selected.Count == 0 ? "放弃回收并再次打出" : "回收费用已支付",
            new Dictionary<string, object?> { ["choiceId"] = choice.ChoiceId, ["playerId"] = choice.PlayerId, ["chosenCount"] = selected.Count }));
        var next = state with { PendingCardChoice = null, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(parent).ToArray() };
        var resumed = ResolvePassPriority(next, new("recycle-continuation", parent.ControllerId, CommandTypes.PassPriority), forceResolve: true);
        return resumed with { Events = events.Concat(resumed.Events).ToArray() };
    }
}
