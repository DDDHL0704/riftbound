using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestConsumeBoonContext(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] string ConquestSourceObjectId,
    [property: JsonRequired] long ConquestTick);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestConsumeBoonPublicContext(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] long ConquestTick);

public sealed partial class CoreRuleEngine
{
    internal static TriggerSpec? BattlefieldConquestConsumeBoonDefinition(string? cardNo)
        => BattlefieldTriggerSpecRules.TryGetTrigger(cardNo,
            spec => BattlefieldTriggerSpecRules.IsBattlefieldConquerConsumeBoonDrawTrigger(spec)
                && spec.ConsumedBoonCount == 1, out var spec) ? spec : null;

    internal static BattlefieldConquestConsumeBoonPublicContext? PublicBattlefieldConquestConsumeBoon(
        BattlefieldConquestConsumeBoonContext? context)
        => context is null ? null : new(context.Source, context.CardNo, context.ControllerId, context.ConquestTick);

    internal static bool ValidBattlefieldConquestConsumeBoonContext(MatchState state,
        BattlefieldConquestConsumeBoonContext context, string effect, string source, string controller, string? cardNo)
        => context.Source is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(context.Source.ObjectId)
            && !string.IsNullOrWhiteSpace(context.ControllerId) && !string.IsNullOrWhiteSpace(context.ConquestSourceObjectId)
            && context.ConquestTick >= 0 && context.ConquestTick <= state.Tick
            && BattlefieldConquestConsumeBoonDefinition(context.CardNo) is { } spec && effect == spec.Kind
            && source == context.Source.ObjectId && controller == context.ControllerId && cardNo == context.CardNo
            && state.Seats.ContainsKey(controller) && state.CardObjects.TryGetValue(source, out var battlefield)
            && (battlefield.CardNo == context.CardNo || ReplacedBattlefieldCard(state, battlefield)?.CardNo == context.CardNo)
            && battlefield.ObjectGeneration >= context.Source.Generation
            && (state.BattlefieldResolutions.Any(r => r.Kind == "CONQUERED" && r.Tick == context.ConquestTick
                    && r.BattlefieldObjectId == source && r.PlayerId == controller
                    && r.SourceObjectId == context.ConquestSourceObjectId)
                || state.BattlefieldResolutions.Count == 12
                    && state.BattlefieldResolutions.All(r => r.Tick > context.ConquestTick));
}
