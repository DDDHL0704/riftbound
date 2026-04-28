using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class RulesetDefaults
{
    public const string RulesetVersion = "rules-260330";
    public const string FaqVersion = "official-pdf-faq-set-2026-04-28";
    public const string AuditStatus = "NEEDS_RULE_AUDIT";
}

public sealed record RuleEvidenceRef(
    string Source,
    string Locator,
    string Note);

public sealed record MatchJournalEntry(
    string RoomId,
    string PlayerId,
    string ClientIntentId,
    string CommandType,
    long StartedTick,
    long CompletedTick,
    bool Accepted,
    string? ErrorMessage,
    IReadOnlyList<GameEvent> Events,
    IReadOnlyDictionary<string, SnapshotDto> Snapshots,
    IReadOnlyDictionary<string, ActionPromptDto> Prompts,
    DateTimeOffset RecordedAt,
    string RulesetVersion = RulesetDefaults.RulesetVersion,
    string FaqVersion = RulesetDefaults.FaqVersion,
    string RulesAuditStatus = RulesetDefaults.AuditStatus,
    IReadOnlyList<RuleEvidenceRef>? RulesEvidence = null);

public interface IMatchJournal
{
    ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken);
}

public sealed class NoopMatchJournal : IMatchJournal
{
    public static NoopMatchJournal Instance { get; } = new();

    private NoopMatchJournal()
    {
    }

    public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
