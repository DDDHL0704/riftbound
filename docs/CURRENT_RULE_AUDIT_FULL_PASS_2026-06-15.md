# Full Rule Path Audit - 2026-06-15

Status: NOT READY. Independent full-rule first-pass audit record only.

Scope:

- Worktree: `/Users/dinghaolin/MyProjects/riftbound-rule-audit-remaining-20260615`
- Branch: `codex/rule-audit-remaining-20260615`
- Synced base: local `main` at `75debf7b` (`test: cover order triggers replay protocol envelope`)
- Main worktree note: `/Users/dinghaolin/MyProjects/riftbound-stage4d-222e-protocol-envelope` still had uncommitted Stage 4D documentation changes during the final sync check. This audit consumed only committed `main`.
- Non-scope: Stage 4D triggerQueue/runtime closure slices, shared coordination board, completion audit, and closure plan edits.
- Local 2P smoke scope remains ordinary versions only; same-name pairs need only one ordinary representative unless a rule gap is tied to the shared feature family.

Rule source baseline:

- `《符文战场》核心规则_260330.pdf`
- `裁判FAQ_251023.pdf`
- `铸魂淬炼系列_官方FAQ_260114.pdf`
- `铸魂淬炼系列_裁判FAQ.pdf`
- `《符文战场》破限系列_裁判FAQ_260416.pdf`
- Extracted text used from `/tmp/riftbound_rules_text/*.txt`.
- Project authority docs checked: `docs/rules-authority-and-audit.md`, `docs/rules-card-baseline.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_SERVER_RULE_AUDIT.md`, `docs/CURRENT_RULE_AUDIT_LOCAL_2P_2026-06-15.md`, and `docs/CURRENT_RULE_AUDIT_REMAINING_PATHS_2026-06-15.md`.

## Chapter Coverage

| Rules | Audit focus | Implementation evidence | First-pass result |
|---|---|---|---|
| `000` general principles | Card text overrides rules, cannot over can, do as much as possible, owner-zone constraints | Rule authority docs require PDF/card evidence; engine has targeted legality and no-mutation guards in command paths | No new local 2P P0/P1 found. Full card-text conflict and do-as-much-as-possible matrix remains broad rule-audit work |
| `100` objects, players, zones, deck, opening | Deck construction, hidden/public/private zones, object ownership/control, 1v1 opening setup | `OfficialDeckRules`, `MatchSession.BuildOfficialOpeningState`, snapshot redaction, opening/mulligan tests | Ordinary local 2P opening path is supported. Full object/control/zone lifecycle still has residual risk in battlefield, standby, attach, and recovery breadth |
| `300` turn structure and timing | Turn start, main/end turn, priority, focus, stack, spell duel, cleanup, triggers | `CoreRuleEngine` turn/pass/spell-duel/cleanup paths plus representative conformance tests | Representative 2P paths are covered. Full cleanup queue, battle task lifecycle, and trigger ordering remain known broad residuals |
| `400` game actions | Draw, exhaust/ready, recycle, damage, play, move, standby, discard, stun, reveal, destroy, attach, battle, score, win, layers | Payment, play, move, reveal, battle, score, surrender, and recovery implementations plus focused tests | No new small local 2P P0/P1 beyond L2P-RG-005. Full action matrix and LayerEngine breadth remain incomplete |
| `476-484` official modes | 1v1 duel setup: 2 players, selected battlefields, win score, second action-player extra rune timing | `MatchSession` 1v1 opening and score/win representative tests | 1v1 local product mode is covered as a representative path. Other modes are not in current product scope |
| `649-652` surrender/removal | 1v1 surrender and opponent win; broader multiplayer removal rules | `CoreRuleEngine.ResolveSurrender` and hub/game tests | 1v1 surrender path is implemented. Multiplayer removal details are out of local 2P scope |
| `700` additional rules | Boon, powerful/extra damage, attach/top/inactive text, dependencies, experience, extra turns, friend/enemy/lone, instruction-text costs | Representative card/effect tests and server audit entries | Many representatives exist, but full official `700` matrix remains residual. No new ordinary local 2P smoke blocker found |
| `800` keywords | Haste, Swift, Assault, Last Breath, Spellshield, Roam, Standby, Encourage, Reaction, Steadfast, Bulwark, Ephemeral, Predict, Assemble, Agile, Echo, Tempered, Ambush, Hunt, Level, Unique, Back Row | `KeywordCoverageReporter` and keyword rule modules expose implemented/deferred status; local smoke fixed Haste, move, target scope; combat assignment priority fixed | Keyword families are explicitly mixed implemented/deferred. Treat gaps as feature-family work, not single-card exceptions |

