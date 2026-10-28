using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record TurnDrawLedger(int TurnNumber, IReadOnlyDictionary<string, int> Counts);
public sealed record FieldTriggerContext(string CardNo, string Kind, long SourceGeneration, string? BattlefieldId = null, string? ReturnFocusPlayerId = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PaidUnitTriggerOrigin? PaidUnitTrigger { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public StunObserverOrigin? StunObserver { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RecycledCardBoonOrigin? RecycledBoon { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EmpoweredTransitionContext? EmpoweredTransition { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ObjectLocationState? SourceLocation { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RuleTextInstanceBinding? Origin { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceTokenFactoryCardNo { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool SourceCeasedToExist { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? AffectedUnit { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool AffectedUnitCeasedToExist { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public LastKnownMightFrame? AttackConditionFrame { get; init; }
}
internal sealed record CapturedDrawSource(string SourceId, string ControllerId, FieldTriggerContext Context);

public sealed partial class CoreRuleEngine
{
    internal const string FieldTriggerEffect = "FIELD_TARGETED_TRIGGER";
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> FieldTriggerFamilies = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["UNL-074/219", "SFD·128/221"]));
    private static bool HasFieldTrigger(string? cardNo, string kind) => FieldTriggerFamilies.Value[kind == "SECOND_DRAW" ? "UNL-074/219" : "SFD·128/221"]
        .Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(cardNo), StringComparer.Ordinal);
    internal static bool ValidFieldContext(FieldTriggerContext context, string effect, string? cardNo = null)
    {
        if (context.Kind == PaidUnitStunKind) return context.StunObserver is null && ValidPaidUnitStunShape(context, effect, cardNo);
        if (IsStunObserverKind(context.Kind)) return context.PaidUnitTrigger is null && ValidStunObserverShape(context, effect, cardNo);
        if (context.PaidUnitTrigger is not null || context.StunObserver is not null) return false;
        if (context.Kind == EmpoweredBanishKind) return ValidEmpoweredBanishShape(context, effect, cardNo);
        if (context.Kind == EmpoweredAttackDestroyKind) return ValidEmpoweredAttackDestroyShape(context, effect, cardNo);
        if (context.Kind == AttackWeaponDamageKind) return ValidAttackWeaponDamageShape(context, effect, cardNo);
        if (context.EmpoweredTransition is not null) return false;
        if (context.Kind != AttackAreaDamageKind && context.AttackConditionFrame is not null) return false;
        return IsBattleLimitedContextShape(context, effect, cardNo) || effect == FieldTriggerEffect && context.SourceGeneration >= 0
            && (context.Kind is "COMBAT_MIGHT" or "STANDBY_DEFEND" or "ATTACK_DECK_PLAY" or "RECYCLE_BOON" or AttackAreaDamageKind
                ? (!context.SourceCeasedToExist || context.SourceTokenFactoryCardNo is not null)
                    && (context.SourceTokenFactoryCardNo is null
                        || P6TokenFactoryCatalog.TryGetByCardNo(context.SourceTokenFactoryCardNo, out var token)
                            && token.Tags.Contains(CardObjectTags.UnitCard))
                : context.SourceTokenFactoryCardNo is null && !context.SourceCeasedToExist)
            && (cardNo is null || cardNo == context.CardNo)
            && (context.Kind == "RECYCLE_BOON" ? HasRecycledCardBoon(context.CardNo) && context.RecycledBoon is { CardCount: > 0, CapturedTick: >= 0, FaceRevision: >= 0 }
                && context.BattlefieldId is null && context.SourceLocation is null && context.AffectedUnit is null
                && !context.AffectedUnitCeasedToExist
                : context.RecycledBoon is null && (context.Kind == "ENEMY_ATTACK_MIGHT" ? EnemyAttackMightSpec(context.CardNo) is not null
                && context.AffectedUnit is { Generation: >= 0 } && context.SourceLocation is null && context.Origin is null
                && !string.IsNullOrWhiteSpace(context.BattlefieldId)
                : context.AffectedUnit is null && !context.AffectedUnitCeasedToExist && (context.Kind == "ATTACK_DECK_PLAY" ? AttackDeckPlaySpec(context.CardNo) is not null && context.SourceLocation is not null && !string.IsNullOrWhiteSpace(context.BattlefieldId)
                : context.Kind == "STANDBY_DEFEND" ? StandbyDefendDefinition(context.CardNo) is not null && context.SourceLocation is not null && !string.IsNullOrWhiteSpace(context.BattlefieldId)
                : context.Kind == "COMBAT_MIGHT" ? CombatMightSpec(context.CardNo) is not null && context.SourceLocation is not null
                : context.Kind == AttackAreaDamageKind ? AttackAreaDamageSpec(context.CardNo) is not null && context.SourceLocation is not null && !string.IsNullOrWhiteSpace(context.BattlefieldId)
                : context.Kind is "SECOND_DRAW" or "DEFEND" && HasFieldTrigger(context.CardNo, context.Kind)
                    && context.SourceLocation is null && context.Origin is null
                    && (context.Kind == "SECOND_DRAW" ? context.BattlefieldId is null : !string.IsNullOrWhiteSpace(context.BattlefieldId)))));

    }

    private static CapturedDrawSource[] CaptureDrawTriggerSources(MatchState state, string player) => CaptureDrawTriggerSources(state.PlayerZones, state.CardObjects, player);

    private static CapturedDrawSource[] CaptureDrawTriggerSources(IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards, string player) => cards.Values
        .Where(c => c.ControllerId == player && !c.IsFaceDown
            && IsObjectOnField(zones, c.ObjectId) && HasFieldTrigger(c.CardNo, "SECOND_DRAW"))
        .OrderBy(c => c.ObjectId, StringComparer.Ordinal)
        .Select(c => new CapturedDrawSource(c.ObjectId, player, new(c.CardNo!, "SECOND_DRAW", c.ObjectGeneration))).ToArray();

    private static ResolutionResult RecordDrawTriggers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || result.State.Phase is not (MatchPhases.Main or MatchPhases.TurnStart or MatchPhases.TurnEnd)) return result;
        var state = result.State;
        var counts = (state.DrawLedger.TurnNumber == state.TurnNumber ? state.DrawLedger.Counts : new Dictionary<string, int>())
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        // The source snapshot is internal to this resolution, never part of the public event log.
        var events = result.Events.Select(e => e.Payload.ContainsKey("drawTriggerSources")
            ? e with { Payload = e.Payload.Where(p => p.Key != "drawTriggerSources").ToDictionary(p => p.Key, p => p.Value) } : e).ToList();
        var queue = state.TriggerQueue.ToList();
        for (var index = 0; index < result.Events.Count; index++)
        {
            var ev = result.Events[index];
            if (ev.Kind != "CARD_DRAWN" || !ev.Payload.TryGetValue("playerId", out var p) || p is not string player
                || !ev.Payload.TryGetValue("count", out var n) || n is not int amount || amount <= 0) continue;
            var prior = counts.GetValueOrDefault(player); counts[player] = checked(prior + amount);
            if (prior >= 2 || counts[player] < 2 || state.Status != MatchStatuses.InProgress) continue;
            var sources = ev.Payload.TryGetValue("drawTriggerSources", out var captured) && captured is CapturedDrawSource[] recorded
                ? recorded : CaptureDrawTriggerSources(before, player);
            foreach (var source in sources)
            {
                var trigger = new TriggerQueueItemState($"draw-{state.TurnNumber}-{state.Tick}-{index}-{source.SourceId}", source.ControllerId,
                    source.SourceId, FieldTriggerEffect, "SECOND_CARD_DRAWN",
                    before.SpellDuelState.IsActive ? TimingStates.SpellDuelOpen : before.BattleState.IsActive ? TimingStates.NeutralClosed : null)
                    { FieldContext = source.Context with { ReturnFocusPlayerId = state.FocusPlayerId } };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        state = state with { DrawLedger = new(state.TurnNumber, counts), TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static string[] FieldTriggerTargets(MatchState state, StackItemState item) => state.CardObjects.Values
        .Where(c => c.Tags.Contains(CardObjectTags.UnitCard) && !c.IsFaceDown
            && IsObjectOnField(state.PlayerZones, c.ObjectId) && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId)
            && (item.FieldContext!.Kind == PaidUnitStunKind ? true
                : item.FieldContext.Kind == StunBoonKind ? EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId
                : item.FieldContext.Kind == EmpoweredBanishKind ? IsEmpoweredBanishTarget(state, item, c)
                : item.FieldContext.Kind == EmpoweredAttackDestroyKind ? IsEmpoweredAttackDestroyTarget(state, item, c)
                : item.FieldContext.Kind == AttackWeaponDamageKind ? IsAttackWeaponDamageTarget(state, item, c)
                : item.FieldContext.Kind == BattleLimitedMarkers.SteadfastDefend ? true
                : item.FieldContext.Kind is "COMBAT_MIGHT" or "STANDBY_DEFEND" ? c.ControllerId != item.ControllerId
                && IsCombatMightTargetHere(state, item, c.ObjectId)
                : item.FieldContext.Kind is "SECOND_DRAW" or "RECYCLE_BOON" ? c.ControllerId == item.ControllerId
                : c.ControllerId != item.ControllerId && state.BattleState.AttackerObjectIds.Contains(c.ObjectId)
                    && state.ObjectLocations.TryGetValue(c.ObjectId, out var location) && location.BattlefieldObjectId == item.FieldContext.BattlefieldId))
        .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static ResolutionResult PrepareTriggerConfirmation(ResolutionResult result)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress) return result;
        var state = result.State;
        if (state.PendingCardChoice is not null || state.PendingPayment is not null || state.PendingHandChoice is not null
            || state.PendingEffectPlay is not null || state.TriggerQueue.Any(IsImmediateTrigger)) return result;
        // Confirm pending items in insertion order, regardless of which rule family
        // supplies their choices or costs (CN 337.1.b).
        var item = state.StackItems.FirstOrDefault(i =>
            HasMovementDamage(i) && i.TargetGenerations is null ||
            i.DravenBattleDeathScore is { OpponentId: null } ||
            i.EffectKind == EndAbilityEffect && i.EndAbility is { Kind: TriggerKinds.TurnEndReadyRunes or "DELAYED_READY_RUNES" } && i.TargetGenerations is null || i.MightySkill is { Confirmed: false } || NeedsAttachmentModeConfirmation(i) || i.Tempered is { Equipment: null } || i.DamageDistribution is not null && i.TargetGenerations is null || i.LocationDamage is not null && i.TargetGenerations is null || i.ReflexiveDuel is not null && i.TargetGenerations is null || NeedsTriggerCostConfirmation(i) || NeedsHeldTargetConfirmation(i) || i.UnitConquest is not null && i.TargetGenerations is null
            || i.PermanentPlayTargetsPending || i.UnitPlayMight is not null && i.TargetGenerations is null || i.ReflexiveCopy is { TargetConfirmed: false }
            || i.CreatedTokenReady is not null && !CreatedTokenReadyHasPayment(i) && i.TargetGenerations is null
            || i.HighCostPlayObserver is not null && i.TargetGenerations is null
            || i.SpellContext is { } spell && NeedsSpellTriggerChoice(spell) && i.TargetGenerations is null
            || i.FieldContext is not null and not { Kind: "ENEMY_ATTACK_MIGHT" or "ATTACK_DECK_PLAY" or AttackAreaDamageKind or StunReadyPowerKind or BattleLimitedMarkers.DoubleAttack or BattleLimitedMarkers.DoubleDefend } && i.TargetGenerations is null
            || i.InsightContext is { Kind: "DUEL", PaymentAccepted: false } || NeedsMovementDiscardRuneConfirmation(i));
        if (item is null) return result;
        if (HasMovementDamage(item)) return PrepareMovementDamage(result, item);
        if (item.DravenBattleDeathScore is not null) return PrepareDravenDeathOpponent(result, item);
        if (item.HighCostPlayObserver is not null) return PrepareHighCostRuneChoice(result, item);
        if (NeedsMovementDiscardRuneConfirmation(item)) return PrepareMovementDiscardRunes(result, item);
        if (item.EndAbility is not null) return PrepareEndRuneTargets(result, item);
        if (item.PermanentPlayTargetsPending) return PreparePermanentPlayTargets(result, item);
        if (item.UnitPlayMight is not null) return PrepareUnitPlayMight(result, item);
        if (item.MightySkill is { Confirmed: false }) return PrepareMightySkill(result, item);
        if (NeedsAttachmentModeConfirmation(item)) return PrepareAttachmentMode(result, item);
        if (item.Tempered is not null) return PrepareTempered(result, item);
        if (item.DamageDistribution is not null) return PrepareDistributionTargets(result, item);
        if (item.LocationDamage is not null) return PrepareLocationDamage(result, item);
        if (item.ReflexiveDuel is not null) return PrepareReflexiveDuelTarget(result, item);
        if (item.UnitConquest is not null) return PrepareConquestTarget(result, item);
        if (HasLegendConquestTarget(item)) return PrepareLegendConquestTarget(result, item);
        if (NeedsHeldTargetConfirmation(item)) return PrepareHeldTargetConfirmation(result, item);
        if (NeedsTriggerCostConfirmation(item)) return PrepareTriggerCostConfirmation(result, item);
        if (item.ReflexiveCopy is { TargetConfirmed: false }) return PrepareReflexiveCopyConfirmation(result);
        if (item.CreatedTokenReady is not null) return PrepareCreatedTokenReady(result, item);
        if (item.SpellContext is not null) return PrepareSpellTriggerConfirmation(result);
        PendingCardChoiceState? choice = null; PendingPaymentState? payment = null;
        if (item.InsightContext is not null)
            payment = new("INSIGHT-PAY:" + item.StackItemId, "INSIGHT_EFFECT", item.ControllerId,
                manaCost: 1, legalPaymentChoiceIds: ["PAY", "DECLINE"], reason: "黛安娜：支付 1 法力确认技能，随后对手可以响应")
                { ResolvingStackItemId = item.StackItemId };
        else
        {
            var legal = FieldTriggerTargets(state, item);
            if (legal.Length == 0 || item.FieldContext!.Kind == "DEFEND"
                && (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source) || source.ObjectGeneration != item.FieldContext.SourceGeneration
                    || source.ControllerId != item.ControllerId || !IsObjectOnField(state.PlayerZones, item.SourceObjectId)))
                return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
            choice = new("TRIGGER-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
                item.FieldContext.Kind is "DEFEND" or PaidUnitStunKind ? 0 : 1, 1, legal, legal,
                item.FieldContext.Kind == PaidUnitStunKind ? "选择一名单位，或不选以放弃；然后确认支付基础费用和法盾费用。"
                    : item.FieldContext.Kind == "DEFEND" ? "选择一名进攻单位并摧毁狂热粉丝，或不选以放弃；确认后开放响应"
                    : item.FieldContext.Kind == "STANDBY_DEFEND" ? "选择此处一名敌方单位；确认目标和法盾费用后开放响应，结算时展示并按待命关键词造成伤害、回收"
                    : item.FieldContext.Kind == "COMBAT_MIGHT" ? "选择此处一名敌方单位，本回合降低战力；确认目标和法盾费用后开放响应"
                    : item.FieldContext.Kind == EmpoweredBanishKind ? "选择一名符合战力上限的敌方战场单位放逐；确认目标和法盾费用后双方可以响应"
                    : item.FieldContext.Kind == EmpoweredAttackDestroyKind ? "选择此处一名战力低于来源的敌方单位；确认目标和法盾费用后双方可以响应。"
                    : item.FieldContext.Kind == AttackWeaponDamageKind ? "选择此处一名敌方单位；确认目标和法盾费用后双方可以响应。"
                    : item.FieldContext.Kind == StunBoonKind ? "选择一名友方单位获得增益；确认后双方可以响应"
                    : item.FieldContext.Kind == "RECYCLE_BOON" ? "选择一名友方单位获得增益；确认后双方可以响应"
                    : item.FieldContext.Kind == BattleLimitedMarkers.SteadfastDefend ? "选择一名单位，在本次战斗期间获得坚守2；确认目标和法盾费用后开放响应"
                    : "选择一名友方单位获得本回合 +2 战力；确认后开放响应",
                item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
        }
        state = state with { PendingCardChoice = choice, PendingPayment = payment, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static MatchState RestoreAfterTriggerConfirmation(MatchState state, StackItemState removed)
    {
        if (state.StackItems.Count > 0) return state with { ActivePlayerId = state.StackItems[^1].ControllerId,
            PriorityPlayerId = state.StackItems[^1].ControllerId, PassedPriorityPlayerIds = [] };
        if (removed.InsightContext is { Kind: "DUEL" } insight)
            return state with { TimingState = TimingStates.SpellDuelOpen, ActivePlayerId = insight.ReturnFocusPlayerId!,
                FocusPlayerId = insight.ReturnFocusPlayerId, PriorityPlayerId = null, PassedPriorityPlayerIds = [], PassedFocusPlayerIds = [] };
        if (removed.TimingContext == TimingStates.SpellDuelOpen)
        {
            var focus = removed.DamageDistribution?.ReturnFocusPlayerId ?? removed.FieldContext?.ReturnFocusPlayerId ?? state.FocusPlayerId ?? NextPlayerIdAfter(state, removed.ControllerId);
            return state with { TimingState = TimingStates.SpellDuelOpen, ActivePlayerId = focus,
                FocusPlayerId = focus, PriorityPlayerId = null, PassedPriorityPlayerIds = [], PassedFocusPlayerIds = [] };
        }
        if (state.BattleState.IsActive) return state with { TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = BattleResponsePriorityPlayerId(state, removed.ControllerId), PassedPriorityPlayerIds = [] };
        return state with { TimingState = TimingStates.NeutralOpen, ActivePlayerId = state.TurnPlayerId, PriorityPlayerId = null };
    }

    private static ResolutionResult DiscardUnconfirmedTrigger(ResolutionResult result, StackItemState item)
    {
        var state = RestoreAfterTriggerConfirmation(result.State with { StackItems = result.State.StackItems.Where(i => i.StackItemId != item.StackItemId).ToArray() }, item);
        var discarded = result with { State = state, Events = result.Events.Append(new GameEvent("TRIGGER_NOT_CONFIRMED", "未确认触发技能",
            new Dictionary<string, object?> { ["sourceObjectId"] = HasMovementDamage(item) || item.HighCostPlayObserver is not null || item.FieldContext is { } field && (field.Kind is PaidUnitStunKind or EmpoweredAttackDestroyKind or AttackWeaponDamageKind || IsStunObserverKind(field.Kind))
                ? ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") : item.SourceObjectId })).ToArray(),
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
        return ResumeDuelChainFocus(discarded);
    }

    internal static bool ValidTriggerChoice(MatchState state, PendingCardChoiceState choice)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId) is { FieldContext: { } context } item
            && (context.Kind is EmpoweredBanishKind or EmpoweredAttackDestroyKind or AttackWeaponDamageKind or PaidUnitStunKind || IsStunObserverKind(context.Kind)
                ? ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
                : ValidFieldContext(context, item.EffectKind, item.CardNo)) && item.TargetGenerations is null
            && (context.Kind != PaidUnitStunKind || context.PaidUnitTrigger is { CostPaid: false })
            && context.Kind != "ENEMY_ATTACK_MIGHT"
            && item.ControllerId == choice.PlayerId && item.SourceObjectId == choice.SourceObjectId
            && choice.ChoiceId == "TRIGGER-TARGET:" + item.StackItemId && choice.EffectKind == item.EffectKind
            && choice.RequiredCount == (context.Kind is "DEFEND" or PaidUnitStunKind ? 0 : 1) && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(FieldTriggerTargets(state, item))
            || ValidMovementDamageChoice(state, choice) || ValidHighCostRuneChoice(state, choice) || ValidUnitPlayMightChoice(state, choice) || ValidCreatedTokenReadyChoice(state, choice) || ValidPermanentPlayTargetChoice(state, choice) || ValidDistributionTargetChoice(state, choice) || ValidLocationDamageChoice(state, choice) || ValidReflexiveDuelChoice(state, choice) || ValidReflexiveCopyChoice(state, choice) || ValidHeldTargetChoice(state, choice) || ValidLegendConquestChoice(state, choice) || ValidConquestTargetChoice(state, choice);

    private static ResolutionResult ResolveTriggerTargetConfirmation(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected, bool advanceTick = true)
    {
        if (!ValidTriggerChoice(state, choice)) return RejectWithCorePrompts(state, "触发技能确认上下文已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item.LocationDamage is not null && selected.Select(id => LocationDamageKey(state.ObjectLocations[id])).Distinct(StringComparer.Ordinal).Count() != selected.Count)
            return RejectWithCorePrompts(state, "每个位置至多选择一名敌方单位。", ErrorCodes.InvalidTarget);
        if (item.PermanentPlayTargetsPending && !ValidPermanentPlayTargetSelection(state, item, selected))
            return RejectWithCorePrompts(state, "所选目标组合不满足技能条件。", ErrorCodes.InvalidTarget);
        var next = state with { Tick = state.Tick + (advanceTick ? 1 : 0), PendingCardChoice = null };
        if (selected.Count == 0 && item.CreatedTokenReady is null && item.HighCostPlayObserver is null) return DiscardUnconfirmedTrigger(new(true, null, next, [], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), item);
        var tax = ResolveSpellshieldTargetTaxPower(state, item.ControllerId, selected, out _);
        var bound = item with { TargetObjectIds = selected.ToArray(), TargetGenerations = selected.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration) };
        if (item.UnitPlayMight is { } unitPlay)
        {
            var target = state.CardObjects[selected.Single()];
            bound = bound with { UnitPlayMight = unitPlay with {
                TargetTokenFactoryCardNo = TokenObjectRules.IsToken(target) ? target.TokenFactoryCardNo ?? target.CardNo : null } };
        }
        if (HasMovementDamage(item))
        {
            var target = state.CardObjects[selected.Single()];
            bound = bound with { MovementObserver = item.MovementObserver! with {
                TargetTokenFactoryCardNo = TokenObjectRules.IsToken(target) ? target.TokenFactoryCardNo ?? target.CardNo : null } };
        }
        if (item.LocationDamage is { } locationDamage)
            bound = bound with { LocationDamage = locationDamage with { TargetLocations = selected.ToDictionary(id => id, id => state.ObjectLocations[id]) } };
        var mana = item.FieldContext?.Kind == PaidUnitStunKind ? PaidUnitStunSpec(item.FieldContext.CardNo)!.ManaCost!.Value : 0;
        if (tax > 0 || mana > 0 || item.HighCostPlayObserver is not null)
        {
            // CN 355.10.c.1 / 405.2: choose targets before paying all costs;
            // a player may decline a triggered skill that has any cost.
            var payment = new PendingPaymentState("TRIGGER-WARD:" + item.StackItemId, TriggerTargetCostWindow, item.ControllerId,
                manaCost: mana, powerCost: tax, legalPaymentChoiceIds: ["PAY", "DECLINE"], reason: item.HighCostPlayObserver is not null
                    ? $"横置来源并支付 {tax} 任意符能，或放弃此触发技能。"
                    : $"已锁定 {DeckChoiceLabel(state, selected[0])}：支付 {mana} 法力和 {tax} 任意符能，或放弃此触发技能。")
                { ResolvingStackItemId = item.StackItemId };
            next = next with { PendingPayment = payment, StackItems = next.StackItems.Select(i => i.StackItemId == item.StackItemId ? bound : i).ToArray() };
            var opened = new GameEvent("PAYMENT_WINDOW_OPENED", "已锁定触发目标，等待支付费用或放弃技能", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = HasMovementDamage(item) || item.HighCostPlayObserver is not null || item.FieldContext?.Kind is PaidUnitStunKind or EmpoweredAttackDestroyKind or AttackWeaponDamageKind
                    ? ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") : item.SourceObjectId,
                ["stackItemId"] = item.StackItemId, ["paymentId"] = payment.PaymentId,
                ["paymentWindow"] = payment.PaymentWindow, ["manaCost"] = mana, ["powerCost"] = tax,
                ["targetObjectIds"] = selected.ToArray() });
            return new(true, null, next, [opened], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
        }
        var completed = CompleteTriggerTargetConfirmation(next, bound, []);
        return completed.Accepted ? completed : RejectWithCorePrompts(state, completed.ErrorMessage!, ErrorCodes.InvalidTarget);
    }

    private static ResolutionResult CompleteTriggerTargetConfirmation(MatchState next, StackItemState item, List<GameEvent> events)
    {
        var state = next;
        var selected = item.TargetObjectIds;
        if (item.HighCostPlayObserver is { } highCost)
        {
            if (!CanPayHighCostExhaustion(state, item))
                return RejectWithCorePrompts(state, "来源已无法支付横置费用。", ErrorCodes.InvalidTarget);
            var source = state.CardObjects[item.SourceObjectId];
            next = next with { CardObjects = new Dictionary<string, CardObjectState>(next.CardObjects) {
                [source.ObjectId] = source with { IsExhausted = true } } };
            item = item with { HighCostPlayObserver = highCost with { Paid = true } };
            events.Add(new("SOURCE_EXHAUSTED", "支付触发技能横置费用", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId,
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, source.ObjectId, "__spectator__") }));
        }
        if (HasLegendConquestTarget(item))
        {
            if (!CanPayLegendConquestExhaustion(state, item))
                return RejectWithCorePrompts(state, "传奇已无法支付横置费用。", ErrorCodes.InvalidTarget);
            var source = state.CardObjects[item.SourceObjectId];
            next = next with { CardObjects = new Dictionary<string, CardObjectState>(next.CardObjects) {
                [source.ObjectId] = source with { IsExhausted = true } } };
            item = item with { TriggerCost = new(new(source.ObjectId, source.ObjectGeneration)) };
            events.Add(new("LEGEND_EXHAUSTED", "支付征服技能横置费用", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId }));
        }
        if (item.FieldContext?.Kind == "DEFEND")
        {
            var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
            if (!cards.TryGetValue(item.SourceObjectId, out var source) || source.ObjectGeneration != item.FieldContext.SourceGeneration
                || source.ControllerId != item.ControllerId || !IsObjectOnField(zones, item.SourceObjectId))
                return RejectWithCorePrompts(state, "狂热粉丝已无法支付摧毁费用。", ErrorCodes.InvalidTarget);
            var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(), state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(),
                next.RunePools, objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { item.SourceObjectId });
            if (removal.Events.Count == 0) return RejectWithCorePrompts(state, "无法支付摧毁费用。", ErrorCodes.InvalidTarget);
            next = next with { PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
                RunePools = removal.RunePools, TriggerQueue = state.TriggerQueue.Concat(removal.TriggerQueue).ToArray(),
                DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, removal.DestroyedUnitOwnerIds) };
            events.AddRange(removal.Events);
        }
        var confirmed = item with { PermanentPlayTargetsPending = false, TargetObjectIds = selected.ToArray(), TargetGenerations = selected.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration) };
        if (item.FieldContext is { Kind: "COMBAT_MIGHT" })
            next = next with { UntilEndOfTurnEffects = MarkEzrealEnemyTargetsThisTurnForUnitAbility(
                state, next.UntilEndOfTurnEffects, item.ControllerId, selected) };
        if (item.ReflexiveCopy is { } copy)
            confirmed = confirmed with { ReflexiveCopy = copy with {
                CopySource = new(selected[0], state.CardObjects[selected[0]].ObjectGeneration), TargetConfirmed = true } };
        next = RestoreAfterTriggerConfirmation(next with { StackItems = next.StackItems.Select(i => i.StackItemId == item.StackItemId ? confirmed : i).ToArray() }, item);
        events.Add(new("TRIGGER_CONFIRMED", "已确认触发技能目标与费用，等待响应", new Dictionary<string, object?> {
            ["sourceObjectId"] = HasMovementDamage(item) || item.HighCostPlayObserver is not null || item.FieldContext is { } field && (field.Kind is PaidUnitStunKind or EmpoweredAttackDestroyKind or AttackWeaponDamageKind || IsStunObserverKind(field.Kind))
                ? ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") : item.SourceObjectId,
            ["targetObjectIds"] = selected.ToArray() }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveFieldTrigger(MatchState state, StackItemState item)
    {
        if (item.FieldContext is { } battleLimited && BattleLimitedMarkers.Kind(battleLimited.Kind))
            return ResolveBattleLimitedRoleSkill(state, item);
        if (item.FieldContext is { } observer && IsStunObserverKind(observer.Kind)) return ResolveStunObserver(state, item);
        if (item.FieldContext is { Kind: EmpoweredBanishKind }) return ResolveEmpoweredBanish(state, item);
        if (item.FieldContext is { Kind: EmpoweredAttackDestroyKind }) return ResolveEmpoweredAttackDestroy(state, item);
        if (item.FieldContext is { Kind: AttackWeaponDamageKind }) return ResolveAttackWeaponDamage(state, item);
        if (item.FieldContext is { Kind: PaidUnitStunKind }) return ResolvePaidUnitStun(state, item);
        if (item.FieldContext is { Kind: "ENEMY_ATTACK_MIGHT" }) return ResolveEnemyAttackMight(state, item);
        if (item.FieldContext is { Kind: "ATTACK_DECK_PLAY" }) return ResolveAttackDeckPlay(state, item);
        if (item.FieldContext is { Kind: AttackAreaDamageKind }) return ResolveAttackAreaDamage(state, item);
        if (item.FieldContext is { Kind: "STANDBY_DEFEND" }) return ResolveStandbyDefendTrigger(state, item);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var events = new List<GameEvent>(); var triggers = new List<TriggerQueueItemState>();
        var target = item.TargetObjectIds.SingleOrDefault();
        if (target is not null && item.TargetGenerations?.GetValueOrDefault(target, -1) == cards.GetValueOrDefault(target)?.ObjectGeneration
            && FieldTriggerTargets(state, item).Contains(target))
        {
            var card = cards[target];
            if (item.FieldContext!.Kind == "COMBAT_MIGHT")
            {
                var spec = CombatMightSpec(item.FieldContext.CardNo)!;
                var behavior = new CardBehaviorDefinition(item.CardNo, "战斗身份触发", 0, item.EffectKind, 0, 1,
                    TargetScope: CardTargetScopes.EnemyUnit, PowerModifierAmount: spec.PowerDelta!.Value,
                    MinimumPowerAfterModifier: spec.MinimumPowerAfterModifier!.Value);
                cards[target] = ApplyPowerModifier(state, card, behavior, item, target, spec.PowerDelta.Value, out var modified);
                events.Add(modified);
            }
            else if (item.FieldContext.Kind == "RECYCLE_BOON")
            {
                using var instruction = new MightInstructionScope(state, zones, cards, events);
                GrantLegendBoon(cards, target, item.ControllerId, item.SourceObjectId, item.EffectKind, events);
            }
            else if (item.FieldContext.Kind == "SECOND_DRAW")
            {
                cards[target] = ApplyDirectUntilEndPowerModifier(card, target, item.SourceObjectId, item.CardNo,
                    item.EffectKind, "SECOND_CARD_DRAWN", 2, card.Power + 2);
                events.Add(new("UNIT_POWER_CHANGED", "冰封宝石：本回合战力 +2", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = target, ["powerDelta"] = 2 }));
            }
            else
            {
                var controller = card.ControllerId!; var equipment = AttachedEquipmentObjectIds(cards, target).ToArray();
                if (!BattlefieldLocalRules.PreventsMoveToBase(state, target) && TryMoveTargetToOwnerBase(zones, cards, item.ControllerId, target, out _))
                {
                    cards[target] = card with { IsAttacking = false, IsDefending = false };
                    events.Add(new("UNIT_MOVED_TO_BASE", "狂热粉丝将进攻单位移动到其基地", new Dictionary<string, object?> {
                        ["playerId"] = controller, ["sourceObjectId"] = target, ["targetObjectId"] = target,
                        ["effectSourceObjectId"] = item.SourceObjectId, ["originZone"] = "BATTLEFIELD", ["destinationZone"] = "BASE",
                        ["origin"] = "BATTLEFIELD:" + item.FieldContext.BattlefieldId, ["destination"] = "BASE" }));
                    events.AddRange(MoveAttachedEquipmentWithHost(zones, equipment, controller, target, "BASE"));
                    events.Add(CaptureMovementObservers(state, cards, target, item.ControllerId, "BASE"));
                }
            }
        }
        events.Add(new("TRIGGER_RESOLVED", "触发技能结算完成", new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events, TriggerQueue = triggers,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
