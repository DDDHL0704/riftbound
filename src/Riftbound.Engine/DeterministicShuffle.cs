using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Riftbound.Engine;

internal static class DeterministicShuffle
{
    internal static IReadOnlyList<T> Shuffle<T>(IReadOnlyList<T> values, long seed, long rngCursor, string domain, string scope)
    {
        var shuffled = values.ToArray();
        ulong sampleIndex = 0;
        ulong NextSample()
        {
            var input = FormattableString.Invariant($"{domain}:{seed}:{rngCursor}:{scope}:{sampleIndex++}");
            return BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        }
        for (var index = shuffled.Length - 1; index > 0; index--)
        {
            var selected = NextIndex(index + 1, NextSample);
            (shuffled[index], shuffled[selected]) = (shuffled[selected], shuffled[index]);
        }
        return shuffled;
    }

    internal static int NextIndex(int exclusiveMax, Func<ulong> nextSample)
    {
        // Reject the incomplete bucket so every index has the same probability.
        var bound = (ulong)exclusiveMax;
        var threshold = unchecked(0UL - bound) % bound;
        ulong sample;
        do { sample = nextSample(); } while (sample < threshold);
        return (int)(sample % bound);
    }
}
