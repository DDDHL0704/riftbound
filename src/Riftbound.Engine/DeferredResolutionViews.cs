using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Riftbound.Engine;

// A resolution pipeline may advance through several states that it never
// publishes. Each view keeps the state selected by its original caller.
internal static class DeferredResolutionViews
{
    private static readonly AsyncLocal<bool> Defer = new();

    internal static Scope DuringResolution() => new(Defer.Value);

    internal static IReadOnlyDictionary<string, T> Build<T>(
        MatchState state, Func<MatchState, IReadOnlyDictionary<string, T>> build)
        => Defer.Value ? new DeferredView<T>(state, build) : build(state);

    internal static ResolutionResult Materialize(ResolutionResult result)
    {
        var previous = Defer.Value;
        Defer.Value = false;
        try
        {
            return result with
            {
                Snapshots = Materialize(result.Snapshots),
                Prompts = Materialize(result.Prompts)
            };
        }
        finally
        {
            Defer.Value = previous;
        }
    }

    private static IReadOnlyDictionary<string, T> Materialize<T>(IReadOnlyDictionary<string, T> view)
        => view is DeferredView<T> deferred ? deferred.Value : view;

    internal readonly struct Scope : IDisposable
    {
        private readonly bool previous;

        internal Scope(bool previous)
        {
            this.previous = previous;
            Defer.Value = true;
        }

        public void Dispose() => Defer.Value = previous;
    }

    private sealed class DeferredView<T>(MatchState state, Func<MatchState, IReadOnlyDictionary<string, T>> build)
        : IReadOnlyDictionary<string, T>
    {
        private readonly Lazy<IReadOnlyDictionary<string, T>> projection = new(() => build(state));
        internal IReadOnlyDictionary<string, T> Value => projection.Value;

        public int Count => Value.Count;
        public IEnumerable<string> Keys => Value.Keys;
        public IEnumerable<T> Values => Value.Values;
        public T this[string key] => Value[key];
        public bool ContainsKey(string key) => Value.ContainsKey(key);
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out T value) => Value.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, T>> GetEnumerator() => Value.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
