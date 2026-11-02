using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoundLegend(ObjectBinding Source, string SourceCardNo, string PlayerId,
    string LegendObjectId, string LegendCardNo, long EnteredAtTick, long? DepartedAtTick = null);

public sealed partial record MatchState
{
    // Provenance is retained after departure; it is never a game zone or part
    // of a player projection. A departed physical legend has no live object.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<BoundLegend>? BoundLegends { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string RegisteredLegendChoiceKind = "REGISTERED_LEGEND_SELECTION";

    private static void AddRegisteredLegend(MatchState state, StackItemState item, ObjectBinding source,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        if (!state.PlayerDecklists.TryGetValue(item.ControllerId, out var deck)) return;
        var available = (deck.AdditionalLegendCardNos ?? []).Where(no =>
            !(state.BoundLegends ?? []).Any(binding => binding.PlayerId == item.ControllerId
                && binding.LegendCardNo == no && binding.DepartedAtTick is null))
            .Where(no => PrintedCardFactory.TryCreate("", item.ControllerId, no, out var card)
                && card.Tags.Contains("CARD_TYPE:LEGEND")).ToArray();
        if (available.Length == 0) return;
        var selected = available.Length == 1 ? available[0] : RuleChoices.Value!.Choose(item.ControllerId,
            "选择一名登记的传奇加入你的传奇区域。", available.Select(no => new RuleChoiceOption(no,
                OfficialCardSourceIdentityGroups.DisplayName(no), [no])).ToArray(),
            kind: RegisteredLegendChoiceKind,
            preview: state with { PlayerZones = zones, CardObjects = cards });
        var id = $"BOUND-LEGEND-{state.Tick + 1}-{(state.BoundLegends?.Count ?? 0) + 1}";
        if (!PrintedCardFactory.TryCreate(id, item.ControllerId, selected, out var legend)) return;
        cards.Add(id, legend);
        zones[item.ControllerId] = zones[item.ControllerId] with {
            LegendZone = [..zones[item.ControllerId].LegendZone, id] };
        var binding = new BoundLegend(source, item.CardNo, item.ControllerId, id, selected, state.Tick + 1);
        events.Add(new("BOUND_LEGEND_ENTERED", "登记的传奇活跃加入传奇区域", new Dictionary<string, object?> {
            ["sourceObjectId"] = source.ObjectId, ["objectId"] = id, ["playerId"] = item.ControllerId,
            ["cardNo"] = selected, ["boundLegend"] = binding }));
    }

