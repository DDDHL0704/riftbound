using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static TriggerSpec? AttackDeckPlaySpec(string? cardNo) => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo)
        .FirstOrDefault(t => t.Kind == TriggerKinds.UnitAttackRevealBanishPlay && t.Timing == TriggerTimings.UnitAttack
            && t.RevealSourceZone == TriggerZones.MainDeck && t.RevealCount is > 0 && t.BanishCount == 1 && t.PlayCount == 1
            && t.PlayDestinationZone == TriggerZones.Stack && t.RecycleDestinationZone == TriggerZones.MainDeck
            && t.Optional == true && t.PayPlayPowerCosts == true && t.PlayUnitAtSourceLocation == true);

    private static TriggerQueueItemState[] CaptureAttackDeckPlayTriggers(MatchState state, IEnumerable<string> attackers,
        string battlefield, string timing, string? returnFocusPlayer = null)
    {
        var queue = new List<TriggerQueueItemState>();
        foreach (var id in attackers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (AttackDeckPlaySpec(instance.CardNo) is null) continue;
                queue.Add(new($"attack-deck-{state.Tick + 1}-{id}-{instance.Id}", source.ControllerId!, id,
                    FieldTriggerEffect, "BATTLE_ROLES_ESTABLISHED", timing) {
                    FieldContext = new(instance.CardNo, "ATTACK_DECK_PLAY", source.ObjectGeneration, battlefield, returnFocusPlayer) {
                        SourceLocation = new(source.ControllerId!, "BATTLEFIELD", battlefield), Origin = instance.Origin,
                        SourceTokenFactoryCardNo = IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null } });
            }
        }
        return queue.ToArray();
    }

    private static StackResolutionResult ResolveAttackDeckPlay(MatchState state, StackItemState item)
    {
        if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
        // This optional instruction is chosen at resolution, before inspecting the deck.
        // The existing synchronous-choice checkpoint rejects forged transcripts and
        // preserves the original action until the reveal decision is answered.
        if (item.DeckPlaySequence is null && RuleChoices.Value!.Choose(item.ControllerId,
            "可以展示主牌堆顶部的两张牌，随后选择放逐并正常打出一张；也可以不展示。",
            [new("REVEAL", "展示并继续选择"), new("SKIP", "不展示，结束此能力")], kind: "DECK_REVEAL") == "SKIP")
            return NoopStackResolutionResult(state);
        TryGetEffectPlayDefinition(item, out var definition);
        return BeginDeckPlaySequence(state, item, definition);
    }

    private static string? AttackDeckPlayHere(MatchState state, PendingEffectPlayState pending)
        => pending.Parent.FieldContext is { Kind: "ATTACK_DECK_PLAY", SourceCeasedToExist: false } field
            && pending.Sources.Keys.SingleOrDefault() is { } chosen && state.CardObjects.TryGetValue(chosen, out var card)
            && card.Tags.Contains(CardObjectTags.UnitCard)
            && CombatMightSourceLocation(state, pending.Parent.SourceObjectId, field.SourceGeneration) is { Zone: "BATTLEFIELD" } location
            ? "BATTLEFIELD:" + location.BattlefieldObjectId : null;

    private static bool IsAttackDeckPlayDestination(MatchState state, string player, string destination)
        => state.PendingEffectPlay is { Parent.FieldContext.Kind: "ATTACK_DECK_PLAY" } pending
            && pending.PlayerId == player && destination == AttackDeckPlayHere(state, pending);
}
