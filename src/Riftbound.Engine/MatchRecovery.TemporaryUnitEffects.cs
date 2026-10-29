using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateTemporaryNumericUnitEffects(MatchState state, IReadOnlyList<RecoveredEvent> events,
        bool historyRequired, List<string> errors)
    {
        var budgets = new Dictionary<(string Target, long Generation, string Budget), int>();
        var bases = new Dictionary<(string Target, long Generation), int>();
        if (historyRequired)
        foreach (var row in events.OrderBy(e => e.Sequence))
        {
            var ev = row.Event;
            if (ev.Kind == "TURN_END_CLEANUP_STARTED") { budgets.Clear(); bases.Clear(); continue; }
            if (ev.Kind is not ("DAMAGE_PREVENTION_GRANTED" or "BASE_MIGHT_SET_UNTIL_END_OF_TURN" or "DAMAGE_APPLIED")) continue;
            if (!TryReadObjectString(ev.Payload, "targetObjectId", out var target)
                || !TryReadObjectLong(ev.Payload, "targetGeneration", out var generation)) continue;
            if (ev.Kind is "DAMAGE_PREVENTION_GRANTED" or "BASE_MIGHT_SET_UNTIL_END_OF_TURN")
            {
                if (!TryReadObjectString(ev.Payload, "cardNo", out var no) || no is null || !CardBehaviorRegistry.TryGetByCardNo(no, out var behavior)) {
                    errors.Add("temporary numeric unit effect has no official behavior"); continue;
                }
                if (ev.Kind == "BASE_MIGHT_SET_UNTIL_END_OF_TURN") {
                    if (!TryReadObjectInt(ev.Payload, "baseMight", out var value) || value != behavior.BasePowerUntilEndOfTurn)
                        errors.Add("base Might grant differs from its official behavior");
                    else bases[(target!, generation)] = value;
                } else {
                    if (!TryReadObjectString(ev.Payload, "budgetId", out var id)
                        || !TryReadObjectInt(ev.Payload, "amount", out var amount) || amount <= 0 || amount != behavior.PreventDamageUntilEndOfTurn
                        || !budgets.TryAdd((target!, generation, id!), amount)) errors.Add("invalid finite damage prevention grant");
                }
            }
            else if (ev.Payload.TryGetValue("damagePreventionSpent", out var raw))
            {
                try {
                    var spent = JsonSerializer.SerializeToElement(raw).Deserialize<CoreRuleEngine.DamagePreventionSpend[]>(
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (spent is null) { errors.Add("missing finite damage prevention spending"); continue; }
                    foreach (var entry in spent) {
                        if (entry is null || string.IsNullOrWhiteSpace(entry.BudgetId)) { errors.Add("malformed finite damage prevention spending"); continue; }
                        var key = (target!, generation, entry.BudgetId);
                        if (!budgets.TryGetValue(key, out var before) || entry.Amount <= 0 || entry.Amount > before
                            || entry.Remaining != before - entry.Amount) errors.Add("finite damage prevention spending differs from its grant");
                        else budgets[key] = entry.Remaining;
                    }
                } catch (JsonException) { errors.Add("malformed finite damage prevention spending"); }
            }
        }

        foreach (var card in state.CardObjects.Values)
        {
            var effects = card.UntilEndOfTurnEffects.Where(e => e.StartsWith(TemporaryUnitEffectRules.BaseMightPrefix, StringComparison.Ordinal)
                || e.StartsWith(TemporaryUnitEffectRules.DamagePreventionPrefix, StringComparison.Ordinal)).ToArray();
            var onField = FieldObjectTypeRules.IsVisibleUnit(card) && state.PlayerZones.Values.Any(z => z.Base.Contains(card.ObjectId) || z.Battlefields.Contains(card.ObjectId));
            if (!onField) {
                if (effects.Length > 0) errors.Add("temporary numeric unit effects require the same public field generation");
                continue;
            }
            var actual = TemporaryUnitEffectRules.PreventionBudgets(card);
            var baseEffects = effects.Where(e => e.StartsWith(TemporaryUnitEffectRules.BaseMightPrefix, StringComparison.Ordinal)).ToArray();
            if (actual.Length + baseEffects.Length != effects.Length || actual.Select(b => b.Id).Distinct().Count() != actual.Length
                || baseEffects.Length > 1 || baseEffects.Length == 1 && TemporaryUnitEffectRules.Number(card, TemporaryUnitEffectRules.BaseMightPrefix) is null)
            {
                errors.Add("malformed temporary numeric unit effect");
                continue;
            }
            if (!historyRequired) continue;
            var expected = budgets.Where(b => b.Key.Target == card.ObjectId && b.Key.Generation == card.ObjectGeneration && b.Value > 0)
                .ToDictionary(b => b.Key.Budget, b => b.Value);
            if (MatchStateHasher.HashValue(expected) != MatchStateHasher.HashValue(actual.ToDictionary(b => b.Id, b => b.Remaining)))
                errors.Add("finite damage prevention budget differs from accepted grants and spending");
            if (TemporaryUnitEffectRules.Number(card, TemporaryUnitEffectRules.BaseMightPrefix) != bases.GetValueOrDefault((card.ObjectId, card.ObjectGeneration), -1)
                && (baseEffects.Length > 0 || bases.ContainsKey((card.ObjectId, card.ObjectGeneration))))
                errors.Add("base Might replacement differs from accepted history");
        }
    }
}
