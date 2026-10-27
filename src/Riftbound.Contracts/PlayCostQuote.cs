namespace Riftbound.Contracts;

public sealed record PlayCostPreviewRequestDto(
    string RequestId,
    string PromptId,
    long SnapshotTick,
    PlayCardCommand Command);

public sealed record PlayCostAdjustmentDto(string Label, int Mana, int Power = 0);

public sealed record PlayCostBreakdownDto(
    int PrintedMana,
    int PrintedPower,
    int Mana,
    int GenericPower,
    IReadOnlyDictionary<string, int> PowerByTrait,
    int Experience,
    int AvailableMana,
    int AvailableRainbowPower,
    IReadOnlyDictionary<string, int> AvailablePowerByTrait,
    int MissingMana,
    int MissingPower,
    int MissingExperience,
    int? RemainingMana,
    int? RemainingRainbowPower,
    IReadOnlyDictionary<string, int>? RemainingPowerByTrait,
    IReadOnlyList<PlayCostAdjustmentDto> Adjustments)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int AvailableSpellOnlyMana { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? RemainingSpellOnlyMana { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int AvailableDuelOnlyMana { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? RemainingDuelOnlyMana { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ActionPromptChoiceDto>? SpellPowerReductionChoices { get; init; }
}

// A quote is informational. Submission always rebuilds and authorizes its plan.
public sealed record PlayCostQuoteDto(
    string RequestId,
    string PromptId,
    long SnapshotTick,
    bool IsValid,
    bool CanPay,
    string Message,
    string? ErrorCode = null,
    PlayCostBreakdownDto? Cost = null)
{
    public static PlayCostQuoteDto Rejected(PlayCostPreviewRequestDto request, long tick, string code, string message)
        => new(request.RequestId, request.PromptId, tick, false, false, message, code);
}
