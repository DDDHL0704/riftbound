using System.Security.Cryptography;
using System.Text;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string UnitHeldRuneKind = "UNIT_CHANNEL";
    internal const string UnitHeldHiddenScoreKind = "UNIT_HIDDEN_HERE_SCORE";
    internal static bool IsParsedUnitHeldContext(HeldTriggerContext? context)
        => context?.Kind is UnitHeldRuneKind or UnitHeldHiddenScoreKind;
    internal static string UnitHeldLabel(HeldTriggerContext context)
        => context.Kind == UnitHeldRuneKind ? "据守战场：召出一枚符文；双方响应后结算。"
            : "据守战场且此处控制背面卡牌：获得1分；双方响应后结算。";

    private static TriggerSpec? UnitHeldSpec(string cardNo)
        => UnitBattlefieldHeldTriggerSpecRules.TryGetTrigger(cardNo, t =>
            t.Timing == TriggerTimings.BattlefieldHeld && t.TargetScope == TriggerTargetScopes.SourceUnit
            && t.RequiredExactOtherFriendlyBattlefieldUnits is null
            && (t.Kind == TriggerKinds.UnitBattlefieldHeldCallRune && t.RuneCallCount is > 0
                && t.RequiresControlledFaceDownCardHere is null
                || t.Kind == TriggerKinds.UnitBattlefieldHeldScore && t.ScoreAmount is > 0
                    && t.RequiresControlledFaceDownCardHere == true), out var spec) ? spec : null;

    private static bool ControlsFaceDownCardHere(MatchState state, string controller, string field)
        => state.CardObjects.Values.Any(c => c.IsFaceDown && IsObjectOnField(state.PlayerZones, c.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == controller
            && state.ObjectLocations.TryGetValue(c.ObjectId, out var at)
            && at.Zone == "BATTLEFIELD" && at.BattlefieldObjectId == field);

    private static TriggerQueueItemState CaptureUnitHeld(MatchState state, string sourceId, string player,
        string field, CardObjectState source, RuleTextInstance instance, TriggerSpec spec, int copy)
    {
        var kind = spec.Kind == TriggerKinds.UnitBattlefieldHeldCallRune ? UnitHeldRuneKind : UnitHeldHiddenScoreKind;
        var key = $"{state.TurnNumber}:{state.Tick}:{field}:{sourceId}:{source.ObjectGeneration}:{instance.Id}:{kind}:{copy}";
        var id = "unit-held-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return new(id, player, sourceId, "HOLD_" + kind, "BATTLEFIELD_HELD") {
            SourceCardNo = instance.CardNo,
            HeldContext = new(instance.CardNo, field, kind, spec.RuneCallCount ?? spec.ScoreAmount!.Value,
                source.ObjectGeneration, Origin: instance.Origin, RuleTextInstanceId: instance.Id,
                UnitSource: new(state.Tick, source.CopyFaceTransitions?.Count ?? 0, AttachedRuleTextRules.Face(source)!,
                    IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null))
        };
    }

    internal static bool ValidParsedUnitHeld(MatchState state, HeldTriggerContext context, string effect,
        string controller, string sourceId, string? cardNo)
    {
        if (!IsParsedUnitHeldContext(context) || UnitHeldSpec(context.CardNo) is not { } spec
            || context.Kind != (spec.RuneCallCount is > 0 ? UnitHeldRuneKind : UnitHeldHiddenScoreKind)
            || context.Amount != (spec.RuneCallCount ?? spec.ScoreAmount) || context.RequiredExactOtherFriendlyBattlefieldUnits is not null
            || effect != "HOLD_" + context.Kind || cardNo != context.CardNo || controller != state.TurnPlayerId
            || state.Phase != MatchPhases.TurnStart || state.TurnStartStep != "CHANNEL"
            || context.SourceGeneration < 0 || string.IsNullOrWhiteSpace(sourceId)
            || BattlefieldLocalRules.Battlefield(state, context.BattlefieldObjectId) is null
            || context.UnitSource is not { CapturedTick: >= 0, FaceRevision: >= 0 } captured
            || captured.CapturedTick > state.Tick || string.IsNullOrWhiteSpace(captured.FaceCardNo)
            || context.RuleTextInstanceId != (context.Origin is { } origin
                ? $"ATTACHED:{origin.Equipment?.ObjectId}:{origin.Equipment?.Generation}:{origin.Revision}"
                : $"PRINTED:{sourceId}:{context.SourceGeneration}")
            || !AttachedRuleTextRules.ValidCaptured(state, context.Origin, sourceId, context.SourceGeneration, context.CardNo)
            || captured.TokenFactoryCardNo is not null && (!P6TokenFactoryCatalog.TryGetByCardNo(captured.TokenFactoryCardNo, out var factory)
                || !factory.Tags.Contains(CardObjectTags.UnitCard))) return false;
        if (!state.CardObjects.TryGetValue(sourceId, out var current))
            return captured.Ceased && captured.TokenFactoryCardNo is not null
                && (context.Origin is not null || captured.FaceCardNo == context.CardNo);
        return !captured.Ceased && current.ObjectGeneration >= context.SourceGeneration
            && IsTokenObject(current) == (captured.TokenFactoryCardNo is not null)
            && (captured.TokenFactoryCardNo is null || (current.TokenFactoryCardNo ?? current.CardNo) == captured.TokenFactoryCardNo)
            && (context.Origin is not null || captured.FaceCardNo == context.CardNo)
            && (current.ObjectGeneration > context.SourceGeneration
                || CopyFaceTimeline.FaceAt(current, captured.FaceRevision, captured.CapturedTick) == captured.FaceCardNo);
    }

    private static HeldTriggerContext? MarkCeasedUnitHeld(MatchState before, IReadOnlySet<string> departed,
        string sourceId, HeldTriggerContext? context)
        => IsParsedUnitHeldContext(context) && context!.UnitSource is { Ceased: false } source
            && departed.Contains(sourceId) && before.CardObjects[sourceId].ObjectGeneration == context.SourceGeneration
            && source.TokenFactoryCardNo == (before.CardObjects[sourceId].TokenFactoryCardNo ?? before.CardObjects[sourceId].CardNo)
            ? context with { UnitSource = source with { Ceased = true } } : context;

    internal static bool ValidCeasedUnitHeldSource(MatchState? state, string? itemId, string? controller,
        string? sourceId, string? effect)
    {
        if (state is null || sourceId is null || controller is null || effect is null || state.CardObjects.ContainsKey(sourceId)) return false;
        var stack = state.StackItems.FirstOrDefault(i => i.StackItemId == itemId);
        if (stack?.HeldContext is { UnitSource.Ceased: true } context)
            return stack.SourceObjectId == sourceId && stack.ControllerId == controller && stack.EffectKind == effect
                && ValidParsedUnitHeld(state, context, effect, controller, sourceId, stack.CardNo);
        var trigger = state.TriggerQueue.FirstOrDefault(t => t.TriggerId == itemId);
        return trigger?.HeldContext is { UnitSource.Ceased: true } held
            && trigger.SourceObjectId == sourceId && trigger.ControllerId == controller && trigger.EffectKind == effect
            && ValidParsedUnitHeld(state, held, effect, controller, sourceId, trigger.SourceCardNo);
    }
}
