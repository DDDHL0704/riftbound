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

public sealed record ConformanceExpected(
    long FinalTick,
    IReadOnlyList<string> EventKinds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PromptActions);

public sealed record ConformanceRunResult(
    long FinalTick,
    IReadOnlyList<string> EventKinds,
    IReadOnlyDictionary<string, ActionPromptDto> Prompts);

public static class ConformanceFixtureRunner
{
    public static async Task<ConformanceRunResult> RunAsync(
        ConformanceFixture fixture,
        IRuleEngine ruleEngine,
        CancellationToken cancellationToken)
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(fixture.RoomId, ruleEngine, journal);
        foreach (var playerId in fixture.Players)
        {
            session.EnsurePlayer(playerId);
        }

        ResolutionResult? last = null;
        foreach (var command in fixture.Commands)
        {
            var mapped = GameCommandJsonMapper.Map(command.Cmd);
            last = await session.SubmitAsync(
                    command.PlayerId,
                    command.ClientIntentId,
                    mapped,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (last is null)
        {
            throw new InvalidOperationException($"Fixture {fixture.FixtureId} does not contain commands.");
        }

        var eventKinds = journal.Entries
            .SelectMany(entry => entry.Events)
            .Select(gameEvent => gameEvent.Kind)
            .ToArray();

        return new ConformanceRunResult(last.State.Tick, eventKinds, last.Prompts);
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
