using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

// Only public objects and public zone contents are retained. Private piles are
// excluded from this historical numeric evaluation context.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LastKnownMightFrame(
    IReadOnlyDictionary<string, PlayerZones> PlayerZones,
    IReadOnlyDictionary<string, CardObjectState> CardObjects,
    IReadOnlyDictionary<string, ObjectLocationState> ObjectLocations,
    IReadOnlyList<string> BattleDeclarations,
    IReadOnlyDictionary<string, int> PlayerExperience);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LastBreathMightContext(string SourceObjectId, string CardNo, long SourceGeneration,
    string ControllerId, int Might, LastKnownMightFrame Frame, string InstanceId, RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    private static LastKnownMightFrame CapturePublicMightFrame(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        IReadOnlyDictionary<string, ObjectLocationState>? locations = null)
    {
        var publicIds = zones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Graveyard)
            .Concat(z.Banished).Concat(z.LegendZone).Concat(z.ChampionZone)).ToHashSet(StringComparer.Ordinal);
        var visible = cards.Where(p => publicIds.Contains(p.Key) && !p.Value.IsFaceDown)
            .ToDictionary(p => p.Key, p => p.Value with {
                UntilEndOfTurnPowerModifiers = p.Value.UntilEndOfTurnPowerModifiers
                    .Select(modifier => modifier with { ChosenActionReplacement = null }).ToArray()
            }, StringComparer.Ordinal);
        string[] Field(IReadOnlyList<string> ids) => ids.Where(visible.ContainsKey).ToArray();
        var publicZones = zones.ToDictionary(p => p.Key, p => new PlayerZones([], [], [],
            Field(p.Value.Base), Field(p.Value.Battlefields), p.Value.Graveyard.ToArray(), Field(p.Value.Banished),
            Field(p.Value.LegendZone), Field(p.Value.ChampionZone)), StringComparer.Ordinal);
        var publicLocations = ReconcileObjectLocations(locations ?? state.ObjectLocations, zones)
            .Where(p => visible.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return new(publicZones, visible, publicLocations, state.UntilEndOfTurnEffects
            .Where(e => e.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal)).ToArray(),
            publicZones.Keys.ToDictionary(id => id, id => state.PlayerExperience.GetValueOrDefault(id), StringComparer.Ordinal));
    }

    private static bool ValidMightFrameExperience(LastKnownMightFrame frame)
        => frame.PlayerExperience is not null && frame.PlayerZones is not null
            && frame.PlayerExperience.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(frame.PlayerZones.Keys)
            && frame.PlayerExperience.Values.All(value => value >= 0);

    private static MatchState MightFrameState(LastKnownMightFrame frame)
    {
        if (!ValidMightFrameExperience(frame)) throw new ArgumentException("Invalid public Might-frame experience inputs.");
        return new("LAST-KNOWN-MIGHT", 0, 0, frame.PlayerZones.Keys.First(),
            frame.PlayerZones.Keys.ToDictionary(p => p, p => p, StringComparer.Ordinal),
            status: MatchStatuses.InProgress, phase: MatchPhases.Main,
            playerZones: frame.PlayerZones, cardObjects: frame.CardObjects,
            objectLocations: frame.ObjectLocations, untilEndOfTurnEffects: frame.BattleDeclarations,
            playerExperience: frame.PlayerExperience);
    }

    private static LastBreathMightContext[] CaptureLastBreathMight(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState unit)
    {
        if (!IsFaceUpNonStandbyUnit(unit)) return [];
        var instances = AttachedRuleTextRules.Instances(zones, cards, unit)
            .Where(i => UnitDestroyedTriggerSpecRules.TryGetTrigger(i.CardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathPowerfulDrawTrigger, out _)).ToArray();
        if (instances.Length == 0) return [];
        var frame = RuleChoices.Value?.CurrentDestructionMightFrame ?? CapturePublicMightFrame(state, zones, cards);
        if (!frame.CardObjects.TryGetValue(unit.ObjectId, out var captured)) return [];
        var might = ResolveCurrentFieldUnitPower(MightFrameState(frame), captured);
        return instances.Select(i => new LastBreathMightContext(unit.ObjectId, i.CardNo, captured.ObjectGeneration,
            EffectiveFieldControllerId(frame.PlayerZones, unit.ObjectId, captured), might, frame, i.Id, i.Origin)).ToArray();
    }

    internal static bool ValidLastBreathMightFrame(LastBreathMightContext context)
    {
        try {
            var state = MightFrameState(context.Frame);
            if (!state.CardObjects.TryGetValue(context.SourceObjectId, out var before)) return false;
            var cards = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal);
            if (IsTokenObject(before)) cards.Remove(before.ObjectId);
            else if (PrintedCardFactory.TryRestoreOutsidePlay(before, before.OwnerId ?? context.ControllerId, out var departed))
                cards[before.ObjectId] = departed with { ObjectGeneration = before.ObjectGeneration + 1 };
            else return false;
            return ValidLastBreathMightContext(state with { CardObjects = cards }, context,
                TriggerKinds.UnitLastBreathPowerfulDraw, context.SourceObjectId, context.ControllerId);
        } catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }

    internal static bool ValidLastBreathMightContext(MatchState state, LastBreathMightContext context,
        string effect, string source, string controller)
    {
        try { return ValidLastBreathMightCore(state, context, effect, source, controller); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }

    private static bool ValidLastBreathMightCore(MatchState state, LastBreathMightContext context,
        string effect, string source, string controller)
    {
        if (context is not { SourceGeneration: >= 0, Might: >= 0, Frame: { } frame }
            || effect != TriggerKinds.UnitLastBreathPowerfulDraw || context.SourceObjectId != source
            || context.ControllerId != controller || frame.CardObjects is null || frame.PlayerZones is null
            || frame.ObjectLocations is null || frame.BattleDeclarations is null || frame.PlayerZones.Count == 0
            || !ValidMightFrameExperience(frame)
            || frame.BattleDeclarations.Any(e => e is null || !e.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal))
            || !frame.CardObjects.TryGetValue(source, out var unit) || unit.ObjectId != source
            || unit.ObjectGeneration != context.SourceGeneration
            || !AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, unit).Any(i => i.Id == context.InstanceId && i.CardNo == context.CardNo)
            || !AttachedRuleTextRules.ValidCaptured(state, context.Origin, source, context.SourceGeneration, context.CardNo)
            || !IsFaceUpNonStandbyUnit(unit) || !IsObjectOnField(frame.PlayerZones, source)
            || EffectiveFieldControllerId(frame.PlayerZones, source, unit) != controller
            || !UnitDestroyedTriggerSpecRules.TryGetTrigger(context.CardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathPowerfulDrawTrigger, out var spec)
            || context.Might < spec.RequiredPowerThreshold.GetValueOrDefault(PowerfulUnitPowerThreshold)) return false;
        if (frame.PlayerZones.Any(p => !state.Seats.ContainsKey(p.Key) || p.Value is null
                || p.Value.Hand.Count != 0 || p.Value.MainDeck.Count != 0 || p.Value.RuneDeck.Count != 0)
            || frame.CardObjects.Any(p => p.Value is null || p.Key != p.Value.ObjectId || p.Value.IsFaceDown
                || (state.CardObjects.TryGetValue(p.Key, out var current)
                    ? current.ObjectGeneration < p.Value.ObjectGeneration || current.ObjectGeneration == p.Value.ObjectGeneration && current.CardNo != p.Value.CardNo
                    : !IsTokenObject(p.Value)))) return false;
        if (!state.CardObjects.TryGetValue(source, out var departed))
            return IsTokenObject(unit) && ResolveCurrentFieldUnitPower(MightFrameState(frame), unit) == context.Might;
        if (departed.ObjectGeneration <= context.SourceGeneration) return false;
        // Independent stored input is checked against the present incarnation
        // wherever its values survive departure; the numeric result is rederived.
        if (departed.ObjectGeneration == context.SourceGeneration + 1
            && (!PrintedCardFactory.TryRestoreOutsidePlay(unit, departed.OwnerId ?? controller, out var printed)
                || departed.Power != printed.Power)) return false;
        return ResolveCurrentFieldUnitPower(MightFrameState(frame), unit) == context.Might;
    }
}
