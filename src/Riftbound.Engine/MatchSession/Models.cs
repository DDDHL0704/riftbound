using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public interface IRuleEngine
{
    PlayCostQuoteDto PreviewPlayCard(MatchState state, string playerId, PlayCostPreviewRequestDto request)
        => PlayCostQuoteDto.Rejected(request, state.Tick, ErrorCodes.UnsupportedCommand, "当前引擎不支持费用预览。");

    ValueTask<ResolutionResult> ResolveAsync(
        MatchState state,
        PlayerIntent intent,
        GameCommand command,
        CancellationToken cancellationToken);
}

public static class MatchStatuses
{
    public const string Empty = "EMPTY";
    public const string Seating = "SEATING";
    public const string InProgress = "IN_PROGRESS";
    public const string Finished = "FINISHED";
}

public static class MatchPhases
{
    public const string Room = "ROOM";
    public const string Mulligan = "MULLIGAN";
    public const string TurnStart = "TURN_START";
    public const string Main = "MAIN";
    public const string TurnEnd = "TURN_END";
}

public static class TimingStates
{
    public const string Room = "ROOM";
    public const string Mulligan = "MULLIGAN";
    public const string NeutralOpen = "NEUTRAL_OPEN";
    public const string NeutralClosed = "NEUTRAL_CLOSED";
    public const string SpellDuelOpen = "SPELL_DUEL_OPEN";
    public const string SpellDuelClosed = "SPELL_DUEL_CLOSED";
}

public static class BattlefieldTaskMarkers
{
    public const string SpellDuelCompletedPrefix = "BATTLEFIELD_SPELL_DUEL_COMPLETED:";
    public const string BattleSkippedPrefix = "BATTLEFIELD_BATTLE_SKIPPED:";
    public const string ScoreGainedThisTurnPrefix = "BATTLEFIELD_SCORE_GAINED_THIS_TURN:";

    public static string SpellDuelCompleted(string battlefieldObjectId)
    {
        return $"{SpellDuelCompletedPrefix}{battlefieldObjectId}";
    }

    public static string BattleSkipped(string battlefieldObjectId)
    {
        return $"{BattleSkippedPrefix}{battlefieldObjectId}";
    }

    public static string ScoreGainedThisTurn(string battlefieldObjectId, string playerId)
    {
        return $"{ScoreGainedThisTurnPrefix}{battlefieldObjectId}:{playerId}";
    }
}

public sealed record RunePool
{
    private static readonly IReadOnlyDictionary<string, int> EmptyPowerByTrait =
        new Dictionary<string, int>(StringComparer.Ordinal);

    [JsonConstructor]
    public RunePool(
        int mana,
        int power,
        IReadOnlyDictionary<string, int>? powerByTrait = null,
        int spellOnlyMana = 0,
        int spellOnlyPower = 0,
        int equipmentOnlyPower = 0,
        int duelOnlyMana = 0,
        int unitOrEquipmentPlayOnlyPower = 0,
        int unitPlayOrUnitAbilityOnlyMana = 0)
    {
        // Preserve malformed input for recovery validation instead of washing
        // negative or unrepresentable resource amounts into a legal pool.
        Mana = mana;
        Power = power;
        PowerByTrait = NormalizePowerByTrait(powerByTrait);
        SpellOnlyMana = spellOnlyMana;
        SpellOnlyPower = spellOnlyPower;
        EquipmentOnlyPower = equipmentOnlyPower;
        DuelOnlyMana = duelOnlyMana;
        UnitOrEquipmentPlayOnlyPower = unitOrEquipmentPlayOnlyPower;
        UnitPlayOrUnitAbilityOnlyMana = unitPlayOrUnitAbilityOnlyMana;
    }

