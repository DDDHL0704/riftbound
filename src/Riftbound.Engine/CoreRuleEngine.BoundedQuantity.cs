using System.Globalization;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string ResourceConversionAmountKind = "RESOURCE_CONVERSION_AMOUNT";

    // Closed integer intervals, including zero. No resource mutation occurs
    // during selection. Even [0,int.MaxValue] takes at most 31 binary choices.
    private static int ChooseBoundedQuantity(MatchState context, string player, string sourceId,
        string ability, string purpose, int minimum, int maximum, string kind, string reason)
    {
        if (minimum < 0 || maximum < minimum) throw new InvalidOperationException("Invalid quantity interval.");
        var generation = context.CardObjects.GetValueOrDefault(sourceId)?.ObjectGeneration.ToString(CultureInfo.InvariantCulture) ?? "NONE";
        var authority = $"QUANTITY:{Uri.EscapeDataString(sourceId)}:{generation}:{Uri.EscapeDataString(ability)}:{Uri.EscapeDataString(purpose)}";
        while (minimum < maximum)
        {
            var midpoint = (int)((long)minimum + ((long)maximum - minimum) / 2);
            var left = $"{authority}:{minimum}:{maximum}:{minimum}:{midpoint}";
            var right = $"{authority}:{minimum}:{maximum}:{midpoint + 1}:{maximum}";
            var selected = RuleChoices.Value!.Choose(player, reason,
                [new(left, $"{minimum}–{midpoint}", [sourceId]), new(right, $"{midpoint + 1}–{maximum}", [sourceId])],
                kind: kind, preview: context with { PendingPayment = null });
            if (selected == left) maximum = midpoint;
            else minimum = midpoint + 1;
        }
        return minimum;
    }
}
