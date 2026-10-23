namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateOrdinaryPrivatePlaySkillSnapshot(
        RecoveredPlayerView view, MatchState state, List<string> errors)
    {
        if (view.SnapshotTick != state.Tick || view.Snapshot.Stack is null) return;
        foreach (var skill in state.StackItems.Where(i => i.SourceConfirmed && i.PermanentPlaySource is null))
        {
            // This expectation comes from authoritative zones and disclosure
            // permissions, never from the production snapshot being validated.
            var privateOwners = state.PlayerZones.Where(p => p.Value.Hand.Contains(skill.SourceObjectId)
                || p.Value.MainDeck.Contains(skill.SourceObjectId) || p.Value.RuneDeck.Contains(skill.SourceObjectId))
                .Select(p => p.Key).ToArray();
            if (privateOwners.Length == 0) continue;
            var hidden = privateOwners.Any(p => p != view.PlayerId)
                && !CoreRuleEngine.CanViewObservedDeckCard(state, skill.SourceObjectId, view.PlayerId)
                && !CoreRuleEngine.IsPubliclyRevealedHandCard(state, skill.SourceObjectId)
                && !CoreRuleEngine.IsPubliclyRevealedDeckCard(state, skill.SourceObjectId);
            var rows = view.Snapshot.Stack.Where(row => TryReadObjectString(row, "stackItemId", out var id)
                && id == skill.StackItemId).ToArray();
            if (rows.Length != 1) {
                errors.Add($"snapshot for {view.PlayerId} ordinary play skill {skill.StackItemId} requires one exact stack row");
                continue;
            }
            var payload = rows[0];
            void Require(string key, string value)
            {
                if (!TryReadObjectString(payload, key, out var actual) || actual != value)
                    errors.Add($"snapshot for {view.PlayerId} ordinary play skill {skill.StackItemId} {key} disagrees with authoritative public skill");
            }
            Require("sourceObjectId", hidden ? "HIDDEN" : skill.SourceObjectId);
            Require("sourceVisibility", hidden ? "HIDDEN" : "VISIBLE");
            Require("cardNo", skill.CardNo);
            Require("effectKind", skill.EffectKind);
            Require("controllerId", skill.ControllerId);
            if (!TryReadObjectBool(payload, "playAbility", out var playAbility) || !playAbility)
                errors.Add($"snapshot for {view.PlayerId} ordinary play skill {skill.StackItemId} must retain playAbility");
        }
    }
}
