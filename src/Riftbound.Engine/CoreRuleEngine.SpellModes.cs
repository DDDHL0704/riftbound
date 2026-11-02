using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record SpellModeSetSpec(string CardNo, int RequiredModes,
    IReadOnlyList<CardBehaviorDefinition> Modes);

public sealed record DeclaredSpellMode(string Mode, SpellExecutionState Execution);

public sealed partial class CoreRuleEngine
{
    // CN 355.3 / 355.5: declare every mode and its targets before paying.
    // This only captures choices; the ordinary play plan still owns permission,
    // timing, costs, one spell source, and the eventual resolution sequence.
    internal static bool TryCaptureSpellModes(MatchState state, string player, string source,
        SpellModeSetSpec spec, IReadOnlyList<SpellModeChoice>? choices,
        out IReadOnlyList<DeclaredSpellMode> captured, out string error)
    {
        captured = [];
        error = "所选模式数量、模式或各模式目标不合法。";
        if (!state.Seats.ContainsKey(player)
            || !state.CardObjects.TryGetValue(source, out var card)
            || card.CardNo != spec.CardNo
            || !SourceObjectControlledByPlayerOrLegacyOwned(card, player)
            || spec.RequiredModes <= 0 || spec.Modes is null
            || spec.RequiredModes > spec.Modes.Count
            || spec.Modes.Any(mode => mode is null || mode.CardNo != spec.CardNo
                || string.IsNullOrWhiteSpace(mode.Mode))
            || spec.Modes.Select(mode => mode.Mode).Distinct(StringComparer.Ordinal).Count() != spec.Modes.Count
            || choices is null || choices.Count != spec.RequiredModes
            || choices.Any(choice => choice is null || string.IsNullOrWhiteSpace(choice.Mode)
                || choice.TargetObjectIds is null || choice.TargetObjectIds.Any(string.IsNullOrWhiteSpace))
            || choices.Select(choice => choice.Mode).Distinct(StringComparer.Ordinal).Count() != choices.Count)
            return false;

        var selected = choices.ToDictionary(choice => choice.Mode, StringComparer.Ordinal);
        if (selected.Keys.Any(id => !spec.Modes.Any(mode => mode.Mode == id))) return false;
        var result = new List<DeclaredSpellMode>();
        // CN 158.1 / 359.3.d: the printed order determines resolution order, including when the
        // caller lists the selected modes in a different order.
        foreach (var mode in spec.Modes)
        {
            if (!selected.TryGetValue(mode.Mode, out var choice)) continue;
            var targets = choice.TargetObjectIds.ToArray();
            // Looking at a hidden deck is an instruction during resolution.
            // No private card identity is accepted as an upfront target.
            if (IsDeferredDeckChoice(mode) ? targets.Length != 0
                : !HasLegalDeclaredTargets(state, player, source, mode, targets, PlayCardTargetScopeForBehavior(mode)))
                return false;
            var generations = targets.Distinct(StringComparer.Ordinal).Where(state.CardObjects.ContainsKey)
                .ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal);
            result.Add(new(mode.Mode, new(mode.EffectKind, targets, generations)));
        }
        captured = result.ToArray();
        error = string.Empty;
        return true;
    }
}
