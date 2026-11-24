using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static class RuntimePlayTriggerCatalog
    {
        // All four indexes scan the same catalog. Share the parsed triggers and keep each index's own filters.
        internal static readonly Lazy<(string CardNo, IReadOnlyList<TriggerSpec> Triggers)[]> Entries =
            new(() => OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
                .Select(card => (card.CardNo, RuleTextParser.Parse(card).Triggers))
                .ToArray());
    }
}
