using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record CityDeckRegistration(OfficialDecklist Deck, IReadOnlyList<string> Sideboard);
public sealed record CityOpeningState(
    IReadOnlyDictionary<string, CityDeckRegistration> Registrations,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Sideboards,
    string? FirstPlayerId = null, string? SecondPlayerId = null,
    IReadOnlyDictionary<string, string>? Battlefields = null, long? SelectionCursor = null);

public sealed partial record MatchState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CityOpeningState? CityOpening { get; init; }
}

public static class CityOpeningRules
{
    private static readonly Lazy<OfficialCardCatalog> Catalog = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult());
    public static string Rule(MatchState state) => state.SessionOptions?.OpeningRule ?? OpeningRules.Standard;
    public static bool Enabled(MatchState state) => Rule(state) != OpeningRules.Standard;

    public static string? OptionError(MatchSessionOptions options)
    {
        if (options.OpeningRule is { } rule && !OpeningRules.IsValid(rule)) return "未知的开局规则。";
        if (options.OpeningRule is not (null or OpeningRules.Standard)
            && (options.DeckFormat == OfficialDeckFormat.Sealed || options.AllowLegacyReadyWithoutDeck))
            return "城市挑战赛测试开局只支持提交正式卡组的构筑单局制。";
        return null;
    }

    public static void RequireOptions(MatchSessionOptions options)
    {
        if (OptionError(options) is { } error) throw new ArgumentException(error, nameof(options));
    }

    internal static OpeningSetupDto? View(MatchState state, string? viewer)
    {
        if (!Enabled(state)) return null;
        var setup = state.CityOpening;
        return new(state.Status == MatchStatuses.InProgress || state.Status == MatchStatuses.Finished ? "PLAYING"
                : setup?.FirstPlayerId is not null ? "SIDEBOARD" : "REGISTER_DECKS",
            setup?.FirstPlayerId ?? "", setup?.SecondPlayerId ?? "",
            viewer is null ? null : setup?.Battlefields?.GetValueOrDefault(viewer),
            viewer is null ? null : setup?.Sideboards?.GetValueOrDefault(viewer));
    }

    // CN tournament 403.3–4, 401.2.a and 601.1: only equal-size main/sideboard
    // exchanges; rune, legend, extra registered roles and battlefield lists stay fixed.
    internal static IReadOnlyList<string> SubmissionErrors(MatchState state, string player,
        OfficialDecklist deck, IReadOnlyList<string> sideboard, OfficialCardCatalog catalog)
    {
        if (!Enabled(state)) return sideboard.Count == 0 ? [] : ["当前开局规则不支持登记备牌。"];
        if (deck.MainDeck.Count != 40 || sideboard.Count > 10) return ["城市挑战赛构筑主牌堆须为 40 张，备牌至多 10 张。"];
        var combined = OfficialDeckValidator.Validate(deck with { MainDeck = deck.MainDeck.Concat(sideboard).ToArray() },
            catalog, state.SessionOptions!.DeckFormat);
        if (!combined.IsValid) return combined.Errors;
        if (state.CityOpening is not { FirstPlayerId: not null } setup) return [];
        if (!setup.Registrations.TryGetValue(player, out var original)
            || !SamePool(original.Deck.MainDeck.Concat(original.Sideboard), deck.MainDeck.Concat(sideboard))
            || !FixedRolesEqual(original.Deck, deck) || sideboard.Count != original.Sideboard.Count)
            return ["备牌只能与已登记主牌堆等量交换，不能改变符文、传奇或战场。"];
        return [];
    }

    internal static bool FixedRolesEqual(OfficialDecklist original, OfficialDecklist deck)
        => original.LegendCardNo == deck.LegendCardNo && original.RuneDeck.SequenceEqual(deck.RuneDeck)
            && original.Battlefields.SequenceEqual(deck.Battlefields)
            && (original.AdditionalLegendCardNos ?? []).SequenceEqual(deck.AdditionalLegendCardNos ?? []);

    internal static bool SamePool(IEnumerable<string> left, IEnumerable<string> right)
        => left.Order(StringComparer.Ordinal).SequenceEqual(right.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    internal static IReadOnlyDictionary<string, int>? SideboardCounts(MatchState state)
        => Enabled(state) && state.CityOpening is { } setup
            ? setup.Sideboards.ToDictionary(x => x.Key, x => x.Value.Count, StringComparer.Ordinal) : null;

    public static IReadOnlyList<string> Validate(MatchState state)
    {
        try
        {
            if (OptionError(state.SessionOptions ?? MatchSessionOptions.Default) is { } option) return [option];
            if (!Enabled(state)) return state.CityOpening is null ? [] : ["city setup outside its opening rule"];
            if (state.CityOpening is not { } setup)
                return state.PlayerDecklists.Count == 0 ? [] : ["city deck registration is missing"];
            if (setup.Registrations is null || setup.Sideboards is null
                || !setup.Registrations.Keys.Order().SequenceEqual(state.PlayerDecklists.Keys.Order())
                || !setup.Sideboards.Keys.Order().SequenceEqual(setup.Registrations.Keys.Order())
                || setup.Registrations.Keys.Any(p => !state.Seats.ContainsKey(p))) return ["invalid city registrations"];
            foreach (var (player, registration) in setup.Registrations)
            {
                if (registration is null || registration.Deck is null || registration.Sideboard is null
                    || setup.Sideboards[player] is null
                    || SubmissionErrors(state, player, registration.Deck, registration.Sideboard, Catalog.Value).Count > 0
                    || SubmissionErrors(state, player, state.PlayerDecklists[player], setup.Sideboards[player], Catalog.Value).Count > 0)
                    return ["invalid city registered deck or sideboard pool"];
            }
            if (setup.FirstPlayerId is null)
                return setup.Registrations.Count < 2 && setup.SecondPlayerId is null && setup.Battlefields is null
                    && setup.SelectionCursor is null && state.ReadyPlayerIds.Count == 0
                    && state.Status is MatchStatuses.Seating or MatchStatuses.Finished
                    ? [] : ["invalid city pre-selection stage"];
            if (state.Seats.Count != 2 || setup.Registrations.Count != 2 || setup.SelectionCursor is not { } cursor
                || cursor < 0 || cursor > state.RngCursor || setup.Battlefields is null) return ["invalid city selected opening"];
            var expected = MatchSession.SelectCityOpening(state, cursor);
            if (expected.First != setup.FirstPlayerId || expected.Second != setup.SecondPlayerId
                || !expected.Fields.OrderBy(x => x.Key).SequenceEqual(setup.Battlefields.OrderBy(x => x.Key))
                || expected.Cursor > state.RngCursor
                || state.OpeningSecondActionPlayerId is not null
                    && state.OpeningSecondActionPlayerId != setup.SecondPlayerId)
                return ["city play order or battlefield selection disagrees with its original random stream"];
            if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.OpeningInsightWindow } insight
                && !CoreRuleEngine.ValidCityInsightChoice(state, insight)) return ["invalid city private Insight continuation"];
            return [];
        }
        catch (Exception e) when (e is NullReferenceException or ArgumentException or InvalidOperationException
            or KeyNotFoundException or OverflowException) { return ["malformed city opening state"]; }
    }
}

