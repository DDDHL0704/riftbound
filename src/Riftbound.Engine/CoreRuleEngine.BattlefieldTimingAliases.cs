using System.Text.Json.Serialization;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldTimingAlias(RuleTextInstanceBinding Grant, string OriginalTiming,
    string EffectiveTiming, ObjectBinding Battlefield, string ControllerId, long CapturedTick, int CapturedTurn,
    NamedConquestActivationOrigin? Activation = null)
{
    [JsonIgnore] public string EventKind => Activation is not null || EffectiveTiming == "HOLD"
        ? "BATTLEFIELD_HELD" : "BATTLEFIELD_CONQUERED";
}

public sealed partial record StackItemState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattlefieldTimingAlias? TimingAlias { get; init; }
}

public sealed partial record TriggerQueueItemState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattlefieldTimingAlias? TimingAlias { get; init; }
}

public sealed partial record DelayedResourceGain
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattlefieldTimingAlias? TimingAlias { get; init; }
}

public sealed partial class CoreRuleEngine
{
    private static readonly Lazy<IReadOnlySet<string>> HoldConquestAliasArmaments = new(() =>
        OfficialArmamentCatalog.All.Where(d => d.EffectText.Trim().TrimEnd('。', '.')
            == "我的据守效果也是征服效果，反之亦然").Select(d => d.CardNo).ToHashSet(StringComparer.Ordinal));

    private static RuleTextInstanceBinding? HoldConquestAliasGrant(MatchState state, string source)
        => state.CardObjects.TryGetValue(source, out var host)
            ? ArmamentRuleTextRules.Instances(state.PlayerZones, state.CardObjects, host,
                HoldConquestAliasArmaments.Value.Contains).FirstOrDefault()?.Origin : null;

    private static BattlefieldTimingAlias CaptureTimingAlias(MatchState state, string source, string player,
        string field, long tick, string original, NamedConquestActivationOrigin? activation = null)
        => new(HoldConquestAliasGrant(state, source)! with { CapturedTick = tick }, original,
            original == "HOLD" ? "CONQUEST" : "HOLD", new(field, state.CardObjects[field].ObjectGeneration),
            player, tick, state.TurnNumber, activation);

    internal static string ConquestCaptureReason(NamedConquestActivationOrigin? activation, BattlefieldTimingAlias? alias)
        => alias?.EventKind ?? ConquestCaptureReason(activation);

    private static IEnumerable<(object Context, BattlefieldTimingAlias? Alias)> TimingContexts(MatchState state)
        => state.TriggerQueue.Select(t => (Context: TimingCaptureContext(t), Alias: t.TimingAlias))
            .Concat(state.StackItems.Select(i => (Context: TimingCaptureContext(i), Alias: i.TimingAlias)))
            .Concat(state.PendingEffectPlay?.Parent is { } parent
                ? [(TimingCaptureContext(parent), parent.TimingAlias)] : [])
            .Where(row => row.Context is not null).Select(row => (row.Context!, row.Alias));

    private static bool IsHeldCaptureTiming(MatchState state, object context, string controller)
        => controller == state.TurnPlayerId && state.Phase == Riftbound.Contracts.MatchPhases.TurnStart
                && state.TurnStartStep == "CHANNEL"
            || TimingContexts(state).Any(row => ReferenceEquals(row.Context, context)
                && row.Alias is { OriginalTiming: "HOLD", EffectiveTiming: "CONQUEST" } alias
                && alias.ControllerId == controller);

    internal static object? TimingCaptureContext(TriggerQueueItemState item)
        => TimingCaptureContext(item.HeldContext, item.SourceUnitTokens, item.InsightContext, item.UnitEvent,
            item.ConquestSource, item.ConquestFlow, item.ConquestToken, item.UnitConquest, item.RecastContext, item.UnitConquestReturn);
    internal static object? TimingCaptureContext(StackItemState item)
        => TimingCaptureContext(item.HeldContext, item.SourceUnitTokens, item.InsightContext, item.UnitEvent,
            item.ConquestSource, item.ConquestFlow, item.ConquestToken, item.UnitConquest, item.RecastContext, item.UnitConquestReturn);

    private static object? TimingCaptureContext(HeldTriggerContext? held, SourceUnitTokenContext? tokens,
        InsightTriggerContext? insight, UnitEventContext? unitEvent, ConquestSourceContext? source,
        ConquestFlowContext? flow, ConquestTokenContext? conquestToken, UnitConquestContext? conquest,
        RecastTriggerContext? recast, UnitConquestReturnContext? unitReturn)
        => held ?? (object?)(tokens is { Timing: "HOLD" } ? tokens : null)
            ?? (insight is { Kind: "HELD" or "CONQUEST_LOOK" } ? insight : null)
            ?? (unitEvent is not null && (UnitEventTimingMatches(unitEvent.Timing, "HOLD")
                || UnitEventTimingMatches(unitEvent.Timing, "CONQUEST")) ? unitEvent : null)
            ?? (object?)source ?? flow ?? (object?)conquestToken ?? conquest ?? (object?)recast ?? unitReturn;

