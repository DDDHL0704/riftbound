# Stage 4D-223J Rule Audit Independent Combat Damage Assignment Audit

Date: 2026-06-15

Status: accepted as a follow-up rule-audit merge into local `main`.

Runtime changed: yes. Test coverage changed: yes.

## Scope

This checkpoint imports `codex/rule-audit-remaining-20260615` commits `b5acfe9a` and `3c8ef8fe` through main merge commit `f2848062`.

This follow-up supersedes the Stage 4D-223I compatibility note: full-batch attacker submissions containing defender-side damage sources are now rejected. `ASSIGN_COMBAT_DAMAGE` must be submitted as independent attacker and defender assignment steps when both sides have required positive-damage sources.

The merged changes:

- derive attacking and defending players from effective field control where card objects are available, falling back to stored battle participant controller ids only when needed;
- limit required assignment sources to the current player's battle side rather than every battle participant controlled by the same player;
- reject defender-first submissions while attacker assignment is pending;
- reject attacker commands that include defender-side sources;
- preserve staged attacker-to-defender handoff and simultaneous final combat damage after all required sources are assigned;
- add regression coverage for independent attacker/defender steps, raw idempotency, stale prompt replay, stale raw replay and Stage 3 preflight coverage.

## Rule Source

Per the root-PDF gate and `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the same combat damage rule source before accepting this follow-up:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `/tmp/riftbound_rules_pdf_text/judge_faq_251023.txt`
- `《符文战场》核心规则_260330.pdf` rules 417.1.a, 417.3.a and 417.6.c for assignment versus damage, simultaneous damage and unit source attribution.
- Core rules 460.2.c-460.2.d for combat damage assignment constraints and simultaneous damage application.
- Core rules 815.1 and 826.3-826.4 for Bulwark and Back Row assignment ordering constraints.
- `裁判FAQ_251023.pdf` questions 6.1-6.5 for assignment/damage distinctions, overassignment, equal-priority ordering, conflicting requirements and impossible-damage targets.
- `docs/符文战场_服务端核心规则自查文档.md` sections 12.3 and 15.11 for the service-side audit summary.

## Validation

- Rule-audit source worktree focused/adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~Stage3PreflightCoversBattleDamage"` passed `411/411`.
- Rule-audit source worktree backend full: `dotnet test Riftbound.slnx --no-restore` passed `8261/8261`.
- Main after merge focused/adjacent: same filter passed `411/411`.
- Main after merge backend full: `dotnet test Riftbound.slnx --no-restore` passed `8261/8261`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the docs checkpoint.
- Coordination: `rule-audit-remaining-20260615` commits `b5acfe9a` and `3c8ef8fe` were merged into main as `f2848062`; DOC_MATRIX_CURRENT remained clean at `17bde0c3`.

Project remains **NOT READY**. This narrows the independent combat damage assignment state model, but full battle assignment matrix breadth, recovery/spectator nested payload breadth, frontend/browser/formal E2E, `fullOfficial` and final readiness remain open.
