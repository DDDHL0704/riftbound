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

    private static IReadOnlyList<string> MarkBattleCombatStarted(MatchState state, IReadOnlyList<string> costs, IReadOnlyList<string>? targets)
    {
        if (!IsInitialBattleDuel(state, out var context)) return state.UntilEndOfTurnEffects;
        return SetBattleResponseDeclarationContextMarker(state.UntilEndOfTurnEffects,
            BuildBattleResponseDeclarationContextMarker(context.BattlefieldId, context.AttackerObjectIds,
                context.DefenderObjectIds, costs, targets ?? [],
                context.InitialFocusPlayerId, context.InitialAttackingPlayerId, combatStarted: true, initialRolesEstablished: true,
                ceasedTokens: context.CeasedTokens, publicObjectBindings: context.PublicObjectBindings));
    }

    private static ResolutionResult RecordBattleDuelRoles(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = result.State;
        if (result.Events.Any(e => e.Kind == "BATTLE_CLOSED")) {
            if (TryReadBattleResponseDeclarationContext(state, string.Empty, out var closing)
                && result.Events.Any(e => e.Kind == "BATTLE_CLOSED"
                    && Equals(e.Payload?.GetValueOrDefault("battlefieldId"), closing.BattlefieldId)))
                state = state with { UntilEndOfTurnEffects = ClearBattleResponseDeclarationContextMarkers(state.UntilEndOfTurnEffects) };
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
            var sameRoleObject = before.CardObjects.TryGetValue(c.ObjectId, out var previous)
                && previous.ObjectGeneration == c.ObjectGeneration && IsObjectLocatedAtBattlefield(before.PlayerZones, before.ObjectLocations, c.ObjectId, context.BattlefieldId);
            if (attacking && (!sameRoleObject || !previous!.IsAttacking)) newAttackers.Add(c.ObjectId);
            if (defending && (!sameRoleObject || !previous!.IsDefending)) newDefenders.Add(c.ObjectId);
        }
        state = state with { CardObjects = cards };
        var focus = context.InitialRolesEstablished ? state.FocusPlayerId ?? context.InitialFocusPlayerId! : context.InitialFocusPlayerId!;
        var roleKind = context.InitialRolesEstablished ? "BATTLE_ROLE_ACQUIRED" : "BATTLE_ROLES_ESTABLISHED";
        var queue = CaptureAttackDamageTriggers(state, newAttackers, context.BattlefieldId, TimingStates.SpellDuelOpen, focus)
            .Select(t => t with { TriggeredByEventKind = roleKind }).ToList();
        queue.AddRange(CaptureCombatMightTriggers(state, newAttackers.Concat(newDefenders),
            context.BattlefieldId, TimingStates.SpellDuelOpen, focus).Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureEnemyAttackMightTriggers(state, newAttackers, context.BattlefieldId,
            TimingStates.SpellDuelOpen, focus).Select(t => t with { TriggeredByEventKind = roleKind }));
        queue.AddRange(CaptureStandbyDefendTriggers(state, newDefenders, context.BattlefieldId,
            TimingStates.SpellDuelOpen, focus).Select(t => t with { TriggeredByEventKind = roleKind }));
        foreach (var id in newDefenders.Where(id => HasFieldTrigger(cards[id].CardNo, "DEFEND")))
            queue.Add(new TriggerQueueItemState($"defend-{state.Tick}-{id}", cards[id].ControllerId!, id,
                FieldTriggerEffect, roleKind, TimingStates.SpellDuelOpen) {
                    FieldContext = new(cards[id].CardNo!, "DEFEND", cards[id].ObjectGeneration, context.BattlefieldId, focus) });
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

    private static ObjectBinding[] CaptureBattlePublicBindings(MatchState state, IEnumerable<string> participants)
        => participants.Distinct(StringComparer.Ordinal).Where(id => state.CardObjects.TryGetValue(id, out var card)
                && FieldObjectTypeRules.IsVisibleUnit(card) && IsObjectOnField(state.PlayerZones, id))
            .Select(id => new ObjectBinding(id, state.CardObjects[id].ObjectGeneration)).ToArray();
}
