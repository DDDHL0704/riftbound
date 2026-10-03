using System;
using System.Linq;
using System.Text.Json;

namespace Riftbound.GodotClient;

public sealed record SnapshotCardRef(
    string ObjectId,
    string CardNo,
    bool Visible,
    bool FaceDown,
    string ControllerOrOwner = "",
    bool IsExhausted = false,
    int? CurrentPower = null,
    int Damage = 0)
{
    public static SnapshotCardRef FromSnapshot(string objectId, JsonElement card, string viewerPlayerId)
    {
        if (card.ValueKind != JsonValueKind.Object) return new(objectId, "", false, true);
        var faceDown = Bool("isFaceDown");
        var controller = Text("controllerId");
        if (string.IsNullOrWhiteSpace(controller)) controller = Text("ownerId");
        // A known own standby may be inspected. A foreign face-down object is
        // redacted defensively even if a malformed snapshot includes its identity.
        var known = !faceDown || (!string.IsNullOrWhiteSpace(viewerPlayerId)
            && string.Equals(controller, viewerPlayerId, StringComparison.Ordinal));
        var cardNo = known ? Text("cardNo") : "";
        var fieldUnit = !faceDown && card.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array
            && tags.EnumerateArray().Any(tag => tag.ValueKind == JsonValueKind.String && tag.GetString() == "CARD_TYPE:UNIT")
            && card.TryGetProperty("location", out var location) && location.ValueKind == JsonValueKind.Object
            && location.TryGetProperty("zone", out var zone) && zone.ValueKind == JsonValueKind.String
            && zone.GetString() is "BASE" or "BATTLEFIELD";
        int? power = fieldUnit && card.TryGetProperty("effectivePower", out var value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
        var damage = fieldUnit && card.TryGetProperty("damage", out var damageValue)
            && damageValue.ValueKind == JsonValueKind.Number && damageValue.TryGetInt32(out var amount) ? amount : 0;
        return new(objectId, cardNo, !string.IsNullOrWhiteSpace(cardNo), faceDown, controller, Bool("isExhausted"), power, damage);

        string Text(string key) => card.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";
        bool Bool(string key) => card.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    }
}
