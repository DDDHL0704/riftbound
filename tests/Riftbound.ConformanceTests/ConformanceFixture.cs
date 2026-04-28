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
    IReadOnlyDictionary<string, ConformanceCardObjectState>? CardObjects = null,
    string? PriorityPlayerId = null,
    IReadOnlyList<string>? PassedPriorityPlayerIds = null,
    IReadOnlyList<ConformanceStackItemState>? StackItems = null);

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

public sealed record ConformanceCardObjectState(
    int? Damage = null,
    IReadOnlyList<string>? UntilEndOfTurnEffects = null);

public sealed record ConformanceStackItemState(
    string? StackItemId = null,
    string? ControllerId = null,
    string? SourceObjectId = null,
    string? EffectKind = null);

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
    IReadOnlyDictionary<string, int>? Scores = null,
    IReadOnlyDictionary<string, ConformanceCardObjectState>? CardObjects = null,
    string? PriorityPlayerId = null,
    IReadOnlyList<string>? PassedPriorityPlayerIds = null,
    IReadOnlyList<ConformanceStackItemState>? StackItems = null);

public sealed record ConformanceExpectedEvent(
    string Kind,
    long? Tick = null,
    long? Sequence = null);

public sealed record ConformanceExpectedPrompt(
    bool? Actionable = null,
    IReadOnlyList<string>? Actions = null);

public sealed record ConformanceRunResult(
    long FinalTick,
    IReadOnlyList<string> EventKinds,
    IReadOnlyDictionary<string, ActionPromptDto> Prompts,
    MatchState FinalState);

public static class ConformanceFixtureRunner
{
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

        var eventKinds = journal.Entries
            .Where(entry => !string.Equals(entry.CommandType, "READY", StringComparison.Ordinal))
            .SelectMany(entry => entry.Events)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();

        return new ConformanceRunResult(last.State.Tick, eventKinds, last.Prompts, last.State);
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
            BuildStackItems(initial));
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

    private static IReadOnlyDictionary<string, CardObjectState> BuildCardObjects(ConformanceInitialState initial)
    {
        return (initial.CardObjects ?? new Dictionary<string, ConformanceCardObjectState>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => new CardObjectState(
                    entry.Key.Trim(),
                    entry.Value.Damage ?? 0,
                    entry.Value.UntilEndOfTurnEffects),
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<StackItemState> BuildStackItems(ConformanceInitialState initial)
    {
        return (initial.StackItems ?? [])
            .Select(item => new StackItemState(
                item.StackItemId,
                item.ControllerId,
                item.SourceObjectId,
                item.EffectKind))
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
