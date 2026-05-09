using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public interface IRuleEngine
{
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

    public static string SpellDuelCompleted(string battlefieldObjectId)
    {
        return $"{SpellDuelCompletedPrefix}{battlefieldObjectId}";
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
        IReadOnlyDictionary<string, int>? powerByTrait = null)
    {
        Mana = Math.Max(0, mana);
        Power = Math.Max(0, power);
        PowerByTrait = NormalizePowerByTrait(powerByTrait);
    }

    public static RunePool Empty { get; } = new(0, 0);

    public int Mana { get; init; }

    public int Power { get; init; }

    public IReadOnlyDictionary<string, int> PowerByTrait { get; init; }

    [JsonIgnore]
    public int TotalPower => Power + PowerByTrait.Values.Sum();

    public bool Equals(RunePool? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && Mana == other.Mana
            && Power == other.Power
            && PowerByTrait.Count == other.PowerByTrait.Count
            && PowerByTrait.All(entry =>
                other.PowerByTrait.TryGetValue(entry.Key, out var otherValue)
                && entry.Value == otherValue);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Mana);
        hash.Add(Power);
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
    IReadOnlyList<string> RelatedEventKinds);

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
    IReadOnlyList<string> RelatedEventKinds);

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
    string? FocusPlayerId,
    IReadOnlyList<string> PassedFocusPlayerIds,
    IReadOnlyList<string> StackItemIds,
    IReadOnlyList<string> StackControllerIds);

public sealed record BattleState(
    bool IsActive,
    string? BattlefieldObjectId,
    IReadOnlyList<string> AttackerObjectIds,
    IReadOnlyList<string> DefenderObjectIds,
    IReadOnlyDictionary<string, string> ParticipantControllerIds);

public static class ContinuousEffectLayers
{
    public const string PowerModifier = "POWER_MODIFIER";
    public const string RuleText = "RULE_TEXT";
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
    int EffectivePower = 0);

public sealed record CardObjectState
{
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
        string? controllerId = null)
    {
        ObjectId = string.IsNullOrWhiteSpace(objectId) ? string.Empty : objectId.Trim();
        Damage = Math.Max(0, damage);
        UntilEndOfTurnEffects = NormalizeEffects(untilEndOfTurnEffects);
        IsFaceDown = isFaceDown;
        IsAttacking = isAttacking;
        IsDefending = isDefending;
        Power = Math.Max(0, power);
        UntilEndOfTurnPowerModifier = untilEndOfTurnPowerModifier;
        IsExhausted = isExhausted;
        Tags = NormalizeTags(tags);
        ManaCost = Math.Max(0, manaCost);
        AttachedToObjectId = NormalizeOptionalText(attachedToObjectId);
        CardNo = NormalizeOptionalText(cardNo);
        OwnerId = NormalizeOptionalText(ownerId);
        ControllerId = NormalizeOptionalText(controllerId);
    }

    public string ObjectId { get; init; }

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

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record StackItemState
{
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
        string? timingContext = null)
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
    }

    public string StackItemId { get; init; }

    public string ControllerId { get; init; }

    public string SourceObjectId { get; init; }

    public string EffectKind { get; init; }

    public string CardNo { get; init; }

    public IReadOnlyList<string> TargetObjectIds { get; init; }

    public int DamageAmount { get; init; }

    public int EffectRepeatCount { get; init; }

    public IReadOnlyList<string> OptionalCosts { get; init; }

    public bool PlayedAfterAnotherCardThisTurn { get; init; }

    public string Destination { get; init; }

    public string TimingContext { get; init; }

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
    [JsonConstructor]
    public TriggerQueueItemState(
        string? triggerId = null,
        string? controllerId = null,
        string? sourceObjectId = null,
        string? effectKind = null,
        string? triggeredByEventKind = null)
    {
        TriggerId = Normalize(triggerId);
        ControllerId = Normalize(controllerId);
        SourceObjectId = Normalize(sourceObjectId);
        EffectKind = Normalize(effectKind);
        TriggeredByEventKind = Normalize(triggeredByEventKind);
    }

    public string TriggerId { get; init; }

    public string ControllerId { get; init; }

    public string SourceObjectId { get; init; }

    public string EffectKind { get; init; }

    public string TriggeredByEventKind { get; init; }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed record MatchState
{
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
        IReadOnlyList<BattleResolutionState>? battleResolutions = null)
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

    public PendingTaskQueueState PendingTaskQueue => BuildPendingTaskQueue(this);

    public IReadOnlyList<ContinuousEffectState> ContinuousEffects => BuildContinuousEffectStates(this);

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

    public static MatchState Create(string roomId)
    {
        return new MatchState(
            roomId,
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal),
            MatchStatuses.Empty,
            [],
            "P1",
            MatchPhases.Room,
            TimingStates.Room,
            new Dictionary<string, RunePool>(StringComparer.Ordinal),
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal),
            null,
            [],
            [],
            null,
            [],
            null);
    }

    private static string InferStatus(IReadOnlyDictionary<string, string> seats)
    {
        return seats.Count == 0 ? MatchStatuses.Empty : MatchStatuses.InProgress;
    }

    private static string InferPhase(string status)
    {
        return string.Equals(status, MatchStatuses.InProgress, StringComparison.Ordinal)
            ? MatchPhases.Main
            : MatchPhases.Room;
    }

    private static string InferTimingState(string status)
    {
        return string.Equals(status, MatchStatuses.InProgress, StringComparison.Ordinal)
            ? TimingStates.NeutralOpen
            : TimingStates.Room;
    }

    private static IReadOnlyList<string> NormalizeReadyPlayers(IReadOnlyList<string>? readyPlayerIds)
    {
        return (readyPlayerIds ?? [])
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Select(playerId => playerId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, RunePool> NormalizeRunePools(
        IReadOnlyDictionary<string, RunePool>? runePools)
    {
        return (runePools ?? new Dictionary<string, RunePool>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => entry.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, PlayerZones> NormalizePlayerZones(
        IReadOnlyDictionary<string, PlayerZones>? playerZones)
    {
        return (playerZones ?? new Dictionary<string, PlayerZones>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => NormalizeZones(entry.Value),
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> NormalizeObjectLocations(
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations)
    {
        return (objectLocations ?? new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value is not null)
            .Select(entry => new KeyValuePair<string, ObjectLocationState>(
                entry.Key.Trim(),
                ObjectLocationState.Normalize(entry.Value)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value.PlayerId)
                && !string.IsNullOrWhiteSpace(entry.Value.Zone))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private static TurnWindowState BuildTurnWindowState(MatchState state)
    {
        var hasStack = state.StackItems.Count > 0;
        var isSpellDuel = IsSpellDuelTimingState(state.TimingState)
            || !string.IsNullOrWhiteSpace(state.FocusPlayerId)
            || state.StackItems.Any(item => string.Equals(item.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal));
        var isClosed = hasStack || string.Equals(state.TimingState, TimingStates.NeutralClosed, StringComparison.Ordinal)
            || string.Equals(state.TimingState, TimingStates.SpellDuelClosed, StringComparison.Ordinal);
        var windowState = isSpellDuel
            ? isClosed ? TimingStates.SpellDuelClosed : TimingStates.SpellDuelOpen
            : isClosed ? TimingStates.NeutralClosed : TimingStates.NeutralOpen;
        var actingPlayerId = isSpellDuel
            ? state.FocusPlayerId
            : hasStack
                ? state.PriorityPlayerId
                : state.ActivePlayerId;

        return new TurnWindowState(windowState, isSpellDuel, isClosed, hasStack, actingPlayerId);
    }

    private static SpellDuelState BuildSpellDuelState(MatchState state)
    {
        var isActive = IsSpellDuelTimingState(state.TimingState)
            || !string.IsNullOrWhiteSpace(state.FocusPlayerId)
            || state.StackItems.Any(item => string.Equals(item.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal));
        if (!isActive)
        {
            return new SpellDuelState(false, false, null, [], [], []);
        }

        var stackItems = state.StackItems
            .Where(item => string.Equals(item.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || IsSpellDuelTimingState(state.TimingState))
            .ToArray();
        return new SpellDuelState(
            true,
            state.TurnWindow.IsClosed,
            state.FocusPlayerId,
            state.PassedFocusPlayerIds,
            stackItems.Select(item => item.StackItemId).ToArray(),
            stackItems
                .Select(item => item.ControllerId)
                .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray());
    }

    private static BattleState BuildBattleState(MatchState state)
    {
        var attackerObjectIds = state.CardObjects
            .Where(entry => entry.Value.IsAttacking
                && entry.Value.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out _))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        var defenderObjectIds = state.CardObjects
            .Where(entry => entry.Value.IsDefending
                && entry.Value.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out _))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        var participantObjectIds = attackerObjectIds
            .Concat(defenderObjectIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var participantControllerIds = participantObjectIds
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && !string.IsNullOrWhiteSpace(EffectiveFieldControllerId(state, objectId, cardObject)))
            .ToDictionary(
                objectId => objectId,
                objectId => EffectiveFieldControllerId(state, objectId, state.CardObjects[objectId]),
                StringComparer.Ordinal);
        var battlefieldObjectIds = participantObjectIds
            .Select(objectId => state.ObjectLocations.TryGetValue(objectId, out var location)
                ? location.BattlefieldObjectId
                : null)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new BattleState(
            participantObjectIds.Length > 0,
            battlefieldObjectIds.Length == 1 ? battlefieldObjectIds[0] : null,
            attackerObjectIds,
            defenderObjectIds,
            participantControllerIds);
    }

    private static bool IsSpellDuelTimingState(string timingState)
    {
        return string.Equals(timingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            || string.Equals(timingState, TimingStates.SpellDuelClosed, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, BattlefieldState> BuildBattlefieldStates(MatchState state)
    {
        var result = new Dictionary<string, BattlefieldState>(StringComparer.Ordinal);
        foreach (var (zonePlayerId, zones) in state.PlayerZones
            .OrderBy(entry => state.Seats.TryGetValue(entry.Key, out var seat) ? seat : entry.Key, StringComparer.Ordinal))
        {
            foreach (var battlefieldObjectId in zones.Battlefields
                .Where(objectId => IsBattlefieldCardStateObject(state.CardObjects, objectId)))
            {
                var battlefieldObject = state.CardObjects.TryGetValue(battlefieldObjectId, out var knownBattlefieldObject)
                    ? knownBattlefieldObject
                    : new CardObjectState(battlefieldObjectId);
                var battlefieldRelatedObjectIds = state.ObjectLocations
                    .Where(entry => string.Equals(entry.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                        && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                        && !string.Equals(entry.Key, battlefieldObjectId, StringComparison.Ordinal)
                        && TryFindFieldObjectLocation(state.PlayerZones, entry.Key, out _)
                        && state.CardObjects.ContainsKey(entry.Key))
                    .Select(entry => entry.Key)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(objectId => objectId, StringComparer.Ordinal)
                    .ToArray();
                var standbyObjectIds = battlefieldRelatedObjectIds
                    .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                        && (cardObject.IsFaceDown || cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)))
                    .ToArray();
                var occupantObjectIds = battlefieldRelatedObjectIds
                    .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                        && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                        && !cardObject.IsFaceDown
                        && !cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal))
                    .ToArray();
                var occupantControllerIds = occupantObjectIds
                    .Select(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                        ? EffectiveFieldControllerId(state, objectId, cardObject)
                        : string.Empty)
                    .Where(controllerId => !string.IsNullOrWhiteSpace(controllerId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
                    .ToArray();
                var contested = occupantControllerIds.Length > 1;
                var effectiveBattlefieldControllerId = EffectiveOwnedControllerId(battlefieldObject);
                var controllerId = string.IsNullOrWhiteSpace(effectiveBattlefieldControllerId)
                    ? null
                    : effectiveBattlefieldControllerId;

                result[battlefieldObjectId] = new BattlefieldState(
                    battlefieldObjectId,
                    zonePlayerId,
                    battlefieldObject.CardNo,
                    controllerId,
                    contested ? "CONTESTED" : controllerId is null ? "UNCONTROLLED" : "CONTROLLED",
                    contested,
                    occupantObjectIds,
                    occupantControllerIds,
                    standbyObjectIds,
                    standbyObjectIds.Count(objectId =>
                        state.CardObjects.TryGetValue(objectId, out var cardObject) && cardObject.IsFaceDown));
            }
        }

        return result;
    }

    private static IReadOnlyList<CleanupTaskState> BuildPendingCleanupTasks(MatchState state)
    {
        var tasks = new List<CleanupTaskState>();
        foreach (var (objectId, cardObject) in state.CardObjects
            .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                && string.IsNullOrWhiteSpace(cardObject.AttachedToObjectId)
                && state.ObjectLocations.TryGetValue(objectId, out var equipmentLocation)
                && string.Equals(equipmentLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(equipmentLocation.BattlefieldObjectId)
                && TryFindFieldObjectLocation(state.PlayerZones, objectId, out _)
                && !cardObject.IsFaceDown
                && !cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal))
            {
                tasks.Add(new CleanupTaskState(
                    $"cleanup:unattached-equipment:{equipmentLocation.BattlefieldObjectId}:{objectId}",
                    "RECALL_UNATTACHED_EQUIPMENT",
                    "UNATTACHED_EQUIPMENT_CLEANUP",
                    EffectiveFieldControllerId(state, objectId, cardObject),
                    objectId,
                    equipmentLocation.BattlefieldObjectId));
            }

            if (!cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                || !TryFindFieldObjectLocation(state.PlayerZones, objectId, out var location))
            {
                continue;
            }

            if (cardObject.Power > 0 && cardObject.Damage < cardObject.Power)
            {
                continue;
            }

            var isZeroPower = IsZeroPowerCleanupCandidate(cardObject);
            if (cardObject.Power <= 0 && !isZeroPower)
            {
                continue;
            }

            tasks.Add(new CleanupTaskState(
                isZeroPower ? $"cleanup:zero-power:{objectId}" : $"cleanup:lethal:{objectId}",
                isZeroPower ? "DESTROY_ZERO_POWER_UNIT" : "DESTROY_LETHAL_UNIT",
                isZeroPower ? "ZERO_POWER" : "LETHAL_DAMAGE",
                location.PlayerId,
                objectId,
                state.ObjectLocations.TryGetValue(objectId, out var objectLocation)
                    ? objectLocation.BattlefieldObjectId
                    : null));
        }

        foreach (var battlefield in state.BattlefieldStates.Values
            .OrderBy(battlefield => battlefield.BattlefieldObjectId, StringComparer.Ordinal))
        {
            foreach (var standbyObjectId in battlefield.StandbyObjectIds
                .OrderBy(objectId => objectId, StringComparer.Ordinal))
            {
                if (!IsIllegalBattlefieldStandby(state, battlefield, standbyObjectId, out var location))
                {
                    continue;
                }

                tasks.Add(new CleanupTaskState(
                    $"cleanup:illegal-standby:{battlefield.BattlefieldObjectId}:{standbyObjectId}",
                    "REMOVE_ILLEGAL_STANDBY",
                    "BATTLEFIELD_CONTROL_CLEANUP",
                    location.PlayerId,
                    standbyObjectId,
                    battlefield.BattlefieldObjectId));
            }
        }

        foreach (var battlefield in state.BattlefieldStates.Values.Where(battlefield => battlefield.Contested))
        {
            tasks.Add(new CleanupTaskState(
                $"cleanup:battlefield-contested:{battlefield.BattlefieldObjectId}",
                "BATTLEFIELD_CONTESTED",
                "BATTLEFIELD_CONTROL_CHECK",
                battlefield.ZonePlayerId,
                null,
                battlefield.BattlefieldObjectId));
            tasks.Add(new CleanupTaskState(
                $"task:start-spell-duel:{battlefield.BattlefieldObjectId}",
                "START_SPELL_DUEL",
                "BATTLEFIELD_CONTESTED",
                battlefield.ZonePlayerId,
                null,
                battlefield.BattlefieldObjectId));
            tasks.Add(new CleanupTaskState(
                $"task:start-battle:{battlefield.BattlefieldObjectId}",
                "START_BATTLE",
                "SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST",
                battlefield.ZonePlayerId,
                null,
                battlefield.BattlefieldObjectId));
        }

        return tasks
            .OrderBy(task => task.TaskId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<BattlefieldTaskState> BuildBattlefieldTaskStates(MatchState state)
    {
        var tasks = new List<BattlefieldTaskState>();
        foreach (var battlefield in state.BattlefieldStates.Values
            .Where(battlefield => battlefield.Contested)
            .OrderBy(battlefield => battlefield.BattlefieldObjectId, StringComparer.Ordinal))
        {
            var participantObjectIds = battlefield.OccupantObjectIds
                .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                    && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
                .OrderBy(objectId => objectId, StringComparer.Ordinal)
                .ToArray();
            var participantControllerIds = participantObjectIds
                .Select(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                    ? EffectiveFieldControllerId(state, objectId, cardObject)
                    : string.Empty)
                .Where(controllerId => !string.IsNullOrWhiteSpace(controllerId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
                .ToArray();
            var stackItemIds = state.StackItems
                .Where(item => string.Equals(item.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                    || IsSpellDuelTimingState(state.TimingState))
                .Select(item => item.StackItemId)
                .ToArray();
            var spellDuelCompleted = HasBattlefieldSpellDuelCompleted(state, battlefield.BattlefieldObjectId);
            var spellDuelStatus = state.SpellDuelState.IsActive
                ? "ACTIVE"
                : spellDuelCompleted ? "COMPLETED" : "PENDING";
            var battleStatus = state.BattleState.IsActive
                && string.Equals(state.BattleState.BattlefieldObjectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal)
                    ? "ACTIVE"
                    : state.SpellDuelState.IsActive ? "WAITING_FOR_SPELL_DUEL" : "PENDING";

            tasks.Add(new BattlefieldTaskState(
                $"task:start-spell-duel:{battlefield.BattlefieldObjectId}",
                "START_SPELL_DUEL",
                spellDuelStatus,
                "BATTLEFIELD_CONTESTED",
                battlefield.BattlefieldObjectId,
                participantControllerIds,
                participantObjectIds,
                state.SpellDuelState.FocusPlayerId,
                stackItemIds));
            tasks.Add(new BattlefieldTaskState(
                $"task:start-battle:{battlefield.BattlefieldObjectId}",
                "START_BATTLE",
                battleStatus,
                "SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST",
                battlefield.BattlefieldObjectId,
                participantControllerIds,
                participantObjectIds,
                state.BattleState.IsActive ? state.ActivePlayerId : null,
                []));
        }

        return tasks;
    }

    private static string EffectiveFieldControllerId(
        MatchState state,
        string objectId,
        CardObjectState cardObject)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return cardObject.ControllerId;
        }

        if (!string.IsNullOrWhiteSpace(cardObject.OwnerId))
        {
            return cardObject.OwnerId;
        }

        return TryFindFieldObjectLocation(state.PlayerZones, objectId, out var location)
            ? location.PlayerId
            : string.Empty;
    }

    private static string EffectiveOwnedControllerId(CardObjectState cardObject)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return cardObject.ControllerId;
        }

        return cardObject.OwnerId ?? string.Empty;
    }

    private static bool HasOwnerOrControllerIdentity(CardObjectState cardObject)
    {
        return !string.IsNullOrWhiteSpace(cardObject.OwnerId)
            || !string.IsNullOrWhiteSpace(cardObject.ControllerId);
    }

    private static bool IsZeroPowerCleanupCandidate(CardObjectState cardObject)
    {
        return cardObject.Power <= 0
            && !cardObject.IsFaceDown
            && !cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            && HasOwnerOrControllerIdentity(cardObject);
    }

    private static PendingTaskQueueState BuildPendingTaskQueue(MatchState state)
    {
        var tasks = state.PendingCleanupTasks
            .OrderBy(task => PendingTaskSortKey(task.Kind))
            .ThenBy(task => task.TaskId, StringComparer.Ordinal)
            .ToArray();
        var activeTask = SelectActivePendingTask(state, tasks);
        var phase = PendingTaskQueuePhase(state, activeTask);

        return new PendingTaskQueueState(
            tasks.Length > 0,
            tasks.Length > 0,
            phase,
            activeTask?.TaskId,
            tasks);
    }

    private static CleanupTaskState? SelectActivePendingTask(MatchState state, IReadOnlyList<CleanupTaskState> tasks)
    {
        if (tasks.Count == 0)
        {
            return null;
        }

        var stateBasedTask = tasks.FirstOrDefault(task => IsStateBasedCleanupTask(task.Kind));
        if (stateBasedTask is not null)
        {
            return stateBasedTask;
        }

        if (state.BattleState.IsActive)
        {
            var activeBattleTask = tasks.FirstOrDefault(task =>
                string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(state.BattleState.BattlefieldObjectId)
                    || string.Equals(task.BattlefieldObjectId, state.BattleState.BattlefieldObjectId, StringComparison.Ordinal)));
            if (activeBattleTask is not null)
            {
                return activeBattleTask;
            }
        }

        var completedBattlefieldSpellDuelTask = tasks.FirstOrDefault(task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(task.BattlefieldObjectId)
            && HasBattlefieldSpellDuelCompleted(state, task.BattlefieldObjectId));
        if (completedBattlefieldSpellDuelTask is not null)
        {
            return completedBattlefieldSpellDuelTask;
        }

        if (state.SpellDuelState.IsActive)
        {
            var activeSpellDuelTask = tasks.FirstOrDefault(task =>
                string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal));
            if (activeSpellDuelTask is not null)
            {
                return activeSpellDuelTask;
            }
        }

        return tasks[0];
    }

    private static string PendingTaskQueuePhase(MatchState state, CleanupTaskState? activeTask)
    {
        if (activeTask is null)
        {
            return "IDLE";
        }

        if (IsStateBasedCleanupTask(activeTask.Kind))
        {
            return "STATE_BASED_CLEANUP";
        }

        if (string.Equals(activeTask.Kind, "START_BATTLE", StringComparison.Ordinal))
        {
            return "BATTLE_TASKS";
        }

        if (string.Equals(activeTask.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
            && state.SpellDuelState.IsActive)
        {
            return "SPELL_DUEL_TASKS";
        }

        return "BATTLEFIELD_TASKS";
    }

    private static int PendingTaskSortKey(string kind)
    {
        return kind switch
        {
            "DESTROY_LETHAL_UNIT" => 0,
            "DESTROY_ZERO_POWER_UNIT" => 1,
            "REMOVE_ILLEGAL_STANDBY" => 2,
            "RECALL_UNATTACHED_EQUIPMENT" => 3,
            "BATTLEFIELD_CONTESTED" => 10,
            "START_SPELL_DUEL" => 20,
            "START_BATTLE" => 30,
            _ => 100
        };
    }

    private static bool IsStateBasedCleanupTask(string kind)
    {
        return string.Equals(kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "DESTROY_ZERO_POWER_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
            || string.Equals(kind, "RECALL_UNATTACHED_EQUIPMENT", StringComparison.Ordinal);
    }

    private static bool IsIllegalBattlefieldStandby(
        MatchState state,
        BattlefieldState battlefield,
        string objectId,
        out ObjectLocationState location)
    {
        if (!state.ObjectLocations.TryGetValue(objectId, out var objectLocation))
        {
            location = new ObjectLocationState(string.Empty, string.Empty);
            return false;
        }

        location = objectLocation;
        return string.Equals(objectLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(location.BattlefieldObjectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal)
            && !string.Equals(objectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal)
            && TryFindFieldObjectLocation(state.PlayerZones, objectId, out _)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && (cardObject.IsFaceDown || cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal))
            && !string.Equals(
                EffectiveFieldControllerId(state, objectId, cardObject),
                battlefield.ControllerId,
                StringComparison.Ordinal);
    }

    private static bool HasBattlefieldSpellDuelCompleted(MatchState state, string battlefieldObjectId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            BattlefieldTaskMarkers.SpellDuelCompleted(battlefieldObjectId),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<ContinuousEffectState> BuildContinuousEffectStates(MatchState state)
    {
        var effects = new List<ContinuousEffectState>();
        foreach (var effectId in state.UntilEndOfTurnEffects.OrderBy(effectId => effectId, StringComparer.Ordinal))
        {
            effects.Add(new ContinuousEffectState(
                $"GLOBAL:{effectId}",
                "GLOBAL",
                ContinuousEffectLayers.RuleText,
                "UNTIL_END_OF_TURN"));
        }

        foreach (var entry in state.CardObjects.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var objectId = entry.Key;
            var cardObject = entry.Value;
            if (cardObject.UntilEndOfTurnPowerModifier != 0)
            {
                effects.Add(new ContinuousEffectState(
                    $"POWER:{objectId}:{cardObject.UntilEndOfTurnPowerModifier}",
                    "OBJECT",
                    ContinuousEffectLayers.PowerModifier,
                    "UNTIL_END_OF_TURN",
                    objectId,
                    null,
                    cardObject.UntilEndOfTurnPowerModifier,
                    ResolveBasePower(cardObject),
                    cardObject.Power));
            }

            foreach (var effectId in cardObject.UntilEndOfTurnEffects.OrderBy(effectId => effectId, StringComparer.Ordinal))
            {
                effects.Add(new ContinuousEffectState(
                    $"OBJECT:{objectId}:{effectId}",
                    "OBJECT",
                    ContinuousEffectLayers.RuleText,
                    "UNTIL_END_OF_TURN",
                    objectId,
                    null,
                    0,
                    ResolveBasePower(cardObject),
                    cardObject.Power));
            }
        }

        return effects
            .OrderBy(effect => effect.Scope, StringComparer.Ordinal)
            .ThenBy(effect => effect.TargetObjectId, StringComparer.Ordinal)
            .ThenBy(effect => effect.Layer, StringComparer.Ordinal)
            .ThenBy(effect => effect.EffectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static int ResolveBasePower(CardObjectState cardObject)
    {
        return Math.Max(0, cardObject.Power - cardObject.UntilEndOfTurnPowerModifier);
    }

    private static bool IsBattlefieldCardStateObject(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId)
    {
        return cardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal);
    }

    private static bool TryFindFieldObjectLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out (string PlayerId, string Zone) location)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BASE");
                return true;
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BATTLEFIELD");
                return true;
            }
        }

        location = default;
        return false;
    }

    private static IReadOnlyDictionary<string, int> NormalizePlayerScores(
        IReadOnlyDictionary<string, int>? playerScores)
    {
        return (playerScores ?? new Dictionary<string, int>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => entry.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> NormalizePlayerExperience(
        IReadOnlyDictionary<string, int>? playerExperience)
    {
        return (playerExperience ?? new Dictionary<string, int>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => entry.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> NormalizePlayerCardsPlayedThisTurn(
        IReadOnlyDictionary<string, int>? playerCardsPlayedThisTurn)
    {
        return (playerCardsPlayedThisTurn ?? new Dictionary<string, int>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value > 0)
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => entry.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, OfficialDecklist> NormalizeOfficialDecklists(
        IReadOnlyDictionary<string, OfficialDecklist>? decklists)
    {
        return (decklists ?? new Dictionary<string, OfficialDecklist>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value is not null)
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => OfficialDeckValidator.Normalize(entry.Value),
                StringComparer.Ordinal);
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
            state.ControllerId);
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
                item.TimingContext))
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
                item.TriggeredByEventKind))
            .ToArray();
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
                NormalizeTextList(resolution.ParticipantObjectIds),
                NormalizeTextList(resolution.RelatedEventKinds)))
            .Where(resolution => !string.IsNullOrWhiteSpace(resolution.BattlefieldObjectId))
            .Take(12)
            .ToArray();
    }

    private static IReadOnlyList<BattleResolutionState> NormalizeBattleResolutions(
        IReadOnlyList<BattleResolutionState>? battleResolutions)
    {
        return (battleResolutions ?? [])
            .Where(resolution => !string.IsNullOrWhiteSpace(resolution.ResolutionId))
            .Select(resolution => new BattleResolutionState(
                resolution.ResolutionId.Trim(),
                resolution.Tick,
                string.IsNullOrWhiteSpace(resolution.Kind) ? string.Empty : resolution.Kind.Trim(),
                string.IsNullOrWhiteSpace(resolution.Reason) ? string.Empty : resolution.Reason.Trim(),
                string.IsNullOrWhiteSpace(resolution.BattlefieldId) ? string.Empty : resolution.BattlefieldId.Trim(),
                NormalizeOptionalText(resolution.AttackingPlayerId),
                NormalizeOptionalText(resolution.DefendingPlayerId),
                NormalizeOptionalText(resolution.WinnerPlayerId),
                NormalizeTextList(resolution.AttackerObjectIds),
                NormalizeTextList(resolution.DefenderObjectIds),
                NormalizeTextList(resolution.SurvivingAttackerObjectIds),
                NormalizeTextList(resolution.SurvivingDefenderObjectIds),
                NormalizeTextList(resolution.DestroyedObjectIds),
                NormalizeTextList(resolution.RelatedEventKinds)))
            .Take(12)
            .ToArray();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyList<string> NormalizeTextList(IReadOnlyList<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static PlayerZones NormalizeZones(PlayerZones zones)
    {
        return new PlayerZones(
            NormalizeZone(zones.MainDeck),
            NormalizeZone(zones.RuneDeck),
            NormalizeZone(zones.Hand),
            NormalizeZone(zones.Base),
            NormalizeZone(zones.Battlefields),
            NormalizeZone(zones.Graveyard),
            NormalizeZone(zones.Banished),
            NormalizeZone(zones.LegendZone),
            NormalizeZone(zones.ChampionZone));
    }

    private static IReadOnlyList<string> NormalizeZone(IReadOnlyList<string>? zone)
    {
        return (zone ?? [])
            .Where(cardId => !string.IsNullOrWhiteSpace(cardId))
            .Select(cardId => cardId.Trim())
            .ToArray();
    }
}

public sealed record ResolutionResult(
    bool Accepted,
    string? ErrorMessage,
    MatchState State,
    IReadOnlyList<GameEvent> Events,
    IReadOnlyDictionary<string, SnapshotDto> Snapshots,
    IReadOnlyDictionary<string, ActionPromptDto> Prompts,
    string? ErrorCode = null)
{
    private const int BaseWinningScore = 8;
    private const string BattlefieldIncreaseWinningScoreCardNo = "OGN·276/298";
    private const string BattlefieldIncreaseWinningScoreAltCardNo = "OGN·276a/298";

    public static ResolutionResult Rejected(
        MatchState state,
        string error,
        string errorCode = ErrorCodes.UnsupportedCommand)
    {
        return new ResolutionResult(false, error, state, [], BuildSnapshots(state), BuildPrompts(state), errorCode);
    }

    public static bool HasBlockingPendingTaskQueue(MatchState state)
    {
        if (state.Status != MatchStatuses.InProgress
            || string.Equals(state.Phase, MatchPhases.Mulligan, StringComparison.Ordinal)
            || string.Equals(state.Phase, MatchPhases.TurnStart, StringComparison.Ordinal)
            || HasOpenStackPriority(state)
            || HasOpenSpellDuelFocus(state))
        {
            return false;
        }

        return state.PendingTaskQueue.IsBlocking;
    }

    public static CleanupTaskState? ActiveStartBattleTask(MatchState state)
    {
        return state.PendingTaskQueue.Tasks.FirstOrDefault(task =>
            string.Equals(task.TaskId, state.PendingTaskQueue.ActiveTaskId, StringComparison.Ordinal)
            && string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
    }

    public static string BlockingPendingTaskQueueReason(MatchState state)
    {
        var queue = state.PendingTaskQueue;
        var activeTask = queue.Tasks.FirstOrDefault(task => string.Equals(task.TaskId, queue.ActiveTaskId, StringComparison.Ordinal))
            ?? queue.Tasks.FirstOrDefault();
        if (activeTask is null)
        {
            return "等待服务端处理任务队列";
        }

        return $"等待服务端处理任务队列：{PendingTaskKindLabel(activeTask.Kind)}";
    }

    private static string PendingTaskKindLabel(string kind)
    {
        return kind switch
        {
            "BATTLEFIELD_CONTESTED" => "战场控制检查",
            "DESTROY_LETHAL_UNIT" => "致命伤害清理",
            "DESTROY_ZERO_POWER_UNIT" => "0 战力清理",
            "REMOVE_ILLEGAL_STANDBY" => "待命清理",
            "RECALL_UNATTACHED_EQUIPMENT" => "装备清理",
            "START_BATTLE" => "开始战斗",
            "START_SPELL_DUEL" => "开始法术对决",
            _ => "服务端任务"
        };
    }

    public static IReadOnlyDictionary<string, SnapshotDto> BuildSnapshots(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            playerId => BuildSnapshotForViewer(state, playerId),
            StringComparer.Ordinal);
    }

    public static SnapshotDto BuildSpectatorSnapshot(MatchState state)
    {
        return BuildSnapshotForViewer(state, "__spectator__");
    }

    private static SnapshotDto BuildSnapshotForViewer(MatchState state, string viewerPlayerId)
    {
        var readyPlayers = state.ReadyPlayerIds.ToHashSet(StringComparer.Ordinal);
        var players = state.Seats.ToDictionary(
            entry => entry.Key,
            entry => (object?)BuildPlayerSnapshotView(state, readyPlayers, viewerPlayerId, entry.Key),
            StringComparer.Ordinal);

        return new SnapshotDto(
            state.Tick,
            state.TurnNumber,
            state.ActivePlayerId,
            players,
            BuildLaneSnapshotView(state),
            state.StackItems.Select(item => (object?)BuildStackItemSnapshotView(item)).ToArray(),
            new Dictionary<string, object?>
            {
                ["phase"] = state.Phase,
                ["timingState"] = state.TimingState,
                ["turnPlayerId"] = state.TurnPlayerId,
                ["priorityPlayerId"] = state.PriorityPlayerId,
                ["passedPriorityPlayerIds"] = state.PassedPriorityPlayerIds,
                ["focusPlayerId"] = state.FocusPlayerId,
                ["passedFocusPlayerIds"] = state.PassedFocusPlayerIds,
                ["winnerPlayerId"] = state.WinnerPlayerId,
                ["destroyedUnitOwnerIdsThisTurn"] = state.DestroyedUnitOwnerIdsThisTurn,
                ["turnWindow"] = BuildTurnWindowSnapshotView(state.TurnWindow),
                ["spellDuel"] = BuildSpellDuelSnapshotView(state.SpellDuelState),
                ["battle"] = BuildBattleSnapshotView(state.BattleState),
                ["battleResolutions"] = state.BattleResolutions.Select(BuildBattleResolutionSnapshotView).ToArray(),
                ["battlefieldTasks"] = state.BattlefieldTasks.Select(BuildBattlefieldTaskSnapshotView).ToArray(),
                ["battlefieldResolutions"] = state.BattlefieldResolutions.Select(BuildBattlefieldResolutionSnapshotView).ToArray(),
                ["pendingTaskQueue"] = BuildPendingTaskQueueSnapshotView(state.PendingTaskQueue),
                ["continuousEffects"] = state.ContinuousEffects.Select(BuildContinuousEffectSnapshotView).ToArray(),
                ["triggerQueue"] = state.TriggerQueue.Select(BuildTriggerQueueItemSnapshotView).ToArray(),
                ["winningScore"] = EffectiveWinningScore(state),
                ["roomStatus"] = state.Status,
                ["readyPlayerIds"] = state.ReadyPlayerIds
            },
            state.TimingState);
    }

    private static int EffectiveWinningScore(MatchState state)
    {
        var modifier = state.PlayerZones
            .Sum(entry => entry.Value.Battlefields.Count(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && (string.Equals(cardObject.CardNo, BattlefieldIncreaseWinningScoreCardNo, StringComparison.Ordinal)
                    || string.Equals(cardObject.CardNo, BattlefieldIncreaseWinningScoreAltCardNo, StringComparison.Ordinal))
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, entry.Key)));
        return BaseWinningScore + modifier;
    }

    private static bool SourceObjectControlledByPlayerOrLegacyOwned(CardObjectState cardObject, string playerId)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return string.Equals(cardObject.ControllerId, playerId, StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(cardObject.OwnerId)
            || string.Equals(cardObject.OwnerId, playerId, StringComparison.Ordinal);
    }

    private static Dictionary<string, object?> BuildStackItemSnapshotView(StackItemState item)
    {
        var view = new Dictionary<string, object?>
        {
            ["stackItemId"] = item.StackItemId,
            ["controllerId"] = item.ControllerId,
            ["sourceObjectId"] = item.SourceObjectId,
            ["effectKind"] = item.EffectKind,
            ["cardNo"] = item.CardNo,
            ["targetObjectIds"] = item.TargetObjectIds,
            ["damageAmount"] = item.DamageAmount
        };
        if (!string.IsNullOrWhiteSpace(item.Destination))
        {
            view["destination"] = item.Destination;
        }

        return view;
    }

    private static Dictionary<string, object?> BuildTriggerQueueItemSnapshotView(TriggerQueueItemState item)
    {
        return new Dictionary<string, object?>
        {
            ["triggerId"] = item.TriggerId,
            ["controllerId"] = item.ControllerId,
            ["sourceObjectId"] = item.SourceObjectId,
            ["effectKind"] = item.EffectKind,
            ["triggeredByEventKind"] = item.TriggeredByEventKind
        };
    }

    private static Dictionary<string, object?> BuildContinuousEffectSnapshotView(ContinuousEffectState effect)
    {
        return new Dictionary<string, object?>
        {
            ["effectId"] = effect.EffectId,
            ["scope"] = effect.Scope,
            ["layer"] = effect.Layer,
            ["duration"] = effect.Duration,
            ["targetObjectId"] = effect.TargetObjectId,
            ["sourceObjectId"] = effect.SourceObjectId,
            ["powerDelta"] = effect.PowerDelta,
            ["basePower"] = effect.BasePower,
            ["effectivePower"] = effect.EffectivePower
        };
    }

    private static Dictionary<string, object?> BuildTurnWindowSnapshotView(TurnWindowState window)
    {
        return new Dictionary<string, object?>
        {
            ["state"] = window.State,
            ["isSpellDuel"] = window.IsSpellDuel,
            ["isClosed"] = window.IsClosed,
            ["hasStack"] = window.HasStack,
            ["actingPlayerId"] = window.ActingPlayerId
        };
    }

    private static Dictionary<string, object?> BuildSpellDuelSnapshotView(SpellDuelState spellDuel)
    {
        return new Dictionary<string, object?>
        {
            ["isActive"] = spellDuel.IsActive,
            ["isClosed"] = spellDuel.IsClosed,
            ["focusPlayerId"] = spellDuel.FocusPlayerId,
            ["passedFocusPlayerIds"] = spellDuel.PassedFocusPlayerIds,
            ["stackItemIds"] = spellDuel.StackItemIds,
            ["stackControllerIds"] = spellDuel.StackControllerIds
        };
    }

    private static Dictionary<string, object?> BuildBattleSnapshotView(BattleState battle)
    {
        return new Dictionary<string, object?>
        {
            ["isActive"] = battle.IsActive,
            ["battlefieldObjectId"] = battle.BattlefieldObjectId,
            ["attackerObjectIds"] = battle.AttackerObjectIds,
            ["defenderObjectIds"] = battle.DefenderObjectIds,
            ["participantControllerIds"] = battle.ParticipantControllerIds
        };
    }

    private static Dictionary<string, object?> BuildBattlefieldTaskSnapshotView(BattlefieldTaskState task)
    {
        return new Dictionary<string, object?>
        {
            ["taskId"] = task.TaskId,
            ["kind"] = task.Kind,
            ["status"] = task.Status,
            ["reason"] = task.Reason,
            ["battlefieldObjectId"] = task.BattlefieldObjectId,
            ["participantControllerIds"] = task.ParticipantControllerIds,
            ["participantObjectIds"] = task.ParticipantObjectIds,
            ["actingPlayerId"] = task.ActingPlayerId,
            ["stackItemIds"] = task.StackItemIds
        };
    }

    private static Dictionary<string, object?> BuildBattlefieldResolutionSnapshotView(BattlefieldResolutionState resolution)
    {
        return new Dictionary<string, object?>
        {
            ["resolutionId"] = resolution.ResolutionId,
            ["tick"] = resolution.Tick,
            ["kind"] = resolution.Kind,
            ["reason"] = resolution.Reason,
            ["battlefieldObjectId"] = resolution.BattlefieldObjectId,
            ["playerId"] = resolution.PlayerId,
            ["previousControllerId"] = resolution.PreviousControllerId,
            ["controllerId"] = resolution.ControllerId,
            ["sourceObjectId"] = resolution.SourceObjectId,
            ["participantObjectIds"] = resolution.ParticipantObjectIds,
            ["relatedEventKinds"] = resolution.RelatedEventKinds
        };
    }

    private static Dictionary<string, object?> BuildBattleResolutionSnapshotView(BattleResolutionState resolution)
    {
        return new Dictionary<string, object?>
        {
            ["resolutionId"] = resolution.ResolutionId,
            ["tick"] = resolution.Tick,
            ["kind"] = resolution.Kind,
            ["reason"] = resolution.Reason,
            ["battlefieldId"] = resolution.BattlefieldId,
            ["attackingPlayerId"] = resolution.AttackingPlayerId,
            ["defendingPlayerId"] = resolution.DefendingPlayerId,
            ["winnerPlayerId"] = resolution.WinnerPlayerId,
            ["attackerObjectIds"] = resolution.AttackerObjectIds,
            ["defenderObjectIds"] = resolution.DefenderObjectIds,
            ["survivingAttackerObjectIds"] = resolution.SurvivingAttackerObjectIds,
            ["survivingDefenderObjectIds"] = resolution.SurvivingDefenderObjectIds,
            ["destroyedObjectIds"] = resolution.DestroyedObjectIds,
            ["relatedEventKinds"] = resolution.RelatedEventKinds
        };
    }

    private static Dictionary<string, object?> BuildPendingTaskQueueSnapshotView(PendingTaskQueueState queue)
    {
        return new Dictionary<string, object?>
        {
            ["hasTasks"] = queue.HasTasks,
            ["isBlocking"] = queue.IsBlocking,
            ["phase"] = queue.Phase,
            ["activeTaskId"] = queue.ActiveTaskId,
            ["tasks"] = queue.Tasks.Select(BuildCleanupTaskSnapshotView).ToArray()
        };
    }

    private static Dictionary<string, object?> BuildCleanupTaskSnapshotView(CleanupTaskState task)
    {
        return new Dictionary<string, object?>
        {
            ["taskId"] = task.TaskId,
            ["kind"] = task.Kind,
            ["reason"] = task.Reason,
            ["playerId"] = task.PlayerId,
            ["objectId"] = task.ObjectId,
            ["battlefieldObjectId"] = task.BattlefieldObjectId
        };
    }

    private static Dictionary<string, object?> BuildPlayerSnapshotView(
        MatchState state,
        HashSet<string> readyPlayers,
        string viewerPlayerId,
        string subjectPlayerId)
    {
        var zones = state.PlayerZones.TryGetValue(subjectPlayerId, out var playerZones)
            ? playerZones
            : PlayerZones.Empty;
        var ownView = string.Equals(viewerPlayerId, subjectPlayerId, StringComparison.Ordinal);

        return new Dictionary<string, object?>
        {
            ["id"] = subjectPlayerId,
            ["name"] = subjectPlayerId,
            ["seat"] = state.Seats[subjectPlayerId],
            ["ready"] = readyPlayers.Contains(subjectPlayerId),
            ["handSize"] = zones.Hand.Count,
            ["score"] = state.PlayerScores.TryGetValue(subjectPlayerId, out var score) ? score : 0,
            ["experience"] = state.PlayerExperience.TryGetValue(subjectPlayerId, out var experience) ? experience : 0,
            ["cardsPlayedThisTurn"] = state.PlayerCardsPlayedThisTurn.TryGetValue(subjectPlayerId, out var cardsPlayedThisTurn)
                ? cardsPlayedThisTurn
                : 0,
            ["deckSubmitted"] = state.PlayerDecklists.ContainsKey(subjectPlayerId),
            ["mulliganCompleted"] = state.MulliganCompletedPlayerIds.Contains(subjectPlayerId, StringComparer.Ordinal),
            ["runePool"] = state.RunePools.TryGetValue(subjectPlayerId, out var runePool)
                ? new Dictionary<string, object?>
                {
                    ["mana"] = runePool.Mana,
                    ["power"] = runePool.TotalPower,
                    ["untypedPower"] = runePool.Power,
                    ["powerByTrait"] = runePool.PowerByTrait
                }
                : new Dictionary<string, object?>
                {
                    ["mana"] = 0,
                    ["power"] = 0,
                    ["untypedPower"] = 0,
                    ["powerByTrait"] = new Dictionary<string, int>(StringComparer.Ordinal)
                },
            ["zones"] = BuildZoneSnapshotView(zones, ownView),
            ["objects"] = BuildObjectSnapshotView(state, VisibleObjectIds(zones, ownView), ownView)
        };
    }

    private static Dictionary<string, object?> BuildZoneSnapshotView(PlayerZones zones, bool ownView)
    {
        return new Dictionary<string, object?>
        {
            ["mainDeckCount"] = zones.MainDeck.Count,
            ["runeDeckCount"] = zones.RuneDeck.Count,
            ["hand"] = ownView ? zones.Hand : [],
            ["handHidden"] = ownView ? 0 : zones.Hand.Count,
            ["base"] = zones.Base,
            ["battlefields"] = zones.Battlefields,
            ["graveyard"] = zones.Graveyard,
            ["banished"] = zones.Banished,
            ["legendZone"] = zones.LegendZone,
            ["championZone"] = zones.ChampionZone
        };
    }

    private static Dictionary<string, object?> BuildLaneSnapshotView(MatchState state)
    {
        var battlefieldObjectIds = state.PlayerZones
            .OrderBy(entry => state.Seats.TryGetValue(entry.Key, out var seat) ? seat : entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields.Select(objectId => new Dictionary<string, object?>
            {
                ["playerId"] = entry.Key,
                ["objectId"] = objectId
            }))
            .ToArray();

        return new Dictionary<string, object?>
        {
            ["battlefieldObjectIds"] = battlefieldObjectIds,
            ["battlefieldCount"] = battlefieldObjectIds.Length,
            ["battlefields"] = BuildBattlefieldStateSnapshotView(state)
        };
    }

    private static IReadOnlyList<Dictionary<string, object?>> BuildBattlefieldStateSnapshotView(MatchState state)
    {
        return state.BattlefieldStates.Values
            .Select(entry => BuildSingleBattlefieldStateSnapshotView(state, entry))
            .ToArray();
    }

    private static Dictionary<string, object?> BuildSingleBattlefieldStateSnapshotView(
        MatchState state,
        BattlefieldState battlefield)
    {
        var pendingTaskKinds = state.PendingCleanupTasks
            .Where(task => string.Equals(task.BattlefieldObjectId, battlefield.BattlefieldObjectId, StringComparison.Ordinal))
            .Select(task => task.Kind)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .ToArray();

        return new Dictionary<string, object?>
        {
            ["battlefieldObjectId"] = battlefield.BattlefieldObjectId,
            ["zonePlayerId"] = battlefield.ZonePlayerId,
            ["cardNo"] = battlefield.CardNo,
            ["controllerId"] = battlefield.ControllerId,
            ["status"] = battlefield.Status,
            ["contested"] = battlefield.Contested,
            ["occupantObjectIds"] = battlefield.OccupantObjectIds,
            ["occupantControllerIds"] = battlefield.OccupantControllerIds,
            ["standbyObjectIds"] = battlefield.StandbyObjectIds,
            ["faceDownStandbyCount"] = battlefield.FaceDownStandbyCount,
            ["pendingTaskKinds"] = pendingTaskKinds
        };
    }

    private static bool IsBattlefieldCardSnapshotObject(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> VisibleObjectIds(PlayerZones zones, bool ownView)
    {
        var ids = new List<string>();
        if (ownView)
        {
            ids.AddRange(zones.Hand);
        }

        ids.AddRange(zones.Base);
        ids.AddRange(zones.Battlefields);
        ids.AddRange(zones.Graveyard);
        ids.AddRange(zones.Banished);
        ids.AddRange(zones.LegendZone);
        ids.AddRange(zones.ChampionZone);
        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, object?> BuildObjectSnapshotView(
        MatchState state,
        IReadOnlyList<string> visibleObjectIds,
        bool ownView)
    {
        return visibleObjectIds
            .Where(objectId => state.CardObjects.ContainsKey(objectId))
            .ToDictionary(
                objectId => objectId,
                objectId => (object?)BuildCardObjectSnapshotView(state, objectId, ownView),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, object?> BuildCardObjectSnapshotView(
        MatchState state,
        string objectId,
        bool ownView)
    {
        var cardObject = state.CardObjects[objectId];
        var location = ResolveObjectLocation(state, objectId);
        if (cardObject.IsFaceDown && !ownView)
        {
            var redacted = new Dictionary<string, object?>
            {
                ["objectId"] = cardObject.ObjectId,
                ["isFaceDown"] = true
            };
            if (location is not null)
            {
                redacted["location"] = BuildObjectLocationSnapshotView(location);
            }

            return redacted;
        }

        var view = new Dictionary<string, object?>
        {
            ["objectId"] = cardObject.ObjectId,
            ["damage"] = cardObject.Damage,
            ["power"] = cardObject.Power,
            ["basePower"] = ResolveBasePower(cardObject),
            ["effectivePower"] = cardObject.Power,
            ["untilEndOfTurnPowerModifier"] = cardObject.UntilEndOfTurnPowerModifier,
            ["isExhausted"] = cardObject.IsExhausted,
            ["isFaceDown"] = cardObject.IsFaceDown,
            ["isAttacking"] = cardObject.IsAttacking,
            ["isDefending"] = cardObject.IsDefending,
            ["tags"] = cardObject.Tags,
            ["untilEndOfTurnEffects"] = cardObject.UntilEndOfTurnEffects,
            ["manaCost"] = cardObject.ManaCost,
            ["attachedToObjectId"] = cardObject.AttachedToObjectId,
            ["cardNo"] = cardObject.CardNo,
            ["ownerId"] = cardObject.OwnerId,
            ["controllerId"] = cardObject.ControllerId
        };
        if (location is not null)
        {
            view["location"] = BuildObjectLocationSnapshotView(location);
        }

        return view;
    }

    private static int ResolveBasePower(CardObjectState cardObject)
    {
        return Math.Max(0, cardObject.Power - cardObject.UntilEndOfTurnPowerModifier);
    }

    private static Dictionary<string, object?> BuildObjectLocationSnapshotView(ObjectLocationState location)
    {
        var view = new Dictionary<string, object?>
        {
            ["playerId"] = location.PlayerId,
            ["zone"] = location.Zone
        };
        if (!string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
        {
            view["battlefieldObjectId"] = location.BattlefieldObjectId;
        }

        return view;
    }

    private static ObjectLocationState? ResolveObjectLocation(MatchState state, string objectId)
    {
        if (state.ObjectLocations.TryGetValue(objectId, out var location))
        {
            return location;
        }

        foreach (var (playerId, zones) in state.PlayerZones)
        {
            if (zones.MainDeck.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "MAIN_DECK");
            }
            if (zones.RuneDeck.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "RUNE_DECK");
            }
            if (zones.Hand.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "HAND");
            }
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BASE");
            }
            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BATTLEFIELD");
            }
            if (zones.Graveyard.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "GRAVEYARD");
            }
            if (zones.Banished.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BANISHED");
            }
            if (zones.LegendZone.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "LEGEND");
            }
            if (zones.ChampionZone.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "CHAMPION");
            }
        }

        return null;
    }

    public static IReadOnlyDictionary<string, ActionPromptDto> BuildPrompts(MatchState state)
    {
        if (state.Status != MatchStatuses.InProgress)
        {
            var readyPlayers = state.ReadyPlayerIds.ToHashSet(StringComparer.Ordinal);
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId =>
            {
                var ready = readyPlayers.Contains(playerId);
                var deckSubmitted = state.PlayerDecklists.ContainsKey(playerId);
                return ActionPromptBuilder.Build(
                    state,
                    playerId,
                    !ready && state.Status != MatchStatuses.Finished,
                    ready
                        ? "已准备，等待对手"
                        : deckSubmitted
                            ? "等待玩家准备"
                            : "等待提交合法卡组",
                    ready ? ["WAIT"] : deckSubmitted ? ["READY"] : ["SUBMIT_DECK"]);
            });
        }

        if (string.Equals(state.Phase, MatchPhases.Mulligan, StringComparison.Ordinal))
        {
            var mulliganPlayerId = OpeningMulliganPlayerId(state);
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, mulliganPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, mulliganPlayerId, StringComparison.Ordinal)
                    ? "请选择 0 到 2 张起手牌进行调度"
                    : "等待对手完成起手调度",
                string.Equals(playerId, mulliganPlayerId, StringComparison.Ordinal)
                    ? WithSurrender("MULLIGAN")
                    : WithSurrender("WAIT")));
        }

        if (HasOpenStackPriority(state))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过优先行动权"
                    : "等待对手优先行动",
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? WithSurrender(ActionPromptBuilder.StackPriorityActions(state, playerId))
                    : WithSurrender("WAIT")));
        }

        if (HasOpenSpellDuelFocus(state))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过焦点"
                    : "等待对手焦点行动",
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? WithSurrender(ActionPromptBuilder.SpellDuelFocusActions(state, playerId))
                    : WithSurrender("WAIT")));
        }

        if (ActiveStartBattleTask(state) is not null)
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId =>
            {
                var canDeclareBattle = string.Equals(playerId, state.ActivePlayerId, StringComparison.Ordinal)
                    && ActionPromptBuilder.CanDeclareBattleForActiveTask(state, playerId);
                return ActionPromptBuilder.Build(
                    state,
                    playerId,
                    canDeclareBattle,
                    canDeclareBattle
                        ? "请为争夺战场声明战斗"
                        : "等待对手处理争夺战场战斗任务",
                    canDeclareBattle ? WithSurrender("DECLARE_BATTLE") : WithSurrender("WAIT"));
            });
        }

        if (HasBlockingPendingTaskQueue(state))
        {
            var reason = BlockingPendingTaskQueueReason(state);
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                false,
                reason,
                WithSurrender("WAIT")));
        }

        return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
            state,
            playerId,
            playerId == state.ActivePlayerId,
            playerId == state.ActivePlayerId ? "当前玩家普通开环行动" : "等待对手行动",
            playerId == state.ActivePlayerId
                ? WithSurrender(
                    "PLAY_CARD",
                    "ACTIVATE_ABILITY",
                    "ASSEMBLE_EQUIPMENT",
                    "MOVE_UNIT",
                    "DECLARE_BATTLE",
                    "HIDE_CARD",
                    "REVEAL_CARD",
                    "TAP_RUNE",
                    "RECYCLE_RUNE",
                    "LEGEND_ACT",
                    "END_TURN"
                )
                : WithSurrender("WAIT")));
    }

    private static IReadOnlyList<string> WithSurrender(params string[] actions)
    {
        return WithSurrender((IReadOnlyList<string>)actions);
    }

    private static IReadOnlyList<string> WithSurrender(IReadOnlyList<string> actions)
    {
        return actions.Contains("SURRENDER", StringComparer.Ordinal)
            ? actions
            : actions.Concat(["SURRENDER"]).ToArray();
    }

    private static bool HasOpenStackPriority(MatchState state)
    {
        return state.StackItems.Count > 0 && !string.IsNullOrWhiteSpace(state.PriorityPlayerId);
    }

    private static bool HasOpenSpellDuelFocus(MatchState state)
    {
        return string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId);
    }

    public static string? OpeningMulliganPlayerId(MatchState state)
    {
        var completed = state.MulliganCompletedPlayerIds.ToHashSet(StringComparer.Ordinal);
        return OpeningMulliganOrder(state)
            .FirstOrDefault(playerId => !completed.Contains(playerId));
    }

    private static IReadOnlyList<string> OpeningMulliganOrder(MatchState state)
    {
        var ordered = new List<string>();
        if (!string.IsNullOrWhiteSpace(state.ActivePlayerId)
            && state.Seats.ContainsKey(state.ActivePlayerId))
        {
            ordered.Add(state.ActivePlayerId);
        }

        if (!string.IsNullOrWhiteSpace(state.OpeningSecondActionPlayerId)
            && state.Seats.ContainsKey(state.OpeningSecondActionPlayerId)
            && !ordered.Contains(state.OpeningSecondActionPlayerId, StringComparer.Ordinal))
        {
            ordered.Add(state.OpeningSecondActionPlayerId);
        }

        ordered.AddRange(state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .Where(playerId => !ordered.Contains(playerId, StringComparer.Ordinal)));
        return ordered;
    }
}

internal static class ActionPromptBuilder
{
    private const string RecycleRunePaymentOptionalCostPrefix = "RECYCLE_RUNE:";
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
    private const string LongSwordCardNo = "SFD·022/221";
    private const int LongSwordAssemblePowerCost = 1;
    private const string LongSwordAssembleOptionalCost = "ASSEMBLE_RED";
    private const string SoulSwordCardNo = "UNL-039/219";
    private const int SoulSwordAssemblePowerCost = 1;
    private const string SoulSwordAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string JaggedDirkCardNo = "SFD·009/221";
    private const int JaggedDirkAssemblePowerCost = 1;
    private const string JaggedDirkAssembleOptionalCost = "ASSEMBLE_RED";
    private const string RecurveBowCardNo = "SFD·016/221";
    private const int RecurveBowAssemblePowerCost = 1;
    private const string RecurveBowAssembleOptionalCost = "ASSEMBLE_RED";
    private const string ArionsFallCardNo = "SFD·030/221";
    private const int ArionsFallAssemblePowerCost = 1;
    private const string ArionsFallAssembleOptionalCost = "ASSEMBLE_RED";
    private const string WitheredBattleaxeCardNo = "UNL-019/219";
    private const int WitheredBattleaxeAssemblePowerCost = 1;
    private const string WitheredBattleaxeAssembleOptionalCost = "ASSEMBLE_RED";
    private const string BrutalizerCardNo = "SFD·042/221";
    private const int BrutalizerAssemblePowerCost = 1;
    private const string BrutalizerAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string GuardianAngelCardNo = "SFD·051/221";
    private const int GuardianAngelAssemblePowerCost = 1;
    private const string GuardianAngelAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string ClothArmorCardNo = "SFD·064/221";
    private const int ClothArmorAssemblePowerCost = 1;
    private const string ClothArmorAssembleOptionalCost = "ASSEMBLE_BLUE";
    private const string HextechInfusedBulwarkCardNo = "SFD·073/221";
    private const int HextechInfusedBulwarkAssemblePowerCost = 1;
    private const string HextechInfusedBulwarkAssembleOptionalCost = "ASSEMBLE_BLUE";
    private const string WanderersGuidebookCardNo = "SFD·086/221";
    private const int WanderersGuidebookAssemblePowerCost = 1;
    private const string WanderersGuidebookAssembleOptionalCost = "ASSEMBLE_BLUE";
    private const string SteraksGageCardNo = "SFD·056/221";
    private const int SteraksGageAssemblePowerCost = 1;
    private const string SteraksGageAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string DoransShieldCardNo = "SFD·033/221";
    private const int DoransShieldAssemblePowerCost = 1;
    private const string DoransShieldAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string DoransRingCardNo = "SFD·124/221";
    private const int DoransRingAssemblePowerCost = 1;
    private const string DoransRingAssembleOptionalCost = "ASSEMBLE_PURPLE";
    private const string DoransBladeCardNo = "SFD·095/221";
    private const int DoransBladeAssemblePowerCost = 1;
    private const string DoransBladeAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string HexdrinkerCardNo = "SFD·102/221";
    private const int HexdrinkerAssemblePowerCost = 1;
    private const string HexdrinkerAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string WarmogsArmorCardNo = "SFD·108/221";
    private const int WarmogsArmorAssemblePowerCost = 1;
    private const string WarmogsArmorAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string TrinityForceCardNo = "SFD·115/221";
    private const int TrinityForceAssemblePowerCost = 1;
    private const string TrinityForceAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string HuntersMacheteCardNo = "UNL-096/219";
    private const int HuntersMacheteAssemblePowerCost = 1;
    private const string HuntersMacheteAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string BoneClubCardNo = "SFD·118/221";
    private const int BoneClubAssemblePowerCost = 1;
    private const string BoneClubAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string BoneClubPromoCardNo = "SFD·118a/221·P";
    private const int BoneClubPromoAssemblePowerCost = 1;
    private const string BoneClubPromoAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string BootsOfSwiftnessCardNo = "SFD·133/221";
    private const int BootsOfSwiftnessAssemblePowerCost = 1;
    private const string BootsOfSwiftnessAssembleOptionalCost = "ASSEMBLE_PURPLE";
    private const string CullCardNo = "SFD·134/221";
    private const int CullAssemblePowerCost = 1;
    private const string CullAssembleOptionalCost = "ASSEMBLE_PURPLE";
    private const string EdgeOfNightCardNo = "SFD·139/221";
    private const int EdgeOfNightAssemblePowerCost = 1;
    private const string EdgeOfNightAssembleOptionalCost = "ASSEMBLE_PURPLE";
    private const string VanguardsEyeCardNo = "SFD·153/221";
    private const int VanguardsEyeAssemblePowerCost = 1;
    private const string VanguardsEyeAssembleOptionalCost = "ASSEMBLE_YELLOW";
    private const string BfSwordCardNo = "SFD·161/221";
    private const int BfSwordAssemblePowerCost = 1;
    private const string BfSwordAssembleOptionalCost = "ASSEMBLE_YELLOW";
    private const string SacredShearsCardNo = "SFD·172/221";
    private const int SacredShearsAssemblePowerCost = 1;
    private const string SacredShearsAssembleOptionalCost = "ASSEMBLE_YELLOW";
    private sealed record AssembleEquipmentProfile(
        string CardNo,
        string DisplayName,
        string OptionalCost,
        string OptionalCostLabel,
        string PowerTrait,
        int PowerCost,
        string PaymentResourceReason);

    private static readonly IReadOnlyDictionary<string, AssembleEquipmentProfile> ImplementedAssembleEquipmentProfiles =
        new Dictionary<string, AssembleEquipmentProfile>(StringComparer.Ordinal)
        {
            [LongSwordCardNo] = new(
                LongSwordCardNo,
                "长剑",
                LongSwordAssembleOptionalCost,
                "装配红色符能",
                RuneTrait.Red,
                LongSwordAssemblePowerCost,
                "payment resource action: recycle red rune for assemble cost"),
            [SoulSwordCardNo] = new(
                SoulSwordCardNo,
                "灵魂之剑",
                SoulSwordAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                SoulSwordAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [JaggedDirkCardNo] = new(
                JaggedDirkCardNo,
                "锯齿短匕",
                JaggedDirkAssembleOptionalCost,
                "装配红色符能",
                RuneTrait.Red,
                JaggedDirkAssemblePowerCost,
                "payment resource action: recycle red rune for assemble cost"),
            [RecurveBowCardNo] = new(
                RecurveBowCardNo,
                "反曲之弓",
                RecurveBowAssembleOptionalCost,
                "装配红色符能",
                RuneTrait.Red,
                RecurveBowAssemblePowerCost,
                "payment resource action: recycle red rune for assemble cost"),
            [ArionsFallCardNo] = new(
                ArionsFallCardNo,
                "阿瑞昂的陨落",
                ArionsFallAssembleOptionalCost,
                "装配红色符能",
                RuneTrait.Red,
                ArionsFallAssemblePowerCost,
                "payment resource action: recycle red rune for assemble cost"),
            [WitheredBattleaxeCardNo] = new(
                WitheredBattleaxeCardNo,
                "枯萎战斧",
                WitheredBattleaxeAssembleOptionalCost,
                "装配红色符能",
                RuneTrait.Red,
                WitheredBattleaxeAssemblePowerCost,
                "payment resource action: recycle red rune for assemble cost"),
            [BrutalizerCardNo] = new(
                BrutalizerCardNo,
                "残暴之力",
                BrutalizerAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                BrutalizerAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [GuardianAngelCardNo] = new(
                GuardianAngelCardNo,
                "守护天使",
                GuardianAngelAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                GuardianAngelAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [ClothArmorCardNo] = new(
                ClothArmorCardNo,
                "布甲",
                ClothArmorAssembleOptionalCost,
                "装配蓝色符能",
                RuneTrait.Blue,
                ClothArmorAssemblePowerCost,
                "payment resource action: recycle blue rune for assemble cost"),
            [HextechInfusedBulwarkCardNo] = new(
                HextechInfusedBulwarkCardNo,
                "海克斯注力刚壁",
                HextechInfusedBulwarkAssembleOptionalCost,
                "装配蓝色符能",
                RuneTrait.Blue,
                HextechInfusedBulwarkAssemblePowerCost,
                "payment resource action: recycle blue rune for assemble cost"),
            [WanderersGuidebookCardNo] = new(
                WanderersGuidebookCardNo,
                "云游图鉴",
                WanderersGuidebookAssembleOptionalCost,
                "装配蓝色符能",
                RuneTrait.Blue,
                WanderersGuidebookAssemblePowerCost,
                "payment resource action: recycle blue rune for assemble cost"),
            [SteraksGageCardNo] = new(
                SteraksGageCardNo,
                "斯特拉克的挑战护手",
                SteraksGageAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                SteraksGageAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [DoransShieldCardNo] = new(
                DoransShieldCardNo,
                "多兰之盾",
                DoransShieldAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                DoransShieldAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [DoransRingCardNo] = new(
                DoransRingCardNo,
                "多兰之戒",
                DoransRingAssembleOptionalCost,
                "装配紫色符能",
                RuneTrait.Purple,
                DoransRingAssemblePowerCost,
                "payment resource action: recycle purple rune for assemble cost"),
            [DoransBladeCardNo] = new(
                DoransBladeCardNo,
                "多兰之刃",
                DoransBladeAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                DoransBladeAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [HexdrinkerCardNo] = new(
                HexdrinkerCardNo,
                "海克斯饮魔刀",
                HexdrinkerAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                HexdrinkerAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [WarmogsArmorCardNo] = new(
                WarmogsArmorCardNo,
                "狂徒铠甲",
                WarmogsArmorAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                WarmogsArmorAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [TrinityForceCardNo] = new(
                TrinityForceCardNo,
                "三相之力",
                TrinityForceAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                TrinityForceAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [HuntersMacheteCardNo] = new(
                HuntersMacheteCardNo,
                "猎人的宽刃刀",
                HuntersMacheteAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                HuntersMacheteAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [BoneClubCardNo] = new(
                BoneClubCardNo,
                "碎骨棒",
                BoneClubAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                BoneClubAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [BoneClubPromoCardNo] = new(
                BoneClubPromoCardNo,
                "碎骨棒",
                BoneClubPromoAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                BoneClubPromoAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [BootsOfSwiftnessCardNo] = new(
                BootsOfSwiftnessCardNo,
                "轻灵之靴",
                BootsOfSwiftnessAssembleOptionalCost,
                "装配紫色符能",
                RuneTrait.Purple,
                BootsOfSwiftnessAssemblePowerCost,
                "payment resource action: recycle purple rune for assemble cost"),
            [CullCardNo] = new(
                CullCardNo,
                "萃取",
                CullAssembleOptionalCost,
                "装配紫色符能",
                RuneTrait.Purple,
                CullAssemblePowerCost,
                "payment resource action: recycle purple rune for assemble cost"),
            [EdgeOfNightCardNo] = new(
                EdgeOfNightCardNo,
                "夜之锋刃",
                EdgeOfNightAssembleOptionalCost,
                "装配紫色符能",
                RuneTrait.Purple,
                EdgeOfNightAssemblePowerCost,
                "payment resource action: recycle purple rune for assemble cost"),
            [VanguardsEyeCardNo] = new(
                VanguardsEyeCardNo,
                "先锋之眼",
                VanguardsEyeAssembleOptionalCost,
                "装配黄色符能",
                RuneTrait.Yellow,
                VanguardsEyeAssemblePowerCost,
                "payment resource action: recycle yellow rune for assemble cost"),
            [BfSwordCardNo] = new(
                BfSwordCardNo,
                "暴风大剑",
                BfSwordAssembleOptionalCost,
                "装配黄色符能",
                RuneTrait.Yellow,
                BfSwordAssemblePowerCost,
                "payment resource action: recycle yellow rune for assemble cost"),
            [SacredShearsCardNo] = new(
                SacredShearsCardNo,
                "神圣剪刀",
                SacredShearsAssembleOptionalCost,
                "装配黄色符能",
                RuneTrait.Yellow,
                SacredShearsAssemblePowerCost,
                "payment resource action: recycle yellow rune for assemble cost")
        };
    private const string CrescentGuardCardNo = "UNL-122/219";
    private const int CrescentGuardReadyPowerCost = 1;
    private const string BattlefieldEphemeralUnitsSteadfastCardNo = "UNL-208/219";
    private const string BattlefieldHeldMoveUnitToBaseCardNo = "UNL-207/219";
    private const string BattlefieldHoldCreateMinionCardNo = "OGN·275/298";
    private const string BattlefieldHoldDrawCardNo = "OGN·280/298";
    private const string BattlefieldHoldCallRuneCardNo = "OGN·288/298";
    private const string BattlefieldHoldGrantBoonCardNo = "OGN·283/298";
    private const string BattlefieldHeldReturnHeroCardNo = "OGN·281/298";
    private const string BattlefieldHeldPayPowerScoreCardNo = "SFD·214/221";
    private const string BattlefieldDestroyedInBattleRecallCardNo = "UNL-206/219";
    private const string BattlefieldGrantLegendAttachArmamentCardNo = "SFD·208/221";
    private const string BattlefieldExtraStandbyCardNo = "OGN·278/298";
    private const string BattlefieldExtraStandbyAltCardNo = "OGN·278a/298";
    private const string BattlefieldHeldActivateConquestEffectsCardNo = "OGN·286/298";
    private const string BattlefieldConquerConsumeBoonDrawCardNo = "OGN·282/298";
    private const string BattlefieldConquerMillTwoCardNo = "SFD·212/221";
    private const string BattlefieldHoldEachPlayerCallRuneCardNo = "SFD·219/221";
    private const string BattlefieldAllUnitsPowerPlusOneCardNo = "OGN·294/298";
    private const string BattlefieldDefenderSteadfastTwoCardNo = "OGN·279/298";
    private const string BattlefieldDefendMoveFriendlyUnitToBaseCardNo = "OGN·285/298";
    private const string BattlefieldConquerRecycleRuneCardNo = "OGN·287/298";
    private const string BattlefieldDefendRevealSpellCardNo = "SFD·215/221";
    private const string BattlefieldIsolatedDefenderSteadfastMinusTwoCardNo = "UNL-210/219";
    private const string BattlefieldConquerPayOneReadyLegendCardNo = "SFD·210/221";
    private const string BattlefieldConquerReadyTwoRunesAtEndCardNo = "OGN·289/298";
    private const string BattlefieldConquerDrawForOtherBattlefieldsCardNo = "SFD·217/221";
    private const string BattlefieldConquerPowerfulPayOneDrawCardNo = "SFD·218/221";
    private const string BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo = "SFD·207/221";
    private const string BattlefieldConquerPayOneCreateGoldCardNo = "SFD·220/221";
    private const string BattlefieldConquerReadyEquipmentCardNo = "SFD·221/221";
    private const string BattlefieldConquerDiscardDrawCardNo = "OGN·298/298";
    private const string BattlefieldConquerOverkillCreateWarhawkCardNo = "UNL-217/219";
    private const string BattlefieldIncreaseWinningScoreCardNo = "OGN·276/298";
    private const string BattlefieldIncreaseWinningScoreAltCardNo = "OGN·276a/298";
    private const string BattlefieldFirstTurnExtraRuneCardNo = "OGN·284/298";
    private const string BattlefieldFirstTurnScoreCardNo = "OGN·290/298";
    private const string BattlefieldScoreDelayCardNo = "SFD·209/221";
    private const string BattlefieldTurnStartDamageAllUnitsCardNo = "UNL-212/219";
    private const string BattlefieldTurnStartDestroyUnitDrawCardNo = "UNL-209/219";
    private const string BattlefieldConquerRevealRecycleCardNo = "OGN·291/298";
    private const string BattlefieldMovedUnitPowerPlusOneCardNo = "OGN·277/298";
    private const string BattlefieldHeldSevenUnitsWinCardNo = "OGN·293/298";
    private const string BattlefieldHeldSevenUnitsWinAltCardNo = "OGN·293a/298";
    private const string BattlefieldPreventMoveToBaseCardNo = "OGN·295/298";
    private const string BattlefieldStaticRoamCardNo = "OGN·297/298";
    private const string BilgewaterBullyCardNo = "OGN·125/298";
    private const int RagingDrakeNextSpellCostReductionMana = 5;
    private const string BattlefieldPreventUnitPlayCardNo = "SFD·216/221";
    private const string BattlefieldEchoCostReductionCardNo = "SFD·211/221";
    private const string BattlefieldHeldNextSpellEchoCardNo = "UNL-216/219";
    private const string BattlefieldEquipmentCostReductionCardNo = "SFD·213/221";
    private const string EagerApprenticeCardNo = "OGN·084/298";
    private const string BattlefieldFriendlySpellDrawCardNo = "OGN·292/298";
    private const string BattlefieldSpellPowerBonusCardNo = "UNL-205/219";
    private const string BattlefieldGrantUnitExperienceCardNo = "UNL-213/219";
    private const string MoveUnitBattlefieldZone = "BATTLEFIELD";
    private const string MoveUnitBaseZone = "BASE";
    private const string MoveUnitRoamOptionalCost = "ROAM";
    private const string MoveUnitRoamKeyword = "游走";

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
        IReadOnlyList<string> RequiredOptionalCosts,
        bool Composable,
        string? UnsupportedReason);

    private sealed record AssembleEquipmentPromptRequirement(
        string SourceObjectId,
        string EquipmentCardNo,
        string DisplayName,
        IReadOnlyList<ActionPromptChoiceDto> TargetChoices,
        IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices,
        IReadOnlyList<ActionPromptChoiceDto> PaymentResourceChoices,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> PaymentResourcePowerByChoice,
        IReadOnlyDictionary<string, int> AvailablePowerByTrait,
        IReadOnlyDictionary<string, int> AvailablePowerByTraitWithPaymentResources,
        IReadOnlyList<string> RequiredOptionalCosts,
        int PowerCost,
        bool Composable,
        string? UnsupportedReason);

    private sealed record ActivateAbilityPromptRequirement(
        string SourceObjectId,
        string CardNo,
        string AbilityId,
        string DisplayName,
        string AbilityLabel,
        int ManaCost,
        int PowerCost,
        int MinTargetCount,
        int MaxTargetCount,
        string TargetScopeLabel,
        IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> TargetChoicesByIndex,
        IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices,
        IReadOnlyList<string> RequiredOptionalCosts,
        bool ExhaustsSource,
        bool ResolvesImmediately,
        bool Composable,
        string? UnsupportedReason);

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
        string? UnsupportedReason);

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
        IReadOnlyList<ActionPromptChoiceDto> BattlefieldChoices,
        IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices,
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
        bool RequiresDifferentArmamentHost = false,
        bool RequiresPlayedAnotherCardThisTurn = false,
        bool RequiresPlayedArmamentThisTurn = false,
        bool RequiresOwnedTeemoUnitTarget = false,
        string ManaCostReductionKind = "",
        int ManaGainAmount = 0,
        int PowerGainAmount = 0,
        string TimingKind = LegendActionTimingKinds.MainOpen,
        bool RequiresPendingSpellStackItem = false,
        bool RequiresPendingEquipmentStackItem = false,
        bool RequiresEzrealEnemyTargetsThisTurn = false,
        bool RequiresPendingFriendlyUnitTarget = false,
        string RequiredControlledBattlefieldCardNo = "");
    private const string BattlefieldHighCostSpellInsightCardNo = "UNL-211/219";
    private const string BattlefieldUnitReturnedCallRuneCardNo = "UNL-214/219";
    private const string BattlefieldPlayUnitPayOneBoonCardNo = "UNL-218/219";
    private const string BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo = "UNL-215/219";
    private const string BattlefieldTargetSpellSkillDamageBonusCardNo = "OGN·296/298";
    private const string BattlefieldHeldUnitCostIncreaseCardNo = "UNL-219/219";
    private const string BattlefieldHeldUnitCostIncreaseEffectPrefix = "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:";
    private const string RagingDrakeNextSpellCostReductionEffectPrefix = "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:";
    private const string BattlefieldUnitGainExperienceAbilityId = "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE";
    private const string BattlefieldGrantedLegendAttachArmamentAbilityId = "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD";
    private const string YasuoLegendCardNo = "FND-259/298";
    private const string YasuoLegendAbilityId = "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT";
    private const string LeeSinLegendCardNo = "OGN·257/298";
    private const string LeeSinLegendAbilityId = "LEGEND_PAY_1_EXHAUST_GRANT_BOON";
    private const string PoppyLegendCardNo = "UNL-237/219";
    private const string PoppyLegendAbilityId = "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW";
    private const string ViktorLegendCardNo = "FND-265/298";
    private const string ViktorLegendAbilityId = "LEGEND_PAY_1_EXHAUST_CREATE_MINION";
    private const string MissFortuneLegendCardNo = "OGN·267/298";
    private const string MissFortuneLegendAbilityId = "LEGEND_EXHAUST_GRANT_ROAM";
    private const string KhazixLegendCardNo = "UNL-201/219";
    private const string KhazixLegendBoonAbilityId = "LEGEND_SPEND_1_EXPERIENCE_EXHAUST_GRANT_BOON";
    private const string KhazixLegendMoveAbilityId = "LEGEND_SPEND_2_EXPERIENCE_EXHAUST_MOVE_DORMANT_UNIT_TO_BASE";
    private const string PykeLegendCardNo = "UNL-185/219";
    private const string PykeLegendAbilityId = "LEGEND_PAY_1_EXHAUST_RECALL_BATTLEFIELD_UNIT_CREATE_COIN";
    private const string JaxSpiritforgedLegendCardNo = "SFD·193/221";
    private const string JaxLegendAttachAbilityId = "LEGEND_PAY_1_EXHAUST_ATTACH_UNATTACHED_ARMAMENT";
    private const string JaxLegendReattachAbilityId = "LEGEND_EXHAUST_REATTACH_ATTACHED_ARMAMENT";
    private const string DariusOriginLegendCardNo = "OGN·253/298";
    private const string DariusLegendAbilityId = "LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA";
    private const string DianaLegendAbilityId = "LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA";
    private const string KaisaLegendAbilityId = "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL";
    private const string OrnnLegendAbilityId = "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT";
    private const string EzrealLegendAbilityId = "LEGEND_REACTION_EXHAUST_DRAW_AFTER_TWO_ENEMY_TARGETS";
    private const string IreliaLegendCardNo = "SFD·195/221";
    private const string IreliaLegendAbilityId = "LEGEND_REACTION_PAY_1_EXHAUST_READY_TARGETED_FRIENDLY_UNIT";
    private const string TeemoOriginLegendCardNo = "OGN·263/298";
    private const string TeemoLegendAbilityId = "LEGEND_PAY_1_EXHAUST_RECALL_OWNED_TEEMO_UNIT";
    private const string AzirSpiritforgedLegendCardNo = "SFD·197/221";
    private const string AzirLegendAbilityId = "LEGEND_PAY_1_EXHAUST_CREATE_SAND_SOLDIER_AFTER_ARMAMENT";
    private const string LilliaLegendCardNo = "UNL-189/219";
    private const string LilliaLegendAbilityId = "LEGEND_DYNAMIC_PAY_EXHAUST_CREATE_FAERIE";
    private const string PlayedArmamentThisTurnEffectPrefix = "PLAYED_ARMAMENT_THIS_TURN:";
    private const string PlayedEquipmentThisTurnEffectPrefix = "PLAYED_EQUIPMENT_THIS_TURN:";
    private const string PlayedSpellThisTurnEffectPrefix = "PLAYED_SPELL_THIS_TURN:";
    private const string EzrealEnemyTargetsThisTurnPrefix = "EZREAL_ENEMY_TARGETS_THIS_TURN:";
    private const int EzrealEnemyTargetThreshold = 2;
    private const int LilliaLegendBaseManaCost = 4;

    private static class LegendActionTimingKinds
    {
        public const string MainOpen = "MAIN_OPEN";
        public const string PriorityWindow = "PRIORITY_WINDOW";
        public const string SpellDuelFocus = "SPELL_DUEL_FOCUS";
    }

    private static class LegendActionManaCostReductionKinds
    {
        public const string FriendlyEphemeralFieldObjects = "FRIENDLY_EPHEMERAL_FIELD_OBJECTS";
    }

    public static IReadOnlyList<string> ActionsWithLegendActIfAvailable(
        MatchState state,
        string playerId,
        string primaryAction)
    {
        var legendSources = SourcesFor(state, playerId, "LEGEND_ACT");
        return legendSources?.Count > 0
            ? ["LEGEND_ACT", primaryAction]
            : [primaryAction];
    }

    public static IReadOnlyList<string> StackPriorityActions(MatchState state, string playerId)
    {
        var actions = new List<string>();
        if (SourcesFor(state, playerId, "PLAY_CARD")?.Count > 0)
        {
            actions.Add("PLAY_CARD");
        }

        if (SourcesFor(state, playerId, "REVEAL_CARD")?.Count > 0)
        {
            actions.Add("REVEAL_CARD");
        }

        if (SourcesFor(state, playerId, "LEGEND_ACT")?.Count > 0)
        {
            actions.Add("LEGEND_ACT");
        }

        actions.Add("PASS_PRIORITY");
        return actions;
    }

    public static IReadOnlyList<string> SpellDuelFocusActions(MatchState state, string playerId)
    {
        var actions = new List<string>();
        if (SourcesFor(state, playerId, "PLAY_CARD")?.Count > 0)
        {
            actions.Add("PLAY_CARD");
        }

        var legendSources = SourcesFor(state, playerId, "LEGEND_ACT");
        if (legendSources?.Count > 0)
        {
            actions.Add("LEGEND_ACT");
        }

        actions.Add("PASS_FOCUS");
        return actions;
    }

    public static bool CanDeclareBattleForActiveTask(MatchState state, string playerId)
    {
        return DeclareBattleSourceRequirements(state, playerId).Count > 0;
    }

    public static ActionPromptDto Build(
        MatchState state,
        string playerId,
        bool actionable,
        string reason,
        IReadOnlyList<string> actions)
    {
        var normalizedActions = actions
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Select(action => action.Trim())
            .ToArray();
        var promptId = $"{state.RoomId}:{state.Tick}:{playerId}:{string.Join(",", normalizedActions)}";
        var candidates = normalizedActions
            .Select(action => BuildCandidate(state, playerId, action, actionable, reason))
            .ToArray();

        return new ActionPromptDto(
            playerId,
            actionable,
            reason,
            normalizedActions,
            promptId,
            state.Tick,
            candidates);
    }

    private static ActionPromptCandidateDto BuildCandidate(
        MatchState state,
        string playerId,
        string action,
        bool promptActionable,
        string promptReason)
    {
        var sources = SourcesFor(state, playerId, action);
        var targets = TargetsFor(state, playerId, action);
        var destinations = DestinationsFor(state, playerId, action);
        var modes = ModesFor(state, playerId, action);
        var optionalCosts = OptionalCostsFor(state, playerId, action);
        var requiresSourceChoices = string.Equals(action, "PLAY_CARD", StringComparison.Ordinal)
            || string.Equals(action, "MOVE_UNIT", StringComparison.Ordinal)
            || string.Equals(action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal)
            || string.Equals(action, "LEGEND_ACT", StringComparison.Ordinal)
            || string.Equals(action, "ACTIVATE_ABILITY", StringComparison.Ordinal)
            || string.Equals(action, "HIDE_CARD", StringComparison.Ordinal)
            || string.Equals(action, "REVEAL_CARD", StringComparison.Ordinal)
            || string.Equals(action, "TAP_RUNE", StringComparison.Ordinal)
            || string.Equals(action, "RECYCLE_RUNE", StringComparison.Ordinal)
            || string.Equals(action, "DECLARE_BATTLE", StringComparison.Ordinal);
        var requiresTargetChoices = string.Equals(action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal)
            || string.Equals(action, "DECLARE_BATTLE", StringComparison.Ordinal);
        var hasRequiredChoices = !requiresSourceChoices
            || sources?.Count > 0;
        hasRequiredChoices = hasRequiredChoices
            && (!requiresTargetChoices || targets?.Count > 0);
        var isSurrender = string.Equals(action, "SURRENDER", StringComparison.Ordinal);
        var enabled = (promptActionable
                || (isSurrender
                    && string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
                    && state.Seats.ContainsKey(playerId)))
            && !string.Equals(action, "WAIT", StringComparison.Ordinal)
            && hasRequiredChoices;
        return new ActionPromptCandidateDto(
            action,
            LabelFor(action),
            enabled,
            enabled ? promptReason : DisabledReasonFor(action, promptReason, hasRequiredChoices),
            sources,
            targets,
            destinations,
            modes,
            optionalCosts,
            MetadataFor(state, playerId, action));
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
            "PLAY_CARD" => zones.Hand
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
        var requirements = new List<HideCardPromptRequirement>();
        foreach (var objectId in zones.Hand.Where(objectId => IsImplementedStandbyHideSource(state, playerId, objectId)))
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
                costChoices,
                StandbyHideManaCost,
                true,
                null));
        }

        return requirements;
    }

    private static IReadOnlyList<RevealCardPromptRequirement> RevealCardSourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        var canRevealInBase = CanRevealStandbyInBase(state, playerId);
        var canPlayReaction = CanRevealStandbyReactionToStack(state, playerId);
        if (!canRevealInBase && !canPlayReaction)
        {
            return [];
        }

        var mode = canPlayReaction ? StandbyReactionMode : StandbyRevealMode;
        var modeLabel = canPlayReaction ? StandbyReactionModeLabel : StandbyRevealModeLabel;
        var destination = canPlayReaction ? StandbyReactionDestination : StandbyRevealDestination;
        var destinationLabel = canPlayReaction ? "结算链" : "基地";
        var optionalCostLabel = canPlayReaction ? "支付 0 作为反应打出" : "支付 0 翻开待命";
        var destinationChoices = new[]
        {
            new ActionPromptChoiceDto(destination, destinationLabel, "服务端待命翻开目的地")
        };
        var optionalCostChoices = new[]
        {
            new ActionPromptChoiceDto(StandbyRevealOptionalCost, optionalCostLabel)
        };
        var requirements = new List<RevealCardPromptRequirement>();
        foreach (var objectId in zones.Base.Where(objectId => IsImplementedStandbyRevealSource(state, playerId, objectId)))
        {
            if (!state.CardObjects.TryGetValue(objectId, out var cardObject)
                || string.IsNullOrWhiteSpace(cardObject.CardNo)
                || !CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo, out var behavior))
            {
                continue;
            }

            requirements.Add(new RevealCardPromptRequirement(
                objectId,
                behavior.CardNo,
                behavior.DisplayName,
                mode,
                modeLabel,
                destinationChoices,
                optionalCostChoices,
                [StandbyRevealOptionalCost],
                true,
                null));
        }

        return requirements;
    }

    private static bool CanRevealStandbyInBase(MatchState state, string playerId)
    {
        return string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            && string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            && string.Equals(state.ActivePlayerId, playerId, StringComparison.Ordinal)
            && state.StackItems.Count == 0;
    }

    private static bool CanRevealStandbyReactionToStack(MatchState state, string playerId)
    {
        return string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            && string.Equals(state.TimingState, TimingStates.NeutralClosed, StringComparison.Ordinal)
            && state.StackItems.Count > 0
            && string.Equals(state.PriorityPlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool IsImplementedStandbyRevealSource(MatchState state, string playerId, string objectId)
    {
        if (!state.CardObjects.TryGetValue(objectId, out var cardObject)
            || !cardObject.IsFaceDown
            || string.IsNullOrWhiteSpace(cardObject.CardNo)
            || !SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            || !CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo, out var behavior))
        {
            return false;
        }

        return cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            || HasDelimitedTag(behavior.SourceUnitTags, CardObjectTags.Standby);
    }

    private static ActionPromptChoiceDto[] HideCardOptionalCostChoicesForState(
        MatchState state,
        string playerId)
    {
        var currentPool = state.RunePools.TryGetValue(playerId, out var runePool)
            ? runePool
            : RunePool.Empty;
        var choices = new List<ActionPromptChoiceDto>();
        if (currentPool.Mana >= StandbyHideManaCost)
        {
            choices.Add(new ActionPromptChoiceDto(StandbyHideOptionalCost, "支付 1 法力布置待命"));
            if (HasTeemoStandbyHidePermission(state, playerId))
            {
                choices.Add(new ActionPromptChoiceDto(StandbyHideTeemoOptionalCost, "提莫布置待命"));
            }
        }

        if (HasFreeStandbyHidePermission(state, playerId))
        {
            choices.Add(new ActionPromptChoiceDto(
                StandbyHideFreeOptionalCost,
                "免费布置待命",
                "本回合已有服务端授权"));
        }

        return choices.ToArray();
    }

    private static ActionPromptChoiceDto[] HideCardDestinationChoicesForState(
        MatchState state,
        string playerId)
    {
        return
        [
            new ActionPromptChoiceDto(StandbyHideDestination, "待命"),
            .. ControlledBattlefieldExtraStandbyObjects(state, playerId)
                .Select(objectId => BattlefieldDestinationChoice(state, objectId, "班德尔树额外待命目的地"))
        ];
    }

    private static bool HasFreeStandbyHidePermission(MatchState state, string playerId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            $"{FreeStandbyHideEffectPrefix}{playerId}",
            StringComparer.Ordinal);
    }

    private static bool HasTeemoStandbyHidePermission(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.LegendZone.Any(legendObjectId =>
                state.CardObjects.TryGetValue(legendObjectId, out var legendState)
                && IsTeemoLegendCardNo(legendState.CardNo));
    }

    private static bool IsTeemoLegendCardNo(string? cardNo)
    {
        return cardNo is TeemoOriginLegendCardNo
            or "OGN·263a/298"
            or "OGN·307/298"
            or "OGN·307*/298";
    }

    private static bool IsMovableUnitSource(MatchState state, string playerId, string objectId)
    {
        return IsControlledObjectWithTag(state, playerId, objectId, CardObjectTags.UnitCard)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && !cardObject.IsFaceDown
            && !cardObject.IsAttacking
            && !cardObject.IsDefending;
    }

    private static IReadOnlyList<MoveUnitPromptRequirement> MoveUnitSourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        var requirements = new List<MoveUnitPromptRequirement>();
        foreach (var objectId in zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => IsMovableUnitSource(state, playerId, objectId))
            .Distinct(StringComparer.Ordinal))
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                requirements.Add(new MoveUnitPromptRequirement(
                    objectId,
                    MoveUnitBaseZone,
                    "基地",
                    "BASE_TO_BATTLEFIELD",
                    "基地 -> 战场",
                    [new ActionPromptChoiceDto(MoveUnitBattlefieldZone, "战场", "implemented coarse battlefield destination")],
                    [],
                    [],
                    true,
                    null));
            }

            if (!zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                continue;
            }

            if (!HasMoveUnitPromptPreventMoveToBase(state, playerId, objectId))
            {
                requirements.Add(new MoveUnitPromptRequirement(
                    objectId,
                    MoveUnitBattlefieldZone,
                    "战场",
                    "BATTLEFIELD_TO_BASE",
                    "战场 -> 基地",
                    [new ActionPromptChoiceDto(MoveUnitBaseZone, "基地", "implemented coarse base destination")],
                    [],
                    [],
                    true,
                    null));
            }

            if (!HasMoveUnitPromptRoamPermission(state, playerId, objectId, zones)
                || !TryMoveUnitPreciseBattlefieldOrigin(state, playerId, objectId, zones, out var preciseOrigin, out var preciseOriginLabel))
            {
                continue;
            }

            var preciseDestinations = MoveUnitPreciseBattlefieldDestinationChoices(
                state,
                playerId,
                preciseOrigin)
                .ToArray();
            if (preciseDestinations.Length == 0)
            {
                continue;
            }

            requirements.Add(new MoveUnitPromptRequirement(
                objectId,
                preciseOrigin,
                preciseOriginLabel,
                "ROAM",
                "游走",
                preciseDestinations,
                [new ActionPromptChoiceDto(MoveUnitRoamOptionalCost, "游走", "required for precise battlefield movement")],
                [MoveUnitRoamOptionalCost],
                true,
                null));
        }

        return requirements;
    }

    private static bool HasMoveUnitPromptPreventMoveToBase(MatchState state, string playerId, string sourceObjectId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal)
            && zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, BattlefieldPreventMoveToBaseCardNo, StringComparison.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId));
    }

    private static bool HasMoveUnitPromptRoamPermission(
        MatchState state,
        string playerId,
        string sourceObjectId,
        PlayerZones zones)
    {
        if (!zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal)
            || !state.CardObjects.TryGetValue(sourceObjectId, out var sourceState))
        {
            return false;
        }

        return sourceState.Tags.Contains(MoveUnitRoamKeyword, StringComparer.Ordinal)
            || sourceState.UntilEndOfTurnEffects.Contains(MoveUnitRoamOptionalCost, StringComparer.Ordinal)
            || HasBilgewaterBullyBoonPromptRoamPermission(sourceState)
            || zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, BattlefieldStaticRoamCardNo, StringComparison.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId));
    }

    private static bool HasBilgewaterBullyBoonPromptRoamPermission(CardObjectState sourceState)
    {
        return string.Equals(sourceState.CardNo, BilgewaterBullyCardNo, StringComparison.Ordinal)
            && sourceState.Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal);
    }

    private static bool TryMoveUnitPreciseBattlefieldOrigin(
        MatchState state,
        string playerId,
        string sourceObjectId,
        PlayerZones zones,
        out string origin,
        out string originLabel)
    {
        origin = string.Empty;
        originLabel = string.Empty;

        if (state.ObjectLocations.TryGetValue(sourceObjectId, out var location)
            && string.Equals(location.PlayerId, playerId, StringComparison.Ordinal)
            && string.Equals(location.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
        {
            origin = $"{MoveUnitBattlefieldZone}:{location.BattlefieldObjectId}";
            originLabel = BattlefieldLocationLabel(state, location.BattlefieldObjectId);
            return true;
        }

        var battlefieldObjectId = zones.Battlefields.FirstOrDefault(objectId =>
            !string.Equals(objectId, sourceObjectId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && IsPromptBattlefieldCardObject(cardObject));
        if (string.IsNullOrWhiteSpace(battlefieldObjectId))
        {
            return false;
        }

        origin = $"{MoveUnitBattlefieldZone}:{battlefieldObjectId}";
        originLabel = BattlefieldLocationLabel(state, battlefieldObjectId);
        return true;
    }

    private static IEnumerable<ActionPromptChoiceDto> MoveUnitPreciseBattlefieldDestinationChoices(
        MatchState state,
        string playerId,
        string origin)
    {
        IEnumerable<ActionPromptChoiceDto> choices = state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Battlefields
                .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                    && IsPromptBattlefieldCardObject(cardObject))
                .Select(objectId => new ActionPromptChoiceDto(
                    $"{MoveUnitBattlefieldZone}:{objectId}",
                    BattlefieldLocationLabel(state, objectId),
                    "implemented precise battlefield roam destination"))
            : [];

        return choices
            .Append(new ActionPromptChoiceDto(
                $"{MoveUnitBattlefieldZone}:{playerId}-MAIN",
                "己方主战场",
                "implemented precise battlefield roam destination"))
            .Where(choice => !string.Equals(choice.Id, origin, StringComparison.Ordinal))
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First());
    }

    private static string BattlefieldLocationLabel(MatchState state, string? battlefieldObjectId)
    {
        if (string.IsNullOrWhiteSpace(battlefieldObjectId))
        {
            return "战场";
        }

        return state.CardObjects.TryGetValue(battlefieldObjectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            ? $"{cardObject.CardNo} / {battlefieldObjectId}"
            : battlefieldObjectId;
    }

    private static bool IsTapRuneSource(MatchState state, string playerId, string objectId)
    {
        return IsObjectInPlayerZone(state, playerId, objectId, "BASE")
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && cardObject.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            && !cardObject.IsFaceDown
            && !cardObject.IsExhausted;
    }

    private static bool IsRecycleRuneSource(MatchState state, string playerId, string objectId)
    {
        return IsObjectInPlayerZone(state, playerId, objectId, "BASE")
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && cardObject.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            && cardObject.Tags.Any(tag => tag.StartsWith("COLOR:", StringComparison.OrdinalIgnoreCase))
            && !cardObject.IsFaceDown;
    }

    private static IReadOnlyList<ActivateAbilityPromptRequirement> ActivateAbilitySourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(objectId => ActivateAbilityRequirementsForSource(state, playerId, objectId))
            .ToArray();
    }

    private static IReadOnlyList<ActivateAbilityPromptRequirement> ActivateAbilityRequirementsForSource(
        MatchState state,
        string playerId,
        string sourceObjectId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones)
            || !state.CardObjects.TryGetValue(sourceObjectId, out var cardObject)
            || !SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            || !cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || cardObject.IsFaceDown)
        {
            return [];
        }

        var requirements = new List<ActivateAbilityPromptRequirement>();
        foreach (var ability in P4ActivatedAbilityCatalog.GetAll())
        {
            if (!string.Equals(cardObject.CardNo, ability.SourceCardNo, StringComparison.Ordinal)
                || (ability.RequiresBattlefieldSource && !zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal))
                || (ability.ExhaustsSourceAsCost && cardObject.IsExhausted))
            {
                continue;
            }

            var targetChoicesByIndex = ActivateAbilityTargetChoicesByIndex(state, playerId, ability);
            if (ability.RequiredTargetCount > 0
                && Enumerable.Range(0, ability.RequiredTargetCount)
                    .Any(index => !targetChoicesByIndex.TryGetValue(index.ToString(System.Globalization.CultureInfo.InvariantCulture), out var choices)
                        || choices.Count == 0))
            {
                continue;
            }

            requirements.Add(new ActivateAbilityPromptRequirement(
                sourceObjectId,
                ability.SourceCardNo,
                ability.AbilityId,
                ActivateAbilityDisplayName(ability),
                ActivateAbilityLabel(ability),
                ability.ManaCost,
                ability.PowerCost,
                ability.RequiredTargetCount,
                ability.RequiredTargetCount,
                ActivateAbilityTargetScopeLabel(ability),
                targetChoicesByIndex,
                [],
                [],
                ability.ExhaustsSourceAsCost,
                false,
                true,
                null));
        }

        if (zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && !cardObject.IsExhausted
            && BattlefieldGrantUnitExperienceObjectId(state, playerId, sourceObjectId) is not null)
        {
            requirements.Add(new ActivateAbilityPromptRequirement(
                sourceObjectId,
                cardObject.CardNo ?? string.Empty,
                BattlefieldUnitGainExperienceAbilityId,
                "蜕变花园授予能力",
                "横置：获得 1 经验",
                0,
                0,
                0,
                0,
                "无目标",
                new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal),
                [],
                [],
                true,
                true,
                true,
                null));
        }

        return requirements
            .Where(requirement => CanPayActivateAbilityRequirement(state, playerId, requirement))
            .ToArray();
    }

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
                || !LegendActionPrerequisitesMet(state, playerId, ability))
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
                true,
                unsupportedReason is null,
                unsupportedReason);

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
            new(
                YasuoLegendAbilityId,
                [YasuoLegendCardNo, "OGN·259/298", "OGN·305*/298", "OGN·305/298"],
                "亚索传奇移动技能",
                2,
                0,
                "SPEND_MANA:2",
                1,
                true,
                "MOVE_FRIENDLY_UNIT"),
            new(
                LeeSinLegendAbilityId,
                [LeeSinLegendCardNo, "OGN·304*/298", "OGN·304/298"],
                "李青传奇增益技能",
                1,
                0,
                "SPEND_MANA:1",
                1,
                true,
                "GRANT_BOON"),
            new(
                PoppyLegendAbilityId,
                ["UNL-203/219", "UNL-237*/219", PoppyLegendCardNo],
                "波比传奇抽牌技能",
                0,
                3,
                "SPEND_EXPERIENCE:3",
                0,
                false,
                "DRAW_ONE"),
            new(
                ViktorLegendAbilityId,
                [ViktorLegendCardNo, "OGN·265/298", "OGN·308*/298", "OGN·308/298"],
                "维克托传奇随从技能",
                1,
                0,
                "SPEND_MANA:1",
                0,
                false,
                "CREATE_MINION"),
            new(
                MissFortuneLegendAbilityId,
                [MissFortuneLegendCardNo, "OGN·309/298", "OGN·309*/298"],
                "赏金猎人传奇游走技能",
                0,
                0,
                string.Empty,
                1,
                true,
                "GRANT_ROAM"),
            new(
                KhazixLegendBoonAbilityId,
                [KhazixLegendCardNo, "UNL-236/219", "UNL-236*/219"],
                "卡兹克传奇增益技能",
                0,
                1,
                "SPEND_EXPERIENCE:1",
                1,
                true,
                "GRANT_BOON"),
            new(
                KhazixLegendMoveAbilityId,
                [KhazixLegendCardNo, "UNL-236/219", "UNL-236*/219"],
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
                [PykeLegendCardNo, "UNL-228/219", "UNL-228*/219"],
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
                [JaxSpiritforgedLegendCardNo, "SFD·245/221"],
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
                [JaxSpiritforgedLegendCardNo, "SFD·245/221"],
                "武器大师传奇重贴附技能",
                0,
                0,
                string.Empty,
                2,
                true,
                "REATTACH_ARMAMENT",
                RequiresArmamentSecondTarget: true,
                RequiresAttachedArmamentSecondTarget: true,
                RequiresDifferentArmamentHost: true),
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
                RequiredControlledBattlefieldCardNo: BattlefieldGrantLegendAttachArmamentCardNo),
            new(
                DariusLegendAbilityId,
                [DariusOriginLegendCardNo, "OGN·302/298", "OGN·302*/298"],
                "诺克萨斯之手传奇鼓舞技能",
                0,
                0,
                string.Empty,
                0,
                false,
                "GAIN_MANA",
                RequiresPlayedAnotherCardThisTurn: true,
                ManaGainAmount: 1),
            new(
                DianaLegendAbilityId,
                ["UNL-197/219", "UNL-234/219", "UNL-234*/219"],
                "皎月女神传奇法术对决法力技能",
                0,
                0,
                string.Empty,
                0,
                false,
                "GAIN_MANA",
                ManaGainAmount: 1,
                TimingKind: LegendActionTimingKinds.SpellDuelFocus),
            new(
                KaisaLegendAbilityId,
                ["OGN·247/298", "OGN·299/298", "OGN·299*/298"],
                "虚空之女传奇法术反应符能技能",
                0,
                0,
                string.Empty,
                0,
                false,
                "GAIN_POWER",
                PowerGainAmount: 1,
                TimingKind: LegendActionTimingKinds.PriorityWindow,
                RequiresPendingSpellStackItem: true),
            new(
                OrnnLegendAbilityId,
                ["SFD·189/221", "SFD·244/221"],
                "山隐之焰传奇装备反应符能技能",
                0,
                0,
                string.Empty,
                0,
                false,
                "GAIN_POWER",
                PowerGainAmount: 1,
                TimingKind: LegendActionTimingKinds.PriorityWindow,
                RequiresPendingEquipmentStackItem: true),
            new(
                EzrealLegendAbilityId,
                ["SFD·199/221", "SFD·248/221"],
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
                [IreliaLegendCardNo, "SFD·195a/221·P", "SFD·246/221"],
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
                [TeemoOriginLegendCardNo, "OGN·263a/298", "OGN·307/298", "OGN·307*/298"],
                "迅捷斥候传奇召回技能",
                1,
                0,
                "SPEND_MANA:1",
                1,
                false,
                "RETURN_OWNED_TEEMO_UNIT_TO_HAND",
                RequiresOwnedTeemoUnitTarget: true),
            new(
                AzirLegendAbilityId,
                [AzirSpiritforgedLegendCardNo, "SFD·247/221"],
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
                [LilliaLegendCardNo, "UNL-230/219", "UNL-230*/219"],
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

    private static IEnumerable<ActionPromptChoiceDto> LegendActionArmamentTargetChoices(
        MatchState state,
        string playerId,
        LegendActionAbilityDefinition ability,
        string? selectedUnitObjectId)
    {
        return ControlledLegendActionObjects(state, playerId)
            .Where(objectId => LegendActionIsValidArmamentSecondTarget(state, playerId, selectedUnitObjectId, objectId, ability))
            .Select(objectId => ObjectChoice(state, objectId, "受控武装目标"));
    }

    private static IEnumerable<ActionPromptChoiceDto> LegendActionOwnedTeemoTargetChoices(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Concat(zones.ChampionZone)
            .Where(objectId => LegendActionIsValidOwnedTeemoUnitTarget(state, playerId, objectId))
            .Select(objectId => ObjectChoice(state, objectId, "己方提莫单位"));
    }

    private static IEnumerable<ActionPromptChoiceDto> LegendActionPendingFriendlyUnitTargetChoices(
        MatchState state,
        string playerId)
    {
        return ControlledLegendActionObjects(state, playerId)
            .Where(objectId => LegendActionIsPendingFriendlyUnitTarget(state, playerId, objectId))
            .Select(objectId => ObjectChoice(state, objectId, "被当前结算链项目指定的友方单位"));
    }

    private static bool LegendActionIsValidFriendlyUnitTarget(
        MatchState state,
        string playerId,
        string targetObjectId,
        LegendActionAbilityDefinition ability)
    {
        if (!LegendActionIsControlledFieldObjectWithTag(state, playerId, targetObjectId, CardObjectTags.UnitCard))
        {
            return false;
        }

        if (!state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            || string.IsNullOrWhiteSpace(targetState.CardNo))
        {
            return false;
        }

        if (ability.RequiresBattlefieldTarget
            && (!TryFindLegendActionFieldObjectLocation(state.PlayerZones, targetObjectId, out var location)
                || !string.Equals(location.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)))
        {
            return false;
        }

        return !ability.RequiresExhaustedTarget
            || targetState.IsExhausted;
    }

    private static bool LegendActionIsValidArmamentSecondTarget(
        MatchState state,
        string playerId,
        string? unitObjectId,
        string equipmentObjectId,
        LegendActionAbilityDefinition ability)
    {
        if (string.IsNullOrWhiteSpace(unitObjectId)
            || string.Equals(unitObjectId, equipmentObjectId, StringComparison.Ordinal)
            || !LegendActionIsControlledFieldObjectWithTag(state, playerId, equipmentObjectId, CardObjectTags.EquipmentCard)
            || !state.CardObjects.TryGetValue(equipmentObjectId, out var equipmentState)
            || string.IsNullOrWhiteSpace(equipmentState.CardNo)
            || equipmentState.IsFaceDown
            || !equipmentState.Tags.Contains("武装", StringComparer.Ordinal))
        {
            return false;
        }

        var isAttached = !string.IsNullOrWhiteSpace(equipmentState.AttachedToObjectId);
        if (ability.RequiresUnattachedArmamentSecondTarget && isAttached)
        {
            return false;
        }

        if (ability.RequiresAttachedArmamentSecondTarget && !isAttached)
        {
            return false;
        }

        return !ability.RequiresDifferentArmamentHost
            || !string.Equals(equipmentState.AttachedToObjectId, unitObjectId, StringComparison.Ordinal);
    }

    private static bool LegendActionIsValidOwnedTeemoUnitTarget(MatchState state, string playerId, string targetObjectId)
    {
        return state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && !targetState.IsFaceDown
            && targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && string.Equals(targetState.OwnerId, playerId, StringComparison.Ordinal)
            && LegendActionIsTeemoUnitCardNo(targetState.CardNo);
    }

    private static bool LegendActionIsTeemoUnitCardNo(string? cardNo)
    {
        return cardNo is "FND-196/298"
            or "OGN·121/298"
            or "OGN·121a/298"
            or "OGN·197/298"
            or "OGN·197a/298"
            or "OGN·197b/298"
            or "SFD·230/221"
            or "SFD·230*/221";
    }

    private static bool LegendActionIsPendingFriendlyUnitTarget(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return LegendActionIsControlledFieldObjectWithTag(state, playerId, targetObjectId, CardObjectTags.UnitCard)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && !string.IsNullOrWhiteSpace(targetState.CardNo)
            && state.StackItems.Any(stackItem =>
                string.Equals(stackItem.ControllerId, playerId, StringComparison.Ordinal)
                && stackItem.TargetObjectIds.Contains(targetObjectId, StringComparer.Ordinal));
    }

    private static bool LegendActionIsControlledFieldObjectWithTag(
        MatchState state,
        string playerId,
        string objectId,
        string tag)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && cardObject.Tags.Contains(tag, StringComparer.Ordinal)
            && TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && string.Equals(location.PlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool TryFindLegendActionFieldObjectLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out (string PlayerId, string Zone) location)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, MoveUnitBaseZone);
                return true;
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, MoveUnitBattlefieldZone);
                return true;
            }
        }

        location = default;
        return false;
    }

    private static bool LegendActionSourceHasAbility(
        MatchState state,
        string playerId,
        string? sourceCardNo,
        LegendActionAbilityDefinition ability)
    {
        return ability.SourceCardNos.Contains(sourceCardNo, StringComparer.Ordinal)
            || (!string.IsNullOrWhiteSpace(ability.RequiredControlledBattlefieldCardNo)
                && PlayerControlsBattlefieldCard(state, playerId, ability.RequiredControlledBattlefieldCardNo));
    }

    private static bool LegendActionTimingAllowed(
        MatchState state,
        string playerId,
        LegendActionAbilityDefinition ability)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal))
        {
            return false;
        }

        return ability.TimingKind switch
        {
            LegendActionTimingKinds.MainOpen =>
                string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                && string.Equals(state.ActivePlayerId, playerId, StringComparison.Ordinal)
                && state.StackItems.Count == 0,
            LegendActionTimingKinds.PriorityWindow =>
                state.StackItems.Count > 0
                && !string.IsNullOrWhiteSpace(state.PriorityPlayerId)
                && string.Equals(state.PriorityPlayerId, playerId, StringComparison.Ordinal),
            LegendActionTimingKinds.SpellDuelFocus =>
                state.StackItems.Count == 0
                && string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(state.FocusPlayerId)
                && string.Equals(state.FocusPlayerId, playerId, StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool LegendActionPrerequisitesMet(
        MatchState state,
        string playerId,
        LegendActionAbilityDefinition ability)
    {
        if (ability.RequiresPlayedAnotherCardThisTurn
            && (!state.PlayerCardsPlayedThisTurn.TryGetValue(playerId, out var playedCount) || playedCount <= 0))
        {
            return false;
        }

        if (ability.RequiresPlayedArmamentThisTurn
            && !state.UntilEndOfTurnEffects.Contains($"{PlayedArmamentThisTurnEffectPrefix}{playerId}", StringComparer.Ordinal))
        {
            return false;
        }

        if (ability.RequiresPendingSpellStackItem
            && !LegendActionPendingStackSourceHasTag(state, CardObjectTags.SpellCard))
        {
            return false;
        }

        if (ability.RequiresPendingEquipmentStackItem
            && !LegendActionPendingStackSourceHasTag(state, CardObjectTags.EquipmentCard))
        {
            return false;
        }

        return !ability.RequiresEzrealEnemyTargetsThisTurn
            || LegendActionEzrealEnemyTargetsThisTurnCount(state.UntilEndOfTurnEffects, playerId) >= EzrealEnemyTargetThreshold;
    }

    private static bool LegendActionPendingStackSourceHasTag(MatchState state, string tag)
    {
        return state.StackItems.Any(stackItem =>
            state.CardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
            && sourceState.Tags.Contains(tag, StringComparer.Ordinal));
    }

    private static int LegendActionEzrealEnemyTargetsThisTurnCount(IReadOnlyList<string> effectIds, string playerId)
    {
        var prefix = $"{EzrealEnemyTargetsThisTurnPrefix}{playerId}:";
        return effectIds
            .Where(effectId => effectId.StartsWith(prefix, StringComparison.Ordinal))
            .Select(effectId => effectId[prefix.Length..])
            .Select(value => int.TryParse(value, out var count) ? count : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static int ResolveLegendActionManaCost(
        MatchState state,
        string playerId,
        LegendActionAbilityDefinition ability)
    {
        var reduction = string.Equals(
            ability.ManaCostReductionKind,
            LegendActionManaCostReductionKinds.FriendlyEphemeralFieldObjects,
            StringComparison.Ordinal)
                ? CountLegendActionFriendlyEphemeralFieldObjects(state, playerId)
                : 0;
        return Math.Max(0, ability.ManaCost - reduction);
    }

    private static int CountLegendActionFriendlyEphemeralFieldObjects(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Base
                .Concat(zones.Battlefields)
                .Count(objectId => state.CardObjects.TryGetValue(objectId, out var objectState)
                    && SourceObjectControlledByPlayerOrLegacyOwned(objectState, playerId)
                    && objectState.Tags.Contains(CardObjectTags.Ephemeral, StringComparer.Ordinal))
            : 0;
    }

    private static IReadOnlyList<string> LegendActionRequiredCostTokens(int manaCost, LegendActionAbilityDefinition ability)
    {
        var requiredCostToken = string.IsNullOrWhiteSpace(ability.RequiredCostToken)
            ? manaCost > 0 ? $"SPEND_MANA:{manaCost}" : string.Empty
            : ability.RequiredCostToken;
        return string.IsNullOrWhiteSpace(requiredCostToken) ? [] : [requiredCostToken];
    }

    private static bool CanPayLegendActionRequirement(
        MatchState state,
        string playerId,
        LegendActionPromptRequirement requirement)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        if (runePool.Mana < requirement.ManaCost)
        {
            return false;
        }

        var experience = state.PlayerExperience.TryGetValue(playerId, out var currentExperience)
            ? currentExperience
            : 0;
        return requirement.ExperienceCost <= 0 || experience >= requirement.ExperienceCost;
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
            EzrealLegendAbilityId => "反应横置：抽 1 张",
            IreliaLegendAbilityId => "反应支付 1 并横置：重置被选为目标的友方单位",
            TeemoLegendAbilityId => "支付 1 并横置：召回己方提莫单位",
            AzirLegendAbilityId => "支付 1 并横置：打出黄沙士兵",
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
            LegendActionTimingKinds.MainOpen => "主阶段开环",
            LegendActionTimingKinds.PriorityWindow => "优先权窗口",
            LegendActionTimingKinds.SpellDuelFocus => "法术对决焦点",
            _ => timingKind
        };
    }

    private static string LegendActionCostLabel(string cost)
    {
        if (cost.StartsWith("SPEND_MANA:", StringComparison.Ordinal)
            && int.TryParse(cost["SPEND_MANA:".Length..], out var mana))
        {
            return $"支付 {mana} 法力";
        }

        if (cost.StartsWith("SPEND_EXPERIENCE:", StringComparison.Ordinal)
            && int.TryParse(cost["SPEND_EXPERIENCE:".Length..], out var experience))
        {
            return $"支付 {experience} 经验";
        }

        return cost;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> ActivateAbilityTargetChoicesByIndex(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability)
    {
        if (ability.RequiredTargetCount == 0)
        {
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
        }

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.XerathDamageAbilityId, StringComparison.Ordinal))
        {
            var choices = PublicBoardObjects(state)
                .Where(objectId => IsPromptFieldUnitObject(state, objectId))
                .Where(objectId => CanPayXerathTargetCost(state, playerId, ability, objectId))
                .Select(objectId => ObjectChoice(
                    state,
                    objectId,
                    IsPromptEnemyFieldObject(state, playerId, objectId)
                        && state.CardObjects.TryGetValue(objectId, out var targetState)
                        && CardResourceKeywordRules.SpellshieldTaxFromTags(targetState.Tags) > 0
                            ? "unit target with spellshield tax"
                            : "unit target"))
                .ToArray();
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
            {
                ["0"] = choices
            };
        }

        return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
    }

    private static bool CanPayXerathTargetCost(
        MatchState state,
        string playerId,
        P4ActivatedAbilityDefinition ability,
        string targetObjectId)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        return CanPayManaAndAnyPower(
            runePool,
            SpellshieldTaxManaForTarget(state, playerId, targetObjectId),
            ability.PowerCost);
    }

    private static bool CanPayActivateAbilityRequirement(
        MatchState state,
        string playerId,
        ActivateAbilityPromptRequirement requirement)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        return CanPayManaAndAnyPower(runePool, requirement.ManaCost, requirement.PowerCost);
    }

    private static bool CanPayManaAndAnyPower(RunePool runePool, int manaCost, int powerCost)
    {
        return manaCost >= 0
            && powerCost >= 0
            && runePool.Mana >= manaCost
            && runePool.TotalPower >= powerCost;
    }

    private static string ActivateAbilityDisplayName(P4ActivatedAbilityDefinition ability)
    {
        return ability.AbilityId switch
        {
            P4ActivatedAbilityCatalog.ViDoublePowerAbilityId => "蔚",
            P4ActivatedAbilityCatalog.XerathDamageAbilityId => "泽拉斯",
            _ => ability.DisplayName
        };
    }

    private static string ActivateAbilityLabel(P4ActivatedAbilityDefinition ability)
    {
        return ability.AbilityId switch
        {
            P4ActivatedAbilityCatalog.ViDoublePowerAbilityId => "蔚：支付 2 法力和 1 符能，战力翻倍",
            P4ActivatedAbilityCatalog.XerathDamageAbilityId => "泽拉斯：横置并支付符能，造成 3 点伤害",
            _ => ability.DisplayName
        };
    }

    private static string ActivateAbilityTargetScopeLabel(P4ActivatedAbilityDefinition ability)
    {
        return ability.RequiredTargetCount == 0
            ? "无目标"
            : string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.XerathDamageAbilityId, StringComparison.Ordinal)
                ? "任意场上单位"
                : "服务端目标";
    }

    private static int SpellshieldTaxManaForTarget(MatchState state, string playerId, string targetObjectId)
    {
        return IsPromptEnemyFieldObject(state, playerId, targetObjectId)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            ? CardResourceKeywordRules.SpellshieldTaxFromTags(targetState.Tags)
            : 0;
    }

    private static string? BattlefieldGrantUnitExperienceObjectId(
        MatchState state,
        string playerId,
        string sourceObjectId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return null;
        }

        return zones.Battlefields.FirstOrDefault(battlefieldObjectId =>
            !string.Equals(battlefieldObjectId, sourceObjectId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(battlefieldObjectId, out var battlefieldState)
            && string.Equals(battlefieldState.CardNo, BattlefieldGrantUnitExperienceCardNo, StringComparison.Ordinal)
            && SourceObjectControlledByPlayerOrLegacyOwned(battlefieldState, playerId));
    }

    private static IReadOnlyList<AssembleEquipmentPromptRequirement> AssembleEquipmentSourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Where(objectId => IsImplementedAssembleEquipmentSource(state, playerId, objectId))
            .Distinct(StringComparer.Ordinal)
            .Select(objectId => AssembleEquipmentSourceRequirement(state, playerId, objectId))
            .Where(requirement => requirement.TargetChoices.Count > 0)
            .ToArray();
    }

    private static AssembleEquipmentPromptRequirement AssembleEquipmentSourceRequirement(
        MatchState state,
        string playerId,
        string objectId)
    {
        var assembleProfile = AssembleEquipmentProfileForObject(state, objectId)
            ?? ImplementedAssembleEquipmentProfiles[LongSwordCardNo];
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var paymentResourceChoices = AssembleEquipmentPaymentResourceChoices(state, playerId, assembleProfile);
        var paymentResourcePowerByTrait = AssembleEquipmentPaymentResourcePowerByTrait(state, playerId, assembleProfile);

        return new AssembleEquipmentPromptRequirement(
            objectId,
            assembleProfile.CardNo,
            assembleProfile.DisplayName,
            AssembleEquipmentTargetChoicesForSource(state, playerId, objectId),
            [new ActionPromptChoiceDto(assembleProfile.OptionalCost, assembleProfile.OptionalCostLabel)],
            paymentResourceChoices,
            AssembleEquipmentPaymentResourcePowerByChoice(state, playerId, assembleProfile),
            PlayCardAvailablePowerByTrait(runePool, new Dictionary<string, int>(StringComparer.Ordinal)),
            PlayCardAvailablePowerByTrait(runePool, paymentResourcePowerByTrait),
            [assembleProfile.OptionalCost],
            assembleProfile.PowerCost,
            true,
            null);
    }

    private static IReadOnlyList<ActionPromptChoiceDto> AssembleEquipmentTargetChoicesForSource(
        MatchState state,
        string playerId,
        string sourceObjectId)
    {
        return ControlledBoardObjects(state, playerId)
            .Where(objectId => !string.Equals(objectId, sourceObjectId, StringComparison.Ordinal))
            .Where(objectId => IsImplementedAssembleEquipmentTarget(state, playerId, objectId))
            .Select(objectId => ObjectChoice(state, objectId, "implemented controlled unit host"))
            .ToArray();
    }

    private static bool IsImplementedAssembleEquipmentSource(MatchState state, string playerId, string objectId)
    {
        if (!IsObjectInPlayerZone(state, playerId, objectId, "BASE")
            || !state.CardObjects.TryGetValue(objectId, out var cardObject)
            || cardObject.IsFaceDown
            || !cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            || !string.IsNullOrWhiteSpace(cardObject.AttachedToObjectId))
        {
            return false;
        }

        if (!SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
        {
            return false;
        }

        var assembleProfile = AssembleEquipmentProfileForObject(state, objectId);
        if (assembleProfile is null)
        {
            return false;
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        return CanPayAssembleEquipmentCost(runePool, assembleProfile, AssembleEquipmentPaymentResourcePowerByTrait(state, playerId, assembleProfile))
            && AssembleEquipmentTargetChoicesForSource(state, playerId, objectId).Count > 0;
    }

    private static AssembleEquipmentProfile? AssembleEquipmentProfileForObject(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && ImplementedAssembleEquipmentProfiles.TryGetValue(cardObject.CardNo, out var assembleProfile)
                ? assembleProfile
                : null;
    }

    private static bool CanPayAssembleEquipmentCost(
        RunePool runePool,
        AssembleEquipmentProfile assembleProfile,
        IReadOnlyDictionary<string, int>? paymentResourcePowerByTrait = null)
    {
        var availablePowerByTrait = PlayCardAvailablePowerByTrait(
            runePool,
            paymentResourcePowerByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
        return availablePowerByTrait.TryGetValue(assembleProfile.PowerTrait, out var power)
            && power >= assembleProfile.PowerCost;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> AssembleEquipmentPaymentResourceChoices(
        MatchState state,
        string playerId,
        AssembleEquipmentProfile assembleProfile)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        if (CanPayAssembleEquipmentCost(runePool, assembleProfile))
        {
            return [];
        }

        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Where(objectId => IsRecycleRuneSource(state, playerId, objectId))
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var runeState)
                && TryGetRuneTrait(runeState, out var runeTrait)
                && string.Equals(runeTrait, assembleProfile.PowerTrait, StringComparison.Ordinal))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .Select(objectId =>
            {
                var choice = ObjectChoice(state, objectId, assembleProfile.PaymentResourceReason);
                return new ActionPromptChoiceDto(
                    $"{RecycleRunePaymentOptionalCostPrefix}{objectId}",
                    $"回收符文支付：{choice.Label}",
                    choice.Reason);
            })
            .ToArray();
    }

    private static IReadOnlyDictionary<string, int> AssembleEquipmentPaymentResourcePowerByTrait(
        MatchState state,
        string playerId,
        AssembleEquipmentProfile assembleProfile)
    {
        var choices = AssembleEquipmentPaymentResourceChoices(state, playerId, assembleProfile);
        if (choices.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [assembleProfile.PowerTrait] = choices.Count * BasicRuneRecyclePowerGain
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> AssembleEquipmentPaymentResourcePowerByChoice(
        MatchState state,
        string playerId,
        AssembleEquipmentProfile assembleProfile)
    {
        var choices = AssembleEquipmentPaymentResourceChoices(state, playerId, assembleProfile);
        if (choices.Count == 0)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        }

        return choices.ToDictionary(
            choice => choice.Id,
            _ => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["trait"] = assembleProfile.PowerTrait,
                ["power"] = BasicRuneRecyclePowerGain
            },
            StringComparer.Ordinal);
    }

    private static bool IsImplementedAssembleEquipmentTarget(MatchState state, string playerId, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && !cardObject.IsFaceDown;
    }

    private static bool IsObjectInPlayerZone(
        MatchState state,
        string playerId,
        string objectId,
        string zone)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        return zone switch
        {
            "BASE" => zones.Base.Contains(objectId, StringComparer.Ordinal),
            "BATTLEFIELD" => zones.Battlefields.Contains(objectId, StringComparer.Ordinal),
            _ => false
        };
    }

    private static bool IsImplementedPlayableHandSource(MatchState state, string playerId, string objectId)
    {
        if (!state.CardObjects.TryGetValue(objectId, out var cardObject)
            || string.IsNullOrWhiteSpace(cardObject.CardNo)
            || !SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
        {
            return false;
        }

        return PlayCardPromptBehaviorsForSource(state, playerId, objectId).Any(behavior =>
            PromptHasRequiredTargetChoices(state, playerId, behavior));
    }

    private static int PromptMinimumManaCost(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId = null)
    {
        var reduction = PromptBaseManaReductionBeforeBattlefieldSpellCost(state, playerId, behavior, sourceObjectId);
        reduction += PromptNextSpellCostReductionMana(state, playerId, behavior, reduction);
        reduction += PromptBattlefieldSpellCostReductionMana(
            state,
            playerId,
            behavior,
            reduction);

        return Math.Max(0, behavior.ManaCost - reduction)
            + PromptBattlefieldHeldUnitCostIncreaseMana(state, playerId, behavior);
    }

    private static int PromptBaseManaReductionBeforeBattlefieldSpellCost(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId = null)
    {
        var reduction = string.Equals(behavior.CostReductionConditionKind, CardCostReductionConditionKinds.None, StringComparison.Ordinal)
            ? 0
            : behavior.CostReductionMana;
        var experience = state.PlayerExperience.TryGetValue(playerId, out var currentExperience)
            ? currentExperience
            : 0;
        if (behavior.OptionalExperienceCost > 0
            && behavior.ManaReductionIfExperiencePaid > 0
            && experience >= behavior.OptionalExperienceCost)
        {
            reduction += behavior.ManaReductionIfExperiencePaid;
        }

        if (behavior.ManaReductionIfDiscardHandCardOptionalCost > 0
            && !string.IsNullOrWhiteSpace(sourceObjectId)
            && HasPromptDiscardHandCardOptionalCostTarget(state, playerId, sourceObjectId))
        {
            reduction += behavior.ManaReductionIfDiscardHandCardOptionalCost;
        }

        return reduction + PromptBattlefieldEquipmentCostReductionMana(state, playerId, behavior);
    }

    private static bool HasPromptDiscardHandCardOptionalCostTarget(
        MatchState state,
        string playerId,
        string? sourceObjectId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Hand.Any(objectId => CanPromptDiscardHandCardAsOptionalCost(state, playerId, sourceObjectId, objectId));
    }

    private static bool CanPromptDiscardHandCardAsOptionalCost(
        MatchState state,
        string playerId,
        string? sourceObjectId,
        string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && (string.IsNullOrWhiteSpace(sourceObjectId)
                || !string.Equals(sourceObjectId, objectId, StringComparison.Ordinal))
            && (!state.CardObjects.TryGetValue(objectId, out var cardObject)
                || SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId));
    }

    private static IReadOnlyList<CardBehaviorDefinition> PlayCardPromptBehaviorsForSource(
        MatchState state,
        string playerId,
        string objectId)
    {
        if (!state.CardObjects.TryGetValue(objectId, out var cardObject)
            || string.IsNullOrWhiteSpace(cardObject.CardNo))
        {
            return [];
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var behaviors = CardBehaviorRegistry.GetAll()
            .Where(behavior => string.Equals(behavior.CardNo, cardObject.CardNo, StringComparison.Ordinal))
            .Where(behavior => CardPermissionKeywordRules.EvaluatePlayTiming(state, playerId, behavior).IsAllowed)
            .Where(behavior => runePool.Mana >= PromptMinimumManaCost(state, playerId, behavior, objectId))
            .Where(behavior => PromptHasRequiredDestinationChoices(state, playerId, behavior))
            .ToArray();
        if (string.Equals(cardObject.CardNo, "SFD·039/221", StringComparison.Ordinal)
            && behaviors.Any(behavior => !string.IsNullOrWhiteSpace(behavior.Mode)
                && PromptHasRequiredTargetChoices(state, playerId, behavior)))
        {
            return behaviors
                .Where(behavior => !string.IsNullOrWhiteSpace(behavior.Mode))
                .ToArray();
        }

        return behaviors;
    }

    private static bool PromptHasRequiredDestinationChoices(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return !behavior.PlaysSourceToBaseAsUnit
            || (PlayCardDestinationChoicesForBehavior(state, playerId, behavior)?.Count ?? 0) > 0;
    }

    private static bool PromptHasRequiredTargetChoices(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        var targetCountConditionApplies = PromptTargetCountConditionApplies(state, playerId, behavior);
        var minTargetCount = PromptMinTargetCount(behavior, targetCountConditionApplies);
        if (minTargetCount == 0)
        {
            return true;
        }

        for (var targetIndex = 0; targetIndex < minTargetCount; targetIndex++)
        {
            if (PromptTargetChoicesForIndex(state, playerId, behavior, targetIndex).Count == 0)
            {
                return false;
            }
        }

        return !PlayCardRequiresServerTargetSelection(state, playerId, behavior)
            || PlayCardLegalTargetSelections(state, playerId, behavior).Count > 0;
    }

    private static bool PlayCardRequiresServerTargetSelection(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        var targetCountConditionApplies = PromptTargetCountConditionApplies(state, playerId, behavior);
        var maxTargetCount = PromptMaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        if (maxTargetCount == 0)
        {
            return false;
        }

        var choicesByIndex = Enumerable.Range(0, maxTargetCount)
            .Select(targetIndex => PromptTargetChoicesForIndex(state, playerId, behavior, targetIndex)
                .Select(choice => choice.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray())
            .ToArray();
        return PlayCardHasServerTargetSelectionConstraint(state, playerId, behavior, choicesByIndex);
    }

    private static bool PromptTargetCountConditionApplies(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.TargetCountConditionKind switch
        {
            CardTargetCountConditionKinds.None => true,
            CardTargetCountConditionKinds.PlayedAfterAnotherCardThisTurn
                => state.PlayerCardsPlayedThisTurn.TryGetValue(playerId, out var count) && count > 0,
            _ => false
        };
    }

    private static int PromptMinTargetCount(
        CardBehaviorDefinition behavior,
        bool targetCountConditionApplies)
    {
        if (!targetCountConditionApplies)
        {
            return 0;
        }

        return behavior.MinTargetCount < 0 ? behavior.RequiredTargetCount : behavior.MinTargetCount;
    }

    private static int PromptMaxTargetCount(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        bool targetCountConditionApplies)
    {
        if (!targetCountConditionApplies)
        {
            return 0;
        }

        if (behavior.TargetEffectAdditionalPowerCost > 0
            && !PromptTargetEffectAdditionalCostAvailable(state, playerId, behavior))
        {
            return 0;
        }

        if (!behavior.UsesFriendlyBattlefieldUnitCountAsMaxTargetCount)
        {
            return behavior.RequiredTargetCount;
        }

        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Battlefields.Count(objectId => state.CardObjects.ContainsKey(objectId))
            : 0;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PromptTargetChoicesForIndex(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        int targetIndex)
    {
        if (behavior.MainDeckLookCount > 0
            && targetIndex >= behavior.MainDeckLookTargetStartIndex
            && string.Equals(behavior.TargetScope, CardTargetScopes.FriendlyMainDeckCard, StringComparison.Ordinal))
        {
            return PromptFriendlyMainDeckLookTargetChoices(state, playerId, behavior);
        }

        return PromptTargetCandidateIds(state, playerId, behavior.TargetScope, targetIndex)
            .Where(objectId => IsPromptTargetObjectInScope(state, playerId, objectId, behavior.TargetScope, targetIndex))
            .Select(objectId => IsPromptStackSpellItem(state, objectId)
                ? StackItemChoice(state, objectId, "legal stack spell target")
                : ObjectChoice(state, objectId, PromptTargetReasonForScope(behavior.TargetScope, targetIndex)))
            .ToArray();
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PromptFriendlyMainDeckLookTargetChoices(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.MainDeck
            .Take(behavior.MainDeckLookCount)
            .Where(objectId => IsPromptKnownCardObject(state, objectId))
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
                && (string.IsNullOrWhiteSpace(behavior.MainDeckTargetRequiredTag)
                    || cardObject.Tags.Contains(behavior.MainDeckTargetRequiredTag, StringComparer.Ordinal)))
            .Select(objectId => ObjectChoice(state, objectId, "己方主牌堆查看候选"))
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<string>> PlayCardLegalTargetSelections(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        var targetCountConditionApplies = PromptTargetCountConditionApplies(state, playerId, behavior);
        var minTargetCount = PromptMinTargetCount(behavior, targetCountConditionApplies);
        var maxTargetCount = PromptMaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        if (maxTargetCount == 0)
        {
            return [];
        }

        var choicesByIndex = Enumerable.Range(0, maxTargetCount)
            .Select(targetIndex => PromptTargetChoicesForIndex(state, playerId, behavior, targetIndex)
                .Select(choice => choice.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray())
            .ToArray();
        if (!PlayCardHasServerTargetSelectionConstraint(state, playerId, behavior, choicesByIndex))
        {
            return [];
        }

        var legalSelections = new List<IReadOnlyList<string>>();
        for (var targetCount = minTargetCount; targetCount <= maxTargetCount; targetCount++)
        {
            if (targetCount == 0)
            {
                var emptySelection = Array.Empty<string>();
                if (IsLegalPlayCardTargetSelection(state, playerId, behavior, emptySelection))
                {
                    legalSelections.Add(emptySelection);
                }

                continue;
            }

            if (choicesByIndex.Take(targetCount).Any(choices => choices.Length == 0))
            {
                continue;
            }

            BuildTargetSelections(0, targetCount, []);
        }

        return legalSelections;

        void BuildTargetSelections(int targetIndex, int targetCount, List<string> currentSelection)
        {
            if (targetIndex >= targetCount)
            {
                var selection = currentSelection.ToArray();
                if (IsLegalPlayCardTargetSelection(state, playerId, behavior, selection))
                {
                    legalSelections.Add(selection);
                }

                return;
            }

            foreach (var choiceId in choicesByIndex[targetIndex])
            {
                if (!behavior.AllowsRepeatedTargets
                    && currentSelection.Contains(choiceId, StringComparer.Ordinal))
                {
                    continue;
                }

                currentSelection.Add(choiceId);
                BuildTargetSelections(targetIndex + 1, targetCount, currentSelection);
                currentSelection.RemoveAt(currentSelection.Count - 1);
            }
        }
    }

    private static bool PlayCardHasServerTargetSelectionConstraint(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<IReadOnlyList<string>> choicesByIndex)
    {
        return behavior.MaxTotalTargetPower > 0
            || choicesByIndex
                .SelectMany(choiceIds => choiceIds)
                .Distinct(StringComparer.Ordinal)
                .Any(objectId => SpellshieldTaxManaForTarget(state, playerId, objectId) > 0);
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
            if (!IsPromptTargetObjectInScope(
                    state,
                    playerId,
                    targetObjectIds[targetIndex],
                    behavior.TargetScope,
                    targetIndex))
            {
                return false;
            }
        }

        if (behavior.MaxTotalTargetPower > 0
            && !HasValidPromptTotalTargetPower(state, behavior, targetObjectIds))
        {
            return false;
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var manaRequired = PromptMinimumManaCost(state, playerId, behavior)
            + targetObjectIds.Sum(targetObjectId => SpellshieldTaxManaForTarget(state, playerId, targetObjectId));
        return runePool.Mana >= manaRequired;
    }

    private static bool HasValidPromptTotalTargetPower(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        var totalPower = 0;
        foreach (var targetObjectId in targetObjectIds)
        {
            if (!state.CardObjects.TryGetValue(targetObjectId, out var targetState)
                || targetState.Power <= 0)
            {
                return false;
            }

            totalPower += targetState.Power;
            if (totalPower > behavior.MaxTotalTargetPower)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> PromptTargetCandidateIds(
        MatchState state,
        string playerId,
        string targetScope,
        int targetIndex)
    {
        if (string.Equals(targetScope, CardTargetScopes.StackSpell, StringComparison.Ordinal)
            || (string.Equals(targetScope, CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell, StringComparison.Ordinal)
                && targetIndex > 0))
        {
            return state.StackItems.Select(item => item.StackItemId);
        }

        if (string.Equals(targetScope, CardTargetScopes.FriendlyHandCard, StringComparison.Ordinal)
            || string.Equals(targetScope, CardTargetScopes.FriendlyHandCardThenBattlefieldUnit, StringComparison.Ordinal)
                && targetIndex == 0
            || string.Equals(targetScope, CardTargetScopes.AnyHandCard, StringComparison.Ordinal))
        {
            return state.PlayerZones.TryGetValue(playerId, out var zones) ? zones.Hand : [];
        }

        if (string.Equals(targetScope, CardTargetScopes.FriendlyGraveyardCard, StringComparison.Ordinal))
        {
            return state.PlayerZones.TryGetValue(playerId, out var zones) ? zones.Graveyard : [];
        }

        if (string.Equals(targetScope, CardTargetScopes.OpponentGraveyardCard, StringComparison.Ordinal))
        {
            return state.PlayerZones
                .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
                .SelectMany(entry => entry.Value.Graveyard);
        }

        if (string.Equals(targetScope, CardTargetScopes.Legend, StringComparison.Ordinal))
        {
            return state.PlayerZones.Values.SelectMany(zones => zones.LegendZone);
        }

        if (string.Equals(targetScope, CardTargetScopes.FriendlyMainDeckCard, StringComparison.Ordinal)
            || string.Equals(targetScope, CardTargetScopes.OpponentHandCard, StringComparison.Ordinal)
            || string.Equals(targetScope, CardTargetScopes.OpponentMainDeckTopCard, StringComparison.Ordinal)
            || string.Equals(targetScope, CardTargetScopes.AnyMainDeckTopFiveCard, StringComparison.Ordinal)
            || string.Equals(targetScope, CardTargetScopes.SacredJudgmentKeepCard, StringComparison.Ordinal))
        {
            return [];
        }

        return PublicBoardObjects(state);
    }

    private static bool IsPromptTargetObjectInScope(
        MatchState state,
        string playerId,
        string objectId,
        string targetScope,
        int targetIndex)
    {
        return targetScope switch
        {
            CardTargetScopes.BattlefieldUnitOrEquipment => IsPromptBattlefieldObject(state, objectId)
                || IsPromptEquipmentObject(state, objectId),
            CardTargetScopes.AnyUnit => IsPromptFieldUnitObject(state, objectId),
            CardTargetScopes.BaseUnit => IsPromptBaseObject(state, objectId),
            CardTargetScopes.FriendlyUnit => IsPromptControlledFieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyUnitThenFriendlyUnit => IsPromptControlledFieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyThenEnemyUnits => targetIndex == 0
                ? IsPromptControlledFieldObject(state, playerId, objectId)
                : IsPromptEnemyFieldObject(state, playerId, objectId),
            CardTargetScopes.UnitThenItsControllersWeapon => targetIndex == 0
                ? IsPromptFieldUnitObject(state, objectId)
                : IsPromptEquipmentObject(state, objectId),
            CardTargetScopes.FriendlyEquipmentThenEnemyEquipment => targetIndex == 0
                ? IsPromptFriendlyEquipmentObject(state, playerId, objectId)
                : IsPromptEnemyEquipmentObject(state, playerId, objectId),
            CardTargetScopes.FriendlyThenEnemyBattlefieldUnits => targetIndex == 0
                ? IsPromptControlledFieldObject(state, playerId, objectId)
                : IsPromptEnemyBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyBattlefieldThenEnemyBattlefieldUnits => targetIndex == 0
                ? IsPromptControlledBattlefieldObject(state, playerId, objectId)
                : IsPromptEnemyBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell => targetIndex == 0
                ? IsPromptControlledBattlefieldObject(state, playerId, objectId)
                : IsPromptStackSpellItem(state, objectId),
            CardTargetScopes.AnyUnitThenFriendlyMainDeckCard => targetIndex == 0
                ? IsPromptFieldUnitObject(state, objectId)
                : false,
            CardTargetScopes.FriendlyBattlefieldUnit => IsPromptControlledBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyHandCard => IsPromptFriendlyHandCard(state, playerId, objectId),
            CardTargetScopes.AnyHandCard => IsPromptFriendlyHandCard(state, playerId, objectId),
            CardTargetScopes.FriendlyHandCardThenBattlefieldUnit => targetIndex == 0
                ? IsPromptFriendlyHandCard(state, playerId, objectId)
                : IsPromptBattlefieldObject(state, objectId),
            CardTargetScopes.FriendlyMainDeckCard => false,
            CardTargetScopes.FriendlyGraveyardCard => IsPromptFriendlyGraveyardCard(state, playerId, objectId),
            CardTargetScopes.FriendlyBaseUnit => IsPromptControlledBaseObject(state, playerId, objectId),
            CardTargetScopes.AttackingUnit => IsPromptAttackingBattlefieldObject(state, objectId),
            CardTargetScopes.EnemyAttackingUnit => IsPromptEnemyFieldObject(state, playerId, objectId)
                && IsPromptAttackingBattlefieldObject(state, objectId),
            CardTargetScopes.EnemyBattlefieldUnit => IsPromptEnemyBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.EnemyUnit => IsPromptEnemyFieldObject(state, playerId, objectId),
            CardTargetScopes.EnemyUnitThenEnemyUnit => IsPromptEnemyFieldObject(state, playerId, objectId),
            CardTargetScopes.OpponentHandCard => false,
            CardTargetScopes.OpponentGraveyardCard => IsPromptOpponentGraveyardCard(state, playerId, objectId),
            CardTargetScopes.OpponentMainDeckTopCard => false,
            CardTargetScopes.AnyMainDeckTopFiveCard => false,
            CardTargetScopes.Equipment => IsPromptEquipmentObject(state, objectId),
            CardTargetScopes.Legend => IsPromptLegendObject(state, objectId),
            CardTargetScopes.StackSpell => IsPromptStackSpellItem(state, objectId),
            CardTargetScopes.SacredJudgmentKeepCard => false,
            _ => IsPromptBattlefieldObject(state, objectId)
        };
    }

    private static bool IsPromptBattlefieldObject(MatchState state, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && IsPromptKnownCardObject(state, objectId)
            && state.PlayerZones.Values.Any(zones => zones.Battlefields.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool IsPromptBaseObject(MatchState state, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && IsPromptKnownCardObject(state, objectId)
            && state.PlayerZones.Values.Any(zones => zones.Base.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool IsPromptFieldUnitObject(MatchState state, string objectId)
    {
        return (IsPromptBaseObject(state, objectId) || IsPromptBattlefieldObject(state, objectId))
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !cardObject.IsFaceDown;
    }

    private static bool IsPromptControlledFieldObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptFieldUnitObject(state, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && string.Equals(location.PlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool IsPromptEnemyFieldObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptFieldUnitObject(state, objectId)
            && IsPromptOpponentControlledFieldObject(state, playerId, objectId);
    }

    private static bool IsPromptControlledBattlefieldObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptBattlefieldObject(state, objectId)
            && IsPromptControlledFieldObject(state, playerId, objectId);
    }

    private static bool IsPromptEnemyBattlefieldObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptBattlefieldObject(state, objectId)
            && IsPromptEnemyFieldObject(state, playerId, objectId);
    }

    private static bool IsPromptControlledBaseObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptBaseObject(state, objectId)
            && IsPromptControlledFieldObject(state, playerId, objectId);
    }

    private static bool IsPromptEquipmentObject(MatchState state, string objectId)
    {
        return (IsPromptBaseObject(state, objectId) || IsPromptBattlefieldObject(state, objectId))
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !cardObject.IsFaceDown;
    }

    private static bool IsPromptLegendObject(MatchState state, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && IsPromptKnownCardObject(state, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && state.PlayerZones.Any(entry => entry.Value.LegendZone.Contains(objectId, StringComparer.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, entry.Key));
    }

    private static bool IsPromptFriendlyEquipmentObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptEquipmentObject(state, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && string.Equals(location.PlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool IsPromptEnemyEquipmentObject(MatchState state, string playerId, string objectId)
    {
        return IsPromptEquipmentObject(state, objectId)
            && IsPromptOpponentControlledFieldObject(state, playerId, objectId);
    }

    private static bool IsPromptOpponentControlledFieldObject(MatchState state, string playerId, string objectId)
    {
        return TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && !string.Equals(location.PlayerId, playerId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, location.PlayerId);
    }

    private static bool IsPromptFriendlyHandCard(MatchState state, string playerId, string objectId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Hand.Contains(objectId, StringComparer.Ordinal)
            && IsPromptKnownCardObjectControlledByPlayerOrLegacyOwned(state, playerId, objectId);
    }

    private static bool IsPromptHandCardControlledByPlayerOrLegacyOwned(
        MatchState state,
        string playerId,
        string objectId)
    {
        return !state.CardObjects.TryGetValue(objectId, out var cardObject)
            || SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static bool IsPromptFriendlyGraveyardCard(MatchState state, string playerId, string objectId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Graveyard.Contains(objectId, StringComparer.Ordinal)
            && IsPromptKnownCardObjectControlledByPlayerOrLegacyOwned(state, playerId, objectId);
    }

    private static bool IsPromptOpponentGraveyardCard(MatchState state, string playerId, string objectId)
    {
        return state.PlayerZones
            .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
            .Any(entry => entry.Value.Graveyard.Contains(objectId, StringComparer.Ordinal)
                && IsPromptKnownCardObjectControlledByPlayerOrLegacyOwned(state, entry.Key, objectId));
    }

    private static bool IsPromptKnownCardObject(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo);
    }

    private static bool IsPromptKnownCardObjectControlledByPlayerOrLegacyOwned(
        MatchState state,
        string playerId,
        string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static bool IsPromptAttackingBattlefieldObject(MatchState state, string objectId)
    {
        return IsPromptBattlefieldObject(state, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.IsAttacking;
    }

    private static bool IsPromptStackSpellItem(MatchState state, string objectId)
    {
        return state.StackItems.Any(item => string.Equals(item.StackItemId, objectId, StringComparison.Ordinal));
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? TargetsFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "PLAY_CARD" => PlayCardTargetChoices(state, playerId),
            "ACTIVATE_ABILITY" => ActivateAbilitySourceRequirements(state, playerId)
                .SelectMany(requirement => requirement.TargetChoicesByIndex.Values)
                .SelectMany(choices => choices)
                .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray(),
            "ASSEMBLE_EQUIPMENT" => AssembleEquipmentSourceRequirements(state, playerId)
                .SelectMany(requirement => requirement.TargetChoices)
                .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray(),
            "DECLARE_BATTLE" => DeclareBattleSourceRequirements(state, playerId)
                .SelectMany(requirement => requirement.TargetChoicesByIndex.Values)
                .SelectMany(choices => choices)
                .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray(),
            "LEGEND_ACT" => LegendActionSourceRequirements(state, playerId)
                .SelectMany(requirement => requirement.TargetChoicesByIndex.Values)
                .SelectMany(choices => choices)
                .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray(),
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? PlayCardTargetChoices(MatchState state, string playerId)
    {
        var choices = PlayablePlayCardBehaviors(state, playerId)
            .SelectMany(behavior =>
            {
                var targetCountConditionApplies = PromptTargetCountConditionApplies(state, playerId, behavior);
                var maxTargetCount = PromptMaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
                return Enumerable.Range(0, maxTargetCount)
                    .SelectMany(targetIndex => PromptTargetChoicesForIndex(state, playerId, behavior, targetIndex));
            })
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? DestinationsFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "PLAY_CARD" => PlayCardDestinationChoices(state, playerId),
            "HIDE_CARD" => [
                .. HideCardSourceRequirements(state, playerId)
                    .SelectMany(requirement => requirement.DestinationChoices)
                    .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                    .Select(group => group.First())
            ],
            "REVEAL_CARD" => [
                .. RevealCardSourceRequirements(state, playerId)
                    .SelectMany(requirement => requirement.DestinationChoices)
                    .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                    .Select(group => group.First())
            ],
            "MOVE_UNIT" => MoveUnitDestinationChoices(state, playerId),
            "DECLARE_BATTLE" => DeclareBattleDestinationChoices(state, playerId),
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? MoveUnitDestinationChoices(
        MatchState state,
        string playerId)
    {
        var choices = MoveUnitSourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.DestinationChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? PlayCardDestinationChoices(MatchState state, string playerId)
    {
        var choices = PlayablePlayCardBehaviors(state, playerId)
            .SelectMany(behavior => PlayCardDestinationChoicesForBehavior(state, playerId, behavior) ?? [])
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? DeclareBattleDestinationChoices(MatchState state, string playerId)
    {
        if (ResolutionResult.ActiveStartBattleTask(state) is { BattlefieldObjectId.Length: > 0 } activeStartBattleTask)
        {
            return IsPromptBattlefieldCardObject(state, activeStartBattleTask.BattlefieldObjectId)
                ? [ObjectChoice(state, activeStartBattleTask.BattlefieldObjectId, "当前争夺战场战斗任务")]
                : null;
        }

        var choices = new[]
            {
                new ActionPromptChoiceDto($"BATTLEFIELD:{playerId}-MAIN", "己方主战场", "默认战斗战场")
            }
            .Concat(PublicBattlefieldCardObjects(state)
                .Select(objectId => ObjectChoice(state, objectId, "公开战场牌")))
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? ModesFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "PLAY_CARD" => PlayCardModeChoices(state, playerId),
            "REVEAL_CARD" => RevealCardModeChoices(state, playerId),
            "ACTIVATE_ABILITY" => ActivateAbilityModeChoices(state, playerId),
            "LEGEND_ACT" => LegendActionModeChoices(state, playerId),
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? PlayCardModeChoices(MatchState state, string playerId)
    {
        var choices = PlayablePlayCardBehaviors(state, playerId)
            .Select(behavior => behavior.Mode)
            .Where(mode => !string.IsNullOrWhiteSpace(mode))
            .Distinct(StringComparer.Ordinal)
            .Select(mode => new ActionPromptChoiceDto(mode, PlayCardModeLabel(mode)))
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? RevealCardModeChoices(MatchState state, string playerId)
    {
        var choices = RevealCardSourceRequirements(state, playerId)
            .GroupBy(requirement => requirement.Mode, StringComparer.Ordinal)
            .Select(group => new ActionPromptChoiceDto(
                group.Key,
                group.First().ModeLabel,
                "server-filtered standby reveal mode"))
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? ActivateAbilityModeChoices(MatchState state, string playerId)
    {
        var choices = ActivateAbilitySourceRequirements(state, playerId)
            .GroupBy(requirement => requirement.AbilityId, StringComparer.Ordinal)
            .Select(group => new ActionPromptChoiceDto(
                group.Key,
                group.First().AbilityLabel,
                "server-filtered implemented ability"))
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? LegendActionModeChoices(MatchState state, string playerId)
    {
        var choices = LegendActionSourceRequirements(state, playerId)
            .GroupBy(requirement => requirement.AbilityId, StringComparer.Ordinal)
            .Select(group => new ActionPromptChoiceDto(
                group.Key,
                group.First().AbilityLabel,
                "server-filtered implemented legend ability"))
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? OptionalCostsFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "PLAY_CARD" => PlayCardOptionalCostChoices(state, playerId),
            "HIDE_CARD" => HideCardOptionalCostChoices(state, playerId),
            "REVEAL_CARD" => RevealCardOptionalCostChoices(state, playerId),
            "MOVE_UNIT" => MoveUnitOptionalCostChoices(state, playerId),
            "ASSEMBLE_EQUIPMENT" => AssembleEquipmentOptionalCostChoices(state, playerId),
            "DECLARE_BATTLE" => DeclareBattleOptionalCostChoices(state, playerId),
            "LEGEND_ACT" => LegendActionOptionalCostChoices(state, playerId),
            _ => null
        };
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

    private static IReadOnlyList<ActionPromptChoiceDto>? MoveUnitOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        var choices = MoveUnitSourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.OptionalCostChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? AssembleEquipmentOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        var choices = AssembleEquipmentSourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.OptionalCostChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? DeclareBattleOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        return DeclareBattleSourceRequirements(state, playerId).Count > 0
            ? [new ActionPromptChoiceDto("COMBAT_ASSIGNMENT", "战斗分配", "服务端当前代表路径必需")]
            : null;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? LegendActionOptionalCostChoices(
        MatchState state,
        string playerId)
    {
        var choices = LegendActionSourceRequirements(state, playerId)
            .SelectMany(requirement => requirement.OptionalCostChoices)
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? PlayCardOptionalCostChoices(MatchState state, string playerId)
    {
        var choices = PlayablePlayCardBehaviorSources(state, playerId)
            .SelectMany(entry => PlayCardOptionalCostChoicesForBehavior(
                state,
                playerId,
                entry.Behavior,
                entry.SourceObjectId))
            .GroupBy(choice => choice.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return choices.Length == 0 ? null : choices;
    }

    private static IReadOnlyList<DeclareBattlePromptRequirement> DeclareBattleSourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, playerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0
            || !state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        var activeStartBattleTask = ResolutionResult.ActiveStartBattleTask(state);
        var activeBattlefieldObjectId = activeStartBattleTask?.BattlefieldObjectId ?? string.Empty;
        var defenderCandidates = OpposingBattlefieldObjects(state, playerId)
            .Where(entry => string.IsNullOrWhiteSpace(activeBattlefieldObjectId)
                || IsObjectLocatedAtBattlefield(state, entry.ObjectId, activeBattlefieldObjectId))
            .Where(entry => IsReadyFaceUpBattlefieldUnitForBattle(state, entry.PlayerId, entry.ObjectId))
            .Select(entry => new
            {
                entry.ObjectId,
                Choice = ObjectChoice(state, entry.ObjectId, "服务端合法防守单位"),
                SupportsMultiDefenderAssignment = HasBattleDamageAssignmentKeyword(state.CardObjects[entry.ObjectId].Tags)
            })
            .ToArray();
        var defenderChoices = defenderCandidates
            .Select(entry => entry.Choice)
            .ToArray();
        var assignmentDefenderChoices = defenderCandidates
            .Where(entry => entry.SupportsMultiDefenderAssignment)
            .Select(entry => entry.Choice)
            .ToArray();
        var battlefieldChoices = string.IsNullOrWhiteSpace(activeBattlefieldObjectId)
            ? DeclareBattleDestinationChoices(state, playerId)?.ToArray() ?? []
            : IsPromptBattlefieldCardObject(state, activeBattlefieldObjectId)
                ? [ObjectChoice(state, activeBattlefieldObjectId, "当前争夺战场战斗任务")]
                : [];
        if (defenderChoices.Length == 0 || battlefieldChoices.Length == 0)
        {
            return [];
        }

        var attackerCandidates = zones.Battlefields
            .Where(objectId => string.IsNullOrWhiteSpace(activeBattlefieldObjectId)
                || IsObjectLocatedAtBattlefield(state, objectId, activeBattlefieldObjectId))
            .Where(objectId => IsReadyFaceUpBattlefieldUnitForBattle(state, playerId, objectId))
            .Where(objectId => state.CardObjects.ContainsKey(objectId))
            .Select(objectId => new
            {
                ObjectId = objectId,
                CardObject = state.CardObjects[objectId],
                Choice = ObjectChoice(state, objectId, "服务端合法攻击单位")
            })
            .ToArray();

        return attackerCandidates
            .Select(attacker =>
            {
                var alternateAttackerChoices = attackerCandidates
                    .Where(candidate => !string.Equals(candidate.ObjectId, attacker.ObjectId, StringComparison.Ordinal))
                    .Select(candidate => candidate.Choice)
                    .ToArray();
                var maxAttackerCount = alternateAttackerChoices.Length > 0 ? 2 : 1;
                var attackerChoicesByIndex = new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
                {
                    ["0"] = [attacker.Choice]
                };
                if (maxAttackerCount > 1)
                {
                    attackerChoicesByIndex["1"] = alternateAttackerChoices;
                }

                var maxDefenderCount = defenderChoices.Length > 1 && assignmentDefenderChoices.Length > 0 ? 2 : 1;
                var secondDefenderChoices = defenderChoices.Length == 2 && assignmentDefenderChoices.Length == 1
                    ? defenderChoices
                    : assignmentDefenderChoices;
                var targetChoicesByIndex = new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal)
                {
                    ["0"] = defenderChoices
                };
                if (maxDefenderCount > 1)
                {
                    targetChoicesByIndex["1"] = secondDefenderChoices;
                }

                return new DeclareBattlePromptRequirement(
                    attacker.ObjectId,
                    attacker.CardObject.CardNo ?? string.Empty,
                    attacker.Choice.Label,
                    1,
                    maxAttackerCount,
                    maxAttackerCount > 1 ? "1 个，或 2 个攻击单位" : "1 个攻击单位",
                    attackerChoicesByIndex,
                    1,
                    maxDefenderCount,
                    maxDefenderCount > 1 ? "1 个，或含壁垒/后排的 2 个" : "1 个防守单位",
                    targetChoicesByIndex,
                    battlefieldChoices,
                    [new ActionPromptChoiceDto("COMBAT_ASSIGNMENT", "战斗分配", "服务端当前代表路径必需")],
                    ["COMBAT_ASSIGNMENT"],
                    true,
                    null);
            })
            .ToArray();
    }

    private static bool IsObjectLocatedAtBattlefield(
        MatchState state,
        string objectId,
        string battlefieldObjectId)
    {
        return state.ObjectLocations.TryGetValue(objectId, out var location)
            && string.Equals(location.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            && string.Equals(location.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal);
    }

    private static IEnumerable<CardBehaviorDefinition> PlayablePlayCardBehaviors(MatchState state, string playerId)
    {
        return PlayablePlayCardBehaviorSources(state, playerId).Select(entry => entry.Behavior);
    }

    private static IEnumerable<(string SourceObjectId, CardBehaviorDefinition Behavior)> PlayablePlayCardBehaviorSources(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Hand
            .Where(objectId => IsImplementedPlayableHandSource(state, playerId, objectId))
            .SelectMany(objectId => PlayCardPromptBehaviorsForSource(state, playerId, objectId)
                .Select(behavior => (SourceObjectId: objectId, Behavior: behavior)));
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PlayCardOptionalCostChoicesForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId = null)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var paymentResourceChoices = PlayCardPaymentResourceChoicesForBehavior(state, playerId, behavior);
        var paymentResourcePowerByTrait = PlayCardPaymentResourcePowerByTraitForBehavior(state, playerId, behavior);
        var effectivePowerWithRecycle = runePool.TotalPower + paymentResourcePowerByTrait.Values.Sum();
        var hasteReadyPowerTrait = HasteReadyPowerTrait(behavior);
        var experience = state.PlayerExperience.TryGetValue(playerId, out var currentExperience)
            ? currentExperience
            : 0;
        var choices = new List<ActionPromptChoiceDto>();

        if (TryPromptEchoOptionalCost(state, playerId, behavior, out var effectiveEchoManaCost, out var echoReason)
            && runePool.Mana >= PromptMinimumManaCost(state, playerId, behavior, sourceObjectId) + effectiveEchoManaCost)
        {
            choices.Add(new ActionPromptChoiceDto(
                "ECHO",
                $"回响：额外支付 {effectiveEchoManaCost} 法力",
                echoReason));
        }

        if ((behavior.HasteReadyManaCost > 0 || behavior.HasteReadyPowerCost > 0)
            && runePool.Mana >= PromptMinimumManaCost(state, playerId, behavior, sourceObjectId) + behavior.HasteReadyManaCost
            && CanPayHasteReadyPowerCost(runePool, paymentResourcePowerByTrait, behavior))
        {
            var powerLabel = string.IsNullOrWhiteSpace(hasteReadyPowerTrait)
                ? $"{behavior.HasteReadyPowerCost} 符能"
                : $"{behavior.HasteReadyPowerCost} {RuneTraitLabel(hasteReadyPowerTrait)}符能";
            choices.Add(new ActionPromptChoiceDto(
                HasteOptionalCostNames.HasteReady,
                $"急速活跃：额外支付 {behavior.HasteReadyManaCost} 法力 / {powerLabel}"));
        }

        if (CanPromptCrescentGuardReadyOptionalCost(state, playerId, behavior)
            && CanPayCrescentGuardReadyPowerCost(runePool, paymentResourcePowerByTrait))
        {
            choices.Add(new ActionPromptChoiceDto(
                $"SPEND_POWER:{RuneTrait.Purple}:{CrescentGuardReadyPowerCost}",
                $"新月禁卫活跃：支付 {CrescentGuardReadyPowerCost} 紫色符能",
                "本回合已打出过法术"));
        }

        if (behavior.OptionalExperienceCost > 0
            && behavior.ManaReductionIfExperiencePaid > 0
            && experience >= behavior.OptionalExperienceCost)
        {
            choices.Add(new ActionPromptChoiceDto(
                $"SPEND_EXPERIENCE:{behavior.OptionalExperienceCost}",
                $"支付 {behavior.OptionalExperienceCost} 经验：费用减少 {behavior.ManaReductionIfExperiencePaid}"));
        }

        if (behavior.SourceBoonAdditionalManaCost > 0
            && runePool.Mana >= PromptMinimumManaCost(state, playerId, behavior, sourceObjectId) + behavior.SourceBoonAdditionalManaCost)
        {
            choices.Add(new ActionPromptChoiceDto(
                $"SPEND_MANA:{behavior.SourceBoonAdditionalManaCost}",
                $"额外支付 {behavior.SourceBoonAdditionalManaCost} 法力：给予我增益",
                $"{behavior.DisplayName}的可选额外费用"));
        }

        if (CanPaySourceDrawOptionalPowerCost(runePool, paymentResourcePowerByTrait, behavior))
        {
            var sourceDrawPowerTrait = RuneTrait.Normalize(behavior.SourceDrawAdditionalPowerTrait);
            choices.Add(new ActionPromptChoiceDto(
                $"SPEND_POWER:{sourceDrawPowerTrait}:{behavior.SourceDrawAdditionalPowerCost}",
                $"额外支付 {behavior.SourceDrawAdditionalPowerCost} {RuneTraitLabel(sourceDrawPowerTrait)}符能：抽 {behavior.SourceDrawCountIfOptionalPowerCostPaid} 张牌",
                $"{behavior.DisplayName}的可选额外费用"));
        }

        if (CanPaySourceReadyPowerModifierOptionalCost(runePool, paymentResourcePowerByTrait, behavior))
        {
            var sourceReadyPowerTrait = RuneTrait.Normalize(behavior.SourceReadyPowerModifierAdditionalPowerTrait);
            choices.Add(new ActionPromptChoiceDto(
                $"SPEND_POWER:{sourceReadyPowerTrait}:{behavior.SourceReadyPowerModifierAdditionalPowerCost}",
                $"额外支付 {behavior.SourceReadyPowerModifierAdditionalPowerCost} {RuneTraitLabel(sourceReadyPowerTrait)}符能：活跃并本回合战力 +{behavior.SourceReadyPowerModifierAmount}",
                $"{behavior.DisplayName}的可选额外费用"));
        }

        if (CanPromptTargetEffectAdditionalCost(state, playerId, runePool, paymentResourcePowerByTrait, behavior, sourceObjectId))
        {
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

        if (behavior.DamageAmountFromOptionalPowerCost && effectivePowerWithRecycle > 0)
        {
            for (var amount = 1; amount <= effectivePowerWithRecycle; amount++)
            {
                choices.Add(new ActionPromptChoiceDto(
                    $"SPEND_POWER:{amount}",
                    $"支付 {amount} 符能"));
            }

            foreach (var entry in PlayCardAvailablePowerByTrait(runePool, paymentResourcePowerByTrait))
            {
                for (var amount = 1; amount <= entry.Value; amount++)
                {
                    choices.Add(new ActionPromptChoiceDto(
                        $"SPEND_POWER:{entry.Key}:{amount}",
                        $"支付 {amount} {RuneTraitLabel(entry.Key)}符能"));
                }
            }
        }

        choices.AddRange(paymentResourceChoices);
        return choices;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PlayCardDiscardHandManaReductionOptionalCostChoices(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        string? sourceObjectId)
    {
        if (behavior.ManaReductionIfDiscardHandCardOptionalCost <= 0
            || !state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Hand
            .Where(objectId => CanPromptDiscardHandCardAsOptionalCost(state, playerId, sourceObjectId, objectId))
            .Select(objectId => new ActionPromptChoiceDto(
                $"DISCARD_HAND_CARD:{objectId}",
                $"弃置 {PromptHandCardLabel(state, objectId)}：费用减少 {behavior.ManaReductionIfDiscardHandCardOptionalCost}",
                $"{behavior.DisplayName}的可选额外费用"))
            .ToArray();
    }

    private static string PromptHandCardLabel(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            ? cardObject.CardNo
            : "一张手牌";
    }

    private static bool TryPromptEchoOptionalCost(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        out int effectiveEchoManaCost,
        out string? reason)
    {
        effectiveEchoManaCost = 0;
        reason = null;
        if (PromptBattlefieldHeldNextSpellEchoActive(state, playerId)
            && IsPromptSpellPlayBehavior(behavior))
        {
            effectiveEchoManaCost = behavior.ManaCost;
            reason = "战场效果授予此法术回响";
            return true;
        }

        if (behavior.EchoManaCost <= 0)
        {
            return false;
        }

        var reduction = PromptBattlefieldEchoCostReductionMana(state, playerId, behavior.EchoManaCost);
        effectiveEchoManaCost = Math.Max(0, behavior.EchoManaCost - reduction);
        reason = reduction > 0 ? $"战场效果已减免 {reduction} 法力" : null;
        return true;
    }

    private static bool IsPromptSpellPlayBehavior(CardBehaviorDefinition behavior)
    {
        return !behavior.PlaysSourceToBaseAsUnit
            && !behavior.PlaysSourceToBaseAsEquipment;
    }

    private static bool PromptBattlefieldHeldNextSpellEchoActive(MatchState state, string playerId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            $"BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:{playerId}",
            StringComparer.Ordinal);
    }

    private static bool PromptPlayerPlayedSpellThisTurn(MatchState state, string playerId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            $"{PlayedSpellThisTurnEffectPrefix}{playerId}",
            StringComparer.Ordinal);
    }

    private static int PromptBattlefieldEchoCostReductionMana(
        MatchState state,
        string playerId,
        int echoManaCost)
    {
        if (echoManaCost <= 0
            || !state.PlayerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, BattlefieldEchoCostReductionCardNo, StringComparison.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)))
        {
            return 0;
        }

        return Math.Min(1, echoManaCost);
    }

    private static int PromptBattlefieldEquipmentCostReductionMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (behavior.ManaCost <= 0
            || !behavior.PlaysSourceToBaseAsEquipment
            || state.UntilEndOfTurnEffects.Contains($"{PlayedEquipmentThisTurnEffectPrefix}{playerId}", StringComparer.Ordinal)
            || !state.PlayerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, BattlefieldEquipmentCostReductionCardNo, StringComparison.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)))
        {
            return 0;
        }

        return Math.Min(1, behavior.ManaCost);
    }

    private static int PromptNextSpellCostReductionMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        int alreadyAppliedBaseReductionMana)
    {
        if (!IsPromptSpellPlayBehavior(behavior)
            || behavior.ManaCost <= 0)
        {
            return 0;
        }

        var effectPrefix = $"{RagingDrakeNextSpellCostReductionEffectPrefix}{playerId}:";
        var effectCount = state.UntilEndOfTurnEffects
            .Count(effectId => effectId.StartsWith(effectPrefix, StringComparison.Ordinal));
        if (effectCount == 0)
        {
            return 0;
        }

        var baseManaAfterExistingReductions = Math.Max(0, behavior.ManaCost - alreadyAppliedBaseReductionMana);
        return Math.Min(
            effectCount * RagingDrakeNextSpellCostReductionMana,
            baseManaAfterExistingReductions);
    }

    private static int PromptBattlefieldSpellCostReductionMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        int alreadyAppliedBaseReductionMana)
    {
        if (!IsPromptSpellPlayBehavior(behavior)
            || behavior.ManaCost <= 1
            || !state.PlayerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, EagerApprenticeCardNo, StringComparison.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
                && !cardObject.IsFaceDown))
        {
            return 0;
        }

        var baseManaAfterExistingReductions = Math.Max(0, behavior.ManaCost - alreadyAppliedBaseReductionMana);
        return baseManaAfterExistingReductions > 1
            ? Math.Min(1, baseManaAfterExistingReductions - 1)
            : 0;
    }

    private static int PromptBattlefieldHeldUnitCostIncreaseMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (!behavior.PlaysSourceToBaseAsUnit
            || behavior.ManaCost <= 0
            || P6TokenFactoryCatalog.TryGetByCardNo(behavior.CardNo, out _)
            || !state.UntilEndOfTurnEffects.Contains(
                $"{BattlefieldHeldUnitCostIncreaseEffectPrefix}{playerId}",
                StringComparer.Ordinal))
        {
            return 0;
        }

        return 1;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> PlayCardPaymentResourceChoicesForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (!PlayCardBehaviorMayNeedPaymentResource(state, playerId, behavior))
        {
            return [];
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var needsPaymentResource = behavior.DamageAmountFromOptionalPowerCost
            || NeedsSourceDrawOptionalPowerPaymentResource(runePool, behavior)
            || NeedsSourceReadyPowerModifierPaymentResource(runePool, behavior)
            || NeedsTargetEffectAdditionalPowerPaymentResource(runePool, behavior)
            || NeedsHasteReadyPaymentResource(runePool, behavior)
            || NeedsCrescentGuardReadyPaymentResource(state, playerId, runePool, behavior);
        if (!needsPaymentResource)
        {
            return [];
        }

        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Where(objectId => IsRecycleRuneSource(state, playerId, objectId))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .Select(objectId =>
            {
                var choice = ObjectChoice(state, objectId, "payment resource action: recycle rune for trait power");
                return new ActionPromptChoiceDto(
                    $"{RecycleRunePaymentOptionalCostPrefix}{objectId}",
                    $"回收符文支付：{choice.Label}",
                    choice.Reason);
            })
            .ToArray();
    }

    private static IReadOnlyDictionary<string, int> PlayCardPaymentResourcePowerByTraitForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (!PlayCardBehaviorMayNeedPaymentResource(state, playerId, behavior))
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var needsPaymentResource = behavior.DamageAmountFromOptionalPowerCost
            || NeedsSourceDrawOptionalPowerPaymentResource(runePool, behavior)
            || NeedsSourceReadyPowerModifierPaymentResource(runePool, behavior)
            || NeedsTargetEffectAdditionalPowerPaymentResource(runePool, behavior)
            || NeedsHasteReadyPaymentResource(runePool, behavior)
            || NeedsCrescentGuardReadyPaymentResource(state, playerId, runePool, behavior);
        if (!needsPaymentResource
            || !state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        return zones.Base
            .Where(objectId => IsRecycleRuneSource(state, playerId, objectId))
            .Select(objectId => state.CardObjects[objectId])
            .Select(runeState => TryGetRuneTrait(runeState, out var runeTrait) ? runeTrait : string.Empty)
            .Where(trait => !string.IsNullOrWhiteSpace(trait))
            .GroupBy(trait => trait, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count() * BasicRuneRecyclePowerGain,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> PlayCardAvailablePowerByTrait(
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait)
    {
        return runePool.PowerByTrait
            .Concat(paymentResourcePowerByTrait)
            .Where(entry => entry.Value > 0)
            .GroupBy(entry => RuneTrait.Normalize(entry.Key), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(entry => entry.Value),
                StringComparer.Ordinal);
    }

    private static bool NeedsHasteReadyPaymentResource(
        RunePool runePool,
        CardBehaviorDefinition behavior)
    {
        return behavior.HasteReadyPowerCost > 0
            && !CanPayHasteReadyPowerCost(
                runePool,
                new Dictionary<string, int>(StringComparer.Ordinal),
            behavior);
    }

    private static bool NeedsSourceDrawOptionalPowerPaymentResource(
        RunePool runePool,
        CardBehaviorDefinition behavior)
    {
        return behavior.SourceDrawAdditionalPowerCost > 0
            && behavior.SourceDrawCountIfOptionalPowerCostPaid > 0
            && !CanPaySourceDrawOptionalPowerCost(
                runePool,
                new Dictionary<string, int>(StringComparer.Ordinal),
                behavior);
    }

    private static bool NeedsSourceReadyPowerModifierPaymentResource(
        RunePool runePool,
        CardBehaviorDefinition behavior)
    {
        return behavior.SourceReadyPowerModifierAdditionalPowerCost > 0
            && behavior.SourceReadyPowerModifierAmount != 0
            && !CanPaySourceReadyPowerModifierOptionalCost(
                runePool,
                new Dictionary<string, int>(StringComparer.Ordinal),
                behavior);
    }

    private static bool NeedsTargetEffectAdditionalPowerPaymentResource(
        RunePool runePool,
        CardBehaviorDefinition behavior)
    {
        return behavior.TargetEffectAdditionalPowerCost > 0
            && !CanPayTargetEffectAdditionalPowerCost(
                runePool,
                new Dictionary<string, int>(StringComparer.Ordinal),
                behavior);
    }

    private static bool NeedsCrescentGuardReadyPaymentResource(
        MatchState state,
        string playerId,
        RunePool runePool,
        CardBehaviorDefinition behavior)
    {
        return CanPromptCrescentGuardReadyOptionalCost(state, playerId, behavior)
            && !CanPayCrescentGuardReadyPowerCost(
                runePool,
                new Dictionary<string, int>(StringComparer.Ordinal));
    }

    private static bool CanPayHasteReadyPowerCost(
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait,
        CardBehaviorDefinition behavior)
    {
        if (behavior.HasteReadyPowerCost <= 0)
        {
            return true;
        }

        var hasteReadyPowerTrait = HasteReadyPowerTrait(behavior);
        if (string.IsNullOrWhiteSpace(hasteReadyPowerTrait))
        {
            return runePool.TotalPower + paymentResourcePowerByTrait.Values.Sum() >= behavior.HasteReadyPowerCost;
        }

        var availablePowerByTrait = PlayCardAvailablePowerByTrait(runePool, paymentResourcePowerByTrait);
        return availablePowerByTrait.TryGetValue(hasteReadyPowerTrait, out var availablePower)
            && availablePower >= behavior.HasteReadyPowerCost;
    }

    private static bool CanPaySourceDrawOptionalPowerCost(
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait,
        CardBehaviorDefinition behavior)
    {
        if (behavior.SourceDrawAdditionalPowerCost <= 0
            || behavior.SourceDrawCountIfOptionalPowerCostPaid <= 0)
        {
            return false;
        }

        var sourceDrawPowerTrait = RuneTrait.Normalize(behavior.SourceDrawAdditionalPowerTrait);
        if (string.IsNullOrWhiteSpace(sourceDrawPowerTrait))
        {
            return false;
        }

        var availablePowerByTrait = PlayCardAvailablePowerByTrait(runePool, paymentResourcePowerByTrait);
        return availablePowerByTrait.TryGetValue(sourceDrawPowerTrait, out var availablePower)
            && availablePower >= behavior.SourceDrawAdditionalPowerCost;
    }

    private static bool CanPaySourceReadyPowerModifierOptionalCost(
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

        var availablePowerByTrait = PlayCardAvailablePowerByTrait(runePool, paymentResourcePowerByTrait);
        return availablePowerByTrait.TryGetValue(sourceReadyPowerTrait, out var availablePower)
            && availablePower >= behavior.SourceReadyPowerModifierAdditionalPowerCost;
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
            && runePool.Mana >= PromptMinimumManaCost(state, playerId, behavior, sourceObjectId)
                + behavior.TargetEffectAdditionalManaCost
            && CanPayTargetEffectAdditionalPowerCost(runePool, paymentResourcePowerByTrait, behavior)
            && PromptTargetChoicesForIndex(state, playerId, behavior, 0).Count > 0;
    }

    private static bool PromptTargetEffectAdditionalCostAvailable(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var paymentResourcePowerByTrait = PlayCardPaymentResourcePowerByTraitForBehavior(state, playerId, behavior);
        return CanPromptTargetEffectAdditionalCost(
            state,
            playerId,
            runePool,
            paymentResourcePowerByTrait,
            behavior,
            null);
    }

    private static bool CanPayTargetEffectAdditionalPowerCost(
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait,
        CardBehaviorDefinition behavior)
    {
        if (behavior.TargetEffectAdditionalPowerCost <= 0)
        {
            return false;
        }

        var targetEffectPowerTrait = RuneTrait.Normalize(behavior.TargetEffectAdditionalPowerTrait);
        if (string.IsNullOrWhiteSpace(targetEffectPowerTrait))
        {
            return false;
        }

        var availablePowerByTrait = PlayCardAvailablePowerByTrait(runePool, paymentResourcePowerByTrait);
        return availablePowerByTrait.TryGetValue(targetEffectPowerTrait, out var availablePower)
            && availablePower >= behavior.TargetEffectAdditionalPowerCost;
    }

    private static string TargetEffectAdditionalManaCostLabel(CardBehaviorDefinition behavior)
    {
        var companionCost = behavior.TargetEffectAdditionalPowerCost > 0
            ? $"，需同时支付 {behavior.TargetEffectAdditionalPowerCost} {RuneTraitLabel(RuneTrait.Normalize(behavior.TargetEffectAdditionalPowerTrait))}符能"
            : string.Empty;
        return $"额外支付 {behavior.TargetEffectAdditionalManaCost} 法力{companionCost}";
    }

    private static string TargetEffectAdditionalPowerCostLabel(
        CardBehaviorDefinition behavior,
        string powerTrait)
    {
        var companionCost = behavior.TargetEffectAdditionalManaCost > 0
            ? $"，需同时支付 {behavior.TargetEffectAdditionalManaCost} 法力"
            : string.Empty;
        var effectLabel = behavior.PowerModifierAmount != 0
            ? $"一名单位本回合战力 {FormatSignedNumber(behavior.PowerModifierAmount)}"
            : behavior.DamageAmount > 0
                ? $"对一名单位造成 {behavior.DamageAmount} 点伤害"
                : "执行目标效果";
        return $"额外支付 {behavior.TargetEffectAdditionalPowerCost} {RuneTraitLabel(powerTrait)}符能{companionCost}：{effectLabel}";
    }

    private static string FormatSignedNumber(int value)
    {
        return value > 0
            ? $"+{value}"
            : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool CanPayCrescentGuardReadyPowerCost(
        RunePool runePool,
        IReadOnlyDictionary<string, int> paymentResourcePowerByTrait)
    {
        var availablePowerByTrait = PlayCardAvailablePowerByTrait(runePool, paymentResourcePowerByTrait);
        return availablePowerByTrait.TryGetValue(RuneTrait.Purple, out var availablePower)
            && availablePower >= CrescentGuardReadyPowerCost;
    }

    private static bool CanPromptCrescentGuardReadyOptionalCost(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return string.Equals(behavior.CardNo, CrescentGuardCardNo, StringComparison.Ordinal)
            && PromptPlayerPlayedSpellThisTurn(state, playerId);
    }

    private static bool PlayCardBehaviorMayNeedPaymentResource(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.DamageAmountFromOptionalPowerCost
            || behavior.SourceDrawAdditionalPowerCost > 0
            || behavior.SourceReadyPowerModifierAdditionalPowerCost > 0
            || behavior.TargetEffectAdditionalPowerCost > 0
            || behavior.HasteReadyPowerCost > 0
            || CanPromptCrescentGuardReadyOptionalCost(state, playerId, behavior);
    }

    private static string HasteReadyPowerTrait(CardBehaviorDefinition behavior)
    {
        return RuneTrait.Normalize(behavior.HasteReadyPowerTrait);
    }

    private static bool TryGetRuneTrait(CardObjectState runeState, out string runeTrait)
    {
        foreach (var tag in runeState.Tags)
        {
            if (!tag.StartsWith("COLOR:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var trait = RuneTrait.Normalize(tag["COLOR:".Length..]);
            if (!string.IsNullOrWhiteSpace(trait))
            {
                runeTrait = trait;
                return true;
            }
        }

        runeTrait = string.Empty;
        return false;
    }

    private static string RuneTraitLabel(string trait)
    {
        return trait switch
        {
            RuneTrait.Red => "红色",
            RuneTrait.Green => "绿色",
            RuneTrait.Blue => "蓝色",
            RuneTrait.Yellow => "黄色",
            RuneTrait.Orange => "橙色",
            RuneTrait.Purple => "紫色",
            _ => trait
        };
    }

    private static string PlayCardModeLabel(string mode)
    {
        return mode switch
        {
            "AMBUSH" => "伏击",
            "HASTE_READY" => "急速活跃",
            "READY_LEGEND" => "活跃传奇",
            "EXHAUST_LEGEND" => "休眠传奇",
            "BATTLEFIELD_UNIT_POWER_MINUS_4" => "战场单位战力 -4",
            "DRAW_1" => "抽 1 张",
            "SELF_BOON" => "给予我增益",
            "DAMAGE_2" => "造成 2 点伤害",
            _ => string.IsNullOrWhiteSpace(mode) ? "默认" : mode
        };
    }

    private static IReadOnlyDictionary<string, object?>? MetadataFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "MULLIGAN" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "opening-hand-cards",
                ["minSelectionCount"] = 0,
                ["maxSelectionCount"] = OfficialDeckValidator.MaximumMulliganCount
            },
            "PLAY_CARD" => PlayCardMetadataFor(state, playerId),
            "HIDE_CARD" => HideCardMetadataFor(state, playerId),
            "REVEAL_CARD" => RevealCardMetadataFor(state, playerId),
            "TAP_RUNE" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "ready-controlled-base-rune",
                ["resourceGain"] = "1-mana"
            },
            "RECYCLE_RUNE" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "controlled-trait-base-rune",
                ["resourceGain"] = "1-matching-trait-power",
                ["destination"] = "rune-deck-bottom"
            },
            "ACTIVATE_ABILITY" => ActivateAbilityMetadataFor(state, playerId),
            "MOVE_UNIT" => MoveUnitMetadataFor(state, playerId),
            "ASSEMBLE_EQUIPMENT" => AssembleEquipmentMetadataFor(state, playerId),
            "DECLARE_BATTLE" => DeclareBattleMetadataFor(state, playerId),
            "LEGEND_ACT" => LegendActionMetadataFor(state, playerId),
            _ => null
        };
    }

    private static IReadOnlyDictionary<string, object?> HideCardMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = HideCardSourceRequirements(state, playerId)
            .Select(HideCardSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-payable-standby-card-only",
            ["destinationPolicy"] = "source-specific-server-filtered-standby-destinations",
            ["optionalCostPolicy"] = "source-specific-server-filtered-standby-costs",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> RevealCardMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = RevealCardSourceRequirements(state, playerId)
            .Select(RevealCardSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "face-down-standby-card-only",
            ["modePolicy"] = "source-specific-server-filtered-standby-reveal-mode",
            ["destinationPolicy"] = "source-specific-server-filtered-standby-reveal-destination",
            ["optionalCostPolicy"] = "source-specific-required-standby-reveal-cost",
            ["sourceRequirements"] = sourceRequirements
        };
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
            ["optionalCostPolicy"] = "source-specific-required-combat-assignment",
            ["attackerCount"] = 1,
            ["attackerCountMin"] = 1,
            ["attackerCountMax"] = 2,
            ["defenderCountMin"] = 1,
            ["defenderCountMax"] = 2,
            ["multiAttackerPolicy"] = "up-to-two-attackers-representative-path",
            ["multiDefenderPolicy"] = "up-to-two-defenders-requires-assignment-keyword-representative-path",
            ["multiParticipantBattlePolicy"] = "up-to-two-attackers-and-defenders-without-independent-assignment-prompt",
            ["samePriorityAssignmentPolicy"] = "preserve-player-submitted-object-order-within-same-priority",
            ["candidateFiltering"] = "battlefield-zone-controlled-ready-face-up-units-not-already-in-combat",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> ActivateAbilityMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = ActivateAbilitySourceRequirements(state, playerId)
            .Select(ActivateAbilitySourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-activated-ability-only",
            ["abilityPolicy"] = "source-specific-server-filtered-abilities",
            ["targetPolicy"] = "source-specific-server-filtered-targets",
            ["optionalCostPolicy"] = "source-specific-server-filtered-costs",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> LegendActionMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = LegendActionSourceRequirements(state, playerId)
            .Select(LegendActionSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-legend-action-only",
            ["abilityPolicy"] = "source-specific-server-filtered-legend-abilities",
            ["targetPolicy"] = "source-specific-server-filtered-targets",
            ["optionalCostPolicy"] = "source-specific-server-filtered-costs",
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
            ["battlefieldChoices"] = requirement.BattlefieldChoices,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["requiredOptionalCosts"] = requirement.RequiredOptionalCosts,
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };
    }

    private static IReadOnlyDictionary<string, object?> LegendActionSourceRequirementView(
        LegendActionPromptRequirement requirement)
    {
        return new Dictionary<string, object?>
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
    }

    private static IReadOnlyDictionary<string, object?> ActivateAbilitySourceRequirementView(
        ActivateAbilityPromptRequirement requirement)
    {
        return new Dictionary<string, object?>
        {
            ["sourceObjectId"] = requirement.SourceObjectId,
            ["cardNo"] = requirement.CardNo,
            ["abilityId"] = requirement.AbilityId,
            ["displayName"] = requirement.DisplayName,
            ["abilityLabel"] = requirement.AbilityLabel,
            ["manaCost"] = requirement.ManaCost,
            ["powerCost"] = requirement.PowerCost,
            ["minTargetCount"] = requirement.MinTargetCount,
            ["maxTargetCount"] = requirement.MaxTargetCount,
            ["targetCountLabel"] = requirement.MinTargetCount == requirement.MaxTargetCount
                ? requirement.MaxTargetCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : $"{requirement.MinTargetCount}-{requirement.MaxTargetCount}",
            ["targetScopeLabel"] = requirement.TargetScopeLabel,
            ["targetChoicesByIndex"] = requirement.TargetChoicesByIndex,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["requiredOptionalCosts"] = requirement.RequiredOptionalCosts,
            ["exhaustsSource"] = requirement.ExhaustsSource,
            ["resolvesImmediately"] = requirement.ResolvesImmediately,
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };
    }

    private static IReadOnlyDictionary<string, object?> MoveUnitMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = MoveUnitSourceRequirements(state, playerId)
            .Select(MoveUnitSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-face-up-controlled-non-combat-unit",
            ["destinationPolicy"] = "source-specific-server-filtered-destinations",
            ["optionalCostPolicy"] = "source-specific-server-filtered-costs",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> AssembleEquipmentMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = AssembleEquipmentSourceRequirements(state, playerId)
            .Select(AssembleEquipmentSourceRequirementView)
            .ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-unattached-controlled-base-equipment",
            ["targetPolicy"] = "source-specific-controlled-unit-hosts",
            ["optionalCostPolicy"] = "source-specific-required-assemble-costs",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IReadOnlyDictionary<string, object?> AssembleEquipmentSourceRequirementView(
        AssembleEquipmentPromptRequirement requirement)
    {
        return new Dictionary<string, object?>
        {
            ["sourceObjectId"] = requirement.SourceObjectId,
            ["equipmentCardNo"] = requirement.EquipmentCardNo,
            ["displayName"] = requirement.DisplayName,
            ["targetChoices"] = requirement.TargetChoices,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["paymentResourceChoices"] = requirement.PaymentResourceChoices,
            ["paymentResourcePowerByChoice"] = requirement.PaymentResourcePowerByChoice,
            ["availablePowerByTrait"] = requirement.AvailablePowerByTrait,
            ["availablePowerByTraitWithPaymentResources"] = requirement.AvailablePowerByTraitWithPaymentResources,
            ["requiredOptionalCosts"] = requirement.RequiredOptionalCosts,
            ["powerCost"] = requirement.PowerCost,
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };
    }

    private static IReadOnlyDictionary<string, object?> MoveUnitSourceRequirementView(
        MoveUnitPromptRequirement requirement)
    {
        return new Dictionary<string, object?>
        {
            ["sourceObjectId"] = requirement.SourceObjectId,
            ["origin"] = requirement.Origin,
            ["originLabel"] = requirement.OriginLabel,
            ["mode"] = requirement.Mode,
            ["modeLabel"] = requirement.ModeLabel,
            ["destinationChoices"] = requirement.DestinationChoices,
            ["optionalCostChoices"] = requirement.OptionalCostChoices,
            ["requiredOptionalCosts"] = requirement.RequiredOptionalCosts,
            ["composable"] = requirement.Composable,
            ["unsupportedReason"] = requirement.UnsupportedReason
        };
    }

    private static IReadOnlyDictionary<string, object?> PlayCardMetadataFor(MatchState state, string playerId)
    {
        var sourceRequirements = PlayCardSourceRequirements(state, playerId).ToArray();
        return new Dictionary<string, object?>
        {
            ["sourcePolicy"] = "implemented-card-behavior-only",
            ["targetPolicy"] = "source-specific-server-filtered-targets",
            ["modePolicy"] = "source-specific-server-filtered-modes",
            ["optionalCostPolicy"] = "source-specific-server-filtered-costs",
            ["destinationPolicy"] = "source-specific-server-filtered-destinations",
            ["sourceRequirements"] = sourceRequirements
        };
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> PlayCardSourceRequirements(
        MatchState state,
        string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Hand
            .Where(objectId => IsImplementedPlayableHandSource(state, playerId, objectId))
            .SelectMany(objectId => PlayCardPromptBehaviorsForSource(state, playerId, objectId)
                .Where(behavior => PromptHasRequiredTargetChoices(state, playerId, behavior))
                .Select(behavior => PlayCardSourceRequirement(state, playerId, objectId, behavior)));
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
        var targetChoicesByIndex = Enumerable.Range(0, maxTargetCount)
            .ToDictionary(
                targetIndex => targetIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                targetIndex => (object?)PromptTargetChoicesForIndex(state, playerId, behavior, targetIndex),
                StringComparer.Ordinal);
        var unsupportedReason = UnsupportedPlayCardCompositionReason(behavior);
        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var paymentResourceChoices = PlayCardPaymentResourceChoicesForBehavior(state, playerId, behavior);
        var paymentResourcePowerByTrait = PlayCardPaymentResourcePowerByTraitForBehavior(state, playerId, behavior);
        var paymentResourcePowerByChoice = PlayCardPaymentResourcePowerByChoiceForBehavior(state, playerId, behavior);
        var availablePowerByTrait = PlayCardAvailablePowerByTrait(
            runePool,
            new Dictionary<string, int>(StringComparer.Ordinal));
        var availablePowerByTraitWithPaymentResources = PlayCardAvailablePowerByTrait(
            runePool,
            paymentResourcePowerByTrait);
        var baseManaReductionBeforeBattlefieldSpellCost = PromptBaseManaReductionBeforeBattlefieldSpellCost(
            state,
            playerId,
            behavior,
            sourceObjectId);
        var nextSpellCostReductionMana = PromptNextSpellCostReductionMana(
            state,
            playerId,
            behavior,
            baseManaReductionBeforeBattlefieldSpellCost);
        var battlefieldSpellCostReductionMana = PromptBattlefieldSpellCostReductionMana(
            state,
            playerId,
            behavior,
            baseManaReductionBeforeBattlefieldSpellCost + nextSpellCostReductionMana);

        return new Dictionary<string, object?>
        {
            ["sourceObjectId"] = sourceObjectId,
            ["cardNo"] = behavior.CardNo,
            ["displayName"] = behavior.DisplayName,
            ["mode"] = string.IsNullOrWhiteSpace(behavior.Mode) ? null : behavior.Mode,
            ["modeLabel"] = PlayCardModeLabel(behavior.Mode),
            ["manaCost"] = behavior.ManaCost,
            ["minimumManaCost"] = PromptMinimumManaCost(state, playerId, behavior, sourceObjectId),
            ["battlefieldEquipmentCostReductionMana"] = PromptBattlefieldEquipmentCostReductionMana(state, playerId, behavior),
            ["nextSpellCostReductionMana"] = nextSpellCostReductionMana,
            ["battlefieldSpellCostReductionMana"] = battlefieldSpellCostReductionMana,
            ["battlefieldHeldUnitCostIncreaseMana"] = PromptBattlefieldHeldUnitCostIncreaseMana(state, playerId, behavior),
            ["minTargetCount"] = minTargetCount,
            ["maxTargetCount"] = maxTargetCount,
            ["targetCountLabel"] = minTargetCount == maxTargetCount
                ? maxTargetCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : $"{minTargetCount}-{maxTargetCount}",
            ["targetScope"] = behavior.TargetScope,
            ["targetScopeLabel"] = PromptTargetScopeLabel(behavior.TargetScope),
            ["allowsRepeatedTargets"] = behavior.AllowsRepeatedTargets,
            ["targetChoicesByIndex"] = targetChoicesByIndex,
            ["legalTargetSelections"] = PlayCardLegalTargetSelections(state, playerId, behavior),
            ["destinationChoices"] = PlayCardDestinationChoicesForBehavior(state, playerId, behavior),
            ["optionalCostChoices"] = PlayCardOptionalCostChoicesForBehavior(state, playerId, behavior, sourceObjectId),
            ["paymentResourceChoices"] = paymentResourceChoices,
            ["paymentResourcePowerByChoice"] = paymentResourcePowerByChoice,
            ["availablePower"] = runePool.TotalPower,
            ["availablePowerByTrait"] = availablePowerByTrait,
            ["availablePowerWithPaymentResources"] = runePool.TotalPower + paymentResourcePowerByTrait.Values.Sum(),
            ["availablePowerByTraitWithPaymentResources"] = availablePowerByTraitWithPaymentResources,
            ["hasteReadyPowerCost"] = behavior.HasteReadyPowerCost,
            ["hasteReadyPowerTrait"] = HasteReadyPowerTrait(behavior),
            ["composable"] = string.IsNullOrWhiteSpace(unsupportedReason),
            ["unsupportedReason"] = unsupportedReason
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> PlayCardPaymentResourcePowerByChoiceForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (!PlayCardBehaviorMayNeedPaymentResource(state, playerId, behavior))
        {
            return new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        }

        var runePool = state.RunePools.TryGetValue(playerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        var needsPaymentResource = behavior.DamageAmountFromOptionalPowerCost
            || NeedsSourceDrawOptionalPowerPaymentResource(runePool, behavior)
            || NeedsSourceReadyPowerModifierPaymentResource(runePool, behavior)
            || NeedsTargetEffectAdditionalPowerPaymentResource(runePool, behavior)
            || NeedsHasteReadyPaymentResource(runePool, behavior)
            || NeedsCrescentGuardReadyPaymentResource(state, playerId, runePool, behavior);
        if (!needsPaymentResource
            || !state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        }

        return zones.Base
            .Where(objectId => IsRecycleRuneSource(state, playerId, objectId))
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
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? PlayCardDestinationChoicesForBehavior(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (!behavior.PlaysSourceToBaseAsUnit)
        {
            return null;
        }

        var choices = new List<ActionPromptChoiceDto>();
        if (!string.Equals(behavior.Mode, "AMBUSH", StringComparison.Ordinal))
        {
            choices.Add(new ActionPromptChoiceDto("BASE", "基地"));
        }

        var battlefieldDestination = $"BATTLEFIELD:{playerId}-MAIN";
        if (!PromptBattlefieldStaticPreventsUnitPlayToBattlefield(state, playerId, battlefieldDestination))
        {
            choices.Add(new ActionPromptChoiceDto(battlefieldDestination, "己方主战场"));
        }

        return choices.Count == 0 ? null : choices;
    }

    private static bool PromptBattlefieldStaticPreventsUnitPlayToBattlefield(
        MatchState state,
        string playerId,
        string destination)
    {
        return string.Equals(destination, $"BATTLEFIELD:{playerId}-MAIN", StringComparison.Ordinal)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, BattlefieldPreventUnitPlayCardNo, StringComparison.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId));
    }

    private static string UnsupportedPlayCardCompositionReason(CardBehaviorDefinition behavior)
    {
        if (behavior.RequiresDestroyFriendlyUnitAdditionalCost
            || behavior.RequiresDestroyFriendlyPowerfulUnitAdditionalCost
            || behavior.RequiresDestroyFriendlyTraitUnitAdditionalCost)
        {
            return "该牌需要服务端提供额外费用牺牲目标选择，当前前端入口暂不提交。";
        }

        if (behavior.RequiresReturnFriendlyEquipmentAdditionalCost)
        {
            return "该牌需要服务端提供额外费用返回装备选择，当前前端入口暂不提交。";
        }

        return string.Empty;
    }

    private static IEnumerable<string> PublicBoardObjects(MatchState state)
    {
        return state.PlayerZones.Values.SelectMany(zones => zones.Base.Concat(zones.Battlefields));
    }

    private static IEnumerable<string> PublicBattlefieldCardObjects(MatchState state)
    {
        return state.PlayerZones.Values
            .SelectMany(zones => zones.Battlefields)
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsPromptBattlefieldCardObject(cardObject));
    }

    private static bool IsPromptBattlefieldCardObject(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && IsPromptBattlefieldCardObject(cardObject);
    }

    private static bool IsPromptBattlefieldCardObject(CardObjectState cardObject)
    {
        return !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && IsBattlefieldCardObject(cardObject);
    }

    private static IEnumerable<(string PlayerId, string ObjectId)> OpposingBattlefieldObjects(MatchState state, string playerId)
    {
        return state.PlayerZones
            .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Battlefields.Select(objectId => (entry.Key, objectId)));
    }

    private static bool IsReadyFaceUpBattlefieldUnitForBattle(MatchState state, string zonePlayerId, string objectId)
    {
        return state.PlayerZones.TryGetValue(zonePlayerId, out var zones)
            && zones.Battlefields.Contains(objectId, StringComparer.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, zonePlayerId)
            && !cardObject.IsFaceDown
            && !cardObject.IsExhausted
            && !cardObject.IsAttacking
            && !cardObject.IsDefending
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal);
    }

    private static bool HasBattleDamageAssignmentKeyword(IReadOnlyList<string> tags)
    {
        return tags.Contains(CardCombatKeywordNames.Bulwark, StringComparer.Ordinal)
            || tags.Contains(CardCombatKeywordNames.BackRow, StringComparer.Ordinal);
    }

    private static IEnumerable<string> ControlledBattlefieldExtraStandbyObjects(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Battlefields.Where(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldExtraStandbyCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
            : [];
    }

    private static bool IsImplementedStandbyHideSource(MatchState state, string playerId, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo, out var behavior)
            && HasDelimitedTag(behavior.SourceUnitTags, CardObjectTags.Standby);
    }

    private static bool SourceObjectControlledByPlayerOrLegacyOwned(CardObjectState cardObject, string playerId)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return string.Equals(cardObject.ControllerId, playerId, StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(cardObject.OwnerId)
            || string.Equals(cardObject.OwnerId, playerId, StringComparison.Ordinal);
    }

    private static bool HasDelimitedTag(string values, string tag)
    {
        return !string.IsNullOrWhiteSpace(values)
            && values
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(tag, StringComparer.Ordinal);
    }

    private static IEnumerable<string> ControlledBoardObjects(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Base.Concat(zones.Battlefields)
            : [];
    }

    private static IEnumerable<string> ControlledLegendActionObjects(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Base.Concat(zones.Battlefields).Concat(zones.ChampionZone)
            : [];
    }

    private static bool PlayerControlsBattlefieldCard(MatchState state, string playerId, string cardNo)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId));
    }

    private static bool IsControlledObjectWithTag(
        MatchState state,
        string playerId,
        string objectId,
        string tag)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            && cardObject.Tags.Contains(tag, StringComparer.Ordinal)
            && TryFindLegendActionFieldObjectLocation(state.PlayerZones, objectId, out var location)
            && string.Equals(location.PlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool IsImplementedActivatedAbilitySource(
        MatchState state,
        string playerId,
        string objectId)
    {
        return ActivateAbilityRequirementsForSource(state, playerId, objectId).Count > 0;
    }

    private static bool IsImplementedLegendActionCardNo(string? cardNo)
    {
        return ImplementedLegendActionAbilities()
            .Any(ability => ability.SourceCardNos.Contains(cardNo, StringComparer.Ordinal));
    }

    private static bool IsBattlefieldCardObject(CardObjectState cardObject)
    {
        return cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldEphemeralUnitsSteadfastCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHeldMoveUnitToBaseCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHoldCreateMinionCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHoldDrawCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHoldCallRuneCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHoldGrantBoonCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHeldReturnHeroCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHeldPayPowerScoreCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldDestroyedInBattleRecallCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldGrantLegendAttachArmamentCardNo, StringComparison.Ordinal)
            || IsBattlefieldExtraStandbyCardNo(cardObject.CardNo)
            || string.Equals(cardObject.CardNo, BattlefieldHeldActivateConquestEffectsCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerConsumeBoonDrawCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerMillTwoCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHoldEachPlayerCallRuneCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldAllUnitsPowerPlusOneCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldDefenderSteadfastTwoCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldDefendMoveFriendlyUnitToBaseCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerRecycleRuneCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldDefendRevealSpellCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldIsolatedDefenderSteadfastMinusTwoCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerPayOneReadyLegendCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerReadyTwoRunesAtEndCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerDrawForOtherBattlefieldsCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerPowerfulPayOneDrawCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerPayOneCreateGoldCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerReadyEquipmentCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerDiscardDrawCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerOverkillCreateWarhawkCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldIncreaseWinningScoreCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldIncreaseWinningScoreAltCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldFirstTurnExtraRuneCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldFirstTurnScoreCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldScoreDelayCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldTurnStartDamageAllUnitsCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldTurnStartDestroyUnitDrawCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerRevealRecycleCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldMovedUnitPowerPlusOneCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHeldSevenUnitsWinCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHeldSevenUnitsWinAltCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldPreventMoveToBaseCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldStaticRoamCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldPreventUnitPlayCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldEchoCostReductionCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHeldNextSpellEchoCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldEquipmentCostReductionCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldFriendlySpellDrawCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldSpellPowerBonusCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldGrantUnitExperienceCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHighCostSpellInsightCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldUnitReturnedCallRuneCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldPlayUnitPayOneBoonCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldTargetSpellSkillDamageBonusCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHeldUnitCostIncreaseCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldExtraStandbyCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldExtraStandbyCardNo, StringComparison.Ordinal)
            || string.Equals(cardNo, BattlefieldExtraStandbyAltCardNo, StringComparison.Ordinal);
    }

    private static ActionPromptChoiceDto ObjectChoice(MatchState state, string objectId, string reason)
    {
        var label = state.CardObjects.TryGetValue(objectId, out var cardObject)
            ? string.IsNullOrWhiteSpace(cardObject.CardNo) ? "服务端对象" : cardObject.CardNo
            : "服务端对象";
        return new ActionPromptChoiceDto(objectId, label, reason);
    }

    private static ActionPromptChoiceDto StackItemChoice(MatchState state, string stackItemId, string reason)
    {
        var stackItem = state.StackItems.FirstOrDefault(item =>
            string.Equals(item.StackItemId, stackItemId, StringComparison.Ordinal));
        var label = stackItem is null
            ? "结算链项目"
            : string.IsNullOrWhiteSpace(stackItem.CardNo) ? "结算链项目" : stackItem.CardNo;
        return new ActionPromptChoiceDto(stackItemId, label, reason);
    }

    private static string PromptTargetReasonForScope(string targetScope, int targetIndex)
    {
        return $"{PromptTargetScopeLabel(targetScope)} / 第 {targetIndex + 1} 个目标";
    }

    private static string PromptTargetScopeLabel(string targetScope)
    {
        return targetScope switch
        {
            CardTargetScopes.BattlefieldUnit => "战场单位",
            CardTargetScopes.BattlefieldUnitOrEquipment => "战场单位或装备",
            CardTargetScopes.BaseUnit => "基地单位",
            CardTargetScopes.AnyUnit => "任意单位",
            CardTargetScopes.FriendlyUnit => "友方单位",
            CardTargetScopes.FriendlyUnitThenFriendlyUnit => "友方单位，然后另一个友方单位",
            CardTargetScopes.FriendlyThenEnemyUnits => "友方单位，然后敌方单位",
            CardTargetScopes.UnitThenItsControllersWeapon => "单位及其控制者武器",
            CardTargetScopes.FriendlyEquipmentThenEnemyEquipment => "友方装备，然后敌方装备",
            CardTargetScopes.FriendlyThenEnemyBattlefieldUnits => "友方单位，然后敌方战场单位",
            CardTargetScopes.FriendlyBattlefieldThenEnemyBattlefieldUnits => "友方战场单位，然后敌方战场单位",
            CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell => "友方战场单位，然后结算链法术",
            CardTargetScopes.AnyUnitThenFriendlyMainDeckCard => "单位，然后己方主牌堆牌",
            CardTargetScopes.FriendlyBattlefieldUnit => "友方战场单位",
            CardTargetScopes.FriendlyHandCard => "己方手牌",
            CardTargetScopes.AnyHandCard => "手牌",
            CardTargetScopes.FriendlyHandCardThenBattlefieldUnit => "己方手牌，然后战场单位",
            CardTargetScopes.FriendlyMainDeckCard => "己方主牌堆牌",
            CardTargetScopes.FriendlyGraveyardCard => "己方废牌堆牌",
            CardTargetScopes.AttackingUnit => "攻击中单位",
            CardTargetScopes.EnemyAttackingUnit => "敌方攻击中单位",
            CardTargetScopes.EnemyBattlefieldUnit => "敌方战场单位",
            CardTargetScopes.EnemyUnit => "敌方单位",
            CardTargetScopes.EnemyUnitThenEnemyUnit => "敌方单位，然后另一个敌方单位",
            CardTargetScopes.OpponentHandCard => "对手手牌",
            CardTargetScopes.OpponentGraveyardCard => "对手废牌堆牌",
            CardTargetScopes.OpponentMainDeckTopCard => "对手主牌堆顶牌",
            CardTargetScopes.AnyMainDeckTopFiveCard => "主牌堆顶五张",
            CardTargetScopes.FriendlyBaseUnit => "友方基地单位",
            CardTargetScopes.Equipment => "装备",
            CardTargetScopes.Legend => "传奇",
            CardTargetScopes.StackSpell => "结算链法术",
            CardTargetScopes.SacredJudgmentKeepCard => "审判日保留牌",
            _ => "战场单位"
        };
    }

    private static ActionPromptChoiceDto BattlefieldDestinationChoice(MatchState state, string objectId, string reason)
    {
        var choice = ObjectChoice(state, objectId, reason);
        return new ActionPromptChoiceDto($"BATTLEFIELD:{objectId}", choice.Label, choice.Reason);
    }

    private static string LabelFor(string action)
    {
        return action switch
        {
            "READY" => "准备",
            "SUBMIT_DECK" => "提交卡组",
            "MULLIGAN" => "起手调度",
            "WAIT" => "等待",
            "PLAY_CARD" => "打出卡牌",
            "ACTIVATE_ABILITY" => "激活能力",
            "ASSEMBLE_EQUIPMENT" => "装配装备",
            "MOVE_UNIT" => "移动单位",
            "DECLARE_BATTLE" => "声明战斗",
            "HIDE_CARD" => "布置待命",
            "REVEAL_CARD" => "翻开待命",
            "TAP_RUNE" => "横置符文",
            "RECYCLE_RUNE" => "回收符文",
            "LEGEND_ACT" => "传奇行动",
            "PASS" => "让过",
            "PASS_PRIORITY" => "让过优先权",
            "PASS_FOCUS" => "让过焦点",
            "END_TURN" => "结束回合",
            "SURRENDER" => "投降",
            _ => IsProtocolActionToken(action) ? "服务端操作" : action
        };
    }

    private static bool IsProtocolActionToken(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        return action.All(character =>
            character is '_' or '-' or ':'
            || (character >= 'A' && character <= 'Z')
            || (character >= '0' && character <= '9'));
    }

    private static string DisabledReasonFor(
        string action,
        string promptReason,
        bool hasRequiredChoices)
    {
        var actionLabel = LabelFor(action);

        if (!hasRequiredChoices)
        {
            return $"{actionLabel} 当前没有服务端可执行候选";
        }

        return string.Equals(action, "WAIT", StringComparison.Ordinal)
            ? promptReason
            : $"当前行动提示不允许执行 {actionLabel}";
    }
}

public sealed class PlaceholderRuleEngine : IRuleEngine
{
    public ValueTask<ResolutionResult> ResolveAsync(
        MatchState state,
        PlayerIntent intent,
        GameCommand command,
        CancellationToken cancellationToken)
    {
        if (command is UnsupportedCommand)
        {
            return ValueTask.FromResult(ResolutionResult.Rejected(
                state,
                "当前命令不受服务端支持。",
                ErrorCodes.UnsupportedCommand));
        }

        var nextState = BuildNextState(state, command);
        var events = BuildAcceptedEvents(intent, command, nextState);

        return ValueTask.FromResult(new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            ResolutionResult.BuildPrompts(nextState)));
    }

    private static MatchState BuildNextState(MatchState state, GameCommand command)
    {
        if (command is EndTurnCommand)
        {
            var nextPlayerId = NextPlayerId(state);
            return state with
            {
                Tick = state.Tick + 1,
                TurnNumber = state.TurnNumber + 1,
                ActivePlayerId = nextPlayerId,
                TurnPlayerId = nextPlayerId,
                Phase = MatchPhases.Main,
                TimingState = TimingStates.NeutralOpen
            };
        }

        if (command is SurrenderCommand)
        {
            return state with
            {
                Tick = state.Tick + 1,
                Status = MatchStatuses.Finished,
                WinnerPlayerId = NextPlayerId(state)
            };
        }

        return state with
        {
            Tick = state.Tick + 1
        };
    }

    private static string NextPlayerId(MatchState state)
    {
        var players = state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToArray();
        if (players.Length == 0)
        {
            return state.ActivePlayerId;
        }

        var activeIndex = Array.IndexOf(players, state.ActivePlayerId);
        if (activeIndex < 0)
        {
            return players[0];
        }

        return players[(activeIndex + 1) % players.Length];
    }

    private static IReadOnlyList<GameEvent> BuildAcceptedEvents(
        PlayerIntent intent,
        GameCommand command,
        MatchState nextState)
    {
        if (command is PassCommand)
        {
            return
            [
                new GameEvent(
                    "TURN_ENDED",
                    $"{intent.PlayerId} 选择暂不行动",
                    new Dictionary<string, object?>())
            ];
        }

        if (command is EndTurnCommand)
        {
            return
            [
                new GameEvent(
                    "TURN_ENDED",
                    $"{intent.PlayerId} 结束回合",
                    new Dictionary<string, object?>()),
                new GameEvent(
                    "TURN_BEGAN",
                    "轮到下一位行动",
                    new Dictionary<string, object?>
                    {
                        ["active"] = nextState.ActivePlayerId
                    }),
                new GameEvent(
                    "RUNE_CHANNELLED",
                    $"{nextState.ActivePlayerId} 通道 1 点占位符能",
                    new Dictionary<string, object?>()),
                new GameEvent(
                    "RUNE_CHANNELLED",
                    $"{nextState.ActivePlayerId} 通道 1 点占位符能",
                    new Dictionary<string, object?>()),
                new GameEvent(
                    "CARD_DRAWN",
                    $"{nextState.ActivePlayerId} 抽 1 张牌",
                    new Dictionary<string, object?>())
            ];
        }

        if (command is SurrenderCommand)
        {
            return
            [
                new GameEvent(
                    "MATCH_WON",
                    $"{nextState.WinnerPlayerId} 因 {intent.PlayerId} 投降获胜",
                    new Dictionary<string, object?>
                    {
                        ["winnerPlayerId"] = nextState.WinnerPlayerId,
                        ["surrenderedPlayerId"] = intent.PlayerId,
                        ["reason"] = "SURRENDER"
                    })
            ];
        }

        return
        [
            new GameEvent(
                command.CmdType,
                "占位规则引擎接受了命令",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["intentId"] = intent.IntentId,
                    ["cmdType"] = command.CmdType
                })
        ];
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
    ValueTask<IMatchSession> GetOrCreateAsync(string roomId, CancellationToken cancellationToken);
}

public sealed record MatchSessionOptions(bool AllowLegacyReadyWithoutDeck = true)
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

public sealed class MatchSession : IMatchSession
{
    private const string BattlefieldEphemeralUnitsSteadfastCardNo = "UNL-208/219";
    private const string BattlefieldHeldMoveUnitToBaseCardNo = "UNL-207/219";
    private const string BattlefieldHoldCreateMinionCardNo = "OGN·275/298";
    private const string BattlefieldHoldDrawCardNo = "OGN·280/298";
    private const string BattlefieldHoldCallRuneCardNo = "OGN·288/298";
    private const string BattlefieldHoldGrantBoonCardNo = "OGN·283/298";
    private const string BattlefieldHeldReturnHeroCardNo = "OGN·281/298";
    private const string BattlefieldHeldPayPowerScoreCardNo = "SFD·214/221";
    private const string BattlefieldDestroyedInBattleRecallCardNo = "UNL-206/219";
    private const string BattlefieldGrantLegendAttachArmamentCardNo = "SFD·208/221";
    private const string BattlefieldExtraStandbyCardNo = "OGN·278/298";
    private const string BattlefieldExtraStandbyAltCardNo = "OGN·278a/298";
    private const string BattlefieldHeldActivateConquestEffectsCardNo = "OGN·286/298";
    private const string BattlefieldConquerConsumeBoonDrawCardNo = "OGN·282/298";
    private const string BattlefieldConquerMillTwoCardNo = "SFD·212/221";
    private const string BattlefieldHoldEachPlayerCallRuneCardNo = "SFD·219/221";
    private const string BattlefieldAllUnitsPowerPlusOneCardNo = "OGN·294/298";
    private const string BattlefieldDefenderSteadfastTwoCardNo = "OGN·279/298";
    private const string BattlefieldDefendMoveFriendlyUnitToBaseCardNo = "OGN·285/298";
    private const string BattlefieldConquerRecycleRuneCardNo = "OGN·287/298";
    private const string BattlefieldDefendRevealSpellCardNo = "SFD·215/221";
    private const string BattlefieldIsolatedDefenderSteadfastMinusTwoCardNo = "UNL-210/219";
    private const string BattlefieldConquerPayOneReadyLegendCardNo = "SFD·210/221";
    private const string BattlefieldConquerReadyTwoRunesAtEndCardNo = "OGN·289/298";
    private const string BattlefieldConquerDrawForOtherBattlefieldsCardNo = "SFD·217/221";
    private const string BattlefieldConquerPowerfulPayOneDrawCardNo = "SFD·218/221";
    private const string BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo = "SFD·207/221";
    private const string BattlefieldConquerPayOneCreateGoldCardNo = "SFD·220/221";
    private const string BattlefieldConquerReadyEquipmentCardNo = "SFD·221/221";
    private const string BattlefieldConquerDiscardDrawCardNo = "OGN·298/298";
    private const string BattlefieldConquerOverkillCreateWarhawkCardNo = "UNL-217/219";
    private const string BattlefieldWinningScoreSeedCardNo = "OGN·276/298";
    private const string BattlefieldFirstTurnExtraRuneCardNo = "OGN·284/298";
    private const string BattlefieldFirstTurnScoreCardNo = "OGN·290/298";
    private const string BattlefieldScoreDelayCardNo = "SFD·209/221";
    private const string BattlefieldTurnStartDamageAllUnitsCardNo = "UNL-212/219";
    private const string BattlefieldTurnStartDestroyUnitDrawCardNo = "UNL-209/219";
    private const string BattlefieldConquerRevealRecycleCardNo = "OGN·291/298";
    private const string BattlefieldMovedUnitPowerPlusOneCardNo = "OGN·277/298";
    private const string BattlefieldHeldSevenUnitsWinCardNo = "OGN·293/298";
    private const string BattlefieldPreventMoveToBaseCardNo = "OGN·295/298";
    private const string BattlefieldStaticRoamCardNo = "OGN·297/298";
    private const string BattlefieldPreventUnitPlayCardNo = "SFD·216/221";
    private const string BattlefieldEchoCostReductionCardNo = "SFD·211/221";
    private const string BattlefieldHeldNextSpellEchoCardNo = "UNL-216/219";
    private const string BattlefieldEquipmentCostReductionCardNo = "SFD·213/221";
    private const string BattlefieldFriendlySpellDrawCardNo = "OGN·292/298";
    private const string BattlefieldSpellPowerBonusCardNo = "UNL-205/219";
    private const string BattlefieldGrantUnitExperienceCardNo = "UNL-213/219";
    private const string BattlefieldHighCostSpellInsightCardNo = "UNL-211/219";
    private const string BattlefieldUnitReturnedCallRuneCardNo = "UNL-214/219";
    private const string BattlefieldPlayUnitPayOneBoonCardNo = "UNL-218/219";
    private const string BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo = "UNL-215/219";
    private const string BattlefieldTargetSpellSkillDamageBonusCardNo = "OGN·296/298";
    private const string BattlefieldHeldUnitCostIncreaseCardNo = "UNL-219/219";
    private const string BattlefieldHeldUnitCostIncreaseEffectPrefix = "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:";
    private const string RagingDrakeNextSpellCostReductionEffectPrefix = "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:";
    private const string BattlefieldUnitGainExperienceAbilityId = "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE";

    private static readonly SemaphoreSlim OfficialCatalogGate = new(1, 1);
    private static OfficialCardCatalog? officialCatalog;

    private readonly IRuleEngine ruleEngine;
    private readonly IMatchJournal journal;
    private readonly IMatchPlayerStore playerStore;
    private readonly MatchSessionOptions options;
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
        state = MatchState.Create(roomId);
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
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore,
        MatchSessionOptions options)
    {
        RoomId = restoredState.RoomId;
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        this.playerStore = playerStore;
        this.options = options ?? MatchSessionOptions.Default;
        state = restoredState;
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
                command.Accepted ? null : ErrorCodes.UnsupportedCommand);
            intentCache[$"{command.PlayerId}:{command.ClientIntentId}"] = new CachedResolution(
                command.CommandType,
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
        if (!recovery.IsConsistent)
        {
            throw new MatchSessionException(
                ErrorCodes.RecoveryInconsistent,
                $"match recovery is inconsistent: {string.Join("; ", recovery.ValidationErrors)}");
        }

        var state = RestoreState(recovery);
        return new MatchSession(
            state,
            recovery.LastEventSequence,
            state.Seats,
            recovery.Commands,
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
            state = state with
            {
                ActivePlayerId = seats.ContainsKey(state.ActivePlayerId) ? state.ActivePlayerId : normalizedPlayerId,
                TurnPlayerId = seats.ContainsKey(state.TurnPlayerId) ? state.TurnPlayerId : normalizedPlayerId,
                Seats = new Dictionary<string, string>(seats),
                Status = state.Status == MatchStatuses.InProgress
                    ? MatchStatuses.InProgress
                    : MatchStatuses.Seating,
                Phase = state.Status == MatchStatuses.InProgress ? state.Phase : MatchPhases.Room,
                TimingState = state.Status == MatchStatuses.InProgress ? state.TimingState : TimingStates.Room,
                ReadyPlayerIds = state.ReadyPlayerIds
                    .Where(seats.ContainsKey)
                    .OrderBy(readyPlayerId => readyPlayerId, StringComparer.Ordinal)
                    .ToArray(),
                RunePools = RunePoolsForSeats(state.RunePools, seats.Keys),
                PlayerZones = PlayerZonesForSeats(state.PlayerZones, seats.Keys),
                PlayerScores = PlayerScoresForSeats(state.PlayerScores, seats.Keys),
                PlayerExperience = PlayerExperienceForSeats(state.PlayerExperience, seats.Keys),
                PlayerCardsPlayedThisTurn = PlayerCardsPlayedThisTurnForSeats(state.PlayerCardsPlayedThisTurn, seats.Keys),
                PlayerDecklists = PlayerDecklistsForSeats(state.PlayerDecklists, seats.Keys),
                MulliganCompletedPlayerIds = state.MulliganCompletedPlayerIds
                    .Where(seats.ContainsKey)
                    .OrderBy(mulliganPlayerId => mulliganPlayerId, StringComparer.Ordinal)
                    .ToArray()
            };
            return PlayerSessionFor(normalizedPlayerId);
        }
    }

    public async ValueTask<PlayerSessionDto> EnsurePlayerAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        lock (seatGate)
        {
            if (seats.ContainsKey(normalizedPlayerId)
                && (!reconnectTokens.TryGetValue(normalizedPlayerId, out var liveToken)
                    || string.IsNullOrWhiteSpace(liveToken)))
            {
                throw new MatchSessionException(
                    ErrorCodes.InvalidReconnectToken,
                    "已有玩家重连需要提供重连令牌。");
            }
        }

        var playerSession = EnsurePlayer(normalizedPlayerId);
        await PersistPlayerSessionAsync(playerSession, cancellationToken).ConfigureAwait(false);
        return playerSession;
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

        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (intentCache.TryGetValue(cacheKey, out var cached))
            {
                if (!string.Equals(cached.CommandType, commandType, StringComparison.Ordinal))
                {
                    throw new MatchSessionException(
                        ErrorCodes.ClientIntentConflict,
                        "该客户端行动编号已用于其他命令。");
                }

                return cached.Result;
            }

            var startedTick = state.Tick;
            var startedEventSequence = lastEventSequence;
            var nextState = BuildDevScenarioState(state, normalizedScenarioId) with
            {
                Tick = state.Tick + 1
            };
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
            };
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
                    DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);
            lastEventSequence = completedEventSequence;

            state = nextState;
            intentCache[cacheKey] = new CachedResolution(commandType, result);
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

        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (intentCache.TryGetValue(cacheKey, out var cached))
            {
                if (!string.Equals(cached.CommandType, "SUBMIT_DECK", StringComparison.Ordinal))
                {
                    throw new MatchSessionException(
                        ErrorCodes.ClientIntentConflict,
                        "该客户端行动编号已用于其他命令。");
                }

                return cached.Result;
            }

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
                var validation = OfficialDeckValidator.Validate(decklist, catalog);
                if (!validation.IsValid)
                {
                    result = ResolutionResult.Rejected(
                        state,
                        $"卡组不合法：{string.Join("；", validation.Errors)}",
                        ErrorCodes.InvalidDeck);
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
                    DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);

            if (result.Accepted)
            {
                state = result.State;
            }
            lastEventSequence = completedEventSequence;
            intentCache[cacheKey] = new CachedResolution("SUBMIT_DECK", result);
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

        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (intentCache.TryGetValue(cacheKey, out var cached))
            {
                if (!string.Equals(cached.CommandType, "READY", StringComparison.Ordinal))
                {
                    throw new MatchSessionException(
                        ErrorCodes.ClientIntentConflict,
                        "该客户端行动编号已用于其他命令。");
                }

                return cached.Result;
            }

            if (state.Status == MatchStatuses.Finished)
            {
                throw new MatchSessionException(ErrorCodes.MatchFinished, "对局已经结束，不能准备。");
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
                intentCache[cacheKey] = new CachedResolution("READY", current);
                return current;
            }

            var startedTick = state.Tick;
            var startedEventSequence = lastEventSequence;
            if (!state.PlayerDecklists.ContainsKey(normalizedPlayerId)
                && (!options.AllowLegacyReadyWithoutDeck || state.PlayerDecklists.Count > 0))
            {
                var rejected = ResolutionResult.Rejected(
                    state,
                    "正式卡组房间需要先提交合法卡组才能准备。",
                    ErrorCodes.InvalidDeck);
                intentCache[cacheKey] = new CachedResolution("READY", rejected);
                return rejected;
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
                        DateTimeOffset.UtcNow),
                    cancellationToken).ConfigureAwait(false);
                lastEventSequence = completedEventSequence;
            }

            state = nextState;
            intentCache[cacheKey] = new CachedResolution("READY", result);
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

        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (intentCache.TryGetValue(cacheKey, out var cached))
            {
                if (!string.Equals(cached.CommandType, command.CmdType, StringComparison.Ordinal))
                {
                    return ResolutionResult.Rejected(
                        state,
                        "该客户端行动编号已用于其他命令。",
                        ErrorCodes.ClientIntentConflict);
                }

                return cached.Result;
            }

            if (state.Status == MatchStatuses.Finished)
            {
                throw new MatchSessionException(ErrorCodes.MatchFinished, "对局已经结束，不能继续提交行动。");
            }

            if (state.Status != MatchStatuses.InProgress)
            {
                throw new MatchSessionException(ErrorCodes.MatchNotStarted, "对局尚未开始。");
            }

            var startedTick = state.Tick;
            var startedEventSequence = lastEventSequence;
            var intent = new PlayerIntent(normalizedIntentId, normalizedPlayerId, command.CmdType);
            var result = await ruleEngine.ResolveAsync(state, intent, command, cancellationToken)
                .ConfigureAwait(false);
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
                    DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);

            if (result.Accepted)
            {
                state = result.State;
            }
            lastEventSequence = completedEventSequence;

            intentCache[cacheKey] = new CachedResolution(command.CmdType, result);
            return result;
        }
        finally
        {
            serialGate.Release();
        }
    }

    private sealed record CachedResolution(string CommandType, ResolutionResult Result);

    private static MatchState RestoreState(MatchRecoveryFrame recovery)
    {
        if (recovery.AuthoritativeState is not null)
        {
            return recovery.AuthoritativeState with
            {
                Tick = recovery.CurrentTick
            };
        }

        if (recovery.PlayerViews.Count == 0)
        {
            return MatchState.Create(recovery.RoomId) with
            {
                Tick = recovery.CurrentTick
            };
        }

        var baseline = recovery.PlayerViews.Values
            .OrderBy(view => view.PlayerId, StringComparer.Ordinal)
            .First()
            .Snapshot;
        var seats = MatchRecoveryValidator.ExtractSeats(baseline);
        return new MatchState(
            recovery.RoomId,
            recovery.CurrentTick,
            baseline.TurnNumber,
            baseline.ActivePlayerId,
            seats);
    }

    private string NextOpenSeat()
    {
        return seats.ContainsValue("P1") ? "P2" : "P1";
    }

    private static IReadOnlyDictionary<string, RunePool> RunePoolsForSeats(
        IReadOnlyDictionary<string, RunePool> current,
        IEnumerable<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => playerId,
            playerId => current.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, PlayerZones> PlayerZonesForSeats(
        IReadOnlyDictionary<string, PlayerZones> current,
        IEnumerable<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => playerId,
            playerId => current.TryGetValue(playerId, out var zones) ? zones : PlayerZones.Empty,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> PlayerScoresForSeats(
        IReadOnlyDictionary<string, int> current,
        IEnumerable<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => playerId,
            playerId => current.TryGetValue(playerId, out var score) ? score : 0,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> PlayerExperienceForSeats(
        IReadOnlyDictionary<string, int> current,
        IEnumerable<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => playerId,
            playerId => current.TryGetValue(playerId, out var experience) ? experience : 0,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> PlayerCardsPlayedThisTurnForSeats(
        IReadOnlyDictionary<string, int> current,
        IEnumerable<string> playerIds)
    {
        return playerIds
            .Where(playerId => current.TryGetValue(playerId, out var count) && count > 0)
            .ToDictionary(
                playerId => playerId,
                playerId => current[playerId],
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, OfficialDecklist> PlayerDecklistsForSeats(
        IReadOnlyDictionary<string, OfficialDecklist> current,
        IEnumerable<string> playerIds)
    {
        var seatSet = playerIds.ToHashSet(StringComparer.Ordinal);
        return current
            .Where(entry => seatSet.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private PlayerSessionDto PlayerSessionFor(string playerId)
    {
        if (!reconnectTokens.TryGetValue(playerId, out var reconnectToken)
            || string.IsNullOrWhiteSpace(reconnectToken))
        {
            reconnectToken = NewReconnectToken();
            reconnectTokens[playerId] = reconnectToken;
        }

        return new PlayerSessionDto(playerId, seats[playerId], reconnectToken);
    }

    private async ValueTask<PlayerSessionDto> RotateReconnectTokenAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        PlayerSessionDto playerSession;
        lock (seatGate)
        {
            reconnectTokens[playerId] = NewReconnectToken();
            playerSession = PlayerSessionFor(playerId);
        }

        await PersistPlayerSessionAsync(playerSession, cancellationToken).ConfigureAwait(false);
        return playerSession;
    }

    private ValueTask PersistPlayerSessionAsync(
        PlayerSessionDto playerSession,
        CancellationToken cancellationToken)
    {
        return playerStore.SavePlayerSessionAsync(
            RoomId,
            playerSession.PlayerId,
            playerSession.Seat,
            ReconnectTokenHasher.Hash(playerSession.ReconnectToken),
            cancellationToken);
    }

    private static async ValueTask<OfficialCardCatalog> LoadOfficialCatalogAsync(CancellationToken cancellationToken)
    {
        if (officialCatalog is not null)
        {
            return officialCatalog;
        }

        await OfficialCatalogGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            officialCatalog ??= await OfficialCardCatalog.LoadDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            return officialCatalog;
        }
        finally
        {
            OfficialCatalogGate.Release();
        }
    }

    private static bool HasOfficialDecklistsForAllSeats(MatchState state)
    {
        return state.Seats.Count == 2
            && state.Seats.Keys.All(playerId => state.PlayerDecklists.ContainsKey(playerId));
    }

    private static OfficialOpeningBuildResult BuildOfficialOpeningState(
        MatchState readyState,
        OfficialCardCatalog catalog)
    {
        var seed = readyState.Seed == 0
            ? (long)(StableHash($"{readyState.RoomId}:official-opening:{string.Join(",", readyState.Seats.Keys.Order(StringComparer.Ordinal))}") & long.MaxValue)
            : readyState.Seed;
        var rngCursor = readyState.RngCursor;
        var seatOrder = readyState.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToArray();
        var turnOrder = RandomizeStable(seatOrder, seed, rngCursor, "TURN_ORDER");
        if (seatOrder.Length > 1)
        {
            rngCursor++;
        }

        var activePlayerId = turnOrder[0];
        var secondActionPlayerId = turnOrder.Count > 1 ? turnOrder[1] : null;
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal);
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        var events = new List<GameEvent>
        {
            new(
                "OFFICIAL_OPENING_STARTED",
                "正式 1v1 开局流程开始",
                new Dictionary<string, object?>
                {
                    ["activePlayerId"] = activePlayerId,
                    ["secondActionPlayerId"] = secondActionPlayerId,
                    ["openingHandCount"] = OfficialDeckValidator.OpeningHandCount
                })
        };

        var cardsByNo = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        foreach (var playerId in seatOrder)
        {
            var decklist = readyState.PlayerDecklists[playerId];
            var playerOpening = BuildOfficialPlayerOpening(
                playerId,
                decklist,
                cardsByNo,
                seed,
                rngCursor);
            rngCursor = playerOpening.RngCursor;
            playerZones[playerId] = playerOpening.Zones;
            foreach (var (objectId, cardObject) in playerOpening.CardObjects)
            {
                cardObjects[objectId] = cardObject;
            }
            foreach (var (objectId, location) in playerOpening.ObjectLocations)
            {
                objectLocations[objectId] = location;
            }

            events.AddRange(playerOpening.Events);
        }

        var runePools = seatOrder.ToDictionary(playerId => playerId, _ => RunePool.Empty, StringComparer.Ordinal);
        var playerScores = seatOrder.ToDictionary(playerId => playerId, _ => 0, StringComparer.Ordinal);
        var playerExperience = seatOrder.ToDictionary(playerId => playerId, _ => 0, StringComparer.Ordinal);
        var nextState = readyState with
        {
            ActivePlayerId = activePlayerId,
            TurnPlayerId = activePlayerId,
            TurnNumber = 1,
            Status = MatchStatuses.InProgress,
            Phase = MatchPhases.Mulligan,
            TimingState = TimingStates.Mulligan,
            RunePools = runePools,
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            PlayerScores = playerScores,
            PlayerExperience = playerExperience,
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal),
            CardObjects = cardObjects,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = [],
            StackItems = [],
            FocusPlayerId = null,
            PassedFocusPlayerIds = [],
            WinnerPlayerId = null,
            DestroyedUnitOwnerIdsThisTurn = [],
            Seed = seed,
            RngCursor = rngCursor,
            UntilEndOfTurnEffects = [],
            ExtraTurnPlayerId = null,
            MulliganCompletedPlayerIds = [],
            OpeningSecondActionPlayerId = secondActionPlayerId
        };

        return new OfficialOpeningBuildResult(nextState, events);
    }

    private static OfficialPlayerOpeningBuildResult BuildOfficialPlayerOpening(
        string playerId,
        OfficialDecklist decklist,
        IReadOnlyDictionary<string, OfficialCard> cardsByNo,
        long seed,
        long rngCursor)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        var mainCardNos = decklist.MainDeck.ToList();
        var championIndex = mainCardNos.FindIndex(cardNo => string.Equals(cardNo, decklist.ChampionCardNo, StringComparison.Ordinal));
        if (championIndex >= 0)
        {
            mainCardNos.RemoveAt(championIndex);
        }

        var legendObjectId = OfficialObjectId(playerId, "LEGEND", decklist.LegendCardNo, 1);
        var championObjectId = OfficialObjectId(playerId, "CHAMPION", decklist.ChampionCardNo, 1);
        cardObjects[legendObjectId] = OfficialCardObject(legendObjectId, playerId, cardsByNo[decklist.LegendCardNo]);
        cardObjects[championObjectId] = OfficialCardObject(championObjectId, playerId, cardsByNo[decklist.ChampionCardNo]);
        objectLocations[legendObjectId] = new ObjectLocationState(playerId, "LEGEND");
        objectLocations[championObjectId] = new ObjectLocationState(playerId, "CHAMPION");

        var mainObjectIds = new List<string>();
        for (var index = 0; index < mainCardNos.Count; index++)
        {
            var cardNo = mainCardNos[index];
            var objectId = OfficialObjectId(playerId, "MAIN", cardNo, index + 1);
            mainObjectIds.Add(objectId);
            cardObjects[objectId] = OfficialCardObject(objectId, playerId, cardsByNo[cardNo]);
        }

        var shuffledMain = RandomizeStable(mainObjectIds, seed, rngCursor, $"{playerId}:MAIN_DECK");
        if (mainObjectIds.Count > 1)
        {
            rngCursor++;
        }

        var hand = shuffledMain.Take(OfficialDeckValidator.OpeningHandCount).ToArray();
        var remainingMain = shuffledMain.Skip(hand.Length).ToArray();

        var runeObjectIds = new List<string>();
        for (var index = 0; index < decklist.RuneDeck.Count; index++)
        {
            var cardNo = decklist.RuneDeck[index];
            var objectId = OfficialObjectId(playerId, "RUNE", cardNo, index + 1);
            runeObjectIds.Add(objectId);
            cardObjects[objectId] = OfficialCardObject(objectId, playerId, cardsByNo[cardNo]);
        }

        var shuffledRunes = RandomizeStable(runeObjectIds, seed, rngCursor, $"{playerId}:RUNE_DECK");
        if (runeObjectIds.Count > 1)
        {
            rngCursor++;
        }

        var battlefieldCardNos = RandomizeStable(decklist.Battlefields, seed, rngCursor, $"{playerId}:BATTLEFIELDS");
        if (decklist.Battlefields.Count > 1)
        {
            rngCursor++;
        }

        var selectedBattlefieldCardNo = battlefieldCardNos[0];
        var battlefieldObjectId = OfficialObjectId(playerId, "BATTLEFIELD", selectedBattlefieldCardNo, 1);
        cardObjects[battlefieldObjectId] = OfficialCardObject(
            battlefieldObjectId,
            playerId,
            cardsByNo[selectedBattlefieldCardNo]);
        objectLocations[battlefieldObjectId] = new ObjectLocationState(
            playerId,
            "BATTLEFIELD",
            battlefieldObjectId);
        foreach (var objectId in remainingMain)
        {
            objectLocations[objectId] = new ObjectLocationState(playerId, "MAIN_DECK");
        }
        foreach (var objectId in hand)
        {
            objectLocations[objectId] = new ObjectLocationState(playerId, "HAND");
        }
        foreach (var objectId in shuffledRunes)
        {
            objectLocations[objectId] = new ObjectLocationState(playerId, "RUNE_DECK");
        }

        var zones = new PlayerZones(
            remainingMain,
            shuffledRunes,
            hand,
            [],
            [battlefieldObjectId],
            [],
            [],
            [legendObjectId],
            [championObjectId]);

        var events = new List<GameEvent>
        {
            new(
                "OFFICIAL_BATTLEFIELD_SELECTED",
                $"{playerId} 随机选择 1 张战场进入本局",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["battlefieldObjectId"] = battlefieldObjectId,
                    ["removedBattlefieldCount"] = Math.Max(0, decklist.Battlefields.Count - 1)
                }),
            new(
                "OPENING_HAND_DRAWN",
                $"{playerId} 抽取起手牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["drawCount"] = hand.Length
                })
        };

        return new OfficialPlayerOpeningBuildResult(zones, cardObjects, objectLocations, events, rngCursor);
    }

    private static CardObjectState OfficialCardObject(string objectId, string playerId, OfficialCard card)
    {
        return new CardObjectState(
            objectId,
            power: Math.Max(0, card.Power ?? 0),
            tags: OfficialCardTags(card),
            manaCost: Math.Max(0, card.Energy ?? 0),
            cardNo: card.CardNo,
            ownerId: playerId,
            controllerId: playerId);
    }

    private static IReadOnlyList<string> OfficialCardTags(OfficialCard card)
    {
        var tags = new List<string>();
        if (card.CardCategoryName.Contains("单位", StringComparison.Ordinal))
        {
            tags.Add(CardObjectTags.UnitCard);
        }
        else if (card.CardCategoryName.Contains("装备", StringComparison.Ordinal))
        {
            tags.Add(CardObjectTags.EquipmentCard);
        }
        else if (card.CardCategoryName.Contains("法术", StringComparison.Ordinal))
        {
            tags.Add(CardObjectTags.SpellCard);
        }
        else if (string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
        {
            tags.Add(CardObjectTags.RuneCard);
        }
        else if (string.Equals(card.CardCategoryName, "传奇", StringComparison.Ordinal))
        {
            tags.Add("CARD_TYPE:LEGEND");
        }
        else if (string.Equals(card.CardCategoryName, "战场", StringComparison.Ordinal))
        {
            tags.Add("CARD_TYPE:BATTLEFIELD");
        }

        if (string.Equals(card.CardCategoryName, "英雄单位", StringComparison.Ordinal))
        {
            tags.Add("CARD_TYPE:HERO");
        }

        if (!string.IsNullOrWhiteSpace(card.Hero))
        {
            tags.Add($"HERO:{card.Hero}");
        }

        tags.AddRange(card.CardColorList
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Select(color => $"COLOR:{color.Trim()}"));
        return tags.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string OfficialObjectId(string playerId, string zone, string cardNo, int index)
    {
        var normalized = new string(cardNo
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');
        return $"{playerId}-{zone}-{normalized}-{index:00}";
    }

    private static IReadOnlyList<T> RandomizeStable<T>(
        IReadOnlyList<T> values,
        long seed,
        long rngCursor,
        string scope)
    {
        if (values.Count <= 1)
        {
            return values.ToArray();
        }

        return values
            .Select((value, index) => new
            {
                Value = value,
                Index = index,
                Order = StableHash($"{seed}:{rngCursor}:{scope}:{value}:{index}")
            })
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Value)
            .ToArray();
    }

    private static ulong StableHash(string value)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        foreach (var ch in value)
        {
            hash ^= ch;
            hash *= prime;
        }

        return hash;
    }

    private sealed record OfficialOpeningBuildResult(
        MatchState State,
        IReadOnlyList<GameEvent> Events);

    private sealed record OfficialPlayerOpeningBuildResult(
        PlayerZones Zones,
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyDictionary<string, ObjectLocationState> ObjectLocations,
        IReadOnlyList<GameEvent> Events,
        long RngCursor);

    private void RequirePlayer(string playerId)
    {
        lock (seatGate)
        {
            if (!seats.ContainsKey(playerId))
            {
                throw new MatchSessionException(ErrorCodes.PlayerNotInRoom, "玩家不在房间中。");
            }
        }
    }

    private static string NewReconnectToken()
    {
        return $"rt_{Guid.NewGuid():N}";
    }

    private static string NormalizeClientIntentId(string clientIntentId)
    {
        if (string.IsNullOrWhiteSpace(clientIntentId))
        {
            throw new MatchSessionException(
                ErrorCodes.ClientIntentIdRequired,
                "客户端行动编号不能为空。");
        }

        return clientIntentId.Trim();
    }

    private static string NormalizeScenarioId(string scenarioId)
    {
        return string.IsNullOrWhiteSpace(scenarioId)
            ? throw new MatchSessionException(ErrorCodes.UnsupportedCommand, "开发测试场景编号不能为空。")
            : scenarioId.Trim();
    }

    private static MatchState BuildDevScenarioState(MatchState current, string scenarioId)
    {
        var p1 = PlayerBySeat(current, "P1");
        var p2 = PlayerBySeat(current, "P2");
        if (p1 is null || p2 is null)
        {
            throw new MatchSessionException(ErrorCodes.PlayerNotInRoom, "开发测试场景需要两名玩家都已加入房间。");
        }

        var seed = new DevScenarioSeed(p1, p2);
        return scenarioId switch
        {
            "basic-play" => BuildBasicPlayScenario(current, seed),
            "royal-attendant-legend-mode" => BuildRoyalAttendantLegendModeScenario(current, seed),
            "ornn-equipment-look" => BuildOrnnEquipmentLookScenario(current, seed),
            "movement" => BuildMovementScenario(current, seed),
            "spell-duel" => BuildSpellDuelScenario(current, seed),
            "spell-duel-focus" => BuildSpellDuelFocusScenario(current, seed),
            "battlefield-contest-stack" => BuildBattlefieldContestStackScenario(current, seed),
            "battlefield-contest-spell-duel-cleanup" => BuildBattlefieldContestSpellDuelCleanupScenario(current, seed),
            "battlefield-illegal-standby" => BuildBattlefieldIllegalStandbyScenario(current, seed),
            "battlefield-unattached-equipment-cleanup" => BuildBattlefieldUnattachedEquipmentCleanupScenario(current, seed),
            "typed-power-payment" => BuildTypedPowerPaymentScenario(current, seed),
            "typed-power-payment-recycle" => BuildTypedPowerPaymentRecycleScenario(current, seed),
            "typed-power-payment-over-recycle" => BuildTypedPowerPaymentOverRecycleScenario(current, seed),
            "typed-power-payment-double-recycle" => BuildTypedPowerPaymentDoubleRecycleScenario(current, seed),
            "typed-power-payment-mixed-recycle" => BuildTypedPowerPaymentMixedRecycleScenario(current, seed),
            "typed-power-payment-generic-mixed-recycle" => BuildTypedPowerPaymentMixedRecycleScenario(current, seed),
            "haste-payment-recycle" => BuildHastePaymentRecycleScenario(current, seed),
            "haste-payment-colored-recycle" => BuildHastePaymentColoredRecycleScenario(current, seed),
            "spellshield-multiple-tax" => BuildSpellshieldMultipleTaxScenario(current, seed),
            "spellshield-tax-insufficient-prompt" => BuildSpellshieldTaxInsufficientPromptScenario(current, seed),
            "unknown-play-source-prompt" => BuildUnknownPlaySourcePromptScenario(current, seed),
            "unknown-play-target-prompt" => BuildUnknownPlayTargetPromptScenario(current, seed),
            "unknown-hide-card-source-prompt" => BuildUnknownHideCardSourcePromptScenario(current, seed),
            "unknown-reveal-card-source-prompt" => BuildUnknownRevealCardSourcePromptScenario(current, seed),
            "unknown-assemble-source-prompt" => BuildUnknownAssembleSourcePromptScenario(current, seed),
            "unknown-assemble-target-prompt" => BuildUnknownAssembleTargetPromptScenario(current, seed),
            "unknown-legend-action-source-prompt" => BuildUnknownLegendActionSourcePromptScenario(current, seed),
            "unknown-legend-action-target-prompt" => BuildUnknownLegendActionTargetPromptScenario(current, seed),
            "unknown-activate-ability-source-prompt" => BuildUnknownActivateAbilitySourcePromptScenario(current, seed),
            "unknown-activate-ability-target-prompt" => BuildUnknownActivateAbilityTargetPromptScenario(current, seed),
            "unknown-move-unit-source-prompt" => BuildUnknownMoveUnitSourcePromptScenario(current, seed),
            "unknown-move-unit-battlefield-prompt" => BuildUnknownMoveUnitBattlefieldPromptScenario(current, seed),
            "unknown-rune-source-prompt" => BuildUnknownRuneSourcePromptScenario(current, seed),
            "unknown-declare-battle-source-prompt" => BuildUnknownDeclareBattleSourcePromptScenario(current, seed),
            "unknown-declare-battle-battlefield-prompt" => BuildUnknownDeclareBattleBattlefieldPromptScenario(current, seed),
            "assemble-payment-recycle" => BuildAssemblePaymentRecycleScenario(current, seed),
            "echo-stack" => BuildEchoStackScenario(current, seed),
            "priority-reaction-counter" => BuildPriorityReactionCounterScenario(current, seed),
            "standby-reaction" => BuildStandbyReactionScenario(current, seed),
            "ambush-reaction" => BuildAmbushReactionScenario(current, seed),
            "equipment" => BuildEquipmentScenario(current, seed),
            "status-showcase" => BuildStatusShowcaseScenario(current, seed),
            "resource-experience" => BuildResourceExperienceScenario(current, seed),
            "legend-act" => BuildLegendActScenario(current, seed),
            "legend-active-actions" => BuildLegendActiveActionsScenario(current, seed),
            "lifecycle-ephemeral" => BuildLifecycleEphemeralScenario(current, seed),
            "lifecycle-ephemeral-leblanc-static" => BuildLifecycleEphemeralLeblancStaticScenario(current, seed),
            "lifecycle-last-breath" => BuildLifecycleLastBreathScenario(current, seed),
            "control" => BuildControlScenario(current, seed),
            "battle-declare" => BuildBattleDeclareScenario(current, seed),
            "battle-prompt-filter" => BuildBattlePromptFilterScenario(current, seed),
            "battle-multi-defender" => BuildBattleMultiDefenderScenario(current, seed),
            "battle-same-priority-bulwark" => BuildBattleSamePriorityBulwarkScenario(current, seed),
            "battle-multi-attacker" => BuildBattleMultiAttackerScenario(current, seed),
            "battle-multi-participant" => BuildBattleMultiParticipantScenario(current, seed),
            "battle-no-result" => BuildBattleNoResultScenario(current, seed),
            "battlefield-ephemeral-steadfast" => BuildBattlefieldEphemeralSteadfastScenario(current, seed),
            "battlefield-held-move-to-base" => BuildBattlefieldHeldMoveToBaseScenario(current, seed),
            "battlefield-held-minion" => BuildBattlefieldHeldMinionScenario(current, seed),
            "battlefield-held-draw" => BuildBattlefieldHeldDrawScenario(current, seed),
            "battlefield-held-boon" => BuildBattlefieldHeldBoonScenario(current, seed),
            "battlefield-held-return-hero" => BuildBattlefieldHeldReturnHeroScenario(current, seed),
            "battlefield-conquer-boon-draw" => BuildBattlefieldConquerBoonDrawScenario(current, seed),
            "battlefield-conquer-warhawk" => BuildBattlefieldConquerWarhawkScenario(current, seed),
            "battlefield-winning-score" => BuildBattlefieldWinningScoreScenario(current, seed),
            "battlefield-first-turn-rune" => BuildBattlefieldFirstTurnRuneScenario(current, seed),
            "battlefield-first-turn-score" => BuildBattlefieldFirstTurnScoreScenario(current, seed),
            "battlefield-score-delay" => BuildBattlefieldScoreDelayScenario(current, seed),
            "battlefield-battle-destroyed-recall" => BuildBattlefieldBattleDestroyedRecallScenario(current, seed),
            "battlefield-turn-start-damage" => BuildBattlefieldTurnStartDamageScenario(current, seed),
            "battlefield-turn-start-destroy-draw" => BuildBattlefieldTurnStartDestroyDrawScenario(current, seed),
            "battlefield-held-score" => BuildBattlefieldHeldScoreScenario(current, seed),
            "battlefield-held-seven-units-win" => BuildBattlefieldHeldSevenUnitsWinScenario(current, seed),
            "battlefield-conquer-reveal-recycle" => BuildBattlefieldConquerRevealRecycleScenario(current, seed),
            "battlefield-move-power" => BuildBattlefieldMovePowerScenario(current, seed),
            "battlefield-static-prevent-play-units" => BuildBattlefieldStaticPreventPlayUnitsScenario(current, seed),
            "battlefield-static-echo-cost-reduction" => BuildBattlefieldStaticEchoCostReductionScenario(current, seed),
            "battlefield-held-next-spell-echo" => BuildBattlefieldHeldNextSpellEchoScenario(current, seed),
            "battlefield-held-next-spell-echo-prompt" => BuildBattlefieldHeldNextSpellEchoPromptScenario(current, seed),
            "battlefield-static-equipment-cost-reduction" => BuildBattlefieldStaticEquipmentCostReductionScenario(current, seed),
            "battlefield-eager-apprentice-spell-cost-reduction" => BuildBattlefieldEagerApprenticeSpellCostReductionScenario(current, seed),
            "raging-drake-next-spell-cost-reduction-prompt" => BuildRagingDrakeNextSpellCostReductionPromptScenario(current, seed),
            "battlefield-legend-attach-armament" => BuildBattlefieldLegendAttachArmamentScenario(current, seed),
            "battlefield-extra-standby" => BuildBattlefieldExtraStandbyScenario(current, seed),
            "battlefield-held-activate-conquest" => BuildBattlefieldHeldActivateConquestScenario(current, seed),
            "battlefield-friendly-spell-draw" => BuildBattlefieldFriendlySpellDrawScenario(current, seed),
            "battlefield-spell-power-bonus" => BuildBattlefieldSpellPowerBonusScenario(current, seed),
            "battlefield-high-cost-spell-insight" => BuildBattlefieldHighCostSpellInsightScenario(current, seed),
            "battlefield-unit-experience-ability" => BuildBattlefieldUnitExperienceAbilityScenario(current, seed),
            "battlefield-return-call-rune" => BuildBattlefieldReturnCallRuneScenario(current, seed),
            "battlefield-play-unit-boon" => BuildBattlefieldPlayUnitBoonScenario(current, seed),
            "battlefield-first-unit-move-other" => BuildBattlefieldFirstUnitMoveOtherScenario(current, seed),
            "battlefield-target-damage-bonus" => BuildBattlefieldTargetDamageBonusScenario(current, seed),
            "battlefield-held-unit-cost-increase" => BuildBattlefieldHeldUnitCostIncreaseScenario(current, seed),
            "battlefield-static-prevent-move-base" => BuildBattlefieldStaticPreventMoveBaseScenario(current, seed),
            "battlefield-static-roam" => BuildBattlefieldStaticRoamScenario(current, seed),
            "battlefield-conquer-mill" => BuildBattlefieldConquerMillScenario(current, seed),
            "battlefield-conquer-discard-draw" => BuildBattlefieldConquerDiscardDrawScenario(current, seed),
            "battlefield-conquer-recycle-rune" => BuildBattlefieldConquerRecycleRuneScenario(current, seed),
            "battlefield-defend-reveal-spell" => BuildBattlefieldDefendRevealSpellScenario(current, seed),
            "battlefield-held-runes" => BuildBattlefieldHeldRunesScenario(current, seed),
            "battlefield-held-rune" => BuildBattlefieldHeldRuneScenario(current, seed),
            "battlefield-static-power" => BuildBattlefieldStaticPowerScenario(current, seed),
            "battlefield-defender-steadfast" => BuildBattlefieldDefenderSteadfastScenario(current, seed),
            "battlefield-defend-move-to-base" => BuildBattlefieldDefendMoveToBaseScenario(current, seed),
            "battlefield-isolated-defender" => BuildBattlefieldIsolatedDefenderScenario(current, seed),
            "battlefield-conquer-ready-legend" => BuildBattlefieldConquerReadyLegendScenario(current, seed),
            "battlefield-conquer-ready-runes-end" => BuildBattlefieldConquerReadyRunesEndScenario(current, seed),
            "battlefield-conquer-draw-other" => BuildBattlefieldConquerDrawOtherScenario(current, seed),
            "battlefield-conquer-powerful-draw" => BuildBattlefieldConquerPowerfulDrawScenario(current, seed),
            "battlefield-conquer-sand-soldier" => BuildBattlefieldConquerSandSoldierScenario(current, seed),
            "battlefield-conquer-gold" => BuildBattlefieldConquerGoldScenario(current, seed),
            "battlefield-conquer-ready-equipment" => BuildBattlefieldConquerReadyEquipmentScenario(current, seed),
            "battle-score" => BuildBattleScoreScenario(current, seed),
            "test-decks" => BuildTestDecksScenario(current, seed),
            "specified-hand" => BuildSpecifiedHandScenario(current, seed),
            _ => throw new MatchSessionException(
                ErrorCodes.UnsupportedCommand,
                $"Unsupported dev scenario: {scenarioId}")
        };
    }

    private static MatchState BuildBattlefieldIllegalStandbyScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303071,
            71,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-ILLEGAL-STANDBY-001", "P1-STANDBY-ILLEGAL-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ILLEGAL-STANDBY-001"] = new(
                    "P1-BATTLEFIELD-ILLEGAL-STANDBY-001",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-STANDBY-ILLEGAL-001"] = new(
                    "P1-STANDBY-ILLEGAL-001",
                    cardNo: "OGN·121/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ILLEGAL-STANDBY-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-ILLEGAL-STANDBY-001"),
                ["P1-STANDBY-ILLEGAL-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-ILLEGAL-STANDBY-001")
            }
        };
    }

    private static MatchState BuildBattlefieldUnattachedEquipmentCleanupScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303072,
            72,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields:
                    [
                        "P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001",
                        "P1-EQUIPMENT-UNATTACHED-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001"] = new(
                    "P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-EQUIPMENT-UNATTACHED-001"] = new(
                    "P1-EQUIPMENT-UNATTACHED-001",
                    cardNo: "SFD·022/221",
                    power: 1,
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001"),
                ["P1-EQUIPMENT-UNATTACHED-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001")
            }
        };
    }

    private static MatchState BuildBasicPlayScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302690,
            690,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-UNIT-MIGHTY-FAERIE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MIGHTY-FAERIE"] = new(
                    "P1-UNIT-MIGHTY-FAERIE",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 4,
                    cardNo: "SFD·125/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildRoyalAttendantLegendModeScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303072,
            172,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-UNIT-ROYAL-ATTENDANT"],
                    legendZone: ["P1-LEGEND-ROYAL-TARGET"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-ROYAL-TARGET"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ROYAL-ATTENDANT"] = new(
                    "P1-UNIT-ROYAL-ATTENDANT",
                    cardNo: "SFD·039/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 3,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-LEGEND-ROYAL-TARGET"] = new(
                    "P1-LEGEND-ROYAL-TARGET",
                    cardNo: "FND-259/298",
                    isExhausted: true,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-LEGEND-ROYAL-TARGET"] = new(
                    "P2-LEGEND-ROYAL-TARGET",
                    cardNo: "OGN·257/298",
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildOrnnEquipmentLookScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303073,
            173,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(5, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck:
                    [
                        "P1-ORNN-EQUIPMENT-001",
                        "P1-ORNN-UNIT-001",
                        "P1-ORNN-SPELL-001",
                        "P1-ORNN-EQUIPMENT-002",
                        "P1-ORNN-DECK-TAIL-001"
                    ],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-UNIT-SFD-058-ORNN"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SFD-058-ORNN"] = new(
                    "P1-UNIT-SFD-058-ORNN",
                    cardNo: "SFD·058/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 5,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-ORNN-EQUIPMENT-001"] = new(
                    "P1-ORNN-EQUIPMENT-001",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-ORNN-UNIT-001"] = new(
                    "P1-ORNN-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-ORNN-SPELL-001"] = new(
                    "P1-ORNN-SPELL-001",
                    cardNo: "OGN·058/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-ORNN-EQUIPMENT-002"] = new(
                    "P1-ORNN-EQUIPMENT-002",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildTypedPowerPaymentScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302692,
            692,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 2
                    }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-SPELL-BULLET-TIME"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BULLET-TIME-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BULLET-TIME-UNIT-001"] = new(
                    "P2-BULLET-TIME-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildTypedPowerPaymentRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302693,
            693,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-SPELL-BULLET-TIME"],
                    baseZone: ["P1-RUNE-RED-PARTIAL-PAYMENT-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BULLET-TIME-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-PARTIAL-PAYMENT-001"] = new(
                    "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BULLET-TIME-UNIT-001"] = new(
                    "P2-BULLET-TIME-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildTypedPowerPaymentOverRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302694,
            694,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-SPELL-BULLET-TIME"],
                    baseZone:
                    [
                        "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                        "P1-RUNE-RED-EXTRA-PAYMENT-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BULLET-TIME-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-PARTIAL-PAYMENT-001"] = new(
                    "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-EXTRA-PAYMENT-001"] = new(
                    "P1-RUNE-RED-EXTRA-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BULLET-TIME-UNIT-001"] = new(
                    "P2-BULLET-TIME-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildTypedPowerPaymentDoubleRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302695,
            695,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-SPELL-BULLET-TIME"],
                    baseZone:
                    [
                        "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                        "P1-RUNE-RED-EXTRA-PAYMENT-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BULLET-TIME-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-PARTIAL-PAYMENT-001"] = new(
                    "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-EXTRA-PAYMENT-001"] = new(
                    "P1-RUNE-RED-EXTRA-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BULLET-TIME-UNIT-001"] = new(
                    "P2-BULLET-TIME-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildTypedPowerPaymentMixedRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302696,
            696,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-SPELL-BULLET-TIME"],
                    baseZone:
                    [
                        "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                        "P1-RUNE-BLUE-EXTRA-PAYMENT-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BULLET-TIME-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-PARTIAL-PAYMENT-001"] = new(
                    "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BLUE-EXTRA-PAYMENT-001"] = new(
                    "P1-RUNE-BLUE-EXTRA-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BULLET-TIME-UNIT-001"] = new(
                    "P2-BULLET-TIME-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildHastePaymentRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302697,
            697,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(5, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-UNIT-SIVIR"],
                    baseZone: ["P1-RUNE-PURPLE-HASTE-PAYMENT-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SIVIR"] = new(
                    "P1-UNIT-SIVIR",
                    cardNo: "SFD·143/221",
                    tags: [CardObjectTags.UnitCard, CardPermissionKeywordNames.Haste],
                    manaCost: 4,
                    power: 4,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-PURPLE-HASTE-PAYMENT-001"] = new(
                    "P1-RUNE-PURPLE-HASTE-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:purple"],
                    cardNo: "UNL-R04",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildHastePaymentColoredRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302698,
            698,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(5, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-UNIT-SIVIR"],
                    baseZone:
                    [
                        "P1-RUNE-BLUE-HASTE-PAYMENT-001",
                        "P1-RUNE-PURPLE-HASTE-PAYMENT-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SIVIR"] = new(
                    "P1-UNIT-SIVIR",
                    cardNo: "SFD·143/221",
                    tags: [CardObjectTags.UnitCard, CardPermissionKeywordNames.Haste],
                    manaCost: 4,
                    power: 4,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BLUE-HASTE-PAYMENT-001"] = new(
                    "P1-RUNE-BLUE-HASTE-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-PURPLE-HASTE-PAYMENT-001"] = new(
                    "P1-RUNE-PURPLE-HASTE-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:purple"],
                    cardNo: "UNL-R04",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildSpellshieldMultipleTaxScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304159,
            4159,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(6, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001"],
                    hand: ["P1-SPELL-SPIRIT-FIRE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001"],
                    battlefields:
                    [
                        "P2-SPIRIT-FIRE-SPELLSHIELD-001",
                        "P2-SPIRIT-FIRE-SPELLSHIELD2-001",
                        "P2-SPIRIT-FIRE-KEEPER-001"
                    ],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SPIRIT-FIRE"] = new(
                    "P1-SPELL-SPIRIT-FIRE",
                    cardNo: "OGN·256/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 3,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-SPIRIT-FIRE-SPELLSHIELD-001"] = new(
                    "P2-SPIRIT-FIRE-SPELLSHIELD-001",
                    cardNo: "OGN·013/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-SPIRIT-FIRE-SPELLSHIELD2-001"] = new(
                    "P2-SPIRIT-FIRE-SPELLSHIELD2-001",
                    cardNo: "SFD·085/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "法盾2"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-SPIRIT-FIRE-KEEPER-001"] = new(
                    "P2-SPIRIT-FIRE-KEEPER-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildSpellshieldTaxInsufficientPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304160,
            4160,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-SPELL-INCINERATE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-SPELLSHIELD-TAX-TARGET-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-INCINERATE"] = new(
                    "P1-SPELL-INCINERATE",
                    cardNo: "OGS·003/024",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-SPELLSHIELD-TAX-TARGET-001"] = new(
                    "P2-SPELLSHIELD-TAX-TARGET-001",
                    cardNo: "OGN·013/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildUnknownPlaySourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304161,
            4161,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-HAND-UNKNOWN-PLAY-SOURCE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNKNOWN-PLAY-SOURCE"] = new(
                    "P1-HAND-UNKNOWN-PLAY-SOURCE",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownPlayTargetPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304162,
            4162,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-SPELL-UNKNOWN-PLAY-TARGET"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-UNIT-UNKNOWN-PLAY-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-UNKNOWN-PLAY-TARGET"] = new(
                    "P1-SPELL-UNKNOWN-PLAY-TARGET",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-UNKNOWN-PLAY-TARGET"] = new(
                    "P2-UNIT-UNKNOWN-PLAY-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildUnknownHideCardSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304169,
            4169,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-HAND-UNKNOWN-HIDE-SOURCE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNKNOWN-HIDE-SOURCE"] = new(
                    "P1-HAND-UNKNOWN-HIDE-SOURCE",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownRevealCardSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304170,
            4170,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone: ["P1-FACEDOWN-UNKNOWN-REVEAL-SOURCE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FACEDOWN-UNKNOWN-REVEAL-SOURCE"] = new(
                    "P1-FACEDOWN-UNKNOWN-REVEAL-SOURCE",
                    isFaceDown: true,
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownAssembleSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304162,
            4162,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(0, 0, powerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone: ["P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE", "P1-UNIT-ASSEMBLE-TARGET"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE"] = new(
                    "P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownAssembleTargetPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304171,
            4171,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(0, 0, powerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone: ["P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER", "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER"] = new(
                    "P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-UNKNOWN-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownLegendActionSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304164,
            4164,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone: ["P1-UNIT-LEGEND-ACTION-TARGET", "P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT"],
                    battlefields: ["P1-BATTLEFIELD-PORO-FORGE"],
                    legendZone: ["P1-LEGEND-UNKNOWN-ACTION-SOURCE"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-UNKNOWN-ACTION-SOURCE"] = new(
                    "P1-LEGEND-UNKNOWN-ACTION-SOURCE",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-PORO-FORGE"] = new(
                    "P1-BATTLEFIELD-PORO-FORGE",
                    cardNo: BattlefieldGrantLegendAttachArmamentCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-LEGEND-ACTION-TARGET"] = new(
                    "P1-UNIT-LEGEND-ACTION-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT"] = new(
                    "P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownLegendActionTargetPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304172,
            4172,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET"],
                    legendZone: ["P1-LEGEND-YASUO-TARGET-FILTER"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-YASUO-TARGET-FILTER"] = new(
                    "P1-LEGEND-YASUO-TARGET-FILTER",
                    cardNo: "FND-259/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET"] = new(
                    "P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownActivateAbilitySourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304165,
            4165,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-BATTLEFIELD-MUTATION-GARDEN", "P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MUTATION-GARDEN"] = new(
                    "P1-BATTLEFIELD-MUTATION-GARDEN",
                    cardNo: BattlefieldGrantUnitExperienceCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownActivateAbilityTargetPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304175,
            4175,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(0, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-UNIT-XERATH-TARGET-FILTER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH-TARGET-FILTER"] = new(
                    "P1-UNIT-XERATH-TARGET-FILTER",
                    cardNo: "UNL-026/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"] = new(
                    "P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildUnknownMoveUnitSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304166,
            4166,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone: ["P1-UNIT-UNKNOWN-MOVE-SOURCE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-UNKNOWN-MOVE-SOURCE"] = new(
                    "P1-UNIT-UNKNOWN-MOVE-SOURCE",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownMoveUnitBattlefieldPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603304173,
            4173,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields:
                    [
                        "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN",
                        "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER",
                        "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"] = new(
                    "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-ROAM-MOVE-DESTINATION-FILTER"] = new(
                    "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER",
                    cardNo: "SFD·096/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "游走"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"] = new(
                    seed.P1,
                    "BATTLEFIELD",
                    "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"),
                ["P1-UNIT-ROAM-MOVE-DESTINATION-FILTER"] = new(
                    seed.P1,
                    "BATTLEFIELD",
                    "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"),
                ["P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"] = new(
                    seed.P1,
                    "BATTLEFIELD",
                    "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION")
            }
        };
    }

    private static MatchState BuildUnknownRuneSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304167,
            4167,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone: ["P1-RUNE-UNKNOWN-SOURCE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-RUNE-UNKNOWN-SOURCE"] = new(
                    "P1-RUNE-UNKNOWN-SOURCE",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownDeclareBattleSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304168,
            4168,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-BATTLE-UNKNOWN-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BATTLE-UNKNOWN-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-UNKNOWN-ATTACKER"] = new(
                    "P1-BATTLE-UNKNOWN-ATTACKER",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-UNKNOWN-DEFENDER"] = new(
                    "P2-BATTLE-UNKNOWN-DEFENDER",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildUnknownDeclareBattleBattlefieldPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304172,
            4172,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields:
                    [
                        "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION",
                        "P1-BATTLE-KNOWN-ATTACKER"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BATTLE-KNOWN-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-KNOWN-ATTACKER"] = new(
                    "P1-BATTLE-KNOWN-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-KNOWN-DEFENDER"] = new(
                    "P2-BATTLE-KNOWN-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildAssemblePaymentRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304163,
            4163,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-ASSEMBLE-PAYMENT-001"],
                    baseZone:
                    [
                        "P1-EQUIPMENT-LONG-SWORD-ASSEMBLE-PAYMENT",
                        "P1-UNIT-ASSEMBLE-PAYMENT-TARGET",
                        "P1-RUNE-RED-ASSEMBLE-PAYMENT-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD-ASSEMBLE-PAYMENT"] = new(
                    "P1-EQUIPMENT-LONG-SWORD-ASSEMBLE-PAYMENT",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-ASSEMBLE-PAYMENT-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-PAYMENT-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-ASSEMBLE-PAYMENT-001"] = new(
                    "P1-RUNE-RED-ASSEMBLE-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-ASSEMBLE-PAYMENT-001"] = new(
                    "P1-RUNE-BOTTOM-ASSEMBLE-PAYMENT-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildMovementScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302093,
            68,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-RIDE-THE-WIND"],
                    battlefields: ["P1-BATTLEFIELD-UNIT-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-RIDE-THE-WIND"] = new(
                    "P1-SPELL-RIDE-THE-WIND",
                    cardNo: "OGN·173/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-UNIT-001"] = new(
                    "P1-BATTLEFIELD-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    damage: 1,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-UNIT-001"] = new(
                    "P2-BATTLEFIELD-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildSpellDuelScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302019,
            9,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-HEXTECH-RAY"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HEXTECH-RAY"] = new(
                    "P1-SPELL-HEXTECH-RAY",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildSpellDuelFocusScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303023,
            93,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-SPELL-HEXTECH-RAY"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-UNIT-HEXTECH-RAY-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HEXTECH-RAY"] = new(
                    "P1-SPELL-HEXTECH-RAY",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-HEXTECH-RAY-001"] = new(
                    "P2-UNIT-HEXTECH-RAY-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            TimingState = TimingStates.SpellDuelOpen,
            FocusPlayerId = seed.P1,
            PassedFocusPlayerIds = []
        };
    }

    private static MatchState BuildBattlefieldContestStackScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303027,
            97,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-BATTLEFIELD-CONTEST-001", "P1-UNIT-CONTEST-001", "P1-STANDBY-CONTEST-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-UNIT-CONTEST-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-CONTEST-001"] = new(
                    "P1-BATTLEFIELD-CONTEST-001",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-CONTEST-001"] = new(
                    "P1-UNIT-CONTEST-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-STANDBY-CONTEST-001"] = new(
                    "P1-STANDBY-CONTEST-001",
                    cardNo: "OGN·121/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-CONTEST-001"] = new(
                    "P2-UNIT-CONTEST-001",
                    cardNo: "UNL-036/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            ActivePlayerId = seed.P2,
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = seed.P2,
            PassedPriorityPlayerIds = [seed.P1],
            StackItems =
            [
                new StackItemState(
                    "STACK-BATTLEFIELD-CONTEST-001",
                    seed.P1,
                    "P1-SPELL-BATTLEFIELD-CONTEST",
                    "TEST_RESOLVE",
                    "TEST-000",
                    [])
            ],
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-CONTEST-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-CONTEST-001"),
                ["P1-UNIT-CONTEST-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-CONTEST-001"),
                ["P1-STANDBY-CONTEST-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-CONTEST-001"),
                ["P2-UNIT-CONTEST-001"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-CONTEST-001")
            }
        };
    }

    private static MatchState BuildBattlefieldContestSpellDuelCleanupScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303028,
            98,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001", "P1-UNIT-SPELL-DUEL-CLEANUP-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-UNIT-SPELL-DUEL-CLEANUP-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001"] = new(
                    "P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-SPELL-DUEL-CLEANUP-001"] = new(
                    "P1-UNIT-SPELL-DUEL-CLEANUP-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-SPELL-DUEL-CLEANUP-001"] = new(
                    "P2-UNIT-SPELL-DUEL-CLEANUP-001",
                    cardNo: "SFD·125/221",
                    damage: 3,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            ActivePlayerId = seed.P2,
            TimingState = TimingStates.SpellDuelOpen,
            FocusPlayerId = seed.P2,
            PassedFocusPlayerIds = [seed.P1],
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001"),
                ["P1-UNIT-SPELL-DUEL-CLEANUP-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001"),
                ["P2-UNIT-SPELL-DUEL-CLEANUP-001"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001")
            }
        };
    }

    private static MatchState BuildEchoStackScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303061,
            61,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-DRAW-001", "P1-DRAW-002"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-CENTER-STAGE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-CENTER-STAGE"] = new(
                    "P1-SPELL-CENTER-STAGE",
                    cardNo: "UNL-061/219",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildPriorityReactionCounterScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303136,
            136,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = new(2, 0)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    hand: ["P2-SPELL-HARD-BARGAIN"],
                    battlefields: ["P2-UNIT-HARD-BARGAIN-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SPELL-HARD-BARGAIN"] = new(
                    "P2-SPELL-HARD-BARGAIN",
                    cardNo: "SFD·136/221",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-UNIT-HARD-BARGAIN-TARGET"] = new(
                    "P2-UNIT-HARD-BARGAIN-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = seed.P2,
            PassedPriorityPlayerIds = [seed.P1],
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-SPELL-INCINERATE",
                    seed.P1,
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P2-UNIT-HARD-BARGAIN-TARGET"])
            ]
        };
    }

    private static MatchState BuildStandbyReactionScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303197,
            97,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    baseZone: ["P1-FACEDOWN-OGN-TEEMO-PURPLE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FACEDOWN-OGN-TEEMO-PURPLE"] = new(
                    "P1-FACEDOWN-OGN-TEEMO-PURPLE",
                    isFaceDown: true,
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    manaCost: 2,
                    cardNo: "OGN·197/298")
            });

        return state with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = seed.P1,
            StackItems = [PendingProbeStackItem(seed)]
        };
    }

    private static MatchState BuildAmbushReactionScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303021,
            21,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-HAND-UNL-GLOOMY-APOTHECARY"],
                    battlefields: ["P1-BATTLEFIELD-FRIENDLY-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-GLOOMY-APOTHECARY"] = new(
                    "P1-HAND-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush],
                    manaCost: 3,
                    cardNo: "UNL-021/219"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(power: 2, tags: [CardObjectTags.UnitCard])
            });

        return state with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = seed.P1,
            StackItems = [PendingProbeStackItem(seed)]
        };
    }

    private static MatchState BuildEquipmentScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302787,
            787,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    2,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-LONG-SWORD-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-EQUIPMENT-LONG-SWORD"],
                    baseZone: ["P1-UNIT-ASSEMBLE-TARGET"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    manaCost: 2,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    power: 3,
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildStatusShowcaseScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303555,
            555,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 1),
                [seed.P2] = new(1, 0)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    baseZone:
                    [
                        "P1-UNIT-STATUS-ANCHOR",
                        "P1-EQUIPMENT-LONG-SWORD",
                        "P1-STANDBY-FACEDOWN",
                        "P1-EPHEMERAL-UNIT"
                    ],
                    battlefields: ["P1-BATTLE-DAMAGED"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    baseZone: ["P2-STUNNED-UNIT"],
                    battlefields: ["P2-CONTROLLED-UNIT"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-STATUS-ANCHOR"] = new(
                    "P1-UNIT-STATUS-ANCHOR",
                    power: 3,
                    untilEndOfTurnPowerModifier: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield, CardCombatKeywordNames.Roam],
                    untilEndOfTurnEffects: ["BOON:+1", "POWER:+2_UNTIL_END_OF_TURN"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard],
                    cardNo: "SFD·022/221",
                    attachedToObjectId: "P1-UNIT-STATUS-ANCHOR",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-STANDBY-FACEDOWN"] = new(
                    "P1-STANDBY-FACEDOWN",
                    isFaceDown: true,
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    manaCost: 2,
                    cardNo: "OGN·197/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-EPHEMERAL-UNIT"] = new(
                    "P1-EPHEMERAL-UNIT",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-DAMAGED"] = new(
                    "P1-BATTLE-DAMAGED",
                    damage: 2,
                    isExhausted: true,
                    isAttacking: true,
                    power: 4,
                    untilEndOfTurnPowerModifier: -1,
                    tags: [CardObjectTags.UnitCard, CardPermissionKeywordNames.Swift],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-STUNNED-UNIT"] = new(
                    "P2-STUNNED-UNIT",
                    damage: 1,
                    isExhausted: true,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "眩晕"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-CONTROLLED-UNIT"] = new(
                    "P2-CONTROLLED-UNIT",
                    isDefending: true,
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    ownerId: seed.P2,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildResourceExperienceScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303092,
            92,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(5, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-RESOURCE-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand:
                    [
                        "P1-UNIT-DEMACIA-ENVOY",
                        "P1-UNIT-MOSS-STEPPER"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-DEMACIA-ENVOY"] = new(
                    "P1-UNIT-DEMACIA-ENVOY",
                    cardNo: "UNL-092/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 3,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-MOSS-STEPPER"] = new(
                    "P1-UNIT-MOSS-STEPPER",
                    cardNo: "UNL-047/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 2,
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 2,
                [seed.P2] = 0
            }
        };
    }

    private static MatchState BuildLegendActScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603307905,
            905,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-LEGEND-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    legendZone: ["P1-LEGEND-POPPY"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-POPPY"] = new(
                    "P1-LEGEND-POPPY",
                    cardNo: "UNL-237/219",
                    ownerId: seed.P1,
                    controllerId: seed.P1,
                    tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-DRAW-001"] = new(
                    "P1-LEGEND-DRAW-001",
                    cardNo: "SFD·125/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1,
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            });

        return state with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 3,
                [seed.P2] = 0
            }
        };
    }

    private static MatchState BuildLegendActiveActionsScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603307906,
            906,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-LEGEND-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    baseZone: ["P1-LEGEND-BASE-UNIT", "P1-LEGEND-EPHEMERAL-UNIT"],
                    battlefields: ["P1-LEGEND-BATTLEFIELD-UNIT", "P1-LEGEND-EXHAUSTED-BATTLEFIELD-UNIT"],
                    legendZone:
                    [
                        "P1-LEGEND-YASUO",
                        "P1-LEGEND-LEE-SIN",
                        "P1-LEGEND-POPPY",
                        "P1-LEGEND-VIKTOR",
                        "P1-LEGEND-MISS-FORTUNE",
                        "P1-LEGEND-KHAZIX",
                        "P1-LEGEND-PYKE",
                        "P1-LEGEND-AZIR",
                        "P1-LEGEND-LILLIA"
                    ],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-YASUO"] = new("P1-LEGEND-YASUO", cardNo: "FND-259/298", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-LEE-SIN"] = new("P1-LEGEND-LEE-SIN", cardNo: "OGN·257/298", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-POPPY"] = new("P1-LEGEND-POPPY", cardNo: "UNL-237/219", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-VIKTOR"] = new("P1-LEGEND-VIKTOR", cardNo: "FND-265/298", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-MISS-FORTUNE"] = new("P1-LEGEND-MISS-FORTUNE", cardNo: "OGN·267/298", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-KHAZIX"] = new("P1-LEGEND-KHAZIX", cardNo: "UNL-201/219", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-PYKE"] = new("P1-LEGEND-PYKE", cardNo: "UNL-185/219", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-AZIR"] = new("P1-LEGEND-AZIR", cardNo: "SFD·197/221", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-LILLIA"] = new("P1-LEGEND-LILLIA", cardNo: "UNL-189/219", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-BASE-UNIT"] = new("P1-LEGEND-BASE-UNIT", power: 2, tags: [CardObjectTags.UnitCard], ownerId: seed.P1, controllerId: seed.P1),
                ["P1-LEGEND-EPHEMERAL-UNIT"] = new("P1-LEGEND-EPHEMERAL-UNIT", power: 1, tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral], ownerId: seed.P1, controllerId: seed.P1),
                ["P1-LEGEND-BATTLEFIELD-UNIT"] = new("P1-LEGEND-BATTLEFIELD-UNIT", power: 3, tags: [CardObjectTags.UnitCard], ownerId: seed.P1, controllerId: seed.P1),
                ["P1-LEGEND-EXHAUSTED-BATTLEFIELD-UNIT"] = new("P1-LEGEND-EXHAUSTED-BATTLEFIELD-UNIT", power: 2, isExhausted: true, tags: [CardObjectTags.UnitCard], ownerId: seed.P1, controllerId: seed.P1),
                ["P1-LEGEND-DRAW-001"] = new("P1-LEGEND-DRAW-001", cardNo: "SFD·125/221", ownerId: seed.P1, controllerId: seed.P1, power: 3, tags: [CardObjectTags.UnitCard])
            }) with
            {
                PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [seed.P1] = 3,
                    [seed.P2] = 0
                },
                UntilEndOfTurnEffects = [$"PLAYED_ARMAMENT_THIS_TURN:{seed.P1}"]
            };
    }

    private static MatchState BuildLifecycleEphemeralScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303801,
            801,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    baseZone: ["P1-EPHEMERAL-OTHER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    baseZone: ["P2-EPHEMERAL-BASE", "P2-KEEP-BASE"],
                    battlefields: ["P2-EPHEMERAL-BATTLEFIELD"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EPHEMERAL-OTHER"] = new(power: 1, tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral]),
                ["P2-EPHEMERAL-BASE"] = new(power: 1, tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral]),
                ["P2-EPHEMERAL-BATTLEFIELD"] = new(power: 2, tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral]),
                ["P2-KEEP-BASE"] = new(power: 3, tags: [CardObjectTags.UnitCard])
            });
    }

    private static MatchState BuildLifecycleEphemeralLeblancStaticScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303811,
            811,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    baseZone: ["P2-EPHEMERAL-BASE", "P2-KEEP-BASE"],
                    battlefields:
                    [
                        "P2-BATTLEFIELD-LEBLANC",
                        "P2-LEBLANC-STATIC",
                        "P2-EPHEMERAL-PROTECTED",
                        "P2-BATTLEFIELD-OTHER",
                        "P2-EPHEMERAL-OTHER-BATTLEFIELD"
                    ],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-EPHEMERAL-BASE"] = new(
                    "P2-EPHEMERAL-BASE",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-KEEP-BASE"] = new(
                    "P2-KEEP-BASE",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-LEBLANC"] = new(
                    "P2-BATTLEFIELD-LEBLANC",
                    cardNo: "UNL·T01",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-LEBLANC-STATIC"] = new(
                    "P2-LEBLANC-STATIC",
                    cardNo: "UNL-090a/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardCombatKeywordNames.BackRow],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-EPHEMERAL-PROTECTED"] = new(
                    "P2-EPHEMERAL-PROTECTED",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-OTHER"] = new(
                    "P2-BATTLEFIELD-OTHER",
                    cardNo: "UNL·T03",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-EPHEMERAL-OTHER-BATTLEFIELD"] = new(
                    "P2-EPHEMERAL-OTHER-BATTLEFIELD",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-MAIN-001"] = new(seed.P1, "MAIN_DECK"),
                ["P1-RUNE-001"] = new(seed.P1, "RUNE_DECK"),
                ["P1-RUNE-002"] = new(seed.P1, "RUNE_DECK"),
                ["P2-MAIN-001"] = new(seed.P2, "MAIN_DECK"),
                ["P2-RUNE-001"] = new(seed.P2, "RUNE_DECK"),
                ["P2-RUNE-002"] = new(seed.P2, "RUNE_DECK"),
                ["P2-EPHEMERAL-BASE"] = new(seed.P2, "BASE"),
                ["P2-KEEP-BASE"] = new(seed.P2, "BASE"),
                ["P2-BATTLEFIELD-LEBLANC"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-LEBLANC"),
                ["P2-LEBLANC-STATIC"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-LEBLANC"),
                ["P2-EPHEMERAL-PROTECTED"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-LEBLANC"),
                ["P2-BATTLEFIELD-OTHER"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-OTHER"),
                ["P2-EPHEMERAL-OTHER-BATTLEFIELD"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-OTHER")
            }
        };
    }

    private static MatchState BuildLifecycleLastBreathScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303802,
            802,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-VENGEANCE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-LAST-BREATH-DRAW-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-WATCHFUL-SENTINEL-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-VENGEANCE"] = new(
                    "P1-SPELL-VENGEANCE",
                    cardNo: "OGN·229/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 4,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-WATCHFUL-SENTINEL-001"] = new(
                    "P2-WATCHFUL-SENTINEL-001",
                    cardNo: "OGN·096/298",
                    ownerId: seed.P2,
                    controllerId: seed.P2,
                    power: 1,
                    tags: [CardObjectTags.UnitCard])
            });
    }

    private static MatchState BuildControlScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302821,
            821,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(5, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-HOSTILE-TAKEOVER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-HOSTILE-TAKEOVER-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-HOSTILE-TAKEOVER-TARGET"] = new(power: 4, isExhausted: true, tags: ["CARD_TYPE:UNIT"])
            });
    }

    private static MatchState BuildBattleDeclareScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303025,
            95,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLE-ATTACKER-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLE-DEFENDER-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-ATTACKER-001"] = new(
                    "P1-BATTLE-ATTACKER-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-DEFENDER-001"] = new(
                    "P2-BATTLE-DEFENDER-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlePromptFilterScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303071,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    baseZone: ["P1-BATTLE-PROMPT-BASE-UNIT"],
                    battlefields:
                    [
                        "P1-BATTLE-PROMPT-ATTACKER",
                        "P1-BATTLE-PROMPT-FACEDOWN",
                        "P1-BATTLE-PROMPT-ALREADY-ATTACKING"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    baseZone: ["P2-BATTLE-PROMPT-BASE-DEFENDER"],
                    battlefields:
                    [
                        "P2-BATTLE-PROMPT-DEFENDER",
                        "P2-BATTLE-PROMPT-FACEDOWN",
                        "P2-BATTLE-PROMPT-ALREADY-DEFENDING",
                        "P2-BATTLE-PROMPT-EQUIPMENT"
                    ],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-PROMPT-BASE-UNIT"] = new(
                    "P1-BATTLE-PROMPT-BASE-UNIT",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-PROMPT-ATTACKER"] = new(
                    "P1-BATTLE-PROMPT-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-PROMPT-FACEDOWN"] = new(
                    "P1-BATTLE-PROMPT-FACEDOWN",
                    isFaceDown: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-PROMPT-ALREADY-ATTACKING"] = new(
                    "P1-BATTLE-PROMPT-ALREADY-ATTACKING",
                    isAttacking: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-PROMPT-BASE-DEFENDER"] = new(
                    "P2-BATTLE-PROMPT-BASE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-PROMPT-DEFENDER"] = new(
                    "P2-BATTLE-PROMPT-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-PROMPT-FACEDOWN"] = new(
                    "P2-BATTLE-PROMPT-FACEDOWN",
                    isFaceDown: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-PROMPT-ALREADY-DEFENDING"] = new(
                    "P2-BATTLE-PROMPT-ALREADY-DEFENDING",
                    isDefending: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-PROMPT-EQUIPMENT"] = new(
                    "P2-BATTLE-PROMPT-EQUIPMENT",
                    cardNo: "SFD·011/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattleMultiDefenderScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303072,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLE-MULTI-VOLIBEAR"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLE-MULTI-LEBLANC", "P2-BATTLE-MULTI-KITTEN"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-MULTI-VOLIBEAR"] = new(
                    "P1-BATTLE-MULTI-VOLIBEAR",
                    cardNo: "OGN·158/298",
                    power: 10,
                    tags: [CardObjectTags.UnitCard, "坚守3", "壁垒"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-MULTI-LEBLANC"] = new(
                    "P2-BATTLE-MULTI-LEBLANC",
                    cardNo: "UNL-090/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "后排"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-MULTI-KITTEN"] = new(
                    "P2-BATTLE-MULTI-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattleSamePriorityBulwarkScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303074,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLE-SAME-VOLIBEAR"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLE-SAME-BULWARK-A", "P2-BATTLE-SAME-BULWARK-B"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-SAME-VOLIBEAR"] = new(
                    "P1-BATTLE-SAME-VOLIBEAR",
                    cardNo: "OGN·158/298",
                    power: 10,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-SAME-BULWARK-A"] = new(
                    "P2-BATTLE-SAME-BULWARK-A",
                    cardNo: "UNL-036/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-SAME-BULWARK-B"] = new(
                    "P2-BATTLE-SAME-BULWARK-B",
                    cardNo: "UNL-036/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattleMultiAttackerScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303073,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLE-MULTI-GAREN", "P1-BATTLE-MULTI-YI"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLE-MULTI-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-MULTI-GAREN"] = new(
                    "P1-BATTLE-MULTI-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-MULTI-YI"] = new(
                    "P1-BATTLE-MULTI-YI",
                    cardNo: "UNL-059/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-MULTI-DEFENDER"] = new(
                    "P2-BATTLE-MULTI-DEFENDER",
                    cardNo: "UNL-036/219",
                    power: 6,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattleMultiParticipantScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303076,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLE-MULTI-PARTICIPANT-GAREN", "P1-BATTLE-MULTI-PARTICIPANT-YI"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLE-MULTI-PARTICIPANT-BULWARK", "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-MULTI-PARTICIPANT-GAREN"] = new(
                    "P1-BATTLE-MULTI-PARTICIPANT-GAREN",
                    cardNo: "OGS·007/024",
                    power: 6,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-MULTI-PARTICIPANT-YI"] = new(
                    "P1-BATTLE-MULTI-PARTICIPANT-YI",
                    cardNo: "UNL-059/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-MULTI-PARTICIPANT-BULWARK"] = new(
                    "P2-BATTLE-MULTI-PARTICIPANT-BULWARK",
                    cardNo: "UNL-036/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-MULTI-PARTICIPANT-DEFENDER"] = new(
                    "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER",
                    cardNo: "UNL-036/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattleNoResultScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303075,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLE-NO-RESULT-GAREN"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLE-NO-RESULT-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-NO-RESULT-GAREN"] = new(
                    "P1-BATTLE-NO-RESULT-GAREN",
                    cardNo: "OGS·007/024",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-NO-RESULT-DEFENDER"] = new(
                    "P2-BATTLE-NO-RESULT-DEFENDER",
                    cardNo: "UNL-036/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldEphemeralSteadfastScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303032,
            102,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-EPHEMERAL-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-BLACK-FLAME", "P2-BATTLEFIELD-EPHEMERAL-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-EPHEMERAL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-EPHEMERAL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-BLACK-FLAME"] = new(
                    "P2-BATTLEFIELD-BLACK-FLAME",
                    cardNo: BattlefieldEphemeralUnitsSteadfastCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-EPHEMERAL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-EPHEMERAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldMinionScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303029,
            99,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-MINION-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-UNITY-SANCTUM", "P2-BATTLEFIELD-MINION-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MINION-ATTACKER"] = new(
                    "P1-BATTLEFIELD-MINION-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-UNITY-SANCTUM"] = new(
                    "P2-BATTLEFIELD-UNITY-SANCTUM",
                    cardNo: BattlefieldHoldCreateMinionCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-MINION-DEFENDER"] = new(
                    "P2-BATTLEFIELD-MINION-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303026,
            96,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-HELD-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-BATTLEFIELD-HELD-DRAW-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-DREAM-TREE", "P2-BATTLEFIELD-HELD-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-HELD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-HELD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-DREAM-TREE"] = new(
                    "P2-BATTLEFIELD-DREAM-TREE",
                    cardNo: BattlefieldHoldDrawCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-HELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-HELD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldMoveToBaseScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303046,
            116,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-REHEARSAL-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-REHEARSAL-HALL", "P2-BATTLEFIELD-REHEARSAL-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-REHEARSAL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-REHEARSAL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-REHEARSAL-HALL"] = new(
                    "P2-BATTLEFIELD-REHEARSAL-HALL",
                    cardNo: BattlefieldHeldMoveUnitToBaseCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-REHEARSAL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-REHEARSAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldBoonScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303043,
            113,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-BOON-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-NAVORI-ARENA", "P2-BATTLEFIELD-BOON-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-BOON-ATTACKER"] = new(
                    "P1-BATTLEFIELD-BOON-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-NAVORI-ARENA"] = new(
                    "P2-BATTLEFIELD-NAVORI-ARENA",
                    cardNo: BattlefieldHoldGrantBoonCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-BOON-DEFENDER"] = new(
                    "P2-BATTLEFIELD-BOON-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldReturnHeroScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303059,
            114,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-TOMB-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-HALLOWED-TOMB", "P2-BATTLEFIELD-TOMB-DEFENDER"],
                    graveyard: ["P2-HERO-TOMB-RETURN"],
                    legendZone: ["P2-LEGEND-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-TOMB-ATTACKER"] = new(
                    "P1-BATTLEFIELD-TOMB-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-HALLOWED-TOMB"] = new(
                    "P2-BATTLEFIELD-HALLOWED-TOMB",
                    cardNo: BattlefieldHeldReturnHeroCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-TOMB-DEFENDER"] = new(
                    "P2-BATTLEFIELD-TOMB-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-HERO-TOMB-RETURN"] = new(
                    "P2-HERO-TOMB-RETURN",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "CARD_CATEGORY:英雄单位"],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldConquerBoonDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303045,
            115,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-BATTLEFIELD-BOON-DRAW-CARD"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-SHIRANA-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-SHIRANA-MONASTERY", "P2-BATTLEFIELD-SHIRANA-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SHIRANA-ATTACKER"] = new(
                    "P1-BATTLEFIELD-SHIRANA-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-BOON-DRAW-CARD"] = new(
                    "P1-BATTLEFIELD-BOON-DRAW-CARD",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-SHIRANA-MONASTERY"] = new(
                    "P2-BATTLEFIELD-SHIRANA-MONASTERY",
                    cardNo: BattlefieldConquerConsumeBoonDrawCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-SHIRANA-DEFENDER"] = new(
                    "P2-BATTLEFIELD-SHIRANA-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldConquerWarhawkScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303047,
            117,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-HUNTING-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-HUNTING-GROUNDS", "P2-BATTLEFIELD-HUNTING-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-HUNTING-ATTACKER"] = new(
                    "P1-BATTLEFIELD-HUNTING-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-HUNTING-GROUNDS"] = new(
                    "P2-BATTLEFIELD-HUNTING-GROUNDS",
                    cardNo: BattlefieldConquerOverkillCreateWarhawkCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-HUNTING-DEFENDER"] = new(
                    "P2-BATTLEFIELD-HUNTING-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldWinningScoreScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303048,
            117,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-HIGHROAD"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    graveyard: ["P2-BURNOUT-RECYCLE-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-HIGHROAD"] = new(
                    "P1-BATTLEFIELD-HIGHROAD",
                    cardNo: BattlefieldWinningScoreSeedCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BURNOUT-RECYCLE-001"] = new(
                    "P2-BURNOUT-RECYCLE-001",
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
            {
                PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [seed.P1] = 7,
                    [seed.P2] = 0
                }
            };
    }

    private static MatchState BuildBattlefieldFirstTurnRuneScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303049,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-RUNE-OBELISK"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck:
                    [
                        "P2-RUNE-001",
                        "P2-RUNE-002",
                        "P2-RUNE-003",
                        "P2-RUNE-004"
                    ],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RUNE-OBELISK"] = new(
                    "P1-BATTLEFIELD-RUNE-OBELISK",
                    cardNo: BattlefieldFirstTurnExtraRuneCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldFirstTurnScoreScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303050,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-GLORY-ARENA"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GLORY-ARENA"] = new(
                    "P1-BATTLEFIELD-GLORY-ARENA",
                    cardNo: BattlefieldFirstTurnScoreCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldScoreDelayScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303052,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-GLORY-ARENA", "P1-BATTLEFIELD-FORGOTTEN-MONUMENT"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GLORY-ARENA"] = new(
                    "P1-BATTLEFIELD-GLORY-ARENA",
                    cardNo: BattlefieldFirstTurnScoreCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-FORGOTTEN-MONUMENT"] = new(
                    "P1-BATTLEFIELD-FORGOTTEN-MONUMENT",
                    cardNo: BattlefieldScoreDelayCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldBattleDestroyedRecallScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303067,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = new(3, 0)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-BLOOD-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLEFIELD-BLOOD-ALTAR", "P2-BATTLEFIELD-BLOOD-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-BLOOD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-BLOOD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-BLOOD-ALTAR"] = new(
                    "P2-BATTLEFIELD-BLOOD-ALTAR",
                    cardNo: BattlefieldDestroyedInBattleRecallCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-BLOOD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-BLOOD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldTurnStartDamageScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303058,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-FROST-HOLD", "P1-BATTLEFIELD-FROST-FALLING"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"],
                    battlefields: ["P2-BATTLEFIELD-FROST-SURVIVOR"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FROST-HOLD"] = new(
                    "P1-BATTLEFIELD-FROST-HOLD",
                    cardNo: BattlefieldTurnStartDamageAllUnitsCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-FROST-FALLING"] = new(
                    "P1-BATTLEFIELD-FROST-FALLING",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-FROST-SURVIVOR"] = new(
                    "P2-BATTLEFIELD-FROST-SURVIVOR",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldTurnStartDestroyDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303060,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-ROSE-DRAW-001", "P2-NORMAL-DRAW-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"],
                    battlefields: ["P2-BATTLEFIELD-ROSE-LAB", "P2-BATTLEFIELD-ROSE-SACRIFICE"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-ROSE-LAB"] = new(
                    "P2-BATTLEFIELD-ROSE-LAB",
                    cardNo: BattlefieldTurnStartDestroyUnitDrawCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-ROSE-SACRIFICE"] = new(
                    "P2-BATTLEFIELD-ROSE-SACRIFICE",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-ROSE-DRAW-001"] = new(
                    "P2-ROSE-DRAW-001",
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-NORMAL-DRAW-001"] = new(
                    "P2-NORMAL-DRAW-001",
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldScoreScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303051,
            151,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = new(0, 4)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-ENERGY-HUB", "P2-BATTLEFIELD-ENERGY-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ENERGY-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-ENERGY-HUB"] = new(
                    "P2-BATTLEFIELD-ENERGY-HUB",
                    cardNo: BattlefieldHeldPayPowerScoreCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"] = new(
                    "P2-BATTLEFIELD-ENERGY-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldSevenUnitsWinScenario(MatchState current, DevScenarioSeed seed)
    {
        var defenderUnitObjectIds = Enumerable.Range(1, 7)
            .Select(index => $"P2-BATTLEFIELD-GRAND-UNIT-{index:000}")
            .ToArray();
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-GRAND-ATTACKER"] = new(
                "P1-BATTLEFIELD-GRAND-ATTACKER",
                cardNo: "SFD·125/221",
                power: 1,
                tags: [CardObjectTags.UnitCard],
                ownerId: seed.P1,
                controllerId: seed.P1),
            ["P2-BATTLEFIELD-GRAND-PLAZA"] = new(
                "P2-BATTLEFIELD-GRAND-PLAZA",
                cardNo: BattlefieldHeldSevenUnitsWinCardNo,
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: seed.P2,
                controllerId: seed.P2)
        };
        foreach (var defenderUnitObjectId in defenderUnitObjectIds)
        {
            cardObjects[defenderUnitObjectId] = new CardObjectState(
                defenderUnitObjectId,
                cardNo: "SFD·125/221",
                power: string.Equals(defenderUnitObjectId, defenderUnitObjectIds[0], StringComparison.Ordinal) ? 3 : 1,
                tags: [CardObjectTags.UnitCard],
                ownerId: seed.P2,
                controllerId: seed.P2);
        }

        return BuildScenarioState(
            current,
            seed,
            2603303052,
            152,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-GRAND-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: new[] { "P2-BATTLEFIELD-GRAND-PLAZA" }.Concat(defenderUnitObjectIds).ToArray(),
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            cardObjects);
    }

    private static MatchState BuildBattlefieldConquerRevealRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303053,
            153,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck:
                    [
                        "P1-BATTLEFIELD-CANDLE-001",
                        "P1-BATTLEFIELD-CANDLE-002",
                        "P1-BATTLEFIELD-CANDLE-003"
                    ],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-CANDLELIT-SANCTUM", "P1-BATTLEFIELD-CANDLE-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-CANDLE-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-CANDLELIT-SANCTUM"] = new(
                    "P1-BATTLEFIELD-CANDLELIT-SANCTUM",
                    cardNo: BattlefieldConquerRevealRecycleCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-CANDLE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-CANDLE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-CANDLE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-CANDLE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-CANDLE-001"] = new(
                    "P1-BATTLEFIELD-CANDLE-001",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-CANDLE-002"] = new(
                    "P1-BATTLEFIELD-CANDLE-002",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-CANDLE-003"] = new(
                    "P1-BATTLEFIELD-CANDLE-003",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldMovePowerScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303063,
            163,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-BACK-ALLEY-BAR", "P1-BATTLEFIELD-BAR-REGULAR"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-BACK-ALLEY-BAR"] = new(
                    "P1-BATTLEFIELD-BACK-ALLEY-BAR",
                    cardNo: BattlefieldMovedUnitPowerPlusOneCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-BAR-REGULAR"] = new(
                    "P1-BATTLEFIELD-BAR-REGULAR",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldStaticPreventPlayUnitsScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303064,
            164,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-HAND-UNL-GLOOMY-APOTHECARY"],
                    battlefields: ["P1-BATTLEFIELD-FALLING-ROCKS", "P1-BATTLEFIELD-FRIENDLY-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-GLOOMY-APOTHECARY"] = new(
                    "P1-HAND-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush],
                    manaCost: 3,
                    cardNo: "UNL-021/219"),
                ["P1-BATTLEFIELD-FALLING-ROCKS"] = new(
                    "P1-BATTLEFIELD-FALLING-ROCKS",
                    cardNo: BattlefieldPreventUnitPlayCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = seed.P1,
            StackItems = [PendingProbeStackItem(seed)]
        };
    }

    private static MatchState BuildBattlefieldStaticEchoCostReductionScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303065,
            165,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001", "P1-MAIN-002"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-CENTER-STAGE"],
                    battlefields: ["P1-BATTLEFIELD-MARAI-SPIRE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-CENTER-STAGE"] = new(
                    "P1-SPELL-CENTER-STAGE",
                    cardNo: "UNL-061/219",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-MARAI-SPIRE"] = new(
                    "P1-BATTLEFIELD-MARAI-SPIRE",
                    cardNo: BattlefieldEchoCostReductionCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldHeldNextSpellEchoScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303066,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-PILTOVER-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLEFIELD-PILTOVER-ACADEMY", "P2-BATTLEFIELD-PILTOVER-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-PILTOVER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-PILTOVER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-PILTOVER-ACADEMY"] = new(
                    "P2-BATTLEFIELD-PILTOVER-ACADEMY",
                    cardNo: BattlefieldHeldNextSpellEchoCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-PILTOVER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-PILTOVER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldNextSpellEchoPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303067,
            67,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = new(4, 0)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-DRAW-001", "P2-DRAW-002"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    hand: ["P2-SPELL-CENTER-STAGE"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SPELL-CENTER-STAGE"] = new(
                    "P2-SPELL-CENTER-STAGE",
                    cardNo: "UNL-061/219",
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            ActivePlayerId = seed.P2,
            TurnPlayerId = seed.P2,
            PriorityPlayerId = seed.P2,
            UntilEndOfTurnEffects = [$"BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:{seed.P2}"]
        };
    }

    private static MatchState BuildBattlefieldStaticEquipmentCostReductionScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303066,
            166,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-EQUIPMENT-LONG-SWORD"],
                    battlefields: ["P1-BATTLEFIELD-ORNN-FORGE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-ORNN-FORGE"] = new(
                    "P1-BATTLEFIELD-ORNN-FORGE",
                    cardNo: BattlefieldEquipmentCostReductionCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldEagerApprenticeSpellCostReductionScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303069,
            169,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-WELL-TRAINED"],
                    battlefields: ["P1-UNIT-EAGER-APPRENTICE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-WELL-TRAINED"] = new(
                    "P1-SPELL-WELL-TRAINED",
                    cardNo: "OGN·058/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-EAGER-APPRENTICE"] = new(
                    "P1-UNIT-EAGER-APPRENTICE",
                    cardNo: "OGN·084/298",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    cardNo: "UNL-097/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildRagingDrakeNextSpellCostReductionPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303070,
            170,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-WELL-TRAINED"],
                    baseZone: ["P1-UNIT-RAGING-DRAKE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-WELL-TRAINED"] = new(
                    "P1-SPELL-WELL-TRAINED",
                    cardNo: "OGN·058/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-RAGING-DRAKE"] = new(
                    "P1-UNIT-RAGING-DRAKE",
                    cardNo: "OGN·031/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "龙"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    cardNo: "UNL-097/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            UntilEndOfTurnEffects =
            [
                $"{RagingDrakeNextSpellCostReductionEffectPrefix}{seed.P1}:P1-UNIT-RAGING-DRAKE"
            ]
        };
    }

    private static MatchState BuildBattlefieldLegendAttachArmamentScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303068,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    baseZone: ["P1-UNIT-PORO-FORGE-TARGET", "P1-EQUIPMENT-PORO-FORGE-ARMAMENT"],
                    battlefields: ["P1-BATTLEFIELD-PORO-FORGE"],
                    legendZone: ["P1-LEGEND-PORO-FORGE"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-PORO-FORGE"] = new(
                    "P1-LEGEND-PORO-FORGE",
                    cardNo: "UNL-237/219",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-PORO-FORGE-TARGET"] = new(
                    "P1-UNIT-PORO-FORGE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-EQUIPMENT-PORO-FORGE-ARMAMENT"] = new(
                    "P1-EQUIPMENT-PORO-FORGE-ARMAMENT",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-PORO-FORGE"] = new(
                    "P1-BATTLEFIELD-PORO-FORGE",
                    cardNo: BattlefieldGrantLegendAttachArmamentCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldExtraStandbyScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303069,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    hand: ["P1-STANDBY-BANDLE-TEEMO"],
                    battlefields: ["P1-BATTLEFIELD-BANDLE-TREE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-STANDBY-BANDLE-TEEMO"] = new(
                    "P1-STANDBY-BANDLE-TEEMO",
                    cardNo: "OGN·121/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-BANDLE-TREE"] = new(
                    "P1-BATTLEFIELD-BANDLE-TREE",
                    cardNo: BattlefieldExtraStandbyCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldHeldActivateConquestScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303070,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-BATTLEFIELD-RECKONER-DRAW-001"],
                    battlefields:
                    [
                        "P2-BATTLEFIELD-RECKONER-ARENA",
                        "P2-BATTLEFIELD-BAD-PORO",
                        "P2-BATTLEFIELD-KAISA"
                    ],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RECKONER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-RECKONER-ARENA"] = new(
                    "P2-BATTLEFIELD-RECKONER-ARENA",
                    cardNo: BattlefieldHeldActivateConquestEffectsCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-BAD-PORO"] = new(
                    "P2-BATTLEFIELD-BAD-PORO",
                    cardNo: "UNL-222/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "魄罗", "海盗"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-KAISA"] = new(
                    "P2-BATTLEFIELD-KAISA",
                    cardNo: "OGN·039/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-RECKONER-DRAW-001"] = new(
                    "P2-BATTLEFIELD-RECKONER-DRAW-001",
                    cardNo: "SFD·001/221",
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldFriendlySpellDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303066,
            167,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-DRAWN"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-SAVAGE-STRENGTH"],
                    battlefields: ["P1-BATTLEFIELD-DREAMTREE", "P1-BATTLEFIELD-ALLY"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SAVAGE-STRENGTH"] = new(
                    "P1-SPELL-SAVAGE-STRENGTH",
                    cardNo: "SFD·034/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-MAIN-DRAWN"] = new(
                    "P1-MAIN-DRAWN",
                    cardNo: "SFD·001/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-ALLY"] = new(
                    "P1-BATTLEFIELD-ALLY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1,
                    power: 2),
                ["P1-BATTLEFIELD-DREAMTREE"] = new(
                    "P1-BATTLEFIELD-DREAMTREE",
                    cardNo: BattlefieldFriendlySpellDrawCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldSpellPowerBonusScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303066,
            168,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-SAVAGE-STRENGTH"],
                    battlefields: ["P1-BATTLEFIELD-WASTE-HALL", "P1-BATTLEFIELD-ALLY"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SAVAGE-STRENGTH"] = new(
                    "P1-SPELL-SAVAGE-STRENGTH",
                    cardNo: "SFD·034/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-ALLY"] = new(
                    "P1-BATTLEFIELD-ALLY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1,
                    power: 2),
                ["P1-BATTLEFIELD-WASTE-HALL"] = new(
                    "P1-BATTLEFIELD-WASTE-HALL",
                    cardNo: BattlefieldSpellPowerBonusCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldHighCostSpellInsightScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303067,
            169,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(7, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-INSIGHT-RECYCLE", "P1-MAIN-KEEPER"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-MOONFALL"],
                    battlefields: ["P1-BATTLEFIELD-LOST-LIBRARY"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-ENEMY"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-MOONFALL"] = new(
                    "P1-SPELL-MOONFALL",
                    cardNo: "UNL-066/219",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-INSIGHT-RECYCLE"] = new(
                    "P1-INSIGHT-RECYCLE",
                    cardNo: "SFD·001/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-MAIN-KEEPER"] = new(
                    "P1-MAIN-KEEPER",
                    cardNo: "SFD·002/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-ENEMY"] = new(
                    "P2-BATTLEFIELD-ENEMY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2,
                    power: 2),
                ["P1-BATTLEFIELD-LOST-LIBRARY"] = new(
                    "P1-BATTLEFIELD-LOST-LIBRARY",
                    cardNo: BattlefieldHighCostSpellInsightCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldUnitExperienceAbilityScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303075,
            175,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-MUTATION-GARDEN", "P1-BATTLEFIELD-EXPERIENCE-UNIT"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MUTATION-GARDEN"] = new(
                    "P1-BATTLEFIELD-MUTATION-GARDEN",
                    cardNo: BattlefieldGrantUnitExperienceCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-EXPERIENCE-UNIT"] = new(
                    "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                    cardNo: "SFD·001/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldReturnCallRuneScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303076,
            176,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-GHOST-BAY-RUNE-001", "P1-GHOST-BAY-RUNE-002"],
                    hand: ["P1-SPELL-RECONSIDER"],
                    battlefields: ["P1-BATTLEFIELD-GHOST-BAY", "P1-BATTLEFIELD-RETURN-UNIT"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-RECONSIDER"] = new(
                    "P1-SPELL-RECONSIDER",
                    cardNo: "OGN·104/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-GHOST-BAY"] = new(
                    "P1-BATTLEFIELD-GHOST-BAY",
                    cardNo: BattlefieldUnitReturnedCallRuneCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-RETURN-UNIT"] = new(
                    "P1-BATTLEFIELD-RETURN-UNIT",
                    cardNo: "SFD·001/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-GHOST-BAY-RUNE-001"] = new(
                    "P1-GHOST-BAY-RUNE-001",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-GHOST-BAY-RUNE-002"] = new(
                    "P1-GHOST-BAY-RUNE-002",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldTargetDamageBonusScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303068,
            170,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-PUNISHMENT"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-VOID-GATE", "P2-BATTLEFIELD-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-PUNISHMENT"] = new(
                    "P1-SPELL-PUNISHMENT",
                    cardNo: "UNL-007/219",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-TARGET"] = new(
                    "P2-BATTLEFIELD-TARGET",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2,
                    power: 5),
                ["P2-BATTLEFIELD-VOID-GATE"] = new(
                    "P2-BATTLEFIELD-VOID-GATE",
                    cardNo: BattlefieldTargetSpellSkillDamageBonusCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldPlayUnitBoonScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303070,
            172,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-UNIT-CRAFTSMAN"],
                    battlefields: ["P1-BATTLEFIELD-IDOL-VALLEY"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CRAFTSMAN"] = new(
                    "P1-UNIT-CRAFTSMAN",
                    cardNo: "OGN·211/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-IDOL-VALLEY"] = new(
                    "P1-BATTLEFIELD-IDOL-VALLEY",
                    cardNo: BattlefieldPlayUnitPayOneBoonCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldHeldUnitCostIncreaseScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303069,
            171,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-UNIT-CRAFTSMAN"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CRAFTSMAN"] = new(
                    "P1-UNIT-CRAFTSMAN",
                    cardNo: "OGN·211/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            }) with
            {
                UntilEndOfTurnEffects = [$"{BattlefieldHeldUnitCostIncreaseEffectPrefix}{seed.P1}"]
            };
    }

    private static MatchState BuildBattlefieldFirstUnitMoveOtherScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303071,
            173,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-UNIT-CRAFTSMAN"],
                    battlefields: ["P1-BATTLEFIELD-METEOR-SPRING", "P1-BATTLEFIELD-ALLY"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CRAFTSMAN"] = new(
                    "P1-UNIT-CRAFTSMAN",
                    cardNo: "OGN·211/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-METEOR-SPRING"] = new(
                    "P1-BATTLEFIELD-METEOR-SPRING",
                    cardNo: BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-ALLY"] = new(
                    "P1-BATTLEFIELD-ALLY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1,
                    power: 2)
            });
    }

    private static MatchState BuildBattlefieldStaticPreventMoveBaseScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303062,
            162,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-VILEMAW-LAIR", "P1-BATTLEFIELD-TRAPPED-UNIT"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-VILEMAW-LAIR"] = new(
                    "P1-BATTLEFIELD-VILEMAW-LAIR",
                    cardNo: BattlefieldPreventMoveToBaseCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-TRAPPED-UNIT"] = new(
                    "P1-BATTLEFIELD-TRAPPED-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldStaticRoamScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303061,
            161,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-WIND-HILL", "P1-BATTLEFIELD-WIND-RUNNER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-WIND-HILL"] = new(
                    "P1-BATTLEFIELD-WIND-HILL",
                    cardNo: BattlefieldStaticRoamCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-WIND-RUNNER"] = new(
                    "P1-BATTLEFIELD-WIND-RUNNER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldConquerMillScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303027,
            97,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-BATTLEFIELD-MILL-001", "P1-BATTLEFIELD-MILL-002", "P1-BATTLEFIELD-MILL-003"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-SCRAPYARD", "P1-BATTLEFIELD-CONQUER-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-CONQUER-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SCRAPYARD"] = new(
                    "P1-BATTLEFIELD-SCRAPYARD",
                    cardNo: BattlefieldConquerMillTwoCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-CONQUER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-CONQUER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-CONQUER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-CONQUER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldConquerDiscardDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303031,
            101,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-BATTLEFIELD-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-BATTLEFIELD-DISCARD-001"],
                    battlefields: ["P1-BATTLEFIELD-ZAUN-SUMP", "P1-BATTLEFIELD-DISCARD-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-DISCARD-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ZAUN-SUMP"] = new(
                    "P1-BATTLEFIELD-ZAUN-SUMP",
                    cardNo: BattlefieldConquerDiscardDrawCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-DISCARD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-DISCARD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-DISCARD-001"] = new(
                    "P1-BATTLEFIELD-DISCARD-001",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-DISCARD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DISCARD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldConquerRecycleRuneScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303034,
            104,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-002"],
                    baseZone: ["P1-BATTLEFIELD-RECYCLE-RUNE-001"],
                    battlefields: ["P1-BATTLEFIELD-THUNDER-RUNE", "P1-BATTLEFIELD-RECYCLE-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-RECYCLE-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-THUNDER-RUNE"] = new(
                    "P1-BATTLEFIELD-THUNDER-RUNE",
                    cardNo: BattlefieldConquerRecycleRuneCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-RECYCLE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RECYCLE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-RECYCLE-RUNE-001"] = new(
                    "P1-BATTLEFIELD-RECYCLE-RUNE-001",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-RECYCLE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-RECYCLE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldDefendRevealSpellScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303035,
            105,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-REVEAL-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-BATTLEFIELD-REVEAL-SPELL", "P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-RAVENBLOOM", "P2-BATTLEFIELD-REVEAL-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-REVEAL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-REVEAL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-RAVENBLOOM"] = new(
                    "P2-BATTLEFIELD-RAVENBLOOM",
                    cardNo: BattlefieldDefendRevealSpellCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-REVEAL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-REVEAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-REVEAL-SPELL"] = new(
                    "P2-BATTLEFIELD-REVEAL-SPELL",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldRunesScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303030,
            100,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-BATTLEFIELD-RUNE-001"],
                    battlefields: ["P1-BATTLEFIELD-RUNES-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-BATTLEFIELD-RUNE-001"],
                    battlefields: ["P2-BATTLEFIELD-CONFETTI-TREE", "P2-BATTLEFIELD-RUNES-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RUNES-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RUNES-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-CONFETTI-TREE"] = new(
                    "P2-BATTLEFIELD-CONFETTI-TREE",
                    cardNo: BattlefieldHoldEachPlayerCallRuneCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-RUNES-DEFENDER"] = new(
                    "P2-BATTLEFIELD-RUNES-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldRuneScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303041,
            111,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001"],
                    battlefields: ["P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-BATTLEFIELD-SINGLE-RUNE-001"],
                    battlefields: ["P2-BATTLEFIELD-STAR-PEAK", "P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-STAR-PEAK"] = new(
                    "P2-BATTLEFIELD-STAR-PEAK",
                    cardNo: BattlefieldHoldCallRuneCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldStaticPowerScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303028,
            98,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-POWER-PLUS", "P1-BATTLEFIELD-STATIC-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-STATIC-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-POWER-PLUS"] = new(
                    "P1-BATTLEFIELD-POWER-PLUS",
                    cardNo: BattlefieldAllUnitsPowerPlusOneCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-STATIC-ATTACKER"] = new(
                    "P1-BATTLEFIELD-STATIC-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-STATIC-DEFENDER"] = new(
                    "P2-BATTLEFIELD-STATIC-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldDefenderSteadfastScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303033,
            103,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-FORTIFIED-POSITION", "P2-BATTLEFIELD-FORTIFIED-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"] = new(
                    "P1-BATTLEFIELD-FORTIFIED-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-FORTIFIED-POSITION"] = new(
                    "P2-BATTLEFIELD-FORTIFIED-POSITION",
                    cardNo: BattlefieldDefenderSteadfastTwoCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-FORTIFIED-DEFENDER"] = new(
                    "P2-BATTLEFIELD-FORTIFIED-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldDefendMoveToBaseScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303044,
            114,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-PLUNDER-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-PLUNDER-ALLEY", "P2-BATTLEFIELD-PLUNDER-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-PLUNDER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-PLUNDER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-PLUNDER-ALLEY"] = new(
                    "P2-BATTLEFIELD-PLUNDER-ALLEY",
                    cardNo: BattlefieldDefendMoveFriendlyUnitToBaseCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-PLUNDER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-PLUNDER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldIsolatedDefenderScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303036,
            106,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-ISOLATED-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-FORBIDDEN-WASTELAND", "P2-BATTLEFIELD-ISOLATED-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ISOLATED-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ISOLATED-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-FORBIDDEN-WASTELAND"] = new(
                    "P2-BATTLEFIELD-FORBIDDEN-WASTELAND",
                    cardNo: BattlefieldIsolatedDefenderSteadfastMinusTwoCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-ISOLATED-DEFENDER"] = new(
                    "P2-BATTLEFIELD-ISOLATED-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldConquerReadyLegendScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303037,
            107,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-LEGEND-HALL", "P1-BATTLEFIELD-READY-ATTACKER"],
                    legendZone: ["P1-LEGEND-READY-TARGET"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-READY-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-LEGEND-HALL"] = new(
                    "P1-BATTLEFIELD-LEGEND-HALL",
                    cardNo: BattlefieldConquerPayOneReadyLegendCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-READY-ATTACKER"] = new(
                    "P1-BATTLEFIELD-READY-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-LEGEND-READY-TARGET"] = new(
                    "P1-LEGEND-READY-TARGET",
                    cardNo: "SFD·195/221",
                    isExhausted: true,
                    tags: ["CARD_TYPE:LEGEND"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-READY-DEFENDER"] = new(
                    "P2-BATTLEFIELD-READY-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldConquerReadyRunesEndScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303054,
            124,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-DECK-001"],
                    baseZone: ["P1-BATTLEFIELD-READY-RUNE-001", "P1-BATTLEFIELD-READY-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-MOUNT-TARGON", "P1-BATTLEFIELD-RUNE-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-RUNE-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MOUNT-TARGON"] = new(
                    "P1-BATTLEFIELD-MOUNT-TARGON",
                    cardNo: BattlefieldConquerReadyTwoRunesAtEndCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-RUNE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RUNE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-RUNE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-RUNE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-READY-RUNE-001"] = new(
                    "P1-BATTLEFIELD-READY-RUNE-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-READY-RUNE-002"] = new(
                    "P1-BATTLEFIELD-READY-RUNE-002",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldConquerDrawOtherScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303038,
            108,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck:
                    [
                        "P1-BATTLEFIELD-DRAW-OTHER-001",
                        "P1-BATTLEFIELD-DRAW-OTHER-002",
                        "P1-BATTLEFIELD-DRAW-OTHER-003"
                    ],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields:
                    [
                        "P1-BATTLEFIELD-THRONE-OF-POWER",
                        "P1-BATTLEFIELD-OTHER-001",
                        "P1-BATTLEFIELD-OTHER-002",
                        "P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-THRONE-OF-POWER"] = new(
                    "P1-BATTLEFIELD-THRONE-OF-POWER",
                    cardNo: BattlefieldConquerDrawForOtherBattlefieldsCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-OTHER-001"] = new(
                    "P1-BATTLEFIELD-OTHER-001",
                    cardNo: BattlefieldHoldDrawCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-OTHER-002"] = new(
                    "P1-BATTLEFIELD-OTHER-002",
                    cardNo: BattlefieldHoldCreateMinionCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DRAW-OTHER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-DRAW-OTHER-001"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-001",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-DRAW-OTHER-002"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-002",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-DRAW-OTHER-003"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-003",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldConquerPowerfulDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303039,
            109,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-BATTLEFIELD-POWERFUL-DRAW-001", "P1-BATTLEFIELD-POWERFUL-DRAW-002"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-SUNKEN-TEMPLE", "P1-BATTLEFIELD-POWERFUL-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-POWERFUL-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SUNKEN-TEMPLE"] = new(
                    "P1-BATTLEFIELD-SUNKEN-TEMPLE",
                    cardNo: BattlefieldConquerPowerfulPayOneDrawCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-POWERFUL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-POWERFUL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-POWERFUL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-POWERFUL-DRAW-001"] = new(
                    "P1-BATTLEFIELD-POWERFUL-DRAW-001",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-POWERFUL-DRAW-002"] = new(
                    "P1-BATTLEFIELD-POWERFUL-DRAW-002",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldConquerSandSoldierScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303053,
            123,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-EMPEROR-SHRINE", "P1-BATTLEFIELD-SAND-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-SAND-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-EMPEROR-SHRINE"] = new(
                    "P1-BATTLEFIELD-EMPEROR-SHRINE",
                    cardNo: BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-SAND-ATTACKER"] = new(
                    "P1-BATTLEFIELD-SAND-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-SAND-DEFENDER"] = new(
                    "P2-BATTLEFIELD-SAND-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldConquerGoldScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303040,
            110,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-TREASURE-PILE", "P1-BATTLEFIELD-GOLD-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-GOLD-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-TREASURE-PILE"] = new(
                    "P1-BATTLEFIELD-TREASURE-PILE",
                    cardNo: BattlefieldConquerPayOneCreateGoldCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-GOLD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-GOLD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-GOLD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-GOLD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldConquerReadyEquipmentScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303042,
            112,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    baseZone: ["P1-BATTLEFIELD-EQUIPMENT-HOST", "P1-BATTLEFIELD-ARMAMENT"],
                    battlefields: ["P1-BATTLEFIELD-MOONVEIL-ALTAR", "P1-BATTLEFIELD-EQUIPMENT-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MOONVEIL-ALTAR"] = new(
                    "P1-BATTLEFIELD-MOONVEIL-ALTAR",
                    cardNo: BattlefieldConquerReadyEquipmentCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-EQUIPMENT-ATTACKER"] = new(
                    "P1-BATTLEFIELD-EQUIPMENT-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-EQUIPMENT-HOST"] = new(
                    "P1-BATTLEFIELD-EQUIPMENT-HOST",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-ARMAMENT"] = new(
                    "P1-BATTLEFIELD-ARMAMENT",
                    cardNo: "SFD·022/221",
                    isExhausted: true,
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    attachedToObjectId: "P1-BATTLEFIELD-EQUIPMENT-HOST",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"] = new(
                    "P2-BATTLEFIELD-EQUIPMENT-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattleScoreScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302501,
            75,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-BATTLE-COMMAND-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    baseZone: ["P1-BATTLE-COMMAND-BASE-001"],
                    battlefields: ["P1-BATTLE-COMMAND-FIELD-KEEPER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    baseZone: ["P2-BATTLE-COMMAND-BASE-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-COMMAND-BASE-001"] = new(power: 4, damage: 1, tags: ["CARD_TYPE:UNIT"]),
                ["P1-BATTLE-COMMAND-FIELD-KEEPER"] = new(power: 2, tags: ["CARD_TYPE:UNIT"]),
                ["P2-BATTLE-COMMAND-BASE-001"] = new(power: 3, isExhausted: true, tags: ["CARD_TYPE:UNIT"])
            });
    }

    private static MatchState BuildTestDecksScenario(MatchState current, DevScenarioSeed seed)
    {
        var p1MainDeck = new[]
        {
            "P1-DECK-MIGHTY-FAERIE-001",
            "P1-DECK-MIGHTY-FAERIE-002",
            "P1-DECK-HEXTECH-RAY-001",
            "P1-DECK-LONG-SWORD-001",
            "P1-DECK-RIDE-THE-WIND-001",
            "P1-DECK-HOSTILE-TAKEOVER-001",
            "P1-DECK-DEMACIA-ENVOY-001",
            "P1-DECK-MOSS-STEPPER-001",
            "P1-DECK-MIGHTY-FAERIE-003",
            "P1-DECK-HEXTECH-RAY-002",
            "P1-DECK-LONG-SWORD-002",
            "P1-DECK-RIDE-THE-WIND-002"
        };
        var p2MainDeck = new[]
        {
            "P2-DECK-MIGHTY-FAERIE-001",
            "P2-DECK-MIGHTY-FAERIE-002",
            "P2-DECK-HEXTECH-RAY-001",
            "P2-DECK-LONG-SWORD-001",
            "P2-DECK-RIDE-THE-WIND-001",
            "P2-DECK-HOSTILE-TAKEOVER-001",
            "P2-DECK-DEMACIA-ENVOY-001",
            "P2-DECK-MOSS-STEPPER-001",
            "P2-DECK-MIGHTY-FAERIE-003",
            "P2-DECK-HEXTECH-RAY-002",
            "P2-DECK-LONG-SWORD-002",
            "P2-DECK-RIDE-THE-WIND-002"
        };
        var p1RuneDeck = Enumerable.Range(1, 8)
            .Select(index => $"P1-RUNE-DECK-{index:000}")
            .ToArray();
        var p2RuneDeck = Enumerable.Range(1, 8)
            .Select(index => $"P2-RUNE-DECK-{index:000}")
            .ToArray();

        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-UNIT-MIGHTY-FAERIE"] = Unit("P1-UNIT-MIGHTY-FAERIE", seed.P1, "SFD·125/221", 3, manaCost: 4),
            ["P1-SPELL-HEXTECH-RAY"] = Spell("P1-SPELL-HEXTECH-RAY", seed.P1, "OGN·009/298", manaCost: 2),
            ["P1-EQUIPMENT-LONG-SWORD"] = Equipment("P1-EQUIPMENT-LONG-SWORD", seed.P1, "SFD·022/221", manaCost: 2),
            ["P1-SPELL-RIDE-THE-WIND"] = Spell("P1-SPELL-RIDE-THE-WIND", seed.P1, "OGN·173/298", manaCost: 2),
            ["P1-SPELL-HOSTILE-TAKEOVER"] = Spell("P1-SPELL-HOSTILE-TAKEOVER", seed.P1, "SFD·202/221", manaCost: 4),
            ["P2-UNIT-MIGHTY-FAERIE"] = Unit("P2-UNIT-MIGHTY-FAERIE", seed.P2, "SFD·125/221", 3, manaCost: 4),
            ["P2-SPELL-HEXTECH-RAY"] = Spell("P2-SPELL-HEXTECH-RAY", seed.P2, "OGN·009/298", manaCost: 2),
            ["P2-EQUIPMENT-LONG-SWORD"] = Equipment("P2-EQUIPMENT-LONG-SWORD", seed.P2, "SFD·022/221", manaCost: 2),
            ["P2-SPELL-RIDE-THE-WIND"] = Spell("P2-SPELL-RIDE-THE-WIND", seed.P2, "OGN·173/298", manaCost: 2),
            ["P2-SPELL-HOSTILE-TAKEOVER"] = Spell("P2-SPELL-HOSTILE-TAKEOVER", seed.P2, "SFD·202/221", manaCost: 4),
            ["P1-BASE-GUARD-001"] = Unit("P1-BASE-GUARD-001", seed.P1, "SFD·125/221", 2, damage: 1),
            ["P1-BATTLEFIELD-UNIT-001"] = Unit("P1-BATTLEFIELD-UNIT-001", seed.P1, "SFD·125/221", 3),
            ["P1-BATTLE-ATTACKER-001"] = Unit("P1-BATTLE-ATTACKER-001", seed.P1, "SFD·125/221", 4),
            ["P1-LEGEND-POPPY"] = Legend("P1-LEGEND-POPPY", seed.P1, "UNL-237/219"),
            ["P1-CHAMPION-001"] = Hero("P1-CHAMPION-001", seed.P1, "FND-259/298"),
            ["P2-BASE-GUARD-001"] = Unit("P2-BASE-GUARD-001", seed.P2, "SFD·125/221", 2),
            ["P2-UNIT-001"] = Unit("P2-UNIT-001", seed.P2, "SFD·125/221", 2),
            ["P2-HOSTILE-TAKEOVER-TARGET"] = Unit("P2-HOSTILE-TAKEOVER-TARGET", seed.P2, "SFD·125/221", 4, isExhausted: true),
            ["P2-BATTLE-DEFENDER-001"] = Unit("P2-BATTLE-DEFENDER-001", seed.P2, "SFD·125/221", 3),
            ["P2-LEGEND-YASUO"] = Legend("P2-LEGEND-YASUO", seed.P2, "FND-259/298"),
            ["P2-CHAMPION-001"] = Hero("P2-CHAMPION-001", seed.P2, "FND-259/298")
        };

        AddDeckCards(cardObjects, p1MainDeck, seed.P1);
        AddDeckCards(cardObjects, p2MainDeck, seed.P2);
        AddRuneCards(cardObjects, p1RuneDeck, seed.P1);
        AddRuneCards(cardObjects, p2RuneDeck, seed.P2);

        var state = BuildScenarioState(
            current,
            seed,
            2603307001,
            1001,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(12, 3),
                [seed.P2] = new(12, 3)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: p1MainDeck,
                    runeDeck: p1RuneDeck,
                    hand:
                    [
                        "P1-UNIT-MIGHTY-FAERIE",
                        "P1-SPELL-HEXTECH-RAY",
                        "P1-EQUIPMENT-LONG-SWORD",
                        "P1-SPELL-RIDE-THE-WIND",
                        "P1-SPELL-HOSTILE-TAKEOVER"
                    ],
                    baseZone: ["P1-BASE-GUARD-001"],
                    battlefields: ["P1-BATTLEFIELD-UNIT-001", "P1-BATTLE-ATTACKER-001"],
                    legendZone: ["P1-LEGEND-POPPY"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: p2MainDeck,
                    runeDeck: p2RuneDeck,
                    hand:
                    [
                        "P2-UNIT-MIGHTY-FAERIE",
                        "P2-SPELL-HEXTECH-RAY",
                        "P2-EQUIPMENT-LONG-SWORD",
                        "P2-SPELL-RIDE-THE-WIND",
                        "P2-SPELL-HOSTILE-TAKEOVER"
                    ],
                    baseZone: ["P2-BASE-GUARD-001"],
                    battlefields: ["P2-UNIT-001", "P2-HOSTILE-TAKEOVER-TARGET", "P2-BATTLE-DEFENDER-001"],
                    legendZone: ["P2-LEGEND-YASUO"],
                    championZone: ["P2-CHAMPION-001"])
            },
            cardObjects);

        return state with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 3,
                [seed.P2] = 3
            }
        };

        static CardObjectState Unit(
            string objectId,
            string playerId,
            string cardNo,
            int power,
            int manaCost = 0,
            int damage = 0,
            bool isExhausted = false)
        {
            return Card(objectId, playerId, cardNo, CardObjectTags.UnitCard, power, manaCost, damage, isExhausted);
        }

        static CardObjectState Spell(string objectId, string playerId, string cardNo, int manaCost)
        {
            return Card(objectId, playerId, cardNo, CardObjectTags.SpellCard, power: 0, manaCost);
        }

        static CardObjectState Equipment(string objectId, string playerId, string cardNo, int manaCost)
        {
            return Card(objectId, playerId, cardNo, CardObjectTags.EquipmentCard, power: 0, manaCost);
        }

        static CardObjectState Legend(string objectId, string playerId, string cardNo)
        {
            return Card(objectId, playerId, cardNo, "CARD_TYPE:LEGEND", power: 0, manaCost: 0);
        }

        static CardObjectState Hero(string objectId, string playerId, string cardNo)
        {
            return Card(objectId, playerId, cardNo, "CARD_TYPE:HERO", power: 0, manaCost: 0);
        }

        static CardObjectState Card(
            string objectId,
            string playerId,
            string cardNo,
            string cardType,
            int power,
            int manaCost,
            int damage = 0,
            bool isExhausted = false)
        {
            return new CardObjectState(
                objectId,
                damage: damage,
                power: power,
                isExhausted: isExhausted,
                tags: [cardType],
                manaCost: manaCost,
                cardNo: cardNo,
                ownerId: playerId,
                controllerId: playerId);
        }

        static void AddDeckCards(
            Dictionary<string, CardObjectState> cardObjects,
            IReadOnlyList<string> objectIds,
            string playerId)
        {
            var templates = new[]
            {
                ("SFD·125/221", CardObjectTags.UnitCard, 3, 4),
                ("OGN·009/298", CardObjectTags.SpellCard, 0, 2),
                ("SFD·022/221", CardObjectTags.EquipmentCard, 0, 2),
                ("OGN·173/298", CardObjectTags.SpellCard, 0, 2),
                ("SFD·202/221", CardObjectTags.SpellCard, 0, 4),
                ("SFD·125/221", CardObjectTags.UnitCard, 3, 4)
            };

            for (var index = 0; index < objectIds.Count; index++)
            {
                var template = templates[index % templates.Length];
                cardObjects[objectIds[index]] = Card(
                    objectIds[index],
                    playerId,
                    template.Item1,
                    template.Item2,
                    template.Item3,
                    template.Item4);
            }
        }

        static void AddRuneCards(
            Dictionary<string, CardObjectState> cardObjects,
            IReadOnlyList<string> objectIds,
            string playerId)
        {
            foreach (var objectId in objectIds)
            {
                cardObjects[objectId] = Card(
                    objectId,
                    playerId,
                    "SFD·001/221",
                    CardObjectTags.RuneCard,
                    power: 0,
                    manaCost: 0);
            }
        }
    }

    private static MatchState BuildSpecifiedHandScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302999,
            999,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(12, 2),
                [seed.P2] = new(2, 0)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001", "P1-MAIN-002"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand:
                    [
                        "P1-UNIT-MIGHTY-FAERIE",
                        "P1-SPELL-HEXTECH-RAY",
                        "P1-EQUIPMENT-LONG-SWORD",
                        "P1-SPELL-RIDE-THE-WIND"
                    ],
                    battlefields: ["P1-BATTLEFIELD-UNIT-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-UNIT-001", "P2-HOSTILE-TAKEOVER-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNIT-001"] = new(power: 3, isExhausted: true, tags: ["CARD_TYPE:UNIT"]),
                ["P2-UNIT-001"] = new(power: 2, tags: ["CARD_TYPE:UNIT"]),
                ["P2-HOSTILE-TAKEOVER-TARGET"] = new(power: 4, isExhausted: true, tags: ["CARD_TYPE:UNIT"])
            });
    }

    private static MatchState BuildScenarioState(
        MatchState current,
        DevScenarioSeed seed,
        long scenarioSeed,
        int turnNumber,
        IReadOnlyDictionary<string, RunePool> runePools,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects)
    {
        return current with
        {
            TurnNumber = turnNumber,
            ActivePlayerId = seed.P1,
            Status = MatchStatuses.InProgress,
            ReadyPlayerIds = [seed.P1, seed.P2],
            TurnPlayerId = seed.P1,
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = runePools,
            PlayerZones = playerZones,
            PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 0,
                [seed.P2] = 0
            },
            CardObjects = cardObjects,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = [],
            StackItems = [],
            FocusPlayerId = null,
            PassedFocusPlayerIds = [],
            WinnerPlayerId = null,
            DestroyedUnitOwnerIdsThisTurn = [],
            Seed = scenarioSeed,
            RngCursor = 0,
            UntilEndOfTurnEffects = [],
            ExtraTurnPlayerId = null
        };
    }

    private static PlayerZones Zones(
        IReadOnlyList<string>? mainDeck = null,
        IReadOnlyList<string>? runeDeck = null,
        IReadOnlyList<string>? hand = null,
        IReadOnlyList<string>? baseZone = null,
        IReadOnlyList<string>? battlefields = null,
        IReadOnlyList<string>? graveyard = null,
        IReadOnlyList<string>? banished = null,
        IReadOnlyList<string>? legendZone = null,
        IReadOnlyList<string>? championZone = null)
    {
        return new PlayerZones(
            mainDeck ?? [],
            runeDeck ?? [],
            hand ?? [],
            baseZone ?? [],
            battlefields ?? [],
            graveyard ?? [],
            banished ?? [],
            legendZone ?? [],
            championZone ?? []);
    }

    private static StackItemState PendingProbeStackItem(DevScenarioSeed seed)
    {
        return new StackItemState(
            "STACK-0-P2-SPELL-PROBE",
            seed.P2,
            "P2-SPELL-PROBE",
            "PENDING_TEST_SPELL",
            "TEST-000",
            []);
    }

    private static string? PlayerBySeat(MatchState state, string seat)
    {
        return state.Seats
            .Where(entry => string.Equals(entry.Value, seat, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .FirstOrDefault();
    }

    private static string NormalizePlayerId(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new MatchSessionException(ErrorCodes.PlayerIdRequired, "玩家编号不能为空。");
        }

        return playerId.Trim();
    }

    private sealed record DevScenarioSeed(string P1, string P2);
}

public sealed class MatchSessionException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
