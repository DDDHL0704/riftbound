namespace Riftbound.Contracts;

public sealed record PromptDiagnosticsDto(
    string? PromptId,
    long? SnapshotTick,
    ActionPromptInspectionDto? Inspection,
    IReadOnlyList<ActionPromptObjectContextDto>? ObjectContexts,
    ActionPromptServerFlowDto? ServerFlow);
