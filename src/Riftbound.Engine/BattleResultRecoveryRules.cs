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
        if (c.Stage is not ("RESULT_TRIGGERS" or "CONTROL_TRIGGERS") || c.ResultTick <= 0 || c.ResultTick > state.Tick
            || c.AssignedOverkillDamage < 0 || string.IsNullOrWhiteSpace(c.BattlefieldId)
            || !state.Seats.ContainsKey(c.AttackingPlayerId) || c.DefendingPlayerId is null
            || !state.Seats.ContainsKey(c.DefendingPlayerId) || c.AttackingPlayerId == c.DefendingPlayerId
            || c.WinnerPlayerId != c.AttackingPlayerId && c.WinnerPlayerId != c.DefendingPlayerId
            || c.AttackerObjectIds is null || c.DefenderObjectIds is null || c.SurvivingAttackerObjectIds is null
            || c.SurvivingDefenderObjectIds is null || c.DestroyedObjectIds is null || c.RelatedEventKinds is null
            || c.PublicObjectBindings is null || c.AttackerObjectIds.Count == 0 || c.DefenderObjectIds.Count == 0) return false;
        var all = c.AttackerObjectIds.Concat(c.DefenderObjectIds).ToArray();
        return all.All(id => !string.IsNullOrWhiteSpace(id)) && all.Distinct(StringComparer.Ordinal).Count() == all.Length
            && c.SurvivingAttackerObjectIds.All(c.AttackerObjectIds.Contains)
            && c.SurvivingDefenderObjectIds.All(c.DefenderObjectIds.Contains)
            && c.DestroyedObjectIds.All(all.Contains)
            && (c.WinnerPlayerId == c.AttackingPlayerId
                ? c.SurvivingAttackerObjectIds.Count > 0 && c.SurvivingDefenderObjectIds.Count == 0
                : c.SurvivingDefenderObjectIds.Count > 0 && c.SurvivingAttackerObjectIds.Count == 0)
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
        var errors = new List<string>();
        if (!ValidContinuation(state)) return ["invalid captured battle result continuation"];
        if (state.PendingBattleResult is { } continuation)
        {
            var origins = frame.Events.Where(e => e.Tick == continuation.ResultTick
                && e.Event.Kind == "BATTLE_RESULT_DETERMINED"
                && Same(e.Event.Payload.GetValueOrDefault("continuation"), continuation with { Stage = "RESULT_TRIGGERS" })).ToArray();
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

    private static bool Same(object? a, object? b) => a is not null && b is not null
        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a, a.GetType(), Json), JsonSerializer.SerializeToNode(b, b.GetType(), Json));
    private static string? Text(GameEvent e, string key) => e.Payload.GetValueOrDefault(key) switch
    { string value => value, JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(), _ => null };
}