    public static RunePool Empty { get; } = new(0, 0);

    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int Mana { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int SpellOnlyMana { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int DuelOnlyMana { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int UnitPlayOrUnitAbilityOnlyMana { get; init; }

    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int Power { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int SpellOnlyPower { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int EquipmentOnlyPower { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int UnitOrEquipmentPlayOnlyPower { get; init; }

    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public IReadOnlyDictionary<string, int> PowerByTrait { get; init; }

    [JsonIgnore]
    public int TotalPower => PowerByTrait is not null
        && (long)Power + PowerByTrait.Values.Sum(value => (long)value) is >= 0 and <= int.MaxValue
            ? (int)((long)Power + PowerByTrait.Values.Sum(value => (long)value)) : -1;

    // TotalPower remains ordinary spendable power. A payment purpose selects
    // eligible restricted power; all stored power shares the representation limit.
    [JsonIgnore]
    public int TotalStoredPower => PowerByTrait is not null
        && (long)Power + PowerByTrait.Values.Sum(value => (long)value) + SpellOnlyPower + EquipmentOnlyPower + UnitOrEquipmentPlayOnlyPower is >= 0 and <= int.MaxValue
            ? (int)((long)Power + PowerByTrait.Values.Sum(value => (long)value) + SpellOnlyPower + EquipmentOnlyPower + UnitOrEquipmentPlayOnlyPower) : -1;

    [JsonIgnore]
    public int TotalStoredMana => (long)Mana + SpellOnlyMana + DuelOnlyMana + UnitPlayOrUnitAbilityOnlyMana is >= 0 and <= int.MaxValue
        ? (int)((long)Mana + SpellOnlyMana + DuelOnlyMana + UnitPlayOrUnitAbilityOnlyMana) : -1;

    [JsonIgnore]
    public bool HasSupportedAmounts => PowerByTrait is not null && Mana >= 0 && SpellOnlyMana >= 0 && DuelOnlyMana >= 0 && UnitPlayOrUnitAbilityOnlyMana >= 0 && Power >= 0
        && SpellOnlyPower >= 0 && EquipmentOnlyPower >= 0 && UnitOrEquipmentPlayOnlyPower >= 0
        && (long)Mana + SpellOnlyMana + DuelOnlyMana + UnitPlayOrUnitAbilityOnlyMana <= int.MaxValue
        && PowerByTrait.Values.All(value => value >= 0)
        && (long)Power + PowerByTrait.Values.Sum(value => (long)value) + SpellOnlyPower + EquipmentOnlyPower + UnitOrEquipmentPlayOnlyPower <= int.MaxValue;

    public bool Equals(RunePool? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && Mana == other.Mana
            && SpellOnlyMana == other.SpellOnlyMana
            && DuelOnlyMana == other.DuelOnlyMana
            && UnitPlayOrUnitAbilityOnlyMana == other.UnitPlayOrUnitAbilityOnlyMana
            && Power == other.Power
            && SpellOnlyPower == other.SpellOnlyPower
            && EquipmentOnlyPower == other.EquipmentOnlyPower
            && UnitOrEquipmentPlayOnlyPower == other.UnitOrEquipmentPlayOnlyPower
            && PowerByTrait.Count == other.PowerByTrait.Count
            && PowerByTrait.All(entry =>
                other.PowerByTrait.TryGetValue(entry.Key, out var otherValue)
                && entry.Value == otherValue);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Mana);
        hash.Add(SpellOnlyMana);
        hash.Add(DuelOnlyMana);
        hash.Add(UnitPlayOrUnitAbilityOnlyMana);
        hash.Add(Power);
        hash.Add(SpellOnlyPower);
        hash.Add(EquipmentOnlyPower);
        hash.Add(UnitOrEquipmentPlayOnlyPower);
        foreach (var entry in PowerByTrait.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            hash.Add(entry.Key);
            hash.Add(entry.Value);
        }

        return hash.ToHashCode();
    }

    private static IReadOnlyDictionary<string, int> NormalizePowerByTrait(
        IReadOnlyDictionary<string, int>? powerByTrait)
    {
        if (powerByTrait is not null && (powerByTrait.Values.Any(value => value < 0)
            || powerByTrait.Values.Sum(value => (long)value) > int.MaxValue))
            return powerByTrait.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var normalized = (powerByTrait ?? EmptyPowerByTrait)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value > 0)
            .GroupBy(entry => RuneTrait.Normalize(entry.Key), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(entry => Math.Max(0, entry.Value)),
                StringComparer.Ordinal);

        return normalized.Count == 0
            ? EmptyPowerByTrait
            : normalized;
    }
}

public static class RuneTrait
{
    public const string Red = "red";
    public const string Green = "green";
    public const string Blue = "blue";
    public const string Yellow = "yellow";
    public const string Orange = "orange";
    public const string Purple = "purple";

    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["red"] = Red,
            ["红"] = Red,
            ["红色"] = Red,
            ["green"] = Green,
            ["绿"] = Green,
            ["绿色"] = Green,
            ["blue"] = Blue,
            ["蓝"] = Blue,
            ["蓝色"] = Blue,
            ["yellow"] = Yellow,
            ["黄"] = Yellow,
            ["黄色"] = Yellow,
            ["orange"] = Orange,
            ["橙"] = Orange,
            ["橙色"] = Orange,
            ["purple"] = Purple,
            ["紫"] = Purple,
            ["紫色"] = Purple
        };

    public static string Normalize(string? trait)
    {
        if (string.IsNullOrWhiteSpace(trait))
        {
            return string.Empty;
        }

        var normalized = trait.Trim();
        return Aliases.TryGetValue(normalized, out var alias)
            ? alias
            : normalized.ToLowerInvariant();
    }
}

public sealed record PlayerZones(
    IReadOnlyList<string> MainDeck,
    IReadOnlyList<string> RuneDeck,
    IReadOnlyList<string> Hand,
    IReadOnlyList<string> Base,
    IReadOnlyList<string> Battlefields,
    IReadOnlyList<string> Graveyard,
    IReadOnlyList<string> Banished,
    IReadOnlyList<string> LegendZone,
    IReadOnlyList<string> ChampionZone)
{
    public static PlayerZones Empty { get; } = new([], [], [], [], [], [], [], [], []);
}

public sealed record ObjectLocationState(
    string PlayerId,
    string Zone,
    string? BattlefieldObjectId = null)
{
    public static ObjectLocationState Normalize(ObjectLocationState location)
    {
        return new ObjectLocationState(
            string.IsNullOrWhiteSpace(location.PlayerId) ? string.Empty : location.PlayerId.Trim(),
            string.IsNullOrWhiteSpace(location.Zone) ? string.Empty : location.Zone.Trim(),
            string.IsNullOrWhiteSpace(location.BattlefieldObjectId) ? null : location.BattlefieldObjectId.Trim());
    }
}

public sealed record BattlefieldState(
    string BattlefieldObjectId,
    string ZonePlayerId,
    string? CardNo,
    string? ControllerId,
    string Status,
    bool Contested,
    IReadOnlyList<string> OccupantObjectIds,
    IReadOnlyList<string> OccupantControllerIds,
    IReadOnlyList<string> StandbyObjectIds,
    int FaceDownStandbyCount);

public sealed record CleanupTaskState(
    string TaskId,
    string Kind,
    string Reason,
    string? PlayerId = null,
    string? ObjectId = null,
    string? BattlefieldObjectId = null);

public sealed record BattlefieldTaskState(
    string TaskId,
    string Kind,
    string Status,
    string Reason,
    string BattlefieldObjectId,
    IReadOnlyList<string> ParticipantControllerIds,
    IReadOnlyList<string> ParticipantObjectIds,
    string? ActingPlayerId,
    IReadOnlyList<string> StackItemIds);

public sealed record BattlefieldResolutionState(
    string ResolutionId,
    long Tick,
    string Kind,
    string Reason,
    string BattlefieldObjectId,
    string? PlayerId,
    string? PreviousControllerId,
    string? ControllerId,
    string? SourceObjectId,
    IReadOnlyList<string> ParticipantObjectIds,
    IReadOnlyList<string> RelatedEventKinds)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ObjectBinding>? PublicObjectBindings { get; init; }
}

public sealed record BattleResolutionState(
    string ResolutionId,
    long Tick,
    string Kind,
    string Reason,
    string BattlefieldId,
    string? AttackingPlayerId,
    string? DefendingPlayerId,
    string? WinnerPlayerId,
    IReadOnlyList<string> AttackerObjectIds,
    IReadOnlyList<string> DefenderObjectIds,
    IReadOnlyList<string> SurvivingAttackerObjectIds,
    IReadOnlyList<string> SurvivingDefenderObjectIds,
    IReadOnlyList<string> DestroyedObjectIds,
    IReadOnlyList<string> RelatedEventKinds)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ObjectBinding>? PublicObjectBindings { get; init; }
}

public sealed record PendingTaskQueueState(
    bool HasTasks,
    bool IsBlocking,
    string Phase,
    string? ActiveTaskId,
    IReadOnlyList<CleanupTaskState> Tasks);

public sealed record TurnWindowState(
    string State,
    bool IsSpellDuel,
    bool IsClosed,
    bool HasStack,
    string? ActingPlayerId);

public sealed record SpellDuelState(
    bool IsActive,
    bool IsClosed,
    string? SpellDuelId,
    string? BattlefieldObjectId,
    string? FocusPlayerId,
    IReadOnlyList<string> PassedFocusPlayerIds,
    IReadOnlyList<string> StackItemIds,
    IReadOnlyList<string> StackControllerIds);

public sealed record BattleState(
    bool IsActive,
    string? BattleId,
    string? BattlefieldObjectId,
    IReadOnlyList<string> AttackerObjectIds,
    IReadOnlyList<string> DefenderObjectIds,
    IReadOnlyDictionary<string, string> ParticipantControllerIds);

internal sealed record BattleDamageAssignmentLedger(
    string BattleId,
    string BattlefieldId,
    IReadOnlyList<CombatDamageAssignmentDto> Assignments);

internal static class BattleLifecycleIds
{
    public static string? SpellDuelIdForBattlefield(string? battlefieldObjectId)
    {
        return string.IsNullOrWhiteSpace(battlefieldObjectId)
            ? null
            : $"spell-duel:{battlefieldObjectId}";
    }

