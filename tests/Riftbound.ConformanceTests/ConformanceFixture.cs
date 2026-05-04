using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;

namespace Riftbound.ConformanceTests;

public sealed record ConformanceFixture(
    int SchemaVersion,
    string FixtureId,
    string Description,
    string Source,
    string RoomId,
    IReadOnlyList<string> Players,
    IReadOnlyList<ConformanceCommand> Commands,
    ConformanceExpected Expected,
    ConformanceInitialState? InitialState = null,
    long? Seed = null,
    IReadOnlyList<RuleEvidence>? RulesEvidence = null,
    string? AuditStatus = null,
    string? RulesVersion = null,
    string? FaqVersion = null,
    string? CatalogVersion = null,
    string? JavaCommit = null,
    JsonElement? LegacyOracle = null,
    JsonElement? Oracle = null)
{
    public bool RequiresRuleAudit =>
        string.Equals(AuditStatus, "NEEDS_RULE_AUDIT", StringComparison.OrdinalIgnoreCase)
        || RulesEvidence is null
        || RulesEvidence.Count == 0;

    public bool HasLegacyOracle =>
        LegacyOracle is { ValueKind: JsonValueKind.Object };

    public bool HasCompatibilityOracle =>
        Oracle is { ValueKind: JsonValueKind.Object };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ConformanceFixture> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var fixture = await JsonSerializer.DeserializeAsync<ConformanceFixture>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return fixture ?? throw new InvalidOperationException($"Fixture {path} could not be deserialized.");
    }
}

public sealed record ConformanceCommand(
    string PlayerId,
    string ClientIntentId,
    JsonElement Cmd);

public sealed record RuleEvidence(
    string Source,
    string Locator,
    string Note);

public sealed record ConformanceInitialState(
    long? Seed = null,
    int? TurnNumber = null,
    string? ActivePlayerId = null,
    string? TurnPlayerId = null,
    string? Phase = null,
    string? TimingState = null,
    IReadOnlyDictionary<string, ConformancePlayerInitialState>? Players = null,
    IReadOnlyDictionary<string, RunePool>? RunePools = null,
    IReadOnlyDictionary<string, int>? Scores = null,
    IReadOnlyDictionary<string, int>? Experience = null,
    IReadOnlyDictionary<string, ConformanceCardObjectState>? CardObjects = null,
    string? PriorityPlayerId = null,
    IReadOnlyList<string>? PassedPriorityPlayerIds = null,
    IReadOnlyList<ConformanceStackItemState>? StackItems = null,
    string? FocusPlayerId = null,
    IReadOnlyList<string>? PassedFocusPlayerIds = null,
    string? WinnerPlayerId = null,
    IReadOnlyList<string>? UntilEndOfTurnEffects = null);

public sealed record ConformancePlayerInitialState(
    IReadOnlyList<string>? MainDeck = null,
    IReadOnlyList<string>? RuneDeck = null,
    IReadOnlyList<string>? Hand = null,
    IReadOnlyList<string>? Base = null,
    IReadOnlyList<string>? Battlefields = null,
    IReadOnlyList<string>? Graveyard = null,
    IReadOnlyList<string>? Banished = null,
    IReadOnlyList<string>? LegendZone = null,
    IReadOnlyList<string>? ChampionZone = null);

public sealed record ConformanceCardObjectState
{
    public int? Damage { get; init; }

    public IReadOnlyList<string>? UntilEndOfTurnEffects { get; init; }

    public bool? IsFaceDown { get; init; }

    public bool? IsAttacking { get; init; }

    public bool? IsDefending { get; init; }

    public bool? IsExhausted { get; init; }

    public int? Power { get; init; }

