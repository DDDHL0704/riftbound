using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static MatchState EstablishBattleDuel(MatchState state, string battlefield, string attacker)
    {
        if (TryReadBattleResponseDeclarationContext(state, string.Empty, out _)) return state;
        var units = state.CardObjects.Values.Where(c => IsFaceUpNonStandbyUnit(c)
            && IsObjectLocatedAtBattlefield(state.PlayerZones, state.ObjectLocations, c.ObjectId, battlefield)).OrderBy(c => c.ObjectId, StringComparer.Ordinal).ToArray();
        var attacking = units.Where(c => EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == attacker).Select(c => c.ObjectId).ToArray();
        var defending = units.Where(c => EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != attacker).Select(c => c.ObjectId).ToArray();
        if (attacking.Length == 0 || defending.Length == 0) return state;
        var marker = BuildBattleResponseDeclarationContextMarker(battlefield, attacking, defending, [DeclareBattleOptionalCost], [],
            state.FocusPlayerId ?? attacker, attacker, publicObjectBindings: CaptureBattlePublicBindings(state, attacking.Concat(defending)));
        return state with { UntilEndOfTurnEffects = SetBattleResponseDeclarationContextMarker(state.UntilEndOfTurnEffects, marker) };
    }

    internal static bool IsInitialBattleDuel(MatchState state, out BattleResponseDeclarationContext context)
        => TryReadBattleResponseDeclarationContext(state, string.Empty, out context) && context.InitialAttackingPlayerId is not null;

    internal static bool IsEstablishedBattleDuelParticipant(MatchState state, string id)
        => IsInitialBattleDuel(state, out var context) && IsObjectLocatedAtBattlefield(state.PlayerZones, state.ObjectLocations, id, context.BattlefieldId)
            && state.CardObjects.TryGetValue(id, out var card) && IsFaceUpNonStandbyUnit(card)
            && (card.IsAttacking || card.IsDefending);

    internal static bool IsBattleAwaitingCombatStart(MatchState state)
        => IsInitialBattleDuel(state, out var context) && !context.CombatStarted;

    internal static string? BattleRoleOrderingPlayer(MatchState state)
        => state.TriggerQueue.Any(t => t.TriggeredByEventKind == "BATTLE_ROLES_ESTABLISHED")
            && IsInitialBattleDuel(state, out var context) ? context.InitialAttackingPlayerId : null;

    private static IReadOnlyList<string> MarkBattleCombatStarted(MatchState state, IReadOnlyList<string> costs, IReadOnlyList<string>? targets,
        IReadOnlyList<string> attackers, IReadOnlyList<string> defenders)
    {
        if (!IsInitialBattleDuel(state, out var context)) return state.UntilEndOfTurnEffects;
        return SetBattleResponseDeclarationContextMarker(state.UntilEndOfTurnEffects,
            BuildBattleResponseDeclarationContextMarker(context.BattlefieldId, attackers,
                defenders, costs, targets ?? [],
                context.InitialFocusPlayerId, context.InitialAttackingPlayerId, combatStarted: true, initialRolesEstablished: true,
                ceasedTokens: context.CeasedTokens, publicObjectBindings: context.PublicObjectBindings));
    }

    private static ResolutionResult RecordBattleDuelRoles(MatchState before, ResolutionResult result, int roleEventOffset = 0)
    {
        if (!result.Accepted) return result;
        var state = result.State;
        if (result.Events.Any(e => e.Kind == "BATTLE_CLOSED")) {
            var next = AdvancePendingBattlefieldTasksAfterStateChange(state, state.ActivePlayerId, before);
            state = next.State;
            result = result with { State = state, Events = result.Events.Concat(next.Events).ToArray(),
                Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
        }
        if (!IsInitialBattleDuel(state, out var context)) return result;
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var newAttackers = new List<string>(); var newDefenders = new List<string>();
        foreach (var c in cards.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal).ToArray()) {
            var participating = IsFaceUpNonStandbyUnit(c) && IsObjectLocatedAtBattlefield(state.PlayerZones, state.ObjectLocations, c.ObjectId, context.BattlefieldId);
            var attacking = participating && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == context.InitialAttackingPlayerId;
            var defending = participating && !attacking;
            if (attacking != c.IsAttacking || defending != c.IsDefending)
                cards[c.ObjectId] = c with { IsAttacking = attacking, IsDefending = defending };
            var sameRoleObject = before.CardObjects.TryGetValue(c.ObjectId, out var previous) && context.InitialRolesEstablished
                && previous.ObjectGeneration == c.ObjectGeneration && IsObjectLocatedAtBattlefield(before.PlayerZones, before.ObjectLocations, c.ObjectId, context.BattlefieldId);
            if (attacking && (!sameRoleObject || !previous!.IsAttacking)) newAttackers.Add(c.ObjectId);
            if (defending && (!sameRoleObject || !previous!.IsDefending)) newDefenders.Add(c.ObjectId);
        }
        state = state with { CardObjects = cards };
        var focus = context.InitialRolesEstablished ? state.FocusPlayerId ?? context.InitialFocusPlayerId! : context.InitialFocusPlayerId!;
        var roleKind = context.InitialRolesEstablished ? "BATTLE_ROLE_ACQUIRED" : "BATTLE_ROLES_ESTABLISHED";
        var roleEventOrder = roleEventOffset + result.Events.Count(e => e.Kind != CapturedMightInstruction
            && e.Kind != "INTERNAL_CARD_PLAY_COMPLETION");
        state = CaptureFirstBattleRoles(state, context, newAttackers, newDefenders, roleEventOrder,
            out var firstAttackers, out var firstDefenders, out var newScope);
        var queue = CaptureAttackDamageTriggers(state, firstAttackers, context.BattlefieldId, TimingStates.SpellDuelOpen, focus)
            .Select(t => t with { TriggeredByEventKind = roleKind }).ToList();
        queue.AddRange(CaptureAttackUnitEvents(state, firstAttackers, TimingStates.SpellDuelOpen, roleKind, state.Tick));
        queue.AddRange(CaptureRoleUnitEvents(state, firstDefenders, "DEFEND", TimingStates.SpellDuelOpen, roleKind));
        queue.AddRange(CaptureAttackAreaDamageTriggers(state, firstAttackers, context.BattlefieldId, TimingStates.SpellDuelOpen, focus)
            .Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureDisarmTriggers(state, firstAttackers, context.BattlefieldId, TimingStates.SpellDuelOpen, focus)
            .Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureAttackWeaponDamageTriggers(state, firstAttackers, context.BattlefieldId, TimingStates.SpellDuelOpen, focus)
            .Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureEmpoweredAttackDestroyTriggers(state, firstAttackers, context.BattlefieldId, TimingStates.SpellDuelOpen, focus)
            .Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CapturePaidUnitAttackTriggers(state, firstAttackers, context.BattlefieldId, TimingStates.SpellDuelOpen, focus)
            .Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureAttackMightDamage(state, firstAttackers, context.BattlefieldId, TimingStates.SpellDuelOpen, focus)
            .Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureAttackDeckPlayTriggers(state, firstAttackers, context.BattlefieldId, TimingStates.SpellDuelOpen, focus)
            .Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureAttackGraveyardBanishTriggers(state, firstAttackers, context.BattlefieldId, TimingStates.SpellDuelOpen, focus)
            .Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureCombatMightTriggers(state, firstAttackers.Concat(firstDefenders),
            context.BattlefieldId, TimingStates.SpellDuelOpen, focus).Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureEnemyAttackMightTriggers(state, firstAttackers, context.BattlefieldId,
            TimingStates.SpellDuelOpen, focus).Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureStandbyDefendTriggers(state, firstDefenders, context.BattlefieldId,
            TimingStates.SpellDuelOpen, focus).Select(t => t with { TriggeredByEventKind = roleKind }));
        foreach (var id in firstDefenders.Where(id => HasFieldTrigger(cards[id].CardNo, "DEFEND")))
            queue.Add(new TriggerQueueItemState($"defend-{state.Tick}-{id}", cards[id].ControllerId!, id,
                FieldTriggerEffect, roleKind, TimingStates.SpellDuelOpen) {
                    FieldContext = new(cards[id].CardNo!, "DEFEND", cards[id].ObjectGeneration, context.BattlefieldId, focus) });
        queue.AddRange(CaptureBattleLimitedRoleSkills(state, context.BattlefieldId, firstAttackers, firstDefenders, newScope, focus, roleKind));
        var marker = BuildBattleResponseDeclarationContextMarker(context.BattlefieldId,
            context.AttackerObjectIds.Concat(newAttackers.Where(id => !context.DefenderObjectIds.Contains(id))).Distinct(StringComparer.Ordinal).ToArray(),
            context.DefenderObjectIds.Concat(newDefenders.Where(id => !context.AttackerObjectIds.Contains(id))).Distinct(StringComparer.Ordinal).ToArray(), context.OptionalCosts,
            context.BattlefieldTargetObjectIds, context.InitialFocusPlayerId, context.InitialAttackingPlayerId, context.CombatStarted,
            initialRolesEstablished: true, ceasedTokens: context.CeasedTokens,
            publicObjectBindings: context.PublicObjectBindings?.Concat(CaptureBattlePublicBindings(state, newAttackers.Concat(newDefenders)))
                .GroupBy(b => b.ObjectId, StringComparer.Ordinal).Select(g => g.First()).ToArray());
        state = state with { UntilEndOfTurnEffects = SetBattleResponseDeclarationContextMarker(state.UntilEndOfTurnEffects, marker),
            TriggerQueue = state.TriggerQueue.Concat(queue).ToArray() };
        var events = result.Events.ToList();
        if (newAttackers.Count + newDefenders.Count > 0)
            events.Add(new GameEvent(roleKind, "确立进攻和防守身份", new Dictionary<string, object?> {
                ["battlefieldId"] = context.BattlefieldId, ["attackingPlayerId"] = context.InitialAttackingPlayerId,
                ["attackerObjectIds"] = newAttackers.ToArray(), ["defenderObjectIds"] = newDefenders.ToArray(),
                ["optionalCosts"] = context.OptionalCosts, ["battlefieldTargetObjectIds"] = context.BattlefieldTargetObjectIds,
                ["declarationContextPreserved"] = true }));
        events.AddRange(queue.Select(BuildTriggerQueuedEvent));
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static MatchState FinalizeRuleChoiceBattleRoleCoordinates(MatchState before, ResolutionResult result,
        IReadOnlyList<GameEvent> committedEvents, long completedTick)
    {
        // Replay still uses the original command's coordinates while choices
        // are pending. Only its final, retained events receive journal slots.
        var previous = before.UntilEndOfTurnEffects.ToHashSet(StringComparer.Ordinal);
        int EventOrder(int originalOrder)
        {
            var roleEvent = result.Events[originalOrder];
            if (roleEvent.Kind is "BATTLE_ROLES_ESTABLISHED" or "BATTLE_ROLE_ACQUIRED")
                for (var order = 0; order < committedEvents.Count; order++)
                    if (ReferenceEquals(committedEvents[order], roleEvent)) return order;
            throw new InvalidOperationException("New battle role has no retained command event.");
        }
        var scopes = new Dictionary<BattleLimitedScope, BattleLimitedScope>();
        foreach (var text in result.State.UntilEndOfTurnEffects)
            if (!previous.Contains(text) && BattleLimitedMarkers.TryScope(text, out var scope))
                scopes.Add(scope!, scope! with { StartedAtTick = completedTick, RoleEventOrder = EventOrder(scope.RoleEventOrder) });
        var effects = result.State.UntilEndOfTurnEffects.Select(text => {
            // Resource children and earlier commands already have accepted
            // coordinates; their identities must survive the parent replay.
            if (previous.Contains(text)) return text;
            if (BattleLimitedMarkers.TryScope(text, out var scope))
                return BattleLimitedMarkers.ScopeId(scopes[scope!]);
            if (BattleLimitedMarkers.TryRole(text, out var role))
                return BattleLimitedMarkers.RoleId(role! with {
                    Scope = scopes.GetValueOrDefault(role.Scope, role.Scope),
                    AcquiredAtTick = completedTick, RoleEventOrder = EventOrder(role.RoleEventOrder) });
            return text;
        }).ToArray();
        return result.State with { UntilEndOfTurnEffects = effects };
    }

    private static ObjectBinding[] CaptureBattlePublicBindings(MatchState state, IEnumerable<string> participants)
        => participants.Distinct(StringComparer.Ordinal).Where(id => state.CardObjects.TryGetValue(id, out var card)
                && FieldObjectTypeRules.IsVisibleUnit(card) && IsObjectOnField(state.PlayerZones, id))
            .Select(id => new ObjectBinding(id, state.CardObjects[id].ObjectGeneration)).ToArray();
}
