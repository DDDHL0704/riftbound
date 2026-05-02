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
        string? attachedToObjectId = null)
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
        IReadOnlyList<string>? optionalCosts = null)
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
        IReadOnlyDictionary<string, int>? playerCardsPlayedThisTurn = null)
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
            state.AttachedToObjectId);
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
                item.OptionalCosts))
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
                state.StackItems.Select(item => (object?)new Dictionary<string, object?>
                {
                    ["stackItemId"] = item.StackItemId,
                    ["controllerId"] = item.ControllerId,
                    ["sourceObjectId"] = item.SourceObjectId,
                    ["effectKind"] = item.EffectKind,
                    ["cardNo"] = item.CardNo,
                    ["targetObjectIds"] = item.TargetObjectIds,
                    ["damageAmount"] = item.DamageAmount
                }).ToArray(),
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
                    ["seed"] = state.Seed,
                    ["rngCursor"] = state.RngCursor,
                    ["roomStatus"] = state.Status,
                    ["readyPlayerIds"] = state.ReadyPlayerIds
                },
                state.TimingState);
        });
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
            ["objects"] = BuildObjectSnapshotView(state, VisibleObjectIds(zones, ownView))
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

    private static Dictionary<string, object?> BuildObjectSnapshotView(MatchState state, IReadOnlyList<string> visibleObjectIds)
    {
        return visibleObjectIds
            .Where(objectId => state.CardObjects.ContainsKey(objectId))
            .ToDictionary(
                objectId => objectId,
                objectId => (object?)BuildCardObjectSnapshotView(state.CardObjects[objectId]),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, object?> BuildCardObjectSnapshotView(CardObjectState cardObject)
    {
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
            ["attachedToObjectId"] = cardObject.AttachedToObjectId
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
                return new ActionPromptDto(
                    playerId,
                    !ready && state.Status != MatchStatuses.Finished,
                    ready ? "已准备，等待对手" : "等待玩家准备",
                    ready ? ["WAIT"] : ["READY"]);
            });
        }

        if (state.StackItems.Count > 0 && !string.IsNullOrWhiteSpace(state.PriorityPlayerId))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => new ActionPromptDto(
                playerId,
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过优先行动权"
                    : "等待对手优先行动",
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? ["PASS_PRIORITY"]
                    : ["WAIT"]));
        }

        if (string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => new ActionPromptDto(
                playerId,
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过焦点"
                    : "等待对手焦点行动",
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? ["PASS_FOCUS"]
                    : ["WAIT"]));
        }

        return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => new ActionPromptDto(
            playerId,
            playerId == state.ActivePlayerId,
            playerId == state.ActivePlayerId ? "当前玩家普通开环行动" : "等待对手行动",
            playerId == state.ActivePlayerId
                ? [
                    "PLAY_CARD",
                    "ACTIVATE_ABILITY",
                    "ASSEMBLE_EQUIPMENT",
                    "MOVE_UNIT",
                    "HIDE_CARD",
                    "TAP_RUNE",
                    "LEGEND_ACT",
                    "PASS",
                    "END_TURN"
                ]
                : ["WAIT"]));
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
            "equipment" => BuildEquipmentScenario(current, seed),
            "control" => BuildControlScenario(current, seed),
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

    private static MatchState BuildEquipmentScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302787,
            787,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-LONG-SWORD-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-EQUIPMENT-LONG-SWORD"],
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
