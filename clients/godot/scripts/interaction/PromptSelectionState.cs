using System.Collections.Generic;

namespace Riftbound.GodotClient.Interaction;

public sealed record PromptSelectionState(
    string PromptId,
    long SnapshotTick,
    string ActionName,
    string? SourceId,
    IReadOnlyList<string> TargetIds,
    string? DestinationId,
    string? Mode,
    IReadOnlyList<string> OptionalCostIds,
    bool CanSubmit,
    string Summary);
