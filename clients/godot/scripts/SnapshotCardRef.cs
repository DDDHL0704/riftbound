namespace Riftbound.GodotClient;

public sealed record SnapshotCardRef(
    string ObjectId,
    string CardNo,
    bool Visible,
    bool FaceDown);
