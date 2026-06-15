# Remaining Rule Path Audit - 2026-06-15

Status: NOT READY. Independent local 2P / rule-path audit record only.

Scope:

- Worktree: `/Users/dinghaolin/MyProjects/riftbound-rule-audit-remaining-20260615`
- Branch: `codex/rule-audit-remaining-20260615`
- Base after rebase: `origin/main` at `1b69b479` (`checkpoint: stage 4D legend act replay protocol envelope`)
- Non-scope: Stage 4D triggerQueue/runtime closure slices, shared coordination board, completion audit, and closure plan docs.
- Server remains the only rules authority. Frontend and smoke scripts must submit intents and render server snapshots/prompts/events/errors/candidates.

Rule source baseline:

- Local PDFs supplied in `/Users/dinghaolin/IdeaProjects/riftbound`:
  - `《符文战场》核心规则_260330.pdf`
  - `裁判FAQ_251023.pdf`
  - `铸魂淬炼系列_官方FAQ_260114.pdf`
  - `铸魂淬炼系列_裁判FAQ.pdf`
  - `《符文战场》破限系列_裁判FAQ_260416.pdf`
- Text extraction used for this pass: `/tmp/riftbound_rules_text/*.txt`.
- Project authority docs checked: `README.md`, `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, and `docs/CURRENT_RULE_AUDIT_LOCAL_2P_2026-06-15.md`.

## Path Audit Summary

| Path | Rule baseline | Current implementation evidence | Audit status |
|---|---|---|---|
| Create / join / reconnect | Core 107-129; project session contract | `GameHubJoinTests` representative set passed in this pass; local 2P smoke doc records Chrome create/join/reconnect-facing evidence | No new P0/P1 found in this pass |
| Deck submit / ready / opening | Core 103, 107-129 | `OfficialOpeningTests` representative set passed in this pass; prior local 2P smoke covered ordinary two-client ready/mulligan path | No new P0/P1 found in this pass |
| Mulligan / hidden hand | Core 107-129 hidden-info boundary | Opening and GameHub tests passed; recovery hidden/redaction breadth remains covered by existing Stage 4D recovery suites | No new P0/P1 found in this pass |
| Turn start / draw / call rune / clear rune pool | Core 164-167, 315.1-315.4; JFAQ cleanup refs | Prior L2P-RG-003 fixed turn-start readying; representative tests passed again | No new P0/P1 found in this pass |
| Resource payment / play card / optional costs | Core 349+, 355-357, 377, 403-405, 414, 416; JFAQ q2.5 | `PaymentEngineUnificationTests` representative set passed; prior L2P-RG-002 fixed the common unpaid haste-entry family for local smoke | Broader payment matrix remains outside this smoke audit |
| Target selection | Core target legality; card text evidence by source card | Prior L2P-RG-004 fixed field-unit target filtering; `EnemyBattlefieldUnitTargetScopeGuardTests` passed again | No new P0/P1 found in this pass |
| Priority / stack / spell duel | Core 307-313, 333-348; JFAQ q2.2-q5.4 | `SpellDuelBattleStateMachineTests` representative set passed | No new P0/P1 found in this pass |
| Movement / battlefield tasks | Core 187-189, 442, 455-457; cleanup refs | Prior L2P-RG-001 fixed standard move exhaustion; `BoardTaskQueueFoundationTests` passed again | No new P0/P1 found in this pass |
| Attack / defend / combat damage | Core 454-461, especially 460.2.c; JFAQ q6.1-q6.4 | This pass found one fixed P1 gap (L2P-RG-006) and one remaining P1 design gap (L2P-RG-005) | See gaps below |
| End turn / cleanup / next turn | Core 316-324; JFAQ q5.1-q5.4 | Existing p2 preflight docs and representative tests cover turn-end cleanup / next-turn handoff; prior local 2P smoke reached pass / turn advance | No new P0/P1 found in this pass |
| Score / control / win | Core 323.1, 461-464 | Battle lifecycle tests cover representative battlefield control and battle result paths | Broader battlefield scoring matrix remains outside this smoke audit |
| Hidden info / two-client sync | Core 107-129; recovery redaction guard docs | GameHub and MatchRecovery representative tests passed; prior Chrome smoke covered two local clients seeing server state | No new P0/P1 found in this pass |

## Gap L2P-RG-005 - Combat Damage Assignment Needs Independent Player Choice

- Rule source: Core 460.2.c says, starting with the attacker, each player assigns damage equal to that player's side's total might to opposing units. Core 460.2.c.1 and JFAQ q6.1 say assignment is not damage, and damage is dealt simultaneously only after all assignment is complete. Core 460.2.c.6-c.7 and JFAQ q6.3-q6.4 make same-priority and conflicting assignment choices belong to the assigning player.
- Backend reproduction: `ResolutionResult.BattleDamageAssigningPlayerId` currently returns the attacker controller for the assignment window; `BuildCorePrompts` exposes `ASSIGN_COMBAT_DAMAGE` only to that player; `ValidateCombatDamageAssignments` requires a complete damage assignment for every battle participant in one command. Existing tests assert P2 is a `WAIT` prompt and wrong-player P2 assignment is rejected.
- Frontend reproduction: `ActionPanel.tsx` renders one damage assignment panel from server `assignmentChoices`. The actionable player can submit rows for all sources exposed by server metadata.
- Expected: after the battle spell-duel closes, the attacking player assigns attacking-side damage, then the defending player assigns defending-side damage. The battle should deal all assigned damage simultaneously only after both sides' assignments are complete.
- Actual: one attacker-side prompt submits attacker and defender source assignments together; the defender-side player waits and cannot submit their own side's assignment choices.
- Blocking level: P1 for local 2P battles that open the assignment window, because the defender loses required game choices. It is not hit by the prior ordinary one-attacker/one-defender Chrome smoke path.
- Minimal backend conformance test needed: open a natural assignment battle with one attacker and multiple defenders, submit only attacker-source assignments, assert the battle remains active and P2 receives `ASSIGN_COMBAT_DAMAGE`; P2 submits only defender-source assignments; only then assert simultaneous damage and battle close. Also reject P1 assigning defender sources and P2 assigning attacker sources.
- Chrome/2P smoke needed: yes, after the backend state model gains a partial assignment ledger and two-step prompt flow.
- Frontend fix needed: likely yes after backend change, to render only current assigning-player source rows and then rerender for the next player.
- Current status: open. Not fixed in this branch because it requires a battle assignment ledger/state-model change rather than a small local smoke fix.

## Gap L2P-RG-006 - Assignment Keyword Target Order Must Not Depend On Object Id

- Rule source: Core 460.2.c.3-c.5 requires lethal-before-next and assignment restrictions; Core 460.2.c.5 example gives `Bulwark` first, normal next, last-take-damage effects last. Core 460.2.c.6 preserves player choice only inside the same assignment priority. JFAQ q6.2-q6.4 repeats no-overkill, same-priority choice, and conflicting-requirement choice.
- Backend reproduction before fix: the assignment validation and prompt metadata used `BattleState.AttackerObjectIds` / `DefenderObjectIds` directly for legal target order. `BuildBattleState` sorts participants by object id, so a Back Row object whose id sorts before a Bulwark object could be treated as the first legal target, allowing the overkill tail to land on Bulwark instead of Back Row.
- Expected: legal target ordering for combat assignment must be server-derived from the target units' assignment keyword priority: Bulwark first, ordinary units next, Back Row last. Within the same priority, retain the stable source order.
- Actual before fix: assignment ordering could follow object id order, not keyword priority, in the `ASSIGN_COMBAT_DAMAGE` validation path and related prompt/recovery metadata.
- Blocking level: P1 for local 2P battles involving assignment-ordering keywords. It is a common feature-family bug, not a single-card exception.
- Minimal backend conformance test: `BattleDamageAssignmentLifecycleTests.NaturalAssignCombatDamageUsesKeywordPriorityWhenDefendersDeclaredOutOfOrder` creates a Back Row defender with an object id that sorts before the Bulwark defender, verifies prompt legal targets still list Bulwark first, rejects Back Row first overkill, and accepts the keyword-sorted assignment.
- Backend status: fixed in this branch by sorting combat damage legal targets from card tags in `CoreRuleEngine`, `MatchSession` prompt/timing metadata, and `MatchRecovery` validation.
- Frontend fix needed: no separate frontend fix. The frontend consumes server `legalTargets` / `assignmentChoices`.
- Chrome/2P smoke needed: optional follow-up if the local smoke includes assignment-keyword battles. Backend conformance is the primary guard for this feature family.

## Validation

- Restore: `/Users/dinghaolin/.dotnet/dotnet restore Riftbound.slnx` passed. Initial `--no-restore` attempts were invalid in the fresh worktree because `project.assets.json` did not exist yet.
- Focused new regression: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~NaturalAssignCombatDamageUsesKeywordPriorityWhenDefendersDeclaredOutOfOrder"` passed `1/1`.
- Battle damage lifecycle: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests"` passed `47/47`.
- Local 2P adjacent rule paths: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~TurnStartReadiesObjectsTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~EnemyBattlefieldUnitTargetScopeGuardTests"` passed `81/81`.
- Opening / payment / spell-duel / GameHub representative set: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~GameHubJoinTests"` passed `932/932`.
- Recovery adjacency for changed metadata helper: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1938/1938`.

## Next

1. Treat L2P-RG-005 as the next combat P1 if local 2P smoke expands into assignment-keyword battles.
2. Any fix for L2P-RG-005 should be state-model first: partial assignment ledger, current assigning side, source-scoped metadata, and simultaneous damage commit after both submissions.
3. If Chrome smoke is rerun from this branch, keep it to ordinary versions only and one card per same-name pair.
