using System.Globalization;
using System.Text;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record TimeAssassinNextUnitReduction(
    int TurnNumber,
    string ControllerId,
    ObjectBinding Source,
    string CardNo,
    long CreatedTick);

public sealed record TimeAssassinDestructionContext(
    int TurnNumber,
    string ControllerId,
    ObjectBinding Source,
    ObjectBinding Target,
    string CardNo,
    long CreatedTick);

public sealed partial class CoreRuleEngine
{
    internal const string TimeAssassinNextUnitReductionPrefix = "TIME_ASSASSIN_NEXT_UNIT|";
    internal const string TimeAssassinDestructionPrefix = "TIME_ASSASSIN_DESTROY|";

    private static bool ValidTimeAssassinLegendAction(MatchState state, StackItemState item)
        => item.LegendUnitEffect is { } context
            && context.AbilityId == LegendActionAbilityCatalog.TimeAssassinLegendAbilityId
            && context.SourceGeneration >= 0 && context.SpellshieldTaxPower == 0
            && LegendActionAbilityCatalog.IsSourceCardNoForAbility(context.AbilityId, item.CardNo)
            && state.Seats.ContainsKey(item.ControllerId)
            && state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration >= context.SourceGeneration
            && (source.ObjectGeneration != context.SourceGeneration || source.CardNo == item.CardNo)
            && item.EffectKind == LegendAbilityEffectKinds.TimeAssassinNextUnitReduction
            && item.TimingContext == TimingStates.NeutralOpen && !item.SourceConfirmed
            && item.EffectRepeatCount == 1 && item.DamageAmount == 0 && item.OptionalCosts.Count == 0
            && item.Destination.Length == 0 && item.TargetObjectIds.Count == 0
            && item.TargetGenerations is { Count: 0 };

