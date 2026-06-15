# Stage 4D-223I Rule Audit Combat Damage Assignment Staging Audit

Date: 2026-06-15

Status: accepted as a rule-audit merge into local `main`.

Runtime changed: yes. Test coverage changed: yes.

## Scope

This checkpoint imports `codex/rule-audit-remaining-20260615` commit `15c9490e` through main merge commit `1de8a90e`.

The merged runtime change stages `ASSIGN_COMBAT_DAMAGE` by the current damage-assignment player instead of requiring a single all-sources submission:

- `CoreRuleEngine` now tracks accepted battle damage assignments in an internal battle-scoped ledger marker while the assignment window remains open.
- The assignment prompt advances from the attacking player to the defending player when the attacking player's controlled damage sources are fully assigned and the defender still has required damage sources.
- If the attacking player contributes no positive combat damage, the prompt can open directly for the defending player.
- Final combat damage is still applied simultaneously after all required player-controlled sources are assigned.
- The internal ledger marker is hidden from public continuous-effect projection and cleared when combat damage is committed or a battle assignment window is replaced/closed.
- Existing full-batch legal assignment submissions remain accepted for compatibility, but tests now cover the staged player path.

## Rule Source

Per the root-PDF gate and `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the local extracted rules before accepting this merge:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `/tmp/riftbound_rules_pdf_text/judge_faq_251023.txt`
- `《符文战场》核心规则_260330.pdf` rules 417.1.a, 417.3.a and 417.6.c: assignment is not damage, assigned combat damage is caused simultaneously after assignment, and combat damage source attribution is unit-based.
- Core rules 460.2.c-460.2.d: combat damage assignment requirements, lethal/overassignment limits and simultaneous damage application.
- Core rules 815.1 and 826.3-826.4: Bulwark and Back Row damage-assignment ordering constraints.
- `裁判FAQ_251023.pdf` questions 6.1-6.5: assignment versus damage, overassignment, equal-priority ordering, conflicting requirements and impossible-damage targets.
- `docs/符文战场_服务端核心规则自查文档.md` sections 12.3 and 15.11: service-side audit summary for staged combat damage assignment and keyword constraints.

## Validation

- Rule-audit source worktree focused/adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~GameHubJoinTests"` passed `409/409`.
- Rule-audit source worktree backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Main after merge focused/adjacent: same BattleDamage/AssignCombatDamage/DeclareBattle/StartBattle/BattlefieldContest/SpellDuel/GameHub filter passed `409/409`.
- Main after merge backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the docs checkpoint.
- Coordination: `rule-audit-remaining-20260615` commit `15c9490e` was merged into main as `1de8a90e`; DOC_MATRIX_CURRENT remained clean at `17bde0c3`.

Project remains **NOT READY**. This narrows the combat damage assignment state model and L2P-RG-005 path, but it does not close full battle assignment matrix breadth, recovery/spectator nested payload breadth, frontend/browser/formal E2E, `fullOfficial` or final readiness.