## Path Coverage

| Path | Rule source anchors | Current state | Gap disposition |
|---|---|---|---|
| Create / join / reconnect | Core hidden-info and session ownership rules | Latest committed `GameHubJoinTests` include protocol envelope and replay assertions; redaction/reconnect tests exist | No new local 2P P0/P1 found |
| Deck submit / ready / opening | Core 103, 107-129, 476-484 | Standard deck validation, chosen hero handling, random battlefield selection, opening hand 4, mulligan up to 2, first-turn setup are represented | No new local 2P P0/P1 found |
| Hidden information / recovery | Core public/private/hidden zones; standby face-down rules | Snapshot redaction hides opponent hand/decks and face-down standby identity; recovery validation is heavily covered by Stage 4D slices | Full recovery/random/command breadth remains residual, but no new ordinary local 2P P0/P1 found |
| Turn start / draw / call rune / rune pool clear | Core 159-167, 315-324 | Turn-start readying and rune/draw flow fixed by local 2P work; command paths clear rune pools at draw/end boundaries | No new local 2P P0/P1 found |
| Play card / payment / optional costs | Core 349-359, 403-416; FAQ payment timing | Shared `PaymentPlan` and representative payment tests exist; Haste unpaid entry was fixed as a common feature-family rule | Full official payment matrix remains FULL-RG-005 |
| Targets and command legality | Core target legality and card text | AnyUnit target scope fixed as field-unit family; many target guards exist | No new local 2P P0/P1 found |
| Priority / focus / stack / spell duel | Core 307-313, 325-348; JFAQ focus and battle-spell-duel clarifications | Representative state machine tests exist; pass priority/focus are server-controlled | Full trigger/order and uncommon timing breadth remains FULL-RG-004 |
| Movement / battlefield tasks | Core 187-189, 442, 455-457 | Standard move exhaustion fixed; server validates control, exhaustion, destination, combatant state | Full battlefield/control/held/conquer lifecycle remains FULL-RG-001 |
| Battle / damage assignment / scoring | Core 454-464; JFAQ q6.1-q6.4 | Keyword-priority legal target ordering is fixed. Defender-side assignment ownership remains open | L2P-RG-005 remains open P1 |
| Cleanup / state actions | Core 316-324; JFAQ cleanup/special cleanup | Representative cleanup and state-based tests exist | Full cleanup queue remains FULL-RG-001 |
| Standby / Reaction / replay protocol paths | Core 811, 813; hidden-info and trigger-ordering rules | Latest committed main includes RevealCard and ORDER_TRIGGERS replay protocol envelope tests; standby/reaction representatives exist | Full standby/reaction and trigger-ordering matrices remain FULL-RG-002 / FULL-RG-004 |
| Surrender / win | Core 649-652, 461-464 | 1v1 surrender resolves opponent win; score/win representatives exist | No new local 2P P0/P1 found |

## Gap Ledger

### L2P-RG-005 - Combat Damage Assignment Needs Independent Player Choice

- Rule source: Core 460.2.c says each player assigns that player's side's combat damage, starting with the attacker. JFAQ q6.1-q6.4 separates assignment from simultaneous damage and preserves same-priority/conflicting-requirement choices for the assigning player.
- Backend evidence: `ResolutionResult.BattleDamageAssigningPlayerId` currently resolves to attacker-side control for the natural assignment window; prompts expose `ASSIGN_COMBAT_DAMAGE` to that player only; `ValidateCombatDamageAssignments` expects one complete assignment payload for all combatants.
- Expected: attacker assigns attacking-side sources, then defender assigns defending-side sources, then damage is dealt simultaneously.
- Actual: one attacker-side prompt can submit all source assignments; defender is a waiting player and cannot submit the defender-side choices.
- Severity: P1 for local 2P battles that open assignment windows with meaningful defender choices.
- Minimal conformance needed: partial assignment ledger, source-side ownership checks, attacker submission leaves the battle pending, defender submission commits simultaneous damage, and cross-side assignment attempts are rejected.
- Chrome/2P smoke needed after fix: yes, because the UI must rerender the second assignment step from server prompts.
- Current status: open. Not fixed in this pass because it is a battle state-model change, not a small isolated smoke fix.