    public static string? BattleIdForBattlefield(string? battlefieldObjectId)
    {
        return string.IsNullOrWhiteSpace(battlefieldObjectId)
            ? null
            : $"battle:{battlefieldObjectId}";
    }
}

public static class ContinuousEffectLayers
{
    public const string PowerModifier = "POWER_MODIFIER";
    public const string RuleText = "RULE_TEXT";
    public const string StaticAura = "STATIC_AURA";
}

public sealed record ContinuousEffectState(
    string EffectId,
    string Scope,
    string Layer,
    string Duration,
    string? TargetObjectId = null,
    string? SourceObjectId = null,
    int PowerDelta = 0,
    int BasePower = 0,
    int EffectivePower = 0,
    string EffectKind = "",
    string? SourceCardNo = null,
    string SourcePath = "",
    bool IsLayerEngineFoundationOnly = false,
    IReadOnlyList<string>? DeferredLayerEngineResiduals = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? RequestedPowerDelta = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? AppliedPowerDelta = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? MinimumPower = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? ResultingPower = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? AppliedOrder = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Condition = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Lifecycle = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? ParticipantObjectIds = null,
    int Sequence = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? SourceDependencyObjectIds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? TargetDependencyObjectIds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? ParticipantDependencyObjectIds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? SourceOrder = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? SnapshotMight = null);

public sealed record PowerModifierLedgerEntry
{
    [JsonConstructor]
    public PowerModifierLedgerEntry(
        string? effectId = null,
        string? effectKind = null,
        string? duration = null,
        string? targetObjectId = null,
        string? sourceObjectId = null,
        string? sourceCardNo = null,
        int powerDelta = 0,
        int basePower = 0,
        int effectivePower = 0,
        string? sourcePath = null,
        int requestedPowerDelta = int.MinValue,
        int minimumPower = 0,
        int resultingPower = int.MinValue,
        int? appliedOrder = null,
        int? snapshotMight = null)
    {
        EffectId = NormalizeText(effectId);
        EffectKind = NormalizeText(effectKind);
        Duration = string.IsNullOrWhiteSpace(duration) ? "UNTIL_END_OF_TURN" : duration.Trim();
        TargetObjectId = NormalizeOptionalText(targetObjectId);
        SourceObjectId = NormalizeOptionalText(sourceObjectId);
        SourceCardNo = NormalizeOptionalText(sourceCardNo);
        PowerDelta = powerDelta;
        BasePower = basePower;
        EffectivePower = effectivePower;
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? "CoreRuleEngine.ApplyPowerModifier" : sourcePath.Trim();
        RequestedPowerDelta = requestedPowerDelta == int.MinValue ? powerDelta : requestedPowerDelta;
        MinimumPower = Math.Max(0, minimumPower);
        ResultingPower = resultingPower == int.MinValue ? effectivePower : resultingPower;
        AppliedOrder = appliedOrder is > 0 ? appliedOrder.Value : null;
        SnapshotMight = snapshotMight;
    }

