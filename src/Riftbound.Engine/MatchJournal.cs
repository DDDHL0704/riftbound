using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class RulesetDefaults
{
    public const string RulesetVersion = "UNBOUND";
    public const string FaqVersion = "UNBOUND";
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
    JsonElement? RawCommand,
    long StartedTick,
    long CompletedTick,
    long StartedEventSequence,
    long CompletedEventSequence,
    bool Accepted,
    string? ErrorMessage,
    MatchState AuthoritativeState,
    IReadOnlyList<GameEvent> Events,
    IReadOnlyDictionary<string, SnapshotDto> Snapshots,
    IReadOnlyDictionary<string, ActionPromptDto> Prompts,
    DateTimeOffset RecordedAt,
    string RulesetVersion = RulesetDefaults.RulesetVersion,
    string FaqVersion = RulesetDefaults.FaqVersion,
    string RulesAuditStatus = RulesetDefaults.AuditStatus,
    IReadOnlyList<RuleEvidenceRef>? RulesEvidence = null)
{
    [System.Text.Json.Serialization.JsonRequired]
    public RuleInputIdentity? RuleInputs { get; init; } = AuthoritativeState.RuleInputs;
    public string RulesetVersion { get; init; } = RulesetVersion == RulesetDefaults.RulesetVersion
        ? AuthoritativeState.RuleInputs?.IdentitySha256 ?? RulesetDefaults.RulesetVersion : RulesetVersion;
    public string FaqVersion { get; init; } = FaqVersion == RulesetDefaults.FaqVersion
        ? AuthoritativeState.RuleInputs?.AuthoritySha256 ?? RulesetDefaults.FaqVersion : FaqVersion;

    public void ValidateRuleInputs()
    {
        RuleInputIdentity.RequireCurrentState(AuthoritativeState);
        var inputs = RuleInputs;
        if (inputs is null || inputs != AuthoritativeState.RuleInputs
            || RulesetVersion != inputs.IdentitySha256 || FaqVersion != inputs.AuthoritySha256)
            throw new MatchSessionException(ErrorCodes.RecoveryInconsistent,
                "journal rule input identity or version does not match authoritative state");
    }
}

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
