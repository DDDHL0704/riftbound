using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string BattlefieldPermanentPlayUsePrefix = "BATTLEFIELD_PERMANENT_PLAY_THIS_TURN:";

    internal static string BattlefieldPermanentPlayUse(string player, string battlefield)
        => BattlefieldPermanentPlayUsePrefix + JsonSerializer.Serialize(new[] { player, battlefield });

    internal static bool TryBattlefieldPermanentPlay(GameEvent ev, out string player, out string battlefield)
    {
        player = battlefield = "";
        if (ev.Kind is not ("UNIT_PLAYED_TO_BATTLEFIELD" or "EQUIPMENT_PLAYED_TO_BATTLEFIELD"
            or "UNIT_TOKEN_CREATED" or "EQUIPMENT_TOKEN_CREATED")) return false;
        static string Text(GameEvent entry, string key) => entry.Payload.GetValueOrDefault(key) switch {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString() ?? "",
            _ => ""
        };
        if (Text(ev, "destinationZone") != "BATTLEFIELD") return false;
        player = Text(ev, "playerId");
        battlefield = Text(ev, "battlefieldObjectId");
        if (battlefield.Length == 0) battlefield = Text(ev, "battlefieldId");
        if (battlefield.Length == 0 && Text(ev, "destination") is var destination
            && destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)) battlefield = destination[12..];
        return player.Length > 0 && battlefield.Length > 0;
    }

    private static ResolutionResult RecordBattlefieldPermanentPlays(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var turn = before.TurnNumber;
        var uses = result.State.UntilEndOfTurnEffects.ToHashSet(StringComparer.Ordinal);
        var events = result.Events.ToList();
        var queue = result.State.TriggerQueue.ToList();
        foreach (var (ev, index) in result.Events.Select((ev, index) => (ev, index)))
        {
            if (ev.Kind == "TURN_PLAYER_ADVANCED" && ev.Payload.GetValueOrDefault("turnNumber") is int advanced)
                turn = advanced;
            if (turn != result.State.TurnNumber || !TryBattlefieldPermanentPlay(ev, out var player, out var battlefield)
                || !uses.Add(BattlefieldPermanentPlayUse(player, battlefield))) continue;
            var source = BattlefieldLocalRules.Battlefield(result.State, battlefield);
            if (result.State.Status != MatchStatuses.InProgress || FirstPermanentInsightSpec(source?.CardNo) is not { } spec) continue;
            var trigger = new TriggerQueueItemState($"first-permanent-insight-{result.State.Tick}-{index}", player,
                source!.ObjectId, InsightTriggerEffect, ev.Kind) { SourceCardNo = source.CardNo,
                InsightContext = new(source.CardNo!, player, spec.RevealCount!.Value, source.ObjectGeneration,
                    "FIRST_PERMANENT", source.ObjectId) };
            queue.Add(trigger);
            events.Add(BuildTriggerQueuedEvent(trigger));
        }
        if (uses.SetEquals(result.State.UntilEndOfTurnEffects)) return result;
        var next = result.State with { UntilEndOfTurnEffects = uses.Order(StringComparer.Ordinal).ToArray(), TriggerQueue = queue };
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static TriggerSpec? FirstPermanentInsightSpec(string? cardNo)
        => BattlefieldTriggerSpecRules.TriggersForCard(cardNo).SingleOrDefault(t =>
            t.Kind == TriggerKinds.BattlefieldFirstPermanentPlayedInsight && t.OncePerTurn == true && t.RevealCount is > 0);

    internal static int BattlefieldFirstPermanentManaReduction(MatchState state, string player,
        CardBehaviorDefinition behavior, string? destination)
    {
        if (state.TurnPlayerId != player || !behavior.PlaysSourceToBaseAsUnit && !behavior.PlaysSourceToBaseAsEquipment)
            return 0;
        return state.BattlefieldStates.Keys
            .Where(id => destination is null || destination == "BATTLEFIELD:" + id)
            .Where(id => !state.UntilEndOfTurnEffects.Contains(BattlefieldPermanentPlayUse(player, id), StringComparer.Ordinal))
            .Select(id => BattlefieldLocalRules.Battlefield(state, id))
            .Select(card => card is not null && BattlefieldStaticAbilitySpecRules.TryGetAbility(card.CardNo,
                a => a.Kind == StaticAbilityKinds.BattlefieldFirstPermanentPlayManaReduction && a.Amount > 0, out var spec)
                    ? spec.Amount : 0).DefaultIfEmpty().Max();
    }
}