### L2P-RG-006 - Assignment Keyword Target Order Must Not Depend On Object Id

- Rule source: Core 460.2.c.3-c.6 and JFAQ q6.2-q6.4 require assignment restrictions and preserve choice only within the same priority. Bulwark/ordinary/Back Row ordering must come from rule priority, not object id order.
- Previous actual: prompt/recovery/validation metadata could inherit participant object-id order.
- Status: fixed before this full pass as a common combat assignment feature-family issue.
- Guard: `BattleDamageAssignmentLifecycleTests.NaturalAssignCombatDamageUsesKeywordPriorityWhenDefendersDeclaredOutOfOrder`.

### FULL-RG-001 - Battlefield, Cleanup, Battle Task, And Control Lifecycle Breadth

- Rule source: Core battlefield/control rules, turn cleanup rules, battle rules, and JFAQ cleanup/control clarifications.
- Evidence: `docs/rules-evidence-index.md` keeps P0E-004/P0E-005/P0E-006/P0E-007 as broad residuals; representative tests exist but do not close the full official matrix.
- Current local 2P impact: ordinary smoke paths can run, but uncommon contested battlefield/control/cleanup interleavings can still diverge from official rules.
- Fix policy: do not touch Stage 4D triggerQueue/runtime closure in this audit. Future fixes should be feature-family state-machine work with focused fixtures.

### FULL-RG-002 - Keyword Families Are Mixed Implemented/Deferred

- Rule source: Core 800 plus Soul/Break FAQ keyword corrections.
- Evidence: keyword modules and `/catalog/keyword-coverage` intentionally expose implemented, delegated, mixed, and deferred families.
- Current local 2P impact: Haste, standard move, AnyUnit scope, and assignment target priority have specific local 2P fixes; other keyword families remain representative-only or deferred.
- Fix policy: when a keyword bug is found, fix the keyword family path and add conformance coverage across same-feature cards, not one card.

### FULL-RG-003 - Layer, Attachment, Equipment, And `700` Extra Rule Breadth

- Rule source: Core 700 plus Soul FAQ equipment/control/layer clarifications.
- Evidence: server audit calls out LayerEngine, attach/detach, equipment ownership/control, top-card/inactive text, dependency, experience, and extra turn breadth as representative-only.
- Current local 2P impact: no ordinary local 2P P0/P1 found in this pass, but complete official behavior is not closed.
- Fix policy: require PDF/card evidence and targeted tests before changing runtime.

### FULL-RG-004 - Trigger Ordering / ORDER_TRIGGERS Full Official Breadth

- Rule source: Core triggered skill and priority rules; JFAQ simultaneous trigger ordering.
- Evidence: `docs/rules-evidence-index.md` keeps P0E-009 as a broad residual. Existing Stage 4C/4D slices prove representatives, not all official trigger behavior.
- Current local 2P impact: ordinary smoke paths can progress, but full trigger ordering remains a high-risk rule area.
- Fix policy: explicitly out of this pass per scope. No Stage 4D triggerQueue closure work was done here.

### FULL-RG-005 - Payment / Optional / Additional Cost Full Official Breadth

- Rule source: Core play/payment rules, cost legality rules, Haste/Echo/Spellshield/Assemble/Tempered FAQ clarifications.
- Evidence: shared `PaymentPlan` and representative tests cover many paths; `docs/rules-evidence-index.md` still treats full payment breadth as residual.
- Current local 2P impact: ordinary payment and fixed Haste local smoke path are covered; unusual optional/additional payment contexts still need rule-by-rule fixtures.
- Fix policy: keep future changes in shared payment feature paths, not card-specific shortcuts.

## Validation

- Mechanical: `git diff --check` passed.
- Focused representative conformance: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~GameHubJoinTests"` passed `944/944`.
- Runtime changed: no. This pass adds audit documentation only.

## Current Conclusion

All core rule chapters in the supplied PDF set have now been audited at least once against the current committed implementation and existing evidence docs.

No additional small P0/P1 local 2P blocker was found beyond open L2P-RG-005. The project remains NOT READY because the full official battlefield/cleanup/battle/payment/trigger/keyword/layer matrix is still broader than the representative local 2P path.

Recommended next rule work:

1. Fix L2P-RG-005 as a battle assignment state-machine feature-family change.
2. Keep future keyword/card findings grouped by shared rule feature, not by one named card.
3. Continue syncing this branch from local `main` before each new audit or fix batch.
