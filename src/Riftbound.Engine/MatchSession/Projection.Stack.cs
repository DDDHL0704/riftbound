using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
public sealed partial record ResolutionResult
{

    private static Dictionary<string, object?> BuildRuleQueueCoverageSnapshotRow(
        string key,
        int liveCount,
        IReadOnlyList<string> evidenceKeys)
    {
        return new Dictionary<string, object?>
        {
            ["key"] = key,
            ["liveCount"] = liveCount,
            ["evidenceKeys"] = evidenceKeys
        };
    }

    private static IReadOnlyList<string> RuleQueueCoverageEvidenceKeys(params (string Key, int Count)[] evidence)
    {
        return evidence
            .Where(item => item.Count > 0)
            .Select(item => item.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsRuleQueueBattleTaskKind(string kind)
    {
        return kind.StartsWith("BATTLE", StringComparison.Ordinal)
            || string.Equals(kind, "START_BATTLE", StringComparison.Ordinal);
    }

    private static bool IsRuleQueuePaymentTaskKind(string kind)
    {
        return kind.StartsWith("PAYMENT", StringComparison.Ordinal)
            || string.Equals(kind, "COST_PAID", StringComparison.Ordinal);
    }

    private static Dictionary<string, object?>? BuildPendingPaymentSnapshotView(
        MatchState state,
        PendingPaymentState? payment)
    {
        if (payment is null)
        {
            return null;
        }

        var view = new Dictionary<string, object?>
        {
            ["paymentId"] = payment.PaymentId,
            ["choiceKind"] = state.PendingRuleChoice?.Request.Kind,
            ["paymentWindow"] = payment.PaymentWindow,
            ["playerId"] = payment.PlayerId,
            ["cost"] = BuildPaymentCostView(payment),
            ["paymentChoices"] = payment.LegalPaymentChoiceIds,
            ["paymentResourceActions"] = PendingPaymentResourceActionIds(state, payment)
        };
        if (state.PendingRuleChoice?.Request.Kind == "DECK_OBSERVATION_BANISH" && state.DeckObservationView is { } observation)
            view["deckObservationKind"] = observation.Public ? "REVEAL" : "LOOK";
        return view;
    }

    private static Dictionary<string, object?> BuildPaymentCostView(PendingPaymentState payment)
        => payment.CostView();

    private static Dictionary<string, object?> BuildContinuousEffectSnapshotView(ContinuousEffectState effect)
    {
        var view = new Dictionary<string, object?>
        {
            ["effectId"] = effect.EffectId,
            ["scope"] = effect.Scope,
            ["layer"] = effect.Layer,
            ["duration"] = effect.Duration,
            ["targetObjectId"] = effect.TargetObjectId,
            ["sourceObjectId"] = effect.SourceObjectId,
            ["powerDelta"] = effect.PowerDelta,
            ["basePower"] = effect.BasePower,
            ["effectivePower"] = effect.EffectivePower,
            ["sequence"] = effect.Sequence
        };
        if (!string.IsNullOrWhiteSpace(effect.EffectKind))
        {
            view["effectKind"] = effect.EffectKind;
        }

        if (!string.IsNullOrWhiteSpace(effect.SourceCardNo))
        {
            view["sourceCardNo"] = effect.SourceCardNo;
        }

        if (!string.IsNullOrWhiteSpace(effect.SourcePath))
        {
            view["sourcePath"] = effect.SourcePath;
        }

        if (effect.IsLayerEngineFoundationOnly)
        {
            view["layerEngineStatus"] = "FOUNDATION_ONLY";
        }

        if (effect.RequestedPowerDelta.HasValue)
        {
            view["requestedPowerDelta"] = effect.RequestedPowerDelta.Value;
        }

        if (effect.AppliedPowerDelta.HasValue)
        {
            view["appliedPowerDelta"] = effect.AppliedPowerDelta.Value;
        }

        if (effect.MinimumPower.HasValue)
        {
            view["minimumPower"] = effect.MinimumPower.Value;
        }

        if (effect.ResultingPower.HasValue)
        {
            view["resultingPower"] = effect.ResultingPower.Value;
        }

        if (effect.SnapshotMight.HasValue)
        {
            view["snapshotMight"] = effect.SnapshotMight.Value;
        }

        if (effect.AppliedOrder.HasValue)
        {
            view["appliedOrder"] = effect.AppliedOrder.Value;
        }

        if (effect.SourceOrder.HasValue)
        {
            view["sourceOrder"] = effect.SourceOrder.Value;
        }

        if (!string.IsNullOrWhiteSpace(effect.Condition))
        {
            view["condition"] = effect.Condition;
        }

        if (!string.IsNullOrWhiteSpace(effect.Lifecycle))
        {
            view["lifecycle"] = effect.Lifecycle;
        }

        if (effect.ParticipantObjectIds is { Count: > 0 })
        {
            view["participantObjectIds"] = effect.ParticipantObjectIds;
        }

        if (effect.SourceDependencyObjectIds is { Count: > 0 })
        {
            view["sourceDependencyObjectIds"] = effect.SourceDependencyObjectIds;
        }

        if (effect.TargetDependencyObjectIds is { Count: > 0 })
        {
            view["targetDependencyObjectIds"] = effect.TargetDependencyObjectIds;
        }

        if (effect.ParticipantDependencyObjectIds is { Count: > 0 })
        {
            view["participantDependencyObjectIds"] = effect.ParticipantDependencyObjectIds;
        }

        if (effect.DeferredLayerEngineResiduals is { Count: > 0 })
        {
            view["deferredLayerEngineResiduals"] = effect.DeferredLayerEngineResiduals;
        }

        return view;
    }
}
