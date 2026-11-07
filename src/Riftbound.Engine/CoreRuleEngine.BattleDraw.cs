using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string BattleDrawEvent = "BATTLE_TIED";
    internal const string AttackingBattleDrawTiming = "ATTACKING_BATTLE_DRAW";
    internal const string BattleDrawRecallEffect = "RECALL_BATTLEFIELD_UNITS";

    // CN740.3.a: a tie is observed at cleanup step3d while both players still
    // occupy the battlefield. It is distinct from later no-result outcomes.
    private static void CaptureAttackingBattleDraw(MatchState state, string battlefield,
        string attacker, string defender, List<GameEvent> events)
    {
        var current = state with { ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, state.PlayerZones) };
        if (BattlefieldLocalRules.Battlefield(current, battlefield) is not { } field
            || attacker == defender || !current.Seats.ContainsKey(attacker) || !current.Seats.ContainsKey(defender)
            || !HasParty(attacker) || !HasParty(defender)) return;
        var sources = current.CardObjects.Values.Where(card =>
            EffectiveFieldControllerId(current.PlayerZones, card.ObjectId, card) == attacker).Select(card => card.ObjectId);
        var captured = CaptureUnitEvents(current.PlayerZones, current.CardObjects, sources, AttackingBattleDrawTiming);
        if (captured.Length == 0) return;
        events.Add(new(BattleDrawEvent, "战斗清理时双方仍有单位，触发平局技能",
            new Dictionary<string, object?> { ["battlefieldId"] = battlefield,
                ["attackingPlayerId"] = attacker, ["defendingPlayerId"] = defender }));
        RuleChoices.Value!.InstructionUnitEvents.AddRange(captured.Select(observation => observation with {
            Context = observation.Context with { BattleTieBattlefield = new(field.ObjectId, field.ObjectGeneration) },
            InstructionEventKind = BattleDrawEvent }));

        bool HasParty(string player) => current.CardObjects.Values.Any(card => IsFaceUpNonStandbyUnit(card)
            && IsObjectOnField(current.PlayerZones, card.ObjectId)
            && EffectiveFieldControllerId(current.PlayerZones, card.ObjectId, card) == player
            && current.ObjectLocations.TryGetValue(card.ObjectId, out var at)
            && at.Zone == "BATTLEFIELD" && at.BattlefieldObjectId == battlefield);
    }

    private static bool ValidBattleDrawContext(MatchState state, UnitEventContext context,
        UnitEventAbilitySpec spec, string? controller)
    {
        if (spec.Timing != AttackingBattleDrawTiming) return context.BattleTieBattlefield is null;
        return spec.Effect == BattleDrawRecallEffect && context.BattleTieBattlefield is { Generation: >= 0 } field
            && BattlefieldLocalRules.Battlefield(state, field.ObjectId) is { } current
            && current.ObjectGeneration == field.Generation
            && state.PendingBattleResult is { Stage: "CLEANUP_TRIGGERS" } battle
            && battle.BattlefieldId == field.ObjectId && battle.ResultTick == context.CapturedTick
            && battle.RelatedEventKinds.Contains(BattleDrawEvent, StringComparer.Ordinal)
            && (controller is null || controller == battle.AttackingPlayerId);
    }

    private static StackResolutionResult ResolveBattleDrawRecall(MatchState state, StackItemState item)
    {
        var field = item.UnitEvent!.BattleTieBattlefield!;
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var source = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "召回战场上的单位",
            new Dictionary<string, object?> { ["sourceObjectId"] = source, ["battlefieldId"] = field.ObjectId,
                ["effectKind"] = item.EffectKind }) };
        var units = cards.Values.Where(card => IsFaceUpNonStandbyUnit(card) && IsObjectOnField(zones, card.ObjectId)
            && state.ObjectLocations.TryGetValue(card.ObjectId, out var at)
            && at.Zone == "BATTLEFIELD" && at.BattlefieldObjectId == field.ObjectId)
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
        foreach (var id in units)
        {
            var unit = cards[id]; var controller = EffectiveFieldControllerId(zones, id, unit);
            var owner = unit.OwnerId is { } owned && zones.ContainsKey(owned) ? owned : controller;
            // CN457-460/719: recall the attached group to the unit's own base;
            // preserve damage, readiness and boons, and emit no movement event.
            RecallAttachedGroupToBase(zones, cards, id, owner);
            cards[id] = unit with { IsAttacking = false, IsDefending = false };
            events.Add(new("UNIT_RECALLED_TO_BASE", "战斗平局效果召回单位",
                new Dictionary<string, object?> { ["sourceObjectId"] = source, ["targetObjectId"] = id,
                    ["battlefieldId"] = field.ObjectId, ["ownerPlayerId"] = owner, ["controllerId"] = controller,
                    ["originZone"] = "BATTLEFIELD", ["destinationZone"] = "BASE", ["reason"] = AttackingBattleDrawTiming }));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
