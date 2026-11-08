using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class UnitBattleWinTriggerSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> ByCardNo = new(Build);

    public static bool IsUnitBattleWinDrawTrigger(TriggerSpec spec)
        => spec.Kind == TriggerKinds.UnitBattleWinDraw && spec.Timing == TriggerTimings.UnitBattleWon
            && spec.TargetScope == TriggerTargetScopes.SourceUnit && spec.DrawCount is > 0
            && spec.CreatedTokenCount is null;

    public static bool IsUnitBattleWinDormantGoldTrigger(TriggerSpec spec)
        => spec.Kind == TriggerKinds.UnitBattleWinCreateDormantGold && spec.Timing == TriggerTimings.UnitBattleWon
            && spec.TargetScope == TriggerTargetScopes.SourceUnit && spec.DrawCount is null
            && spec.CreatedTokenCount is > 0 && spec.CreatedTokenName == "金币"
            && spec.CreatedTokenDestination == TriggerZones.Base && spec.CreatedTokenExhausted == true;

    internal static IReadOnlyList<TriggerSpec> Rewards(string? cardNo)
        => cardNo is not null && ByCardNo.Value.TryGetValue(cardNo, out var entries) ? entries : [];

    internal static bool TryGet(string? cardNo, int index, out TriggerSpec spec)
    {
        spec = default!;
        var entries = Rewards(cardNo);
        if (index < 0 || index >= entries.Count) return false;
        spec = entries[index];
        return true;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>> Build()
    {
        var catalog = OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult();
        return catalog.Cards.ToDictionary(card => card.CardNo,
            card => (IReadOnlyList<TriggerSpec>)TriggerParser.Parse(card.CardEffect ?? "")
                .Where(t => IsUnitBattleWinDrawTrigger(t) || IsUnitBattleWinDormantGoldTrigger(t)).ToArray());
    }
}
