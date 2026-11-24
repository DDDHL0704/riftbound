namespace Riftbound.Contracts;

// Prepend AddedOptionalCosts to the original command's optional costs when submitting this quote.
public sealed record PlayCostQuoteWithRuneTapsDto(
    PlayCostQuoteDto Quote,
    IReadOnlyList<string> AddedOptionalCosts);
