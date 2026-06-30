using System.Collections.Generic;

namespace Riftbound.GodotClient;

public sealed record PreconstructedDeck(
    string Id,
    string Name,
    string Description,
    string LegendCardNo,
    string ChampionCardNo,
    IReadOnlyList<string> MainDeck,
    IReadOnlyList<string> RuneDeck,
    IReadOnlyList<string> Battlefields);
