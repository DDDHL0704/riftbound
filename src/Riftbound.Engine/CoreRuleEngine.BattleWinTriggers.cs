using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnitBattleWinDrawContext(ObjectBinding Source, string CardNo, string ControllerId,
    string BattlefieldId, long ResultTick, string Role, bool ObservesBattlefield = false,
    int DrawCount = 1, RuleTextInstanceBinding? Origin = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int GoldCount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TriggerIndex { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string UnitBattleWinDrawEffect = "CAPTURED_UNIT_BATTLE_WIN_DRAW";

    // Kept for the runtime inventory's legacy draw classification. Admission and
    // resolution below both consume the exact declarative reward specification.
    private static bool IsUnitBattleWinDrawPrinting(string? cardNo)
        => UnitBattleWinTriggerSpecRules.Rewards(cardNo).Any(UnitBattleWinTriggerSpecRules.IsUnitBattleWinDrawTrigger);

    private sealed record CapturedUnitBattleWinDraw(TriggerQueueItemState Trigger, UnitBattleWinDrawContext Context);

    private static IReadOnlyList<CapturedUnitBattleWinDraw> CaptureUnitBattleWinDrawObservations(
        MatchState capturedParticipantsState, string battlefieldId, string winnerPlayerId,
        IReadOnlyList<string> attackerObjectIds, IReadOnlyList<string> defenderObjectIds, long enqueueTick)
    {
        var state = capturedParticipantsState;
        var result = new List<CapturedUnitBattleWinDraw>();
        foreach (var (ids, role) in new[] { (attackerObjectIds, "ATTACKER"), (defenderObjectIds, "DEFENDER") })
        {
            foreach (var id in ids.Distinct(StringComparer.Ordinal))
            {
                // CN468.3.c: only actual surviving participants inherit the
                // controller's result, before control/conquest is established.
                if (!state.CardObjects.TryGetValue(id, out var card) || card.IsFaceDown
                    || card.ControllerId != winnerPlayerId || !IsVisibleFieldUnitObject(state.CardObjects, id)
                    || !state.PlayerZones.TryGetValue(winnerPlayerId, out var zones) || !zones.Battlefields.Contains(id)
                    || !state.ObjectLocations.TryGetValue(id, out var location)
                    || location.Zone != "BATTLEFIELD" || location.BattlefieldObjectId != battlefieldId) continue;
                foreach (var instance in AttachedRuleTextRules.Instances(state, card))
                {
                    var rewards = UnitBattleWinTriggerSpecRules.Rewards(instance.CardNo);
                    for (var index = 0; index < rewards.Count; index++)
                    {
                        var spec = rewards[index];
                        var context = new UnitBattleWinDrawContext(new(id, card.ObjectGeneration), instance.CardNo,
                            winnerPlayerId, battlefieldId, enqueueTick, role,
                            DrawCount: spec.DrawCount.GetValueOrDefault(), Origin: instance.Origin) {
                            GoldCount = spec.CreatedTokenCount.GetValueOrDefault(), TriggerIndex = index };
                        var suffix = instance.Origin is null && index == 0 ? "" : $"-{instance.Id}-{index}";
                        var trigger = new TriggerQueueItemState($"unit-battle-win-draw-{enqueueTick}-{battlefieldId}-{id}" + suffix,
                            winnerPlayerId, id, UnitBattleWinDrawEffect, "BATTLE_RESULT_DETERMINED") {
                            SourceCardNo = instance.CardNo, EnqueuedAtTick = enqueueTick };
                        result.Add(new(trigger, context));
                    }
                }
            }
        }
        return result;
    }

    private static IReadOnlyList<CapturedUnitBattleWinDraw> CaptureBattlefieldWinDrawObservations(
        MatchState state, string battlefieldId, string winner, string role, long tick)
    {
        var result = new List<CapturedUnitBattleWinDraw>();
        foreach (var card in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (card.IsFaceDown || EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) != winner
                || !IsObjectOnField(state.PlayerZones, card.ObjectId)
                || !(IsVisibleFieldUnitObject(state.CardObjects, card.ObjectId) || card.Tags.Contains(CardObjectTags.EquipmentCard))
                || !state.ObjectLocations.TryGetValue(card.ObjectId, out var location)
                || location.Zone != "BATTLEFIELD" || location.BattlefieldObjectId != battlefieldId) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, card))
            {
                if (!CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var definition)
                    || definition.BattlefieldWinDrawCount <= 0) continue;
                var context = new UnitBattleWinDrawContext(new(card.ObjectId, card.ObjectGeneration), instance.CardNo,
                    winner, battlefieldId, tick, role, ObservesBattlefield: true,
                    DrawCount: definition.BattlefieldWinDrawCount, Origin: instance.Origin);
                var trigger = new TriggerQueueItemState($"battlefield-win-draw-{tick}-{battlefieldId}-{instance.Id}",
                    winner, card.ObjectId, UnitBattleWinDrawEffect, "BATTLE_RESULT_DETERMINED")
                    { SourceCardNo = instance.CardNo, EnqueuedAtTick = tick };
                result.Add(new(trigger, context));
            }
        }
        return result;
    }

    internal static bool HasValidUnitBattleWinDrawShape(UnitBattleWinDrawContext? context)
        => context is not null && context.Source is not null && context.Source.Generation >= 0
            && !string.IsNullOrWhiteSpace(context.Source.ObjectId)
            && (context.ObservesBattlefield
                ? CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var definition)
                    && definition.BattlefieldWinDrawCount > 0 && context.DrawCount == definition.BattlefieldWinDrawCount
                    && context.GoldCount == 0 && context.TriggerIndex == 0
                : UnitBattleWinTriggerSpecRules.TryGet(context.CardNo, context.TriggerIndex, out var spec)
                    && context.DrawCount == spec.DrawCount.GetValueOrDefault()
                    && context.GoldCount == spec.CreatedTokenCount.GetValueOrDefault())
            && !string.IsNullOrWhiteSpace(context.ControllerId) && !string.IsNullOrWhiteSpace(context.BattlefieldId)
            && context.ResultTick > 0 && context.Role is "ATTACKER" or "DEFENDER";

    private static StackResolutionResult ResolveUnitBattleWinDraw(MatchState state, StackItemState item,
        UnitBattleWinDrawContext context)
    {
        if (!HasValidUnitBattleWinDrawShape(context) || context.ResultTick > state.Tick
            || item.EffectKind != UnitBattleWinDrawEffect || item.SourceObjectId != context.Source.ObjectId
            || item.ControllerId != context.ControllerId || item.CardNo != context.CardNo)
            throw new InvalidOperationException("Invalid captured unit battle-win reward.");
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        var events = new List<GameEvent> { new("UNIT_TRIGGER_RESOLVED", "战斗获胜技能结算",
            new Dictionary<string, object?> {
                ["playerId"] = context.ControllerId, ["sourceObjectId"] = visibleSource,
                ["sourceCardNo"] = context.CardNo, ["battlefieldId"] = context.BattlefieldId,
                ["trigger"] = UnitBattleWinDrawEffect, ["drawCount"] = context.DrawCount,
                ["goldCount"] = context.GoldCount }) };
        // The captured controller keeps the independent reward after source
        // departure, control changes, new incarnations or copied-text removal.
        if (context.GoldCount > 0)
        {
            UnitBattleWinTriggerSpecRules.TryGet(context.CardNo, context.TriggerIndex, out var spec);
            for (var i = 0; i < context.GoldCount; i++)
                CreateLegendEquipmentToken(zones, cards, context.ControllerId, item.SourceObjectId, spec.Kind,
                    spec.CreatedTokenName!, [CardObjectTags.EquipmentCard, spec.CreatedTokenName!],
                    spec.CreatedTokenExhausted == true, events, context.CardNo, publicSourceObjectId: visibleSource);
            return NoopStackResolutionResult(state) with {
                PlayerZones = zones, CardObjects = cards, Events = events,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        }
        var draw = ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, context.ControllerId,
            context.DrawCount, state.RngCursor, events, deferNormalVictory: true);
        return NoopStackResolutionResult(state) with {
            PlayerZones = zones, CardObjects = cards, PlayerScores = draw.PlayerScores, WinnerPlayerId = draw.WinnerPlayerId,
            RngCursor = draw.RngCursor, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
