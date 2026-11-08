using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Destruction choices and costs select permanents without targeting them.
    // A caller can require a card type or exclude its source; protection and
    // target taxes belong to the separate target-confirmation flow.
    private static bool IsFriendlyDestructionChoice(MatchState state, string player,
        string id, string? requiredTag = null, string? excludedObjectId = null)
        => IsDestructionChoice(state, player, id, new(1, requiredTag), excludedObjectId);

    private static CardObjectState[] FriendlyDestructionChoices(MatchState state, string player,
        string? requiredTag = null, string? excludedObjectId = null)
        => state.CardObjects.Values.Where(card => IsFriendlyDestructionChoice(state, player,
                card.ObjectId, requiredTag, excludedObjectId))
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal).ToArray();
}
