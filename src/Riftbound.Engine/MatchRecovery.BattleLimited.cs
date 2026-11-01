namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateBattleLimitedHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        bool Accepted(RecoveredEvent e) => commands.Any(c => c.Accepted && c.CompletedTick == e.Tick
            && c.StartedEventSequence < e.Sequence && e.Sequence <= c.CompletedEventSequence);
        RecoveredEvent? RoleEvent(long tick, int order, string field, string attacker)
        {
            var matching = events.Where(e => e.Tick == tick && e.Order == order
                && e.Event.Kind is "BATTLE_ROLES_ESTABLISHED" or "BATTLE_ROLE_ACQUIRED"
                && TryReadObjectString(e.Event.Payload, "battlefieldId", out var id) && id == field
                && TryReadObjectString(e.Event.Payload, "attackingPlayerId", out var player) && player == attacker).Take(2).ToArray();
            return matching.Length == 1 ? matching[0] : null;
        }
        foreach (var text in state.UntilEndOfTurnEffects ?? []) {
            if (BattleLimitedMarkers.TryScope(text, out var scope) && scope is not null) {
                var birth = RoleEvent(scope.StartedAtTick, scope.RoleEventOrder, scope.BattlefieldId, scope.AttackingPlayerId);
                if (birth is null || birth.Event.Kind != "BATTLE_ROLES_ESTABLISHED" || !Accepted(birth))
                    errors.Add("battle scope has no matching accepted initial role event");
            }
            if (!BattleLimitedMarkers.TryRole(text, out var role) || role?.Scope is null) continue;
            var acquired = RoleEvent(role.AcquiredAtTick, role.RoleEventOrder, role.Scope.BattlefieldId, role.Scope.AttackingPlayerId);
            if (acquired is null || !Accepted(acquired) || role.Role == "PLAYER_DEFENDER"
                    && (role.AcquiredAtTick != role.Scope.StartedAtTick || role.RoleEventOrder != role.Scope.RoleEventOrder)
                || role.Role != "PLAYER_DEFENDER" && (!TryReadObjectStringList(acquired?.Event.Payload,
                    role.Role == "ATTACKER" ? "attackerObjectIds" : "defenderObjectIds", out var ids) || !ids.Contains(role.SourceObjectId)))
                errors.Add("battle first-role memo has no matching accepted acquisition event");
        }
        foreach (var card in state.CardObjects.Values)
            foreach (var text in card.UntilEndOfTurnEffects) {
                if (!BattleLimitedMarkers.TryGrant(text, out var grant) || grant?.Context is null) continue;
                var self = grant.Context.Kind is "BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND";
                var births = events.Where(e => e.Event.Kind == (self ? "UNIT_POWER_CHANGED" : "BATTLEFIELD_TRIGGER_RESOLVED")
                    && TryReadObjectString(e.Event.Payload, "stackItemId", out var stack) && stack == grant.StackItemId
                    && TryReadObjectString(e.Event.Payload, "duration", out var duration) && duration == "UNTIL_END_OF_BATTLE"
                    && TryReadObjectString(e.Event.Payload, "targetObjectId", out var target) && target == grant.TargetObjectId
                    && TryReadObjectString(e.Event.Payload, self ? "sourceObjectId" : "battlefieldObjectId", out var source) && source == grant.SourceObjectId).ToArray();
                if (births.Length != 1 || !Accepted(births[0]) || self && (!TryReadObjectInt(births[0].Event.Payload, "snapshotMight", out var snapshot)
                    || snapshot != grant.SnapshotMight || !TryReadObjectInt(births[0].Event.Payload, "powerDelta", out var delta) || delta != grant.SnapshotMight)
                    || !self && (!TryReadObjectString(births[0].Event.Payload, "battlefieldCardNo", out var no)
                        || !TryReadObjectString(births[0].Event.Payload, "keyword", out var keyword)
                        || !TryReadObjectInt(births[0].Event.Payload, "keywordBonus", out var amount)
                        || !BattleLimitedMarkers.Definition(no ?? "", grant.Context.Kind)
                        || !BattlefieldTriggerSpecRules.TryGetTrigger(no, t =>
                            grant.Context.Kind == BattleLimitedMarkers.BulwarkDefend
                                ? BattlefieldTriggerSpecRules.IsBattlefieldDefendGrantBulwarkTrigger(t)
                                : BattlefieldTriggerSpecRules.IsBattlefieldDefendGrantSteadfastTrigger(t), out var expected)
                        || keyword != expected.GrantedKeyword || amount != expected.KeywordBonus))
                    errors.Add("battle grant has no matching accepted resolution event");
            }
    }

    private static bool BattleMarkerFamily(string? text) => text?.StartsWith("BATTLE_LIMITED_", StringComparison.Ordinal) == true
        || text?.StartsWith("BATTLE_ROLE_", StringComparison.Ordinal) == true;

    // Decode the private wire format, then independently check its meaning and
    // its exact ledger/keyword pair. Original action replay still proves birth.
    private static void ValidateBattleLimitedMarkers(MatchState state, List<string> errors)
    {
        var scopes = new List<BattleLimitedScope>(); var roles = new List<BattleFirstRole>();
        foreach (var text in (state.UntilEndOfTurnEffects ?? []).Where(BattleMarkerFamily)) {
            if (BattleLimitedMarkers.TryScope(text, out var scope) && scope is not null) scopes.Add(scope);
            else if (BattleLimitedMarkers.TryRole(text, out var role) && role?.Scope is not null) roles.Add(role);
            else errors.Add("invalid or misplaced private battle marker");
        }
        if (scopes.GroupBy(s => s.BattlefieldId).Any(g => g.Count() != 1)) errors.Add("duplicate private battle scope");
        foreach (var scope in scopes) {
            if (string.IsNullOrWhiteSpace(scope.BattlefieldId) || scope.StartedAtTick < 0 || scope.StartedAtTick > state.Tick
                || scope.RoleEventOrder < 0 || !state.Seats.ContainsKey(scope.AttackingPlayerId ?? "")
                || !state.Seats.ContainsKey(scope.DefendingPlayerId ?? "") || scope.AttackingPlayerId == scope.DefendingPlayerId
                || !state.CardObjects.TryGetValue(scope.BattlefieldId, out var field) || !state.BattlefieldStates.ContainsKey(scope.BattlefieldId)
                || !(state.BattleState.IsActive && state.BattleState.BattlefieldObjectId == scope.BattlefieldId
                    || CoreRuleEngine.IsInitialBattleDuel(state, out var battle) && battle.BattlefieldId == scope.BattlefieldId
                    || state.PendingBattleResult?.BattlefieldId == scope.BattlefieldId))
                errors.Add("invalid private battle scope lifetime or players");
            if (CoreRuleEngine.IsInitialBattleDuel(state, out var declared) && declared.BattlefieldId == scope.BattlefieldId
                && declared.InitialAttackingPlayerId != scope.AttackingPlayerId
                || state.PendingBattleResult is { } result && result.BattlefieldId == scope.BattlefieldId
                    && (result.AttackingPlayerId != scope.AttackingPlayerId || result.DefendingPlayerId != scope.DefendingPlayerId))
                errors.Add("private battle scope disagrees with captured battle players");
        }
        if (roles.GroupBy(r => (r.Scope, r.SourceObjectId, r.SourceGeneration, r.Role)).Any(g => g.Count() != 1))
            errors.Add("duplicate private battle first-role identity");
        foreach (var scope in scopes) {
            var initial = roles.Where(r => r.Scope == scope && r.AcquiredAtTick == scope.StartedAtTick
                && r.RoleEventOrder == scope.RoleEventOrder && r.Role is "ATTACKER" or "DEFENDER").ToArray();
            var oneOnOne = initial.Count(r => r.Role == "ATTACKER") == 1 && initial.Count(r => r.Role == "DEFENDER") == 1;
            if (!initial.Any(r => r.Role == "ATTACKER") || !initial.Any(r => r.Role == "DEFENDER")
                || initial.Any(r => r.OneOnOne != oneOnOne)) errors.Add("initial battle one-on-one memo disagrees with first-role frame");
        }
        foreach (var role in roles) {
            if (!scopes.Contains(role.Scope) || string.IsNullOrWhiteSpace(role.SourceObjectId) || role.SourceGeneration < 0
                || role.FaceRevision < 0 || role.AcquiredAtTick < role.Scope.StartedAtTick || role.AcquiredAtTick > state.Tick
                || role.RoleEventOrder < 0 || role.AcquiredAtTick == role.Scope.StartedAtTick && role.RoleEventOrder < role.Scope.RoleEventOrder
                || role.Role is not ("ATTACKER" or "DEFENDER" or "PLAYER_DEFENDER") || string.IsNullOrWhiteSpace(role.SourceFace)
                || role.ControllerId != (role.Role == "ATTACKER" ? role.Scope.AttackingPlayerId : role.Scope.DefendingPlayerId)
                || role.Role == "PLAYER_DEFENDER" && (role.SourceObjectId != role.Scope.BattlefieldId
                    || role.FaceRevision != 0 || role.TokenFactoryCardNo is not null
                    || !BattleLimitedMarkers.Definition(role.SourceFace, BattleLimitedMarkers.SteadfastDefend)
                        && !BattleLimitedMarkers.Definition(role.SourceFace, BattleLimitedMarkers.BulwarkDefend))
                || state.CardObjects.TryGetValue(role.SourceObjectId, out var source)
                    && (source.ObjectGeneration < role.SourceGeneration || source.ObjectGeneration == role.SourceGeneration
                        && role.Role != "PLAYER_DEFENDER" && (!CopyFaceTimeline.Valid(state, source)
                            || CopyFaceTimeline.FaceAt(source, role.FaceRevision, role.AcquiredAtTick) != role.SourceFace)))
                errors.Add("invalid private battle first-role binding");
            if (role.TokenFactoryCardNo is { } factory && (!P6TokenFactoryCatalog.TryGetByCardNo(factory, out var token)
                || !token.Tags.Contains(CardObjectTags.UnitCard))) errors.Add("invalid battle first-role token factory");
            if (!state.CardObjects.ContainsKey(role.SourceObjectId ?? "") && role.TokenFactoryCardNo is null)
                errors.Add("missing ordinary battle first-role source");
        }
        var seenGrants = new HashSet<(BattleLimitedScope, string)>();
        foreach (var trigger in state.TriggerQueue.Where(t => t.FieldContext?.Kind is "BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND" or "BATTLE_STEADFAST_DEFEND" or "BATTLE_BULWARK_DEFEND"))
            if (trigger.TriggeredByEventKind is not ("BATTLE_ROLES_ESTABLISHED" or "BATTLE_ROLE_ACQUIRED"))
                errors.Add("battle role skill has an unrelated trigger event");
        foreach (var item in state.StackItems.Where(i => i.FieldContext?.Kind is "BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND" or "BATTLE_STEADFAST_DEFEND" or "BATTLE_BULWARK_DEFEND"))
            if (item.DamageAmount != 0 || item.EffectRepeatCount != 1 || item.Destination.Length != 0
                || item.TargetedPowerAbility is not null || item.DamageDistribution is not null || item.OptionalCosts.Count != 0)
                errors.Add("battle role skill has unrelated stack instructions");
        foreach (var (id, card) in state.CardObjects) {
            var pairedPowers = new HashSet<string>(StringComparer.Ordinal); var pairedKeywords = new HashSet<string>(StringComparer.Ordinal);
            foreach (var text in card.UntilEndOfTurnEffects.Where(BattleMarkerFamily)) {
                if (!BattleLimitedMarkers.TryGrant(text, out var grant) || grant?.Scope is null || grant.Context is null) {
                    errors.Add("invalid object battle grant marker"); continue;
                }
                var c = grant.Context; var self = c.Kind is "BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND";
                var roleName = self ? c.Kind == "BATTLE_DOUBLE_SELF_ATTACK" ? "ATTACKER" : "DEFENDER" : "PLAYER_DEFENDER";
                var first = roles.Where(r => r.Scope == grant.Scope && r.SourceObjectId == grant.SourceObjectId
                    && r.SourceGeneration == grant.SourceGeneration && r.Role == roleName).ToArray();
                var correctDefinition = self ? c.CardNo is "SFD·110/221" or "SFD·110a/221"
                    : BattleLimitedMarkers.Definition(c.CardNo, c.Kind);
                var onField = state.PlayerZones.Values.Any(z => z.Base.Contains(id) || z.Battlefields.Contains(id));
                if (!scopes.Contains(grant.Scope) || !seenGrants.Add((grant.Scope, grant.StackItemId))
                    || string.IsNullOrWhiteSpace(grant.StackItemId) || first.Length != 1 || first[0].ControllerId != grant.ControllerId
                    || self && !first[0].OneOnOne
                    || !correctDefinition || grant.TargetObjectId != id || grant.TargetGeneration != card.ObjectGeneration
                    || grant.SourceGeneration != c.SourceGeneration || grant.Scope.BattlefieldId != c.BattlefieldId
                    || !onField || card.IsFaceDown || !card.Tags.Contains(CardObjectTags.UnitCard)
                    || !CoreRuleEngine.ValidFieldContextState(state, c, grant.SourceObjectId, grant.ControllerId,
                        CoreRuleEngine.FieldTriggerEffect, c.CardNo)) errors.Add("invalid battle grant source, target or definition");
                if (self) {
                    var power = card.UntilEndOfTurnPowerModifiers.Where(p => p.EffectId == grant.PowerEffectId).ToArray();
                    if (grant.KeywordEffectId is not null || grant.SnapshotMight is not > 0 || grant.SourceObjectId != id
                        || grant.SourceGeneration != card.ObjectGeneration || power.Length != 1) {
                        errors.Add("invalid battle snapshot grant pair"); continue;
                    }
                    var p = power[0];
                    if (!pairedPowers.Add(p.EffectId) || p.AppliedOrder is not > 0
                        || p.EffectId != BattleLimitedMarkers.PowerId(new(grant.Scope, grant.StackItemId, id, card.ObjectGeneration, p.AppliedOrder ?? 0))
                        || p.Duration != "UNTIL_END_OF_BATTLE" || p.EffectKind != c.Kind || p.SourcePath != "CoreRuleEngine.ResolveBattleLimitedRoleSkill"
                        || p.SourceObjectId != id || p.TargetObjectId != id || p.SourceCardNo != c.CardNo
                        || p.SnapshotMight != grant.SnapshotMight || p.RequestedPowerDelta != grant.SnapshotMight
                        || p.PowerDelta != grant.SnapshotMight || p.MinimumPower != 0 || p.ResultingPower != p.EffectivePower
                        || p.ChosenNegativeMight is not null)
                        errors.Add("invalid battle snapshot numeric ledger");
                }
                else if (grant.PowerEffectId is not null || grant.SnapshotMight is not null || grant.KeywordEffectId is not { } keyword
                    || !TemporaryCombatKeywordRules.IsValid(keyword)
                    || !BattlefieldTriggerSpecRules.TryGetTrigger(c.CardNo, t => c.Kind == BattleLimitedMarkers.BulwarkDefend
                        ? BattlefieldTriggerSpecRules.IsBattlefieldDefendGrantBulwarkTrigger(t)
                        : BattlefieldTriggerSpecRules.IsBattlefieldDefendGrantSteadfastTrigger(t), out var expectedKeyword)
                    || keyword != "COMBAT_KEYWORD:" + expectedKeyword.GrantedKeyword
                        + (expectedKeyword.GrantedKeyword == CardCombatKeywordNames.Bulwark ? "" : expectedKeyword.KeywordBonus)
                        + ":" + keyword[(keyword.LastIndexOf(':') + 1)..]
                    || card.UntilEndOfTurnEffects.Count(t => t == keyword) != 1 || !pairedKeywords.Add(keyword))
                    errors.Add("invalid battle keyword grant pair");
            }
            foreach (var p in card.UntilEndOfTurnPowerModifiers ?? [])
                if (p is not null && (p.Duration == "UNTIL_END_OF_BATTLE" || p.EffectId?.StartsWith("BATTLE_LIMITED_", StringComparison.Ordinal) == true
                    || p.EffectKind is "BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND")
                    && (p.EffectId is null || !pairedPowers.Contains(p.EffectId)))
                    errors.Add("unpaired battle power ledger");
        }
    }

    private static void ValidateBattleLimitedView(RecoveredPlayerView view, MatchState state, List<string> errors)
    {
        if (view.Snapshot is null || view.SnapshotTick != state.Tick) return;
        var expected = new Dictionary<string, (BattleLimitedGrant Grant, CardObjectState Card, PowerModifierLedgerEntry? Power)>(StringComparer.Ordinal);
        foreach (var card in state.CardObjects.Values)
            foreach (var text in card.UntilEndOfTurnEffects.Where(t => t.StartsWith("BATTLE_LIMITED_GRANT:", StringComparison.Ordinal))) {
                if (!BattleLimitedMarkers.TryGrant(text, out var grant) || grant?.Context is null) continue;
                var powers = card.UntilEndOfTurnPowerModifiers.Where(p => p.EffectId == grant.PowerEffectId).ToArray();
                if (powers.Length > 1) errors.Add("duplicate battle power ledger in view input");
                var power = powers.FirstOrDefault();
                var id = grant.KeywordEffectId is { } keyword ? $"OBJECT:{card.ObjectId}:{keyword}" : grant.PowerEffectId;
                if (id is not null && !expected.TryAdd(id, (grant, card, power))) errors.Add("duplicate expected battle effect");
            }
        var actual = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (TryReadObjectList(view.Snapshot.Timing, "continuousEffects", out var effects))
            foreach (var effect in effects) {
                TryReadObjectString(effect, "effectId", out var id); TryReadObjectString(effect, "duration", out var duration);
                TryReadObjectString(effect, "effectKind", out var kind);
                if (duration != "UNTIL_END_OF_BATTLE" && kind is not ("BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND" or "BATTLE_STEADFAST_DEFEND" or "BATTLE_BULWARK_DEFEND")
                    && id?.StartsWith("BATTLE_LIMITED_", StringComparison.Ordinal) != true) continue;
                if (id is null || !actual.TryAdd(id, effect)) errors.Add("invalid duplicate battle effect view");
            }
        if (!expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(actual.Keys)) errors.Add("battle effect view identities disagree with private grants");
        foreach (var (id, entry) in expected) {
            if (!actual.TryGetValue(id, out var payload)) continue;
            var g = entry.Grant; var p = entry.Power; var card = entry.Card;
            var strings = new Dictionary<string, string?> {
                ["scope"] = "OBJECT", ["layer"] = p is null ? "RULE_TEXT" : "POWER_MODIFIER", ["duration"] = "UNTIL_END_OF_BATTLE",
                ["targetObjectId"] = card.ObjectId, ["sourceObjectId"] = g.SourceObjectId, ["effectKind"] = g.Context.Kind,
                ["sourceCardNo"] = g.Context.CardNo, ["sourcePath"] = "CoreRuleEngine.ResolveBattleLimitedRoleSkill" };
            var numbers = new Dictionary<string, int?> {
                ["powerDelta"] = p?.PowerDelta ?? 0, ["basePower"] = p?.BasePower ?? card.Power - card.UntilEndOfTurnPowerModifier,
                ["effectivePower"] = p?.EffectivePower ?? card.Power, ["requestedPowerDelta"] = p?.RequestedPowerDelta,
                ["appliedPowerDelta"] = p?.PowerDelta, ["minimumPower"] = p?.MinimumPower, ["resultingPower"] = p?.ResultingPower,
                ["appliedOrder"] = p?.AppliedOrder, ["snapshotMight"] = g.SnapshotMight };
            if (strings.Any(pair => !OptionalStringSnapshotValueMatches(payload, pair.Key, pair.Value))
                || numbers.Any(pair => !OptionalIntSnapshotValueMatches(payload, pair.Key, pair.Value)))
                errors.Add("battle effect view fields disagree with captured grant and numeric ledger");
        }
    }
}
