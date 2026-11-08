using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendConquestUnitOrigin(ObjectBinding Unit, string CardNo, long FaceRevision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TokenFactoryCardNo = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendConquestDrawOrigin(string ControllerId, long CapturedTick, long FaceRevision,
    ObjectBinding Battlefield, IReadOnlyList<LegendConquestUnitOrigin> CountedUnits);

public sealed partial class CoreRuleEngine
{
    private static bool ValidLegendConquestDrawShape(LegendConquestContext context, TriggerSpec spec)
        => context.Draw is { CapturedTick: > 0, FaceRevision: >= 0 } draw
            && !string.IsNullOrWhiteSpace(draw.ControllerId)
            && draw.Battlefield is { Generation: >= 0 } && draw.Battlefield.ObjectId == context.BattlefieldId
            && draw.CountedUnits is { } units && units.Count >= spec.RequiredUnitCount!.Value
            && units.All(unit => unit is { Unit.Generation: >= 0, FaceRevision: >= 0 }
                && !string.IsNullOrWhiteSpace(unit.Unit.ObjectId))
            && units.Select(unit => unit.Unit.ObjectId).Distinct(StringComparer.Ordinal).Count() == units.Count;

    internal static bool ValidLegendConquestState(MatchState state, LegendConquestContext context,
        string source, string controller, string effect, string? cardNo = null)
    {
        if (!ValidLegendConquest(context, effect, cardNo)) return false;
        if (context.Kind != LegendConquestDrawKind) return true;
        var draw = context.Draw!;
        return draw.ControllerId == controller && state.Seats.ContainsKey(controller)
            && draw.CapturedTick <= state.Tick
            && PredictBindingMatches(state, new(source, context.SourceGeneration), context.CardNo,
                draw.FaceRevision, draw.CapturedTick, null)
            && state.CardObjects.TryGetValue(draw.Battlefield.ObjectId, out var field)
            && field.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag) && field.ObjectGeneration >= draw.Battlefield.Generation
            && draw.CountedUnits.All(unit => unit.Unit.ObjectId != source
                && TryPredictEnteredFace(unit.CardNo, out var face) && face.Tags.Contains(CardObjectTags.UnitCard)
                && PredictBindingMatches(state, unit.Unit, unit.CardNo, unit.FaceRevision,
                    draw.CapturedTick, unit.TokenFactoryCardNo));
    }

    internal static bool ValidLegendConquestDrawStack(MatchState state, StackItemState item)
        => item.LegendConquest?.Kind != LegendConquestDrawKind
            || ValidLegendConquestState(state, item.LegendConquest, item.SourceObjectId, item.ControllerId,
                item.EffectKind, item.CardNo)
                && !item.SourceConfirmed && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
                && item.TriggerCost is null && item.DamageAmount == 0 && item.EffectRepeatCount == 1
                && item.OptionalCosts.Count == 0 && item.Destination.Length == 0;

    private static StackResolutionResult ResolveLegendConquestDraw(MatchState state, StackItemState item)
    {
        if (!ValidLegendConquestDrawStack(state, item))
            throw new InvalidOperationException("Invalid captured conditional conquest draw.");
        var context = item.LegendConquest!; var spec = LegendConquestDefinition(context.CardNo)!.Value.Spec;
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent> { new("LEGEND_TRIGGER_RESOLVED", "征服条件抽牌技能结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["legendObjectId"] = item.SourceObjectId, ["legendCardNo"] = context.CardNo,
            ["trigger"] = spec.Kind, ["battlefieldId"] = context.BattlefieldId,
            ["controlledBattlefieldUnitCount"] = context.Draw!.CountedUnits.Count }) };
        // CN 383.2.a.1: the count is a trigger condition captured at conquest.
        // Responses may change those units or remove the legend before this draw.
        var draw = ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, item.ControllerId,
            spec.DrawCount!.Value, state.RngCursor, events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), PlayerScores = draw.PlayerScores,
            WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor, Events = events };
    }
}
