using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool IsTokenObject(CardObjectState card) =>
        TokenObjectRules.IsToken(card);

    private static ResolutionResult FinalizeTokenDepartures(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var departed = before.CardObjects.Where(e => IsTokenObject(e.Value)
            && !result.State.CardObjects.ContainsKey(e.Key)).Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        if (departed.Count == 0) return result;
        CeasedRecycledBoonSource CaptureBoonSource(string sourceId, FieldTriggerContext context, RecycledCardBoonOrigin boon)
        {
            var source = before.CardObjects[sourceId];
            var revision = context.Origin?.FaceRevision ?? boon.FaceRevision;
            var capturedTick = context.Origin?.CapturedTick ?? boon.CapturedTick;
            return new(new(sourceId, context.SourceGeneration), context.SourceTokenFactoryCardNo!, result.State.Tick,
                CopyFaceTimeline.FaceAt(source, revision, capturedTick) ?? string.Empty, revision, capturedTick);
        }
        FieldTriggerContext? Invalidate(string sourceId, FieldTriggerContext? context) =>
            context is { Kind: PaidUnitStunKind, PaidUnitTrigger: { } paid }
                && departed.Contains(sourceId) && before.CardObjects[sourceId].ObjectGeneration == context.SourceGeneration
                && context.SourceTokenFactoryCardNo == before.CardObjects[sourceId].TokenFactoryCardNo
                ? context with { SourceCeasedToExist = true, PaidUnitTrigger = paid with {
                    CeasedSource = new(result.State.Tick, context.Origin?.CardNo
                        ?? CopyFaceTimeline.FaceAt(before.CardObjects[sourceId], paid.FaceRevision, paid.CapturedTick) ?? string.Empty) } }
                : context is { Kind: EmpoweredBanishKind, EmpoweredTransition: { } birth }
                && departed.Contains(sourceId) && before.CardObjects[sourceId].ObjectGeneration == context.SourceGeneration
                && context.SourceTokenFactoryCardNo == before.CardObjects[sourceId].TokenFactoryCardNo
                ? context with { SourceCeasedToExist = true, EmpoweredTransition = birth with {
                    CeasedSource = new(result.State.Tick, context.Origin?.CardNo
                        ?? CopyFaceTimeline.FaceAt(before.CardObjects[sourceId], birth.FaceRevision, birth.CapturedTick) ?? string.Empty) } }
                : context is { Kind: "RECYCLE_BOON", RecycledBoon: { } origin }
                && departed.Contains(sourceId) && before.CardObjects[sourceId].ObjectGeneration == context.SourceGeneration
                && context.SourceTokenFactoryCardNo == before.CardObjects[sourceId].TokenFactoryCardNo
                ? context with { SourceCeasedToExist = true, RecycledBoon = origin with {
                    CeasedSource = CaptureBoonSource(sourceId, context, origin) } }
                : context is { Kind: "COMBAT_MIGHT" or "STANDBY_DEFEND" or "ATTACK_DECK_PLAY" or AttackAreaDamageKind or BattleLimitedMarkers.DoubleAttack or BattleLimitedMarkers.DoubleDefend }
                && departed.Contains(sourceId) && before.CardObjects[sourceId].ObjectGeneration == context.SourceGeneration
                && context.SourceTokenFactoryCardNo == (before.CardObjects[sourceId].TokenFactoryCardNo ?? before.CardObjects[sourceId].CardNo)
                ? context with { SourceCeasedToExist = true }
                : context is { Kind: "ENEMY_ATTACK_MIGHT", AffectedUnit: { } affected }
                && departed.Contains(affected.ObjectId) && before.CardObjects[affected.ObjectId].ObjectGeneration == affected.Generation
                ? context with { AffectedUnitCeasedToExist = true } : context;
        var state = result.State with { ObjectLocations = result.State.ObjectLocations
            .Where(e => !departed.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal),
            StackItems = result.State.StackItems.Select(i => i.FieldContext is null ? i : i with { FieldContext = Invalidate(i.SourceObjectId, i.FieldContext) }).ToArray(),
            TriggerQueue = result.State.TriggerQueue.Select(t => t.FieldContext is null ? t : t with { FieldContext = Invalidate(t.SourceObjectId, t.FieldContext) }).ToArray() };
        // Preserve the originally declared participant identities. The current
        // resolution's before-state proves these missing objects were tokens;
        // recovery never invents a live unit to replace a ceased participant.
        var participants = new Dictionary<string, CardObjectState>(state.CardObjects);
        foreach (var id in departed) participants[id] = before.CardObjects[id];
        if (TryReadBattleResponseDeclarationContext(state with { CardObjects = participants }, string.Empty, out var context)) {
            var ceased = context.CeasedTokens?.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal)
                ?? new Dictionary<string, CeasedBattleToken>(StringComparer.Ordinal);
            foreach (var id in departed.Order(StringComparer.Ordinal))
                if (context.AttackerObjectIds.Concat(context.DefenderObjectIds).Contains(id, StringComparer.Ordinal))
                    ceased[id] = new(before.CardObjects[id].TokenFactoryCardNo ?? before.CardObjects[id].CardNo!, before.CardObjects[id].ObjectGeneration);
            if (ceased.Count > 0)
                state = state with { UntilEndOfTurnEffects = SetBattleResponseDeclarationContextMarker(state.UntilEndOfTurnEffects,
                    BuildBattleResponseDeclarationContextMarker(context.BattlefieldId, context.AttackerObjectIds, context.DefenderObjectIds,
                        context.OptionalCosts, context.BattlefieldTargetObjectIds, context.InitialFocusPlayerId, context.InitialAttackingPlayerId,
                        context.CombatStarted, context.InitialRolesEstablished, ceased, context.PublicObjectBindings)) };
        }
        var events = result.Events.ToList();
        foreach (var id in departed)
            if (!events.Any(e => e.Kind == "TOKEN_CEASED_TO_EXIST"
                && e.Payload.TryGetValue("targetObjectId", out var target) && Equals(target, id)))
                events.Add(new("TOKEN_CEASED_TO_EXIST", "离场指示物消失", new Dictionary<string, object?>
                    { ["targetObjectId"] = id, ["cardNo"] = before.CardObjects[id].CardNo }));
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
