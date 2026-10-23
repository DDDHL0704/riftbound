using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KhazixBattleWinContext(ObjectBinding Source, string CardNo, string ControllerId,
    string BattlefieldId, long ResultTick, string Role);

public sealed partial class CoreRuleEngine
{
    internal const string KhazixBattleWinEffect = "CAPTURED_KHAZIX_BATTLE_WIN_EXPERIENCE";

    private static bool IsKhazixBattleWinPrinting(string? cardNo)
        => cardNo is "UNL-201/219" or "UNL-236/219" or "UNL-236*/219";

    // The shared result continuation supplies the actual winner and role, and
    // owns admission, serialization and recovery authentication of this context.
    private sealed record CapturedKhazixBattleWin(TriggerQueueItemState Trigger, KhazixBattleWinContext Context);

    private static IReadOnlyList<CapturedKhazixBattleWin> CaptureKhazixBattleWinObservations(
        MatchState resultState, string battlefieldId, string winnerPlayerId, string role, long resultTick)
    {
        if (role is not ("ATTACKER" or "DEFENDER")
            || !resultState.PlayerZones.TryGetValue(winnerPlayerId, out var zones)) return [];
        var captured = new List<CapturedKhazixBattleWin>();
        foreach (var id in zones.LegendZone.Distinct(StringComparer.Ordinal))
        {
            if (!resultState.CardObjects.TryGetValue(id, out var card) || card.IsFaceDown
                || card.ControllerId != winnerPlayerId || !card.Tags.Contains("CARD_TYPE:LEGEND", StringComparer.Ordinal)
                || !IsKhazixBattleWinPrinting(card.CardNo)) continue;
            var context = new KhazixBattleWinContext(new(id, card.ObjectGeneration), card.CardNo!,
                winnerPlayerId, battlefieldId, resultTick, role);
            var trigger = new TriggerQueueItemState($"khazix-battle-win-{resultTick}-{battlefieldId}-{id}",
                winnerPlayerId, id, KhazixBattleWinEffect, "BATTLE_RESULT_DETERMINED")
            {
                SourceCardNo = card.CardNo, EnqueuedAtTick = resultTick
            };
            captured.Add(new(trigger, context));
        }
        return captured;
    }

    // Shape validation does not establish that the historical battle occurred.
    internal static bool HasValidKhazixBattleWinShape(KhazixBattleWinContext? context)
        => context is not null && context.Source is not null && context.Source.Generation >= 0
            && !string.IsNullOrWhiteSpace(context.Source.ObjectId) && IsKhazixBattleWinPrinting(context.CardNo)
            && !string.IsNullOrWhiteSpace(context.ControllerId) && !string.IsNullOrWhiteSpace(context.BattlefieldId)
            && context.ResultTick > 0 && context.Role is "ATTACKER" or "DEFENDER";

    private static StackResolutionResult ResolveKhazixBattleWin(MatchState state, StackItemState item,
        KhazixBattleWinContext context)
    {
        if (!HasValidKhazixBattleWinShape(context) || context.ResultTick > state.Tick
            || item.EffectKind != KhazixBattleWinEffect || item.SourceObjectId != context.Source.ObjectId
            || item.ControllerId != context.ControllerId || item.CardNo != context.CardNo)
            throw new InvalidOperationException("Invalid captured Kha'Zix battle-win experience.");

        // A triggered ability keeps its captured controller if the source later
        // leaves play, changes generation or changes controller during responses.
        var events = new List<GameEvent>
        {
            new("LEGEND_TRIGGER_RESOLVED", "虚空掠夺者赢得战斗，获得经验", new Dictionary<string, object?>
            {
                ["playerId"] = context.ControllerId, ["legendObjectId"] = context.Source.ObjectId,
                ["legendCardNo"] = context.CardNo, ["battlefieldId"] = context.BattlefieldId,
                ["trigger"] = KhazixBattleWinEffect
            })
        };
        var experience = GainExperience(state.PlayerExperience, context.ControllerId, 1, item, events);
        return NoopStackResolutionResult(state) with
        {
            PlayerExperience = experience, Events = events,
            UntilEndOfTurnEffects = MarkPlayersWhoGainedExperienceThisTurn(state.UntilEndOfTurnEffects, events)
        };
    }
}
