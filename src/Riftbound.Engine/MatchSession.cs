using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record CardObjectState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StandbyPlacement? StandbyPlacement { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StandbyPlacement? LastStandbyPlay { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsEmpowered { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AttachmentUnitCopyLedger? AttachmentUnitCopies { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CopyFaceTransition>? CopyFaceTransitions { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AttachedRuleTextState? AttachedRuleText { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long AttachmentRevision { get; init; }
    // The live battlefield object retains its identity; this links the replaced
    // physical card kept in exile under a separate inventory id (CN 438).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? ReplacedBattlefieldCard { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReplacedAtBattlefieldId { get; init; }

    // Intrinsic token identity survives copying and changes to tags/card face (CN 185.1).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenFactoryCardNo { get; init; }

    [JsonConstructor]
    public CardObjectState(
        string? objectId = null,
        int damage = 0,
        IReadOnlyList<string>? untilEndOfTurnEffects = null,
        bool isFaceDown = false,
        bool isAttacking = false,
        bool isDefending = false,
        int power = 0,
        int untilEndOfTurnPowerModifier = 0,
        bool isExhausted = false,
        IReadOnlyList<string>? tags = null,
        int manaCost = 0,
        string? attachedToObjectId = null,
        string? cardNo = null,
        string? ownerId = null,
        string? controllerId = null,
        IReadOnlyList<PowerModifierLedgerEntry>? untilEndOfTurnPowerModifiers = null,
        long objectGeneration = 0)
    {
        ObjectId = string.IsNullOrWhiteSpace(objectId) ? string.Empty : objectId.Trim();
        Damage = Math.Max(0, damage);
        UntilEndOfTurnEffects = NormalizeEffects(untilEndOfTurnEffects);
        IsFaceDown = isFaceDown;
        IsAttacking = isAttacking;
        IsDefending = isDefending;
        Power = power;
        UntilEndOfTurnPowerModifier = untilEndOfTurnPowerModifier;
        IsExhausted = isExhausted;
        Tags = NormalizeTags(tags);
        ManaCost = Math.Max(0, manaCost);
        AttachedToObjectId = NormalizeOptionalText(attachedToObjectId);
        CardNo = NormalizeOptionalText(cardNo);
        OwnerId = NormalizeOptionalText(ownerId);
        ControllerId = NormalizeOptionalText(controllerId);
        UntilEndOfTurnPowerModifiers = NormalizePowerModifierLedger(untilEndOfTurnPowerModifiers);
        ObjectGeneration = Math.Max(0, objectGeneration);
    }

    public string ObjectId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long ObjectGeneration { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int MovementCountThisTurn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? DamageMarkedByPlayerIds { get; init; }
    // Granted rule text stays active on attached permanents (CN 135.4 / 718.2).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? GrantedTags { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RuleDerivedTags { get; init; }

    public int Damage { get; init; }

    public IReadOnlyList<string> UntilEndOfTurnEffects { get; init; }

    public bool IsFaceDown { get; init; }

    public bool IsAttacking { get; init; }

    public bool IsDefending { get; init; }

    public int Power { get; init; }

    public int UntilEndOfTurnPowerModifier { get; init; }

    public bool IsExhausted { get; init; }

    public IReadOnlyList<string> Tags { get; init; }

    public int ManaCost { get; init; }

    public string? AttachedToObjectId { get; init; }

    public string? CardNo { get; init; }

    public string? OwnerId { get; init; }

    public string? ControllerId { get; init; }

    public IReadOnlyList<PowerModifierLedgerEntry> UntilEndOfTurnPowerModifiers { get; init; }

    private static IReadOnlyList<string> NormalizeEffects(IReadOnlyList<string>? effectIds)
    {
        return (effectIds ?? [])
            .Where(effectId => !string.IsNullOrWhiteSpace(effectId))
            .Select(effectId => effectId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(effectId => effectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        return (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PowerModifierLedgerEntry> NormalizePowerModifierLedger(
        IReadOnlyList<PowerModifierLedgerEntry>? entries)
    {
        var normalized = (entries ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry.EffectId)
                && entry.PowerDelta != 0)
            .ToArray();

        if (normalized.Any(entry => entry.AppliedOrder.HasValue))
        {
            return normalized
                .Select((entry, index) => new { Entry = entry, Index = index })
                .OrderBy(item => item.Entry.AppliedOrder ?? item.Index + 1)
                .ThenBy(item => item.Index)
                .Select(item => item.Entry)
                .ToArray();
        }

        return normalized
            .OrderBy(entry => entry.EffectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record StackItemState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FlowPlayReceipt? FlowPlay { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObservedDeckPlayContext? ObservedDeckPlay {get;init;}
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObservedDeckPlayPermission? ObservedPlayPermission {get;init;}
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EndAbilityContext? EndAbility { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DestroyDeckPlayContext? DestroyDeckPlay { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TurnUnitPlayBoonContext? TurnUnitPlayBoon { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeckPlaySequenceContext? DeckPlaySequence { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeckPlayPendingCardContext? DeckPlayPendingCard { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeckPlayReflexiveContext? DeckPlayReflexive { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResolutionDamageChoiceContext? ResolutionDamageChoice { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MovementFollowUpContext? MovementFollowUp { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourceReplayProgress? SourceReplay { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SourceReplayDecisionCompleted { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StandbyPlayedObserverContext? StandbyObserver { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StandbyPlacement? PlayedFromStandby { get; init; }
    [JsonRequired]
    public bool PlayedFromHand { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathMightContext? LastBreathMight { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathUnitCreationContext? LastBreathUnitCreation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathRuneCallContext? LastBreathRuneCall { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathDrawContext? LastBreathDraw { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathBattlefieldDamageContext? LastBreathBattlefieldDamage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DelayedEmpowerContext? DelayedEmpower { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EmpowerOtherObserverContext? EmpowerOtherObserver { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DelayedDestructionContext? DelayedDestruction { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DamageDestructionContext? DamageDestruction { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GraveyardPlayContext? GraveyardPlay { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MightySkillContext? MightySkill { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattlefieldConquestMightContext? BattlefieldConquestMight { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TargetedPowerAbilityContext? TargetedPowerAbility { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EmpowerActivationContext? EmpowerActivation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DamageDistributionContext? DamageDistribution { get; init; }
    public AttachmentObserverContext? AttachmentObserver { get; init; }
    public TemperedContext? Tempered { get; init; }
    public LegendAttachmentContext? LegendAttachment { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LegendDuelMovementContext? LegendDuelMovement { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LegendUnitKeywordContext? LegendUnitKeyword { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LegendUnitEffectContext? LegendUnitEffect { get; init; }
    public AzirMovementContext? AzirMovement { get; init; }
    public EquipContext? Equip { get; init; }
    public IReadOnlyList<string>? MovementDestinations { get; init; }
    public TriggerCostReceipt? TriggerCost { get; init; }
    public TokenEntryPlan? TokenEntryPlan { get; init; }
    public int CompletedRepeatExecutions { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CreatedTokenReadyContext? CreatedTokenReady { get; init; }
    public ReflexiveCopyContext? ReflexiveCopy { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? SelfCopySource { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? PermanentPlaySource { get; init; }
    public UnitEntryTriggerContext? UnitEntryContext { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnitPlayMightContext? UnitPlayMight { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourcePlayDrawContext? SourcePlayDraw { get; init; }
    public EmpowerInteractionContext? EmpowerInteraction { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReadyObjectTriggerContext? ReadyObjectTrigger { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourceReadiedContext? SourceReadied { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnitBattleWinDrawContext? UnitBattleWinDraw { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LegendBattleWinDrawContext? LegendBattleWinDraw { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KhazixBattleWinContext? KhazixBattleWin { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DravenBattleWinScoreContext? DravenBattleWinScore { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DravenBattleDeathScoreContext? DravenBattleDeathScore { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SivirRuneRecycleContext? SivirRuneRecycle { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ConquestTokenContext? ConquestToken { get; init; }

    public AfterPlayRecycleInstruction? AfterPlayRecycle { get; init; }
    public RecycledUnitReceipt? RecycledUnit { get; init; }
    public CardPlayCostReceipt? PlayCost { get; init; }
    public RecastTriggerContext? RecastContext { get; init; }
    public SpellTriggerContext? SpellContext { get; init; }
    [JsonConstructor]
    public StackItemState(
        string? stackItemId = null,
        string? controllerId = null,
        string? sourceObjectId = null,
        string? effectKind = null,
        string? cardNo = null,
        IReadOnlyList<string>? targetObjectIds = null,
        int damageAmount = 0,
        int effectRepeatCount = 1,
        IReadOnlyList<string>? optionalCosts = null,
        bool playedAfterAnotherCardThisTurn = false,
        string? destination = null,
        string? timingContext = null,
        IReadOnlyDictionary<string, long>? targetGenerations = null,
        bool sourceConfirmed = false,
        bool effectPlayCompleted = false)
    {
        StackItemId = Normalize(stackItemId);
        ControllerId = Normalize(controllerId);
        SourceObjectId = Normalize(sourceObjectId);
        EffectKind = Normalize(effectKind);
        CardNo = Normalize(cardNo);
        TargetObjectIds = NormalizeList(targetObjectIds);
        DamageAmount = Math.Max(0, damageAmount);
        EffectRepeatCount = Math.Max(1, effectRepeatCount);
        OptionalCosts = NormalizeList(optionalCosts);
        PlayedAfterAnotherCardThisTurn = playedAfterAnotherCardThisTurn;
        Destination = Normalize(destination);
        TimingContext = Normalize(timingContext);
        TargetGenerations = targetGenerations;
        SourceConfirmed = sourceConfirmed;
        EffectPlayCompleted = effectPlayCompleted;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SpellExecutionState>? RepeatExecutions { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? TargetStackSources { get; init; }
    public int CompletedHandExecutions { get; init; }
    public int CompletedDeckExecutions { get; init; }
    public bool DeckChoiceCompleted { get; init; }
    public bool InsightCompleted { get; init; }

    public string StackItemId { get; init; }

    public string ControllerId { get; init; }

    public string SourceObjectId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HeldTriggerContext? HeldContext { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InsightTriggerContext? InsightContext { get; init; }
    public DeathRevealContext? DeathRevealContext { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public HandDiscardObserverContext? HandDiscardObserver { get; init; }
    public DeathObserverContext? DeathObserver { get; init; }
    public MovementObserverContext? MovementObserver { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FieldTriggerContext? FieldContext { get; init; }
    public LegendConquestContext? LegendConquest { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReflexiveDuelContext? ReflexiveDuel { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattlefieldStartContext? BattlefieldStart { get; init; }
    public EphemeralStartContext? EphemeralStart { get; init; }
    public BeginningRecycleContext? BeginningRecycle { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BeginningRuneContext? BeginningRune { get; init; }
    public BeginningDrawContext? BeginningDraw { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LocationDamageContext? LocationDamage { get; init; }

    public UnitConquestContext? UnitConquest { get; init; }


    public string EffectKind { get; init; }

    public string CardNo { get; init; }

    public IReadOnlyList<string> TargetObjectIds { get; init; }

    public int DamageAmount { get; init; }

    public int EffectRepeatCount { get; init; }

    public IReadOnlyList<string> OptionalCosts { get; init; }

    public bool PlayedAfterAnotherCardThisTurn { get; init; }

    public string Destination { get; init; }

    public string TimingContext { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, long>? TargetGenerations { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SourceConfirmed { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PermanentPlayTargetsPending { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EffectPlayCompleted { get; init; }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
    }
}

public sealed record TriggerQueueItemState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ControllerOrderConfirmed { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? EnqueuedAtTick { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PermanentPlayStackItemId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EndAbilityContext? EndAbility { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TurnUnitPlayBoonContext? TurnUnitPlayBoon { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StandbyPlayedObserverContext? StandbyObserver { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathMightContext? LastBreathMight { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathUnitCreationContext? LastBreathUnitCreation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathRuneCallContext? LastBreathRuneCall { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathDrawContext? LastBreathDraw { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathBattlefieldDamageContext? LastBreathBattlefieldDamage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DelayedEmpowerContext? DelayedEmpower { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EmpowerOtherObserverContext? EmpowerOtherObserver { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DelayedDestructionContext? DelayedDestruction { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DamageDestructionContext? DamageDestruction { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GraveyardPlayContext? GraveyardPlay { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MightySkillContext? MightySkill { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattlefieldConquestMightContext? BattlefieldConquestMight { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DamageDistributionContext? DamageDistribution { get; init; }
    public AttachmentObserverContext? AttachmentObserver { get; init; }
    public TemperedContext? Tempered { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CreatedTokenReadyContext? CreatedTokenReady { get; init; }
    public ReflexiveCopyContext? ReflexiveCopy { get; init; }
    public UnitEntryTriggerContext? UnitEntryContext { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnitPlayMightContext? UnitPlayMight { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourcePlayDrawContext? SourcePlayDraw { get; init; }
    public EmpowerInteractionContext? EmpowerInteraction { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReadyObjectTriggerContext? ReadyObjectTrigger { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourceReadiedContext? SourceReadied { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnitBattleWinDrawContext? UnitBattleWinDraw { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LegendBattleWinDrawContext? LegendBattleWinDraw { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KhazixBattleWinContext? KhazixBattleWin { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DravenBattleWinScoreContext? DravenBattleWinScore { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DravenBattleDeathScoreContext? DravenBattleDeathScore { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SivirRuneRecycleContext? SivirRuneRecycle { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ConquestTokenContext? ConquestToken { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceCardNo { get; init; }

    public RecastTriggerContext? RecastContext { get; init; }
    public SpellTriggerContext? SpellContext { get; init; }
    [JsonConstructor]
    public TriggerQueueItemState(
        string? triggerId = null,
        string? controllerId = null,
        string? sourceObjectId = null,
        string? effectKind = null,
        string? triggeredByEventKind = null,
        string? timingContext = null)
    {
        TriggerId = Normalize(triggerId);
        ControllerId = Normalize(controllerId);
        SourceObjectId = Normalize(sourceObjectId);
        EffectKind = Normalize(effectKind);
        TriggeredByEventKind = Normalize(triggeredByEventKind);
        TimingContext = Normalize(timingContext);
    }

    public string TriggerId { get; init; }

    public string ControllerId { get; init; }

    public string SourceObjectId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HeldTriggerContext? HeldContext { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InsightTriggerContext? InsightContext { get; init; }
    public DeathRevealContext? DeathRevealContext { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public HandDiscardObserverContext? HandDiscardObserver { get; init; }
    public DeathObserverContext? DeathObserver { get; init; }
    public MovementObserverContext? MovementObserver { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FieldTriggerContext? FieldContext { get; init; }
    public LegendConquestContext? LegendConquest { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReflexiveDuelContext? ReflexiveDuel { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattlefieldStartContext? BattlefieldStart { get; init; }
    public EphemeralStartContext? EphemeralStart { get; init; }
    public BeginningRecycleContext? BeginningRecycle { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BeginningRuneContext? BeginningRune { get; init; }
    public BeginningDrawContext? BeginningDraw { get; init; }

    public UnitConquestContext? UnitConquest { get; init; }


    public string EffectKind { get; init; }

    public string TriggeredByEventKind { get; init; }

    public string TimingContext { get; init; }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed record PendingHandChoiceState
{
    [JsonConstructor]
    public PendingHandChoiceState(
        string? choiceId = null,
        string? choiceWindow = null,
        string? playerId = null,
        int requiredCount = 0,
        int maxCount = 0,
        IReadOnlyList<string>? legalObjectIds = null,
        string? reason = null,
        string? sourceObjectId = null,
        string? effectKind = null,
        int drawCount = 2)
    {
        ChoiceId = Normalize(choiceId);
        ChoiceWindow = Normalize(choiceWindow);
        PlayerId = Normalize(playerId);
        RequiredCount = Math.Max(0, requiredCount);
        MaxCount = Math.Max(RequiredCount, maxCount);
        LegalObjectIds = NormalizeList(legalObjectIds);
        Reason = Normalize(reason);
        SourceObjectId = Normalize(sourceObjectId);
        EffectKind = Normalize(effectKind);
        DrawCount = Math.Max(0, drawCount);
    }

    public string ChoiceId { get; init; }

    public string ChoiceWindow { get; init; }

    public string PlayerId { get; init; }

    public int RequiredCount { get; init; }

    public int MaxCount { get; init; }

    public IReadOnlyList<string> LegalObjectIds { get; init; }

    public string Reason { get; init; }

    public string SourceObjectId { get; init; }

    public string EffectKind { get; init; }

    public int DrawCount { get; init; }

    public MovementObserverContext? MovementObserver { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastBreathDrawContext? LastBreathDraw { get; init; }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record MatchState
{
    [JsonRequired]
    public RuleInputIdentity? RuleInputs { get; init; } = RuleInputIdentity.Current;

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RuleChoiceContinuation? PendingRuleChoice { get; init; }
    public IReadOnlyList<FaceDownLookPermission> FaceDownLookPermissions { get; init; } = [];
    public IReadOnlyList<LinkedExileGroup> LinkedExiles { get; init; } = [];
    public TurnDeathLedger DeathLedger { get; init; } = new(0, new Dictionary<string,int>());

    public TurnDrawLedger DrawLedger { get; init; } = new(0, new Dictionary<string, int>());

    [JsonRequired]
    public TurnCardPlayHistory CardPlays { get; init; } = new(0, [], []);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuleTextModeUseLedger? RuleTextModeUses { get; init; }

    private const string BattleResponseDeclarationContextPrefix = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
    private const string BattleDamageAssignmentLedgerPrefix = "BATTLE_DAMAGE_ASSIGNMENT_LEDGER:";

    public MatchState(
        string roomId,
        long tick,
        int turnNumber,
        string activePlayerId,
        IReadOnlyDictionary<string, string> seats)
        : this(roomId, tick, turnNumber, activePlayerId, seats, status: null)
    {
    }

    [JsonConstructor]
    public MatchState(
        string roomId,
        long tick,
        int turnNumber,
        string activePlayerId,
        IReadOnlyDictionary<string, string>? seats,
        string? status = null,
        IReadOnlyList<string>? readyPlayerIds = null,
        string? turnPlayerId = null,
        string? phase = null,
        string? timingState = null,
        IReadOnlyDictionary<string, RunePool>? runePools = null,
        IReadOnlyDictionary<string, PlayerZones>? playerZones = null,
        IReadOnlyDictionary<string, int>? playerScores = null,
        IReadOnlyDictionary<string, CardObjectState>? cardObjects = null,
        string? priorityPlayerId = null,
        IReadOnlyList<string>? passedPriorityPlayerIds = null,
        IReadOnlyList<StackItemState>? stackItems = null,
        string? focusPlayerId = null,
        IReadOnlyList<string>? passedFocusPlayerIds = null,
        string? winnerPlayerId = null,
        IReadOnlyList<string>? destroyedUnitOwnerIdsThisTurn = null,
        long seed = 0,
        long rngCursor = 0,
        IReadOnlyList<string>? untilEndOfTurnEffects = null,
        string? extraTurnPlayerId = null,
        IReadOnlyDictionary<string, int>? playerExperience = null,
        IReadOnlyDictionary<string, int>? playerCardsPlayedThisTurn = null,
        IReadOnlyList<TriggerQueueItemState>? triggerQueue = null,
        IReadOnlyDictionary<string, OfficialDecklist>? playerDecklists = null,
        IReadOnlyList<string>? mulliganCompletedPlayerIds = null,
        string? openingSecondActionPlayerId = null,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations = null,
        IReadOnlyList<BattlefieldResolutionState>? battlefieldResolutions = null,
        IReadOnlyList<BattleResolutionState>? battleResolutions = null,
        PendingPaymentState? pendingPayment = null,
        PendingHandChoiceState? pendingHandChoice = null,
        PendingCardChoiceState? pendingCardChoice = null,
        PendingEffectPlayState? pendingEffectPlay = null)
    {
        RoomId = roomId;
        Tick = tick;
        TurnNumber = turnNumber;
        ActivePlayerId = activePlayerId;
        Seats = seats is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(seats, StringComparer.Ordinal);
        Status = string.IsNullOrWhiteSpace(status)
            ? InferStatus(Seats)
            : status.Trim();
        ReadyPlayerIds = NormalizeReadyPlayers(readyPlayerIds);
        TurnPlayerId = string.IsNullOrWhiteSpace(turnPlayerId)
            ? activePlayerId
            : turnPlayerId.Trim();
        Phase = string.IsNullOrWhiteSpace(phase)
            ? InferPhase(Status)
            : phase.Trim();
        TimingState = string.IsNullOrWhiteSpace(timingState)
            ? InferTimingState(Status)
            : timingState.Trim();
        RunePools = NormalizeRunePools(runePools);
        PlayerZones = NormalizePlayerZones(playerZones);
        ObjectLocations = NormalizeObjectLocations(objectLocations);
        PlayerScores = NormalizePlayerScores(playerScores);
        PlayerExperience = NormalizePlayerExperience(playerExperience);
        PlayerCardsPlayedThisTurn = NormalizePlayerCardsPlayedThisTurn(playerCardsPlayedThisTurn);
        PlayerDecklists = NormalizeOfficialDecklists(playerDecklists);
        MulliganCompletedPlayerIds = NormalizeTextList(mulliganCompletedPlayerIds);
        OpeningSecondActionPlayerId = NormalizeOptionalText(openingSecondActionPlayerId);
        CardObjects = NormalizeCardObjects(cardObjects);
        BattlefieldResolutions = NormalizeBattlefieldResolutions(battlefieldResolutions);
        BattleResolutions = NormalizeBattleResolutions(battleResolutions);
        PendingPayment = NormalizePendingPayment(pendingPayment);
        PendingHandChoice = NormalizePendingHandChoice(pendingHandChoice);
        PendingCardChoice = NormalizePendingCardChoice(pendingCardChoice);
        PendingEffectPlay = pendingEffectPlay;
        PriorityPlayerId = NormalizeOptionalText(priorityPlayerId);
        PassedPriorityPlayerIds = NormalizeTextList(passedPriorityPlayerIds);
        StackItems = NormalizeStackItems(stackItems);
        TriggerQueue = NormalizeTriggerQueue(triggerQueue);
        FocusPlayerId = NormalizeOptionalText(focusPlayerId);
        PassedFocusPlayerIds = NormalizeTextList(passedFocusPlayerIds);
        WinnerPlayerId = NormalizeOptionalText(winnerPlayerId);
        DestroyedUnitOwnerIdsThisTurn = NormalizeTextList(destroyedUnitOwnerIdsThisTurn);
        Seed = seed;
        RngCursor = Math.Max(0, rngCursor);
        UntilEndOfTurnEffects = NormalizeTextList(untilEndOfTurnEffects);
        ExtraTurnPlayerId = NormalizeOptionalText(extraTurnPlayerId);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TurnStartStep { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EndStepContinuation? EndStep { get; init; }

    public IReadOnlyList<DelayedResourceGain> DelayedResourceGains { get; init; } = [];

    public string RoomId { get; init; }

    public long Tick { get; init; }

    public int TurnNumber { get; init; }

    public string ActivePlayerId { get; init; }

    public IReadOnlyDictionary<string, string> Seats { get; init; }

    public string Status { get; init; }

    public IReadOnlyList<string> ReadyPlayerIds { get; init; }

    public string TurnPlayerId { get; init; }

    public string Phase { get; init; }

    public string TimingState { get; init; }

    public TurnWindowState TurnWindow => BuildTurnWindowState(this);

    public SpellDuelState SpellDuelState => BuildSpellDuelState(this);

    public BattleState BattleState => BuildBattleState(this);

    public IReadOnlyDictionary<string, RunePool> RunePools { get; init; }

    public IReadOnlyDictionary<string, PlayerZones> PlayerZones { get; init; }

    public IReadOnlyDictionary<string, ObjectLocationState> ObjectLocations { get; init; }

    public IReadOnlyDictionary<string, BattlefieldState> BattlefieldStates => BuildBattlefieldStates(this);

    public IReadOnlyList<CleanupTaskState> PendingCleanupTasks => BuildPendingCleanupTasks(this);

    public IReadOnlyList<BattlefieldTaskState> BattlefieldTasks => BuildBattlefieldTaskStates(this);

    public IReadOnlyList<BattlefieldResolutionState> BattlefieldResolutions { get; init; }

    public IReadOnlyList<BattleResolutionState> BattleResolutions { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleResultContinuation? PendingBattleResult { get; init; }

    public PendingTaskQueueState PendingTaskQueue => BuildPendingTaskQueue(this);

    public IReadOnlyList<ContinuousEffectState> ContinuousEffects => BuildContinuousEffectStates(this);

    public PendingPaymentState? PendingPayment { get; init; }

    public PendingHandChoiceState? PendingHandChoice { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DuelChainFocusState? DuelChainFocus { get; init; }

    public PendingCardChoiceState? PendingCardChoice { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeckObservationView? DeckObservationView {get;init;}
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ObservedDeckPlayContext>? ObservedDeckPlays {get;init;}
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingEffectPlayState? PendingEffectPlay { get; init; }

    public IReadOnlyDictionary<string, int> PlayerScores { get; init; }

    public IReadOnlyDictionary<string, int> PlayerExperience { get; init; }

    public IReadOnlyDictionary<string, int> PlayerCardsPlayedThisTurn { get; init; }

    public IReadOnlyDictionary<string, OfficialDecklist> PlayerDecklists { get; init; }

    public IReadOnlyList<string> MulliganCompletedPlayerIds { get; init; }

    public string? OpeningSecondActionPlayerId { get; init; }

    public IReadOnlyDictionary<string, CardObjectState> CardObjects { get; init; }

    public string? PriorityPlayerId { get; init; }

    public IReadOnlyList<string> PassedPriorityPlayerIds { get; init; }

    public IReadOnlyList<StackItemState> StackItems { get; init; }

    public IReadOnlyList<TriggerQueueItemState> TriggerQueue { get; init; }

    public string? FocusPlayerId { get; init; }

    public IReadOnlyList<string> PassedFocusPlayerIds { get; init; }

    public string? WinnerPlayerId { get; init; }

    public IReadOnlyList<string> DestroyedUnitOwnerIdsThisTurn { get; init; }

    public long Seed { get; init; }

    public long RngCursor { get; init; }

    public IReadOnlyList<string> UntilEndOfTurnEffects { get; init; }

    public string? ExtraTurnPlayerId { get; init; }

    private static IReadOnlyList<ContinuousEffectState> BuildSourceParticipantCountPowerStaticAuraEffects(
        MatchState state,
        string objectId,
        CardObjectState cardObject)
    {
        var effects = new List<ContinuousEffectState>();
        if (string.IsNullOrWhiteSpace(cardObject.CardNo)
            || cardObject.IsFaceDown
            || !cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !TryFindFieldObjectLocation(state.PlayerZones, objectId, out var fieldLocation)
            || !IsPublicFieldObjectLocationCompatible(state, objectId, fieldLocation.Zone))
        {
            return effects;
        }

        var controllerId = EffectiveFieldControllerId(state, objectId, cardObject);
        if (string.IsNullOrWhiteSpace(controllerId))
        {
            return effects;
        }

        foreach (var aura in StaticAuraSpecRules.GetStaticAuras(cardObject.CardNo)
            .Where(StaticAuraSpecRules.IsSourceParticipantCountPowerStaticAura))
        {
            var participantObjectIds = SourceParticipantCountPowerStaticAuraParticipantObjectIds(
                state,
                objectId,
                fieldLocation,
                controllerId,
                aura);
            var powerDelta = SourceParticipantCountPowerDelta(aura, participantObjectIds.Count);
            if (powerDelta == 0
                && !string.Equals(
                    aura.ParticipantScope,
                    StaticAuraParticipantScopes.FriendlyPublicFieldEquipment,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var sourceDependencyObjectIds = PublicFieldDependencyObjectIds(state, [objectId]);
            var targetDependencyObjectIds = PublicFieldDependencyObjectIds(state, [objectId]);
            var participantDependencyObjectIds = PublicFieldDependencyObjectIds(state, participantObjectIds);
            var metadata = SourceParticipantCountPowerStaticAuraProjectionMetadata(aura);
            effects.Add(new ContinuousEffectState(
                $"{metadata.EffectIdPrefix}:{objectId}",
                "OBJECT",
                ContinuousEffectLayers.StaticAura,
                aura.Duration,
                objectId,
                objectId,
                powerDelta,
                SourceParticipantCountPowerStaticAuraBasePower(cardObject, aura, powerDelta),
                cardObject.Power + SourceParticipantCountPowerStaticAuraProjectedPowerAdjustment(aura, powerDelta),
                aura.Kind,
                cardObject.CardNo,
                metadata.SourcePath,
                true,
                LayerEngineFoundationResiduals(),
                Condition: metadata.Condition,
                Lifecycle: metadata.Lifecycle,
                ParticipantObjectIds: participantObjectIds,
                SourceDependencyObjectIds: sourceDependencyObjectIds,
                TargetDependencyObjectIds: targetDependencyObjectIds,
                ParticipantDependencyObjectIds: participantDependencyObjectIds));
        }

        return effects;
    }

    private static IReadOnlyList<string> SourceParticipantCountPowerStaticAuraParticipantObjectIds(
        MatchState state,
        string objectId,
        (string PlayerId, string Zone) fieldLocation,
        string controllerId,
        StaticAuraSpec aura)
    {
        if (string.Equals(
            aura.ParticipantScope,
            StaticAuraParticipantScopes.FriendlyPublicFieldEquipment,
            StringComparison.Ordinal))
        {
            return ControlledPublicFieldEquipmentObjectIds(state, controllerId);
        }

        if (string.Equals(
            aura.ParticipantScope,
            StaticAuraParticipantScopes.SameBattlefieldFriendlyFilteredPublicUnits,
            StringComparison.Ordinal))
        {
            if (!state.ObjectLocations.TryGetValue(objectId, out var sourceLocation)
                || !string.Equals(sourceLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(sourceLocation.BattlefieldObjectId))
            {
                return [];
            }

            return SameBattlefieldFriendlyFilteredUnitObjectIds(
                state,
                sourceLocation.BattlefieldObjectId,
                controllerId,
                aura);
        }

        if (string.Equals(
            aura.ParticipantScope,
            StaticAuraParticipantScopes.SameLocationOtherFriendlyPublicUnits,
            StringComparison.Ordinal))
        {
            return SameLocationOtherFriendlyUnitObjectIds(
                state,
                objectId,
                fieldLocation,
                controllerId);
        }

        return [];
    }

    private static int SourceParticipantCountPowerDelta(StaticAuraSpec aura, int participantCount)
    {
        if (string.Equals(
            aura.ParticipantScope,
            StaticAuraParticipantScopes.SameLocationOtherFriendlyPublicUnits,
            StringComparison.Ordinal))
        {
            return participantCount >= aura.RequiredParticipantCount.GetValueOrDefault(1)
                ? aura.PowerDeltaPerParticipant
                : 0;
        }

        return participantCount * aura.PowerDeltaPerParticipant;
    }

    private static int SourceParticipantCountPowerStaticAuraBasePower(
        CardObjectState cardObject,
        StaticAuraSpec aura,
        int powerDelta)
    {
        if (string.Equals(
            aura.ParticipantScope,
            StaticAuraParticipantScopes.FriendlyPublicFieldEquipment,
            StringComparison.Ordinal))
        {
            var printedPower = !string.IsNullOrWhiteSpace(cardObject.CardNo)
                && CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo, out var behavior)
                ? behavior.SourceUnitPower
                : 0;
            return printedPower > 0
                ? printedPower
                : cardObject.Power - cardObject.UntilEndOfTurnPowerModifier - powerDelta;
        }

        return cardObject.Power;
    }

    private static IReadOnlyDictionary<string, CardObjectState> NormalizeCardObjects(
        IReadOnlyDictionary<string, CardObjectState>? cardObjects)
    {
        return (cardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => NormalizeCardObject(entry.Key, entry.Value),
                StringComparer.Ordinal);
    }

    private static CardObjectState NormalizeCardObject(string objectId, CardObjectState state)
    {
        var normalizedObjectId = string.IsNullOrWhiteSpace(state.ObjectId)
            ? objectId.Trim()
            : state.ObjectId.Trim();
        return new CardObjectState(
            normalizedObjectId,
            state.Damage,
            state.UntilEndOfTurnEffects,
            state.IsFaceDown,
            state.IsAttacking,
            state.IsDefending,
            state.Power,
            state.UntilEndOfTurnPowerModifier,
            state.IsExhausted,
            state.Tags,
            state.ManaCost,
            state.AttachedToObjectId,
            state.CardNo,
            state.OwnerId,
            state.ControllerId,
            state.UntilEndOfTurnPowerModifiers,
            state.ObjectGeneration) { StandbyPlacement = state.StandbyPlacement, LastStandbyPlay = state.LastStandbyPlay, IsEmpowered = state.IsEmpowered, AttachmentUnitCopies = state.AttachmentUnitCopies, CopyFaceTransitions = state.CopyFaceTransitions?.ToArray(), AttachedRuleText = state.AttachedRuleText, AttachmentRevision = state.AttachmentRevision, GrantedTags = state.GrantedTags?.ToArray(), RuleDerivedTags = state.RuleDerivedTags?.ToArray(), DamageMarkedByPlayerIds = state.DamageMarkedByPlayerIds?.ToArray(), MovementCountThisTurn = state.MovementCountThisTurn, TokenFactoryCardNo = state.TokenFactoryCardNo, ReplacedBattlefieldCard = state.ReplacedBattlefieldCard, ReplacedAtBattlefieldId = state.ReplacedAtBattlefieldId };
    }

    private static IReadOnlyList<StackItemState> NormalizeStackItems(IReadOnlyList<StackItemState>? stackItems)
    {
        return (stackItems ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.StackItemId))
            .Select(item => new StackItemState(
                item.StackItemId,
                item.ControllerId,
                item.SourceObjectId,
                item.EffectKind,
                item.CardNo,
                item.TargetObjectIds,
                item.DamageAmount,
                item.EffectRepeatCount,
                item.OptionalCosts,
                item.PlayedAfterAnotherCardThisTurn,
                item.Destination,
                item.TimingContext,
                item.TargetGenerations,
                item.SourceConfirmed,
                item.EffectPlayCompleted) { FlowPlay=item.FlowPlay, ObservedDeckPlay=item.ObservedDeckPlay, ObservedPlayPermission=item.ObservedPlayPermission, EndAbility = item.EndAbility, DestroyDeckPlay = item.DestroyDeckPlay, DeckPlayPendingCard = item.DeckPlayPendingCard, DeckPlayReflexive = item.DeckPlayReflexive, DeckPlaySequence = item.DeckPlaySequence, TurnUnitPlayBoon = item.TurnUnitPlayBoon, ResolutionDamageChoice = item.ResolutionDamageChoice, StandbyObserver = item.StandbyObserver, PlayedFromStandby = item.PlayedFromStandby, PlayedFromHand = item.PlayedFromHand, MovementFollowUp = item.MovementFollowUp, SourceReplay = item.SourceReplay, SourceReplayDecisionCompleted = item.SourceReplayDecisionCompleted, PermanentPlayTargetsPending = item.PermanentPlayTargetsPending, EmpowerActivation = item.EmpowerActivation, TargetedPowerAbility = item.TargetedPowerAbility, DamageDistribution = item.DamageDistribution, AttachmentObserver = item.AttachmentObserver, Tempered = item.Tempered, LegendAttachment = item.LegendAttachment, LegendDuelMovement = item.LegendDuelMovement, LegendUnitKeyword = item.LegendUnitKeyword, LegendUnitEffect = item.LegendUnitEffect, AzirMovement = item.AzirMovement, Equip = item.Equip, TriggerCost = item.TriggerCost, TokenEntryPlan = item.TokenEntryPlan, CompletedRepeatExecutions = item.CompletedRepeatExecutions, ReflexiveCopy = item.ReflexiveCopy, CreatedTokenReady = item.CreatedTokenReady, SelfCopySource = item.SelfCopySource, PermanentPlaySource = item.PermanentPlaySource, UnitEntryContext = item.UnitEntryContext, UnitPlayMight = item.UnitPlayMight, SourcePlayDraw = item.SourcePlayDraw, ReadyObjectTrigger = item.ReadyObjectTrigger, SourceReadied = item.SourceReadied, UnitBattleWinDraw = item.UnitBattleWinDraw, LegendBattleWinDraw = item.LegendBattleWinDraw, KhazixBattleWin = item.KhazixBattleWin, DravenBattleWinScore = item.DravenBattleWinScore, DravenBattleDeathScore = item.DravenBattleDeathScore, HeldContext = item.HeldContext, RepeatExecutions = item.RepeatExecutions, MovementDestinations = item.MovementDestinations, TargetStackSources = item.TargetStackSources, CompletedHandExecutions = item.CompletedHandExecutions, CompletedDeckExecutions = item.CompletedDeckExecutions, DeckChoiceCompleted = item.DeckChoiceCompleted, InsightCompleted = item.InsightCompleted, InsightContext = item.InsightContext, SivirRuneRecycle = item.SivirRuneRecycle, ConquestToken = item.ConquestToken, DeathRevealContext = item.DeathRevealContext, HandDiscardObserver = item.HandDiscardObserver, DeathObserver = item.DeathObserver, MovementObserver = item.MovementObserver, FieldContext = item.FieldContext, LastBreathMight = item.LastBreathMight, LastBreathUnitCreation = item.LastBreathUnitCreation, LastBreathRuneCall = item.LastBreathRuneCall, LastBreathDraw = item.LastBreathDraw, LastBreathBattlefieldDamage = item.LastBreathBattlefieldDamage, DelayedEmpower = item.DelayedEmpower, EmpowerOtherObserver = item.EmpowerOtherObserver, DelayedDestruction = item.DelayedDestruction, DamageDestruction = item.DamageDestruction, GraveyardPlay = item.GraveyardPlay, MightySkill = item.MightySkill, BattlefieldConquestMight = item.BattlefieldConquestMight, LegendConquest = item.LegendConquest, UnitConquest = item.UnitConquest, ReflexiveDuel = item.ReflexiveDuel, BeginningRecycle = item.BeginningRecycle, BeginningRune = item.BeginningRune, BeginningDraw = item.BeginningDraw, EphemeralStart = item.EphemeralStart, BattlefieldStart = item.BattlefieldStart, LocationDamage = item.LocationDamage, SpellContext = item.SpellContext, RecastContext = item.RecastContext, AfterPlayRecycle = item.AfterPlayRecycle, RecycledUnit = item.RecycledUnit, PlayCost = item.PlayCost, EmpowerInteraction = item.EmpowerInteraction })
            .ToArray();
    }

    private static IReadOnlyList<TriggerQueueItemState> NormalizeTriggerQueue(IReadOnlyList<TriggerQueueItemState>? triggerQueue)
    {
        return (triggerQueue ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.TriggerId))
            .Select(item => new TriggerQueueItemState(
                item.TriggerId,
                item.ControllerId,
                item.SourceObjectId,
                item.EffectKind,
                item.TriggeredByEventKind,
                item.TimingContext) { ControllerOrderConfirmed = item.ControllerOrderConfirmed, EnqueuedAtTick = item.EnqueuedAtTick, PermanentPlayStackItemId = item.PermanentPlayStackItemId, EndAbility = item.EndAbility, TurnUnitPlayBoon = item.TurnUnitPlayBoon, StandbyObserver = item.StandbyObserver, DamageDistribution = item.DamageDistribution, AttachmentObserver = item.AttachmentObserver, Tempered = item.Tempered, ReflexiveCopy = item.ReflexiveCopy, CreatedTokenReady = item.CreatedTokenReady, UnitEntryContext = item.UnitEntryContext, UnitPlayMight = item.UnitPlayMight, SourcePlayDraw = item.SourcePlayDraw, ReadyObjectTrigger = item.ReadyObjectTrigger, SourceReadied = item.SourceReadied, UnitBattleWinDraw = item.UnitBattleWinDraw, LegendBattleWinDraw = item.LegendBattleWinDraw, KhazixBattleWin = item.KhazixBattleWin, DravenBattleWinScore = item.DravenBattleWinScore, DravenBattleDeathScore = item.DravenBattleDeathScore, SourceCardNo = item.SourceCardNo, HeldContext = item.HeldContext, InsightContext = item.InsightContext, SivirRuneRecycle = item.SivirRuneRecycle, ConquestToken = item.ConquestToken, DeathRevealContext = item.DeathRevealContext, HandDiscardObserver = item.HandDiscardObserver, DeathObserver = item.DeathObserver, MovementObserver = item.MovementObserver, FieldContext = item.FieldContext, LastBreathMight = item.LastBreathMight, LastBreathUnitCreation = item.LastBreathUnitCreation, LastBreathRuneCall = item.LastBreathRuneCall, LastBreathDraw = item.LastBreathDraw, LastBreathBattlefieldDamage = item.LastBreathBattlefieldDamage, DelayedEmpower = item.DelayedEmpower, EmpowerOtherObserver = item.EmpowerOtherObserver, DelayedDestruction = item.DelayedDestruction, DamageDestruction = item.DamageDestruction, GraveyardPlay = item.GraveyardPlay, MightySkill = item.MightySkill, BattlefieldConquestMight = item.BattlefieldConquestMight, LegendConquest = item.LegendConquest, UnitConquest = item.UnitConquest, ReflexiveDuel = item.ReflexiveDuel, BeginningRecycle = item.BeginningRecycle, BeginningRune = item.BeginningRune, BeginningDraw = item.BeginningDraw, EphemeralStart = item.EphemeralStart, BattlefieldStart = item.BattlefieldStart, SpellContext = item.SpellContext, RecastContext = item.RecastContext, EmpowerInteraction = item.EmpowerInteraction })
            .ToArray();
    }

    private static PendingPaymentState? NormalizePendingPayment(PendingPaymentState? pendingPayment)
    {
        if (pendingPayment is null
            || string.IsNullOrWhiteSpace(pendingPayment.PaymentId)
            || string.IsNullOrWhiteSpace(pendingPayment.PaymentWindow)
            || string.IsNullOrWhiteSpace(pendingPayment.PlayerId))
        {
            return null;
        }

        return new PendingPaymentState(
            pendingPayment.PaymentId,
            pendingPayment.PaymentWindow,
            pendingPayment.PlayerId,
            pendingPayment.ManaCost,
            pendingPayment.PowerCost,
            pendingPayment.PowerCostByTrait,
            pendingPayment.LegalPaymentChoiceIds,
            pendingPayment.Reason,
            pendingPayment.PaymentResourceActionIds) { ResolvingStackItemId = pendingPayment.ResolvingStackItemId, ExperienceCost = pendingPayment.ExperienceCost };
    }

    private static PendingHandChoiceState? NormalizePendingHandChoice(PendingHandChoiceState? pendingHandChoice)
    {
        if (pendingHandChoice is null
            || string.IsNullOrWhiteSpace(pendingHandChoice.ChoiceId)
            || string.IsNullOrWhiteSpace(pendingHandChoice.ChoiceWindow)
            || string.IsNullOrWhiteSpace(pendingHandChoice.PlayerId)
            || pendingHandChoice.RequiredCount <= 0
            || pendingHandChoice.MaxCount < pendingHandChoice.RequiredCount
            || pendingHandChoice.LegalObjectIds.Count < pendingHandChoice.RequiredCount)
        {
            return null;
        }

        return new PendingHandChoiceState(
            pendingHandChoice.ChoiceId,
            pendingHandChoice.ChoiceWindow,
            pendingHandChoice.PlayerId,
            pendingHandChoice.RequiredCount,
            pendingHandChoice.MaxCount,
            pendingHandChoice.LegalObjectIds,
            pendingHandChoice.Reason,
            pendingHandChoice.SourceObjectId,
            pendingHandChoice.EffectKind,
            pendingHandChoice.DrawCount) { MovementObserver = pendingHandChoice.MovementObserver, LastBreathDraw = pendingHandChoice.LastBreathDraw };
    }

    private static PendingCardChoiceState? NormalizePendingCardChoice(PendingCardChoiceState? pendingCardChoice)
    {
        if (pendingCardChoice is null
            || string.IsNullOrWhiteSpace(pendingCardChoice.ChoiceId)
            || string.IsNullOrWhiteSpace(pendingCardChoice.ChoiceWindow)
            || string.IsNullOrWhiteSpace(pendingCardChoice.PlayerId)
            || pendingCardChoice.RequiredCount < 0
            || pendingCardChoice.MaxCount < pendingCardChoice.RequiredCount
            || pendingCardChoice.LegalObjectIds.Count < pendingCardChoice.RequiredCount
            || pendingCardChoice.ContextObjectIds.Count == 0 && !CoreRuleEngine.IsEmptyRevealedHandChoice(pendingCardChoice))
        {
            return null;
        }

        return new PendingCardChoiceState(
            pendingCardChoice.ChoiceId,
            pendingCardChoice.ChoiceWindow,
            pendingCardChoice.PlayerId,
            pendingCardChoice.RequiredCount,
            pendingCardChoice.MaxCount,
            pendingCardChoice.LegalObjectIds,
            pendingCardChoice.ContextObjectIds,
            pendingCardChoice.Reason,
            pendingCardChoice.SourceObjectId,
            pendingCardChoice.EffectKind) { DeckContext = pendingCardChoice.DeckContext, HandContext = pendingCardChoice.HandContext, ResolvingStackItemId = pendingCardChoice.ResolvingStackItemId };
    }

    private static IReadOnlyList<BattlefieldResolutionState> NormalizeBattlefieldResolutions(
        IReadOnlyList<BattlefieldResolutionState>? battlefieldResolutions)
    {
        return (battlefieldResolutions ?? [])
            .Where(resolution => resolution is not null && !string.IsNullOrWhiteSpace(resolution.ResolutionId))
            .Select(resolution => new BattlefieldResolutionState(
                resolution.ResolutionId.Trim(),
                Math.Max(0, resolution.Tick),
                string.IsNullOrWhiteSpace(resolution.Kind) ? "UNKNOWN" : resolution.Kind.Trim(),
                string.IsNullOrWhiteSpace(resolution.Reason) ? "UNKNOWN" : resolution.Reason.Trim(),
                string.IsNullOrWhiteSpace(resolution.BattlefieldObjectId) ? string.Empty : resolution.BattlefieldObjectId.Trim(),
                NormalizeOptionalText(resolution.PlayerId),
                NormalizeOptionalText(resolution.PreviousControllerId),
                NormalizeOptionalText(resolution.ControllerId),
                NormalizeOptionalText(resolution.SourceObjectId),
                NormalizeOrderedTextList(resolution.ParticipantObjectIds),
                NormalizeOrderedTextList(resolution.RelatedEventKinds)) { PublicObjectBindings = resolution.PublicObjectBindings })
            .Where(resolution => !string.IsNullOrWhiteSpace(resolution.BattlefieldObjectId))
            .Take(12)
            .ToArray();
    }
}

public sealed partial record ResolutionResult(
    bool Accepted,
    string? ErrorMessage,
    MatchState State,
    IReadOnlyList<GameEvent> Events,
    IReadOnlyDictionary<string, SnapshotDto> Snapshots,
    IReadOnlyDictionary<string, ActionPromptDto> Prompts,
    string? ErrorCode = null)
{
    private const int BaseWinningScore = 8;
    private const string BattleDamageAssignmentLedgerPrefix = "BATTLE_DAMAGE_ASSIGNMENT_LEDGER:";

    internal static IReadOnlyList<IReadOnlyDictionary<string, object?>> OrderTriggerControllerBlockOrder(MatchState state)
        => CoreRuleEngine.TriggerInsertionControllerBlocks(state);

    internal static string OrderTriggerSummary(
        MatchState state,
        TriggerQueueItemState trigger,
        string viewerPlayerId)
    {
        if (trigger.HandDiscardObserver is not null || trigger.UnitBattleWinDraw is not null || trigger.LegendBattleWinDraw is not null) return "赢得战斗：抽一张牌；双方响应后结算。";
        if (trigger.KhazixBattleWin is not null) return "赢得战斗：获得1经验；双方响应后结算。";
        if (trigger.DravenBattleDeathScore is not null) return "战斗中被摧毁：选择一名对手获得1分；双方响应后结算。";
        if (trigger.DravenBattleWinScore is not null) return "本回合首次赢得战斗：获得1分；双方响应后结算。";
        if (IsHiddenTriggerSourceForViewer(state, trigger, viewerPlayerId))
        {
            return $"隐藏来源触发 ({trigger.TriggerId})";
        }

        if (trigger.ReflexiveDuel is not null) return "移动终点：选择另一名敌方单位互斗";
        if (trigger.UnitConquest is not null) return "征服未受控制战场：战力伤害";
        if (trigger.HandDiscardObserver is { } discard) return CoreRuleEngine.HandDiscardObserverLabel(discard);
        if (trigger.MovementObserver is { } movement) return CoreRuleEngine.MovementObserverLabel(movement);
        if (trigger.DamageDestruction is not null) return "受伤后摧毁原单位";
        if (trigger.GraveyardPlay is not null) return "支付费用，从废牌堆打出原卡牌";
        if (trigger.DelayedEmpower is { } delayedEmpower) return CoreRuleEngine.DelayedEmpowerLabel(delayedEmpower);
        if (trigger.EmpowerOtherObserver is not null) return "其他物体被强化：强化来源";
        if (trigger.DelayedDestruction is not null) return "延迟摧毁：打出休眠金币";
        if (trigger.SourceReadied is not null) return "活跃后选择本回合关键词。";
        if (trigger.ReadyObjectTrigger is not null) return "活跃除来源之外的一个休眠物体。";
        if (trigger.DeathObserver is { } death) return CoreRuleEngine.DeathObserverLabel(death);
        return string.IsNullOrWhiteSpace(trigger.EffectKind)
            ? trigger.TriggerId
            : $"{trigger.EffectKind} ({trigger.TriggerId})";
    }

    internal static string OrderTriggerVisibleText(
        MatchState state,
        TriggerQueueItemState trigger,
        string viewerPlayerId)
    {
        if (trigger.HandDiscardObserver is not null || trigger.UnitBattleWinDraw is not null || trigger.LegendBattleWinDraw is not null) return "赢得战斗：抽一张牌；双方响应后结算。";
        if (trigger.KhazixBattleWin is not null) return "赢得战斗：获得1经验；双方响应后结算。";
        if (trigger.DravenBattleDeathScore is not null) return "战斗中被摧毁：选择一名对手获得1分；双方响应后结算。";
        if (trigger.DravenBattleWinScore is not null) return "本回合首次赢得战斗：获得1分；双方响应后结算。";
        if (IsHiddenTriggerSourceForViewer(state, trigger, viewerPlayerId))
        {
            return "对手隐藏来源的触发能力";
        }

        if (trigger.HandDiscardObserver is { } discard) return CoreRuleEngine.HandDiscardObserverLabel(discard) + "；双方响应后结算。";
        if (trigger.MovementObserver is { } movement) return CoreRuleEngine.MovementObserverLabel(movement) + "；双方响应后结算。";
        if (trigger.DamageDestruction is not null) return "原单位已受到伤害；双方可以响应，结算时摧毁其原实例。";
        if (trigger.GraveyardPlay is not null) return "使用法术摧毁单位后，可选择支付基础费用确认；双方响应后，从废牌堆打出原卡牌。";
        if (trigger.DelayedEmpower is not null) return "双方响应后，对此前绑定的同一代单位执行强化状态操作。";
        if (trigger.EmpowerOtherObserver is not null) return "其他物体被你强化；双方响应后强化来源。";
        if (trigger.DelayedDestruction is not null) return "目标在本回合被摧毁；双方响应后打出一个休眠金币。";
        if (trigger.SourceReadied is not null) return "活跃后选择本回合关键词。";
        if (trigger.ReadyObjectTrigger is not null) return "活跃除来源之外的一个休眠物体。";
        if (trigger.DeathObserver is { } death) return CoreRuleEngine.DeathObserverLabel(death) + "；记录死亡时的控制者，双方响应后结算。";
        var source = string.IsNullOrWhiteSpace(trigger.SourceObjectId)
            ? "未知来源"
            : trigger.SourceObjectId;
        var eventKind = string.IsNullOrWhiteSpace(trigger.TriggeredByEventKind)
            ? "触发事件"
            : trigger.TriggeredByEventKind;
        return $"{source} 的触发能力，来源事件：{eventKind}";
    }

    private static string OrderTriggerSourceObjectIdForViewer(
        MatchState state,
        TriggerQueueItemState trigger,
        string viewerPlayerId)
    {
        if (trigger.ReadyObjectTrigger is not null || trigger.SourceReadied is not null || trigger.BeginningRune is not null || trigger.LastBreathUnitCreation is not null || trigger.LastBreathRuneCall is not null || trigger.LastBreathDraw is not null || trigger.LastBreathBattlefieldDamage is not null || trigger.DelayedEmpower is not null || trigger.EmpowerOtherObserver is not null || trigger.DeathObserver is not null || trigger.MovementObserver is not null || trigger.FieldContext?.Kind is CoreRuleEngine.EmpoweredBanishKind or CoreRuleEngine.AttackAreaDamageKind || trigger.HandDiscardObserver is not null || trigger.UnitBattleWinDraw is not null || trigger.LegendBattleWinDraw is not null || trigger.KhazixBattleWin is not null || trigger.DravenBattleWinScore is not null || trigger.DravenBattleDeathScore is not null)
            return VisibleObjectIdForViewer(state, trigger.SourceObjectId, viewerPlayerId);
        return IsHiddenTriggerSourceForViewer(state, trigger, viewerPlayerId)
            ? "HIDDEN"
            : trigger.SourceObjectId;
    }

    private static string OrderTriggerEffectKindForViewer(
        MatchState state,
        TriggerQueueItemState trigger,
        string viewerPlayerId)
    {
        return IsHiddenTriggerSourceForViewer(state, trigger, viewerPlayerId)
            ? "HIDDEN"
            : trigger.EffectKind;
    }

    private static bool IsHiddenTriggerSourceForViewer(
        MatchState state,
        TriggerQueueItemState trigger,
        string viewerPlayerId)
    {
        return trigger.EmpowerInteraction is null && trigger.DamageDestruction is null && trigger.GraveyardPlay is null && trigger.LastBreathUnitCreation is null && trigger.LastBreathRuneCall is null && trigger.LastBreathDraw is null && trigger.LastBreathBattlefieldDamage is null && trigger.DelayedEmpower is null && trigger.EmpowerOtherObserver is null && trigger.DelayedDestruction is null && trigger.ReflexiveDuel is null && trigger.UnitConquest is null && trigger.HandDiscardObserver is null && trigger.MovementObserver is null && trigger.DeathObserver is null && !string.IsNullOrWhiteSpace(trigger.SourceObjectId)
            && IsHiddenBattlefieldStandbyForViewer(state, trigger.SourceObjectId, viewerPlayerId);
    }

    internal static bool HasOpenBattleDamageAssignmentWindow(MatchState state)
    {
        var battle = state.BattleState;
        return string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
            && state.PendingBattleResult is null
            && !CoreRuleEngine.IsBattleAwaitingCombatStart(state)
            && string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            && string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            && state.PendingPayment is null
            && state.PendingHandChoice is null
            && state.PendingCardChoice is null
            && state.StackItems.Count == 0
            && battle.IsActive
            && !string.IsNullOrWhiteSpace(battle.BattleId)
            && !string.IsNullOrWhiteSpace(battle.BattlefieldObjectId)
            && battle.AttackerObjectIds.Count > 0
            && battle.DefenderObjectIds.Count > 0
            && !string.IsNullOrWhiteSpace(BattleDamageAssigningPlayerId(state));
    }

    private static bool SourceObjectControlledByPlayerOrLegacyOwned(CardObjectState cardObject, string playerId)
    {
        return CardControlRules.IsControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static Dictionary<string, object?> BuildStackItemSnapshotView(
        MatchState state,
        StackItemState item,
        string viewerPlayerId)
    {
        var hiddenSource = IsHiddenStackSourceForViewer(state, item, viewerPlayerId);
        // The public skill keeps its printing and effect, but never identifies
        // a source's new private-zone incarnation in the public source field.
        var sourceId = hiddenSource ? "HIDDEN" : item.UnitBattleWinDraw is not null || item.LegendBattleWinDraw is not null || item.KhazixBattleWin is not null || item.DravenBattleWinScore is not null || item.DravenBattleDeathScore is not null
            || item.ReadyObjectTrigger is not null || item.SourceReadied is not null || item.BeginningRune is not null || item.LastBreathUnitCreation is not null || item.LastBreathRuneCall is not null || item.LastBreathDraw is not null || item.LastBreathBattlefieldDamage is not null || item.DelayedEmpower is not null || item.EmpowerOtherObserver is not null || item.EmpowerActivation is not null || item.HandDiscardObserver is not null || item.DeathObserver is not null || item.MovementObserver is not null || item.FieldContext?.Kind is CoreRuleEngine.EmpoweredBanishKind or CoreRuleEngine.AttackAreaDamageKind
            || item.SourceConfirmed && item.PermanentPlaySource is null && !CoreRuleEngine.HasHistoricalPublicDamageSource(state, item)
            ? VisibleObjectIdForViewer(state, item.SourceObjectId, viewerPlayerId) : item.SourceObjectId;
        var view = new Dictionary<string, object?>
        {
            ["stackItemId"] = item.StackItemId,
            ["controllerId"] = item.ControllerId,
            ["sourceObjectId"] = sourceId,
            ["sourceVisibility"] = sourceId == "HIDDEN" ? "HIDDEN" : "VISIBLE",
            ["effectKind"] = hiddenSource ? "HIDDEN" : item.EffectKind,
            ["cardNo"] = hiddenSource ? "HIDDEN" : item.CardNo,
            ["targetObjectIds"] = VisibleObjectIdsForViewer(state, item.TargetObjectIds, viewerPlayerId),
            ["damageAmount"] = item.DamageAmount
        };
        if (item.RepeatExecutions is { } executions && !hiddenSource)
            view["repeatExecutions"] = executions.Select((execution, index) =>
            {
                var row = new Dictionary<string, object?> {
                    ["index"] = index, ["effectKind"] = execution.EffectKind,
                    ["targetObjectIds"] = VisibleObjectIdsForViewer(state, execution.TargetObjectIds, viewerPlayerId),
                    ["movementDestinations"] = execution.MovementDestinations, ["destination"] = execution.Destination };
                if (execution.MovementDeclarationPlayerId is not null)
                    row["movementDeclarationPlayerId"] = execution.MovementDeclarationPlayerId;
                return row;
            }).ToArray();
        if (!hiddenSource && item.HandDiscardObserver is { } discard) {
            view["handDiscardObserver"] = CoreRuleEngine.PublicHandDiscardObserver(discard);
            view["abilityLabel"] = CoreRuleEngine.HandDiscardObserverLabel(discard);
        }
        if (!hiddenSource && item.LastBreathMight is not null) view["lastBreathMight"] = item.LastBreathMight;
        if (!hiddenSource && item.InsightContext is { Kind: "LAST_BREATH" } lastBreathInsight) view["lastBreathInsight"] = lastBreathInsight;
        if (!hiddenSource && item.InsightContext is { Kind: "DUEL" } duelInsight) view["duelInsight"] = duelInsight;
        if (!hiddenSource && item.InsightContext is { Kind: "EMPOWERED" } empoweredInsight) view["empoweredInsight"] = empoweredInsight;
        if (!hiddenSource && item.DelayedDestruction is not null) view["delayedDestruction"] = item.DelayedDestruction;
        if (!hiddenSource && item.DamageDestruction is not null) view["damageDestruction"] = item.DamageDestruction;
        if (!hiddenSource && item.GraveyardPlay is not null) view["graveyardPlay"] = item.GraveyardPlay;
        if (!hiddenSource && item.SourceReplay is not null) view["sourceReplay"] = item.SourceReplay;
        if (!hiddenSource && item.ResolutionDamageChoice is not null) view["resolutionDamageChoice"] = item.ResolutionDamageChoice;
        if (!hiddenSource && item.TurnUnitPlayBoon is not null) view["turnUnitPlayBoon"] = item.TurnUnitPlayBoon;
        if (!hiddenSource && item.FlowPlay is not null) view["playedWithFlow"] = true;
        if (!hiddenSource && item.DeckPlayPendingCard is not null) view["deckPlayPendingCard"] = item.DeckPlayPendingCard;
        if (item.DeckPlayReflexive is { } deckReflexive) {
            // The captured card may since have entered a private zone. Keep the
            // binding in authoritative state, never in the public stack view.
            view["deckPlayReflexive"] = new DeckPlayReflexiveView(deckReflexive.ExecutionIndex, deckReflexive.Confirmed);
            view["abilityLabel"] = "内嵌强化";
        }
        if (!hiddenSource && item.BattlefieldConquestMight is not null) view["battlefieldConquestMight"] = item.BattlefieldConquestMight;
        if (!hiddenSource && item.MightySkill is not null) view["mightySkill"] = item.MightySkill;
        if (!hiddenSource && item.MovementDestinations is { } moves)
            view["movementDestinations"] = moves.ToArray();
        if (!hiddenSource && item.HeldContext is { } held && (held.Kind switch {
            "LEBLANC_DISCARD" => "映像创建", "VEX" => "据守抽牌", "RENATA" => "据守创建金币",
            "PAY_POWER_SCORE" => "据守额外得分", "BOON" => "据守增益", "MOVE_BASE" => "据守移回基地", "RETURN_PERMANENT" => "据守返回手牌", "RETURN_HERO" => "据守返回选定英雄", "CHANNEL_OPTIONAL" => "据守召出符文", "IVERN" => "替换为草丛", "BRUSH_RETURN" => "换回原战场", _ => null }) is { } heldLabel) view["abilityLabel"] = heldLabel;
        if (!hiddenSource && item.ReflexiveDuel is not null) view["abilityLabel"] = "移动終点的单位互斗";
        if (!hiddenSource && item.UnitConquest is not null) view["abilityLabel"] = "征服未受控制战场：战力伤害";
        if (!hiddenSource && item.LegendConquest is { } conquest) view["abilityLabel"] = conquest.Kind == "EXHAUST_DECK_PLAY" ? "征服展示并打出" : conquest.Kind == "EXHAUST_READY_UNIT" ? "征服活跃单位" : "征服活跃传奇";
        if (!hiddenSource && item.MovementObserver is { } movement) view["abilityLabel"] = CoreRuleEngine.MovementObserverLabel(movement);
        if (!hiddenSource && item.DelayedDestruction is not null) view["abilityLabel"] = "延迟摧毁：打出休眠金币";
        if (!hiddenSource && item.DamageDestruction is not null) view["abilityLabel"] = "受伤后摧毁原单位";
        if (!hiddenSource && item.GraveyardPlay is not null) view["abilityLabel"] = "支付费用，从废牌堆打出原卡牌";
        if (!hiddenSource && item.DeathObserver is { } death) view["abilityLabel"] = CoreRuleEngine.DeathObserverLabel(death);
        if (!hiddenSource && item.CreatedTokenReady is not null) view["abilityLabel"] = "内嵌活跃";
        if (!hiddenSource && item.ReflexiveCopy is not null) view["abilityLabel"] = "内嵌复制";
        if (!hiddenSource && item.UnitEntryContext is not null) view["abilityLabel"] = "进场眩晕与移动限制";
        if (!hiddenSource && item.UnitPlayMight is not null) view["abilityLabel"] = "打出单位：本回合战力 +1";
        if (!hiddenSource && state.TriggerQueue.Any(t => t.PermanentPlayStackItemId == item.StackItemId)) view["awaitingTriggerOrder"] = true;
        if (!hiddenSource && item.ReadyObjectTrigger is not null) view["abilityLabel"] = "活跃其他物体";
        if (!hiddenSource && item.SourceReadied is { } readied) { view["abilityLabel"] = "活跃后获得关键词"; if (readied.Mode is { } mode) view["mode"] = mode; }
        if (!hiddenSource && item.SourcePlayDraw is not null) view["abilityLabel"] = "其他装备条件抽牌";
        if (!hiddenSource && CoreRuleEngine.LegendUnitTokenLabel(item.EffectKind) is { } legendLabel) view["abilityLabel"] = legendLabel;
        if (!hiddenSource && item.DamageDistribution is not null) {
            view["damageDistribution"] = item.DamageDistribution;
            view["abilityLabel"] = "进攻分摊伤害";
        }
        if (!hiddenSource && item.LocationDamage is not null) {
            view["locationDamage"] = item.LocationDamage;
            view["abilityLabel"] = "每个位置至多一名敌方单位受到伤害";
        }
        if (!hiddenSource && item.AttachmentObserver is { } attachment) {
            view["abilityLabel"] = CoreRuleEngine.AttachmentObserverLabel(attachment);
            if (attachment.Mode is not null) view["mode"] = attachment.Mode;
        }
        if (!hiddenSource && item.Tempered is not null) view["abilityLabel"] = "百炼：双方响应后支付装配费用并贴附武装";
        if (!hiddenSource && item.LegendAttachment is not null) view["abilityLabel"] = "传奇技能：双方响应后贴附武装";
        if (!hiddenSource && item.LegendDuelMovement is not null) view["abilityLabel"] = "传奇技能：如果是你的回合，将法术对决中的友方单位移至基地；如果已强化，使其活跃";
        if (!hiddenSource && item.LegendUnitKeyword is not null) view["abilityLabel"] = "传奇技能：双方响应后给予单位关键词";
        if (!hiddenSource && item.LegendUnitEffect is not null) view["abilityLabel"] = "传奇技能：双方响应后处理单位";
        if (!hiddenSource && item.EmpowerOtherObserver is not null) view["abilityLabel"] = "其他物体被强化：双方响应后强化来源";
        if (!hiddenSource && item.Equip is not null) view["abilityLabel"] = "装配：结算时贴附至目标单位";
        if (item.BeginningRune is not null) {
            view["beginningRune"] = CoreRuleEngine.PublicBeginningRune(item.BeginningRune);
            view["abilityLabel"] = "开始阶段：符文较少时获得战力";
        }
        if (item.BeginningRecycle is not null) {
            view["beginningRecycle"] = item.BeginningRecycle;
            view["abilityLabel"] = "开始阶段：从废牌堆回收三张牌";
        }
        if (item.BeginningDraw is not null) {
            view["beginningDraw"] = item.BeginningDraw;
            view["abilityLabel"] = item.BeginningDraw.Kind == "HAND_BELOW_TWO" ? "开始阶段：手牌少于两张时抽一张" : "开始阶段：抽一张牌";
        }
        if (item.EphemeralStart is not null) {
            view["ephemeralStart"] = item.EphemeralStart;
            view["abilityLabel"] = "瞬息：摧毁此物体";
        }
        if (item.BattlefieldStart is not null) {
            view["battlefieldStart"] = item.BattlefieldStart;
            view["abilityLabel"] = item.BattlefieldStart.DrawCount > 0 ? "开始阶段：摧毁费用后抽牌" : "开始阶段：对该战场所有单位造成伤害";
            if (item.BattlefieldStart.DrawCount > 0) view["triggerCost"] = item.TriggerCost;
        }
        if (!hiddenSource && item.DelayedEmpower is { } delayedEmpower)
            view["abilityLabel"] = CoreRuleEngine.DelayedEmpowerLabel(delayedEmpower);
        if (item.SourceConfirmed) view["playAbility"] = true;
        if (!string.IsNullOrWhiteSpace(item.Destination))
        {
            view["destination"] = item.Destination;
        }

        return view;
    }

    private static bool IsHiddenStackSourceForViewer(
        MatchState state,
        StackItemState item,
        string viewerPlayerId)
    {
        return item.EmpowerInteraction is null && item.DamageDestruction is null && item.GraveyardPlay is null && item.LastBreathUnitCreation is null && item.LastBreathRuneCall is null && item.LastBreathDraw is null && item.LastBreathBattlefieldDamage is null && item.DelayedEmpower is null && item.EmpowerOtherObserver is null && item.DelayedDestruction is null && item.DamageDistribution is null && item.LocationDamage is null && item.ReflexiveDuel is null && item.UnitConquest is null && item.HandDiscardObserver is null && item.MovementObserver is null && item.DeathObserver is null && !string.IsNullOrWhiteSpace(item.SourceObjectId)
            && IsHiddenBattlefieldStandbyForViewer(state, item.SourceObjectId, viewerPlayerId);
    }

    private static IReadOnlyList<Dictionary<string, object?>> BuildRuleQueueCoverageSnapshotView(MatchState state)
    {
        var pendingBattleTaskCount = state.PendingTaskQueue.Tasks.Count(task => IsRuleQueueBattleTaskKind(task.Kind));
        var pendingPaymentTaskCount = state.PendingTaskQueue.Tasks.Count(task => IsRuleQueuePaymentTaskKind(task.Kind));
        var liveBattleCount =
            (state.BattleState.IsActive ? 1 : 0)
            + state.BattlefieldTasks.Count
            + pendingBattleTaskCount
            + state.BattlefieldResolutions.Count
            + state.BattleResolutions.Count;
        var livePaymentCount = (state.PendingPayment is null ? 0 : 1) + pendingPaymentTaskCount;
        var liveWindowCount = string.IsNullOrWhiteSpace(state.TurnWindow.State) ? 0 : 1;

        return
        [
            BuildRuleQueueCoverageSnapshotRow(
                "stack",
                state.StackItems.Count,
                RuleQueueCoverageEvidenceKeys(("stack", state.StackItems.Count))),
            BuildRuleQueueCoverageSnapshotRow(
                "trigger",
                state.TriggerQueue.Count,
                RuleQueueCoverageEvidenceKeys(("triggerQueue", state.TriggerQueue.Count))),
            BuildRuleQueueCoverageSnapshotRow(
                "battle",
                liveBattleCount,
                RuleQueueCoverageEvidenceKeys(
                    ("battle", state.BattleState.IsActive ? 1 : 0),
                    ("battlefieldTasks", state.BattlefieldTasks.Count),
                    ("pendingTaskQueue", pendingBattleTaskCount),
                    ("battlefieldResolutions", state.BattlefieldResolutions.Count),
                    ("battleResolutions", state.BattleResolutions.Count))),
            BuildRuleQueueCoverageSnapshotRow(
                "window",
                liveWindowCount,
                RuleQueueCoverageEvidenceKeys(("turnWindow", liveWindowCount))),
            BuildRuleQueueCoverageSnapshotRow(
                "payment",
                livePaymentCount,
                RuleQueueCoverageEvidenceKeys(
                    ("pendingPayment", state.PendingPayment is null ? 0 : 1),
                    ("pendingTaskQueue", pendingPaymentTaskCount)))
        ];
    }

    private static IReadOnlyList<string> PendingPaymentResourceActionIds(
        MatchState state,
        PendingPaymentState payment)
    {
        return payment.PaymentResourceActionIds
                .Concat(payment.LegalPaymentChoiceIds.Where(choiceId =>
                    choiceId.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal)))

                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }

    private static Dictionary<string, object?>? BuildPendingHandChoiceSnapshotView(
        MatchState state,
        PendingHandChoiceState? choice,
        string viewerPlayerId)
    {
        if (choice is null)
        {
            return null;
        }

        var ownChoice = string.Equals(choice.PlayerId, viewerPlayerId, StringComparison.Ordinal);
        var view = new Dictionary<string, object?>
        {
            ["choiceId"] = choice.ChoiceId,
            ["choiceWindow"] = choice.ChoiceWindow,
            ["playerId"] = choice.PlayerId,
            ["requiredCount"] = choice.RequiredCount,
            ["maxCount"] = choice.MaxCount,
            ["reason"] = choice.Reason,
            ["sourceObjectId"] = choice.MovementObserver is null && choice.LastBreathDraw is null ? choice.SourceObjectId
                : VisibleObjectIdForViewer(state, choice.SourceObjectId, viewerPlayerId),
            ["effectKind"] = choice.EffectKind,
            ["choiceState"] = ownChoice ? "PENDING_CHOICE" : "WAITING_FOR_CHOICE"
        };
        if (ownChoice)
        {
            view["legalObjectIds"] = choice.LegalObjectIds;
        }

        return view;
    }

    private static Dictionary<string, object?>? BuildPendingCardChoiceSnapshotView(
        MatchState state,
        string viewerPlayerId)
    {
        var choice = state.PendingCardChoice;
        if (choice is null)
        {
            return null;
        }

        var ownChoice = string.Equals(choice.PlayerId, viewerPlayerId, StringComparison.Ordinal);
        var view = new Dictionary<string, object?>
        {
            ["choiceId"] = choice.ChoiceId,
            ["choiceWindow"] = choice.ChoiceWindow,
            ["playerId"] = choice.PlayerId,
            ["requiredCount"] = choice.RequiredCount,
            ["maxCount"] = choice.MaxCount,
            ["reason"] = choice.Reason,
            ["sourceObjectId"] = VisibleObjectIdForViewer(state, choice.SourceObjectId, viewerPlayerId),
            ["effectKind"] = choice.EffectKind,
            ["choiceState"] = ownChoice ? "PENDING_CHOICE" : "WAITING_FOR_CHOICE",
            ["legalCount"] = choice.LegalObjectIds.Count,
            ["contextCount"] = choice.ContextObjectIds.Count
        };
        if (ownChoice)
        {
            view["legalObjectIds"] = choice.LegalObjectIds;
        }

        if (!ownChoice && choice.DeckContext is not null)
        {
            view.Remove("legalCount");
            view.Remove("requiredCount");
            view.Remove("maxCount");
        }
        return view;
    }
}

internal static partial class ActionPromptBuilder
{
    private const string RecycleRunePaymentOptionalCostPrefix = "RECYCLE_RUNE:";
    private const string DestroyFriendlyUnitAdditionalCostPrefix = "DESTROY_FRIENDLY_UNIT:";
    private const string RecycleGraveyardCardAdditionalCostPrefix = "RECYCLE_GRAVEYARD_CARD:";
    private const int BasicRuneRecyclePowerGain = 1;
    private const string StandbyHideDestination = "STANDBY";
    private const string StandbyHideOptionalCost = "STANDBY_A";
    private const string StandbyHideFreeOptionalCost = "STANDBY_FREE";
    private const string StandbyHideTeemoOptionalCost = "STANDBY_TEEMO_MANA";
    private const int StandbyHideManaCost = 1;
    private const string FreeStandbyHideEffectPrefix = "FREE_STANDBY_HIDE:";
    private const string StandbyRevealMode = "STANDBY_REVEAL";
    private const string StandbyRevealModeLabel = "翻开待命";
    private const string StandbyRevealDestination = "BASE";
    private const string StandbyRevealOptionalCost = "STANDBY_REVEAL_0";
    private const string StandbyReactionMode = "STANDBY_REACTION";
    private const string StandbyReactionModeLabel = "作为反应打出";
    private const string StandbyReactionDestination = "STACK";
    private const int BaseWinningScore = 8;

    private const string MoveUnitBattlefieldZone = "BATTLEFIELD";
    private const string MoveUnitBaseZone = "BASE";
    private const string MoveUnitRoamOptionalCost = "ROAM";
    private const string MoveUnitRoamKeyword = "游走";
    private const string BaronNestMoveStaticPermission = "BARON_NEST_MOVE_STATIC";

    private sealed record MoveUnitPromptRequirement(
        string SourceObjectId,
        string Origin,
        string OriginLabel,
        string Mode,
        string ModeLabel,
        IReadOnlyList<ActionPromptChoiceDto> DestinationChoices,
        IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices,
        IReadOnlyList<string> RequiredOptionalCosts,
        bool Composable,
        string? UnsupportedReason);


    private sealed record HideCardPromptRequirement(
        string SourceObjectId,
        string CardNo,
        string DisplayName,
        IReadOnlyList<ActionPromptChoiceDto> DestinationChoices,
        IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices,
        int ManaCost,
        bool Composable,
        string? UnsupportedReason);

    private sealed record RevealCardPromptRequirement(
        string SourceObjectId,
        string CardNo,
        string DisplayName,
        string Mode,
        string ModeLabel,
        IReadOnlyList<ActionPromptChoiceDto> DestinationChoices,
        IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices,
        IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> TargetChoicesByIndex,
        int MinTargetCount,
        int MaxTargetCount,
        string TargetScope,
        IReadOnlyList<string> RequiredOptionalCosts,
        bool Composable,
        string? UnsupportedReason);

    private sealed record AssembleEquipmentPromptRequirement(
        string SourceObjectId,
        string EquipmentCardNo,
        string DisplayName,
        IReadOnlyList<ActionPromptChoiceDto> TargetChoices,
        IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices,
        IReadOnlyList<ActionPromptChoiceDto> AdditionalCostChoices,
        int RequiredAdditionalCostChoiceCount,
        IReadOnlyList<ActionPromptChoiceDto> PaymentResourceChoices,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> PaymentResourcePowerByChoice,
        IReadOnlyDictionary<string, int> AvailablePowerByTrait,
        IReadOnlyDictionary<string, int> AvailablePowerByTraitWithPaymentResources,
        IReadOnlyList<string> RequiredOptionalCosts,
        int ManaCost,
        int BaseManaCost,
        IReadOnlyDictionary<string, int> ManaCostByTargetObjectId,
        IReadOnlyDictionary<string, int> TargetPowerManaReductionByTargetObjectId,
        int PowerCost,
        int ExperienceCost,
        bool Composable,
        string? UnsupportedReason)
    { internal RunePaymentReachability? PowerReachability { get; init; } }

    private sealed record ActivateAbilityPromptRequirement(
        string SourceObjectId,
        string CardNo,
        string AbilityId,
        string DisplayName,
        string AbilityLabel,
        int ManaCost,
        int PowerCost,
        int ExperienceCost,
        IReadOnlyDictionary<string, int> PowerCostByTrait,
        int MinTargetCount,
        int MaxTargetCount,
        string TargetScopeLabel,
        IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> TargetChoicesByIndex,
        IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices,
        IReadOnlyList<ActionPromptChoiceDto> PaymentResourceChoices,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> PaymentResourcePowerByChoice,
        int AvailablePower,
        IReadOnlyDictionary<string, int> AvailablePowerByTrait,
        int AvailablePowerWithPaymentResources,
        IReadOnlyDictionary<string, int> AvailablePowerByTraitWithPaymentResources,
        IReadOnlyList<string> RequiredOptionalCosts,
        bool ExhaustsSource,
        bool ResolvesImmediately,
        bool Composable,
        string? UnsupportedReason,
        IReadOnlyDictionary<string, int>? SpellshieldTaxPowerByTargetObjectId = null,
        bool RenataGoldExtraManaAvailable = false,
        int BonusMana = 0,
        IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>? AzirArmamentReattachChoicesByTargetObjectId = null,
        string? AbilityInstanceId = null, string? CopyEquipmentObjectId = null,
        IReadOnlyList<ActionPromptChoiceDto>? AdditionalCostChoices = null, int RequiredAdditionalCostChoiceCount = 0);

    private sealed record LegendActionPromptRequirement(
        string SourceObjectId,
        string CardNo,
        string AbilityId,
        string DisplayName,
        string AbilityLabel,
        int ManaCost,
        int ExperienceCost,
        int MinTargetCount,
        int MaxTargetCount,
        string TargetScopeLabel,
        IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> TargetChoicesByIndex,
        IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices,
        IReadOnlyList<string> RequiredOptionalCosts,
        string TimingLabel,
        bool ExhaustsSource,
        bool ResolvesImmediately,
        bool Composable,
        string? UnsupportedReason,
        int PowerCost = 0,
        IReadOnlyDictionary<string, int>? SpellshieldTaxPowerByTargetObjectId = null);

    private sealed record DeclareBattlePromptRequirement(
        string SourceObjectId,
        string CardNo,
        string DisplayName,
        int MinAttackerCount,
        int MaxAttackerCount,
        string AttackerCountLabel,
        IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> AttackerChoicesByIndex,
        int MinDefenderCount,
        int MaxDefenderCount,
        string DefenderCountLabel,
        IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> TargetChoicesByIndex,
        int MinBattlefieldTargetCount,
        int MaxBattlefieldTargetCount,
        string BattlefieldTargetScope,
        IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> BattlefieldTargetChoicesByIndex,
        IReadOnlyList<ActionPromptChoiceDto> BattlefieldChoices,
        IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices,
        IReadOnlyList<ActionPromptChoiceDto> PaymentResourceChoices,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> PaymentResourcePowerByChoice,
        int AvailablePower,
        int AvailablePowerWithPaymentResources,
        IReadOnlyList<string> RequiredOptionalCosts,
        bool Composable,
        string? UnsupportedReason);

    private sealed record LegendActionAbilityDefinition(
        string AbilityId,
        IReadOnlyList<string> SourceCardNos,
        string DisplayName,
        int ManaCost,
        int ExperienceCost,
        string RequiredCostToken,
        int RequiredTargetCount,
        bool RequiresFriendlyUnitTarget,
        string EffectKind,
        bool RequiresBattlefieldTarget = false,
        bool RequiresExhaustedTarget = false,
        bool RequiresArmamentSecondTarget = false,
        bool RequiresUnattachedArmamentSecondTarget = false,
        bool RequiresAttachedArmamentSecondTarget = false,
        bool RequiresPlayedAnotherCardThisTurn = false,
        bool RequiresPlayedArmamentThisTurn = false,
        bool RequiresOwnedTeemoUnitTarget = false,
        string ManaCostReductionKind = "",
        int ManaGainAmount = 0,
        int PowerGainAmount = 0,
        string TimingKind = LegendActionTimingKinds.MainOpen,
        bool RequiresEzrealEnemyTargetsThisTurn = false,
        bool RequiresPendingFriendlyUnitTarget = false,
        string RequiredControlledBattlefieldStaticAbilityKind = "",
        PowerPaymentPurpose GrantedPowerPurpose = PowerPaymentPurpose.Other,
        int PowerCost = 0,
        bool RequiresAnyUnitTarget = false,
        bool RequiresEmpoweredSource = false);
    private const string BattlefieldHeldUnitCostIncreaseEffectPrefix = "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:";
    private const string BattlefieldUnitGainExperienceAbilityId = "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE";
    private const string BattlefieldGrantedLegendAttachArmamentAbilityId = "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD";
    private const string YasuoLegendAbilityId = LegendActionAbilityCatalog.YasuoLegendAbilityId;
    private const string LeeSinLegendAbilityId = LegendActionAbilityCatalog.LeeSinLegendAbilityId;
    private const string PoppyLegendAbilityId = LegendActionAbilityCatalog.PoppyLegendAbilityId;
    private const string ViktorLegendAbilityId = LegendActionAbilityCatalog.ViktorLegendAbilityId;
    private const string MissFortuneLegendAbilityId = LegendActionAbilityCatalog.MissFortuneLegendAbilityId;
    private const string KhazixLegendBoonAbilityId = LegendActionAbilityCatalog.KhazixLegendBoonAbilityId;
    private const string KhazixLegendMoveAbilityId = LegendActionAbilityCatalog.KhazixLegendMoveAbilityId;
    private const string PykeLegendAbilityId = LegendActionAbilityCatalog.PykeLegendAbilityId;
    private const string JaxLegendAttachAbilityId = LegendActionAbilityCatalog.JaxLegendAttachAbilityId;
    private const string JaxLegendReattachAbilityId = LegendActionAbilityCatalog.JaxLegendReattachAbilityId;
    private const string DariusLegendAbilityId = LegendActionAbilityCatalog.DariusLegendAbilityId;
    private const string DianaLegendAbilityId = LegendActionAbilityCatalog.DianaLegendAbilityId;
    private const string KaisaLegendAbilityId = LegendActionAbilityCatalog.KaisaLegendAbilityId;
    private const string OrnnLegendAbilityId = LegendActionAbilityCatalog.OrnnLegendAbilityId;
    private const string RenektonLegendAbilityId = LegendActionAbilityCatalog.RenektonLegendAbilityId;
    private const string EzrealLegendAbilityId = LegendActionAbilityCatalog.EzrealLegendAbilityId;
    private const string IreliaLegendAbilityId = LegendActionAbilityCatalog.IreliaLegendAbilityId;
    private const string TeemoLegendAbilityId = LegendActionAbilityCatalog.TeemoLegendAbilityId;
    private const string LilliaLegendAbilityId = LegendActionAbilityCatalog.LilliaLegendAbilityId;
    private const string PlayedArmamentThisTurnEffectPrefix = "PLAYED_ARMAMENT_THIS_TURN:";
    private const string PlayedEquipmentThisTurnEffectPrefix = "PLAYED_EQUIPMENT_THIS_TURN:";
    private const string PlayedSpellThisTurnEffectPrefix = "PLAYED_SPELL_THIS_TURN:";
    private const string EzrealEnemyTargetsThisTurnPrefix = "EZREAL_ENEMY_TARGETS_THIS_TURN:";
    private const int EzrealEnemyTargetThreshold = 2;
    private const int LilliaLegendBaseManaCost = 4;

    private static class LegendActionTimingKinds
    {
        public const string MainOpen = "MAIN_OPEN";
        public const string QuickOpen = "QUICK_OPEN";
        public const string PriorityWindow = "PRIORITY_WINDOW";
        public const string ResourceAction = "RESOURCE_ACTION";
        public const string SpellDuelFocus = "SPELL_DUEL_FOCUS";
    }

    private static class LegendActionManaCostReductionKinds
    {
        public const string FriendlyEphemeralFieldObjects = "FRIENDLY_EPHEMERAL_FIELD_OBJECTS";
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? SourcesFor(
        MatchState state,
        string playerId,
        string action)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return null;
        }

        return action switch
        {
            "MULLIGAN" => zones.Hand
                .Where(objectId => IsPromptHandCardControlledByPlayerOrLegacyOwned(state, playerId, objectId))
                .Select(objectId => ObjectChoice(state, objectId, "起手调整候选"))
                .ToArray(),
            "PLAY_CARD" => CoreRuleEngine.EffectPlaySources(state, playerId)
                .Where(objectId => IsImplementedPlayableHandSource(state, playerId, objectId))
                .Select(objectId => ObjectChoice(state, objectId, "implemented payable PLAY_CARD source"))
                .ToArray(),
            "HIDE_CARD" => HideCardSourceRequirements(state, playerId)
                .Select(requirement => requirement.SourceObjectId)
                .Distinct(StringComparer.Ordinal)
                .Select(objectId => ObjectChoice(state, objectId, "implemented standby source"))
                .ToArray(),
            "REVEAL_CARD" => RevealCardSourceRequirements(state, playerId)
                .Select(requirement => requirement.SourceObjectId)
                .Distinct(StringComparer.Ordinal)
                .Select(objectId => ObjectChoice(state, objectId, "face-down standby source"))
                .ToArray(),
            "TAP_RUNE" => zones.Base
                .Where(objectId => IsTapRuneSource(state, playerId, objectId))
                .Select(objectId => ObjectChoice(state, objectId, "ready controlled base rune"))
                .ToArray(),
            "RECYCLE_RUNE" => zones.Base
                .Where(objectId => IsRecycleRuneSource(state, playerId, objectId))
                .Select(objectId => ObjectChoice(state, objectId, "controlled trait base rune"))
                .ToArray(),
            "ACTIVATE_ABILITY" => ActivateAbilitySourceRequirements(state, playerId)
                .Select(requirement => requirement.SourceObjectId)
                .Distinct(StringComparer.Ordinal)
                .Select(objectId => ObjectChoice(state, objectId, "implemented activated ability source"))
                .ToArray(),
            "MOVE_UNIT" => MoveUnitSourceRequirements(state, playerId)
                .Select(requirement => requirement.SourceObjectId)
                .Distinct(StringComparer.Ordinal)
                .Select(objectId => ObjectChoice(state, objectId, "face-up controlled non-combat unit with implemented move route"))
                .ToArray(),
            "ASSEMBLE_EQUIPMENT" => AssembleEquipmentSourceRequirements(state, playerId)
                .Select(requirement => requirement.SourceObjectId)
                .Distinct(StringComparer.Ordinal)
                .Select(objectId => ObjectChoice(state, objectId, "implemented assemble equipment source"))
                .ToArray(),
            "DECLARE_BATTLE" => DeclareBattleSourceRequirements(state, playerId)
                .Select(requirement => requirement.SourceObjectId)
                .Distinct(StringComparer.Ordinal)
                .Select(objectId => ObjectChoice(state, objectId, "implemented declare battle attacker source"))
                .ToArray(),
            "LEGEND_ACT" => LegendActionSourceRequirements(state, playerId)
                .Select(requirement => requirement.SourceObjectId)
                .Distinct(StringComparer.Ordinal)
                .Select(objectId => ObjectChoice(state, objectId, "implemented legend action source"))
                .ToArray(),
            _ => null
        };
    }

    private static IReadOnlyList<HideCardPromptRequirement> HideCardSourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        var costChoices = HideCardOptionalCostChoicesForState(state, playerId);
        if (costChoices.Length == 0)
        {
            return [];
        }

        var destinationChoices = HideCardDestinationChoicesForState(state, playerId);
        if (destinationChoices.Length == 0) return [];
        var requirements = new List<HideCardPromptRequirement>();
        foreach (var objectId in CoreRuleEngine.StandbyHideSources(state, playerId))
        {
            if (!state.CardObjects.TryGetValue(objectId, out var cardObject)
                || string.IsNullOrWhiteSpace(cardObject.CardNo)
                || !CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo, out var behavior))
            {
                continue;
            }

            requirements.Add(new HideCardPromptRequirement(
                objectId,
                behavior.CardNo,
                behavior.DisplayName,
                destinationChoices,
                costChoices.Where(c => c.Id == StandbyHideTeemoOptionalCost || StandbyRules.HasPrintedKeyword(behavior.CardNo)).ToArray(),
                0,
                true,
                null));
        }

        return requirements;
    }

    private static IReadOnlyList<RevealCardPromptRequirement> RevealCardSourceRequirements(MatchState state, string playerId)
    {
        var result = new List<RevealCardPromptRequirement>();
        foreach (var source in CoreRuleEngine.StandbyPlaySources(state, playerId))
        foreach (var behavior in PlayCardPromptBehaviorsForSource(state, playerId, source))
        {
            var condition = PromptTargetCountConditionApplies(state, playerId, behavior);
            var count = PromptMaxTargetCount(state, playerId, behavior, condition);
            var destination = behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment
                ? "BATTLEFIELD:" + behavior.StandbyPlayFrom!.Battlefield.ObjectId : StandbyReactionDestination;
            var mode = string.IsNullOrEmpty(behavior.Mode) ? StandbyReactionMode : behavior.Mode;
            result.Add(new(source, behavior.CardNo, behavior.DisplayName, mode, "从待命打出",
                [new(destination, destination == StandbyReactionDestination ? "结算链" : "待命原战场")],
                [new(StandbyRevealOptionalCost, "免基础费用；额外费用请使用完整出牌面板")],
                Enumerable.Range(0,count).ToDictionary(i=>i.ToString(),i=>(IReadOnlyList<ActionPromptChoiceDto>)PromptTargetChoicesForIndex(state,playerId,behavior,i)),
                PromptMinTargetCount(behavior,condition), count, PromptTargetScopeForBehavior(behavior), [StandbyRevealOptionalCost],
                !CoreRuleEngine.UsesDeclaredMovementDestination(behavior), CoreRuleEngine.UsesDeclaredMovementDestination(behavior) ? "请使用完整出牌面板选择移动计划" : null));
        }
        return result;
    }

    private static bool TryResolveStandbyReactionBattlefieldContext(
        MatchState state,
        string playerId,
        string sourceObjectId,
        out string battlefieldObjectId)
    {
        if (TryResolveBattlefieldStandbyRevealDestination(state, playerId, sourceObjectId, out battlefieldObjectId))
        {
            return true;
        }

        if (!state.PlayerZones.TryGetValue(playerId, out var zones)
            || !zones.Base.Contains(sourceObjectId, StringComparer.Ordinal)
            || !CanRevealStandbyReactionToStack(state, playerId))
        {
            battlefieldObjectId = string.Empty;
            return false;
        }

        if (string.Equals(state.TimingState, TimingStates.SpellDuelClosed, StringComparison.Ordinal)
            && state.StackItems.Any(item => string.Equals(item.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal))
            && !string.IsNullOrWhiteSpace(state.SpellDuelState.BattlefieldObjectId))
        {
            battlefieldObjectId = state.SpellDuelState.BattlefieldObjectId;
            return true;
        }

        if (HasOpenBattleResponsePriority(state)
            && !string.IsNullOrWhiteSpace(state.BattleState.BattlefieldObjectId))
        {
            battlefieldObjectId = state.BattleState.BattlefieldObjectId;
            return true;
        }

        battlefieldObjectId = string.Empty;
        return false;
    }

    private static ActionPromptChoiceDto[] HideCardOptionalCostChoicesForState(MatchState state, string playerId)
    {
        var pool = state.RunePools.GetValueOrDefault(playerId) ?? RunePool.Empty;
        var choices = new List<ActionPromptChoiceDto>();
        if (pool.TotalPower >= 1) choices.Add(new(StandbyHideOptionalCost, "支付 1 点任意特性符能布置待命"));
        if (PaymentCostRules.AvailableMana(pool, ManaPaymentPurpose.Other, state) >= 1 && HasTeemoStandbyHidePermission(state, playerId)) choices.Add(new(StandbyHideTeemoOptionalCost, "支付 1 法力：提莫替换待命布置费用"));
        if (HasFreeStandbyHidePermission(state, playerId)) choices.Add(new(StandbyHideFreeOptionalCost, "免费布置待命", "本回合的结算效果许可"));
        return choices.ToArray();
    }

    private static ActionPromptChoiceDto[] HideCardDestinationChoicesForState(MatchState state, string playerId)
        => CoreRuleEngine.StandbyHideBattlefields(state, playerId).Select(id =>
            new ActionPromptChoiceDto("BATTLEFIELD:" + id, BattlefieldLocationLabel(state, id))).ToArray();

    private static IReadOnlyList<LegendActionPromptRequirement> LegendActionSourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.LegendZone
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var legendObject)
                && !legendObject.IsExhausted
                && SourceObjectControlledByPlayerOrLegacyOwned(legendObject, playerId))
            .SelectMany(objectId => LegendActionRequirementsForSource(state, playerId, objectId))
            .ToArray();
    }

    private static IReadOnlyList<LegendActionPromptRequirement> LegendActionRequirementsForSource(
        MatchState state,
        string playerId,
        string sourceObjectId)
    {
        if (!state.CardObjects.TryGetValue(sourceObjectId, out var sourceState)
            || string.IsNullOrWhiteSpace(sourceState.CardNo))
        {
            return [];
        }

        var requirements = new List<LegendActionPromptRequirement>();
        foreach (var ability in ImplementedLegendActionAbilities())
        {
            if (!LegendActionSourceHasAbility(state, playerId, sourceState.CardNo, ability)
                || !LegendActionTimingAllowed(state, playerId, ability)
                || !LegendActionPrerequisitesMet(state, playerId, ability)
                || ability.RequiresEmpoweredSource && !sourceState.IsEmpowered)
            {
                continue;
            }

            var targetChoicesByIndex = LegendActionTargetChoicesByIndex(state, playerId, ability);
            var requiredTargetCount = ability.RequiredTargetCount;
            if (requiredTargetCount > 0
                && Enumerable.Range(0, requiredTargetCount)
                    .Any(targetIndex => !targetChoicesByIndex.TryGetValue(targetIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), out var choices)
                        || choices.Count == 0))
            {
                continue;
            }

            var manaCost = ResolveLegendActionManaCost(state, playerId, ability);
            var requiredCosts = LegendActionRequiredCostTokens(manaCost, ability);
            var optionalCostChoices = requiredCosts
                .Select(cost => new ActionPromptChoiceDto(cost, LegendActionCostLabel(cost)))
                .ToArray();
            var unsupportedReason = LegendActionUnsupportedCompositionReason(ability);
            var spellshieldByTarget = ability.RequiresAnyUnitTarget
                ? targetChoicesByIndex["0"].ToDictionary(choice => choice.Id,
                    choice => SpellshieldTaxPowerForTarget(state, playerId, choice.Id), StringComparer.Ordinal)
                : null;
            var requirement = new LegendActionPromptRequirement(
                sourceObjectId,
                sourceState.CardNo ?? string.Empty,
                ability.AbilityId,
                ability.DisplayName,
                LegendActionAbilityLabel(ability),
                manaCost,
                ability.ExperienceCost,
                ability.RequiredTargetCount,
                ability.RequiredTargetCount,
                LegendActionTargetScopeLabel(ability),
                targetChoicesByIndex,
                optionalCostChoices,
                requiredCosts,
                LegendActionTimingLabel(ability.TimingKind),
                true,
                !CoreRuleEngine.IsLegendAttachmentEffect(ability.EffectKind) && !CoreRuleEngine.IsLegendDuelMovementEffect(ability.EffectKind)
                    && !CoreRuleEngine.IsLegendUnitKeywordEffect(ability.EffectKind) && !CoreRuleEngine.IsLegendUnitEffect(ability.EffectKind),
                unsupportedReason is null,
                unsupportedReason,
                ability.PowerCost,
                spellshieldByTarget);

            if (CanPayLegendActionRequirement(state, playerId, requirement))
            {
                requirements.Add(requirement);
            }
        }

        return requirements;
    }

    private static IReadOnlyList<LegendActionAbilityDefinition> ImplementedLegendActionAbilities()
    {
        return [
            new(LegendActionAbilityCatalog.MelLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.MelLegendAbilityId),
                "流光镜影传奇减战力技能", 0, 0, string.Empty, 1, false, "LEGEND_UNIT_POWER_MODIFIER",
                RequiresBattlefieldTarget: true, RequiresAnyUnitTarget: true, RequiresEmpoweredSource: true),
            new(LegendActionAbilityCatalog.AmbessaLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.AmbessaLegendAbilityId),
                "铁血狼母传奇活跃单位技能", 0, 0, string.Empty, 1, false, "LEGEND_READY_UNIT",
                PowerCost: 1, RequiresAnyUnitTarget: true, RequiresEmpoweredSource: true),
            new(LegendActionAbilityCatalog.AkaliLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.AkaliLegendAbilityId),
                "离群之刺传奇迅捷移动技能", 0, 0, string.Empty, 1, true, "LEGEND_MOVE_DUEL_UNIT",
                RequiresBattlefieldTarget: true, TimingKind: LegendActionTimingKinds.QuickOpen),
            new(LegendActionAbilityCatalog.ShenLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.ShenLegendAbilityId),
                "暮光之眼传奇壁垒技能", 0, 0, string.Empty, 1, true, "LEGEND_GRANT_UNIT_KEYWORD",
                TimingKind: LegendActionTimingKinds.QuickOpen),
            new(
                YasuoLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(YasuoLegendAbilityId),
                "亚索传奇移动技能",
                2,
                0,
                "SPEND_MANA:2",
                1,
                true,
                "MOVE_FRIENDLY_UNIT"),
            new(
                LeeSinLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(LeeSinLegendAbilityId),
                "李青传奇增益技能",
                1,
                0,
                "SPEND_MANA:1",
                1,
                true,
                "GRANT_BOON"),
            new(
                PoppyLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(PoppyLegendAbilityId),
                "波比传奇抽牌技能",
                0,
                3,
                "SPEND_EXPERIENCE:3",
                0,
                false,
                "DRAW_ONE"),
            new(
                ViktorLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(ViktorLegendAbilityId),
                "维克托传奇随从技能",
                1,
                0,
                "SPEND_MANA:1",
                0,
                false,
                "CREATE_MINION"),
            new(
                MissFortuneLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(MissFortuneLegendAbilityId),
                "赏金猎人传奇游走技能",
                0,
                0,
                string.Empty,
                1,
                true,
                "GRANT_ROAM"),
            new(
                KhazixLegendBoonAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(KhazixLegendBoonAbilityId),
                "卡兹克传奇增益技能",
                0,
                1,
                "SPEND_EXPERIENCE:1",
                1,
                true,
                "GRANT_BOON"),
            new(
                KhazixLegendMoveAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(KhazixLegendMoveAbilityId),
                "卡兹克传奇休眠单位移动技能",
                0,
                2,
                "SPEND_EXPERIENCE:2",
                1,
                true,
                "MOVE_FRIENDLY_UNIT",
                RequiresBattlefieldTarget: true,
                RequiresExhaustedTarget: true),
            new(
                PykeLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(PykeLegendAbilityId),
                "派克传奇召回金币技能",
                1,
                0,
                "SPEND_MANA:1",
                1,
                true,
                "RETURN_BATTLEFIELD_UNIT_AND_CREATE_COIN",
                RequiresBattlefieldTarget: true),
            new(
                JaxLegendAttachAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(JaxLegendAttachAbilityId),
                "武器大师传奇贴附技能",
                1,
                0,
                "SPEND_MANA:1",
                2,
                true,
                "ATTACH_ARMAMENT",
                RequiresArmamentSecondTarget: true,
                RequiresUnattachedArmamentSecondTarget: true),
            new(
                JaxLegendReattachAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(JaxLegendReattachAbilityId),
                "武器大师传奇重贴附技能",
                0,
                0,
                string.Empty,
                2,
                true,
                "REATTACH_ARMAMENT",
                RequiresArmamentSecondTarget: true,
                RequiresAttachedArmamentSecondTarget: true),
            new(
                BattlefieldGrantedLegendAttachArmamentAbilityId,
                [],
                "魄罗熔炉传奇贴附技能",
                0,
                0,
                string.Empty,
                2,
                true,
                "ATTACH_ARMAMENT",
                RequiresArmamentSecondTarget: true,
                RequiredControlledBattlefieldStaticAbilityKind: StaticAbilityKinds.BattlefieldGrantLegendAttachArmament),
            new(
                DariusLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(DariusLegendAbilityId),
                "诺克萨斯之手传奇鼓舞技能",
                0,
                0,
                string.Empty,
                0,
                false,
                "GAIN_MANA",
                RequiresPlayedAnotherCardThisTurn: true,
                ManaGainAmount: 1,
                TimingKind: LegendActionTimingKinds.ResourceAction),
            new(
                DianaLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(DianaLegendAbilityId),
                "皎月女神传奇法术对决法力技能",
                0,
                0,
                string.Empty,
                0,
                false,
                "GAIN_DUEL_MANA",
                ManaGainAmount: 1,
                TimingKind: LegendActionTimingKinds.ResourceAction),
            new(
                KaisaLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(KaisaLegendAbilityId),
                "虚空之女传奇法术反应符能技能",
                0,
                0,
                string.Empty,
                0,
                false,
                "GAIN_POWER",
                PowerGainAmount: 1,
                TimingKind: LegendActionTimingKinds.ResourceAction,
                GrantedPowerPurpose: PowerPaymentPurpose.PlaySpell),
            new(
                OrnnLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(OrnnLegendAbilityId),
                "山隐之焰传奇装备反应符能技能",
                0,
                0,
                string.Empty,
                0,
                false,
                "GAIN_POWER",
                PowerGainAmount: 1,
                TimingKind: LegendActionTimingKinds.ResourceAction,
                GrantedPowerPurpose: PowerPaymentPurpose.Equipment),
            new(
                RenektonLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(RenektonLegendAbilityId),
                "荒漠屠夫传奇单位法力技能",
                0, 0, string.Empty, 0, false, "GAIN_UNIT_ONLY_MANA",
                ManaGainAmount: 2, TimingKind: LegendActionTimingKinds.ResourceAction, PowerCost: 2),
            new(
                EzrealLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(EzrealLegendAbilityId),
                "探险家传奇反应抽牌技能",
                0,
                0,
                string.Empty,
                0,
                false,
                "DRAW_ONE",
                TimingKind: LegendActionTimingKinds.PriorityWindow,
                RequiresEzrealEnemyTargetsThisTurn: true),
            new(
                IreliaLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(IreliaLegendAbilityId),
                "刀锋舞者传奇反应重置技能",
                1,
                0,
                "SPEND_MANA:1",
                1,
                true,
                "READY_FRIENDLY_UNIT",
                TimingKind: LegendActionTimingKinds.PriorityWindow,
                RequiresPendingFriendlyUnitTarget: true),
            new(
                TeemoLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(TeemoLegendAbilityId),
                "迅捷斥候传奇召回技能",
                1,
                0,
                "SPEND_MANA:1",
                1,
                false,
                "RETURN_OWNED_TEEMO_UNIT_TO_HAND",
                RequiresOwnedTeemoUnitTarget: true),
            new(
                LegendActionAbilityCatalog.AzirLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.AzirLegendAbilityId),
                "沙漠皇帝传奇沙兵技能",
                1,
                0,
                "SPEND_MANA:1",
                0,
                false,
                "CREATE_SAND_SOLDIER",
                RequiresPlayedArmamentThisTurn: true),
            new(
                LilliaLegendAbilityId,
                LegendActionAbilityCatalog.SourceCardNosForAbility(LilliaLegendAbilityId),
                "莉莉娅传奇精灵技能",
                LilliaLegendBaseManaCost,
                0,
                string.Empty,
                0,
                false,
                "CREATE_FAERIE",
                ManaCostReductionKind: LegendActionManaCostReductionKinds.FriendlyEphemeralFieldObjects)
        ];
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> LegendActionTargetChoicesByIndex(
        MatchState state,
        string playerId,
        LegendActionAbilityDefinition ability)
    {
        if (ability.RequiredTargetCount == 0)
        {
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
        if (ability.RequiresAnyUnitTarget)
        {
            result["0"] = state.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields))
                .Distinct(StringComparer.Ordinal)
                .Where(id => CoreRuleEngine.IsLegendUnitEffectTarget(state, playerId, id, ability.RequiresBattlefieldTarget))
                .Select(id => ObjectChoice(state, id, LegendActionTargetScopeLabel(ability))).ToArray();
            return result;
        }
        if (ability.RequiresArmamentSecondTarget)
        {
            var unitChoices = LegendActionFriendlyUnitTargetChoices(state, playerId, ability).ToArray();
            result["0"] = unitChoices;
            result["1"] = LegendActionArmamentTargetChoices(state, playerId, ability, unitChoices.FirstOrDefault()?.Id).ToArray();
            return result;
        }

        if (ability.RequiresOwnedTeemoUnitTarget)
        {
            result["0"] = LegendActionOwnedTeemoTargetChoices(state, playerId).ToArray();
            return result;
        }

        if (ability.RequiresPendingFriendlyUnitTarget)
        {
            result["0"] = LegendActionPendingFriendlyUnitTargetChoices(state, playerId).ToArray();
            return result;
        }

        if (ability.RequiresFriendlyUnitTarget)
        {
            result["0"] = LegendActionFriendlyUnitTargetChoices(state, playerId, ability).ToArray();
        }

        return result;
    }

    private static IEnumerable<ActionPromptChoiceDto> LegendActionFriendlyUnitTargetChoices(
        MatchState state,
        string playerId,
        LegendActionAbilityDefinition ability)
    {
        return ControlledLegendActionObjects(state, playerId)
            .Where(objectId => LegendActionIsValidFriendlyUnitTarget(state, playerId, objectId, ability))
            .Select(objectId => ObjectChoice(state, objectId, LegendActionTargetScopeLabel(ability)));
    }

    private static string? LegendActionUnsupportedCompositionReason(LegendActionAbilityDefinition ability)
    {
        return ability.RequiresArmamentSecondTarget
            ? "该传奇行动需要第二目标依赖第一目标，服务端已公开候选但前端组合器暂不提交依赖型双目标。"
            : null;
    }

    private static string LegendActionAbilityLabel(LegendActionAbilityDefinition ability)
    {
        return ability.AbilityId switch
        {
            LegendActionAbilityCatalog.MelLegendAbilityId => "解除强化并横置：战场上一名单位本回合战力 -2",
            LegendActionAbilityCatalog.AmbessaLegendAbilityId => "解除强化、支付 1 符能并横置：使一名单位活跃",
            LegendActionAbilityCatalog.AkaliLegendAbilityId => "迅捷横置：如果是你的回合，将法术对决中的友方单位移至基地；如果已强化，使其活跃",
            LegendActionAbilityCatalog.ShenLegendAbilityId => "迅捷横置：给予友方单位壁垒",
            YasuoLegendAbilityId => "支付 2 并横置：移动友方单位",
            LeeSinLegendAbilityId => "支付 1 并横置：给予友方单位增益",
            PoppyLegendAbilityId => "花费 3 经验并横置：抽 1 张",
            ViktorLegendAbilityId => "支付 1 并横置：打出 1 战力随从",
            MissFortuneLegendAbilityId => "横置：给予友方单位游走",
            KhazixLegendBoonAbilityId => "花费 1 经验并横置：给予友方单位增益",
            KhazixLegendMoveAbilityId => "花费 2 经验并横置：移动休眠友方单位回基地",
            PykeLegendAbilityId => "支付 1 并横置：召回战场友方单位并打出金币",
            JaxLegendAttachAbilityId => "支付 1 并横置：贴附未贴附武装",
            JaxLegendReattachAbilityId => "横置：重贴附已贴附武装",
            BattlefieldGrantedLegendAttachArmamentAbilityId => "魄罗熔炉横置：贴附受控武装",
            DariusLegendAbilityId => "鼓舞并横置：获得 1 法力",
            DianaLegendAbilityId => "法术对决横置：获得 1 法力",
            KaisaLegendAbilityId => "法术反应横置：获得 1 符能",
            OrnnLegendAbilityId => "装备反应横置：获得 1 符能",
            RenektonLegendAbilityId => "支付 2 符能并横置：获得仅用于打出单位或单位主动技能的 2 法力",
            EzrealLegendAbilityId => "反应横置：抽 1 张",
            IreliaLegendAbilityId => "反应支付 1 并横置：重置被选为目标的友方单位",
            TeemoLegendAbilityId => "支付 1 并横置：召回己方提莫单位",
            LegendActionAbilityCatalog.AzirLegendAbilityId => "支付 1 并横置：打出黄沙士兵",
            LilliaLegendAbilityId => "动态支付并横置：打出精灵",
            _ => ability.DisplayName
        };
    }

    private static string LegendActionTargetScopeLabel(LegendActionAbilityDefinition ability)
    {
        if (ability.RequiredTargetCount == 0)
        {
            return "无需目标";
        }

        if (ability.RequiresAnyUnitTarget) return ability.RequiresBattlefieldTarget ? "战场上的单位" : "场上的单位";

        if (ability.RequiresOwnedTeemoUnitTarget)
        {
            return "己方提莫单位";
        }

        if (ability.RequiresPendingFriendlyUnitTarget)
        {
            return "被当前结算链项目指定的友方单位";
        }

        if (ability.RequiresArmamentSecondTarget)
        {
            return "友方单位 + 受控武装";
        }

        if (ability.RequiresBattlefieldTarget)
        {
            return "友方战场单位";
        }

        return ability.RequiresFriendlyUnitTarget ? "友方单位" : "服务端目标";
    }

    private static string LegendActionTimingLabel(string timingKind)
    {
        return timingKind switch
        {
            LegendActionTimingKinds.QuickOpen => "主阶段或法术对决开环",
            LegendActionTimingKinds.MainOpen => "主阶段开环",
            LegendActionTimingKinds.PriorityWindow => "优先权窗口",
            LegendActionTimingKinds.ResourceAction => "反应资源行动窗口",
            LegendActionTimingKinds.SpellDuelFocus => "法术对决焦点",
            _ => timingKind
        };
    }

    private static int PromptMinimumManaCost(
        MatchState state, string playerId, CardBehaviorDefinition behavior,
        string? sourceObjectId = null, int additionalMana = 0)
        => CoreRuleEngine.MinimumPlayManaCost(state, playerId, behavior, sourceObjectId, additionalMana).Total;

    private static IReadOnlyList<CoreRuleEngine.OptionalSacrificePlayCost> AffordableOptionalSacrificePlayCosts(
        MatchState state, string playerId, CardBehaviorDefinition behavior, string? source,
        int additionalMana = 0, int additionalPower = 0, string additionalTrait = "")
    {
        var pool = PaymentCostRules.PowerPoolForPurpose(state.RunePools.GetValueOrDefault(playerId) ?? RunePool.Empty,
            CoreRuleEngine.PowerPurposeForPlay(behavior));
        var resources = PlayCardPaymentResourcePowerByTraitForBehavior(state, playerId, behavior, source);
        var reachability = new RunePaymentReachability(pool, resources);
        return CoreRuleEngine.OptionalSacrificePlayCosts(state, playerId, behavior, source,
                additionalMana, additionalPower, additionalTrait)
            .Where(cost => PaymentCostRules.AvailableMana(pool, cost.ManaPurpose, state: state) >= cost.Mana.Total
                && reachability.CanPay(cost.GenericPower, cost.TypedPower)).ToArray();
    }

    private static bool PromptCardObjectHasTags(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId,
        string requiredTags)
    {
        return cardObjects.TryGetValue(objectId, out var cardObject)
            && PromptDelimitedValues(requiredTags)
                .All(tag => cardObject.Tags.Contains(tag, StringComparer.Ordinal));
    }

    private static bool PlayCardHasServerTargetSelectionConstraint(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<IReadOnlyList<string>> choicesByIndex)
    {
        if (behavior.AnyNumberOfTargets) return false;
        return behavior.SelectedBattlefieldEffect is not null || behavior.TargetReferencedMovement is not null || behavior.OptionalEquipmentAfterMovement
            || behavior.MaxTotalTargetPower > 0
            || !string.IsNullOrWhiteSpace(behavior.AnyTargetRequiredTag)
            || behavior.SwapsTargetLocations
            || choicesByIndex
                .SelectMany(choiceIds => choiceIds)
                .Distinct(StringComparer.Ordinal)
                .Any(objectId => !behavior.IgnoresSpellshield && SpellshieldTaxPowerForTarget(state, playerId, objectId) > 0);
    }

    private static bool IsLegalPlayCardTargetSelection(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        var targetCountConditionApplies = PromptTargetCountConditionApplies(state, playerId, behavior);
        var minTargetCount = PromptMinTargetCount(behavior, targetCountConditionApplies);
        var maxTargetCount = PromptMaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        if (targetObjectIds.Count < minTargetCount
            || targetObjectIds.Count > maxTargetCount)
        {
            return false;
        }

        if (!behavior.AllowsRepeatedTargets
            && targetObjectIds.Distinct(StringComparer.Ordinal).Count() != targetObjectIds.Count)
        {
            return false;
        }

        for (var targetIndex = 0; targetIndex < targetObjectIds.Count; targetIndex++)
        {
            var targetScope = PromptTargetScopeForBehavior(behavior);
                if (!IsPromptTargetObjectInScope(
                    state,
                    playerId,
                    targetObjectIds[targetIndex],
                    targetScope,
                    targetIndex,
                    behavior.CanCounterAbilities)
                || !IsPromptPlayCardTargetAllowed(state, playerId, behavior, targetObjectIds[targetIndex], targetIndex))
            {
                return false;
            }
            if (!CoreRuleEngine.IsSelectedBattlefieldTargetRelationAllowed(state, behavior, targetObjectIds, targetIndex)
                || !CoreRuleEngine.IsTargetReferencedMovementRelationAllowed(state, playerId, behavior, targetObjectIds, targetIndex)
                || !CoreRuleEngine.IsMovementEquipmentRelationAllowed(state, playerId, behavior, targetObjectIds, targetIndex))
                return false;
        }

        if (!CoreRuleEngine.HasValidTargetGroup(state, behavior, targetObjectIds))
        {
            return false;
        }

        if (!PromptHasRequiredAnyTargetTag(state, behavior, targetObjectIds)
            || !PromptHasValidSwapTargetLocations(state, behavior, targetObjectIds))
        {
            return false;
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var manaRequired = PromptMinimumManaCost(state, playerId, behavior);
        var ward = behavior.IgnoresSpellshield ? 0 : targetObjectIds.Sum(target => SpellshieldTaxPowerForTarget(state, playerId, target));
        return AvailablePlayMana(state, runePool, behavior) + PromptLuxSpellOnlyGeneratedMana(state, playerId, behavior, null, manaRequired) >= manaRequired
            && CanPayPlayPowerCosts(state, playerId, runePool, PlayCardPaymentResourcePowerByTraitForBehavior(state, playerId, behavior), behavior, ward, "");
    }

    private static bool PromptHasRequiredAnyTargetTag(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        return string.IsNullOrWhiteSpace(behavior.AnyTargetRequiredTag)
            || targetObjectIds.Any(targetObjectId =>
                PromptCardObjectHasTags(state.CardObjects, targetObjectId, behavior.AnyTargetRequiredTag));
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? ActivateAbilityOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        var choices = ActivateAbilitySourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.OptionalCostChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? HideCardOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        var choices = HideCardSourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.OptionalCostChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? RevealCardOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        var choices = RevealCardSourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.OptionalCostChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PrintedPowerChoices(CardBehaviorDefinition behavior)
        => PrintedPowerCostRules.ForPlay(behavior) is not { Amount: > 0, Traits.Count: >= 2 } ? [] :
            PrintedPowerCostRules.AllocationsForPlay(behavior).Select(allocation => new ActionPromptChoiceDto(
                PrintedPowerCostRules.ChoiceId(allocation),
                "卡面费用：" + string.Join(" + ", allocation.Select(x => $"{x.Value} {RuneTraitLabel(x.Key)}符能")),
                "选择支付卡面符能的特性，彩虹符能可补足不足部分")).ToArray();

    private static IReadOnlyList<ActionPromptChoiceDto> PlayCardOptionalCostChoicesForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId = null,
        ICollection<CoreRuleEngine.OptionalCostReductionGroup>? reductionGroups = null)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var paymentResourceChoices = PlayCardPaymentResourceChoicesForBehavior(state, playerId, behavior, sourceObjectId);
        var paymentResourcePowerByTrait = PlayCardPaymentResourcePowerByTraitForBehavior(state, playerId, behavior, sourceObjectId);
        var effectivePowerWithRecycle = new RunePaymentReachability(runePool, paymentResourcePowerByTrait).MaximumTotal;
        var experience = state.PlayerExperience.TryGetValue(playerId, out var currentExperience)
            ? currentExperience
            : 0;
        var choices = new List<ActionPromptChoiceDto>();
        choices.AddRange(CoreRuleEngine.SpellPowerReductionChoices(state, playerId, behavior));

        var reductionChoiceIds = new HashSet<string>(StringComparer.Ordinal);
        void AddReductionChoices(IReadOnlyList<ActionPromptChoiceDto> reductions)
        {
            foreach (var reduction in reductions)
                if (reductionChoiceIds.Add(reduction.Id)) choices.Add(reduction);
        }
        var echoReachability = new RunePaymentReachability(runePool, paymentResourcePowerByTrait);
        if (!CoreRuleEngine.EffectPlayIgnoresAllCosts(state, playerId))
            choices.AddRange(CoreRuleEngine.MandatoryPlayAdditionalCostCandidates(state, playerId, behavior).Select(cost => {
                var parts = cost.Split(':', 2);
                var choice = ObjectChoice(state, parts[1], "必付额外费用；不是法术目标");
                var label = parts[0] switch {
                    "DESTROY_FRIENDLY_POWERFUL_UNIT" => "摧毁友方强力单位", "DESTROY_FRIENDLY_TRAIT_UNIT" => "摧毁符合属性的友方单位",
                    "RETURN_FRIENDLY_EQUIPMENT" => "返回友方装备", _ => "摧毁友方单位" };
                return new ActionPromptChoiceDto(cost, $"{label}：{choice.Label}", choice.Reason);
            }));

        if (behavior.MaySacrificeUnitForPrintedCostReduction)
            choices.AddRange(AffordableOptionalSacrificePlayCosts(state, playerId, behavior, sourceObjectId)
                .Where(cost => cost.Choice is not null).Select(cost => {
                    var target = ObjectChoice(state, cost.Choice!.Split(':', 2)[1], "可选额外费用；不是技能目标");
                    return new ActionPromptChoiceDto(cost.Choice!, $"摧毁友方单位：{target.Label}",
                        $"支付后 {cost.Mana.Total} 法力 / {cost.GenericPower + cost.TypedPower.Values.Sum()} 符能");
                }));

        if (HasSourceStealEnemyEquipmentOptionalCost(behavior))
        {
            var originalAffordable = CanPromptSourceStealEnemyEquipmentOptionalCost(state, playerId, behavior,
                runePool, paymentResourcePowerByTrait, sourceObjectId);
            foreach (var steal in SourceStealEnemyEquipmentChoices(state, playerId, behavior))
            {
                var reduced = CoreRuleEngine.CanOfferOptionalCostWithReductions(state, playerId, behavior,
                    sourceObjectId, [steal.Id], runePool, out var reductionChoices, reductionGroups, echoReachability);
                if (!originalAffordable && !reduced) continue;
                choices.Add(steal);
                AddReductionChoices(reductionChoices);
            }
        }

        var echoReduction = EchoCostRules.Reduction(state, playerId);
        foreach (var echo in EchoCostRules.SupportsRepeatResolution(behavior) ? EchoCostRules.Available(state, playerId, behavior) : [])
        {
            var mana = Math.Max(0, echo.Mana - echoReduction);
            var echoGeneric = (long)echo.GenericPower + CoreRuleEngine.SourceReplayPowerCost(state, playerId);
            var canPayEcho = echoGeneric <= int.MaxValue
                && CoreRuleEngine.CanReachPlayPowerCost(state, playerId, behavior, echoReachability,
                    (int)echoGeneric, echo.TypedPower, EchoCostRules.PrintedCosts([echo]));
            var reduced = CoreRuleEngine.CanOfferOptionalCostWithReductions(state, playerId, behavior,
                sourceObjectId, [echo.Id], runePool, out var reductionChoices, reductionGroups, echoReachability);
            if (!reduced && (AvailablePlayMana(state, runePool, behavior) < PromptMinimumManaCost(state, playerId, behavior, sourceObjectId, mana)
                || !canPayEcho)) continue;
            var parts = new List<string> { $"{mana} 法力" };
            if (echo.GenericPower > 0) parts.Add($"{echo.GenericPower} 任意符能");
            parts.AddRange(echo.TypedPower.Select(entry => $"{entry.Value} {RuneTraitLabel(entry.Key)}符能"));
            if (echo.PrintedCardNo is { } printedCard)
            {
                var printed = PrintedPowerCostRules.ForCard(printedCard);
                if (printed.Amount > 0) parts.Add($"{printed.Amount} {string.Join("/", printed.Traits.Select(RuneTraitLabel))}符能");
            }
            var origin = echo.Id.StartsWith("ECHO:SOURCE:", StringComparison.Ordinal) ? "辛德拉回响" : echo.PrintedCardNo is null ? "卡牌回响" : "授予回响";
            var reason = echo.PrintedCardNo is null ? "卡牌回响；每项可支付一次" : "授予回响；支付完整基础费用";
            if (echoReduction > 0 && echo.Mana > 0) reason += $"；已减免 {Math.Min(echoReduction, echo.Mana)} 法力";
            choices.Add(new ActionPromptChoiceDto(echo.Id, $"{origin}：{string.Join(" + ", parts)}", reason));
            AddReductionChoices(reductionChoices);
        }

        foreach (var haste in CardPermissionKeywordRules.HasteReadyCostChoices(behavior))
        {
            var hasteReadyPowerTrait = haste.Value;
            var reduced = CoreRuleEngine.CanOfferOptionalCostWithReductions(state, playerId, behavior,
                sourceObjectId, [haste.Key], runePool, out var reductionChoices, reductionGroups, echoReachability);
            if (!reduced && (behavior.MaySacrificeUnitForPrintedCostReduction
                ? AffordableOptionalSacrificePlayCosts(state, playerId, behavior, sourceObjectId,
                    behavior.HasteReadyManaCost, behavior.HasteReadyPowerCost, hasteReadyPowerTrait).Count == 0
                : AvailablePlayMana(state, runePool, behavior) < PromptMinimumManaCost(state, playerId, behavior, sourceObjectId, behavior.HasteReadyManaCost)
                || !CanPayHasteReadyPowerCost(state, playerId, runePool, paymentResourcePowerByTrait,
                    behavior with { HasteReadyPowerTrait = hasteReadyPowerTrait }))) continue;
            var powerLabel = string.IsNullOrWhiteSpace(hasteReadyPowerTrait)
                ? $"{behavior.HasteReadyPowerCost} 符能"
                : $"{behavior.HasteReadyPowerCost} {RuneTraitLabel(hasteReadyPowerTrait)}符能";
            choices.Add(new ActionPromptChoiceDto(
                haste.Key,
                $"急速活跃：额外支付 {behavior.HasteReadyManaCost} 法力 / {powerLabel}"));
            AddReductionChoices(reductionChoices);
        }

        if (CanPromptSourceReadyOptionalCost(state, playerId, behavior))
        {
            var sourceReadyTrait = RuneTrait.Normalize(behavior.SourceReadyAdditionalPowerTrait);
            var id = $"SPEND_POWER:{sourceReadyTrait}:{behavior.SourceReadyAdditionalPowerCost}";
            var reduced = CoreRuleEngine.CanOfferOptionalCostWithReductions(state, playerId, behavior,
                sourceObjectId, [id], runePool, out var reductionChoices, reductionGroups, echoReachability);
            if (CanPaySourceReadyOptionalCost(state, playerId, runePool, paymentResourcePowerByTrait, behavior) || reduced)
            {
                choices.Add(new ActionPromptChoiceDto(id,
                    $"{behavior.DisplayName}活跃：支付 {behavior.SourceReadyAdditionalPowerCost} {RuneTraitLabel(sourceReadyTrait)}符能",
                    SourceReadyOptionalCostReason(behavior.SourceReadyConditionKind)));
                AddReductionChoices(reductionChoices);
            }
        }

        if (behavior.OptionalExperienceCost > 0
            && behavior.ManaReductionIfExperiencePaid > 0
            && experience >= behavior.OptionalExperienceCost)
        {
            choices.Add(new ActionPromptChoiceDto(
                $"SPEND_EXPERIENCE:{behavior.OptionalExperienceCost}",
                $"支付 {behavior.OptionalExperienceCost} 经验：费用减少 {behavior.ManaReductionIfExperiencePaid}"));
        }

        if (behavior.SourceBoonAdditionalManaCost > 0)
        {
            var id = $"SPEND_MANA:{behavior.SourceBoonAdditionalManaCost}";
            var reduced = CoreRuleEngine.CanOfferOptionalCostWithReductions(state, playerId, behavior,
                sourceObjectId, [id], runePool, out var reductionChoices, reductionGroups, echoReachability);
            if (AvailablePlayMana(state, runePool, behavior) >= PromptMinimumManaCost(state, playerId, behavior, sourceObjectId, behavior.SourceBoonAdditionalManaCost) || reduced)
            {
                choices.Add(new ActionPromptChoiceDto(id, $"额外支付 {behavior.SourceBoonAdditionalManaCost} 法力：给予我增益",
                    $"{behavior.DisplayName}的可选额外费用"));
                AddReductionChoices(reductionChoices);
            }
        }

        if (behavior.OptionalPowerModifier is { } modifier)
        {
            var id = CoreRuleEngine.OptionalPowerModifierCostId(modifier);
            var reduced = CoreRuleEngine.CanOfferOptionalCostWithReductions(state, playerId, behavior,
                sourceObjectId, [id], runePool, out var reductionChoices, reductionGroups, echoReachability);
            if (CanPayPlayPowerCosts(state, playerId, runePool, paymentResourcePowerByTrait, behavior, modifier.PowerCost, modifier.Trait) || reduced)
            {
                choices.Add(new ActionPromptChoiceDto(id, $"额外支付 {modifier.PowerCost} {RuneTraitLabel(modifier.Trait)}符能：第一目标本回合战力 +{modifier.Amount}",
                    $"{behavior.DisplayName}的可选额外费用"));
                AddReductionChoices(reductionChoices);
            }
        }

        if (behavior.SourceDrawAdditionalPowerCost > 0 && behavior.SourceDrawCountIfOptionalPowerCostPaid > 0)
        {
            var sourceDrawPowerTrait = RuneTrait.Normalize(behavior.SourceDrawAdditionalPowerTrait);
            var id = $"SPEND_POWER:{sourceDrawPowerTrait}:{behavior.SourceDrawAdditionalPowerCost}";
            var reduced = CoreRuleEngine.CanOfferOptionalCostWithReductions(state, playerId, behavior,
                sourceObjectId, [id], runePool, out var reductionChoices, reductionGroups, echoReachability);
            if (CanPaySourceDrawOptionalPowerCost(state, playerId, runePool, paymentResourcePowerByTrait, behavior) || reduced)
            {
                choices.Add(new ActionPromptChoiceDto(id,
                    $"额外支付 {behavior.SourceDrawAdditionalPowerCost} {RuneTraitLabel(sourceDrawPowerTrait)}符能：抽 {behavior.SourceDrawCountIfOptionalPowerCostPaid} 张牌",
                    $"{behavior.DisplayName}的可选额外费用"));
                AddReductionChoices(reductionChoices);
            }
        }

        if (behavior.SourceReadyPowerModifierAdditionalPowerCost > 0 && behavior.SourceReadyPowerModifierAmount != 0)
        {
            var sourceReadyPowerTrait = RuneTrait.Normalize(behavior.SourceReadyPowerModifierAdditionalPowerTrait);
            var id = $"SPEND_POWER:{sourceReadyPowerTrait}:{behavior.SourceReadyPowerModifierAdditionalPowerCost}";
            var reduced = CoreRuleEngine.CanOfferOptionalCostWithReductions(state, playerId, behavior,
                sourceObjectId, [id], runePool, out var reductionChoices, reductionGroups, echoReachability);
            if (CanPaySourceReadyPowerModifierOptionalCost(state, playerId, runePool, paymentResourcePowerByTrait, behavior) || reduced)
            {
                choices.Add(new ActionPromptChoiceDto(id,
                    $"额外支付 {behavior.SourceReadyPowerModifierAdditionalPowerCost} {RuneTraitLabel(sourceReadyPowerTrait)}符能：活跃并本回合战力 +{behavior.SourceReadyPowerModifierAmount}",
                    $"{behavior.DisplayName}的可选额外费用"));
                AddReductionChoices(reductionChoices);
            }
        }

        if (CanPromptTargetEffectAdditionalCost(state, playerId, runePool, paymentResourcePowerByTrait, behavior, sourceObjectId))
        {
            CoreRuleEngine.CanOfferOptionalCostWithReductions(state, playerId, behavior, sourceObjectId,
                TargetEffectAdditionalCostIds(behavior), runePool, out var reductionChoices, reductionGroups, echoReachability);
            AddReductionChoices(reductionChoices);
            var targetEffectPowerTrait = RuneTrait.Normalize(behavior.TargetEffectAdditionalPowerTrait);
            if (behavior.TargetEffectAdditionalManaCost > 0)
            {
                choices.Add(new ActionPromptChoiceDto(
                    $"SPEND_MANA:{behavior.TargetEffectAdditionalManaCost}",
                    TargetEffectAdditionalManaCostLabel(behavior),
                    $"{behavior.DisplayName}的可选额外费用"));
            }

            if (behavior.TargetEffectAdditionalPowerCost > 0)
            {
                choices.Add(new ActionPromptChoiceDto(
                    $"SPEND_POWER:{targetEffectPowerTrait}:{behavior.TargetEffectAdditionalPowerCost}",
                    TargetEffectAdditionalPowerCostLabel(behavior, targetEffectPowerTrait),
                    $"{behavior.DisplayName}的可选额外费用"));
            }
        }

        choices.AddRange(PlayCardDiscardHandManaReductionOptionalCostChoices(state, playerId, behavior, sourceObjectId));
        choices.AddRange(PlayCardDiscardHandDamageOptionalCostChoices(state, playerId, behavior, sourceObjectId));

        choices.AddRange(PrintedPowerChoices(behavior));
        choices.AddRange(paymentResourceChoices);
        return choices;
    }

    private static bool CanPromptSourceStealEnemyEquipmentOptionalCost(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait,
        string? sourceObjectId)
    {
        if (!HasSourceStealEnemyEquipmentOptionalCost(behavior)
            || AvailablePlayMana(state, runePool, behavior) < PromptMinimumManaCost(state, playerId, behavior, sourceObjectId)
            || !SourceStealEnemyEquipmentChoices(state, playerId, behavior).Any())
        {
            return false;
        }

        var requiredTrait = RuneTrait.Normalize(behavior.SourceStealEnemyEquipmentAdditionalPowerTrait);
        return CanPayPlayPowerCosts(state, playerId, runePool, paymentResourcePowerByTrait, behavior,
            behavior.SourceStealEnemyEquipmentAdditionalPowerCost, requiredTrait);
    }

    private static bool HasSourceStealEnemyEquipmentOptionalCost(CardBehaviorDefinition behavior)
    {
        var powerTrait = RuneTrait.Normalize(behavior.SourceStealEnemyEquipmentAdditionalPowerTrait);
        return behavior.PlaysSourceToBaseAsUnit
            && behavior.SourceStealEnemyEquipmentAdditionalPowerCost > 0
            && !string.IsNullOrWhiteSpace(powerTrait)
            && !string.IsNullOrWhiteSpace(behavior.SourceStealEnemyEquipmentOptionalCostPrefix);
    }

    private static IReadOnlyList<CoreRuleEngine.RenektonPaymentChoice> PromptRenektonPaymentChoices(
        MatchState state, string playerId)
        => CoreRuleEngine.RenektonPlayPaymentChoices(state, playerId,
            state.RunePools.GetValueOrDefault(playerId, RunePool.Empty),
            CoreRuleEngine.RenektonRecyclablePower(state, playerId));

    private static int PromptAvailableManaWithLuxSpellOnlyResource(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var requiredMana = PromptMinimumManaCost(state, playerId, behavior, sourceObjectId);
        var available = AvailablePlayMana(state, runePool, behavior)
            + PromptLuxSpellOnlyGeneratedMana(state, playerId, behavior, sourceObjectId, requiredMana);
        if (behavior.PlaysSourceToBaseAsUnit && PromptRenektonPaymentChoices(state, playerId).Any(choice =>
                CanPayPlayPowerCosts(state, playerId, choice.RemainingPool, choice.RemainingRunePower,
                    behavior, 0, "", choice.RemainingRuneCapacity)))
            available += 2;
        return available;
    }

    private static int PromptLuxSpellOnlyGeneratedMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId,
        int requiredMana)
    {
        if (!IsPromptSpellPlayBehavior(behavior))
        {
            return 0;
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var sources = PromptLuxSpellOnlyResourceSourceObjectIds(state, playerId, sourceObjectId).Count;
        var capacity = Math.Max(0, int.MaxValue - runePool.TotalStoredMana);
        return Math.Min(sources, capacity / P4ActivatedAbilityCatalog.LuxGeneratedMana) * P4ActivatedAbilityCatalog.LuxGeneratedMana;
    }

    private static IReadOnlyList<string> PromptLuxSpellOnlyResourceSourceObjectIds(
        MatchState state,
        string playerId,
        string? playedSourceObjectId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => !string.Equals(objectId, playedSourceObjectId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Where(objectId => CanPromptLuxSpellOnlyResourceSource(state, playerId, objectId))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool PromptPlayerPlayedSpellThisTurn(MatchState state, string playerId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            $"{PlayedSpellThisTurnEffectPrefix}{playerId}",
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PlayCardPaymentResourceChoicesForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId = null)
    {
        if (CoreRuleEngine.EffectPlayIgnoresAllCosts(state, playerId)
            || !PlayCardBehaviorMayNeedPaymentResource(state, playerId, behavior, sourceObjectId)) return [];
        return RenektonAwareRecycleRuneChoices(state, playerId,
                "payment resource action: recycle rune while playing card")
            .Concat(PlayCardLuxSpellOnlyResourceChoicesForBehavior(state, playerId, behavior, sourceObjectId))
            .Concat(PromptRenektonPaymentChoices(state, playerId)
                .Select(choice => new ActionPromptChoiceDto(choice.Id, choice.Label,
                    "传奇支付资源行动；其符能费用与打出牌的费用分别核对")))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, int> PlayCardPaymentResourcePowerByTraitForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId = null)
    {
        if (!PlayCardBehaviorMayNeedPaymentResource(state, playerId, behavior, sourceObjectId))
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var paymentResourceChoices = PlayCardPaymentResourceChoicesForBehavior(state, playerId, behavior, sourceObjectId);
        if (paymentResourceChoices.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var powerByTrait = paymentResourceChoices
            .Select(choice => TryParseRecycleRunePaymentActionId(choice.Id, out var objectId)
                && state.CardObjects.TryGetValue(objectId, out var runeState)
                && TryGetRuneTrait(runeState, out var runeTrait)
                    ? runeTrait
                    : string.Empty)
            .Where(trait => !string.IsNullOrWhiteSpace(trait))
            .GroupBy(trait => trait, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count() * BasicRuneRecyclePowerGain,
                StringComparer.Ordinal);

        return powerByTrait;
    }

    private static bool CanPaySourceReadyPowerModifierOptionalCost(
        MatchState state,
        string playerId,
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait,
        CardBehaviorDefinition behavior)
    {
        if (behavior.SourceReadyPowerModifierAdditionalPowerCost <= 0
            || behavior.SourceReadyPowerModifierAmount == 0)
        {
            return false;
        }

        var sourceReadyPowerTrait = RuneTrait.Normalize(behavior.SourceReadyPowerModifierAdditionalPowerTrait);
        if (string.IsNullOrWhiteSpace(sourceReadyPowerTrait))
        {
            return false;
        }

        return CanPayPlayPowerCosts(state, playerId, runePool, paymentResourcePowerByTrait, behavior,
            behavior.SourceReadyPowerModifierAdditionalPowerCost, sourceReadyPowerTrait);
    }

    private static bool CanPromptTargetEffectAdditionalCost(
        MatchState state,
        string playerId,
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait,
        CardBehaviorDefinition behavior,
        string? sourceObjectId)
    {
        return behavior.TargetEffectAdditionalPowerCost > 0
            && (AvailablePlayMana(state, runePool, behavior) >= PromptMinimumManaCost(state, playerId, behavior, sourceObjectId)
                    + behavior.TargetEffectAdditionalManaCost
                && CanPayTargetEffectAdditionalPowerCost(state, playerId, runePool, paymentResourcePowerByTrait, behavior)
                || CoreRuleEngine.CanOfferOptionalCostWithReductions(state, playerId, behavior, sourceObjectId,
                    TargetEffectAdditionalCostIds(behavior), runePool, out _,
                    powerReachability: new RunePaymentReachability(runePool, paymentResourcePowerByTrait)))
            && (CoreRuleEngine.HasIndependentPermanentTargets(behavior)
                || PromptTargetChoicesForIndex(state, playerId, behavior, 0).Count > 0);
    }

    private static IReadOnlyList<string> TargetEffectAdditionalCostIds(CardBehaviorDefinition behavior)
    {
        var costs = new List<string>();
        if (behavior.TargetEffectAdditionalManaCost > 0) costs.Add($"SPEND_MANA:{behavior.TargetEffectAdditionalManaCost}");
        if (behavior.TargetEffectAdditionalPowerCost > 0)
            costs.Add($"SPEND_POWER:{RuneTrait.Normalize(behavior.TargetEffectAdditionalPowerTrait)}:{behavior.TargetEffectAdditionalPowerCost}");
        return costs;
    }

    private static IReadOnlyDictionary<string, object?> CardChoiceMetadataFor(MatchState state, string playerId)
    {
        var choice = state.PendingCardChoice;
        if (choice is null)
        {
            return new Dictionary<string, object?>
            {
                ["serverCardChoiceState"] = "WAIT"
            };
        }

        var ownChoice = string.Equals(choice.PlayerId, playerId, StringComparison.Ordinal);
        var metadata = new Dictionary<string, object?>
        {
            ["choiceId"] = choice.ChoiceId,
            ["choiceWindow"] = choice.ChoiceWindow,
            ["choosingPlayerId"] = choice.PlayerId,
            ["requiredCount"] = choice.RequiredCount,
            ["maxCount"] = choice.MaxCount,
            ["reason"] = choice.Reason,
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, choice.SourceObjectId, playerId),
            ["effectKind"] = choice.EffectKind,
            ["legalCount"] = choice.LegalObjectIds.Count,
            ["contextCount"] = choice.ContextObjectIds.Count,
            ["serverCardChoiceState"] = ownChoice ? "PENDING" : "WAITING"
        };

        if (ownChoice)
        {
            metadata["cardChoices"] = PendingCardChoiceDtos(state, choice);
            if (choice.DeckContext is not null)
                metadata["viewedCards"] = choice.ContextObjectIds.Select(id => new ActionPromptChoiceDto(id, CoreRuleEngine.DeckChoiceLabel(state, id), state.StackItems.LastOrDefault() is { } deckItem && CoreRuleEngine.DeckPlaySequenceReveals(deckItem) ? "已公开展示" : "仅你可见；查看不等于展示")).ToArray();
            if (choice.HandContext is not null)
            {
                metadata["viewedCardsPublic"] = true;
                metadata["viewedCards"] = choice.ContextObjectIds.Select(id => new ActionPromptChoiceDto(id, CoreRuleEngine.DeckChoiceLabel(state, id), "已公开展示")).ToArray();
            }
            metadata["legalObjectIds"] = choice.LegalObjectIds;
        }

        return metadata;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PendingCardChoiceDtos(
        MatchState state,
        PendingCardChoiceState choice)
    {
        return choice.LegalObjectIds
            .Select(objectId => choice.ChoiceWindow == CoreRuleEngine.ReadiedModeWindow
                ? new ActionPromptChoiceDto(objectId, CoreRuleEngine.ReadiedModeLabel(objectId), choice.Reason)
                : choice.ChoiceWindow == CoreRuleEngine.AttachmentModeWindow
                ? new ActionPromptChoiceDto(objectId, CoreRuleEngine.AttachmentModeLabel(state, choice, objectId), choice.Reason)
                : choice.ChoiceWindow == CoreRuleEngine.DravenDeathOpponentWindow
                ? new ActionPromptChoiceDto(objectId, $"对手 {objectId} 获得 1 分", choice.Reason)
                : choice.ChoiceWindow == CoreRuleEngine.ResolutionDamageChoiceWindow
                ? new ActionPromptChoiceDto(objectId, "允许法术控制者抓牌", choice.Reason)
                : choice.HandContext is not null
                ? new ActionPromptChoiceDto(objectId, CoreRuleEngine.DeckChoiceLabel(state, objectId), choice.Reason)
                : choice.ChoiceWindow == "TRIGGER_CONFIRMATION"
                ? new ActionPromptChoiceDto(objectId, CoreRuleEngine.DeckChoiceLabel(state, objectId), choice.Reason)
                : choice.DeckContext is null ? ObjectChoice(state, objectId, "可选卡牌")
                : new ActionPromptChoiceDto(objectId, CoreRuleEngine.DeckChoiceLabel(state, objectId), choice.ChoiceWindow == "INSIGHT" ? "选择回收；不选则留在牌库顶" : choice.ChoiceWindow == "INSIGHT_ORDER" ? "按从顶到底顺序选择" : "选择加入手牌"))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, object?> PayCostMetadataFor(MatchState state, string playerId)
    {
        var payment = state.PendingPayment;
        if (payment is null
            || !string.Equals(payment.PlayerId, playerId, StringComparison.Ordinal))
        {
            return new Dictionary<string, object?>
            {
                ["paymentState"] = "WAIT"
            };
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var reachability = new RunePaymentReachability(runePool, PendingPaymentResourcePowerByTrait(state, payment));
        var view = new Dictionary<string, object?>
        {
            ["paymentId"] = payment.PaymentId,
            ["paymentWindow"] = payment.PaymentWindow,
            ["cost"] = PendingPaymentCostView(payment),
            ["paymentChoices"] = PendingPaymentChoiceDtos(state, payment),
            ["paymentResourceChoices"] = PendingPaymentResourceChoiceDtos(state, payment),
            ["paymentResourcePowerByChoice"] = PendingPaymentResourcePowerByChoice(state, payment),
            ["paymentResourceActionIds"] = PendingPaymentResourceActionIds(state, payment),
            ["serverPaymentState"] = "PENDING",
            ["resourceLedgerBeforePayment"] = new Dictionary<string, object?>
            {
                ["mana"] = runePool.Mana,
                ["power"] = runePool.Power,
                ["powerByTrait"] = runePool.PowerByTrait
            },
            ["availablePowerWithPaymentResources"] = reachability.MaximumTotal,
            ["availablePowerByTraitWithPaymentResources"] = reachability.TraitUpperBounds()
        };
        return WithPowerCapacity(view, reachability);
    }

    private static IReadOnlyDictionary<string, object?> RevealCardSourceRequirementView(
        RevealCardPromptRequirement requirement)
    {
        return new Dictionary<string, object?>
        {
            ["sourceObjectId"] = requirement.SourceObjectId,
            ["cardNo"] = requirement.CardNo,
            ["displayName"] = requirement.DisplayName,
            ["mode"] = requirement.Mode,
            ["modeLabel"] = requirement.ModeLabel,
            ["destinationChoices"] = requirement.DestinationChoices,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["targetChoicesByIndex"] = requirement.TargetChoicesByIndex,
            ["minTargetCount"] = requirement.MinTargetCount,
            ["maxTargetCount"] = requirement.MaxTargetCount,
            ["targetScope"] = requirement.TargetScope,
            ["requiredOptionalCosts"] = requirement.RequiredOptionalCosts,
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };
    }

    private static IReadOnlyDictionary<string, object?> HideCardSourceRequirementView(
        HideCardPromptRequirement requirement)
    {
        return new Dictionary<string, object?>
        {
            ["sourceObjectId"] = requirement.SourceObjectId,
            ["cardNo"] = requirement.CardNo,
            ["displayName"] = requirement.DisplayName,
            ["destinationChoices"] = requirement.DestinationChoices,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["manaCost"] = requirement.ManaCost,
            ["powerCost"] = requirement.OptionalCostChoices.Any(c => c.Id == StandbyHideOptionalCost) ? 1 : 0,
            ["costsByChoice"] = requirement.OptionalCostChoices.ToDictionary(c => c.Id, c => (object)new Dictionary<string, object?> { ["mana"] = c.Id == StandbyHideTeemoOptionalCost ? 1 : 0, ["power"] = c.Id == StandbyHideOptionalCost ? 1 : 0 }),
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };
    }

    private static IReadOnlyDictionary<string, object?> DeclareBattleMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = DeclareBattleSourceRequirements(state, playerId)
            .Select(DeclareBattleSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-declare-battle-attacker-only",
            ["targetPolicy"] = "source-specific-server-filtered-defenders",
            ["battlefieldPolicy"] = "source-specific-server-filtered-battlefields",
            ["optionalCostPolicy"] = "source-specific-required-combat-assignment-plus-server-filtered-brush-score-replacement",
            ["attackerCount"] = 1,
            ["attackerCountMin"] = 1,
            ["attackerCountMax"] = sourceRequirements.Select(requirement => (int)requirement["maxAttackerCount"]!).DefaultIfEmpty(0).Max(),
            ["defenderCountMin"] = 1,
            ["defenderCountMax"] = sourceRequirements.Select(requirement => (int)requirement["maxDefenderCount"]!).DefaultIfEmpty(0).Max(),
            ["multiAttackerPolicy"] = "all-units-at-physical-battlefield",
            ["multiDefenderPolicy"] = "all-units-at-physical-battlefield",
            ["multiParticipantBattlePolicy"] = "server-includes-all-physical-battlefield-participants",
            ["samePriorityAssignmentPolicy"] = "preserve-player-submitted-object-order-within-same-priority",
            ["candidateFiltering"] = "battlefield-zone-controlled-face-up-units-not-already-in-combat",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> DeclareBattleSourceRequirementView(
        DeclareBattlePromptRequirement requirement)
    {
        return new Dictionary<string, object?>
        {
            ["sourceObjectId"] = requirement.SourceObjectId,
            ["cardNo"] = requirement.CardNo,
            ["displayName"] = requirement.DisplayName,
            ["minAttackerCount"] = requirement.MinAttackerCount,
            ["maxAttackerCount"] = requirement.MaxAttackerCount,
            ["attackerCountLabel"] = requirement.AttackerCountLabel,
            ["attackerChoicesByIndex"] = requirement.AttackerChoicesByIndex,
            ["minDefenderCount"] = requirement.MinDefenderCount,
            ["maxDefenderCount"] = requirement.MaxDefenderCount,
            ["defenderCountLabel"] = requirement.DefenderCountLabel,
            ["targetChoicesByIndex"] = requirement.TargetChoicesByIndex,
            ["minBattlefieldTargetCount"] = requirement.MinBattlefieldTargetCount,
            ["maxBattlefieldTargetCount"] = requirement.MaxBattlefieldTargetCount,
            ["battlefieldTargetScope"] = requirement.BattlefieldTargetScope,
            ["battlefieldTargetChoicesByIndex"] = requirement.BattlefieldTargetChoicesByIndex,
            ["battlefieldChoices"] = requirement.BattlefieldChoices,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["paymentResourceChoices"] = requirement.PaymentResourceChoices,
            ["paymentResourcePowerByChoice"] = requirement.PaymentResourcePowerByChoice,
            ["availablePower"] = requirement.AvailablePower,
            ["availablePowerWithPaymentResources"] = requirement.AvailablePowerWithPaymentResources,
            ["requiredOptionalCosts"] = requirement.RequiredOptionalCosts,
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };
    }

    private static IReadOnlyDictionary<string, object?> LegendActionSourceRequirementView(
        LegendActionPromptRequirement requirement)
    {
        var view = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = requirement.SourceObjectId,
            ["cardNo"] = requirement.CardNo,
            ["abilityId"] = requirement.AbilityId,
            ["displayName"] = requirement.DisplayName,
            ["abilityLabel"] = requirement.AbilityLabel,
            ["manaCost"] = requirement.ManaCost,
            ["experienceCost"] = requirement.ExperienceCost,
            ["minTargetCount"] = requirement.MinTargetCount,
            ["maxTargetCount"] = requirement.MaxTargetCount,
            ["targetCountLabel"] = requirement.MinTargetCount == requirement.MaxTargetCount
                ? requirement.MaxTargetCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : $"{requirement.MinTargetCount}-{requirement.MaxTargetCount}",
            ["targetScopeLabel"] = requirement.TargetScopeLabel,
            ["targetChoicesByIndex"] = requirement.TargetChoicesByIndex,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["requiredOptionalCosts"] = requirement.RequiredOptionalCosts,
            ["timingLabel"] = requirement.TimingLabel,
            ["exhaustsSource"] = requirement.ExhaustsSource,
            ["resolvesImmediately"] = requirement.ResolvesImmediately,
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };

        if (requirement.PowerCost > 0 || requirement.SpellshieldTaxPowerByTargetObjectId?.Values.Any(value => value > 0) == true)
        {
            view["powerCost"] = requirement.PowerCost;
            view["powerSpendSelection"] = PlayPowerSpendSelectionRules.Metadata;
        }

        if (requirement.SpellshieldTaxPowerByTargetObjectId is not null)
            view["spellshieldTaxPowerByTargetObjectId"] = requirement.SpellshieldTaxPowerByTargetObjectId;

        return view;
    }

    private static IReadOnlyDictionary<string, object?> ActivateAbilitySourceRequirementView(
        ActivateAbilityPromptRequirement requirement)
    {
        var view = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = requirement.SourceObjectId,
            ["cardNo"] = requirement.CardNo,
            ["abilityId"] = requirement.AbilityId,
            ["displayName"] = requirement.DisplayName,
            ["abilityLabel"] = requirement.AbilityLabel,
            ["manaCost"] = requirement.ManaCost,
            ["powerCost"] = requirement.PowerCost,
            ["experienceCost"] = requirement.ExperienceCost,
            ["powerCostByTrait"] = requirement.PowerCostByTrait,
            ["minTargetCount"] = requirement.MinTargetCount,
            ["maxTargetCount"] = requirement.MaxTargetCount,
            ["targetCountLabel"] = requirement.MinTargetCount == requirement.MaxTargetCount
                ? requirement.MaxTargetCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : $"{requirement.MinTargetCount}-{requirement.MaxTargetCount}",
            ["targetScopeLabel"] = requirement.TargetScopeLabel,
            ["targetChoicesByIndex"] = requirement.TargetChoicesByIndex,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["paymentResourceChoices"] = requirement.PaymentResourceChoices,
            ["paymentResourcePowerByChoice"] = requirement.PaymentResourcePowerByChoice,
            ["availablePower"] = requirement.AvailablePower,
            ["availablePowerByTrait"] = requirement.AvailablePowerByTrait,
            ["availablePowerWithPaymentResources"] = requirement.AvailablePowerWithPaymentResources,
            ["availablePowerByTraitWithPaymentResources"] = requirement.AvailablePowerByTraitWithPaymentResources,
            ["requiredOptionalCosts"] = requirement.RequiredOptionalCosts,
            ["exhaustsSource"] = requirement.ExhaustsSource,
            ["resolvesImmediately"] = requirement.ResolvesImmediately,
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };

        if (requirement.RequiredAdditionalCostChoiceCount > 0)
        {
            view["additionalCostChoices"] = requirement.AdditionalCostChoices;
            view["requiredAdditionalCostChoiceCount"] = requirement.RequiredAdditionalCostChoiceCount;
        }
        if (requirement.PaymentResourceChoices.Any(choice => choice.Id.StartsWith("LEGEND_RESOURCE:", StringComparison.Ordinal)))
            view["paymentResourceOrderMatters"] = true;
        if (requirement.AbilityInstanceId is not null)
        {
            view["abilityInstanceId"] = requirement.AbilityInstanceId;
            view["abilityChoiceId"] = requirement.AbilityInstanceId;
            view["copyEquipmentObjectId"] = requirement.CopyEquipmentObjectId;
        }
        if (P4ActivatedAbilityCatalog.TryGetByAbilityId(requirement.AbilityId, out var powerAbility)
            && powerAbility.Kind == ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn)
        {
            view["powerModifierAmount"] = powerAbility.PowerModifierAmount;
            view["minimumPowerAfterModifier"] = powerAbility.MinimumPowerAfterModifier;
            view["appliesSpellshieldTargetTax"] = true;
            view["stackPolicy"] = "ordinary-stack-item-before-power-modifier";
            view["paymentPolicy"] = "payment-plan-spellshield-tax-exhaust-as-cost";
        }
        if (P4ActivatedAbilityCatalog.TryGetByAbilityId(requirement.AbilityId, out var resourceAbility)
            && resourceAbility.IsResourceSkill && resourceAbility.ReactionSpeed)
        {
            view["resourceSkill"] = true;
            view["reactionSpeed"] = true;
            view["generatedMana"] = resourceAbility.GeneratedMana;
            view["generatedPower"] = resourceAbility.GeneratedPower;
            view["generatedPowerByTrait"] = P4ActivatedAbilityCatalog.GeneratedPowerByTraitForAbility(resourceAbility);
            view["resourceLifecycle"] = "rune-pool";
            view["reactionPolicy"] = "resolves-immediately-without-passing-action-rights";
            view["usesSourceAsDestroyCost"] = P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(requirement.AbilityId);
            view["bonusMana"] = requirement.BonusMana;
        }

        if (string.Equals(requirement.AbilityId, P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, StringComparison.Ordinal))
        {
            view["resourceSkill"] = true;
            view["paymentOnly"] = false;
            view["generatedPower"] = P4ActivatedAbilityCatalog.MalzaharResourceGeneratedPower;
            view["usesTargetAsCost"] = true;

            view["timingPolicy"] = "open-main-and-spell-duel-focus-representative";
            view["reactionPolicy"] = "resolves-immediately-without-stack-item";
            view["resourceLifecycle"] = "rune-pool";
            view["allowedPaymentKinds"] = new[] { PaymentCostRules.RuneCostPaymentKind };
        }













        if (string.Equals(requirement.AbilityId, P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId, StringComparison.Ordinal))
        {
            view["requiresBattlefieldSource"] = true;
            view["drawCount"] = 1;
            view["powerSpendSelection"] = PlayPowerSpendSelectionRules.Metadata;
            view["timingPolicy"] = "open-main-representative";
            view["stackPolicy"] = "ordinary-stack-item-before-draw";
            view["paymentPolicy"] = "payment-plan-typed-blue";
        }

        if (string.Equals(requirement.AbilityId, P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId, StringComparison.Ordinal))
        {
            view["requiresBattlefieldSource"] = true;
            view["scoreAmount"] = 1;
            view["powerSpendSelection"] = PlayPowerSpendSelectionRules.Metadata;
            view["timingPolicy"] = "open-main-representative";
            view["stackPolicy"] = "ordinary-stack-item-before-score";
            view["paymentPolicy"] = "payment-plan-typed-blue-exhaust-as-cost";
        }

        if (string.Equals(requirement.AbilityId, P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, StringComparison.Ordinal))
        {
            view["swift"] = true;
            view["oncePerTurn"] = true;
            view["targetScope"] = "controlled-face-up-unit";
            view["swapPolicy"] = "two-moves-using-original-precise-locations";
            view["timingPolicy"] = "neutral-open-or-any-turn-duel-focus";
            view["stackPolicy"] = "ordinary-stack-item-before-swap";
            view["paymentPolicy"] = "payment-plan-typed-green";
            view["armamentReattachPolicy"] = P4ActivatedAbilityCatalog.AzirArmamentReattachPolicy;
            view["armamentReattachChoicePrefix"] = P4ActivatedAbilityCatalog.AzirArmamentReattachOptionalCostPrefix;
            view["armamentReattachChoicesByTargetObjectId"] =
                requirement.AzirArmamentReattachChoicesByTargetObjectId
                ?? new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
            view["targetChoicesByFirstTarget"] = view["armamentReattachChoicesByTargetObjectId"];
            view["additionalTargetLabel"] = "确认武装目标";
            view["armamentDecisionTiming"] = "during-resolution-after-movements";
        }

        if (string.Equals(requirement.AbilityId, P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId, StringComparison.Ordinal))
        {
            view["targetScope"] = "enemy-controlled-battlefield-with-lower-enemy-unit-power";
            view["movePolicy"] = "move-source-to-target-battlefield";
            view["timingPolicy"] = "open-main-representative";
            view["stackPolicy"] = "ordinary-stack-item-before-move";
            view["paymentPolicy"] = "payment-plan-typed-purple";
            view["staticCannotBecomeActivePolicy"] = "implemented";
        }

        if (string.Equals(requirement.AbilityId, P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId, StringComparison.Ordinal))
        {
            view["swift"] = true;
            view["targetScope"] = "self";
            view["destinationZone"] = MoveUnitBaseZone;
            view["movePolicy"] = "move-source-to-controller-base";
            view["timingPolicy"] = "open-main-representative";
            view["swiftTimingPolicy"] = "full-swift-reaction-timing-deferred";
            view["stackPolicy"] = "ordinary-stack-item-before-move-to-base";
            view["paymentPolicy"] = "payment-plan-typed-blue";
            view["combatDamageStaticPolicy"] = "deferred";
            view["attackDefenseDamageTriggerPolicy"] = "deferred";
        }

        if (string.Equals(requirement.AbilityId, P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, StringComparison.Ordinal))
        {
            view["requiresBaseEquipmentSource"] = true;
            view["readyTargetCount"] = 1;
            view["appliesSpellshieldTargetTax"] = true;
            view["timingPolicy"] = "open-main-representative";
            view["stackPolicy"] = "ordinary-stack-item-before-ready";
            view["paymentPolicy"] = "payment-plan-experience-and-spellshield-tax";
        }

        if (string.Equals(requirement.AbilityId, P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId, StringComparison.Ordinal))
        {
            view["requiresBattlefieldSource"] = true;
            view["tokenCardNo"] = P4ActivatedAbilityCatalog.WarhawkTokenCardNo;
            view["tokenCount"] = P4ActivatedAbilityCatalog.FluftPoroWarhawkTokenCount;
            if (P6TokenFactoryCatalog.TryGetByCardNo(P4ActivatedAbilityCatalog.WarhawkTokenCardNo, out var tokenDefinition))
            {
                view["tokenName"] = tokenDefinition.TokenFamilyName;
                view["tokenPower"] = tokenDefinition.DefaultPower;
                view["tokenTags"] = tokenDefinition.Tags.ToArray();
            }

            view["timingPolicy"] = "open-main-representative";
            view["stackPolicy"] = "ordinary-stack-item-before-token-create";
            view["paymentPolicy"] = "payment-plan-zero-cost-exhaust-as-cost";
        }

        if (string.Equals(requirement.AbilityId, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal))
        {
            view["requiresBattlefieldSource"] = true;
            view["swift"] = true;
            view["appliesSpellshieldTargetTax"] = true;
            view["targetScope"] = "enemy-attacking-unit-at-this-battlefield";
            view["statusEffectId"] = "STUNNED";
            view["timingPolicy"] = "swift-open-focus-before-combat-assignment";
            view["stackPolicy"] = "ordinary-stack-item-before-stun";
            view["paymentPolicy"] = "payment-plan-mana-generic-power-spellshield-tax-exhaust-as-cost";
            view["spellshieldTaxPowerByTargetObjectId"] = requirement.SpellshieldTaxPowerByTargetObjectId
                ?? new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var typedTotal = requirement.AvailablePowerByTrait.Values.Sum(v => (long)v);
        var actualPower = new RunePool(0, checked((int)(requirement.AvailablePower - typedTotal)), requirement.AvailablePowerByTrait);
        return WithPowerCapacity(view, new RunePaymentReachability(actualPower, CandidatePowerByTrait(requirement.PaymentResourcePowerByChoice)));
    }

    private static IReadOnlyDictionary<string, object?> PlayCardSourceRequirement(
        MatchState state,
        string playerId,
        string sourceObjectId,
        CardBehaviorDefinition behavior)
    {
        var targetCountConditionApplies = PromptTargetCountConditionApplies(state, playerId, behavior);
        var minTargetCount = PromptMinTargetCount(behavior, targetCountConditionApplies);
        var maxTargetCount = PromptMaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        var targetScope = PromptTargetScopeForBehavior(behavior);
        var targetChoicesByIndex = Enumerable.Range(0, maxTargetCount)
            .ToDictionary(
                targetIndex => targetIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                targetIndex => (object?)PromptTargetChoicesForIndex(state, playerId, behavior, targetIndex),
                StringComparer.Ordinal);
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var powerPurpose = CoreRuleEngine.PowerPurposeForPlay(behavior);
        var sourceBalances = PaymentCostRules.AvailablePowerSources(runePool, powerPurpose);
        var showPowerSources = runePool.UnitOrEquipmentPlayOnlyPower > 0 || sourceBalances.Count > 1;
        runePool = PaymentCostRules.PowerPoolForPurpose(runePool, powerPurpose);
        var paymentResourceChoices = PlayCardPaymentResourceChoicesForBehavior(state, playerId, behavior, sourceObjectId);
        var paymentResourcePowerByTrait = PlayCardPaymentResourcePowerByTraitForBehavior(state, playerId, behavior, sourceObjectId);
        var paymentResourcePowerByChoice = PlayCardPaymentResourcePowerByChoiceForBehavior(state, playerId, behavior, sourceObjectId);
        var availablePowerByTrait = PlayCardAvailablePowerByTrait(
            runePool,
            new Dictionary<string, int>(StringComparer.Ordinal));
        var availablePowerByTraitWithPaymentResources = PlayCardAvailablePowerByTrait(
            runePool,
            paymentResourcePowerByTrait);
        var manaCost = CoreRuleEngine.MinimumPlayManaCost(state, playerId, behavior, sourceObjectId);
        var minimumManaCost = manaCost.Total;
        var luxSpellOnlyGeneratedMana = PromptLuxSpellOnlyGeneratedMana(
            state,
            playerId,
            behavior,
            sourceObjectId,
            minimumManaCost);

        CoreRuleEngine.TrySelectPlayPowerCost(state, playerId, behavior, null, runePool, CoreRuleEngine.SourceReplayPowerCost(state, playerId), new Dictionary<string, int>(),
            out var printedGenericCost, out var printedTypedCost);
        if (behavior.MaySacrificeUnitForPrintedCostReduction
            && AffordableOptionalSacrificePlayCosts(state, playerId, behavior, sourceObjectId).MinBy(cost => cost.Mana.Total) is { } sacrifice)
        {
            manaCost = sacrifice.Mana;
            minimumManaCost = manaCost.Total;
            printedGenericCost = sacrifice.GenericPower;
            printedTypedCost = sacrifice.TypedPower;
        }
        var printedPowerShortfall = PaymentCostRules.PowerDeficit(runePool, printedGenericCost, printedTypedCost);
        var reductionGroups = new List<CoreRuleEngine.OptionalCostReductionGroup>();
        var optionalCostChoices = CoreRuleEngine.EffectPlayIgnoresAllCosts(state, playerId)
            ? Array.Empty<ActionPromptChoiceDto>()
            : PlayCardOptionalCostChoicesForBehavior(state, playerId, behavior, sourceObjectId, reductionGroups);

        var requirement = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = sourceObjectId,
            ["cardNo"] = behavior.CardNo,
            ["displayName"] = behavior.DisplayName,
            ["sourceZone"] = state.PendingEffectPlay?.SourceZone
                ?? (behavior.AppliedFlowCost is not null ? "GRAVEYARD" : behavior.StandbyPlayFrom is not null ? "STANDBY" : state.PlayerZones[playerId].ChampionZone.Contains(sourceObjectId, StringComparer.Ordinal) ? "CHAMPION" : "HAND"),
            ["mode"] = string.IsNullOrWhiteSpace(behavior.Mode) ? null : behavior.Mode,
            ["modeLabel"] = PlayCardModeLabel(behavior.Mode),
            ["manaCost"] = behavior.ManaCost,
            ["minimumManaCost"] = minimumManaCost,
            ["minimumPrintedPowerShortfall"] = printedPowerShortfall,
            ["printedPowerCost"] = PrintedPowerCostRules.ForCard(behavior.CardNo).Amount,
            ["effectPlayReason"] = behavior.AppliedFlowCost is not null ? "流转：从废牌堆支付替代基础费用打出；离开结算链时改为放逐" : behavior.StandbyPlayFrom is not null ? "从待命打出：免基础费用，额外费用另计；遵守原战场限制" : state.PendingEffectPlay is { } effectPlay ? CoreRuleEngine.EffectPlayReason(effectPlay) : null,
            ["printedPowerTraits"] = PrintedPowerCostRules.ForCard(behavior.IgnorePrintedPowerCost ? "" : behavior.CardNo).Traits,
            ["printedPowerChoices"] = PrintedPowerChoices(behavior),
            ["powerSpendSelection"] = PlayPowerSpendSelectionRules.Metadata,
            ["manaSpendSelection"] = ManaSpendSelectionRules.Metadata,
            ["availableRainbowPower"] = runePool.Power,
            ["battlefieldEquipmentCostReductionMana"] = manaCost.EquipmentReduction,
            ["dragonUnitCostReductionMana"] = manaCost.UnitReduction,
            ["nextSpellCostReductionMana"] = manaCost.NextSpellReduction,
            ["battlefieldSpellCostReductionMana"] = manaCost.BattlefieldSpellReduction,
            ["battlefieldHeldUnitCostIncreaseMana"] = manaCost.Increase,
            ["minTargetCount"] = minTargetCount,
            ["maxTargetCount"] = maxTargetCount,
            ["targetCountLabel"] = minTargetCount == maxTargetCount
                ? maxTargetCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : $"{minTargetCount}-{maxTargetCount}",
            ["targetScope"] = targetScope,
            ["targetScopeLabel"] = PromptTargetScopeLabel(targetScope),
            ["allowsRepeatedTargets"] = behavior.AllowsRepeatedTargets,
            ["supportsSeparateExecutions"] = CoreRuleEngine.SupportsSeparateExecution(behavior),
            ["targetChoicesByIndex"] = targetChoicesByIndex,
            ["legalTargetSelections"] = PlayCardLegalTargetSelections(state, playerId, behavior),
            ["destinationChoices"] = PlayCardDestinationChoicesForBehavior(state, playerId, behavior),
            ["movementDestinationChoicesByIndex"] = behavior.MovesTargetsSequentially
                ? Enumerable.Range(0, behavior.RequiredTargetCount).ToDictionary(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    i => CoreRuleEngine.SequentialMovementDestinations(state, playerId, i)) : null,
            ["destinationLabel"] = behavior.MovesTargetsToChosenLocation || behavior.CommandedMovement is not null ? "移动目的地" : "入场位置",
            ["optionalCostChoices"] = optionalCostChoices,
            ["paymentResourceChoices"] = paymentResourceChoices,
            ["paymentResourcePowerByChoice"] = paymentResourcePowerByChoice,
            ["availableMana"] = AvailablePlayMana(state, runePool, behavior),
            ["availableSpellOnlyMana"] = IsPromptSpellPlayBehavior(behavior) ? runePool.SpellOnlyMana : 0,
            ["availableManaWithPaymentResources"] = PromptAvailableManaWithLuxSpellOnlyResource(state, playerId, behavior, sourceObjectId),
            ["paymentResourceMana"] = PromptAvailableManaWithLuxSpellOnlyResource(state, playerId, behavior, sourceObjectId)
                - AvailablePlayMana(state, runePool, behavior),
            ["availablePower"] = runePool.TotalPower,
            ["availablePowerByTrait"] = availablePowerByTrait,
            ["availablePowerWithPaymentResources"] = new RunePaymentReachability(runePool, paymentResourcePowerByTrait).MaximumTotal,
            ["availablePowerByTraitWithPaymentResources"] = availablePowerByTraitWithPaymentResources,
            ["hasteReadyPowerCost"] = behavior.HasteReadyPowerCost,
            ["hasteReadyPowerTrait"] = HasteReadyPowerTrait(behavior),
            ["composable"] = true,
            ["unsupportedReason"] = string.Empty
        };
        var spellPowerReductionChoices = CoreRuleEngine.SpellPowerReductionChoices(state, playerId, behavior);
        if (spellPowerReductionChoices.Count > 0) requirement["spellPowerReductionChoices"] = spellPowerReductionChoices;
        if (paymentResourceChoices.Any(choice => choice.Id.StartsWith("LEGEND_RESOURCE:", StringComparison.Ordinal)))
            requirement["paymentResourceOrderMatters"] = true;
        if (manaCost.EmpoweredSpellReduction > 0) requirement["empoweredSpellCostReductionMana"] = manaCost.EmpoweredSpellReduction;
        if (showPowerSources) requirement["availableWildcardSources"] = sourceBalances;
        if (reductionGroups.Count > 0)
            requirement["optionalCostReductionGroups"] = reductionGroups
                .GroupBy(group => (group.SourceObjectId, group.SourceGeneration, group.SourceInstanceId, group.ComponentId))
                .Select(group => new Dictionary<string, object?> {
                    ["sourceObjectId"] = group.Key.SourceObjectId, ["sourceGeneration"] = group.Key.SourceGeneration,
                    ["sourceInstanceId"] = group.Key.SourceInstanceId, ["ruleTextOrigin"] = group.First().RuleTextOrigin,
                    ["componentId"] = group.Key.ComponentId, ["minSelections"] = 1, ["maxSelections"] = 1,
                    ["choiceIds"] = group.SelectMany(item => item.ChoiceIds).Distinct(StringComparer.Ordinal).ToArray(),
                    ["requiresOneOfOptionalCostSelections"] = group.Select(item => item.RequiredOptionalCosts)
                        .DistinctBy(costs => JsonSerializer.Serialize(costs)).ToArray() }).ToArray();
        return WithPowerCapacity(requirement, new RunePaymentReachability(runePool, paymentResourcePowerByTrait));
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> PlayCardPaymentResourcePowerByChoiceForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId = null)
    {
        if (!PlayCardBehaviorMayNeedPaymentResource(state, playerId, behavior, sourceObjectId))
        {
            return new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var paymentResourceChoices = PlayCardPaymentResourceChoicesForBehavior(state, playerId, behavior, sourceObjectId);
        if (paymentResourceChoices.Count == 0)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        }

        var metadata = paymentResourceChoices
            .Select(choice => choice.Id)
            .Where(choiceId => TryParseRecycleRunePaymentActionId(choiceId, out _))
            .Select(choiceId =>
            {
                TryParseRecycleRunePaymentActionId(choiceId, out var objectId);
                return objectId;
            })
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .Select(objectId => state.CardObjects[objectId])
            .Where(runeState => TryGetRuneTrait(runeState, out _))
            .ToDictionary(
                runeState => $"{RecycleRunePaymentOptionalCostPrefix}{runeState.ObjectId}",
                runeState =>
                {
                    TryGetRuneTrait(runeState, out var runeTrait);
                    return (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["trait"] = runeTrait,
                        ["power"] = BasicRuneRecyclePowerGain
                    };
                },
                StringComparer.Ordinal);

        var legendChoices = PromptRenektonPaymentChoices(state, playerId);
        foreach (var choice in paymentResourceChoices)
        {
            var legend = legendChoices.SingleOrDefault(candidate => candidate.Id == choice.Id);
            if (legend is not null)
            {
                metadata[choice.Id] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mana"] = 2,
                    ["unitPlayOrUnitAbilityOnly"] = true,
                    ["powerCost"] = 2,
                    ["powerSpend"] = legend.PowerSpend,
                    ["sourceObjectId"] = legend.SourceObjectId,
                    ["requiredRecycleRunePowerByTrait"] = legend.RequiredRunePower,
                    ["resourceLifecycle"] = "rune-pool"
                };
                continue;
            }
            if (!TryParseLuxSpellOnlyResourceActionId(choice.Id, out var luxSourceObjectId))
            {
                continue;
            }

            metadata[choice.Id] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mana"] = P4ActivatedAbilityCatalog.LuxGeneratedMana,
                ["paymentOnly"] = false,
                ["spellOnly"] = true,
                ["sourceObjectId"] = luxSourceObjectId,
                ["cardNo"] = state.CardObjects[luxSourceObjectId].CardNo,
                ["abilityId"] = P4ActivatedAbilityCatalog.LuxResourceAbilityId,
                ["resourceRestriction"] = P4ActivatedAbilityCatalog.LuxSpellOnlyResourceRestriction,
                ["resourceLifecycle"] = "rune-pool",
                ["generatedResourceCannotBeTargetedAsResponse"] = true
            };
        }

        return metadata;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? PlayCardDestinationChoicesForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (behavior.StandbyPlayFrom is { } shadow && (behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment))
            return [new("BATTLEFIELD:" + shadow.Battlefield.ObjectId, "待命原战场")];
        if (behavior.MovesTargetsSequentially) return [];
        if (behavior.CommandedMovement is { } commanded)
            return CoreRuleEngine.CommandedMovementDestinations(state, playerId, commanded);
        if (behavior.MovesTargetsToChosenLocation)
            return CoreRuleEngine.ChosenMovementDestinations(state, playerId);
        if (state.PendingEffectPlay is not null && behavior.PlaysSourceToBaseAsUnit)
            return CoreRuleEngine.EffectPlayDestinations(state, playerId).Select(id => new ActionPromptChoiceDto(id, id == "BASE" ? "基地" : state.PendingEffectPlay.RevealedHand is not null ? "效果指定战场" : BattlefieldLocationLabel(state, id["BATTLEFIELD:".Length..]))).ToArray();

        if (!behavior.PlaysSourceToBaseAsUnit)
        {
            return null;
        }

        var choices = CoreRuleEngine.UnitPlayDestinationChoices(state, playerId, behavior)
            .Select(choice => choice.Id.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
                ? choice with { Label = BattlefieldLocationLabel(state, choice.Id["BATTLEFIELD:".Length..]) } : choice).ToArray();
        return choices.Length == 0 ? null : choices;
    }
}

public sealed partial class MatchSession : IMatchSession
{
    private const string ClientIntentConflictMessage = "该客户端行动编号已用于其他命令。";
    private const string BattlefieldLegendAttachArmamentSeedCardNo = "SFD·208/221";
    private const string BattlefieldWinningScoreSeedCardNo = "OGN·276/298";
    private const string BattlefieldHeldUnitCostIncreaseEffectPrefix = "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:";
    private const string RagingDrakeNextSpellCostReductionEffectPrefix = "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:";
    private const string BattlefieldUnitGainExperienceAbilityId = "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE";

    private static readonly SemaphoreSlim OfficialCatalogGate = new(1, 1);
    private static OfficialCardCatalog? officialCatalog;

    private readonly IRuleEngine ruleEngine;
    private readonly IMatchJournal journal;
    private readonly IMatchPlayerStore playerStore;
    private readonly MatchSessionOptions options;
    private readonly long openingSeed;
    private readonly object seatGate = new();
    private readonly SemaphoreSlim serialGate = new(1, 1);
    private readonly Dictionary<string, string> seats = new();
    private readonly Dictionary<string, string?> reconnectTokens = new();
    private readonly Dictionary<string, CachedResolution> intentCache = new();
    private MatchState state;
    private long lastEventSequence;

    public MatchSession(string roomId, IRuleEngine ruleEngine)
        : this(roomId, ruleEngine, NoopMatchJournal.Instance)
    {
    }

    public MatchSession(string roomId, IRuleEngine ruleEngine, IMatchJournal journal)
        : this(roomId, ruleEngine, journal, NoopMatchPlayerStore.Instance)
    {
    }

    public MatchSession(
        string roomId,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore)
        : this(roomId, ruleEngine, journal, playerStore, MatchSessionOptions.Default)
    {
    }

    public MatchSession(
        string roomId,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore,
        MatchSessionOptions options)
    {
        RoomId = roomId;
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        this.playerStore = playerStore;
        this.options = options ?? MatchSessionOptions.Default;
        state = MatchState.Create(roomId) with { Seed = CreatePrivateOpeningSeed() };
        openingSeed = state.Seed;
    }

    public MatchSession(MatchState initialState, IRuleEngine ruleEngine, IMatchJournal journal)
        : this(initialState, ruleEngine, journal, NoopMatchPlayerStore.Instance)
    {
    }

    public MatchSession(
        MatchState initialState,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore)
        : this(initialState, ruleEngine, journal, playerStore, MatchSessionOptions.Default)
    {
    }

    public MatchSession(
        MatchState initialState,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore,
        MatchSessionOptions options)
        : this(
            initialState,
            0,
            initialState.Seats,
            [],
            initialState.Seed,
            ruleEngine,
            journal,
            playerStore,
            options)
    {
    }

    private MatchSession(
        MatchState restoredState,
        long restoredLastEventSequence,
        IReadOnlyDictionary<string, string> restoredSeats,
        IReadOnlyList<RecoveredCommand> restoredCommands,
        long openingSeed,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore,
        MatchSessionOptions options)
    {
        RuleInputIdentity.RequireCurrentState(restoredState);
        RoomId = restoredState.RoomId;
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        this.playerStore = playerStore;
        this.options = options ?? MatchSessionOptions.Default;
        state = restoredState;
        this.openingSeed = openingSeed;
        lastEventSequence = restoredLastEventSequence;
        foreach (var (playerId, seat) in restoredSeats)
        {
            seats[playerId] = seat;
            reconnectTokens[playerId] = null;
        }

        foreach (var command in restoredCommands)
        {
            var result = new ResolutionResult(
                command.Accepted,
                command.ErrorMessage,
                restoredState,
                [],
                ResolutionResult.BuildSnapshots(restoredState),
                ResolutionResult.BuildPrompts(restoredState),
                command.ErrorCode);
            intentCache[$"{command.PlayerId}:{command.ClientIntentId}"] = new CachedResolution(
                command.CommandType,
                InputFingerprint(command.RawCommand, command.TypedCommand ?? TypedCommandInput.Parameterless(command.CommandType)),
                result);
        }
    }

    public string RoomId { get; }

    public static MatchSession Restore(
        MatchRecoveryFrame recovery,
        IRuleEngine ruleEngine,
        IMatchJournal journal)
    {
        return Restore(recovery, ruleEngine, journal, NoopMatchPlayerStore.Instance);
    }

    public static MatchSession Restore(
        MatchRecoveryFrame recovery,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore)
    {
        return Restore(recovery, ruleEngine, journal, playerStore, MatchSessionOptions.Default);
    }

    public static MatchSession Restore(
        MatchRecoveryFrame recovery,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore,
        MatchSessionOptions options)
    {
        if (recovery.ReplayInitialState is { } replayInitialState)
            RuleInputIdentity.RequireCurrentState(replayInitialState);
        if (recovery.Commands.Any(command => !RuleInputIdentity.IsCurrent(command.RuleInputs)))
            throw new MatchSessionException(ErrorCodes.RecoveryInconsistent,
                "match recovery command rule input identity is missing or differs from running inputs");
        if (!recovery.IsConsistent)
        {
            throw new MatchSessionException(
                ErrorCodes.RecoveryInconsistent,
                $"match recovery is inconsistent: {string.Join("; ", recovery.ValidationErrors)}");
        }

        var state = RestoreState(recovery);
        if (recovery.ReplayInitialState is { } original && original.Seed != recovery.OpeningSeed
            || state.Seed != recovery.OpeningSeed && !recovery.Commands.Any(command => command.CommandType == "DEV_SEED_SCENARIO"
                || command.CommandType.StartsWith("DEV_SEED_SCENARIO:", StringComparison.Ordinal)))
            throw new MatchSessionException(ErrorCodes.RecoveryInconsistent,
                "match recovery state seed differs from recorded opening seed");
        return new MatchSession(
            state,
            recovery.LastEventSequence,
            state.Seats,
            recovery.Commands,
            recovery.OpeningSeed,
            ruleEngine,
            journal,
            playerStore,
            options);
    }

    public PlayerSessionDto EnsurePlayer(string playerId)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        lock (seatGate)
        {
            if (seats.ContainsKey(normalizedPlayerId))
            {
                return PlayerSessionFor(normalizedPlayerId);
            }

            if (seats.Count >= 2)
            {
                throw new MatchSessionException(ErrorCodes.RoomFull, "房间已有两名玩家。");
            }

            seats[normalizedPlayerId] = NextOpenSeat();
            reconnectTokens[normalizedPlayerId] = NewReconnectToken();
            state = StateWithRoomSeats(state, seats, normalizedPlayerId);
            return PlayerSessionFor(normalizedPlayerId);
        }
    }

    public async ValueTask<PlayerSessionDto> EnsurePlayerAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var registered = await RegisterPlayerAsync(playerId, cancellationToken).ConfigureAwait(false);
        return registered.Player;
    }

    public PlayerSessionDto ReconnectPlayer(string playerId, string reconnectToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        lock (seatGate)
        {
            if (!reconnectTokens.TryGetValue(normalizedPlayerId, out var expectedToken)
                || string.IsNullOrWhiteSpace(reconnectToken)
                || !string.Equals(expectedToken, reconnectToken.Trim(), StringComparison.Ordinal))
            {
                throw new MatchSessionException(ErrorCodes.InvalidReconnectToken, "重连令牌无效。");
            }

            return PlayerSessionFor(normalizedPlayerId);
        }
    }

    public async ValueTask<PlayerSessionDto> ReconnectPlayerAsync(
        string playerId,
        string reconnectToken,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        var normalizedToken = reconnectToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            throw new MatchSessionException(ErrorCodes.InvalidReconnectToken, "重连令牌无效。");
        }

        bool valid;
        lock (seatGate)
        {
            RequirePlayer(normalizedPlayerId);
            valid = reconnectTokens.TryGetValue(normalizedPlayerId, out var expectedToken)
                && !string.IsNullOrWhiteSpace(expectedToken)
                && string.Equals(expectedToken, normalizedToken, StringComparison.Ordinal);
        }

        if (!valid)
        {
            var tokenHash = ReconnectTokenHasher.Hash(normalizedToken);
            valid = await playerStore.HasReconnectTokenHashAsync(
                    RoomId,
                    normalizedPlayerId,
                    tokenHash,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!valid)
        {
            throw new MatchSessionException(ErrorCodes.InvalidReconnectToken, "重连令牌无效。");
        }

        return await RotateReconnectTokenAsync(normalizedPlayerId, cancellationToken).ConfigureAwait(false);
    }

    public SnapshotDto SnapshotFor(string playerId)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        RequirePlayer(normalizedPlayerId);
        return ResolutionResult.BuildSnapshots(state)[normalizedPlayerId];
    }

    public ActionPromptDto PromptFor(string playerId)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        RequirePlayer(normalizedPlayerId);
        return ResolutionResult.BuildPrompts(state)[normalizedPlayerId];
    }

    public async ValueTask<PlayCostQuoteDto> PreviewPlayCardAsync(
        string playerId, PlayCostPreviewRequestDto request, CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        RequirePlayer(normalizedPlayerId);
        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (request.Command is null || string.IsNullOrWhiteSpace(request.RequestId))
                return PlayCostQuoteDto.Rejected(request, state.Tick, ErrorCodes.InvalidTarget, "费用预览请求不完整。");
            var prompts = BuildPromptFreshnessCandidates(state, normalizedPlayerId);
            var prompt = prompts.FirstOrDefault(p => p.PromptId == request.PromptId && p.SnapshotTick == request.SnapshotTick);
            if (prompt is null)
                return PlayCostQuoteDto.Rejected(request, state.Tick, ErrorCodes.PromptExpired, "行动窗口已更新，请重新选择。");
            if (!prompt.Actionable || !prompt.Actions.Contains(CommandTypes.PlayCard, StringComparer.Ordinal))
                return PlayCostQuoteDto.Rejected(request, state.Tick, ErrorCodes.PhaseNotAllowed, "当前不能打出卡牌。");
            return ruleEngine.PreviewPlayCard(state, normalizedPlayerId, request);
        }
        finally { serialGate.Release(); }
    }

    public async ValueTask<ResolutionResult> SeedScenarioAsync(
        string playerId,
        string clientIntentId,
        string scenarioId,
        JsonElement? rawCommand,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        RequirePlayer(normalizedPlayerId);
        var normalizedIntentId = NormalizeClientIntentId(clientIntentId);
        var normalizedScenarioId = NormalizeScenarioId(scenarioId);
        var commandType = $"DEV_SEED_SCENARIO:{normalizedScenarioId}";
        var cacheKey = $"{normalizedPlayerId}:{normalizedIntentId}";
        if (rawCommand is { } presentRaw && presentRaw.ValueKind != JsonValueKind.Undefined)
            rawCommand = presentRaw.Clone();
        var typedInput = TypedCommandInput.CaptureScenario(normalizedScenarioId);
        var rawCommandHash = InputFingerprint(rawCommand, typedInput);

        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (intentCache.TryGetValue(cacheKey, out var cached))
            {
                if (IntentConflicts(cached, commandType, rawCommandHash))
                {
                    throw new MatchSessionException(
                        ErrorCodes.ClientIntentConflict,
                        ClientIntentConflictMessage);
                }

                if (!MatchesPresentInput(rawCommand, commandType, typedInput))
                    return ResolutionResult.Rejected(state, "行动请求输入不一致或格式无效。", ErrorCodes.InvalidPayload);

                return cached.Result;
            }

            if (!HasCanonicalPresentRawEnvelope(rawCommand, "DEV_SEED_SCENARIO"))
                return ResolutionResult.Rejected(state, "行动请求格式无效，请按最新提示重试。", ErrorCodes.InvalidPayload);

            if (!MatchesPresentInput(rawCommand, commandType, typedInput))
                return ResolutionResult.Rejected(state, "行动请求输入不一致或格式无效。", ErrorCodes.InvalidPayload);

            var startedTick = state.Tick;
            var startedEventSequence = lastEventSequence;
            var nextState = BuildDevScenarioState(state, normalizedScenarioId) with
            {
                Tick = state.Tick + 1
            };
            IReadOnlyList<GameEvent> preparationEvents = [];
            if (normalizedScenarioId == "standby-reaction")
            {
                var preparation = await PrepareStandbyDevelopmentScenario(nextState, cancellationToken).ConfigureAwait(false);
                nextState = preparation.State;
                preparationEvents = preparation.Events;
            }
            var events = new[]
            {
                new GameEvent(
                    "DEV_SCENARIO_SEEDED",
                    $"开发测试场景已载入: {normalizedScenarioId}",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = normalizedPlayerId,
                        ["scenarioId"] = normalizedScenarioId
                    })
            }.Concat(preparationEvents).ToArray();
            var result = new ResolutionResult(
                true,
                null,
                nextState,
                events,
                ResolutionResult.BuildSnapshots(nextState),
                ResolutionResult.BuildPrompts(nextState));

            var completedEventSequence = startedEventSequence + events.Length;
            await journal.RecordAsync(new MatchJournalEntry(
                    RoomId,
                    normalizedPlayerId,
                    normalizedIntentId,
                    commandType,
                    rawCommand,
                    startedTick,
                    nextState.Tick,
                    startedEventSequence,
                    completedEventSequence,
                    true,
                    null,
                    nextState,
                    events,
                    result.Snapshots,
                    result.Prompts,
                    DateTimeOffset.UtcNow) { OpeningSeed = openingSeed, ErrorCode = result.ErrorCode, TypedCommand = rawCommand is null ? typedInput : null },
                cancellationToken).ConfigureAwait(false);
            lastEventSequence = completedEventSequence;

            state = nextState;
            intentCache[cacheKey] = new CachedResolution(commandType, rawCommandHash, result);
            return result;
        }
        finally
        {
            serialGate.Release();
        }
    }

    public async ValueTask<ResolutionResult> SubmitDeckAsync(
        string playerId,
        string clientIntentId,
        SubmitDeckCommand command,
        JsonElement? rawCommand,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalizedPlayerId = NormalizePlayerId(playerId);
        RequirePlayer(normalizedPlayerId);
        var normalizedIntentId = NormalizeClientIntentId(clientIntentId);
        var cacheKey = $"{normalizedPlayerId}:{normalizedIntentId}";
        if (rawCommand is { } presentRaw && presentRaw.ValueKind != JsonValueKind.Undefined)
            rawCommand = presentRaw.Clone();
        var typedInput = TypedCommandInput.Capture(command);
        command = (SubmitDeckCommand)typedInput.Restore();
        var rawCommandHash = InputFingerprint(rawCommand, typedInput);

        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (intentCache.TryGetValue(cacheKey, out var cached))
            {
                if (IntentConflicts(cached, "SUBMIT_DECK", rawCommandHash))
                {
                    throw new MatchSessionException(
                        ErrorCodes.ClientIntentConflict,
                        ClientIntentConflictMessage);
                }

                if (!MatchesPresentInput(rawCommand, "SUBMIT_DECK", typedInput))
                    return ResolutionResult.Rejected(state, "行动请求输入不一致或格式无效。", ErrorCodes.InvalidPayload);

                return cached.Result;
            }

            if (!HasCanonicalPresentRawEnvelope(rawCommand, "SUBMIT_DECK"))
                return ResolutionResult.Rejected(state, "行动请求格式无效，请按最新提示重试。", ErrorCodes.InvalidPayload);

            if (!MatchesPresentInput(rawCommand, "SUBMIT_DECK", typedInput))
                return ResolutionResult.Rejected(state, "行动请求输入不一致或格式无效。", ErrorCodes.InvalidPayload);

            var startedTick = state.Tick;
            var startedEventSequence = lastEventSequence;
            ResolutionResult result;
            if (state.Status == MatchStatuses.Finished)
            {
                result = ResolutionResult.Rejected(
                    state,
                    "对局已经结束，不能提交卡组。",
                    ErrorCodes.MatchFinished);
            }
            else if (TryRejectStalePrompt(state, normalizedPlayerId, rawCommand, out var promptRejection))
            {
                result = promptRejection;
            }
            else if (state.Status == MatchStatuses.InProgress)
            {
                result = ResolutionResult.Rejected(
                    state,
                    "对局开始后不能更改卡组。",
                    ErrorCodes.PhaseNotAllowed);
            }
            else if (state.ReadyPlayerIds.Contains(normalizedPlayerId, StringComparer.Ordinal))
            {
                result = ResolutionResult.Rejected(
                    state,
                    "玩家准备后不能更改卡组。",
                    ErrorCodes.PhaseNotAllowed);
            }
            else
            {
                var decklist = OfficialDeckValidator.Normalize(new OfficialDecklist(
                    command.LegendCardNo,
                    command.ChampionCardNo,
                    command.MainDeck,
                    command.RuneDeck,
                    command.Battlefields));
                var catalog = await LoadOfficialCatalogAsync(cancellationToken).ConfigureAwait(false);
                var validation = OfficialDeckValidator.Validate(decklist, catalog, options.DeckFormat);
                if (!validation.IsValid)
                {
                    result = ResolutionResult.Rejected(
                        state,
                        $"卡组不合法：{string.Join("；", validation.Errors)}",
                        ErrorCodes.InvalidDeck);
                }
                else if (state.PlayerDecklists.TryGetValue(normalizedPlayerId, out var existingDecklist)
                    && OfficialDecklistsEqual(existingDecklist, decklist))
                {
                    result = ResolutionResult.Rejected(
                        state,
                        "玩家已经提交相同卡组。",
                        ErrorCodes.PhaseNotAllowed);
                }
                else
                {
                    var decklists = state.PlayerDecklists.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                    decklists[normalizedPlayerId] = decklist;
                    var nextState = state with
                    {
                        Tick = state.Tick + 1,
                        PlayerDecklists = decklists
                    };
                    var events = new[]
                    {
                        new GameEvent(
                            "DECK_SUBMITTED",
                            $"{normalizedPlayerId} 提交正式卡组",
                            new Dictionary<string, object?>
                            {
                                ["playerId"] = normalizedPlayerId,
                                ["mainDeckCount"] = decklist.MainDeck.Count,
                                ["runeDeckCount"] = decklist.RuneDeck.Count,
                                ["battlefieldCount"] = decklist.Battlefields.Count,
                                ["legendCardNo"] = decklist.LegendCardNo,
                                ["championCardNo"] = decklist.ChampionCardNo
                            })
                    };
                    result = new ResolutionResult(
                        true,
                        null,
                        nextState,
                        events,
                        ResolutionResult.BuildSnapshots(nextState),
                        ResolutionResult.BuildPrompts(nextState));
                }
            }

            var completedEventSequence = startedEventSequence + result.Events.Count;
            await journal.RecordAsync(new MatchJournalEntry(
                    RoomId,
                    normalizedPlayerId,
                    normalizedIntentId,
                    "SUBMIT_DECK",
                    rawCommand,
                    startedTick,
                    result.State.Tick,
                    startedEventSequence,
                    completedEventSequence,
                    result.Accepted,
                    result.ErrorMessage,
                    result.State,
                    result.Events,
                    result.Snapshots,
                    result.Prompts,
                    DateTimeOffset.UtcNow) { OpeningSeed = openingSeed, ErrorCode = result.ErrorCode, TypedCommand = rawCommand is null ? typedInput : null },
                cancellationToken).ConfigureAwait(false);

            if (result.Accepted)
            {
                state = result.State;
            }
            lastEventSequence = completedEventSequence;
            intentCache[cacheKey] = new CachedResolution("SUBMIT_DECK", rawCommandHash, result);
            return result;
        }
        finally
        {
            serialGate.Release();
        }
    }

    public async ValueTask<ResolutionResult> ReadyAsync(
        string playerId,
        string clientIntentId,
        JsonElement? rawCommand,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        RequirePlayer(normalizedPlayerId);
        var normalizedIntentId = NormalizeClientIntentId(clientIntentId);
        var cacheKey = $"{normalizedPlayerId}:{normalizedIntentId}";
        if (rawCommand is { } presentRaw && presentRaw.ValueKind != JsonValueKind.Undefined)
            rawCommand = presentRaw.Clone();
        var typedInput = TypedCommandInput.Capture(new ReadyCommand());
        var rawCommandHash = InputFingerprint(rawCommand, typedInput);

        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (intentCache.TryGetValue(cacheKey, out var cached))
            {
                if (IntentConflicts(cached, "READY", rawCommandHash))
                {
                    throw new MatchSessionException(
                        ErrorCodes.ClientIntentConflict,
                        ClientIntentConflictMessage);
                }

                if (!MatchesPresentInput(rawCommand, "READY", typedInput))
                    return ResolutionResult.Rejected(state, "行动请求输入不一致或格式无效。", ErrorCodes.InvalidPayload);

                return cached.Result;
            }

            if (!HasCanonicalPresentRawEnvelope(rawCommand, "READY"))
                return ResolutionResult.Rejected(state, "行动请求格式无效，请按最新提示重试。", ErrorCodes.InvalidPayload);

            if (!MatchesPresentInput(rawCommand, "READY", typedInput))
                return ResolutionResult.Rejected(state, "行动请求输入不一致或格式无效。", ErrorCodes.InvalidPayload);

            if (state.Status == MatchStatuses.Finished)
            {
                throw new MatchSessionException(ErrorCodes.MatchFinished, "对局已经结束，不能准备。");
            }

            var startedTick = state.Tick;
            var startedEventSequence = lastEventSequence;
            if (TryRejectStalePrompt(state, normalizedPlayerId, rawCommand, out var promptRejection))
            {
                var completedEventSequence = startedEventSequence + promptRejection.Events.Count;
                await journal.RecordAsync(new MatchJournalEntry(
                        RoomId,
                        normalizedPlayerId,
                        normalizedIntentId,
                        "READY",
                        rawCommand,
                        startedTick,
                        promptRejection.State.Tick,
                        startedEventSequence,
                        completedEventSequence,
                        promptRejection.Accepted,
                        promptRejection.ErrorMessage,
                        promptRejection.State,
                        promptRejection.Events,
                        promptRejection.Snapshots,
                        promptRejection.Prompts,
                        DateTimeOffset.UtcNow) { OpeningSeed = openingSeed, ErrorCode = promptRejection.ErrorCode, TypedCommand = rawCommand is null ? typedInput : null },
                    cancellationToken).ConfigureAwait(false);
                lastEventSequence = completedEventSequence;
                intentCache[cacheKey] = new CachedResolution("READY", rawCommandHash, promptRejection);
                return promptRejection;
            }

            if (state.Status == MatchStatuses.InProgress)
            {
                var current = new ResolutionResult(
                    true,
                    null,
                    state,
                    [],
                    ResolutionResult.BuildSnapshots(state),
                    ResolutionResult.BuildPrompts(state));
                intentCache[cacheKey] = new CachedResolution("READY", rawCommandHash, current);
                return current;
            }

            if (!state.PlayerDecklists.ContainsKey(normalizedPlayerId)
                && (!options.AllowLegacyReadyWithoutDeck || state.PlayerDecklists.Count > 0))
            {
                var rejected = ResolutionResult.Rejected(
                    state,
                    "正式卡组房间需要先提交合法卡组才能准备。",
                    ErrorCodes.InvalidDeck);
                intentCache[cacheKey] = new CachedResolution("READY", rawCommandHash, rejected);
                return rejected;
            }

            if (options.DeckFormat != OfficialDeckFormat.CoreRules)
            {
                var catalog = await LoadOfficialCatalogAsync(cancellationToken).ConfigureAwait(false);
                foreach (var submitted in state.PlayerDecklists.Values)
                {
                    var validation = OfficialDeckValidator.Validate(submitted, catalog, options.DeckFormat);
                    if (!validation.IsValid)
                    {
                        var rejected = ResolutionResult.Rejected(state,
                            $"卡组需要更新：{string.Join("；", validation.Errors)}", ErrorCodes.InvalidDeck);
                        intentCache[cacheKey] = new CachedResolution("READY", rawCommandHash, rejected);
                        return rejected;
                    }
                }
            }

            var readyPlayers = state.ReadyPlayerIds.ToHashSet(StringComparer.Ordinal);
            var events = new List<GameEvent>();
            var addedReady = readyPlayers.Add(normalizedPlayerId);

            if (state.Status != MatchStatuses.InProgress && addedReady)
            {
                events.Add(new GameEvent(
                    "PLAYER_READY",
                    $"{normalizedPlayerId} 已准备",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = normalizedPlayerId
                    }));
            }

            var nextStatus = state.Status;
            if (state.Status != MatchStatuses.InProgress)
            {
                nextStatus = readyPlayers.Count == 2 && state.Seats.Count == 2
                    ? MatchStatuses.InProgress
                    : MatchStatuses.Seating;
            }

            var nextState = state with
            {
                Status = nextStatus,
                Phase = nextStatus == MatchStatuses.InProgress ? MatchPhases.Main : MatchPhases.Room,
                TimingState = nextStatus == MatchStatuses.InProgress
                    ? TimingStates.NeutralOpen
                    : TimingStates.Room,
                TurnPlayerId = state.ActivePlayerId,
                ReadyPlayerIds = readyPlayers
                    .OrderBy(readyPlayerId => readyPlayerId, StringComparer.Ordinal)
                    .ToArray()
            };
            if (nextStatus == MatchStatuses.InProgress
                && HasOfficialDecklistsForAllSeats(nextState))
            {
                var catalog = await LoadOfficialCatalogAsync(cancellationToken).ConfigureAwait(false);
                var officialOpening = BuildOfficialOpeningState(nextState, catalog);
                nextState = officialOpening.State;
                events.AddRange(officialOpening.Events);
            }

            if (nextStatus == MatchStatuses.InProgress)
            {
                events.Add(new GameEvent(
                    "MATCH_STARTED",
                    "双方已准备，比赛开始",
                    new Dictionary<string, object?>
                    {
                        ["activePlayerId"] = nextState.ActivePlayerId
                    }));
            }
            var result = new ResolutionResult(
                true,
                null,
                nextState,
                events,
                ResolutionResult.BuildSnapshots(nextState),
                ResolutionResult.BuildPrompts(nextState));

            if (events.Count > 0)
            {
                var completedEventSequence = startedEventSequence + events.Count;
                await journal.RecordAsync(new MatchJournalEntry(
                        RoomId,
                        normalizedPlayerId,
                        normalizedIntentId,
                        "READY",
                        rawCommand,
                        startedTick,
                        nextState.Tick,
                        startedEventSequence,
                        completedEventSequence,
                        true,
                        null,
                        nextState,
                        events,
                        result.Snapshots,
                        result.Prompts,
                        DateTimeOffset.UtcNow) { OpeningSeed = openingSeed, ErrorCode = result.ErrorCode, TypedCommand = rawCommand is null ? typedInput : null },
                    cancellationToken).ConfigureAwait(false);
                lastEventSequence = completedEventSequence;
            }

            state = nextState;
            intentCache[cacheKey] = new CachedResolution("READY", rawCommandHash, result);
            return result;
        }
        finally
        {
            serialGate.Release();
        }
    }

    public async ValueTask<ResolutionResult> SubmitAsync(
        string playerId,
        string clientIntentId,
        GameCommand command,
        JsonElement? rawCommand,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        RequirePlayer(normalizedPlayerId);
        var normalizedIntentId = NormalizeClientIntentId(clientIntentId);
        var cacheKey = $"{normalizedPlayerId}:{normalizedIntentId}";
        if (rawCommand is { } presentRaw && presentRaw.ValueKind != JsonValueKind.Undefined)
            rawCommand = presentRaw.Clone();
        var typedInput = TypedCommandInput.Capture(command);
        command = (GameCommand)typedInput.Restore();
        var rawCommandHash = InputFingerprint(rawCommand, typedInput);

        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (intentCache.TryGetValue(cacheKey, out var cached))
            {
                if (IntentConflicts(cached, command.CmdType, rawCommandHash))
                {
                    return ResolutionResult.Rejected(
                        state,
                        ClientIntentConflictMessage,
                        ErrorCodes.ClientIntentConflict);
                }

                if (!MatchesPresentInput(rawCommand, command.CmdType, typedInput))
                    return ResolutionResult.Rejected(state, "行动请求输入不一致或格式无效。", ErrorCodes.InvalidPayload);

                return cached.Result;
            }

            if (!HasCanonicalPresentRawEnvelope(rawCommand, command.CmdType))
                return ResolutionResult.Rejected(state, "行动请求格式无效，请按最新提示重试。", ErrorCodes.InvalidPayload);

            // Both accepted and rejected journal rows undergo independent raw
            // recovery validation. Reject malformed arrays before any path can
            // persist the request, including stale-prompt rejections.
            if (command is (PlayCardCommand or RefusePlayPaymentCommand)
                && rawCommand is { ValueKind: JsonValueKind.Object } playRaw
                && !HasCanonicalRequiredStringArray(playRaw, "targetObjectIds"))
                return ResolutionResult.Rejected(state, "出牌目标格式无效，请按最新提示重试。", ErrorCodes.InvalidPayload);
            if (!HasCanonicalOptionalCosts(command, rawCommand))
                return ResolutionResult.Rejected(state, "可选费用格式无效，请按最新提示重试。", ErrorCodes.InvalidPayload);
            if (command is RefusePlayPaymentCommand && rawCommand is { ValueKind: JsonValueKind.Object } refusalRaw
                && (!HasCanonicalRequiredText(refusalRaw, "sourceObjectId") || !HasCanonicalRequiredText(refusalRaw, "cardNo")))
                return ResolutionResult.Rejected(state, "拒付的出牌来源格式无效，请按最新提示重试。", ErrorCodes.InvalidPayload);
            if (command is DeclareBattleCommand
                && rawCommand is { ValueKind: JsonValueKind.Object } raw
                && !HasCanonicalOptionalStringArray(raw, "battlefieldTargetObjectIds"))
                return ResolutionResult.Rejected(state, "战场目标选项格式无效，请按最新提示重试。", ErrorCodes.InvalidPayload);

            if (!MatchesPresentInput(rawCommand, command.CmdType, typedInput))
                return ResolutionResult.Rejected(state, "行动请求输入不一致或格式无效。", ErrorCodes.InvalidPayload);

            var startedTick = state.Tick;
            var startedEventSequence = lastEventSequence;
            ResolutionResult result;
            if (state.Status == MatchStatuses.Finished)
            {
                if (TryRejectStalePrompt(state, normalizedPlayerId, rawCommand, out var promptRejection))
                {
                    result = promptRejection;
                }
                else
                {
                    throw new MatchSessionException(ErrorCodes.MatchFinished, "对局已经结束，不能继续提交行动。");
                }
            }
            else if (state.Status != MatchStatuses.InProgress)
            {
                throw new MatchSessionException(ErrorCodes.MatchNotStarted, "对局尚未开始。");
            }
            else if (TryRejectStalePrompt(state, normalizedPlayerId, rawCommand, out var promptRejection))
            {
                result = promptRejection;
            }
            else
            {
                var intent = new PlayerIntent(normalizedIntentId, normalizedPlayerId, command.CmdType);
                result = await ruleEngine.ResolveAsync(state, intent, command, cancellationToken)
                    .ConfigureAwait(false);
            }
            var completedEventSequence = startedEventSequence + result.Events.Count;
            await journal.RecordAsync(new MatchJournalEntry(
                    RoomId,
                    normalizedPlayerId,
                    normalizedIntentId,
                    command.CmdType,
                    rawCommand,
                    startedTick,
                    result.State.Tick,
                    startedEventSequence,
                    completedEventSequence,
                    result.Accepted,
                    result.ErrorMessage,
                    result.State,
                    result.Events,
                    result.Snapshots,
                    result.Prompts,
                    DateTimeOffset.UtcNow) { OpeningSeed = openingSeed, ErrorCode = result.ErrorCode, TypedCommand = rawCommand is null ? typedInput : null },
                cancellationToken).ConfigureAwait(false);

            if (result.Accepted)
            {
                state = result.State;
            }
            lastEventSequence = completedEventSequence;

            intentCache[cacheKey] = new CachedResolution(command.CmdType, rawCommandHash, result);
            return result;
        }
        finally
        {
            serialGate.Release();
        }
    }

    private sealed record CachedResolution(string CommandType, string? InputHash, ResolutionResult Result);

    private sealed record OfficialOpeningBuildResult(
        MatchState State,
        IReadOnlyList<GameEvent> Events);

    private sealed record OfficialPlayerOpeningBuildResult(
        PlayerZones Zones,
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyDictionary<string, ObjectLocationState> ObjectLocations,
        IReadOnlyList<GameEvent> Events,
        long RngCursor);

    private sealed record DevScenarioSeed(string P1, string P2);
}
