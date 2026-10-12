namespace Riftbound.Engine;

public sealed record CopyFaceTransition(long Tick, long Generation, string FromCardNo, string ToCardNo);

// Copying changes characteristics of the same object. Keep the preceding face
// so an earlier text acquisition can be checked without consulting today's face.
internal static class CopyFaceTimeline
{
    internal static CardObjectState ChangeFace(CardObjectState card, string face, long tick)
    {
        var previous = AttachedRuleTextRules.Face(card)!;
        if (previous == face) return card;
        return card with { CardNo = face, CopyFaceTransitions = (card.CopyFaceTransitions ?? [])
            .Append(new CopyFaceTransition(tick, card.ObjectGeneration, previous, face)).ToArray() };
    }

    internal static bool Valid(MatchState state, CardObjectState card)
    {
        if (card.CopyFaceTransitions is null) return card.TokenFactoryCardNo is null
            || card.TokenFactoryCardNo == AttachedRuleTextRules.Face(card);
        if (card.CopyFaceTransitions.Count == 0) return false;
        if (card.TokenFactoryCardNo is { } intrinsic
            && card.CopyFaceTransitions[0]?.FromCardNo != intrinsic) return false;
        string? preceding = null; long tick = -1;
        foreach (var frame in card.CopyFaceTransitions) {
            if (frame is null || frame.Tick < tick || frame.Tick > state.Tick || frame.Generation != card.ObjectGeneration
                || !CopyCharacteristics.HasFace(frame.FromCardNo) || !CopyCharacteristics.HasFace(frame.ToCardNo)
                || frame.FromCardNo == frame.ToCardNo || preceding is not null && frame.FromCardNo != preceding) return false;
            tick = frame.Tick; preceding = frame.ToCardNo;
        }
        return preceding == AttachedRuleTextRules.Face(card);
    }

    internal static string? FaceAt(CardObjectState card, long? revision, long? acquiredTick)
    {
        if (revision is not >= 0 || acquiredTick is not >= 0) return null;
        var transitions = card.CopyFaceTransitions;
        if (transitions is null) return revision == 0 ? AttachedRuleTextRules.Face(card) : null;
        if (transitions.Count == 0 || transitions.Any(frame => frame is null)) return null;
        if (revision > transitions.Count) return null;
        var index = (int)revision.Value;
        if (index > 0 && transitions[index - 1].Tick > acquiredTick
            || index < transitions.Count && transitions[index].Tick < acquiredTick) return null;
        return index == 0 ? transitions[0].FromCardNo : transitions[index - 1].ToCardNo;
    }
}
