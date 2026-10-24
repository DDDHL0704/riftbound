using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static MatchState CaptureFirstBattleRoles(MatchState state, BattleResponseDeclarationContext battle,
        IReadOnlyList<string> acquiredAttackers, IReadOnlyList<string> acquiredDefenders, int eventOrder,
        out string[] firstAttackers, out string[] firstDefenders, out bool newScope)
    {
        var scope = BattleLimitedMarkers.ScopeFor(state, battle.BattlefieldId);
        newScope = scope is null;
        var effects = state.UntilEndOfTurnEffects.ToList();
        if (scope is null) {
            var defender = state.Seats.Keys.Single(id => id != battle.InitialAttackingPlayerId);
            scope = new(battle.BattlefieldId, state.Tick, eventOrder, battle.InitialAttackingPlayerId!, defender);
            effects.Add(BattleLimitedMarkers.ScopeId(scope));
        }
        var attack = new List<string>(); var defend = new List<string>();
        var occupants = state.CardObjects.Values.Where(c => IsFaceUpNonStandbyUnit(c)
            && IsObjectLocatedAtBattlefield(state.PlayerZones, state.ObjectLocations, c.ObjectId, battle.BattlefieldId)).ToArray();
        var oneOnOne = occupants.Count(c => c.IsAttacking) == 1 && occupants.Count(c => c.IsDefending) == 1;
        foreach (var (ids, role, output) in new[] { (acquiredAttackers, "ATTACKER", attack), (acquiredDefenders, "DEFENDER", defend) })
            foreach (var id in ids.Distinct(StringComparer.Ordinal)) {
                var source = state.CardObjects[id];
                if (BattleLimitedMarkers.First(state, scope, id, source.ObjectGeneration, role) is not null) continue;
                effects.Add(BattleLimitedMarkers.RoleId(new(scope, id, source.ObjectGeneration, source.ControllerId!, role,
                    AttachedRuleTextRules.Face(source)!, source.CopyFaceTransitions?.Count ?? 0, state.Tick, eventOrder,
                    oneOnOne, IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null)));
                output.Add(id);
            }
        if (newScope && TryGetBattlefieldCardObject(state.PlayerZones, state.CardObjects, battle.BattlefieldId,
            out var fieldId, out var field) && BattleLimitedMarkers.Definition(field.CardNo ?? "", BattleLimitedMarkers.SteadfastDefend)
            && SourceObjectControlledByPlayerOrLegacyOwned(field, scope.DefendingPlayerId))
            effects.Add(BattleLimitedMarkers.RoleId(new(scope, fieldId, field.ObjectGeneration, scope.DefendingPlayerId,
                "PLAYER_DEFENDER", field.CardNo!, 0, state.Tick, eventOrder, oneOnOne, null)));
        firstAttackers = attack.ToArray(); firstDefenders = defend.ToArray();
        return state with { UntilEndOfTurnEffects = effects.ToArray() };
    }

    private static TriggerQueueItemState[] CaptureBattleLimitedRoleSkills(MatchState state, string battlefield,
        IReadOnlyList<string> firstAttackers, IReadOnlyList<string> firstDefenders, bool newScope, string focus, string eventKind)
    {
        var scope = BattleLimitedMarkers.ScopeFor(state, battlefield)!;
        var queue = new List<TriggerQueueItemState>();
        var occupants = state.CardObjects.Values.Where(c => IsFaceUpNonStandbyUnit(c)
            && IsObjectLocatedAtBattlefield(state.PlayerZones, state.ObjectLocations, c.ObjectId, battlefield)).ToArray();
        var oneOnOne = occupants.Count(c => c.IsAttacking) == 1 && occupants.Count(c => c.IsDefending) == 1;
        if (oneOnOne) foreach (var (ids, kind) in new[] { (firstAttackers, BattleLimitedMarkers.DoubleAttack), (firstDefenders, BattleLimitedMarkers.DoubleDefend) })
            foreach (var id in ids) {
                var source = state.CardObjects[id];
                foreach (var instance in AttachedRuleTextRules.Instances(state, source)) {
                    if (!BattleLimitedMarkers.Definition(instance.CardNo, kind)) continue;
                    var context = new FieldTriggerContext(instance.CardNo, kind, source.ObjectGeneration, battlefield, focus) {
                        Origin = instance.Origin, SourceLocation = new(source.ControllerId!, "BATTLEFIELD", battlefield),
                        SourceTokenFactoryCardNo = IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null };
                    queue.Add(new($"battle-limited-{scope.StartedAtTick}-{scope.RoleEventOrder}-{state.Tick}-{id}-{instance.Id}-{kind}",
                        source.ControllerId!, id, FieldTriggerEffect, eventKind, TimingStates.SpellDuelOpen) { FieldContext = context });
                }
            }
        if (newScope && TryGetBattlefieldCardObject(state.PlayerZones, state.CardObjects, battlefield, out var fieldId, out var field)
            && BattleLimitedMarkers.Definition(field.CardNo ?? "", BattleLimitedMarkers.SteadfastDefend)
            && SourceObjectControlledByPlayerOrLegacyOwned(field, scope.DefendingPlayerId)) {
            var context = new FieldTriggerContext(field.CardNo!, BattleLimitedMarkers.SteadfastDefend,
                field.ObjectGeneration, battlefield, focus) { SourceLocation = new(scope.DefendingPlayerId, "BATTLEFIELD", battlefield) };
            queue.Add(new($"battle-limited-{scope.StartedAtTick}-{scope.RoleEventOrder}-{fieldId}-steadfast", scope.DefendingPlayerId,
                fieldId, FieldTriggerEffect, eventKind, TimingStates.SpellDuelOpen) { FieldContext = context });
        }
        return queue.ToArray();
    }

    private static bool IsBattleLimitedContextShape(FieldTriggerContext context, string effect, string? cardNo)
        => BattleLimitedMarkers.Kind(context.Kind) && effect == FieldTriggerEffect
            && BattleLimitedMarkers.Definition(context.CardNo, context.Kind) && (cardNo is null || cardNo == context.CardNo)
            && context.SourceGeneration >= 0 && !string.IsNullOrWhiteSpace(context.BattlefieldId)
            && context.SourceLocation is { Zone: "BATTLEFIELD" } location && location.BattlefieldObjectId == context.BattlefieldId
            && !string.IsNullOrWhiteSpace(location.PlayerId) && context.ReturnFocusPlayerId is not null
            && context.RecycledBoon is null && context.AffectedUnit is null && !context.AffectedUnitCeasedToExist
            && (context.SourceTokenFactoryCardNo is null
                || P6TokenFactoryCatalog.TryGetByCardNo(context.SourceTokenFactoryCardNo, out var token)
                    && token.Tags.Contains(CardObjectTags.UnitCard))
            && (context.Kind == BattleLimitedMarkers.SteadfastDefend
                ? context.Origin is null && context.SourceTokenFactoryCardNo is null && !context.SourceCeasedToExist
                : !context.SourceCeasedToExist || context.SourceTokenFactoryCardNo is not null);

    private static bool ValidBattleLimitedContextState(MatchState state, FieldTriggerContext context,
        string sourceId, string controller, string effect, string? cardNo)
    {
        if (!IsBattleLimitedContextShape(context, effect, cardNo) || !state.Seats.ContainsKey(controller)
            || context.ReturnFocusPlayerId is null || !state.Seats.ContainsKey(context.ReturnFocusPlayerId)
            || BattleLimitedMarkers.ScopeFor(state, context.BattlefieldId!) is not { } scope) return false;
        var role = context.Kind == BattleLimitedMarkers.SteadfastDefend ? "PLAYER_DEFENDER" : BattleLimitedMarkers.Role(context.Kind);
        var first = BattleLimitedMarkers.First(state, scope, sourceId, context.SourceGeneration, role);
        if (first is null || first.ControllerId != controller || context.SourceLocation!.PlayerId != controller
            || context.Kind != BattleLimitedMarkers.SteadfastDefend && !first.OneOnOne
            || context.Origin is null && first.SourceFace != context.CardNo
            || context.Origin?.CapturedTick is { } capturedTick && capturedTick > first.AcquiredAtTick
            || !AttachedRuleTextRules.ValidCaptured(state, context.Origin, sourceId, context.SourceGeneration, context.CardNo)) return false;
        if (!state.CardObjects.TryGetValue(sourceId, out var source))
            return context.SourceCeasedToExist && first.TokenFactoryCardNo == context.SourceTokenFactoryCardNo
                && context.SourceTokenFactoryCardNo is not null
                && TryReadBattleResponseDeclarationContext(state, context.BattlefieldId!, out var battle)
                && battle.CeasedTokens?.TryGetValue(sourceId, out var token) == true
                && token.Generation == context.SourceGeneration && token.FactoryCardNo == context.SourceTokenFactoryCardNo;
        if (source.ObjectGeneration < context.SourceGeneration || context.SourceCeasedToExist) return false;
        if (context.SourceTokenFactoryCardNo != first.TokenFactoryCardNo) return false;
        if (context.Origin is not null || source.ObjectGeneration > context.SourceGeneration) return true;
        if (context.Kind == BattleLimitedMarkers.SteadfastDefend) {
            if (source.CardNo == first.SourceFace && first.SourceFace == context.CardNo) return true;
            return first.SourceFace == context.CardNo && source.ReplacedBattlefieldCard is { } old
                && state.CardObjects.TryGetValue(old.ObjectId, out var previous) && previous.ObjectGeneration == old.Generation
                && previous.CardNo == context.CardNo && previous.ReplacedAtBattlefieldId == context.BattlefieldId;
        }
        return CopyFaceTimeline.Valid(state, source) && first.SourceFace == context.CardNo
            && CopyFaceTimeline.FaceAt(source, first.FaceRevision, first.AcquiredAtTick) == context.CardNo;
    }
}
