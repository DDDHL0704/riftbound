using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlayedUnitRuneObservation(ObjectBinding Unit, LastKnownMightFrame Frame, string ControllerId);

public sealed partial class CoreRuleEngine
{
    private const string PlayedUnitCallRunes = "PLAYED_UNIT_CALL_RUNES";
    private const string PlayedUnitReadyRunes = "PLAYED_UNIT_READY_RUNES";
    private static readonly Lazy<IReadOnlyDictionary<string, UnitEventAbilitySpec>> PlayedUnitRuneSpecs = new(() =>
    {
        var specs = new Dictionary<string, UnitEventAbilitySpec>(StringComparer.Ordinal);
        foreach (var card in OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards)
        {
            var text = (card.CardEffect ?? "").Normalize(NormalizationForm.FormKC);
            if (text.Contains("当你打出一名{{强力}}单位时，你可以选择让我变为休眠状态，以此召出一枚休眠的符文。".Normalize(NormalizationForm.FormKC), StringComparison.Ordinal))
                specs[card.CardNo] = new("FRIENDLY_UNIT_PLAYED", PlayedUnitCallRunes, 1,
                    Optional: true, LeadingSourceExhaust: true, MinimumPlayedUnitPower: PowerfulUnitPowerThreshold);
            var ready = Regex.Match(text, "当你打出我或其他[“\"](?<tag>[^”\"]+)[”\"]属性单位时，让最多(?<count>[一两二三四五六七八九十0-9]+)枚符文变为活跃状态。".Normalize(NormalizationForm.FormKC), RegexOptions.CultureInvariant);
            if (ready.Success)
                specs[card.CardNo] = new("FRIENDLY_UNIT_PLAYED", PlayedUnitReadyRunes,
                    RunePlayNumber(ready.Groups["count"].Value),
                    Optional: true, AnyNumberOfTargets: true, RequiredPlayedUnitTag: ready.Groups["tag"].Value);
        }
        return specs;
    });

    private static int RunePlayNumber(string text) => int.TryParse(text, out var value) ? value : text switch {
        "一" => 1, "两" or "二" => 2, "三" => 3, "四" => 4, "五" => 5, "六" => 6, "七" => 7, "八" => 8, "九" => 9, "十" => 10,
        _ => throw new InvalidOperationException("Invalid rune count.") };

    private static bool IsPlayedUnitRuneSpec(UnitEventAbilitySpec? spec)
        => spec?.Effect is PlayedUnitCallRunes or PlayedUnitReadyRunes;

