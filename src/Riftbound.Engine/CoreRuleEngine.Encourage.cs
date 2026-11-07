namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 721.2 / 727.1.b / 812.1: an inactive dependency cannot be
    // activated. The other card must have been finalized by this controller.
    internal static bool EncourageActive(MatchState state, string player, string source)
        => state.CardPlays.TurnNumber == state.TurnNumber && state.CardPlays.Finalized
            .Any(play => play.PlayerId == player && play.Source.ObjectId != source);
}
