using System.Text.Json;
using System.Text.Json.Serialization;
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
    public const string TurnStart = "TURN_START";
    public const string Main = "MAIN";
    public const string TurnEnd = "TURN_END";
}

public static class TimingStates
{
    public const string Room = "ROOM";
    public const string NeutralOpen = "NEUTRAL_OPEN";
    public const string NeutralClosed = "NEUTRAL_CLOSED";
    public const string SpellDuelOpen = "SPELL_DUEL_OPEN";
    public const string SpellDuelClosed = "SPELL_DUEL_CLOSED";
}

public sealed record RunePool(int Mana, int Power)
{
    public static RunePool Empty { get; } = new(0, 0);
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
        string? destination = null)
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
        long? seed = null,
        long? rngCursor = null,
        IReadOnlyList<string>? untilEndOfTurnEffects = null,
        string? extraTurnPlayerId = null,
        IReadOnlyDictionary<string, int>? playerExperience = null,
        IReadOnlyDictionary<string, int>? playerCardsPlayedThisTurn = null,
        IReadOnlyList<TriggerQueueItemState>? triggerQueue = null)
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
        PlayerScores = NormalizePlayerScores(playerScores);
        PlayerExperience = NormalizePlayerExperience(playerExperience);
        PlayerCardsPlayedThisTurn = NormalizePlayerCardsPlayedThisTurn(playerCardsPlayedThisTurn);
        CardObjects = NormalizeCardObjects(cardObjects);
        PriorityPlayerId = NormalizeOptionalText(priorityPlayerId);
        PassedPriorityPlayerIds = NormalizeTextList(passedPriorityPlayerIds);
        StackItems = NormalizeStackItems(stackItems);
        TriggerQueue = NormalizeTriggerQueue(triggerQueue);
        FocusPlayerId = NormalizeOptionalText(focusPlayerId);
        PassedFocusPlayerIds = NormalizeTextList(passedFocusPlayerIds);
        WinnerPlayerId = NormalizeOptionalText(winnerPlayerId);
        DestroyedUnitOwnerIdsThisTurn = NormalizeTextList(destroyedUnitOwnerIdsThisTurn);
        Seed = seed ?? 0;
        RngCursor = Math.Max(0, rngCursor ?? 0);
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

    public IReadOnlyDictionary<string, RunePool> RunePools { get; init; }

    public IReadOnlyDictionary<string, PlayerZones> PlayerZones { get; init; }

    public IReadOnlyDictionary<string, int> PlayerScores { get; init; }

    public IReadOnlyDictionary<string, int> PlayerExperience { get; init; }

    public IReadOnlyDictionary<string, int> PlayerCardsPlayedThisTurn { get; init; }

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
                item.Destination))
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

    public static IReadOnlyDictionary<string, SnapshotDto> BuildSnapshots(MatchState state)
    {
        var readyPlayers = state.ReadyPlayerIds.ToHashSet(StringComparer.Ordinal);
        return state.Seats.Keys.ToDictionary(playerId => playerId, playerId =>
        {
            var players = state.Seats.ToDictionary(
                entry => entry.Key,
                entry => (object?)BuildPlayerSnapshotView(state, readyPlayers, playerId, entry.Key));

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
                    ["triggerQueue"] = state.TriggerQueue.Select(BuildTriggerQueueItemSnapshotView).ToArray(),
                    ["seed"] = state.Seed,
                    ["rngCursor"] = state.RngCursor,
                    ["winningScore"] = EffectiveWinningScore(state),
                    ["roomStatus"] = state.Status,
                    ["readyPlayerIds"] = state.ReadyPlayerIds
                },
                state.TimingState);
        });
    }

    private static int EffectiveWinningScore(MatchState state)
    {
        var modifier = state.PlayerZones.Values
            .SelectMany(zones => zones.Battlefields)
            .Count(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && (string.Equals(cardObject.CardNo, BattlefieldIncreaseWinningScoreCardNo, StringComparison.Ordinal)
                    || string.Equals(cardObject.CardNo, BattlefieldIncreaseWinningScoreAltCardNo, StringComparison.Ordinal)));
        return BaseWinningScore + modifier;
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
            ["runePool"] = state.RunePools.TryGetValue(subjectPlayerId, out var runePool)
                ? new Dictionary<string, object?>
                {
                    ["mana"] = runePool.Mana,
                    ["power"] = runePool.Power
                }
                : new Dictionary<string, object?>
                {
                    ["mana"] = 0,
                    ["power"] = 0
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
            ["battlefieldCount"] = battlefieldObjectIds.Length
        };
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
                objectId => (object?)BuildCardObjectSnapshotView(state.CardObjects[objectId], ownView),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, object?> BuildCardObjectSnapshotView(CardObjectState cardObject, bool ownView)
    {
        if (cardObject.IsFaceDown && !ownView)
        {
            return new Dictionary<string, object?>
            {
                ["objectId"] = cardObject.ObjectId,
                ["isFaceDown"] = true
            };
        }

        return new Dictionary<string, object?>
        {
            ["objectId"] = cardObject.ObjectId,
            ["damage"] = cardObject.Damage,
            ["power"] = cardObject.Power,
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
    }

    public static IReadOnlyDictionary<string, ActionPromptDto> BuildPrompts(MatchState state)
    {
        if (state.Status != MatchStatuses.InProgress)
        {
            var readyPlayers = state.ReadyPlayerIds.ToHashSet(StringComparer.Ordinal);
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId =>
            {
                var ready = readyPlayers.Contains(playerId);
                return ActionPromptBuilder.Build(
                    state,
                    playerId,
                    !ready && state.Status != MatchStatuses.Finished,
                    ready ? "已准备，等待对手" : "等待玩家准备",
                    ready ? ["WAIT"] : ["READY"]);
            });
        }

        if (state.StackItems.Count > 0 && !string.IsNullOrWhiteSpace(state.PriorityPlayerId))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过优先行动权"
                    : "等待对手优先行动",
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? ActionPromptBuilder.ActionsWithLegendActIfAvailable(state, playerId, "PASS_PRIORITY")
                    : ["WAIT"]));
        }

        if (string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过焦点"
                    : "等待对手焦点行动",
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? ActionPromptBuilder.ActionsWithLegendActIfAvailable(state, playerId, "PASS_FOCUS")
                    : ["WAIT"]));
        }

        return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
            state,
            playerId,
            playerId == state.ActivePlayerId,
            playerId == state.ActivePlayerId ? "当前玩家普通开环行动" : "等待对手行动",
            playerId == state.ActivePlayerId
                ? [
                    "PLAY_CARD",
                    "ACTIVATE_ABILITY",
                    "ASSEMBLE_EQUIPMENT",
                    "MOVE_UNIT",
                    "DECLARE_BATTLE",
                    "HIDE_CARD",
                    "TAP_RUNE",
                    "LEGEND_ACT",
                    "PASS",
                    "END_TURN"
                ]
                : ["WAIT"]));
    }
}