    private static UnitEventObservation[] CapturePlayedUnitRunes(GameEvent ev,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState entered, string player, IReadOnlyList<string> effects, UnitEventObservation[] observations)
    {
        if (!observations.Any(o => IsPlayedUnitRuneSpec(UnitEventSpec(o.Context.CardNo)))) return observations;
        var origin = RuleChoices.Value!.ResolutionState ?? RuleChoices.Value.Origin;
        var locations = ReconcileObjectLocations(origin.ObjectLocations, zones);
        var zone = ev.Payload.GetValueOrDefault("destinationZone") as string ?? "BASE";
        var field = ev.Payload.GetValueOrDefault("battlefieldObjectId") as string
            ?? ev.Payload.GetValueOrDefault("battlefieldId") as string;
        locations[entered.ObjectId] = new(player, zone, zone == "BATTLEFIELD" ? field : null);
        var current = origin with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, UntilEndOfTurnEffects = effects };
        var frame = CapturePublicMightFrame(current, zones, cards, locations);
        return observations.Where(o => !IsPlayedUnitRuneSpec(UnitEventSpec(o.Context.CardNo))
                || PlayedUnitMeetsRuneCondition(current, entered, o.Source, UnitEventSpec(o.Context.CardNo)!))
            .Select(o => !IsPlayedUnitRuneSpec(UnitEventSpec(o.Context.CardNo)) ? o : o with { Context = o.Context with {
                PlayedUnitRunes = new(new(entered.ObjectId, entered.ObjectGeneration), frame, player) } }).ToArray();
    }

    private static UnitEventContext NormalizePlayedUnitRuneFrame(MatchState before, UnitEventContext context)
    {
        if (context.PlayedUnitRunes is not { } observation) return context;
        var frame = observation.Frame;
        var cards = frame.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        foreach (var (id, card) in frame.CardObjects)
            if (before.CardObjects.TryGetValue(id, out var prior) && !IsObjectOnField(before.PlayerZones, id)
                && IsObjectOnField(frame.PlayerZones, id))
                cards[id] = card with { ObjectGeneration = Math.Max(card.ObjectGeneration, prior.ObjectGeneration + 1) };
        return context with { PlayedUnitRunes = observation with {
            Unit = observation.Unit with { Generation = cards[observation.Unit.ObjectId].ObjectGeneration },
            Frame = frame with { CardObjects = cards } } };
    }

    private static bool PlayedUnitMeetsRuneCondition(MatchState state, CardObjectState unit, string source, UnitEventAbilitySpec spec)
        => IsFaceUpNonStandbyUnit(unit) && IsObjectOnField(state.PlayerZones, unit.ObjectId)
            && (spec.MinimumPlayedUnitPower is not { } minimum || ResolveCurrentFieldUnitPower(state, unit) >= minimum)
            && (spec.RequiredPlayedUnitTag is not { } tag || source == unit.ObjectId || unit.Tags.Contains(tag));

    private static bool ValidPlayedUnitRuneObservation(MatchState state, UnitEventContext context, string source, string? expectedController)
    {
        var spec = UnitEventSpec(context.CardNo);
        if (!IsPlayedUnitRuneSpec(spec)) return context.PlayedUnitRunes is null;
        try
        {
            if (context.PlayedUnitRunes is not { Unit: { Generation: >= 0 } binding, Frame: { } frame }
                || !ValidMightFrameExperience(frame) || frame.CardObjects is null || frame.ObjectLocations is null || frame.BattleDeclarations is null
                || !frame.PlayerZones.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(state.Seats.Keys)
                || frame.PlayerZones.Values.Any(z => z is null || z.Hand.Count != 0 || z.MainDeck.Count != 0 || z.RuneDeck.Count != 0)
                || frame.BattleDeclarations.Any(e => e is null || !e.StartsWith(BattleResponseDeclarationContextPrefix, StringComparison.Ordinal))
                || !frame.CardObjects.TryGetValue(source, out var observer) || observer.ObjectGeneration != context.SourceGeneration
                || observer.ControllerId is not { } controller || !state.Seats.ContainsKey(controller)
                || context.PlayedUnitRunes.ControllerId != controller || expectedController is not null && controller != expectedController
                || !IsObjectOnField(frame.PlayerZones, source) && !frame.PlayerZones[controller].LegendZone.Contains(source)
                || !AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, observer)
                    .Any(i => i.CardNo == context.CardNo && i.Origin == context.Origin)
                || !frame.CardObjects.TryGetValue(binding.ObjectId, out var unit) || unit.ObjectGeneration != binding.Generation
                || unit.ControllerId != controller || !PlayedUnitMeetsRuneCondition(MightFrameState(frame), unit, source, spec!)) return false;
            var publicIds = frame.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Graveyard)
                .Concat(z.Banished).Concat(z.LegendZone).Concat(z.ChampionZone)).ToHashSet(StringComparer.Ordinal);
            return !frame.CardObjects.Any(p => p.Value is null || p.Key != p.Value.ObjectId || p.Value.IsFaceDown
                    || !publicIds.Contains(p.Key) || (state.CardObjects.TryGetValue(p.Key, out var current)
                        ? current.ObjectGeneration < p.Value.ObjectGeneration || !ValidHistoricalMightyFace(state, current, p.Value)
                        : !HasKnownHistoricalTokenFactory(p.Value)))
                && frame.ObjectLocations.Keys.All(frame.CardObjects.ContainsKey);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }

    private static StackResolutionResult ResolvePlayedUnitRunes(MatchState state, StackItemState item, UnitEventAbilitySpec spec)
    {
        var cards = state.CardObjects.ToDictionary();
        var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent>();
        if (spec.Effect == PlayedUnitCallRunes)
        {
            if (item.TriggerCost is null) return NoopStackResolutionResult(state);
            var called = CallRunes(zones, cards, item.ControllerId, spec.Amount);
            events.Add(new("RUNES_CALLED", "打出单位触发召出符文", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds.ToArray() }));
        }
        else
        {
            var legal = UnitEventTargets(state, item);
            foreach (var id in item.TargetObjectIds.Where(id => legal.Contains(id) && StackObjectRules.ReferencesCurrentObject(state, item, id)))
                if (cards[id].IsExhausted)
                {
                    cards[id] = cards[id] with { IsExhausted = false };
                    events.Add(new("RUNE_READIED", "打出单位触发活跃已选择符文", new Dictionary<string, object?> {
                        ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["objectIds"] = new[] { id }, ["count"] = 1 }));
                }
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
