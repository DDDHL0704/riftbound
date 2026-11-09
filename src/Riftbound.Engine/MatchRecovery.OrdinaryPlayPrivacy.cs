namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidatePrivateSourceSkillSnapshot(
        RecoveredPlayerView view, MatchState state, List<string> errors)
    {
        if (view.SnapshotTick != state.Tick) return;
        foreach (var skill in state.StackItems.Where(i => i.TimingAlias is not null || i.UnitEvent?.Origin?.ArmamentGranted == true || i.StandbyObserver is not null
            || i.SourceConfirmed && (i.PermanentPlaySource is null || CoreRuleEngine.IsEquipmentTheftPlaySkill(i))
                && !CoreRuleEngine.HasHistoricalPublicDamageSource(state, i)))
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
            var rows = (view.Snapshot.Stack ?? []).Where(row => TryReadObjectString(row, "stackItemId", out var id)
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
            if (skill.TimingAlias is null && skill.UnitEvent?.Origin?.ArmamentGranted != true && skill.StandbyObserver is null
                && (!TryReadObjectBool(payload, "playAbility", out var playAbility) || !playAbility))
                errors.Add($"snapshot for {view.PlayerId} ordinary play skill {skill.StackItemId} must retain playAbility");
        }
    }
}
