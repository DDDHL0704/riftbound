namespace Riftbound.Contracts;

public sealed record PlayCostPreviewRequestDto(
    string RequestId,
    string PromptId,
    long SnapshotTick,
    PlayCardCommand Command);

public sealed record PlayCostAdjustmentDto(string Label, int Mana, int Power = 0);
public sealed record PlayPaymentResourceActionQuoteDto(
    string ActionId, int PowerCost, int UnitPlayOrUnitAbilityOnlyManaGained);

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
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int AvailableUnitPlayOrUnitAbilityOnlyMana { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? RemainingUnitPlayOrUnitAbilityOnlyMana { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? RemainingOrdinaryMana { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ActionPromptChoiceDto>? SpellPowerReductionChoices { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? AvailableWildcardSources { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? RemainingWildcardSources { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PlayPaymentResourceActionQuoteDto>? PaymentResourceActions { get; init; }
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
    /// <summary>The quote contains known costs; target selection is still required before play.</summary>
    public bool RequiresTargetSelection { get; init; }
    /// <summary>Target-dependent costs, including Ward, will be included after targets are selected.</summary>
    public bool TargetDependentCosts { get; init; }

    public static PlayCostQuoteDto Rejected(PlayCostPreviewRequestDto request, long tick, string code, string message)
        => new(request.RequestId, request.PromptId, tick, false, false, message, code);
}
