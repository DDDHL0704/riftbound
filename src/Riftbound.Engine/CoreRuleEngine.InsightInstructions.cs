using System.Globalization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string OpeningInsightWindow = "S5_OPENING_INSIGHT";

    // Shared CN 437 instruction: private look (including observed-card permissions),
    // then recycle an arbitrary subset to the bottom using the normal recycle observers.
    private static string[] LookForInsight(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, StackItemState instruction, int count, List<GameEvent> events)
        => ObserveDeckCards(state, zones, cards, instruction, instruction.ControllerId,
            zones[instruction.ControllerId].MainDeck.Take(count).ToArray(), "LOOK", instruction.CompletedDeckExecutions, events);

    private static (string[] Retained, long Cursor) RecycleInsightSelection(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, string player,
        IReadOnlyList<string> viewed, IReadOnlyList<string> selected, string? source, List<GameEvent> events)
    {
        var retained = viewed.Where(id => !selected.Contains(id)).ToArray();
        var recycled = RandomizeForMainDeckBottom(selected, state.Seed, state.RngCursor, source ?? OpeningInsightWindow);
        var cursor = state.RngCursor + (selected.Count > 1 ? 1 : 0);
        zones[player] = zones[player] with
        {
            MainDeck = retained.Concat(zones[player].MainDeck.Skip(viewed.Count)).Concat(recycled).ToArray()
        };
        if (selected.Count > 0)
        {
            var payload = new Dictionary<string, object?> { ["playerId"] = player, ["count"] = selected.Count };
            if (source is not null) payload["sourceObjectId"] = source;
            events.Add(CaptureRecycleObservers(new("CARDS_RECYCLED", $"洞察回收 {selected.Count} 张牌", payload), zones, cards, selected));
        }
        return (retained, cursor);
    }

    private static StackItemState CityInsightInstruction(string player, long tick)
        => new($"S5-INSIGHT:{tick}", player, OpeningRules.S5CityMode2, OpeningInsightWindow);

    private static MatchState BeginCityOpeningInsight(MatchState state, List<GameEvent> events)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var instruction = CityInsightInstruction(state.TurnPlayerId, state.Tick);
        var viewed = LookForInsight(state, zones, cards, instruction, 1, events);
        var next = state with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), TurnStartStep = "S5_DRAW" };
        if (viewed.Length == 0) return next;
        events.Add(new("INSIGHT_REQUESTED", "后手第一次抽牌阶段：先进行洞察1",
            new Dictionary<string, object?> { ["playerId"] = state.TurnPlayerId, ["count"] = 1 }));
        return next with
        {
            PendingCardChoice = new($"S5-INSIGHT:{state.Tick}", OpeningInsightWindow, state.TurnPlayerId,
                0, 1, viewed, viewed, "洞察1：查看主牌堆顶部一张牌，可以选择回收；不选则保留，然后抽牌。",
                effectKind: OpeningInsightWindow)
        };
    }

    internal static bool ValidCityInsightChoice(MatchState state, PendingCardChoiceState choice)
        => CityOpeningRules.Rule(state) == OpeningRules.S5CityMode2 && state.TurnNumber == 2
            && state.TurnPlayerId == state.OpeningSecondActionPlayerId && state.TurnStartStep == "S5_DRAW"
            && choice.PlayerId == state.TurnPlayerId && choice.ChoiceWindow == OpeningInsightWindow
            && choice.ChoiceId.StartsWith("S5-INSIGHT:", StringComparison.Ordinal)
            && long.TryParse(choice.ChoiceId.AsSpan("S5-INSIGHT:".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var captured)
            && captured >= 0 && captured <= state.Tick && choice.RequiredCount == 0 && choice.MaxCount == 1
            && choice.SourceObjectId == "" && choice.EffectKind == OpeningInsightWindow && choice.DeckContext is null
            && choice.ContextObjectIds.Count == 1 && choice.ContextObjectIds.SequenceEqual(choice.LegalObjectIds)
            && state.PlayerZones.TryGetValue(choice.PlayerId, out var zones)
            && zones.MainDeck.Take(1).SequenceEqual(choice.ContextObjectIds);

    private static ResolutionResult ResolveCityInsightChoice(MatchState state, PendingCardChoiceState choice,
        IReadOnlyList<string> selected)
    {
        if (!ValidCityInsightChoice(state, choice))
            return RejectWithCorePrompts(state, "测试开局洞察上下文已经失效。", ErrorCodes.InvalidTarget);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>();
        var recycled = RecycleInsightSelection(state, zones, cards, choice.PlayerId, choice.ContextObjectIds,
            selected, null, events);
        events.Add(new("INSIGHT_COMPLETED", "后手已完成开局洞察，继续第一次抽牌阶段",
            new Dictionary<string, object?> { ["playerId"] = choice.PlayerId, ["recycledCount"] = selected.Count }));
        var next = state with { Tick = state.Tick + 1, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), RngCursor = recycled.Cursor,
            PendingCardChoice = null };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    internal static bool ValidCityObservationParent(MatchState state, StackItemState parent)
        => CityOpeningRules.Rule(state) == OpeningRules.S5CityMode2 && state.TurnNumber >= 2
            && parent.ControllerId == state.OpeningSecondActionPlayerId
            && parent.StackItemId.StartsWith("S5-INSIGHT:", StringComparison.Ordinal)
            && long.TryParse(parent.StackItemId.AsSpan("S5-INSIGHT:".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var tick)
            && tick >= 0 && tick <= state.Tick
            && MatchStateHasher.HashValue(parent) == MatchStateHasher.HashValue(CityInsightInstruction(parent.ControllerId, tick));
}
