using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;

namespace Riftbound.Engine;

// Validate persisted observations independently of their producer. An earned
// death skill refers to the departed incarnation, never its current controller.
internal static class DravenBattleDeathRecoveryRules
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Effect = "CAPTURED_DRAVEN_BATTLE_DEATH_OPPONENT_SCORE";

    private static bool ValidContext(MatchState state, DravenBattleDeathScoreContext? c,
        string effect, string source, string controller, string? cardNo)
    {
        if (c is null) return effect != Effect;
        return effect == Effect && c.Source is { Generation: >= 0 }
            && !string.IsNullOrWhiteSpace(c.Source.ObjectId) && c.Source.ObjectId == source
            && c.CardNo is "SFD·148/221" or "SFD·148a/221" && c.CardNo == cardNo
            && c.ControllerId == controller && state.Seats.ContainsKey(controller)
            && !string.IsNullOrWhiteSpace(c.BattlefieldId) && !string.IsNullOrWhiteSpace(c.DeathId)
            && c.DeathTick > 0 && c.DeathTick <= state.Tick && c.Role is "ATTACKER" or "DEFENDER"
            && (c.OpponentId is null || c.OpponentId != controller && state.Seats.ContainsKey(c.OpponentId));
    }

    internal static bool Valid(MatchState state, StackItemState item)
        => ValidContext(state, item.DravenBattleDeathScore, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
            && (item.DravenBattleDeathScore is not { } c || item.StackItemId == "ordered-" + Id(c)
                && item.UnitBattleWinDraw is null && item.LegendBattleWinDraw is null && item.KhazixBattleWin is null
                && item.DravenBattleWinScore is null && item.DeathObserver is null && item.TargetObjectIds.Count == 0);

    internal static bool Valid(MatchState state, TriggerQueueItemState item)
        => ValidContext(state, item.DravenBattleDeathScore, item.EffectKind, item.SourceObjectId, item.ControllerId, item.SourceCardNo)
            && (item.DravenBattleDeathScore is not { } c || c.OpponentId is null && item.TriggerId == Id(c)
                && item.EnqueuedAtTick == c.DeathTick && item.TriggeredByEventKind == "UNIT_DESTROYED"
                && item.UnitBattleWinDraw is null && item.LegendBattleWinDraw is null && item.KhazixBattleWin is null
                && item.DravenBattleWinScore is null && item.DeathObserver is null);

    internal static bool IsCapturedSkill(MatchState state, StackItemState item) => item.DravenBattleDeathScore is not null && Valid(state, item);
    internal static bool IsCapturedSkill(MatchState state, TriggerQueueItemState item) => item.DravenBattleDeathScore is not null && Valid(state, item);

    internal static bool ValidChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        var legal = state.Seats.OrderBy(p => p.Value, StringComparer.Ordinal).Select(p => p.Key)
            .Where(p => p != choice.PlayerId).ToArray();
        return item is { DravenBattleDeathScore.OpponentId: null } && Valid(state, item)
            && choice.ChoiceWindow == CoreRuleEngine.DravenDeathOpponentWindow
            && choice.ChoiceId == "DRAVEN-DEATH:" + item.StackItemId && choice.EffectKind == Effect
            && choice.PlayerId == item.ControllerId && choice.SourceObjectId == item.SourceObjectId
            && choice.RequiredCount == 1 && choice.MaxCount == 1 && choice.LegalObjectIds.SequenceEqual(legal)
            && choice.ContextObjectIds.SequenceEqual([item.SourceObjectId])
            && state.PriorityPlayerId is null && state.ActivePlayerId == item.ControllerId;
    }

    internal static IReadOnlyList<string> ValidateOriginalTriggers(MatchRecoveryFrame frame)
    {
        if (frame.AuthoritativeState is not { } state) return [];
        var errors = new List<string>();
        foreach (var item in state.TriggerQueue)
        {
            if (!Valid(state, item)) errors.Add("invalid queued Draven battle-death skill");
            else if (item.DravenBattleDeathScore is { } c) Check(c, item.TriggerId, null);
        }
        foreach (var item in state.StackItems)
        {
            if (!Valid(state, item)) errors.Add("invalid stacked Draven battle-death skill");
            else if (item.DravenBattleDeathScore is { } c) Check(c, item.StackItemId[8..], item.StackItemId);
        }
        return errors;

        RecoveredCommand? Command(RecoveredEvent ev)
        {
            var matches = frame.Commands.Where(c => c.Accepted && c.CompletedTick == ev.Tick
                && c.StartedEventSequence < ev.Sequence && c.CompletedEventSequence >= ev.Sequence).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        void Check(DravenBattleDeathScoreContext c, string triggerId, string? stackId)
        {
            var births = frame.Events.Where(e => e.Event.Kind == "TRIGGER_QUEUED" && Text(e.Event, "triggerId") == triggerId).ToArray();
            if (births.Length != 1 || Command(births[0]) is not { } original || births[0].Tick != c.DeathTick
                || Text(births[0].Event, "controllerId") != c.ControllerId || Text(births[0].Event, "sourceObjectId") != c.Source.ObjectId
                || Text(births[0].Event, "effectKind") != Effect
                || !Same(births[0].Event.Payload.GetValueOrDefault("dravenBattleDeathScore"), c with { OpponentId = null }))
            { errors.Add("Draven battle-death skill differs from its original trigger birth"); return; }

            var deaths = frame.Events.Where(e => e.Event.Kind == "UNIT_DESTROYED"
                && e.Sequence > original.StartedEventSequence && e.Sequence < births[0].Sequence
                && Text(e.Event, "deathId") == c.DeathId && Text(e.Event, "targetObjectId") == c.Source.ObjectId).ToArray();
            var fact = deaths.Length == 1 ? Read<DestroyedUnitFact>(deaths[0].Event.Payload.GetValueOrDefault("destroyedUnitFact")) : null;
            if (fact is null || fact.ObjectId != c.Source.ObjectId || fact.Generation != c.Source.Generation
                || fact.CardNo != c.CardNo || fact.ControllerId != c.ControllerId)
                errors.Add("Draven battle-death skill differs from its original destroyed incarnation");

            var roles = frame.Events.LastOrDefault(e => e.Sequence < births[0].Sequence && e.Event.Kind == "BATTLE_ROLES_ESTABLISHED"
                && Text(e.Event, "battlefieldId") == c.BattlefieldId);
            if (roles is null || Command(roles) is null
                || c.Role != (Text(roles.Event, "attackingPlayerId") == c.ControllerId ? "ATTACKER" : "DEFENDER")
                || frame.Events.Any(e => e.Sequence > roles.Sequence && e.Sequence < births[0].Sequence
                    && e.Event.Kind == "BATTLE_CLOSED" && Text(e.Event, "battlefieldId") == c.BattlefieldId))
                errors.Add("Draven battle-death skill differs from its original active battle");

            var choices = stackId is null ? [] : frame.Events.Where(e => e.Event.Kind == "DRAVEN_DEATH_OPPONENT_CONFIRMED"
                && Text(e.Event, "stackItemId") == stackId).ToArray();
            if (c.OpponentId is null ? choices.Length != 0 : choices.Length != 1 || Command(choices[0]) is not { } choiceCommand
                || choiceCommand.PlayerId != c.ControllerId || choices[0].Sequence <= births[0].Sequence
                || Text(choices[0].Event, "controllerId") != c.ControllerId || Text(choices[0].Event, "opponentId") != c.OpponentId
                || Text(choices[0].Event, "sourceObjectId") != c.Source.ObjectId)
                errors.Add("Draven battle-death opponent differs from its original confirmation");
        }
    }

    private static string Id(DravenBattleDeathScoreContext c) => $"draven-death-score-{c.DeathId}-{c.Source.ObjectId}";
    private static T? Read<T>(object? value) where T : class
    {
        if (value is null) return null;
        try { return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, value.GetType(), Json), Json); }
        catch (JsonException) { return null; }
    }
    private static bool Same(object? a, object? b) => a is not null && b is not null
        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a, a.GetType(), Json), JsonSerializer.SerializeToNode(b, b.GetType(), Json));
    private static string? Text(GameEvent e, string key) => e.Payload.GetValueOrDefault(key) switch
    { string value => value, JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(), _ => null };
}
