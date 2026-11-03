using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Destruction choices and costs select permanents without targeting them.
    // A caller can require a card type or exclude its source; protection and
    // target taxes belong to the separate target-confirmation flow.
    private static bool IsFriendlyDestructionChoice(MatchState state, string player,
        string id, string? requiredTag = null, string? excludedObjectId = null)
        => id != excludedObjectId && IsObjectOnField(state.PlayerZones, id)
            && state.CardObjects.TryGetValue(id, out var card) && !card.IsFaceDown
            && EffectiveFieldControllerId(state.PlayerZones, id, card) == player
            && (card.Tags.Contains(CardObjectTags.UnitCard) || card.Tags.Contains(CardObjectTags.EquipmentCard))
            && (requiredTag is null || card.Tags.Contains(requiredTag));

    private static CardObjectState[] FriendlyDestructionChoices(MatchState state, string player,
        string? requiredTag = null, string? excludedObjectId = null)
        => state.CardObjects.Values.Where(card => IsFriendlyDestructionChoice(state, player,
                card.ObjectId, requiredTag, excludedObjectId))
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal).ToArray();
}
