using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record UnitEntryTriggerContext(string CardNo, long SourceGeneration, ObjectBinding Entered, string EnteringPlayerId);

public static class MovementRestrictionRules
{
    public static string ForPlayer(string player) => "MOVEMENT_PROHIBITED:" + player;
    public static bool CanMove(CardObjectState? card, string player)
        => card is not null && !card.UntilEndOfTurnEffects.Contains(ForPlayer(player), StringComparer.Ordinal);
}

public sealed partial class CoreRuleEngine
{
    internal const string EnemyUnitEntryEffect = "ENEMY_UNIT_ENTERED_STUN_AND_RESTRICT_MOVEMENT";
    private static readonly Lazy<IReadOnlyList<string>> EnemyUnitEntrySources = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["UNL-150/219"])["UNL-150/219"]);
    private static bool HasEnemyEntryAbility(string? cardNo) => EnemyUnitEntrySources.Value.Contains(
        OfficialCardSourceIdentityGroups.NormalizeCardNo(cardNo), StringComparer.Ordinal);
    internal static bool ValidUnitEntryTrigger(UnitEntryTriggerContext c, string effect, string controller, string? cardNo = null)
        => effect == EnemyUnitEntryEffect && HasEnemyEntryAbility(c.CardNo) && (cardNo is null || cardNo == c.CardNo)
            && c.SourceGeneration >= 0 && c.Entered is not null && c.Entered.Generation >= 0 && !string.IsNullOrWhiteSpace(c.Entered.ObjectId)
            && !string.IsNullOrWhiteSpace(c.EnteringPlayerId) && c.EnteringPlayerId != controller;

    private sealed record UnitEntryObserver(string SourceId, string ControllerId, string CardNo, long Generation);
    private sealed record UnitEntryObservation(string ObjectId, long Generation, string PlayerId, UnitEntryObserver[] Observers);

    // Capture at the entry instruction, before later instructions can move, kill,
    // copy or change control of an observer. Final-state scans lose that timing.
    private static GameEvent CaptureUnitEntry(GameEvent ev,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards)
    {
        var id = ev.Payload.GetValueOrDefault("tokenObjectId") as string
            ?? ev.Payload.GetValueOrDefault("unitObjectId") as string
            ?? ev.Payload.GetValueOrDefault("targetObjectId") as string;
        if (id is null || !cards.TryGetValue(id, out var entered) || !entered.Tags.Contains(CardObjectTags.UnitCard)) return ev;
        var player = ev.Payload.GetValueOrDefault("playedByPlayerId") as string
            ?? ev.Payload.GetValueOrDefault("playerId") as string ?? entered.ControllerId;
        if (player is null) return ev;
        var sources = zones.Values.SelectMany(z => z.Battlefields).Distinct(StringComparer.Ordinal)
            .Where(cards.ContainsKey).Select(id => cards[id])
            .Where(c => HasEnemyEntryAbility(c.CardNo) && !c.IsFaceDown && !c.Tags.Contains(CardObjectTags.Standby)
                && c.ControllerId is not null && c.ControllerId != player)
            .Select(c => new UnitEntryObserver(c.ObjectId, c.ControllerId!, c.CardNo!, c.ObjectGeneration)).ToArray();
        if (sources.Length == 0) return ev;
        return ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            ["unitEntryObservation"] = new UnitEntryObservation(id, entered.ObjectGeneration, player, sources) } };
    }

    private static ResolutionResult QueueUnitEntryTriggers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = result.State;
        var events = result.Events.ToList();
        var queue = state.TriggerQueue.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Payload.GetValueOrDefault("unitEntryObservation") is not UnitEntryObservation observation) continue;
            var generation = observation.Generation;
            if (before.CardObjects.TryGetValue(observation.ObjectId, out var old) && !IsObjectOnField(before.PlayerZones, observation.ObjectId))
                generation = Math.Max(generation, old.ObjectGeneration + 1);
            foreach (var source in observation.Observers)
            {
                var context = new UnitEntryTriggerContext(source.CardNo, source.Generation,
                    new(observation.ObjectId, generation), observation.PlayerId);
                var trigger = new TriggerQueueItemState($"unit-entry-{state.Tick}-{index}-{source.SourceId}", source.ControllerId,
                    source.SourceId, EnemyUnitEntryEffect, ev.Kind) { UnitEntryContext = context };
                queue.Add(trigger);
                events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        if (queue.Count == state.TriggerQueue.Count) return result;
        state = state with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveUnitEntryTrigger(MatchState state, StackItemState item)
    {
        var context = item.UnitEntryContext!; var id = context.Entered.ObjectId;
        if (!state.CardObjects.TryGetValue(id, out var unit) || unit.ObjectGeneration != context.Entered.Generation
            || !IsObjectOnField(state.PlayerZones, id)) return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        cards[id] = unit with { UntilEndOfTurnEffects = unit.UntilEndOfTurnEffects
            .Concat(["STUNNED", MovementRestrictionRules.ForPlayer(context.EnteringPlayerId)]).Distinct().Order(StringComparer.Ordinal).ToArray() };
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = [
            new("STATUS_EFFECT_APPLIED", "薇古丝眩晕进场单位，并限制出牌者本回合移动该单位", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id,
                ["effectId"] = "STUNNED", ["statusEffectId"] = "STUNNED", ["duration"] = "UNTIL_END_OF_TURN",
                ["restrictedPlayerId"] = context.EnteringPlayerId })] };
    }
}