    public string EffectId { get; init; }

    public string EffectKind { get; init; }

    public string Duration { get; init; }

    public string? TargetObjectId { get; init; }

    public string? SourceObjectId { get; init; }

    public string? SourceCardNo { get; init; }

    public int PowerDelta { get; init; }

    public int BasePower { get; init; }

    public int EffectivePower { get; init; }

    public string SourcePath { get; init; }

    public int RequestedPowerDelta { get; init; }

    public int MinimumPower { get; init; }

    public int ResultingPower { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AppliedOrder { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SnapshotMight { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChosenNegativeMightContext? ChosenNegativeMight { get; init; }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record SpellExecutionState(string EffectKind, IReadOnlyList<string> TargetObjectIds, IReadOnlyDictionary<string, long> TargetGenerations, IReadOnlyList<string>? MovementDestinations = null, string Destination = "",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MovementDeclarationPlayerId = null);

public sealed record TargetedPowerAbilityContext(string AbilityId, string SourceObjectId, long SourceGeneration,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? LastKnownSourceEmpowered = null);

public sealed record PendingPaymentState
{
    [JsonConstructor]
    public PendingPaymentState(
        string? paymentId = null,
        string? paymentWindow = null,
        string? playerId = null,
        int manaCost = 0,
        int powerCost = 0,
        IReadOnlyDictionary<string, int>? powerCostByTrait = null,
        IReadOnlyList<string>? legalPaymentChoiceIds = null,
        string? reason = null,
        IReadOnlyList<string>? paymentResourceActionIds = null)
    {
        PaymentId = Normalize(paymentId);
        PaymentWindow = Normalize(paymentWindow);
        PlayerId = Normalize(playerId);
        ManaCost = Math.Max(0, manaCost);
        PowerCost = Math.Max(0, powerCost);
        PowerCostByTrait = PaymentCostRules.NormalizePowerCostByTrait(
            powerCostByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
        LegalPaymentChoiceIds = NormalizeList(legalPaymentChoiceIds);
        Reason = Normalize(reason);
        PaymentResourceActionIds = NormalizeList(paymentResourceActionIds);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolvingStackItemId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ExperienceCost { get; init; }

    public string PaymentId { get; init; }

    public string PaymentWindow { get; init; }

    public string PlayerId { get; init; }

    public int ManaCost { get; init; }

    public int PowerCost { get; init; }

    public IReadOnlyDictionary<string, int> PowerCostByTrait { get; init; }

    public IReadOnlyList<string> LegalPaymentChoiceIds { get; init; }

    public string Reason { get; init; }

    public IReadOnlyList<string> PaymentResourceActionIds { get; init; }

    internal Dictionary<string, object?> CostView()
    {
        var view = new Dictionary<string, object?> { ["mana"] = ManaCost, ["power"] = PowerCost, ["powerByTrait"] = PowerCostByTrait };
        if (ExperienceCost > 0) view["experience"] = ExperienceCost;
        return view;
    }

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

public sealed record PendingCardChoiceState
{
    [JsonConstructor]
    public PendingCardChoiceState(
        string? choiceId = null,
        string? choiceWindow = null,
        string? playerId = null,
        int requiredCount = 0,
        int maxCount = 0,
        IReadOnlyList<string>? legalObjectIds = null,
        IReadOnlyList<string>? contextObjectIds = null,
        string? reason = null,
        string? sourceObjectId = null,
        string? effectKind = null)
    {
        ChoiceId = Normalize(choiceId);
        ChoiceWindow = Normalize(choiceWindow);
        PlayerId = Normalize(playerId);
        RequiredCount = Math.Max(0, requiredCount);
        MaxCount = Math.Max(RequiredCount, maxCount);
        LegalObjectIds = NormalizeList(legalObjectIds);
        ContextObjectIds = NormalizeList(contextObjectIds);
        Reason = Normalize(reason);
        SourceObjectId = Normalize(sourceObjectId);
        EffectKind = Normalize(effectKind);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolvingStackItemId { get; init; }


    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeckChoiceContext? DeckContext { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TurnBattlefieldConquests? BattlefieldConquests { get; init; }
    public RevealedHandChoiceContext? HandContext { get; init; }

    public string ChoiceId { get; init; }

    public string ChoiceWindow { get; init; }

    public string PlayerId { get; init; }

    public int RequiredCount { get; init; }

    public int MaxCount { get; init; }

    public IReadOnlyList<string> LegalObjectIds { get; init; }

    public IReadOnlyList<string> ContextObjectIds { get; init; }

    public string Reason { get; init; }

    public string SourceObjectId { get; init; }

    public string EffectKind { get; init; }

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

public interface IMatchSession
{
    string RoomId { get; }

    PlayerSessionDto EnsurePlayer(string playerId);

    ValueTask<PlayerSessionDto> EnsurePlayerAsync(string playerId, CancellationToken cancellationToken);

    PlayerSessionDto ReconnectPlayer(string playerId, string reconnectToken);

    ValueTask<PlayerSessionDto> ReconnectPlayerAsync(
        string playerId,
        string reconnectToken,
        CancellationToken cancellationToken);

    SnapshotDto SnapshotFor(string playerId);

    ActionPromptDto PromptFor(string playerId);

    ValueTask<PlayCostQuoteDto> PreviewPlayCardAsync(string playerId, PlayCostPreviewRequestDto request, CancellationToken cancellationToken);

    ValueTask<ResolutionResult> SeedScenarioAsync(
        string playerId,
        string clientIntentId,
        string scenarioId,
        JsonElement? rawCommand,
        CancellationToken cancellationToken);

    ValueTask<ResolutionResult> ReadyAsync(
        string playerId,
        string clientIntentId,
        JsonElement? rawCommand,
        CancellationToken cancellationToken);

    ValueTask<ResolutionResult> SubmitDeckAsync(
        string playerId,
        string clientIntentId,
        SubmitDeckCommand command,
        JsonElement? rawCommand,
        CancellationToken cancellationToken);

    ValueTask<ResolutionResult> SubmitAsync(
        string playerId,
        string clientIntentId,
        GameCommand command,
        JsonElement? rawCommand,
        CancellationToken cancellationToken);
}

public interface IMatchSessionRegistry
{
    ValueTask<IMatchSession?> FindAsync(string roomId, CancellationToken cancellationToken);

    ValueTask<IMatchSession> GetOrCreateAsync(string roomId, CancellationToken cancellationToken);
}

public sealed record MatchSessionOptions(
    bool AllowLegacyReadyWithoutDeck = true,
    OfficialDeckFormat DeckFormat = OfficialDeckFormat.CoreRules)
{
    public static MatchSessionOptions Default { get; } = new();
}

public sealed class InMemoryMatchSessionRegistry : IMatchSessionRegistry
{
    private readonly IRuleEngine ruleEngine;
    private readonly IMatchJournal journal;
    private readonly IMatchRecoveryStore recoveryStore;
    private readonly IMatchPlayerStore playerStore;
    private readonly MatchSessionOptions sessionOptions;
    private readonly Dictionary<string, IMatchSession> sessions = new();
    private readonly SemaphoreSlim gate = new(1, 1);

    public InMemoryMatchSessionRegistry(IRuleEngine ruleEngine, IMatchJournal journal)
        : this(ruleEngine, journal, NoopMatchRecoveryStore.Instance, NoopMatchPlayerStore.Instance)
    {
    }

    public InMemoryMatchSessionRegistry(
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchRecoveryStore recoveryStore)
        : this(ruleEngine, journal, recoveryStore, NoopMatchPlayerStore.Instance)
    {
    }

    public InMemoryMatchSessionRegistry(
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchRecoveryStore recoveryStore,
        IMatchPlayerStore playerStore)
        : this(ruleEngine, journal, recoveryStore, playerStore, MatchSessionOptions.Default)
    {
    }

    public InMemoryMatchSessionRegistry(
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchRecoveryStore recoveryStore,
        IMatchPlayerStore playerStore,
        MatchSessionOptions sessionOptions)
    {
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        this.recoveryStore = recoveryStore;
        this.playerStore = playerStore;
        this.sessionOptions = sessionOptions ?? MatchSessionOptions.Default;
    }

    public async ValueTask<IMatchSession?> FindAsync(string roomId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return sessions.GetValueOrDefault(roomId); }
        finally { gate.Release(); }
    }

    public async ValueTask<IMatchSession> GetOrCreateAsync(string roomId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!sessions.TryGetValue(roomId, out var session))
            {
                session = await CreateSessionAsync(roomId, cancellationToken).ConfigureAwait(false);
                sessions.Add(roomId, session);
            }

            return session;
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<IMatchSession> CreateSessionAsync(string roomId, CancellationToken cancellationToken)
    {
        var recovery = await recoveryStore.LoadAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (recovery is null)
        {
            return new MatchSession(roomId, ruleEngine, journal, playerStore, sessionOptions);
        }

        if (!string.Equals(recovery.RoomId, roomId, StringComparison.Ordinal))
        {
            throw new MatchSessionException(
                ErrorCodes.RecoveryInconsistent,
                $"match recovery returned room {recovery.RoomId} for requested room {roomId}");
        }

        var validationErrors = MatchRecoveryValidator.Validate(
            recovery.RoomId,
            recovery.LastEventSequence,
            recovery.Commands,
            recovery.Events,
            recovery.PlayerViews,
            recovery.AuthoritativeState,
            recovery.CurrentTick,
            recovery.SpectatorReplayFrame);
        if (validationErrors.Count > 0)
        {
            throw new MatchSessionException(
                ErrorCodes.RecoveryInconsistent,
                $"match recovery validation failed: {string.Join("; ", validationErrors)}");
        }

        if (recovery.IsConsistent)
        {
            var replayErrors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(
                    recovery,
                    ruleEngine,
                    cancellationToken)
                .ConfigureAwait(false);
            if (replayErrors.Count > 0)
            {
                throw new MatchSessionException(
                    ErrorCodes.RecoveryInconsistent,
                    $"match recovery action-log audit failed: {string.Join("; ", replayErrors)}");
            }
        }

        return MatchSession.Restore(recovery, ruleEngine, journal, playerStore, sessionOptions);
    }
}

public sealed class MatchSessionException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