    internal static string TimingCaptureHash(object context, BattlefieldTimingAlias? alias)
    {
        // Mutable execution choices and last-known source facts are validated by
        // their own continuations; the original skill and its grant stay fixed.
        var stable = context switch {
            UnitEventContext c => c with { Here = null, LastKnownPower = null, BurnedUnit = null, ReflexiveTick = null, Mode = null },
            HeldTriggerContext { UnitSource: { } source } c => c with { UnitSource = source with { Ceased = false } },
            InsightTriggerContext c => c with { CeasedSource = null },
            _ => context
        };
        return MatchStateHasher.HashValue(new { ContextType = context.GetType().Name, Context = stable, Alias = alias });
    }

    internal static string DelayedTimingCaptureHash(DelayedResourceGain gain) => MatchStateHasher.HashValue(gain);

    internal static bool ValidTimingAlias(MatchState state, BattlefieldTimingAlias? alias, string source,
        string controller, object? context)
    {
        if (alias is null) return true;
        var generation = context switch {
            HeldTriggerContext c => c.SourceGeneration, SourceUnitTokenContext c => c.Source.Generation,
            InsightTriggerContext c => c.SourceGeneration, UnitEventContext c => c.SourceGeneration,
            ConquestSourceContext c => c.Source.Generation, ConquestFlowContext c => c.SourceGeneration,
            ConquestTokenContext c => c.SourceGeneration, UnitConquestContext c => c.SourceGeneration,
            RecastTriggerContext c => c.SourceGeneration, UnitConquestReturnContext c => c.Source.Generation,
            DelayedResourceGain => alias.Grant?.Unit?.Generation ?? -1, _ => -1
        };
        return alias.Grant is { } grant && generation >= 0 && alias.ControllerId == controller
            && state.Seats.ContainsKey(controller) && alias.CapturedTick > 0 && alias.CapturedTick <= state.Tick
            && alias.CapturedTurn >= 0 && alias.CapturedTurn <= state.TurnNumber && grant.CapturedTick == alias.CapturedTick
            && alias.OriginalTiming is "HOLD" or "CONQUEST" && alias.EffectiveTiming is "HOLD" or "CONQUEST"
            && alias.OriginalTiming != alias.EffectiveTiming && alias.Battlefield is { Generation: >= 0 } field
            && (alias.OriginalTiming == "HOLD" ? context is HeldTriggerContext { Kind: not "CONQUEST_UNIT" }
                or SourceUnitTokenContext { Timing: "HOLD" } or InsightTriggerContext { Kind: "HELD" } or DelayedResourceGain
                    || context is UnitEventContext heldEvent && UnitEventTimingMatches(heldEvent.Timing, "HOLD")
                : context is HeldTriggerContext { Kind: "CONQUEST_UNIT" } or ConquestSourceContext or ConquestFlowContext
                    or ConquestTokenContext or UnitConquestContext or RecastTriggerContext or UnitConquestReturnContext
                    or InsightTriggerContext { Kind: "CONQUEST_LOOK" }
                    || context is UnitEventContext conquestEvent && UnitEventTimingMatches(conquestEvent.Timing, "CONQUEST"))
            && BattlefieldLocalRules.Battlefield(state, field.ObjectId) is { } battlefield && battlefield.ObjectGeneration >= field.Generation
            && (alias.Activation is null || alias.EffectiveTiming == "CONQUEST"
                && ValidNamedConquestActivation(state, alias.Activation, field.ObjectId))
            && ArmamentRuleTextRules.ValidCaptured(state, grant, source, generation, grant.CardNo,
                HoldConquestAliasArmaments.Value.Contains);
    }

    private static int HeldEffectCopies(MatchState state, string field, string player)
        => 1 + HeldUnitsAt(state, field, player).Sum(id => AttachedRuleTextRules.Instances(state, state.CardObjects[id])
            .Count(i => HeldDefinition(i.CardNo)?.Kind == "BLUE_DELAYED_POWER"));

    private static MatchState QueueHeldAliasesForConquest(MatchState state, string player, string field,
        IReadOnlyList<string> units, List<Riftbound.Contracts.GameEvent> events,
        NamedConquestActivationOrigin? activation = null)
    {
        var hosts = units.Where(id => HoldConquestAliasGrant(state, id) is not null).ToHashSet(StringComparer.Ordinal);
        return hosts.Count == 0 ? state : QueueHeldAbilities(state, [field], events, player, hosts,
            activation is null ? "BATTLEFIELD_CONQUERED" : "BATTLEFIELD_HELD", activation);
    }

    private static IEnumerable<TriggerQueueItemState> CaptureConquestAliasesForHold(MatchState state,
        string player, string field, long tick)
    {
        var units = HeldUnitsAt(state, field, player).Where(id => HoldConquestAliasGrant(state, id) is not null).ToArray();
        if (units.Length == 0) yield break;
        foreach (var capture in CaptureCurrentUnitConquestAbilities(state, player, field, units, tick,
            "BATTLEFIELD_HELD", includeHunt: false))
            for (var copy = 0; copy < HeldEffectCopies(state, field, player); copy++)
            {
                var unitReturn = capture.UnitConquestReturn is { } returned ? returned with { Occurrence = copy } : null;
                yield return capture with {
                    TriggerId = unitReturn is null ? capture.TriggerId + ":ALIAS-HOLD:" + copy : UnitConquestReturnTriggerId(unitReturn),
                    UnitConquestReturn = unitReturn,
                    TimingAlias = CaptureTimingAlias(state, capture.SourceObjectId, player, field, tick, "CONQUEST") };
            }
    }
}
