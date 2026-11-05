using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateStandbyHandPlayPaymentHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var items = state.StackItems.Concat(state.PendingEffectPlay is { } pending ? [pending.Parent] : [])
            .Where(i => CoreRuleEngine.IsStandbyHandPlayHere(i) && i.TriggerCost is not null);
        foreach (var item in items)
        {
            if (!CardBehaviorRegistry.TryGetByCardNo(item.UnitEvent!.CardNo, out var card)
                || card.UnitEventAbility is not { LeadingPowerTrait: { } trait } spec) continue;
            var paymentId = "TRIGGER-COST:" + item.StackItemId;
            var payments = events.Where(e => e.Event.Kind == "COST_PAID" && e.Tick >= item.UnitEvent!.CapturedTick
                && TryReadObjectString(e.Event.Payload, "paymentId", out var id) && id == paymentId
                && TryReadObjectString(e.Event.Payload, "paymentWindow", out var window) && window == CoreRuleEngine.TriggerCostWindow
                && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == item.ControllerId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == item.SourceObjectId
                && TryReadObjectLong(e.Event.Payload, "totalManaCost", out var mana) && mana == item.TriggerCost!.Mana
                && TryReadObjectLong(e.Event.Payload, "totalPowerCost", out var power) && power == item.TriggerCost!.Power
                && TryReadObjectValue(e.Event.Payload, "powerByTrait", out var traits)
                && JsonSerializer.SerializeToElement(traits) is { ValueKind: JsonValueKind.Object } typed
                && typed.EnumerateObject().Count() == 1 && typed.TryGetProperty(trait, out var paidTrait)
                && paidTrait.ValueKind == JsonValueKind.Number && paidTrait.TryGetInt32(out var amount) && amount == spec.LeadingPowerCost)
                .Take(2).ToArray();
            if (payments.Length != 1 || !commands.Any(command => command.Accepted && command.PlayerId == item.ControllerId
                    && command.CommandType == CommandTypes.PayCost && command.CompletedTick == payments[0].Tick
                    && command.StartedEventSequence < payments[0].Sequence && command.CompletedEventSequence >= payments[0].Sequence
                    && (command.RawCommand ?? command.TypedCommand?.Value) is { } input
                    && TryReadObjectString(input, "paymentId", out var id) && id?.Trim() == paymentId
                    && TryReadObjectString(input, "paymentWindow", out var window) && window?.Trim() == CoreRuleEngine.TriggerCostWindow
                    && TryReadObjectStringList(input, "paymentChoiceIds", out var choices)
                    && choices.Count == 1 && choices[0].Trim() == "PAY"))
                errors.Add("standby hand effect-play receipt is not bound to accepted payment history");
        }
    }
}