    public int? UntilEndOfTurnPowerModifier { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public int? ManaCost { get; init; }

    public string? AttachedToObjectId { get; init; }

    public string? CardNo { get; init; }
}

public sealed record ConformanceStackItemState(
    string? StackItemId = null,
    string? ControllerId = null,
    string? SourceObjectId = null,
    string? EffectKind = null,
    string? CardNo = null,
    IReadOnlyList<string>? TargetObjectIds = null,
    int? DamageAmount = null,
    string? Destination = null);

public sealed record ConformanceExpected(
    long FinalTick,
    IReadOnlyList<string> EventKinds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PromptActions,
    ConformanceExpectedState? FinalState = null,
    IReadOnlyList<ConformanceExpectedEvent>? Events = null,
    IReadOnlyDictionary<string, JsonElement>? Snapshots = null,
    IReadOnlyDictionary<string, ConformanceExpectedPrompt>? Prompts = null);

public sealed record ConformanceExpectedState(
    int? TurnNumber = null,
    string? ActivePlayerId = null,
    string? TurnPlayerId = null,
    string? Phase = null,
    string? TimingState = null,
    IReadOnlyDictionary<string, RunePool>? RunePools = null,
    IReadOnlyDictionary<string, ConformancePlayerInitialState>? Players = null,
    IReadOnlyDictionary<string, int>? Scores = null,
    IReadOnlyDictionary<string, int>? Experience = null,
    IReadOnlyDictionary<string, ConformanceCardObjectState>? CardObjects = null,
    string? PriorityPlayerId = null,
    IReadOnlyList<string>? PassedPriorityPlayerIds = null,
    IReadOnlyList<ConformanceStackItemState>? StackItems = null,
    string? FocusPlayerId = null,
    IReadOnlyList<string>? PassedFocusPlayerIds = null,
    string? WinnerPlayerId = null,
    IReadOnlyList<string>? UntilEndOfTurnEffects = null);

public sealed record ConformanceExpectedEvent(
    string Kind,
    long? Tick = null,
    long? Sequence = null,
    IReadOnlyDictionary<string, JsonElement>? Payload = null);

public sealed record ConformanceExpectedPrompt(
    bool? Actionable = null,
    IReadOnlyList<string>? Actions = null);

public sealed record ConformanceRunResult(
    long FinalTick,
    IReadOnlyList<string> EventKinds,
    IReadOnlyList<ConformanceActualEvent> Events,
    IReadOnlyDictionary<string, ActionPromptDto> Prompts,
    MatchState FinalState);

public sealed record ConformanceActualEvent(
    string Kind,
    long Tick,
    long Sequence,
    IReadOnlyDictionary<string, object?> Payload);

public static class ConformanceFixtureRunner
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ConformanceRunResult> RunAsync(
        ConformanceFixture fixture,
        IRuleEngine ruleEngine,
        CancellationToken cancellationToken)
    {
        var journal = new RecordingMatchJournal();
        var session = await CreateSessionAsync(fixture, ruleEngine, journal, cancellationToken)
            .ConfigureAwait(false);

        ResolutionResult? last = null;
        foreach (var command in fixture.Commands)
        {
            var mapped = GameCommandJsonMapper.Map(command.Cmd);
            last = await session.SubmitAsync(
                    command.PlayerId,
                    command.ClientIntentId,
                    mapped,
                    command.Cmd.Clone(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (last is null)
        {
            throw new InvalidOperationException($"Fixture {fixture.FixtureId} does not contain commands.");
        }

        var events = journal.Entries
            .Where(entry => !string.Equals(entry.CommandType, "READY", StringComparison.Ordinal))
            .SelectMany(entry => entry.Events.Select((gameEvent, index) => new ConformanceActualEvent(
                gameEvent.Kind,
                entry.CompletedTick,
                entry.StartedEventSequence + index + 1,
                gameEvent.Payload)))
            .ToArray();
        var eventKinds = events.Select(gameEvent => gameEvent.Kind).ToArray();

        return new ConformanceRunResult(last.State.Tick, eventKinds, events, last.Prompts, last.State);
    }

    public static IReadOnlyList<string> CompareExpected(
        ConformanceFixture fixture,
        ConformanceRunResult result)
    {
        var mismatches = new List<string>();
        AddMismatch(mismatches, "finalTick", fixture.Expected.FinalTick, result.FinalTick);
        CompareSequence(mismatches, "eventKinds", fixture.Expected.EventKinds, result.EventKinds);
        CompareExpectedEvents(mismatches, fixture.Expected.Events, result.Events);
        ComparePromptActions(mismatches, fixture.Expected.PromptActions, result.Prompts);
        CompareExpectedPrompts(mismatches, fixture.Expected.Prompts, result.Prompts);
        CompareExpectedState(mismatches, fixture.Expected.FinalState, result.FinalState);
        return mismatches;
    }

    private static async ValueTask<MatchSession> CreateSessionAsync(
        ConformanceFixture fixture,
        IRuleEngine ruleEngine,
        IMatchJournal journal,
        CancellationToken cancellationToken)
    {
        if (fixture.InitialState is not null)
        {
            return new MatchSession(BuildInitialState(fixture), ruleEngine, journal);
        }

        var session = new MatchSession(fixture.RoomId, ruleEngine, journal);
        foreach (var playerId in fixture.Players)
        {
            session.EnsurePlayer(playerId);
        }

        return await ReadySessionAsync(session, fixture.Players, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<MatchSession> ReadySessionAsync(
        MatchSession session,
        IReadOnlyList<string> playerIds,
        CancellationToken cancellationToken)
    {
        foreach (var playerId in playerIds)
        {
            await session.ReadyAsync(
                    playerId,
                    $"fixture-ready-{playerId}",
                    JsonSerializer.SerializeToElement(new { cmdType = "READY" }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return session;
    }

    private static MatchState BuildInitialState(ConformanceFixture fixture)
    {
        var initial = fixture.InitialState
            ?? throw new InvalidOperationException($"Fixture {fixture.FixtureId} does not define initialState.");
        var seats = BuildSeats(fixture.Players);
        var activePlayerId = NormalizePlayerId(
            initial.ActivePlayerId,
            fixture.Players.FirstOrDefault() ?? "P1");
        var turnPlayerId = NormalizePlayerId(initial.TurnPlayerId, activePlayerId);
        var turnNumber = initial.TurnNumber ?? 1;
        var phase = NormalizeText(initial.Phase, MatchPhases.Main);
        var timingState = NormalizeText(initial.TimingState, TimingStates.NeutralOpen);

        return new MatchState(
            fixture.RoomId,
            0,
            turnNumber,
            activePlayerId,
            seats,
            MatchStatuses.InProgress,
            fixture.Players,
            turnPlayerId,
            phase,
            timingState,
            BuildRunePools(initial, fixture.Players),
            BuildPlayerZones(initial, fixture.Players),
            BuildPlayerScores(initial, fixture.Players),
            BuildCardObjects(initial),
            initial.PriorityPlayerId,
            initial.PassedPriorityPlayerIds,
            BuildStackItems(initial),
            initial.FocusPlayerId,
            initial.PassedFocusPlayerIds,
            initial.WinnerPlayerId,
            seed: initial.Seed,
            untilEndOfTurnEffects: initial.UntilEndOfTurnEffects,
            playerExperience: BuildPlayerExperience(initial, fixture.Players));
    }

    private static IReadOnlyDictionary<string, string> BuildSeats(IReadOnlyList<string> playerIds)
    {
        return playerIds
            .Select((playerId, index) => new
            {
                PlayerId = NormalizePlayerId(playerId, $"P{index + 1}"),
                Seat = $"P{index + 1}"
            })
            .ToDictionary(entry => entry.PlayerId, entry => entry.Seat, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, RunePool> BuildRunePools(
        ConformanceInitialState initial,
        IReadOnlyList<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => NormalizePlayerId(playerId, playerId),
            playerId => initial.RunePools is not null
                && initial.RunePools.TryGetValue(playerId, out var runePool)
                    ? runePool
                    : RunePool.Empty,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, PlayerZones> BuildPlayerZones(
        ConformanceInitialState initial,
        IReadOnlyList<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => NormalizePlayerId(playerId, playerId),
            playerId => initial.Players is not null
                && initial.Players.TryGetValue(playerId, out var zones)
                    ? ToPlayerZones(zones)
                    : PlayerZones.Empty,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> BuildPlayerScores(
        ConformanceInitialState initial,
        IReadOnlyList<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => NormalizePlayerId(playerId, playerId),
            playerId => initial.Scores is not null && initial.Scores.TryGetValue(playerId, out var score)
                ? score
                : 0,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> BuildPlayerExperience(
        ConformanceInitialState initial,
        IReadOnlyList<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => NormalizePlayerId(playerId, playerId),
            playerId => initial.Experience is not null && initial.Experience.TryGetValue(playerId, out var experience)
                ? experience
                : 0,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, CardObjectState> BuildCardObjects(ConformanceInitialState initial)
    {
        return (initial.CardObjects ?? new Dictionary<string, ConformanceCardObjectState>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => new CardObjectState(
                    entry.Key.Trim(),
                    entry.Value.Damage ?? 0,
                    entry.Value.UntilEndOfTurnEffects,
                    entry.Value.IsFaceDown ?? false,
                    entry.Value.IsAttacking ?? false,
                    entry.Value.IsDefending ?? false,
                    entry.Value.Power ?? 0,
                    entry.Value.UntilEndOfTurnPowerModifier ?? 0,
                    entry.Value.IsExhausted ?? false,
                    entry.Value.Tags,
                    entry.Value.ManaCost ?? 0,
                    entry.Value.AttachedToObjectId,
                    entry.Value.CardNo),
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<StackItemState> BuildStackItems(ConformanceInitialState initial)
    {
        return (initial.StackItems ?? [])
            .Select(item => new StackItemState(
                item.StackItemId,
                item.ControllerId,
                item.SourceObjectId,
                item.EffectKind,
                item.CardNo,
                item.TargetObjectIds,
                damageAmount: item.DamageAmount ?? 0,
                destination: item.Destination))
            .ToArray();
    }

    private static PlayerZones ToPlayerZones(ConformancePlayerInitialState zones)
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

    private static string NormalizePlayerId(string? playerId, string fallback)
    {
        return string.IsNullOrWhiteSpace(playerId) ? fallback : playerId.Trim();
    }

    private static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static void CompareExpectedState(
        List<string> mismatches,
        ConformanceExpectedState? expected,
        MatchState actual)
    {
        if (expected is null)
        {
            return;
        }

        AddMismatch(mismatches, "finalState.turnNumber", expected.TurnNumber, actual.TurnNumber);
        AddMismatch(mismatches, "finalState.activePlayerId", expected.ActivePlayerId, actual.ActivePlayerId);
        AddMismatch(mismatches, "finalState.turnPlayerId", expected.TurnPlayerId, actual.TurnPlayerId);
        AddMismatch(mismatches, "finalState.phase", expected.Phase, actual.Phase);
        AddMismatch(mismatches, "finalState.timingState", expected.TimingState, actual.TimingState);
        AddMismatch(mismatches, "finalState.priorityPlayerId", expected.PriorityPlayerId, actual.PriorityPlayerId);
        CompareSequence(mismatches, "finalState.passedPriorityPlayerIds", expected.PassedPriorityPlayerIds, actual.PassedPriorityPlayerIds);
        AddMismatch(mismatches, "finalState.focusPlayerId", expected.FocusPlayerId, actual.FocusPlayerId);
        CompareSequence(mismatches, "finalState.passedFocusPlayerIds", expected.PassedFocusPlayerIds, actual.PassedFocusPlayerIds);
        AddMismatch(mismatches, "finalState.winnerPlayerId", expected.WinnerPlayerId, actual.WinnerPlayerId);
        CompareRunePools(mismatches, expected.RunePools, actual.RunePools);
        ComparePlayerScores(mismatches, expected.Scores, actual.PlayerScores);
        ComparePlayerExperience(mismatches, expected.Experience, actual.PlayerExperience);
        ComparePlayerZones(mismatches, expected.Players, actual.PlayerZones);
        CompareCardObjects(mismatches, expected.CardObjects, actual.CardObjects);
        CompareStackItems(mismatches, expected.StackItems, actual.StackItems);
        CompareSequence(mismatches, "finalState.untilEndOfTurnEffects", expected.UntilEndOfTurnEffects, actual.UntilEndOfTurnEffects);
    }

    private static void ComparePromptActions(
        List<string> mismatches,
        IReadOnlyDictionary<string, IReadOnlyList<string>> expected,
        IReadOnlyDictionary<string, ActionPromptDto> actual)
    {
        foreach (var (playerId, actions) in expected)
        {
            if (!actual.TryGetValue(playerId, out var prompt))
            {
                mismatches.Add($"prompts.{playerId}: missing prompt");
                continue;
            }

            CompareSequence(mismatches, $"promptActions.{playerId}", actions, prompt.Actions);
        }
    }

    private static void CompareExpectedPrompts(
        List<string> mismatches,
        IReadOnlyDictionary<string, ConformanceExpectedPrompt>? expected,
        IReadOnlyDictionary<string, ActionPromptDto> actual)
    {
        if (expected is null)
        {
            return;
        }

        foreach (var (playerId, expectedPrompt) in expected)
        {
            if (!actual.TryGetValue(playerId, out var actualPrompt))
            {
                mismatches.Add($"prompts.{playerId}: missing prompt");
                continue;
            }

            AddMismatch(mismatches, $"prompts.{playerId}.actionable", expectedPrompt.Actionable, actualPrompt.Actionable);
            CompareSequence(mismatches, $"prompts.{playerId}.actions", expectedPrompt.Actions, actualPrompt.Actions);
        }
    }

    private static void CompareExpectedEvents(
        List<string> mismatches,
        IReadOnlyList<ConformanceExpectedEvent>? expected,
        IReadOnlyList<ConformanceActualEvent> actual)
    {
        if (expected is null)
        {
            return;
        }

        AddMismatch(mismatches, "events.count", expected.Count, actual.Count);
        for (var i = 0; i < Math.Min(expected.Count, actual.Count); i++)
        {
            var expectedEvent = expected[i];
            var actualEvent = actual[i];
            AddMismatch(mismatches, $"events[{i}].kind", expectedEvent.Kind, actualEvent.Kind);
            AddMismatch(mismatches, $"events[{i}].tick", expectedEvent.Tick, actualEvent.Tick);
            AddMismatch(mismatches, $"events[{i}].sequence", expectedEvent.Sequence, actualEvent.Sequence);
            CompareExpectedEventPayload(mismatches, $"events[{i}].payload", expectedEvent.Payload, actualEvent.Payload);
        }
    }

    private static void CompareExpectedEventPayload(
        List<string> mismatches,
        string path,
        IReadOnlyDictionary<string, JsonElement>? expected,
        IReadOnlyDictionary<string, object?> actual)
    {
        if (expected is null)
        {
            return;
        }

        foreach (var (key, expectedValue) in expected)
        {
            if (!actual.TryGetValue(key, out var actualValue))
            {
                mismatches.Add($"{path}.{key}: missing payload value");
                continue;
            }

            var actualElement = JsonSerializer.SerializeToElement(actualValue, PayloadJsonOptions);
            if (!JsonElementEquals(expectedValue, actualElement))
            {
                mismatches.Add(
                    $"{path}.{key}: expected {expectedValue.GetRawText()}, actual {actualElement.GetRawText()}");
            }
        }
    }

    private static bool JsonElementEquals(JsonElement expected, JsonElement actual)
    {
        return string.Equals(
            CanonicalJson.Serialize(expected),
            CanonicalJson.Serialize(actual),
            StringComparison.Ordinal);
    }

    private static void CompareRunePools(
        List<string> mismatches,
        IReadOnlyDictionary<string, RunePool>? expected,
        IReadOnlyDictionary<string, RunePool> actual)
    {
        if (expected is null)
        {
            return;
        }

        foreach (var (playerId, expectedPool) in expected)
        {
            if (!actual.TryGetValue(playerId, out var actualPool))
            {
                mismatches.Add($"finalState.runePools.{playerId}: missing pool");
                continue;
            }

            AddMismatch(mismatches, $"finalState.runePools.{playerId}", expectedPool, actualPool);
        }
    }

    private static void ComparePlayerScores(
        List<string> mismatches,
        IReadOnlyDictionary<string, int>? expected,
        IReadOnlyDictionary<string, int> actual)
    {
        if (expected is null)
        {
            return;
        }

        foreach (var (playerId, expectedScore) in expected)
        {
            if (!actual.TryGetValue(playerId, out var actualScore))
            {
                mismatches.Add($"finalState.scores.{playerId}: missing score");
                continue;
            }

            AddMismatch(mismatches, $"finalState.scores.{playerId}", expectedScore, actualScore);
        }
    }

    private static void ComparePlayerExperience(
        List<string> mismatches,
        IReadOnlyDictionary<string, int>? expected,
        IReadOnlyDictionary<string, int> actual)
    {
        if (expected is null)
        {
            return;
        }

        foreach (var (playerId, expectedExperience) in expected)
        {
            if (!actual.TryGetValue(playerId, out var actualExperience))
            {
                mismatches.Add($"finalState.experience.{playerId}: missing experience");
                continue;
            }

            AddMismatch(mismatches, $"finalState.experience.{playerId}", expectedExperience, actualExperience);
        }
    }

    private static void ComparePlayerZones(
        List<string> mismatches,
        IReadOnlyDictionary<string, ConformancePlayerInitialState>? expected,
        IReadOnlyDictionary<string, PlayerZones> actual)
    {
        if (expected is null)
        {
            return;
        }

        foreach (var (playerId, expectedZones) in expected)
        {
            if (!actual.TryGetValue(playerId, out var actualZones))
            {
                mismatches.Add($"finalState.players.{playerId}: missing zones");
                continue;
            }

            CompareSequence(mismatches, $"finalState.players.{playerId}.mainDeck", expectedZones.MainDeck, actualZones.MainDeck);
            CompareSequence(mismatches, $"finalState.players.{playerId}.runeDeck", expectedZones.RuneDeck, actualZones.RuneDeck);
            CompareSequence(mismatches, $"finalState.players.{playerId}.hand", expectedZones.Hand, actualZones.Hand);
            CompareSequence(mismatches, $"finalState.players.{playerId}.base", expectedZones.Base, actualZones.Base);
            CompareSequence(mismatches, $"finalState.players.{playerId}.battlefields", expectedZones.Battlefields, actualZones.Battlefields);
            CompareSequence(mismatches, $"finalState.players.{playerId}.graveyard", expectedZones.Graveyard, actualZones.Graveyard);
            CompareSequence(mismatches, $"finalState.players.{playerId}.banished", expectedZones.Banished, actualZones.Banished);
            CompareSequence(mismatches, $"finalState.players.{playerId}.legendZone", expectedZones.LegendZone, actualZones.LegendZone);
            CompareSequence(mismatches, $"finalState.players.{playerId}.championZone", expectedZones.ChampionZone, actualZones.ChampionZone);
        }
    }

    private static void CompareCardObjects(
        List<string> mismatches,
        IReadOnlyDictionary<string, ConformanceCardObjectState>? expected,
        IReadOnlyDictionary<string, CardObjectState> actual)
    {
        if (expected is null)
        {
            return;
        }

        foreach (var (objectId, expectedObject) in expected)
        {
            if (!actual.TryGetValue(objectId, out var actualObject))
            {
                mismatches.Add($"finalState.cardObjects.{objectId}: missing card object");
                continue;
            }

            AddMismatch(mismatches, $"finalState.cardObjects.{objectId}.damage", expectedObject.Damage, actualObject.Damage);
            AddMismatch(mismatches, $"finalState.cardObjects.{objectId}.power", expectedObject.Power, actualObject.Power);
            AddMismatch(
                mismatches,
                $"finalState.cardObjects.{objectId}.untilEndOfTurnPowerModifier",
                expectedObject.UntilEndOfTurnPowerModifier,
                actualObject.UntilEndOfTurnPowerModifier);
            CompareSequence(
                mismatches,
                $"finalState.cardObjects.{objectId}.untilEndOfTurnEffects",
                expectedObject.UntilEndOfTurnEffects,
                actualObject.UntilEndOfTurnEffects);
            AddMismatch(mismatches, $"finalState.cardObjects.{objectId}.isFaceDown", expectedObject.IsFaceDown, actualObject.IsFaceDown);
            AddMismatch(mismatches, $"finalState.cardObjects.{objectId}.isAttacking", expectedObject.IsAttacking, actualObject.IsAttacking);
            AddMismatch(mismatches, $"finalState.cardObjects.{objectId}.isDefending", expectedObject.IsDefending, actualObject.IsDefending);
            AddMismatch(mismatches, $"finalState.cardObjects.{objectId}.isExhausted", expectedObject.IsExhausted, actualObject.IsExhausted);
            CompareSequence(mismatches, $"finalState.cardObjects.{objectId}.tags", expectedObject.Tags, actualObject.Tags);
            AddMismatch(mismatches, $"finalState.cardObjects.{objectId}.manaCost", expectedObject.ManaCost, actualObject.ManaCost);
            AddMismatch(
                mismatches,
                $"finalState.cardObjects.{objectId}.attachedToObjectId",
                expectedObject.AttachedToObjectId,
                actualObject.AttachedToObjectId);
            AddMismatch(mismatches, $"finalState.cardObjects.{objectId}.cardNo", expectedObject.CardNo, actualObject.CardNo);
        }
    }

    private static void CompareStackItems(
        List<string> mismatches,
        IReadOnlyList<ConformanceStackItemState>? expected,
        IReadOnlyList<StackItemState> actual)
    {
        if (expected is null)
        {
            return;
        }

        AddMismatch(mismatches, "finalState.stackItems.count", expected.Count, actual.Count);
        for (var i = 0; i < Math.Min(expected.Count, actual.Count); i++)
        {
            var expectedItem = expected[i];
            var actualItem = actual[i];
            AddMismatch(mismatches, $"finalState.stackItems[{i}].stackItemId", expectedItem.StackItemId, actualItem.StackItemId);
            AddMismatch(mismatches, $"finalState.stackItems[{i}].controllerId", expectedItem.ControllerId, actualItem.ControllerId);
            AddMismatch(mismatches, $"finalState.stackItems[{i}].sourceObjectId", expectedItem.SourceObjectId, actualItem.SourceObjectId);
            AddMismatch(mismatches, $"finalState.stackItems[{i}].effectKind", expectedItem.EffectKind, actualItem.EffectKind);
            AddMismatch(mismatches, $"finalState.stackItems[{i}].cardNo", expectedItem.CardNo, actualItem.CardNo);
            CompareSequence(mismatches, $"finalState.stackItems[{i}].targetObjectIds", expectedItem.TargetObjectIds, actualItem.TargetObjectIds);
            AddMismatch(mismatches, $"finalState.stackItems[{i}].damageAmount", expectedItem.DamageAmount, actualItem.DamageAmount);
            AddMismatch(mismatches, $"finalState.stackItems[{i}].destination", expectedItem.Destination, actualItem.Destination);
        }
    }

    private static void CompareSequence<T>(
        List<string> mismatches,
        string path,
        IReadOnlyList<T>? expected,
        IReadOnlyList<T> actual)
    {
        if (expected is null)
        {
            return;
        }

        if (!expected.SequenceEqual(actual))
        {
            mismatches.Add($"{path}: expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}]");
        }
    }

    private static void AddMismatch<T>(
        List<string> mismatches,
        string path,
        T? expected,
        T actual)
        where T : struct
    {
        if (expected.HasValue && !EqualityComparer<T>.Default.Equals(expected.Value, actual))
        {
            mismatches.Add($"{path}: expected {expected.Value}, actual {actual}");
        }
    }

    private static void AddMismatch<T>(
        List<string> mismatches,
        string path,
        T? expected,
        T? actual)
        where T : class
    {
        if (expected is not null && !EqualityComparer<T>.Default.Equals(expected, actual))
        {
            mismatches.Add($"{path}: expected {expected}, actual {actual?.ToString() ?? "<null>"}");
        }
    }

    private static void AddMismatch(
        List<string> mismatches,
        string path,
        string? expected,
        string? actual)
    {
        if (expected is not null && !string.Equals(expected, actual, StringComparison.Ordinal))
        {
            mismatches.Add($"{path}: expected {expected}, actual {actual ?? "<null>"}");
        }
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
