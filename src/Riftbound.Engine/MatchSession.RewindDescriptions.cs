using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    private static (string Public, string? Actor) DescribeRewindCommand(MatchState before, string playerId,
        GameCommand command, IReadOnlyList<GameEvent> events, string fallback)
    {
        // Describe what was visible at this step. Later draws and reveals do not
        // upgrade a private choice, and no client-supplied labels are displayed.
        return command switch
        {
            PlayCardCommand play => (DescribePublicPlay(events, play.SourceObjectId, "打出", fallback), null),
            RevealCardCommand reveal => (DescribePublicPlay(events, reveal.SourceObjectId, "翻开待命牌", fallback), null),
            HideCardCommand hide => (fallback, DescribePrivateCards(before, playerId, [hide.SourceObjectId], "设置待命牌")),
            MulliganCommand mulligan => (fallback, DescribePrivateCards(before, playerId, mulligan.HandObjectIds, fallback)),
            ChooseHandCardsCommand hand => (fallback, DescribePrivateCards(before, playerId, hand.ChosenObjectIds, fallback)),
            ChooseCardsCommand choice => (fallback, DescribePrivateCards(before, playerId, choice.ChosenObjectIds,
                before.PendingCardChoice?.ChoiceWindow == "INSIGHT" ? "洞察回收" : fallback)),
            MoveUnitCommand move => (DescribeMovement(before, move, events) ?? fallback, null),
            ActivateAbilityCommand ability => (DescribePublicSource(before, ability.SourceObjectId, "发动", "的能力") ?? fallback, null),
            LegendActCommand legend => (DescribePublicSource(before, legend.SourceObjectId, "发动", "的能力") ?? fallback, null),
            AssembleEquipmentCommand equipment => (DescribePublicSource(before, equipment.SourceObjectId, "装配", "") ?? fallback, null),
            DeclareBattleCommand battle => (DescribePublicSource(before, battle.BattlefieldId, "进入战斗于", "") ?? fallback, null),
            TapRuneCommand tap => ($"横置 {tap.SourceObjectIds?.Count ?? 1} 枚符文", null),
            RecycleRuneCommand recycle => ($"回收 {recycle.SourceObjectIds?.Count ?? 1} 枚符文", null),
            _ => (fallback, null)
        };
    }

    private static string DescribePublicPlay(IReadOnlyList<GameEvent> events, string source, string verb, string fallback)
    {
        // Opening a payment window alone does not certify a public play.
        foreach (var gameEvent in events)
        {
            var cardNo = gameEvent.Kind switch
            {
                "CARD_PLAYED" or "CARD_REVEALED" when EventText(gameEvent, "sourceObjectId") == source
                    => EventText(gameEvent, "cardNo"),
                "CARD_PLAYED_FROM_GRAVEYARD" when EventText(gameEvent, "playedObjectId") == source
                    => EventText(gameEvent, "playedCardNo"),
                _ => null
            };
            if (OfficialCardSourceIdentityGroups.KnownDisplayName(cardNo) is { } name)
                return $"{verb}「{name}」";
        }
        return fallback;
    }

    private static string? DescribeMovement(MatchState before, MoveUnitCommand command, IReadOnlyList<GameEvent> events)
    {
        var sources = command.SourceObjectIds ?? [command.SourceObjectId];
        var moves = events.Where(gameEvent => (gameEvent.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD")
            && sources.Contains(EventText(gameEvent, "sourceObjectId"), StringComparer.Ordinal)).ToArray();
        if (moves.Length == 0)
            return null;

        var names = moves.Select(move => PublicObjectName(before, EventText(move, "sourceObjectId")!)).ToArray();
        var units = string.Join("、", names.Select(name => name is null ? "单位" : $"「{name}」"));
        var battlefield = EventText(moves[0], "battlefieldObjectId");
        var destination = moves[0].Kind == "UNIT_MOVED_TO_BASE" ? "基地"
            : battlefield is not null && before.BattlefieldStates.TryGetValue(battlefield, out var field)
                && OfficialCardSourceIdentityGroups.KnownDisplayName(field.CardNo) is { } name
                    ? $"「{name}」" : "战场";
        return $"移动{units}到{destination}";
    }

    private static string? DescribePublicSource(MatchState before, string source, string prefix, string suffix)
        => PublicObjectName(before, source) is { } name ? $"{prefix}「{name}」{suffix}" : null;

    private static string? PublicObjectName(MatchState before, string? source)
        => !string.IsNullOrWhiteSpace(source) && before.CardObjects.TryGetValue(source, out var card) && !card.IsFaceDown
            && ResolutionResult.VisibleObjectIdForViewer(before, source, ProjectionViewers.Spectator) != "HIDDEN"
            ? OfficialCardSourceIdentityGroups.KnownDisplayName(card.CardNo) : null;

    private static string? DescribePrivateCards(MatchState before, string playerId, IReadOnlyList<string>? sources, string verb)
    {
        if (sources is null || sources.Count == 0)
            return null;
        var viewedDeckCards = before.PendingCardChoice is { DeckContext: not null } choice && choice.PlayerId == playerId
            ? choice.ContextObjectIds : [];
        var names = new List<string>();
        foreach (var source in sources.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()))
        {
            if (!before.CardObjects.TryGetValue(source, out var card)
                || ResolutionResult.VisibleObjectIdForViewer(before, source, playerId) == "HIDDEN")
                continue;
            // The ordinary object-id projection does not redact an owner's own
            // deck IDs. Only a card currently observed or revealed is nameable.
            if (before.PlayerZones.Values.Any(zones => zones.MainDeck.Contains(source) || zones.RuneDeck.Contains(source))
                && !viewedDeckCards.Contains(source)
                && !CoreRuleEngine.CanViewObservedDeckCard(before, source, playerId)
                && !CoreRuleEngine.IsPubliclyRevealedDeckCard(before, source))
                continue;
            if (OfficialCardSourceIdentityGroups.KnownDisplayName(card.CardNo) is { } name)
                names.Add($"「{name}」");
        }
        return names.Count > 0 ? verb + string.Join("、", names) : null;
    }

    private static string? EventText(GameEvent gameEvent, string field) => gameEvent.Payload.GetValueOrDefault(field) as string;
}
