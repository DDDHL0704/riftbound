using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CapturedMightyUnit(ObjectBinding Source, int Might);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestMightContext(ObjectBinding Source, string CardNo, string ControllerId,
    long ConquestTick, IReadOnlyList<CapturedMightyUnit> PowerfulUnits, LastKnownMightFrame Frame);

public sealed partial class CoreRuleEngine
{
    private static TriggerSpec? BattlefieldConquestMightDefinition(string cardNo)
        => BattlefieldTriggerSpecRules.TryGetTrigger(cardNo,
            BattlefieldTriggerSpecRules.IsBattlefieldConquerPowerfulPayDrawTrigger, out var spec) ? spec : null;

    private static CapturedMightyUnit[] QualifyingConquestUnits(LastKnownMightFrame frame, string battlefield, int threshold)
    {
        var state = MightFrameState(frame);
        return frame.CardObjects.Values.Where(c => IsVisibleFieldUnitObject(frame.CardObjects, c.ObjectId)
                && IsObjectOnField(frame.PlayerZones, c.ObjectId)
                && frame.ObjectLocations.TryGetValue(c.ObjectId, out var at) && at.Zone == "BATTLEFIELD"
                && at.BattlefieldObjectId == battlefield)
            .Select(c => new CapturedMightyUnit(new(c.ObjectId, c.ObjectGeneration), ResolveCurrentFieldUnitPower(state, c)))
            .Where(c => c.Might >= threshold).OrderBy(c => c.Source.ObjectId, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<TriggerQueueItemState> CaptureBattlefieldConquestMight(MatchState state,
        string controller, string battlefieldId, long tick)
    {
        if (!TryGetBattlefieldCardObject(state.PlayerZones, state.CardObjects, battlefieldId, out var id, out var source)
            || string.IsNullOrWhiteSpace(source.CardNo)
            || BattlefieldConquestMightDefinition(source.CardNo) is not { } spec) return [];
        var frame = CapturePublicMightFrame(state, state.PlayerZones, state.CardObjects);
        var units = QualifyingConquestUnits(frame, id, spec.RequiredPowerThreshold!.Value);
        if (units.Length == 0) return [];
        var context = new BattlefieldConquestMightContext(new(id, source.ObjectGeneration), source.CardNo, controller, tick, units, frame);
        return [new($"CONQUEST-MIGHT:{tick}:{id}", controller, id, spec.Kind, "BATTLEFIELD_CONQUERED") {
            SourceCardNo = source.CardNo, BattlefieldConquestMight = context }];
    }

    internal static bool ValidBattlefieldConquestMightFrame(BattlefieldConquestMightContext context)
    {
        try {
            var frame = context.Frame;
            if (!ValidMightFrameExperience(frame)
                || context.ConquestTick < 0 || context.Source.Generation < 0 || context.PowerfulUnits.Count == 0
                || !frame.PlayerZones.ContainsKey(context.ControllerId)
                || BattlefieldConquestMightDefinition(context.CardNo) is not { } spec
                || !frame.CardObjects.TryGetValue(context.Source.ObjectId, out var source)
                || source.ObjectGeneration != context.Source.Generation || source.CardNo != context.CardNo
                || !source.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag)
                || !frame.ObjectLocations.TryGetValue(source.ObjectId, out var location) || location.Zone != "BATTLEFIELD"
                || location.BattlefieldObjectId != source.ObjectId
                || !frame.PlayerZones.TryGetValue(location.PlayerId, out var sourceZones) || !sourceZones.Battlefields.Contains(source.ObjectId)
                || frame.PlayerZones.Values.Any(z => z.Hand.Count != 0 || z.MainDeck.Count != 0 || z.RuneDeck.Count != 0)
                || frame.BattleDeclarations.Any(e => !e.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal))) return false;
            var publicIds = frame.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Graveyard)
                .Concat(z.Banished).Concat(z.LegendZone).Concat(z.ChampionZone)).ToHashSet(StringComparer.Ordinal);
            if (frame.CardObjects.Any(p => p.Key != p.Value.ObjectId || p.Value.IsFaceDown || !publicIds.Contains(p.Key))
                || frame.ObjectLocations.Keys.Any(id => !frame.CardObjects.ContainsKey(id))) return false;
            var expected = QualifyingConquestUnits(frame, source.ObjectId, spec.RequiredPowerThreshold!.Value);
            return context.PowerfulUnits.SequenceEqual(expected);
        } catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }

    internal static bool ValidBattlefieldConquestMightContext(MatchState state, BattlefieldConquestMightContext context,
        string effect, string source, string controller)
        => ValidBattlefieldConquestMightFrame(context)
            && effect == TriggerKinds.BattlefieldConquerPowerfulPayDraw && source == context.Source.ObjectId
            && controller == context.ControllerId && context.ConquestTick <= state.Tick
            && state.Seats.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(context.Frame.PlayerZones.Keys)
            && context.Frame.CardObjects.All(p => state.CardObjects.TryGetValue(p.Key, out var current)
                ? current.ObjectGeneration >= p.Value.ObjectGeneration : IsTokenObject(p.Value));

    private static GameEvent BattlefieldConquestMightPaymentOpened(StackItemState item, PendingPaymentState pending)
        => new("PAYMENT_WINDOW_OPENED", "选择支付征服技能费用或放弃；支付后双方响应，结算时才抽牌。",
            new Dictionary<string, object?> { ["paymentId"] = pending.PaymentId, ["paymentWindow"] = pending.PaymentWindow,
                ["playerId"] = item.ControllerId, ["battlefieldObjectId"] = item.SourceObjectId, ["battlefieldId"] = item.SourceObjectId,
                ["battlefieldCardNo"] = item.CardNo, ["trigger"] = item.EffectKind,
                ["sourceObjectId"] = item.SourceObjectId,
                ["powerfulObjectId"] = item.BattlefieldConquestMight!.PowerfulUnits[0].Source.ObjectId,
                ["requiredPowerThreshold"] = BattlefieldConquestMightDefinition(item.BattlefieldConquestMight.CardNo)!.RequiredPowerThreshold!.Value,
                ["mana"] = pending.ManaCost, ["paymentChoices"] = pending.LegalPaymentChoiceIds });

    private static StackResolutionResult ResolveBattlefieldConquestMight(MatchState state, StackItemState item)
    {
        var context = item.BattlefieldConquestMight!;
        if (!ValidBattlefieldConquestMightContext(state, context, item.EffectKind, item.SourceObjectId, item.ControllerId)
            || item.TriggerCost is null) throw new InvalidOperationException("Invalid confirmed conquest Might context.");
        var spec = BattlefieldConquestMightDefinition(context.CardNo)!;
        var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent> { new("BATTLEFIELD_TRIGGER_RESOLVED", "征服技能结算抽牌",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["battlefieldId"] = context.Source.ObjectId,
                ["battlefieldObjectId"] = context.Source.ObjectId, ["battlefieldCardNo"] = context.CardNo,
                ["sourceObjectId"] = context.Source.ObjectId, ["trigger"] = spec.Kind,
                ["powerfulObjectId"] = context.PowerfulUnits[0].Source.ObjectId, ["drawCount"] = spec.DrawCount!.Value }) };
        var draw = ApplyDrawToPlayer(state, zones, state.PlayerScores, item.ControllerId,
            spec.DrawCount!.Value, state.RngCursor, events);
        return new(zones, state.CardObjects, draw.PlayerScores, NormalizeExperienceForSeats(state), state.RunePools,
            state.UntilEndOfTurnEffects, draw.WinnerPlayerId, events, [], null, [], null, [], draw.RngCursor);
    }
}
