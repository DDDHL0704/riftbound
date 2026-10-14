using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static TriggerSpec? CombatMightSpec(string? cardNo) => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo)
        .FirstOrDefault(t => t.Kind == TriggerKinds.UnitAttackOrDefendPowerModifier
            && t.Timing == TriggerTimings.UnitAttackOrDefend && t.TargetScope == TriggerTargetScopes.EnemyUnitHere
            && t.Duration == "UNTIL_END_OF_TURN" && t.PowerDelta is < 0 && t.MinimumPowerAfterModifier is > 0);

    private static TriggerQueueItemState[] CaptureCombatMightTriggers(MatchState state, IEnumerable<string> participants,
        string battlefield, string timing, string? returnFocusPlayer = null)
    {
        var queue = new List<TriggerQueueItemState>();
        foreach (var id in participants.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source)) {
                if (CombatMightSpec(instance.CardNo) is null) continue;
                var context = new FieldTriggerContext(instance.CardNo, "COMBAT_MIGHT", source.ObjectGeneration,
                    battlefield, returnFocusPlayer) {
                    SourceLocation = new(source.ControllerId!, "BATTLEFIELD", battlefield), Origin = instance.Origin,
                    SourceTokenFactoryCardNo = IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null };
                queue.Add(new($"combat-might-{state.Tick + 1}-{id}-{instance.Id}", source.ControllerId!, id,
                    FieldTriggerEffect, "BATTLE_ROLES_ESTABLISHED", timing) { FieldContext = context });
            }
        }
        return queue.ToArray();
    }

    private static ObjectLocationState? CombatMightSourceLocation(MatchState state, string sourceId, long generation)
        => state.CardObjects.TryGetValue(sourceId, out var source) && source.ObjectGeneration == generation
            && IsObjectOnField(state.PlayerZones, sourceId) && state.ObjectLocations.TryGetValue(sourceId, out var location)
            && location.Zone is "BASE" or "BATTLEFIELD" ? location : null;

    private static ObjectLocationState? CombatMightHere(MatchState state, StackItemState item)
        => CombatMightSourceLocation(state, item.SourceObjectId, item.FieldContext!.SourceGeneration);

    private static bool IsCombatMightTargetHere(MatchState state, StackItemState item, string id)
    {
        var here = CombatMightHere(state, item);
        return here is not null && state.ObjectLocations.TryGetValue(id, out var target) && target.Zone == here.Zone
            && (here.Zone == "BASE" ? target.PlayerId == here.PlayerId : target.BattlefieldObjectId == here.BattlefieldObjectId);
    }

    private static ResolutionResult RecordCombatMightSourceLocations(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        FieldTriggerContext? Update(string sourceId, FieldTriggerContext? context)
        {
            if (context is not { Kind: "COMBAT_MIGHT" or "STANDBY_DEFEND" or "ATTACK_DECK_PLAY" }) return context;
            var location = CombatMightSourceLocation(result.State, sourceId, context.SourceGeneration)
                ?? CombatMightSourceLocation(before, sourceId, context.SourceGeneration);
            return location is null || location == context.SourceLocation ? context : context with { SourceLocation = location };
        }
        var state = result.State with {
            StackItems = result.State.StackItems.Select(i => i.FieldContext is not { Kind: "COMBAT_MIGHT" or "STANDBY_DEFEND" or "ATTACK_DECK_PLAY" } ? i
                : i with { FieldContext = Update(i.SourceObjectId, i.FieldContext) }).ToArray(),
            TriggerQueue = result.State.TriggerQueue.Select(t => t.FieldContext is not { Kind: "COMBAT_MIGHT" or "STANDBY_DEFEND" or "ATTACK_DECK_PLAY" } ? t
                : t with { FieldContext = Update(t.SourceObjectId, t.FieldContext) }).ToArray() };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidFieldContextState(MatchState state, FieldTriggerContext context, string sourceId,
        string controller, string effect, string? cardNo = null)
    {
        if (!ValidFieldContext(context, effect, cardNo)) return false;
        if (context.Kind == "RECYCLE_BOON") return ValidRecycledBoonSource(state, context, sourceId, controller);
        if (context.Kind == "ENEMY_ATTACK_MIGHT")
            return ValidEnemyAttackMightState(state, context, sourceId, controller);
        if (context.Kind is not ("COMBAT_MIGHT" or "STANDBY_DEFEND" or "ATTACK_DECK_PLAY")) return true;
        if (string.IsNullOrWhiteSpace(sourceId) || !state.Seats.ContainsKey(controller)
            || !ValidCapturedFieldSource(state, context, sourceId)
            || context.ReturnFocusPlayerId is not null && !state.Seats.ContainsKey(context.ReturnFocusPlayerId)
            || BattlefieldLocalRules.Battlefield(state, context.BattlefieldId) is null
            || context.SourceLocation is not { } location || !state.Seats.ContainsKey(location.PlayerId)
            || !(location.Zone == "BASE" && location.BattlefieldObjectId is null
                || location.Zone == "BATTLEFIELD" && BattlefieldLocalRules.Battlefield(state, location.BattlefieldObjectId) is not null)
            || !AttachedRuleTextRules.ValidCaptured(state, context.Origin, sourceId, context.SourceGeneration, context.CardNo)) return false;
        return CombatMightSourceLocation(state, sourceId, context.SourceGeneration) is not { } current
            || current == context.SourceLocation;
    }

    private static bool ValidCapturedFieldSource(MatchState state, FieldTriggerContext context, string sourceId)
    {
        if (context.SourceCeasedToExist)
            return !state.CardObjects.ContainsKey(sourceId)
                && TryReadBattleResponseDeclarationContext(state, context.BattlefieldId!, out var battle)
                && battle.CeasedTokens?.TryGetValue(sourceId, out var token) == true
                && token.Generation == context.SourceGeneration && token.FactoryCardNo == context.SourceTokenFactoryCardNo;
        return state.CardObjects.TryGetValue(sourceId, out var source) && source.ObjectGeneration >= context.SourceGeneration
            && (context.Origin is not null || source.ObjectGeneration != context.SourceGeneration || AttachedRuleTextRules.Face(source) == context.CardNo)
            && (context.SourceTokenFactoryCardNo is null ? !IsTokenObject(source)
                : IsTokenObject(source) && (source.TokenFactoryCardNo ?? source.CardNo) == context.SourceTokenFactoryCardNo);
    }
}
