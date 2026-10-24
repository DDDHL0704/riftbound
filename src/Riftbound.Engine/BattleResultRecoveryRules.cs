using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;

namespace Riftbound.Engine;

// Independent persisted-shape and original-journal checks. Current source
// control is deliberately not the authority for an already captured skill.
internal static class BattleResultRecoveryRules
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string UnitDraw = "CAPTURED_UNIT_BATTLE_WIN_DRAW";
    private const string LegendDraw = "CAPTURED_LEGEND_BATTLE_WIN_DRAW";
    private const string Experience = "CAPTURED_KHAZIX_BATTLE_WIN_EXPERIENCE";
    private const string FirstWinScore = "CAPTURED_DRAVEN_FIRST_BATTLE_WIN_SCORE";

    internal static bool ValidContinuation(MatchState state)
    {
        if (state.PendingBattleResult is not { } c) return true;
        if (c.Stage is not ("CLEANUP_TRIGGERS" or "RESULT_TRIGGERS" or "CONTROL_TRIGGERS") || c.ResultTick <= 0 || c.ResultTick > state.Tick
            || c.AssignedOverkillDamage < 0 || string.IsNullOrWhiteSpace(c.BattlefieldId)
            || !state.Seats.ContainsKey(c.AttackingPlayerId) || c.DefendingPlayerId is null
            || !state.Seats.ContainsKey(c.DefendingPlayerId) || c.AttackingPlayerId == c.DefendingPlayerId
            || c.WinnerPlayerId is not null && c.WinnerPlayerId != c.AttackingPlayerId && c.WinnerPlayerId != c.DefendingPlayerId
            || c.AttackerObjectIds is null || c.DefenderObjectIds is null || c.SurvivingAttackerObjectIds is null
            || c.SurvivingDefenderObjectIds is null || c.DestroyedObjectIds is null || c.RelatedEventKinds is null
            || c.PublicObjectBindings is null || c.AttackerObjectIds.Count == 0 || c.DefenderObjectIds.Count == 0) return false;
        var all = c.AttackerObjectIds.Concat(c.DefenderObjectIds).ToArray();
        return all.All(id => !string.IsNullOrWhiteSpace(id)) && all.Distinct(StringComparer.Ordinal).Count() == all.Length
            && c.SurvivingAttackerObjectIds.All(c.AttackerObjectIds.Contains)
            && c.SurvivingDefenderObjectIds.All(c.DefenderObjectIds.Contains)
            && c.DestroyedObjectIds.All(all.Contains)
            && (c.Stage == "CLEANUP_TRIGGERS" ? c.WinnerPlayerId is null : c.WinnerPlayerId is null
                ? c.AttackerRecalledByBattleCleanup || c.SurvivingAttackerObjectIds.Count == 0 && c.SurvivingDefenderObjectIds.Count == 0
                    || c.SurvivingAttackerObjectIds.Count > 0 && c.SurvivingDefenderObjectIds.Count > 0
                : c.WinnerPlayerId == c.AttackingPlayerId
                ? c.SurvivingAttackerObjectIds.Count > 0 && c.SurvivingDefenderObjectIds.Count == 0
                : c.SurvivingDefenderObjectIds.Count > 0 && c.SurvivingAttackerObjectIds.Count == 0)
            && (!c.AttackerRecalledByBattleCleanup || c.WinnerPlayerId is null)
            && c.PublicObjectBindings.All(b => b is not null && b.Generation >= 0 && all.Contains(b.ObjectId));
    }

    internal static bool ValidSkill(MatchState state, string effect, string source, string controller, string? cardNo,
        UnitBattleWinDrawContext? unit, LegendBattleWinDrawContext? legend, KhazixBattleWinContext? khazix, DravenBattleWinScoreContext? score)
    {
        var count = (unit is null ? 0 : 1) + (legend is null ? 0 : 1) + (khazix is null ? 0 : 1) + (score is null ? 0 : 1);
        if (count == 0) return effect is not (UnitDraw or LegendDraw or Experience or FirstWinScore);
        if (count != 1 || !ValidContinuation(state) || state.PendingBattleResult is not { } c) return false;
        var binding = unit?.Source ?? legend?.Source ?? khazix?.Source ?? score!.Source;
        var number = unit?.CardNo ?? legend?.CardNo ?? khazix?.CardNo ?? score!.CardNo;
        var actor = unit?.ControllerId ?? legend?.ControllerId ?? khazix?.ControllerId ?? score!.ControllerId;
        var field = unit?.BattlefieldId ?? legend?.BattlefieldId ?? khazix?.BattlefieldId ?? score!.BattlefieldId;
        var tick = unit?.ResultTick ?? legend?.ResultTick ?? khazix?.ResultTick ?? score!.ResultTick;
        var role = unit?.Role ?? legend?.Role ?? khazix?.Role ?? score!.Role;
        if (binding is null || binding.Generation < 0 || binding.ObjectId != source || actor != controller
            || cardNo is not null && number != cardNo || actor != c.WinnerPlayerId || field != c.BattlefieldId
            || tick != c.ResultTick || role != (actor == c.AttackingPlayerId ? "ATTACKER" : "DEFENDER")) return false;
        return effect switch
        {
            UnitDraw => unit is not null && number is "UNL-114/219" or "SFD·123/221"
                && (role == "ATTACKER" ? c.SurvivingAttackerObjectIds : c.SurvivingDefenderObjectIds).Contains(source),
            LegendDraw => legend is not null && number is "SFD·185/221" or "SFD·242/221",
            Experience => khazix is not null && number is "UNL-201/219" or "UNL-236/219" or "UNL-236*/219",
            FirstWinScore => score is not null && CoreRuleEngine.HasValidDravenBattleWinScoreShape(score)
                && score.TurnNumber == state.TurnNumber
                && state.UntilEndOfTurnEffects.Contains(CoreRuleEngine.DravenBattleWinScoreUsedMarker(score), StringComparer.Ordinal)
                && (role == "ATTACKER" ? c.SurvivingAttackerObjectIds : c.SurvivingDefenderObjectIds).Contains(source),
            _ => false
        };
    }

    internal static bool Valid(MatchState state, StackItemState item) => ValidSkill(state, item.EffectKind,
        item.SourceObjectId, item.ControllerId, item.CardNo, item.UnitBattleWinDraw, item.LegendBattleWinDraw, item.KhazixBattleWin, item.DravenBattleWinScore);
    internal static bool Valid(MatchState state, TriggerQueueItemState item) => ValidSkill(state, item.EffectKind,
        item.SourceObjectId, item.ControllerId, item.SourceCardNo, item.UnitBattleWinDraw, item.LegendBattleWinDraw, item.KhazixBattleWin, item.DravenBattleWinScore);
    internal static bool IsCapturedSkill(MatchState state, StackItemState item)
        => (item.UnitBattleWinDraw is not null || item.LegendBattleWinDraw is not null || item.KhazixBattleWin is not null || item.DravenBattleWinScore is not null) && Valid(state, item);
    internal static bool IsCapturedSkill(MatchState state, TriggerQueueItemState item)
        => (item.UnitBattleWinDraw is not null || item.LegendBattleWinDraw is not null || item.KhazixBattleWin is not null || item.DravenBattleWinScore is not null) && Valid(state, item);

    internal static IReadOnlyList<string> ValidateOriginalTriggers(MatchRecoveryFrame frame)
    {
        if (frame.AuthoritativeState is not { } state) return [];
        var errors = ValidateClosedCleanupRecalls(state, frame.Commands, frame.Events).ToList();
        if (!ValidContinuation(state)) return ["invalid captured battle result continuation"];
        if (state.PendingBattleResult is { } continuation)
        {
            var cleanup = continuation.Stage == "CLEANUP_TRIGGERS";
            var origins = frame.Events.Where(e => e.Tick == continuation.ResultTick
                && e.Event.Kind == (cleanup ? "BATTLE_CLEANUP_COMPLETED" : "BATTLE_RESULT_DETERMINED")
                && Same(e.Event.Payload.GetValueOrDefault("continuation"), continuation with { Stage = cleanup ? "CLEANUP_TRIGGERS" : "RESULT_TRIGGERS" })).ToArray();
            if (origins.Length != 1 || !HasOriginalCommand(origins.SingleOrDefault()))
                errors.Add("battle result continuation differs from its original accepted result event");
        }
        foreach (var item in state.TriggerQueue)
            Check(item.TriggerId, item.EffectKind, item.SourceObjectId, item.ControllerId, item.SourceCardNo,
                item.UnitBattleWinDraw, item.LegendBattleWinDraw, item.KhazixBattleWin, item.DravenBattleWinScore);
        foreach (var item in state.StackItems)
            Check(item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal) ? item.StackItemId[8..] : "",
                item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo,
                item.UnitBattleWinDraw, item.LegendBattleWinDraw, item.KhazixBattleWin, item.DravenBattleWinScore);
        return errors;

        bool HasOriginalCommand(RecoveredEvent? ev) => ev is not null && frame.Commands.Count(c => c.Accepted
            && c.CompletedTick == ev.Tick && c.StartedEventSequence < ev.Sequence && c.CompletedEventSequence >= ev.Sequence) == 1;
        void Check(string id, string effect, string source, string actor, string? cardNo,
            UnitBattleWinDrawContext? unit, LegendBattleWinDrawContext? legend, KhazixBattleWinContext? khazix, DravenBattleWinScoreContext? score)
        {
            if (!ValidSkill(state, effect, source, actor, cardNo, unit, legend, khazix, score))
            { errors.Add("invalid captured battle-result skill"); return; }
            object? context = (object?)unit ?? (object?)legend ?? (object?)khazix ?? score;
            if (context is null) return;
            var origins = frame.Events.Where(e => e.Event.Kind == "TRIGGER_QUEUED"
                && Text(e.Event, "triggerId") == id).ToArray();
            if (origins.Length != 1 || !HasOriginalCommand(origins.SingleOrDefault())
                || Text(origins[0].Event, "sourceObjectId") != source || Text(origins[0].Event, "controllerId") != actor
                || Text(origins[0].Event, "effectKind") != effect
                || !Same(origins[0].Event.Payload.GetValueOrDefault("battleWinContext"), context))
                errors.Add("captured battle-result skill differs from its original trigger birth");
        }
    }

    internal static IReadOnlyList<string> ValidateClosedCleanupRecalls(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events)
    {
        var errors = new List<string>();
        foreach (var h in state.BattleResolutions ?? [])
        {
            if (h?.Reason != CoreRuleEngine.CleanupRecallNoResultReason) continue;
            var closes = events.Where(e => e.Event.Kind == "BATTLE_CLOSED" && e.Tick == h.Tick
                && Text(e.Event, "battlefieldId") == h.BattlefieldId).ToArray();
            var close = closes.Length == 1 ? closes[0] : null;
            var origin = close is null ? default : Latest("BATTLE_RESULT_DETERMINED", close.Sequence, h.BattlefieldId);
            var cleanup = origin.Event is null ? default : Latest("BATTLE_CLEANUP_COMPLETED", origin.Event.Sequence, h.BattlefieldId);
            var c = origin.Context; var start = cleanup.Context;
            var previousClose = cleanup.Event is null ? 0 : events.Where(e => e.Event.Kind == "BATTLE_CLOSED"
                && e.Sequence < cleanup.Event.Sequence && Text(e.Event, "battlefieldId") == h.BattlefieldId)
                .Select(e => e.Sequence).DefaultIfEmpty(0).Max();
            var recalled = start is not null && start.AttackerObjectIds is not null && cleanup.Event is not null
                && events.Any(e => e.Sequence > previousClose && e.Sequence < cleanup.Event.Sequence
                    && e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Text(e.Event, "battlefieldId") == h.BattlefieldId
                    && Text(e.Event, "reason") == CoreRuleEngine.CleanupRecallNoResultReason
                    && start.AttackerObjectIds.Contains(Text(e.Event, "targetObjectId") ?? "") && Accepted(e));
            if (h.Kind != "NO_RESULT" || h.WinnerPlayerId is not null || !Accepted(close) || !Accepted(origin.Event)
                || !Accepted(cleanup.Event) || !recalled || start is null || !start.AttackerRecalledByBattleCleanup
                || start.Stage != "CLEANUP_TRIGGERS" || start.AttackingPlayerId != h.AttackingPlayerId
                || start.DefendingPlayerId != h.DefendingPlayerId || c is null || !c.AttackerRecalledByBattleCleanup
                || c.Stage != "RESULT_TRIGGERS" || c.WinnerPlayerId is not null || c.ResultTick != origin.Event!.Tick
                || c.ResultTick > h.Tick || c.AttackingPlayerId != h.AttackingPlayerId || c.DefendingPlayerId != h.DefendingPlayerId
                || !Same(c.AttackerObjectIds, h.AttackerObjectIds) || !Same(c.DefenderObjectIds, h.DefenderObjectIds)
                || !Same(c.SurvivingAttackerObjectIds, h.SurvivingAttackerObjectIds)
                || !Same(c.SurvivingDefenderObjectIds, h.SurvivingDefenderObjectIds)
                || !Same(c.DestroyedObjectIds, h.DestroyedObjectIds) || !Same(c.PublicObjectBindings, h.PublicObjectBindings))
                errors.Add("closed cleanup-recall history differs from its original accepted result");
        }
        return errors;

        bool Accepted(RecoveredEvent? e) => e is not null && commands.Count(c => c.Accepted
            && c.CompletedTick == e.Tick && c.StartedEventSequence < e.Sequence && e.Sequence <= c.CompletedEventSequence) == 1;
        (RecoveredEvent? Event, BattleResultContinuation? Context) Latest(string kind, long before, string field)
            => events.Where(e => e.Event.Kind == kind && e.Sequence < before)
                .Select(e => (Event: (RecoveredEvent?)e, Context: Read(e)))
                .Where(x => x.Context?.BattlefieldId == field).OrderByDescending(x => x.Event!.Sequence).FirstOrDefault();
        BattleResultContinuation? Read(RecoveredEvent e)
        {
            try {
                var value = e.Event.Payload.GetValueOrDefault("continuation");
                return value as BattleResultContinuation ?? JsonSerializer.SerializeToElement(value, Json).Deserialize<BattleResultContinuation>(Json);
            }
            catch (Exception error) when (error is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
            { return null; }
        }
    }

    private static bool Same(object? a, object? b) => a is not null && b is not null
        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a, a.GetType(), Json), JsonSerializer.SerializeToNode(b, b.GetType(), Json));
    private static string? Text(GameEvent e, string key) => e.Payload.GetValueOrDefault(key) switch
    { string value => value, JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(), _ => null };
}
