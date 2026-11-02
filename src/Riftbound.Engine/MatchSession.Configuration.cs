using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed partial record MatchState
{
    // The implicit configuration is the historical CoreRules/default room.
    // Nondefault room rules travel with the private authoritative checkpoint.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MatchSessionOptions? SessionOptions { get; init; }
}
