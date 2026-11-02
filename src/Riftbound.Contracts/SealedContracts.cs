using System.Text.Json.Serialization;

namespace Riftbound.Contracts;

public static class MatchFormats
{
    public const string Constructed = "constructed";
    public const string Sealed = "sealed";
}

public sealed record CreatePublicMatchOptionsDto(
    string Format = MatchFormats.Constructed,
    string? SetCode = null,
    int BestOf = 1);

public sealed record SealedSetDto(
    string SetCode,
    string Name);

public sealed record SealedPackCardDto(
    string CardNo,
    string Slot,
    bool Foil);

public sealed record SealedPackDto(
    IReadOnlyList<SealedPackCardDto> Cards);

public sealed record SealedPoolCardDto(
    string CardNo,
    int Count);

public sealed record SealedPoolDto(
    string SetCode,
    IReadOnlyList<SealedPackDto> Packs,
    IReadOnlyList<SealedPoolCardDto> Pool);

public sealed record SealedLockedDeckDto(
    string LegendCardNo,
    string ChampionCardNo,
    IReadOnlyList<string> MainDeck,
    IReadOnlyList<string> RuneDeck,
    IReadOnlyList<string> Battlefields);

public sealed record MatchSeriesDto(
    int BestOf,
    int GameNumber,
    IReadOnlyDictionary<string, int> Wins,
    string Phase,
    IReadOnlyDictionary<string, IReadOnlyList<string>> UsedBattlefields,
    string? PlayOrderChooserPlayerId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SealedLockedDeckDto? LockedDeck = null);

public static class MatchSeriesPhases
{
    public const string ChoosingPlayOrder = "choosing_play_order";
    public const string SubmittingDecks = "submitting_decks";
    public const string Playing = "playing";
    public const string Finished = "finished";
}
