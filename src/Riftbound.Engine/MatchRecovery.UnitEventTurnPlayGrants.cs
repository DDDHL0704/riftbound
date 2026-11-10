namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateUnitEventTurnPlayHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var frames = new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>().ToArray();
        if (!frames.Any(frame => CoreRuleEngine.UnitEventTurnPlayGrants(frame).Length > 0
                || CoreRuleEngine.UnitEventTurnPlaySkills(frame).Length > 0)
            && !events.Any(e => e.Event.Kind.StartsWith("UNIT_EVENT_TURN_PLAY_", StringComparison.Ordinal))) return;
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        bool Accepted(RecoveredEvent record) => record.Sequence > boundary && commands.Any(command => command.Accepted
            && command.CompletedTick == record.Tick && command.StartedEventSequence < record.Sequence
            && command.CompletedEventSequence >= record.Sequence);
        var accepted = events.Where(Accepted).OrderBy(e => e.Sequence).ToArray();
        foreach (var frame in frames)
        {
            var grants = CoreRuleEngine.UnitEventTurnPlayGrants(frame);
            var uses = CoreRuleEngine.UnitEventTurnPlayGrantUses(frame);
            var skills = CoreRuleEngine.UnitEventTurnPlaySkills(frame);
            var expectedGrants = new HashSet<string>(StringComparer.Ordinal);
            var expectedUses = new HashSet<string>(StringComparer.Ordinal);
            var usedGrants = new HashSet<string>(StringComparer.Ordinal);
            var expectedSkills = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in accepted.Where(e => e.Tick <= frame.Tick
                && TryReadObjectInt(e.Event.Payload, "turnNumber", out var turn) && turn == frame.TurnNumber))
            {
                var ev = record.Event;
                if (ev.Kind == "UNIT_EVENT_TURN_PLAY_GRANT_CREATED")
                {
                    if (!TryReadObjectString(ev.Payload, "grantHash", out var hash) || hash is null || !expectedGrants.Add(hash))
                        errors.Add("unit event turn play grant has an invalid original creation");
                }
                else if (ev.Kind == "UNIT_EVENT_TURN_PLAY_GRANT_USED")
                {
                    if (!TryReadObjectString(ev.Payload, "useHash", out var hash) || hash is null || !expectedUses.Add(hash)
                        || !TryReadObjectString(ev.Payload, "grantHash", out var grant) || grant is null
                        || !expectedGrants.Contains(grant) || !usedGrants.Add(grant))
                        errors.Add("unit event turn play grant has an invalid original consumption");
                }
                else if (ev.Kind == "UNIT_EVENT_TURN_PLAY_SKILL_CAPTURED")
                {
                    if (!TryReadObjectString(ev.Payload, "skillHash", out var hash) || hash is null || !expectedSkills.Add(hash))
                        errors.Add("unit event turn play skill has an invalid original capture");
                }
                else if (ev.Kind == "UNIT_EVENT_TURN_PLAY_SKILL_RETIRED")
                {
                    if (!TryReadObjectString(ev.Payload, "skillHash", out var hash) || hash is null || !expectedSkills.Remove(hash))
                        errors.Add("unit event turn play skill has an invalid original retirement");
                }
            }
            if (!expectedGrants.SetEquals(grants.Select(g => MatchStateHasher.HashValue(g)))
                || grants.Length != expectedGrants.Count)
                errors.Add("unit event turn play grants differ from original accepted history");
            if (!expectedUses.SetEquals(uses.Select(use => MatchStateHasher.HashValue(use))) || uses.Length != expectedUses.Count)
                errors.Add("unit event turn play uses differ from original accepted history");
            if (!expectedSkills.SetEquals(skills.Select(skill => MatchStateHasher.HashValue(skill))))
                errors.Add("unit event turn play skills differ from original accepted history");
            foreach (var skill in skills)
                if (!expectedUses.Contains(MatchStateHasher.HashValue(CoreRuleEngine.UnitEventTurnPlaySkillUse(skill.Context))))
                    errors.Add("unit event turn play skill differs from its original consumed unit");
            foreach (var grant in grants.Concat(skills.Select(skill => skill.Context.Grant)).DistinctBy(g => g.Marker))
            {
                if (grant.UnitEventOrigin is not { Context: { } context } origin) continue;
                var id = grant.StackItemId.StartsWith("ordered-", StringComparison.Ordinal) ? grant.StackItemId[8..] : "";
                var captures = accepted.Where(record => record.Tick <= frame.Tick && record.Event.Kind == "TRIGGER_QUEUED"
                    && TryReadObjectString(record.Event.Payload, "triggerId", out var trigger) && trigger == id
                    && UnitEventQueuedSourceMatches(record.Event.Payload, grant.Source.ObjectId)
                    && TryReadObjectString(record.Event.Payload, "controllerId", out var controller) && controller == grant.ControllerId
                    && TryReadObjectString(record.Event.Payload, "effectKind", out var effect) && effect == CoreRuleEngine.UnitEventEffect
                    && TryReadObjectString(record.Event.Payload, "timingCaptureHash", out var hash)
                        && hash == CoreRuleEngine.TimingCaptureHash(context, origin.TimingAlias)).Take(2).ToArray();
                if (captures.Length != 1 || !accepted.Any(record => record.Tick <= frame.Tick
                    && record.Sequence > captures[0].Sequence && record.Event.Kind == "TRIGGERS_MOVED_TO_STACK"
                    && TryReadObjectStringList(record.Event.Payload, "stackItemIds", out var ids) && ids.Contains(grant.StackItemId)))
                    errors.Add("unit event turn play grant differs from its original earned source skill");
                if (!frame.CardObjects.ContainsKey(grant.Source.ObjectId) && context.TokenFactoryCardNo is not null
                    && !accepted.Any(record => record.Tick <= frame.Tick && record.Tick >= context.CapturedTick
                        && record.Event.Kind == "TOKEN_CEASED_TO_EXIST"
                        && TryReadObjectString(record.Event.Payload, "targetObjectId", out var target) && target == grant.Source.ObjectId))
                    errors.Add("unit event turn play source disappearance lacks its original token cessation");
            }
        }
    }

    private static bool HasValidatedCeasedUnitEventTurnPlaySource(MatchState? state, string? id,
        string? controller, string? source, string? effect)
    {
        if (state is null || string.IsNullOrWhiteSpace(source) || source == "HIDDEN"
            || state.CardObjects is null || state.CardObjects.ContainsKey(source)) return false;
        var skill = CoreRuleEngine.UnitEventTurnPlaySkills(state).FirstOrDefault(skill =>
            (skill.Id == id || "ordered-" + skill.Id == id) && skill.Controller == controller && skill.Source == source);
        return skill?.Context.Grant.UnitEventOrigin?.Context?.TokenFactoryCardNo is { } factory
            && P6TokenFactoryCatalog.IsTokenFactory(factory)
            && CoreRuleEngine.ValidTurnUnitPlayBoon(state, skill.Context, effect!, source, controller!, skill.Context.Grant.CardNo);
    }
}
