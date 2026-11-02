using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnitBattleWinDrawContext(ObjectBinding Source, string CardNo, string ControllerId,
    string BattlefieldId, long ResultTick, string Role, bool ObservesBattlefield = false,
    int DrawCount = 1, RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    internal const string UnitBattleWinDrawEffect = "CAPTURED_UNIT_BATTLE_WIN_DRAW";

    // Participating units inherit the actual result. Permanent observers instead
    // watch their current battlefield; they do not have to be combatants.
    private static bool IsUnitBattleWinDrawPrinting(string? cardNo)
        => cardNo is "UNL-114/219" or "SFD·123/221";

    // The shared battle-result continuation owns queue admission and preserves
    // each earned skill's source binding until result skills finish resolving.
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
                // CN 468.3.c: inherit the controller's actual result at this
                // battlefield. A living observer in a base/another field is not
                // a participant; a defeated or departed unit does not win.
                if (!state.CardObjects.TryGetValue(id, out var card)
                    || !IsUnitBattleWinDrawPrinting(card.CardNo) || card.IsFaceDown
                    || card.ControllerId != winnerPlayerId
                    || !IsVisibleFieldUnitObject(state.CardObjects, id)
                    || !state.PlayerZones.TryGetValue(winnerPlayerId, out var zones) || !zones.Battlefields.Contains(id)
                    || !state.ObjectLocations.TryGetValue(id, out var location)
                    || location.Zone != "BATTLEFIELD" || location.BattlefieldObjectId != battlefieldId) continue;
                var context = new UnitBattleWinDrawContext(new(id, card.ObjectGeneration), card.CardNo!,
                    winnerPlayerId, battlefieldId, enqueueTick, role);
                var trigger = new TriggerQueueItemState($"unit-battle-win-draw-{enqueueTick}-{battlefieldId}-{id}",
                    winnerPlayerId, id, UnitBattleWinDrawEffect, "BATTLE_RESULT_DETERMINED")
                {
                    SourceCardNo = card.CardNo, EnqueuedAtTick = enqueueTick
                };
                result.Add(new(trigger, context));
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

    // Structural validation only. The shared recovery pipeline must separately
    // authenticate the captured result, participant binding and original journal.
    internal static bool HasValidUnitBattleWinDrawShape(UnitBattleWinDrawContext? context)
        => context is not null && context.Source is not null && context.Source.Generation >= 0
            && !string.IsNullOrWhiteSpace(context.Source.ObjectId)
            && (context.ObservesBattlefield
                ? CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var definition)
                    && definition.BattlefieldWinDrawCount > 0 && context.DrawCount == definition.BattlefieldWinDrawCount
                : IsUnitBattleWinDrawPrinting(context.CardNo) && context.DrawCount == 1 && context.Origin is null)
            && !string.IsNullOrWhiteSpace(context.ControllerId) && !string.IsNullOrWhiteSpace(context.BattlefieldId)
            && context.ResultTick > 0 && context.Role is "ATTACKER" or "DEFENDER";

    private static StackResolutionResult ResolveUnitBattleWinDraw(MatchState state, StackItemState item,
        UnitBattleWinDrawContext context)
    {
        if (!HasValidUnitBattleWinDrawShape(context) || context.ResultTick > state.Tick
            || item.EffectKind != UnitBattleWinDrawEffect || item.SourceObjectId != context.Source.ObjectId
            || item.ControllerId != context.ControllerId || item.CardNo != context.CardNo)
            throw new InvalidOperationException("Invalid captured unit battle-win draw.");

        // The skill has already triggered. Departure, control changes or a new
        // object generation during responses cannot transfer its draw recipient.
        var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent> { new("UNIT_TRIGGER_RESOLVED", "战斗获胜技能抽牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = context.ControllerId, ["sourceObjectId"] = context.Source.ObjectId,
                ["sourceCardNo"] = context.CardNo, ["battlefieldId"] = context.BattlefieldId,
                ["trigger"] = UnitBattleWinDrawEffect, ["drawCount"] = context.DrawCount
            }) };
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var draw = ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, context.ControllerId,
            context.DrawCount, state.RngCursor, events, deferNormalVictory: true);
        return NoopStackResolutionResult(state) with
        {
            PlayerZones = zones, CardObjects = cards, PlayerScores = draw.PlayerScores, WinnerPlayerId = draw.WinnerPlayerId,
            RngCursor = draw.RngCursor, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones)
        };
    }
}