    private static StackResolutionResult ResolveTimeAssassinLegendAction(MatchState state, StackItemState item)
    {
        var marker = TimeAssassinNextUnitReductionMarker(state.TurnNumber, item.ControllerId,
            new(item.SourceObjectId, item.LegendUnitEffect!.SourceGeneration), item.CardNo!, state.Tick + 1);
        return NoopStackResolutionResult(state) with {
            UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(state.UntilEndOfTurnEffects, marker),
            Events = [new("TIME_ASSASSIN_NEXT_UNIT_REDUCTION_CREATED", "创建时间刺客下一名单位减费效果", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["cardNo"] = item.CardNo, ["effectId"] = marker, ["manaReduction"] = 2,
                ["powerReduction"] = 1, ["duration"] = "UNTIL_END_OF_TURN" })]
        };
    }

    private static string TimeAssassinIdentity(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string TimeAssassinMarker(TimeAssassinNextUnitReduction reduction)
        => string.Join('|',
            "TIME_ASSASSIN_NEXT_UNIT",
            reduction.TurnNumber.ToString(CultureInfo.InvariantCulture),
            TimeAssassinIdentity(reduction.ControllerId),
            TimeAssassinIdentity(reduction.Source.ObjectId),
            reduction.Source.Generation.ToString(CultureInfo.InvariantCulture),
            TimeAssassinIdentity(reduction.CardNo),
            reduction.CreatedTick.ToString(CultureInfo.InvariantCulture));

    internal static string TimeAssassinNextUnitReductionMarker(
        int turnNumber,
        string controllerId,
        ObjectBinding source,
        string cardNo,
        long createdTick)
        => TimeAssassinMarker(new(turnNumber, controllerId, source, cardNo, createdTick));

    internal static TimeAssassinNextUnitReduction? ReadTimeAssassinNextUnitReduction(string marker)
    {
        if (!marker.StartsWith(TimeAssassinNextUnitReductionPrefix, StringComparison.Ordinal)) return null;
        var fields = marker.Split('|');
        if (fields.Length != 7
            || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn)
            || turn < 1
            || Decode(fields[2]) is not { Length: > 0 } controller
            || Decode(fields[3]) is not { Length: > 0 } sourceId
            || !long.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var generation)
            || generation < 0
            || Decode(fields[5]) is not { Length: > 0 } cardNo
            || !long.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick)
            || tick < 1)
            return null;
        var reduction = new TimeAssassinNextUnitReduction(turn, controller, new(sourceId, generation), cardNo, tick);
        return TimeAssassinMarker(reduction) == marker ? reduction : null;

        static string? Decode(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
            catch (FormatException) { return null; }
        }
    }

    internal static bool ValidTimeAssassinNextUnitReduction(MatchState state, TimeAssassinNextUnitReduction reduction)
        => reduction is not null
            && reduction.TurnNumber == state.TurnNumber
            && state.Seats.ContainsKey(reduction.ControllerId)
            && reduction.Source.Generation >= 0
            && !string.IsNullOrWhiteSpace(reduction.CardNo)
            && LegendActionAbilityCatalog.IsSourceCardNoForAbility(
                LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, reduction.CardNo)
            && state.CardObjects.TryGetValue(reduction.Source.ObjectId, out var source)
            && source.ObjectGeneration >= reduction.Source.Generation
            && (source.ObjectGeneration != reduction.Source.Generation || source.CardNo == reduction.CardNo)
            && reduction.CreatedTick > 0
            && reduction.CreatedTick <= state.Tick;

    internal static bool ValidCurrentTimeAssassinNextUnitReductions(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null || state.UntilEndOfTurnEffects.Any(value => value is null)) return false;
        var markers = state.UntilEndOfTurnEffects
            .Where(value => value.StartsWith(TimeAssassinNextUnitReductionPrefix, StringComparison.Ordinal))
            .ToArray();
        return markers.Length == markers.Distinct(StringComparer.Ordinal).Count()
            && markers.All(marker => ReadTimeAssassinNextUnitReduction(marker) is { } reduction
                && ValidTimeAssassinNextUnitReduction(state, reduction));
    }

    private static string TimeAssassinDestructionMarker(TimeAssassinDestructionContext context)
        => string.Join('|', "TIME_ASSASSIN_DESTROY",
            context.TurnNumber.ToString(CultureInfo.InvariantCulture),
            TimeAssassinIdentity(context.ControllerId),
            TimeAssassinIdentity(context.Source.ObjectId),
            context.Source.Generation.ToString(CultureInfo.InvariantCulture),
            TimeAssassinIdentity(context.Target.ObjectId),
            context.Target.Generation.ToString(CultureInfo.InvariantCulture),
            TimeAssassinIdentity(context.CardNo),
            context.CreatedTick.ToString(CultureInfo.InvariantCulture));

    internal static string TimeAssassinDestructionMarker(
        int turnNumber, string controllerId, ObjectBinding source, ObjectBinding target, string cardNo, long createdTick)
        => TimeAssassinDestructionMarker(new(turnNumber, controllerId, source, target, cardNo, createdTick));

    internal static TimeAssassinDestructionContext? ReadTimeAssassinDestructionMarker(string marker)
    {
        if (!marker.StartsWith(TimeAssassinDestructionPrefix, StringComparison.Ordinal)) return null;
        var fields = marker.Split('|');
        if (fields.Length != 9
            || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn)
            || turn < 1
            || Decode(fields[2]) is not { Length: > 0 } controller
            || Decode(fields[3]) is not { Length: > 0 } sourceId
            || !long.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sourceGeneration)
            || sourceGeneration < 0
            || Decode(fields[5]) is not { Length: > 0 } targetId
            || !long.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetGeneration)
            || targetGeneration < 0
            || Decode(fields[7]) is not { Length: > 0 } cardNo
            || !long.TryParse(fields[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick)
            || tick < 1)
            return null;
        var context = new TimeAssassinDestructionContext(turn, controller,
            new(sourceId, sourceGeneration), new(targetId, targetGeneration), cardNo, tick);
        return TimeAssassinDestructionMarker(context) == marker ? context : null;

        static string? Decode(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
            catch (FormatException) { return null; }
        }
    }

    internal static bool ValidTimeAssassinDestructionMarker(MatchState state, string marker, string targetObjectId)
    {
        var context = ReadTimeAssassinDestructionMarker(marker);
        return context is not null && state.CardObjects.TryGetValue(targetObjectId, out var target)
            && ValidTimeAssassinDestructionContext(state, context, target);
    }

    private static bool ValidTimeAssassinDestructionContext(
        MatchState state, TimeAssassinDestructionContext context, CardObjectState target)
    {
        if (context.TurnNumber != state.TurnNumber
            || context.CreatedTick < 1 || context.CreatedTick > state.Tick
            || context.Target.ObjectId != target.ObjectId || context.Target.Generation < 0
            || !state.Seats.ContainsKey(context.ControllerId)
            || !LegendActionAbilityCatalog.IsSourceCardNoForAbility(
                LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, context.CardNo)
            || target.ObjectGeneration != context.Target.Generation
            || !target.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || target.IsFaceDown)
            return false;
        return state.CardObjects.TryGetValue(context.Source.ObjectId, out var source)
            && source.ObjectGeneration >= context.Source.Generation
            && (source.ObjectGeneration != context.Source.Generation || source.CardNo == context.CardNo);
    }

    internal static IReadOnlyList<TimeAssassinNextUnitReduction> TimeAssassinNextUnitReductions(
        MatchState state, string playerId)
        => state.UntilEndOfTurnEffects
            .Where(marker => marker.StartsWith(TimeAssassinNextUnitReductionPrefix, StringComparison.Ordinal))
            .Select(ReadTimeAssassinNextUnitReduction).OfType<TimeAssassinNextUnitReduction>()
            .Where(reduction => reduction.ControllerId == playerId && ValidTimeAssassinNextUnitReduction(state, reduction))
            .ToArray();

    private static string? UnitObjectId(GameEvent ev)
        => ev.Payload.GetValueOrDefault("unitObjectId") as string
            ?? ev.Payload.GetValueOrDefault("tokenObjectId") as string
            ?? ev.Payload.GetValueOrDefault("targetObjectId") as string;

    internal static ResolutionResult RecordTimeAssassinNextUnitReductions(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var effects = result.State.UntilEndOfTurnEffects.ToList();
        var cards = result.State.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        foreach (var ev in result.Events)
        {
            events.Add(ev);
            if (ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")
                || UnitObjectId(ev) is not { } objectId || !cards.TryGetValue(objectId, out var unit)
                || !unit.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)) continue;
            var player = ev.Payload.GetValueOrDefault("playerId") as string ?? unit.ControllerId;
            if (string.IsNullOrWhiteSpace(player)) continue;
            var reductions = TimeAssassinNextUnitReductions(result.State with { UntilEndOfTurnEffects = effects }, player);
            if (reductions.Count == 0) continue;
            foreach (var reduction in reductions)
            {
                effects.Remove(TimeAssassinMarker(reduction));
                var marker = TimeAssassinDestructionMarker(result.State.TurnNumber, player,
                    reduction.Source, new(objectId, unit.ObjectGeneration), reduction.CardNo, reduction.CreatedTick);
                unit = unit with { UntilEndOfTurnEffects = unit.UntilEndOfTurnEffects.Append(marker)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
                events.Add(new("TIME_ASSASSIN_UNIT_MARKED_FOR_DESTRUCTION", "时间刺客标记单位于回合结束时摧毁", new Dictionary<string, object?> {
                    ["playerId"] = player, ["sourceObjectId"] = reduction.Source.ObjectId,
                    ["targetObjectId"] = objectId, ["targetGeneration"] = unit.ObjectGeneration,
                    ["cardNo"] = unit.CardNo, ["effectId"] = marker, ["duration"] = "UNTIL_END_OF_TURN" }));
            }
            cards[objectId] = unit;
        }
        if (effects.SequenceEqual(result.State.UntilEndOfTurnEffects) && cards.All(pair =>
                result.State.CardObjects.TryGetValue(pair.Key, out var original) && original == pair.Value)) return result;
        var state = result.State with { UntilEndOfTurnEffects = effects, CardObjects = cards };
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidCurrentTimeAssassinDestructions(MatchState state)
    {
        foreach (var card in state.CardObjects.Values)
        {
            var markers = card.UntilEndOfTurnEffects
                .Where(marker => marker.StartsWith(TimeAssassinDestructionPrefix, StringComparison.Ordinal)).ToArray();
            if (markers.Distinct(StringComparer.Ordinal).Count() != markers.Length
                || markers.Any(marker => !ValidTimeAssassinDestructionMarker(state, marker, card.ObjectId))) return false;
        }
        return true;
    }
}