internal static class ActionPromptBuilder
{
    private const string BattlefieldEphemeralUnitsSteadfastCardNo = "UNL-208/219";
    private const string BattlefieldHeldMoveUnitToBaseCardNo = "UNL-207/219";
    private const string BattlefieldHoldCreateMinionCardNo = "OGN·275/298";
    private const string BattlefieldHoldDrawCardNo = "OGN·280/298";
    private const string BattlefieldHoldCallRuneCardNo = "OGN·288/298";
    private const string BattlefieldHoldGrantBoonCardNo = "OGN·283/298";
    private const string BattlefieldHeldReturnHeroCardNo = "OGN·281/298";
    private const string BattlefieldHeldPayPowerScoreCardNo = "SFD·214/221";
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
    private const string BattlefieldConquerDrawForOtherBattlefieldsCardNo = "SFD·217/221";
    private const string BattlefieldConquerPowerfulPayOneDrawCardNo = "SFD·218/221";
    private const string BattlefieldConquerPayOneCreateGoldCardNo = "SFD·220/221";
    private const string BattlefieldConquerReadyEquipmentCardNo = "SFD·221/221";
    private const string BattlefieldConquerDiscardDrawCardNo = "OGN·298/298";
    private const string BattlefieldConquerOverkillCreateWarhawkCardNo = "UNL-217/219";
    private const string BattlefieldIncreaseWinningScoreCardNo = "OGN·276/298";
    private const string BattlefieldIncreaseWinningScoreAltCardNo = "OGN·276a/298";
    private const string BattlefieldFirstTurnExtraRuneCardNo = "OGN·284/298";
    private const string BattlefieldFirstTurnScoreCardNo = "OGN·290/298";
    private const string BattlefieldTurnStartDamageAllUnitsCardNo = "UNL-212/219";
    private const string BattlefieldTurnStartDestroyUnitDrawCardNo = "UNL-209/219";
    private const string BattlefieldConquerRevealRecycleCardNo = "OGN·291/298";
    private const string BattlefieldMovedUnitPowerPlusOneCardNo = "OGN·277/298";
    private const string BattlefieldHeldSevenUnitsWinCardNo = "OGN·293/298";
    private const string BattlefieldHeldSevenUnitsWinAltCardNo = "OGN·293a/298";
    private const string BattlefieldPreventMoveToBaseCardNo = "OGN·295/298";
    private const string BattlefieldStaticRoamCardNo = "OGN·297/298";
    private const string BattlefieldPreventUnitPlayCardNo = "SFD·216/221";
    private const string BattlefieldEchoCostReductionCardNo = "SFD·211/221";
    private const string BattlefieldEquipmentCostReductionCardNo = "SFD·213/221";
    private const string BattlefieldFriendlySpellDrawCardNo = "OGN·292/298";
    private const string BattlefieldSpellPowerBonusCardNo = "UNL-205/219";
    private const string BattlefieldHighCostSpellInsightCardNo = "UNL-211/219";
    private const string BattlefieldPlayUnitPayOneBoonCardNo = "UNL-218/219";
    private const string BattlefieldTargetSpellSkillDamageBonusCardNo = "OGN·296/298";
    private const string BattlefieldHeldUnitCostIncreaseCardNo = "UNL-219/219";
    private const string BattlefieldHeldUnitCostIncreaseEffectPrefix = "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:";

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
        var modes = ModesFor(action);
        var optionalCosts = OptionalCostsFor(action);
        var hasRequiredChoices = !string.Equals(action, "LEGEND_ACT", StringComparison.Ordinal)
            || sources?.Count > 0;
        var enabled = promptActionable
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
            MetadataFor(action));
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
            "PLAY_CARD" => zones.Hand
                .Where(objectId => !state.CardObjects.TryGetValue(objectId, out var cardObject)
                    || string.IsNullOrWhiteSpace(cardObject.CardNo)
                    || CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo, out _))
                .Select(objectId => ObjectChoice(state, objectId, "implemented PLAY_CARD source"))
                .ToArray(),
            "MOVE_UNIT" => zones.Base
                .Concat(zones.Battlefields)
                .Where(objectId => IsControlledObjectWithTag(state, playerId, objectId, CardObjectTags.UnitCard))
                .Select(objectId => ObjectChoice(state, objectId, "controlled unit"))
                .ToArray(),
            "ASSEMBLE_EQUIPMENT" => zones.Base
                .Concat(zones.Battlefields)
                .Where(objectId => IsControlledObjectWithTag(state, playerId, objectId, CardObjectTags.EquipmentCard))
                .Select(objectId => ObjectChoice(state, objectId, "controlled equipment"))
                .ToArray(),
            "DECLARE_BATTLE" => zones.Battlefields
                .Where(objectId => IsControlledObjectWithTag(state, playerId, objectId, CardObjectTags.UnitCard))
                .Select(objectId => ObjectChoice(state, objectId, "controlled battlefield unit"))
                .ToArray(),
            "LEGEND_ACT" => zones.LegendZone
                .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                    && IsImplementedLegendActionCardNo(cardObject.CardNo)
                    && !cardObject.IsExhausted)
                .Select(objectId => ObjectChoice(state, objectId, "implemented legend action source"))
                .ToArray(),
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? TargetsFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "PLAY_CARD" => PublicBoardObjects(state)
                .Select(objectId => ObjectChoice(state, objectId, "server validates card target scope on submit"))
                .ToArray(),
            "ASSEMBLE_EQUIPMENT" => ControlledBoardObjects(state, playerId)
                .Where(objectId => IsControlledObjectWithTag(state, playerId, objectId, CardObjectTags.UnitCard))
                .Select(objectId => ObjectChoice(state, objectId, "controlled unit host"))
                .ToArray(),
            "DECLARE_BATTLE" => PublicBoardObjects(state)
                .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                    && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && !string.Equals(cardObject.ControllerId, playerId, StringComparison.Ordinal))
                .Select(objectId => ObjectChoice(state, objectId, "opposing battlefield defender candidate"))
                .ToArray(),
            "LEGEND_ACT" => ControlledLegendActionObjects(state, playerId)
                .Where(objectId =>
                    IsControlledObjectWithTag(state, playerId, objectId, CardObjectTags.UnitCard)
                    || IsControlledObjectWithTag(state, playerId, objectId, CardObjectTags.EquipmentCard))
                .Select(objectId => ObjectChoice(state, objectId, "controlled unit/equipment target"))
                .ToArray(),
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? DestinationsFor(
        MatchState state,
        string playerId,
        string action)
    {
        return action switch
        {
            "PLAY_CARD" => [
                new ActionPromptChoiceDto("BASE", "基地"),
                new ActionPromptChoiceDto($"BATTLEFIELD:{playerId}-MAIN", "己方主战场")
            ],
            "MOVE_UNIT" => [
                new ActionPromptChoiceDto("BASE", "基地"),
                new ActionPromptChoiceDto("BATTLEFIELD", "战场"),
                new ActionPromptChoiceDto($"BATTLEFIELD:{playerId}-MAIN", "己方主战场")
            ],
            "DECLARE_BATTLE" => state.Seats.Keys
                .Select(seatPlayerId => new ActionPromptChoiceDto($"BATTLEFIELD:{seatPlayerId}-MAIN", $"{seatPlayerId} 主战场"))
                .Concat(PublicBattlefieldCardObjects(state)
                    .Select(objectId => ObjectChoice(state, objectId, "battlefield card")))
                .ToArray(),
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? ModesFor(string action)
    {
        return action switch
        {
            "PLAY_CARD" => [
                new ActionPromptChoiceDto("AMBUSH", "伏击"),
                new ActionPromptChoiceDto("HASTE_READY", "急速活跃"),
                new ActionPromptChoiceDto("BATTLEFIELD_UNIT_POWER_MINUS_4", "战场单位战力 -4")
            ],
            "LEGEND_ACT" => [
                new ActionPromptChoiceDto("LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT", "支付 2 并横置：移动友方单位"),
                new ActionPromptChoiceDto("LEGEND_PAY_1_EXHAUST_GRANT_BOON", "支付 1 并横置：给予友方单位增益"),
                new ActionPromptChoiceDto("LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW", "花费 3 经验并横置：抽 1 张"),
                new ActionPromptChoiceDto("LEGEND_PAY_1_EXHAUST_CREATE_MINION", "支付 1 并横置：打出 1 战力随从"),
                new ActionPromptChoiceDto("LEGEND_EXHAUST_GRANT_ROAM", "横置：给予友方单位游走"),
                new ActionPromptChoiceDto("LEGEND_SPEND_1_EXPERIENCE_EXHAUST_GRANT_BOON", "花费 1 经验并横置：给予友方单位增益"),
                new ActionPromptChoiceDto("LEGEND_SPEND_2_EXPERIENCE_EXHAUST_MOVE_DORMANT_UNIT_TO_BASE", "花费 2 经验并横置：移动休眠友方单位回基地"),
                new ActionPromptChoiceDto("LEGEND_PAY_1_EXHAUST_RECALL_BATTLEFIELD_UNIT_CREATE_COIN", "支付 1 并横置：召回战场友方单位并打出金币"),
                new ActionPromptChoiceDto("LEGEND_PAY_1_EXHAUST_ATTACH_UNATTACHED_ARMAMENT", "支付 1 并横置：贴附未贴附武装"),
                new ActionPromptChoiceDto("LEGEND_EXHAUST_REATTACH_ATTACHED_ARMAMENT", "横置：重贴附已贴附武装"),
                new ActionPromptChoiceDto("LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA", "鼓舞并横置：获得 1 法力"),
                new ActionPromptChoiceDto("LEGEND_PAY_1_EXHAUST_RECALL_OWNED_TEEMO_UNIT", "支付 1 并横置：召回己方提莫单位"),
                new ActionPromptChoiceDto("LEGEND_PAY_1_EXHAUST_CREATE_SAND_SOLDIER_AFTER_ARMAMENT", "支付 1 并横置：打出黄沙士兵"),
                new ActionPromptChoiceDto("LEGEND_DYNAMIC_PAY_EXHAUST_CREATE_FAERIE", "动态支付并横置：打出精灵"),
                new ActionPromptChoiceDto("LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA", "法术对决横置：获得 1 法力"),
                new ActionPromptChoiceDto("LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL", "法术反应横置：获得 1 符能"),
                new ActionPromptChoiceDto("LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT", "装备反应横置：获得 1 符能"),
                new ActionPromptChoiceDto("LEGEND_REACTION_EXHAUST_DRAW_AFTER_TWO_ENEMY_TARGETS", "反应横置：抽 1 张"),
                new ActionPromptChoiceDto("LEGEND_REACTION_PAY_1_EXHAUST_READY_TARGETED_FRIENDLY_UNIT", "反应支付 1 并横置：重置被选为目标的友方单位")
            ],
            _ => null
        };
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? OptionalCostsFor(string action)
    {
        return action switch
        {
            "PLAY_CARD" => [
                new ActionPromptChoiceDto("ECHO", "回响"),
                new ActionPromptChoiceDto("STANDBY_REVEAL_0", "待命揭示"),
                new ActionPromptChoiceDto("ROAM", "游走"),
                new ActionPromptChoiceDto("SPEND_POWER:1", "支付 1 战力符能")
            ],
            "MOVE_UNIT" => [new ActionPromptChoiceDto("ROAM", "游走")],
            "ASSEMBLE_EQUIPMENT" => [new ActionPromptChoiceDto("ASSEMBLE_RED", "装配红色符能")],
            "DECLARE_BATTLE" => [new ActionPromptChoiceDto("COMBAT_ASSIGNMENT", "战斗分配")],
            "LEGEND_ACT" => [
                new ActionPromptChoiceDto("SPEND_MANA:1", "支付 1 法力"),
                new ActionPromptChoiceDto("SPEND_MANA:2", "支付 2 法力"),
                new ActionPromptChoiceDto("SPEND_MANA:3", "支付 3 法力"),
                new ActionPromptChoiceDto("SPEND_MANA:4", "支付 4 法力"),
                new ActionPromptChoiceDto("SPEND_EXPERIENCE:1", "支付 1 经验"),
                new ActionPromptChoiceDto("SPEND_EXPERIENCE:2", "支付 2 经验"),
                new ActionPromptChoiceDto("SPEND_EXPERIENCE:3", "支付 3 经验")
            ],
            _ => null
        };
    }

    private static IReadOnlyDictionary<string, object?>? MetadataFor(string action)
    {
        return action switch
        {
            "PLAY_CARD" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "implemented-card-behavior-only",
                ["targetPolicy"] = "server-validates-target-scope-on-submit"
            },
            "MOVE_UNIT" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "controlled-unit"
            },
            "ASSEMBLE_EQUIPMENT" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "controlled-equipment",
                ["targetPolicy"] = "controlled-unit-host"
            },
            "DECLARE_BATTLE" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "controlled-battlefield-unit",
                ["targetPolicy"] = "opposing-battlefield-unit",
                ["battlefieldTargetPolicy"] = "server-validates-battlefield-effect-targets-on-submit"
            },
            "LEGEND_ACT" => new Dictionary<string, object?>
            {
                ["sourcePolicy"] = "implemented-legend-action-only",
                ["abilityPolicy"] = "server-validates-legend-ability-on-submit"
            },
            _ => null
        };
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
                && IsBattlefieldCardObject(cardObject));
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

    private static bool IsControlledObjectWithTag(
        MatchState state,
        string playerId,
        string objectId,
        string tag)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && string.Equals(cardObject.ControllerId, playerId, StringComparison.Ordinal)
            && cardObject.Tags.Contains(tag, StringComparer.Ordinal);
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
            || string.Equals(cardObject.CardNo, BattlefieldConquerDrawForOtherBattlefieldsCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerPowerfulPayOneDrawCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerPayOneCreateGoldCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerReadyEquipmentCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerDiscardDrawCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldConquerOverkillCreateWarhawkCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldIncreaseWinningScoreCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldIncreaseWinningScoreAltCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldFirstTurnExtraRuneCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldFirstTurnScoreCardNo, StringComparison.Ordinal)
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
            || string.Equals(cardObject.CardNo, BattlefieldEquipmentCostReductionCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldFriendlySpellDrawCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldSpellPowerBonusCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHighCostSpellInsightCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldPlayUnitPayOneBoonCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldTargetSpellSkillDamageBonusCardNo, StringComparison.Ordinal)
            || string.Equals(cardObject.CardNo, BattlefieldHeldUnitCostIncreaseCardNo, StringComparison.Ordinal);
    }

    private static bool IsImplementedLegendActionCardNo(string? cardNo)
    {
        return cardNo is "FND-259/298"
            or "OGN·259/298"
            or "OGN·305*/298"
            or "OGN·305/298"
            or "OGN·257/298"
            or "OGN·304*/298"
            or "OGN·304/298"
            or "UNL-203/219"
            or "UNL-237*/219"
            or "UNL-237/219"
            or "FND-265/298"
            or "OGN·265/298"
            or "OGN·308*/298"
            or "OGN·308/298"
            or "OGN·267/298"
            or "OGN·309/298"
            or "OGN·309*/298"
            or "UNL-201/219"
            or "UNL-236/219"
            or "UNL-236*/219"
            or "UNL-185/219"
            or "UNL-228/219"
            or "UNL-228*/219"
            or "SFD·193/221"
            or "SFD·245/221"
            or "OGN·253/298"
            or "OGN·302/298"
            or "OGN·302*/298"
            or "OGN·263/298"
            or "OGN·263a/298"
            or "OGN·307/298"
            or "OGN·307*/298"
            or "SFD·197/221"
            or "SFD·247/221"
            or "UNL-197/219"
            or "UNL-234/219"
            or "UNL-234*/219"
            or "OGN·247/298"
            or "OGN·299/298"
            or "OGN·299*/298"
            or "SFD·189/221"
            or "SFD·244/221"
            or "SFD·199/221"
            or "SFD·248/221"
            or "SFD·195/221"
            or "SFD·195a/221·P"
            or "SFD·246/221"
            or "UNL-189/219"
            or "UNL-230*/219"
            or "UNL-230/219";
    }

    private static ActionPromptChoiceDto ObjectChoice(MatchState state, string objectId, string reason)
    {
        var label = state.CardObjects.TryGetValue(objectId, out var cardObject)
            ? string.IsNullOrWhiteSpace(cardObject.CardNo) ? objectId : $"{cardObject.CardNo} / {objectId}"
            : objectId;
        return new ActionPromptChoiceDto(objectId, label, reason);
    }

    private static string LabelFor(string action)
    {
        return action switch
        {
            "READY" => "准备",
            "WAIT" => "等待",
            "PLAY_CARD" => "打出卡牌",
            "ACTIVATE_ABILITY" => "激活能力",
            "ASSEMBLE_EQUIPMENT" => "装配装备",
            "MOVE_UNIT" => "移动单位",
            "DECLARE_BATTLE" => "声明战斗",
            "HIDE_CARD" => "隐藏卡牌",
            "TAP_RUNE" => "横置符文",
            "LEGEND_ACT" => "传奇行动",
            "PASS" => "让过",
            "PASS_PRIORITY" => "让过优先权",
            "PASS_FOCUS" => "让过焦点",
            "END_TURN" => "结束回合",
            _ => action
        };
    }

    private static string DisabledReasonFor(
        string action,
        string promptReason,
        bool hasRequiredChoices)
    {
        if (!hasRequiredChoices)
        {
            return $"{action} 当前没有服务端可执行候选";
        }

        return string.Equals(action, "WAIT", StringComparison.Ordinal)
            ? promptReason
            : $"当前 prompt 不允许执行 {action}";
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
        if (command is UnsupportedCommand unsupported)
        {
            return ValueTask.FromResult(ResolutionResult.Rejected(
                state,
                $"Unsupported command: {unsupported.RawCmdType}",
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

public sealed class InMemoryMatchSessionRegistry : IMatchSessionRegistry
{
    private readonly IRuleEngine ruleEngine;
    private readonly IMatchJournal journal;
    private readonly IMatchRecoveryStore recoveryStore;
    private readonly IMatchPlayerStore playerStore;
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
    {
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        this.recoveryStore = recoveryStore;
        this.playerStore = playerStore;
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
            return new MatchSession(roomId, ruleEngine, journal, playerStore);
        }

        if (!string.Equals(recovery.RoomId, roomId, StringComparison.Ordinal))
        {
            throw new MatchSessionException(
                ErrorCodes.RecoveryInconsistent,
                $"match recovery returned room {recovery.RoomId} for requested room {roomId}");
        }

        return MatchSession.Restore(recovery, ruleEngine, journal, playerStore);
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
    private const string BattlefieldConquerDrawForOtherBattlefieldsCardNo = "SFD·217/221";
    private const string BattlefieldConquerPowerfulPayOneDrawCardNo = "SFD·218/221";
    private const string BattlefieldConquerPayOneCreateGoldCardNo = "SFD·220/221";
    private const string BattlefieldConquerReadyEquipmentCardNo = "SFD·221/221";
    private const string BattlefieldConquerDiscardDrawCardNo = "OGN·298/298";
    private const string BattlefieldConquerOverkillCreateWarhawkCardNo = "UNL-217/219";
    private const string BattlefieldWinningScoreSeedCardNo = "OGN·276/298";
    private const string BattlefieldFirstTurnExtraRuneCardNo = "OGN·284/298";
    private const string BattlefieldFirstTurnScoreCardNo = "OGN·290/298";
    private const string BattlefieldTurnStartDamageAllUnitsCardNo = "UNL-212/219";
    private const string BattlefieldTurnStartDestroyUnitDrawCardNo = "UNL-209/219";
    private const string BattlefieldConquerRevealRecycleCardNo = "OGN·291/298";
    private const string BattlefieldMovedUnitPowerPlusOneCardNo = "OGN·277/298";
    private const string BattlefieldHeldSevenUnitsWinCardNo = "OGN·293/298";
    private const string BattlefieldPreventMoveToBaseCardNo = "OGN·295/298";
    private const string BattlefieldStaticRoamCardNo = "OGN·297/298";
    private const string BattlefieldPreventUnitPlayCardNo = "SFD·216/221";
    private const string BattlefieldEchoCostReductionCardNo = "SFD·211/221";
    private const string BattlefieldEquipmentCostReductionCardNo = "SFD·213/221";
    private const string BattlefieldFriendlySpellDrawCardNo = "OGN·292/298";
    private const string BattlefieldSpellPowerBonusCardNo = "UNL-205/219";
    private const string BattlefieldHighCostSpellInsightCardNo = "UNL-211/219";
    private const string BattlefieldPlayUnitPayOneBoonCardNo = "UNL-218/219";
    private const string BattlefieldTargetSpellSkillDamageBonusCardNo = "OGN·296/298";
    private const string BattlefieldHeldUnitCostIncreaseCardNo = "UNL-219/219";
    private const string BattlefieldHeldUnitCostIncreaseEffectPrefix = "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:";

    private readonly IRuleEngine ruleEngine;
    private readonly IMatchJournal journal;
    private readonly IMatchPlayerStore playerStore;
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
    {
        RoomId = roomId;
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        this.playerStore = playerStore;
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
        : this(
            initialState,
            0,
            initialState.Seats,
            [],
            ruleEngine,
            journal,
            playerStore)
    {
    }

    private MatchSession(
        MatchState restoredState,
        long restoredLastEventSequence,
        IReadOnlyDictionary<string, string> restoredSeats,
        IReadOnlyList<RecoveredCommand> restoredCommands,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        IMatchPlayerStore playerStore)
    {
        RoomId = restoredState.RoomId;
        this.ruleEngine = ruleEngine;
        this.journal = journal;
        this.playerStore = playerStore;
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
            playerStore);
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
                throw new MatchSessionException(ErrorCodes.RoomFull, "room already has two players");
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
                PlayerCardsPlayedThisTurn = PlayerCardsPlayedThisTurnForSeats(state.PlayerCardsPlayedThisTurn, seats.Keys)
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
                    "reconnect token required for existing player");
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
                throw new MatchSessionException(ErrorCodes.InvalidReconnectToken, "invalid reconnect token");
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
            throw new MatchSessionException(ErrorCodes.InvalidReconnectToken, "invalid reconnect token");
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
            throw new MatchSessionException(ErrorCodes.InvalidReconnectToken, "invalid reconnect token");
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
                        "clientIntentId already belongs to another command");
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
                        "clientIntentId already belongs to another command");
                }

                return cached.Result;
            }

            if (state.Status == MatchStatuses.Finished)
            {
                throw new MatchSessionException(ErrorCodes.MatchFinished, "match already finished");
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
                if (nextStatus == MatchStatuses.InProgress)
                {
                    events.Add(new GameEvent(
                        "MATCH_STARTED",
                        "双方已准备，比赛开始",
                        new Dictionary<string, object?>
                        {
                            ["activePlayerId"] = state.ActivePlayerId
                        }));
                }
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
                        "clientIntentId already belongs to another command",
                        ErrorCodes.ClientIntentConflict);
                }

                return cached.Result;
            }

            if (state.Status != MatchStatuses.InProgress)
            {
                throw new MatchSessionException(ErrorCodes.MatchNotStarted, "match has not started");
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

    private void RequirePlayer(string playerId)
    {
        lock (seatGate)
        {
            if (!seats.ContainsKey(playerId))
            {
                throw new MatchSessionException(ErrorCodes.PlayerNotInRoom, "player is not in room");
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
                "clientIntentId is required");
        }

        return clientIntentId.Trim();
    }

    private static string NormalizeScenarioId(string scenarioId)
    {
        return string.IsNullOrWhiteSpace(scenarioId)
            ? throw new MatchSessionException(ErrorCodes.UnsupportedCommand, "scenarioId is required")
            : scenarioId.Trim();
    }

    private static MatchState BuildDevScenarioState(MatchState current, string scenarioId)
    {
        var p1 = PlayerBySeat(current, "P1");
        var p2 = PlayerBySeat(current, "P2");
        if (p1 is null || p2 is null)
        {
            throw new MatchSessionException(ErrorCodes.PlayerNotInRoom, "dev scenarios require two joined players");
        }

        var seed = new DevScenarioSeed(p1, p2);
        return scenarioId switch
        {
            "basic-play" => BuildBasicPlayScenario(current, seed),
            "movement" => BuildMovementScenario(current, seed),
            "spell-duel" => BuildSpellDuelScenario(current, seed),
            "echo-stack" => BuildEchoStackScenario(current, seed),
            "standby-reaction" => BuildStandbyReactionScenario(current, seed),
            "ambush-reaction" => BuildAmbushReactionScenario(current, seed),
            "equipment" => BuildEquipmentScenario(current, seed),
            "status-showcase" => BuildStatusShowcaseScenario(current, seed),
            "resource-experience" => BuildResourceExperienceScenario(current, seed),
            "legend-act" => BuildLegendActScenario(current, seed),
            "legend-active-actions" => BuildLegendActiveActionsScenario(current, seed),
            "lifecycle-ephemeral" => BuildLifecycleEphemeralScenario(current, seed),
            "lifecycle-last-breath" => BuildLifecycleLastBreathScenario(current, seed),
            "control" => BuildControlScenario(current, seed),
            "battle-declare" => BuildBattleDeclareScenario(current, seed),
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
            "battlefield-turn-start-damage" => BuildBattlefieldTurnStartDamageScenario(current, seed),
            "battlefield-turn-start-destroy-draw" => BuildBattlefieldTurnStartDestroyDrawScenario(current, seed),
            "battlefield-held-score" => BuildBattlefieldHeldScoreScenario(current, seed),
            "battlefield-held-seven-units-win" => BuildBattlefieldHeldSevenUnitsWinScenario(current, seed),
            "battlefield-conquer-reveal-recycle" => BuildBattlefieldConquerRevealRecycleScenario(current, seed),
            "battlefield-move-power" => BuildBattlefieldMovePowerScenario(current, seed),
            "battlefield-static-prevent-play-units" => BuildBattlefieldStaticPreventPlayUnitsScenario(current, seed),
            "battlefield-static-echo-cost-reduction" => BuildBattlefieldStaticEchoCostReductionScenario(current, seed),
            "battlefield-static-equipment-cost-reduction" => BuildBattlefieldStaticEquipmentCostReductionScenario(current, seed),
            "battlefield-friendly-spell-draw" => BuildBattlefieldFriendlySpellDrawScenario(current, seed),
            "battlefield-spell-power-bonus" => BuildBattlefieldSpellPowerBonusScenario(current, seed),
            "battlefield-high-cost-spell-insight" => BuildBattlefieldHighCostSpellInsightScenario(current, seed),
            "battlefield-play-unit-boon" => BuildBattlefieldPlayUnitBoonScenario(current, seed),
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
            "battlefield-conquer-draw-other" => BuildBattlefieldConquerDrawOtherScenario(current, seed),
            "battlefield-conquer-powerful-draw" => BuildBattlefieldConquerPowerfulDrawScenario(current, seed),
            "battlefield-conquer-gold" => BuildBattlefieldConquerGoldScenario(current, seed),
            "battlefield-conquer-ready-equipment" => BuildBattlefieldConquerReadyEquipmentScenario(current, seed),
            "battle-score" => BuildBattleScoreScenario(current, seed),
            "specified-hand" => BuildSpecifiedHandScenario(current, seed),
            _ => throw new MatchSessionException(
                ErrorCodes.UnsupportedCommand,
                $"Unsupported dev scenario: {scenarioId}")
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
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal));
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
                ["P1-BATTLEFIELD-UNIT-001"] = new(power: 3, damage: 1, isExhausted: true, tags: ["CARD_TYPE:UNIT"]),
                ["P2-BATTLEFIELD-UNIT-001"] = new(power: 3, isExhausted: true, tags: ["CARD_TYPE:UNIT"])
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
                ["P2-UNIT-001"] = new(power: 2, tags: ["CARD_TYPE:UNIT"])
            });
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
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal));
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
                [seed.P1] = new(2, 1),
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
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
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
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal));

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
                ["P1-BATTLE-ATTACKER-001"] = new(power: 3, tags: ["CARD_TYPE:UNIT"]),
                ["P2-BATTLE-DEFENDER-001"] = new(power: 2, tags: ["CARD_TYPE:UNIT"])
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
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-CANDLE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-CANDLE-DEFENDER",
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
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-CONQUER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-CONQUER-DEFENDER",
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
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-STATIC-DEFENDER"] = new(
                    "P2-BATTLEFIELD-STATIC-DEFENDER",
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
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
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
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DRAW-OTHER-DEFENDER",
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
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-POWERFUL-DEFENDER",
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
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-GOLD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-GOLD-DEFENDER",
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
            throw new MatchSessionException(ErrorCodes.PlayerIdRequired, "playerId is required");
        }

        return playerId.Trim();
    }

    private sealed record DevScenarioSeed(string P1, string P2);
}

public sealed class MatchSessionException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
