using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed partial record MatchState
{
    // Private per-game continuation for tournament 602.4.a.5.b. An absent role
    // leaves its zone empty; it never creates a placeholder card object.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? OpeningRoleDrawPendingPlayerIds { get; init; }
}
