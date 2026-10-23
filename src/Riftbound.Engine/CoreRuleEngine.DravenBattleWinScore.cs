using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DravenBattleWinScoreContext(ObjectBinding Source, string CardNo, string ControllerId,
    string BattlefieldId, long ResultTick, string Role, int TurnNumber);

public sealed partial class CoreRuleEngine
{
    internal const string DravenBattleWinScoreEffect = "CAPTURED_DRAVEN_FIRST_BATTLE_WIN_SCORE";

    private static bool IsDravenBattleWinScorePrinting(string? cardNo)
        => cardNo is "SFD·148/221" or "SFD·148a/221";

    // This is the first occurrence, not an optional once-per-turn resolution.
    // The result continuation must persist UsedMarker when admitting the trigger,
    // even if the opponent later counters the skill. Control changes do not reset
    // an object's first occurrence; leaving play creates a new generation.
    private sealed record CapturedDravenBattleWinScore(TriggerQueueItemState Trigger,
        DravenBattleWinScoreContext Context, string UsedMarker);

    internal static string DravenBattleWinScoreUsedMarker(DravenBattleWinScoreContext context)
        => $"DRAVEN_FIRST_BATTLE_WIN:{context.TurnNumber}:{context.Source.Generation}:{context.Source.ObjectId}";

    private static IReadOnlyList<CapturedDravenBattleWinScore> CaptureDravenBattleWinScoreObservations(
        MatchState resultState, string battlefieldId, string winnerPlayerId,
        IReadOnlyList<string> attackerObjectIds, IReadOnlyList<string> defenderObjectIds, long resultTick)
    {
        var captured = new List<CapturedDravenBattleWinScore>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (ids, role) in new[] { (attackerObjectIds, "ATTACKER"), (defenderObjectIds, "DEFENDER") })
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            if (!seen.Add(id) || !resultState.CardObjects.TryGetValue(id, out var card) || card.IsFaceDown
                || card.ControllerId != winnerPlayerId || !IsDravenBattleWinScorePrinting(card.CardNo)
                || !IsVisibleFieldUnitObject(resultState.CardObjects, id)
                || !resultState.PlayerZones.TryGetValue(winnerPlayerId, out var zones) || !zones.Battlefields.Contains(id)
                || !resultState.ObjectLocations.TryGetValue(id, out var location)
                || location.Zone != "BATTLEFIELD" || location.BattlefieldObjectId != battlefieldId) continue;
            var context = new DravenBattleWinScoreContext(new(id, card.ObjectGeneration), card.CardNo!,
                winnerPlayerId, battlefieldId, resultTick, role, resultState.TurnNumber);
            var marker = DravenBattleWinScoreUsedMarker(context);
            if (resultState.UntilEndOfTurnEffects.Contains(marker, StringComparer.Ordinal)) continue;
            var trigger = new TriggerQueueItemState($"draven-first-win-{resultTick}-{battlefieldId}-{id}",
                winnerPlayerId, id, DravenBattleWinScoreEffect, "BATTLE_RESULT_DETERMINED")
            {
                SourceCardNo = card.CardNo, EnqueuedAtTick = resultTick
            };
            captured.Add(new(trigger, context, marker));
        }
        return captured;
    }

    // Historical result and first-occurrence authenticity belong to shared recovery.
    internal static bool HasValidDravenBattleWinScoreShape(DravenBattleWinScoreContext? context)
        => context is not null && context.Source is not null && context.Source.Generation >= 0
            && !string.IsNullOrWhiteSpace(context.Source.ObjectId) && IsDravenBattleWinScorePrinting(context.CardNo)
            && !string.IsNullOrWhiteSpace(context.ControllerId) && !string.IsNullOrWhiteSpace(context.BattlefieldId)
            && context.ResultTick > 0 && context.TurnNumber > 0 && context.Role is "ATTACKER" or "DEFENDER";

    private static StackResolutionResult ResolveDravenBattleWinScore(MatchState state, StackItemState item,
        DravenBattleWinScoreContext context)
    {
        if (!HasValidDravenBattleWinScoreShape(context) || context.ResultTick > state.Tick
            || context.TurnNumber != state.TurnNumber || item.EffectKind != DravenBattleWinScoreEffect
            || item.SourceObjectId != context.Source.ObjectId || item.ControllerId != context.ControllerId
            || item.CardNo != context.CardNo)
            throw new InvalidOperationException("Invalid captured Draven first battle-win score.");

        var scores = NormalizeScoresForSeats(state);
        scores[context.ControllerId] += 1;
        var events = new List<GameEvent>
        {
            new("UNIT_TRIGGER_RESOLVED", "德莱文本回合首次赢得战斗，获得一分", new Dictionary<string, object?>
            {
                ["playerId"] = context.ControllerId, ["sourceObjectId"] = context.Source.ObjectId,
                ["sourceCardNo"] = context.CardNo, ["battlefieldId"] = context.BattlefieldId,
                ["trigger"] = DravenBattleWinScoreEffect
            }),
            new("SCORE_GAINED", "德莱文赢得战斗获得一分", new Dictionary<string, object?>
            {
                ["playerId"] = context.ControllerId, ["sourceObjectId"] = context.Source.ObjectId,
                ["amount"] = 1, ["score"] = scores[context.ControllerId], ["reason"] = DravenBattleWinScoreEffect
            })
        };
        // Shared parent completion checks normal victory before cleanup and before
        // the pending control/conquest continuation. This is not a conquest point.
        return NoopStackResolutionResult(state) with { PlayerScores = scores, Events = events };
    }
}
