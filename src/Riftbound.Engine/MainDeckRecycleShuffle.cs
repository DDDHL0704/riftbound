using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public enum MainDeckRecycleShuffleVersion
{
    LegacyHashSort,
    FisherYates
}

public sealed partial record MatchState
{
    // Missing in older recovery data. Omit the default to preserve historical state hashes.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public MainDeckRecycleShuffleVersion RecycleShuffleVersion { get; init; }
}

internal static class MainDeckRecycleShuffle
{
    internal static IReadOnlyList<string> Shuffle(MatchState state, IReadOnlyList<string> cardIds, long rngCursor, string sourceObjectId)
    {
        if (cardIds.Count <= 1) return cardIds.ToArray();

        return state.RecycleShuffleVersion switch
        {
            MainDeckRecycleShuffleVersion.LegacyHashSort => cardIds
                .Select((id, index) => (Id: id, Index: index, Order: LegacyHash($"{state.Seed}:{rngCursor}:{sourceObjectId}:{id}:{index}")))
                .OrderBy(entry => entry.Order)
                .ThenBy(entry => entry.Index)
                .Select(entry => entry.Id)
                .ToArray(),
            MainDeckRecycleShuffleVersion.FisherYates => DeterministicShuffle.Shuffle(
                cardIds.Order(StringComparer.Ordinal).ToArray(), state.Seed, rngCursor, "main-deck-recycle-v1", sourceObjectId),
            _ => throw new InvalidOperationException("Unknown main-deck recycle shuffle version.")
        };
    }

    internal static IReadOnlyList<string> EventCardIds(MatchState state, IReadOnlyList<string> shuffledIds)
        => state.RecycleShuffleVersion == MainDeckRecycleShuffleVersion.LegacyHashSort
            ? shuffledIds
            : shuffledIds.Order(StringComparer.Ordinal).ToArray();

    private static ulong LegacyHash(string value)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }
        return hash;
    }
}
