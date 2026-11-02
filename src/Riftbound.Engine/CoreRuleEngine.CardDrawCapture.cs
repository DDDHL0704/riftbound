using Riftbound.Contracts;

namespace Riftbound.Engine;

// An instruction-local observation. It is consumed before publishing events,
// and contains counts rather than identities of newly drawn or existing hand cards.
internal sealed record CapturedDrawInstruction(string PlayerId, int DrawnCount, int OtherHandCount,
    IReadOnlyList<CapturedDrawSource> Sources)
{
    internal IReadOnlyList<DrawHandThresholdContext> ThresholdSources { get; init; } = [];
}

public sealed partial class CoreRuleEngine
{
    private const string DrawInstructionCaptureKey = "drawInstructionCapture";

    private static CapturedDrawInstruction CaptureDrawInstruction(MatchState state, string player,
        int drawnCount, int otherHandCount)
    {
        if (drawnCount < 0 || otherHandCount < 0)
            throw new InvalidOperationException("Invalid draw instruction counts.");
        return new(player, drawnCount, otherHandCount, CaptureDrawTriggerSources(state, player))
        { ThresholdSources = CaptureDrawHandThresholdSources(state, player, drawnCount, otherHandCount) };
    }

    private static CapturedDrawInstruction? CapturedDraw(GameEvent ev, string player, int amount)
    {
        if (!ev.Payload.TryGetValue(DrawInstructionCaptureKey, out var payload)) return null;
        if (payload is not CapturedDrawInstruction capture || capture.PlayerId != player
            || capture.DrawnCount != amount || capture.OtherHandCount < 0)
            throw new InvalidOperationException("Draw event differs from its instruction capture.");
        return capture;
    }
}