public sealed partial class MatchSession
{
    public string OpeningRule => CityOpeningRules.Rule(state);

    private static MatchState RegisterCityDeck(MatchState state, string player, OfficialDecklist deck,
        IReadOnlyList<string> sideboard, List<GameEvent> events)
    {
        if (!CityOpeningRules.Enabled(state)) return state;
        var current = state.CityOpening ?? new(new Dictionary<string, CityDeckRegistration>(),
            new Dictionary<string, IReadOnlyList<string>>());
        var registrations = current.Registrations.ToDictionary();
        var sideboards = current.Sideboards.ToDictionary();
        sideboards[player] = sideboard.ToArray();
        if (current.FirstPlayerId is null) registrations[player] = new(deck, sideboard.ToArray());
        current = current with { Registrations = registrations, Sideboards = sideboards };
        if (current.FirstPlayerId is null && registrations.Count == 2)
        {
            var selection = SelectCityOpening(state with { CityOpening = current }, state.RngCursor);
            current = current with { FirstPlayerId = selection.First, SecondPlayerId = selection.Second,
                Battlefields = selection.Fields, SelectionCursor = state.RngCursor };
            state = state with { RngCursor = selection.Cursor, ActivePlayerId = selection.First, TurnPlayerId = selection.First };
            events.Add(new("S5_PLAY_ORDER_DETERMINED", "测试开局先后手已确定", new Dictionary<string, object?>
                { ["firstPlayerId"] = selection.First, ["secondPlayerId"] = selection.Second }));
            events.Add(new("S5_BATTLEFIELDS_SELECTED", "双方已随机选定战场，各自私下查看后更换备牌",
                new Dictionary<string, object?> { ["count"] = 2 }));
        }
        return state with { CityOpening = current };
    }

    internal static (string First, string Second, Dictionary<string, string> Fields, long Cursor) SelectCityOpening(
        MatchState state, long cursor)
    {
        var players = state.Seats.OrderBy(x => x.Value, StringComparer.Ordinal).Select(x => x.Key).ToArray();
        var order = RandomizeStable(players, state.Seed, cursor++, "TURN_ORDER");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var player in players)
        {
            var choices = state.CityOpening!.Registrations[player].Deck.Battlefields;
            fields[player] = RandomizeStable(choices, state.Seed, cursor, $"{player}:BATTLEFIELDS")[0];
            if (choices.Count > 1) cursor++;
        }
        return (order[0], order[1], fields, cursor);
    }
}