    private static ResolutionResult ReconcileBoundLegends(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = result.State;
        var bindings = (state.BoundLegends ?? []).ToList();
        var events = result.Events.ToList(); var changed = false;
        for (var index = 0; index < events.Count; index++)
        {
            var ev = events[index];
            if (ev.Kind != "BOUND_LEGEND_ENTERED" || ev.Payload.GetValueOrDefault("boundLegend") is not BoundLegend entry
                || bindings.Any(binding => binding.LegendObjectId == entry.LegendObjectId)) continue;
            entry = entry with { EnteredAtTick = RuleChoices.Value?.CompletionTick ?? state.Tick };
            bindings.Add(entry); changed = true;
            events[index] = ev with { Payload = new Dictionary<string, object?>(ev.Payload) { ["boundLegend"] = entry } };
        }
        if (bindings.Count == 0) return result;
        var cards = state.CardObjects.ToDictionary();
        var zones = NormalizeZonesForSeats(state);
        foreach (var (binding, index) in bindings.Select((binding, index) => (binding, index)).ToArray())
        {
            if (binding.DepartedAtTick is not null || cards.TryGetValue(binding.Source.ObjectId, out var source)
                && source.ObjectGeneration == binding.Source.Generation && IsObjectOnField(zones, source.ObjectId)) continue;
            cards.Remove(binding.LegendObjectId);
            foreach (var player in zones.Keys.ToArray())
                zones[player] = zones[player] with { LegendZone = zones[player].LegendZone
                    .Where(id => id != binding.LegendObjectId).ToArray() };
            var departed = binding with { DepartedAtTick = RuleChoices.Value?.CompletionTick ?? state.Tick };
            bindings[index] = departed; changed = true;
            events.Add(new("BOUND_LEGEND_DEPARTED", "持续时间结束，传奇不再存在于任何游戏区域", new Dictionary<string, object?> {
                ["objectId"] = binding.LegendObjectId, ["cardNo"] = binding.LegendCardNo,
                ["playerId"] = binding.PlayerId, ["boundLegend"] = departed }));
        }
        if (!changed) return result;
        var departedIds = bindings.Where(binding => binding.DepartedAtTick is not null)
            .Select(binding => binding.LegendObjectId).ToHashSet(StringComparer.Ordinal);
        var locations = state.ObjectLocations.Where(pair => !departedIds.Contains(pair.Key)).ToDictionary();
        var next = state with { CardObjects = cards, PlayerZones = zones, BoundLegends = bindings,
            ObjectLocations = ReconcileObjectLocations(locations, zones) };
        return result with { State = next, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidBoundLegends(MatchState state)
    {
        var bindings = state.BoundLegends;
        if (bindings is null) return true;
        if (bindings.Count == 0 || bindings.Any(binding => binding is null || binding.Source is null)) return false;
        if (bindings.Select(binding => binding.LegendObjectId).Distinct(StringComparer.Ordinal).Count() != bindings.Count) return false;
        foreach (var binding in bindings)
        {
            if (string.IsNullOrWhiteSpace(binding.LegendObjectId) || string.IsNullOrWhiteSpace(binding.PlayerId)
                || string.IsNullOrWhiteSpace(binding.Source.ObjectId) || string.IsNullOrWhiteSpace(binding.SourceCardNo)
                || string.IsNullOrWhiteSpace(binding.LegendCardNo) || binding.Source.Generation < 0
                || binding.EnteredAtTick <= 0 || binding.EnteredAtTick > state.Tick
                || !state.Seats.ContainsKey(binding.PlayerId)
                || !CardBehaviorRegistry.TryGetByCardNo(binding.SourceCardNo, out var behavior)
                || !behavior.AddsRegisteredLegendWhileSourceOnField
                || !state.PlayerDecklists.TryGetValue(binding.PlayerId, out var deck)
                || !(deck.AdditionalLegendCardNos ?? []).Contains(binding.LegendCardNo, StringComparer.Ordinal)
                || !PrintedCardFactory.TryCreate(binding.LegendObjectId, binding.PlayerId, binding.LegendCardNo, out var printed)
                || !printed.Tags.Contains("CARD_TYPE:LEGEND")) return false;
            if (binding.DepartedAtTick is { } tick)
            {
                if (tick < binding.EnteredAtTick || tick > state.Tick || state.CardObjects.ContainsKey(binding.LegendObjectId)
                    || state.ObjectLocations.ContainsKey(binding.LegendObjectId)
                    || state.PlayerZones.Values.Any(zones => zones.LegendZone.Concat(zones.Base).Concat(zones.Battlefields).Concat(zones.Hand)
                        .Concat(zones.Graveyard).Concat(zones.MainDeck).Concat(zones.RuneDeck).Concat(zones.Banished).Concat(zones.ChampionZone).Contains(binding.LegendObjectId))) return false;
            }
            else if (!state.CardObjects.TryGetValue(binding.Source.ObjectId, out var source)
                || source.ObjectGeneration != binding.Source.Generation || !IsObjectOnField(state.PlayerZones, source.ObjectId)
                || !state.CardObjects.TryGetValue(binding.LegendObjectId, out var legend)
                || legend.CardNo != binding.LegendCardNo || legend.ObjectGeneration != 0 || TokenObjectRules.IsToken(legend)
                || legend.OwnerId != binding.PlayerId || !legend.Tags.Contains("CARD_TYPE:LEGEND")
                || !state.PlayerZones.Values.Any(zones => zones.LegendZone.Contains(binding.LegendObjectId))
                || bindings.Count(other => other.DepartedAtTick is null && other.PlayerId == binding.PlayerId
                    && other.LegendCardNo == binding.LegendCardNo) != 1) return false;
        }
        return true;
    }

    internal static bool IsDepartedBoundLegendSource(MatchState state, string source, string? no = null)
        => ValidBoundLegends(state) && (state.BoundLegends ?? []).Any(binding => binding.LegendObjectId == source
            && binding.DepartedAtTick is not null && (no is null || no == binding.LegendCardNo));

    private static bool DepartedLegendBindingMatches(MatchState state, ObjectBinding source, string no, long revision, long tick)
        => source.Generation == 0 && revision == 0 && IsDepartedBoundLegendSource(state, source.ObjectId, no)
            && state.BoundLegends!.Any(binding => binding.LegendObjectId == source.ObjectId
                && tick >= binding.EnteredAtTick && tick <= binding.DepartedAtTick);
}
