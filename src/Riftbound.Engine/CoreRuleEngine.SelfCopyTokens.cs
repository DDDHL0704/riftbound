using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool ValidSelfCopyTokenPlay(MatchState state, StackItemState item)
    {
        var expected = item.SourceConfirmed && CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var behavior)
            && behavior.CreatedBaseUnitTokensCopySource && behavior.EffectKind == item.EffectKind;
        if (item.SelfCopySource is not { } source) return !expected;
        return expected && source.ObjectId == item.SourceObjectId && source.Generation >= 0
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
            && state.CardObjects.TryGetValue(source.ObjectId, out var card) && source.Generation <= card.ObjectGeneration;
    }

    private static ObjectLocationState? SelfCopyTokenLocation(MatchState state, StackItemState item)
    {
        // CN 359.3.f.2: "here" is read when this instruction executes. Leaving
        // play or returning as a new object cannot reuse the old source/location.
        if (item.SelfCopySource is not { } source || !ValidSelfCopyTokenPlay(state, item)
            || !state.CardObjects.TryGetValue(source.ObjectId, out var card) || card.ObjectGeneration != source.Generation
            || !IsFaceUpNonStandbyUnit(card) || !state.ObjectLocations.TryGetValue(source.ObjectId, out var location)
            || !state.PlayerZones.TryGetValue(location.PlayerId, out var sourceZones)) return null;
        if (location.Zone == "BASE" && location.PlayerId == item.ControllerId && sourceZones.Base.Contains(source.ObjectId)) return location;
        return BattlefieldLocalRules.AtUnit(state, source.ObjectId) is { } battlefield
            && !BattlefieldLocalRules.PreventsUnitPlay(state, "BATTLEFIELD:" + battlefield.ObjectId) ? location : null;
    }

    private static StackResolutionResult ResolveSelfCopyTokenPlay(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        if (SelfCopyTokenLocation(state, item) is not { } location) return NoopStackResolutionResult(state);
        var card = state.CardObjects[item.SourceObjectId];
        var field = location.Zone == "BASE" ? null : location.BattlefieldObjectId;
        var result = CreateUnitTokenBatch(state, item, P6TokenFactoryCatalog.ImageTokenCardNo,
            behavior.CreatedBaseUnitTokenCount * item.EffectRepeatCount + AdditionalUnitTokens(item), behavior.CreatedBaseUnitTokenEntersReady, field);
        var recipients = result.Events.Where(e => e.Kind == "UNIT_TOKEN_CREATED")
            .Select(e => result.CardObjects[(string)e.Payload["tokenObjectId"]!]).ToArray();
        if (recipients.Length == 0) return result;
        var events = result.Events.ToList();
        // April CN erratum, DOC-10-P003-B007: "then do this" creates a
        // separate reflexive trigger. Its self reference is not a chosen target.
        AddReflexiveCopyEvent(events, item.ControllerId, item.SourceObjectId, item.CardNo, card, recipients, copySourceIsSelf: true);
        return result with { Events = events };
    }
}
