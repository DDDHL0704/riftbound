using Riftbound.Contracts;

namespace Riftbound.Engine;

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
    DateTimeOffset RecordedAt);

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
