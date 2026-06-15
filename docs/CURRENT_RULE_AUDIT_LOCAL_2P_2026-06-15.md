# Local 2P Rule Audit - 2026-06-15

Status: NOT READY. Independent RULE_AUDIT / local two-player correctness record only.

Scope:

- Worktree: `/Users/dinghaolin/MyProjects/riftbound-rule-audit-local2p-20260615`
- Branch: `codex/rule-audit-local2p-worktree-20260615`
- Base: local `main` at `d5d776e7` after merging `codex/local-2p-smoke-20260612`
- Non-scope: Stage 4D triggerQueue closure slices, shared coordination board, completion audit, and closure plan docs.
- Server remains the only rule authority; frontend must submit intents and render server state/prompt constraints.

Rule source baseline:

- Official Rules Hub: `https://riftbound.leagueoflegends.com/en-us/rules-hub/`
- Current Core Rules document is linked there and marked last updated `3/30/26`.
- Local smoke evidence:
  - `docs/LOCAL_2P_CHROME_CLICK_SMOKE_2026-06-12.md`
  - `docs/LOCAL_2P_REGULAR_LEGEND_BATCH_SMOKE_2026-06-12.md`

## Gap L2P-RG-001 - Standard MOVE_UNIT Exhaustion

- Rule source: current Core Rules via the official Rules Hub; standard move exhausts the moving unit as the action cost. Card/effect movement is separate and must not be conflated with standard move.
- Frontend reproduction: in a local two-player Chrome room, P1 played a unit to base, selected it on the match page, chose an opponent battlefield, and clicked `确认移动`.
- Expected: accepted standard move changes the unit location and leaves the moved unit exhausted. An already exhausted unit must not be offered as a standard-move source and must be rejected if submitted directly.
- Actual before local-2p merge: `UNIT_MOVED_TO_BATTLEFIELD` was logged, but the moved unit remained `正常`; the main-action prompt still exposed `MOVE_UNIT` for a unit that should have been exhausted.
- Blocking level: P1. This does not prevent room startup, but it breaks repeated local two-player turns by allowing illegal extra movement and misleading the visible board state.
- Minimal backend tests:
  - `BoardTaskQueueFoundationTests.BaseToBattlefieldMoveIntoEmptyBattlefieldKeepsTaskQueueIdle` now asserts the moved source is exhausted.
  - `BoardTaskQueueFoundationTests.ExhaustedMoveUnitSourceIsRejectedAndHiddenFromMainActionPrompt` covers prompt filtering plus direct command rejection for an exhausted source.
- Backend status: fixed by the local-2p merge and covered by this branch's focused conformance test.
- Frontend fix needed: no separate frontend fix currently required. The frontend already renders server `isExhausted` and uses server prompt candidates; it benefits from the server prompt filter.
- Chrome/2P smoke status: covered by `LOCAL_2P_CHROME_CLICK_SMOKE_2026-06-12.md`, including actual clicks for move and post-fix board state.

## Gap L2P-RG-002 - HASTE_READY Unpaid Entry State

- Rule source: current Core Rules via the official Rules Hub plus card text modeled by `HASTE_READY`: optional extra payment allows a Haste unit to enter active/ready; without that optional payment, active entry should not be assumed.
- Frontend reproduction: local two-player smoke with `OGN·010/298 军团后卫` showed the unit entering base as `正常` when played without paying `HASTE_READY`.
- Expected: units whose active entry depends on the Haste optional ready cost should enter exhausted unless the server validates and records the `HASTE_READY` optional payment branch.
- Actual current status: fixed as a common server rule. Any `CardBehaviorDefinition` that declares a `HASTE_READY` entry cost now resolves exhausted unless the server validates the paid `HASTE_READY` branch. Paid `HASTE_READY` still resolves active.
- Blocking level: P1 fixed for the current local two-player smoke blocker and covered across the existing no-optional Haste fixture matrix. Broader card-matrix completeness remains open outside this smoke scope.
- Minimal backend tests added/updated:
  - `LegionRearguardNoOptionalHasteReadyResolvesExhaustedToBase` covers the unpaid local-smoke card.
  - `LegionRearguardPaidHasteReadyResolvesActiveToBase` keeps the paid branch active.
  - `ReksaiNoOptionalPlayCardWithNoTargetsUsesStackAndResolvesToBase` now asserts unpaid Haste entry is exhausted for both printings.
  - `CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost` now asserts every no-optional source-unit fixture tagged `急速` resolves exhausted.
  - 34 no-optional Haste fixtures now expect exhausted final-state entry.
- Backend status: fixed server-side in `CoreRuleEngine.PlaySourceUnitToBase`; no card-specific exception is used.
- Frontend fix needed: no frontend logic fix identified. The server now exposes corrected `isExhausted` values for Haste no-optional entry.
- Chrome/2P smoke status: rerun passed in Chrome on room `rule-audit-2p-ready-0002` and target rerun room `rule-audit-2p-target-0003`. P1 played ordinary `OGN·010/298 军团后卫` to base without paying the optional `HASTE_READY` branch; after both players passed priority, both clients showed it in base as `横置`.

## Gap L2P-RG-003 - Turn Start Ready Step Missing

- Rule source: current Core Rules via the official Rules Hub; at turn start the turn player's exhausted active-zone objects must ready before the player proceeds through their turn.
- Frontend reproduction: local two-player Chrome smoke reached P1 turn 3 after P1 had exhausted two base runes and `OGN·010/298 军团后卫` on turn 1. Before the fix, those exhausted objects stayed unusable across turns, blocking normal local two-player progression.
- Expected: when control returns to a player, their exhausted face-up active-zone objects in base, battlefield, legend, and champion zones ready unless a card-specific rule says the object cannot become active.
- Actual current status: fixed as a common turn-start rule. `ResolveTurnStart` now readies the turn player's eligible objects and emits `OBJECTS_READIED`.
- Blocking level: P1 fixed for local two-player playability. Without this, normal draw/pay/play/move loops degrade after the first pass through both players.
- Minimal backend tests added:
  - `TurnStartReadiesExhaustedActiveZoneObjectsForTurnPlayer` covers base, battlefield, legend, and champion objects and excludes the non-turn player.
  - `EndTurnIntoNextPlayerTurnReadiesTheirObjectsAndReopensMoveUnit` locks the local 2P path: P1 ends turn, P2 gets readied objects, and standard move candidates reopen.
- Backend status: fixed server-side in `CoreRuleEngine.ResolveTurnStart`; no frontend rule simulation added.
- Frontend fix needed: no frontend logic fix identified. The frontend renders the server `isExhausted` state and prompt candidates.
- Chrome/2P smoke status: passed. In room `rule-audit-2p-ready-0002`, after P2 ended turn, Chrome showed the P1 turn-start event `P1 回合开始时重置横置对象`, P1 runes and `OGN·010/298` became `正常`, and `移动单位（需选择）` was available.

## Gap L2P-RG-004 - AnyUnit Target Prompt Included Battlefield Cards

- Rule source: current Core Rules via the official Rules Hub and the `OGN·004/298 顺劈` text; "a unit" target must be a visible field unit, not a battlefield card, rune, equipment, spell, standby card, or face-down object.
- Frontend reproduction: local two-player Chrome smoke on P2 turn opened ordinary `OGN·004/298 顺劈`. The target composer exposed the real unit `OGN·010/298` and also exposed public battlefield cards as target candidates.
- Expected: AnyUnit target candidates include visible units in base/battlefield zones only. Public battlefield cards must remain visible board objects but must not be selectable as unit targets, and direct illegal submissions must be rejected without mutation.
- Actual current status: fixed as a common server prompt and validation rule. Prompt generation and command validation now require visible unit identity for field-unit target scopes, while preserving legacy untyped unit fixtures and excluding explicit non-unit card-type tags.
- Blocking level: P1 fixed for local two-player playability. The bad prompt did not crash the room, but it asked players to choose illegal targets and could submit invalid unit-target intents.
- Minimal backend tests added:
  - `AnyUnitPlayCardPromptTargetListOnlyExposesPublicFieldUnits` covers prompt target choices across own/opponent base and battlefield units.
  - `AnyUnitPlayCardRejectsPublicFieldNonUnitTargetsWithoutMutation` covers direct rejection for battlefield-card targets.
- Backend status: fixed server-side in `CoreRuleEngine` and `ActionPromptBuilder`; this is a target-scope family fix, not a Cleave-only exception.
- Frontend fix needed: no separate frontend fix identified. The frontend target composer uses server target choices and now receives the corrected candidate set.
- Chrome/2P smoke status: passed after the fix. In room `rule-audit-2p-target-0003`, P2 opened ordinary `OGN·004/298 顺劈`; the target slot showed only `OGN·010/298`, did not show `OGN·276/298` or `OGN·277/298`, and the spell resolved after both players passed priority.

Validation:

- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LegionRearguardHasteReadyEntryTests|FullyQualifiedName~ReksaiNoOptionalHasteOverwhelmGuardTests|FullyQualifiedName~ArmedAssaulterHasteTemperedTests"`: 38/38 passed.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost|FullyQualifiedName~P4PermissionKeywordsKeepExistingP2FixturesGreen|FullyQualifiedName~P4HasteOptionalReadyBranch"`: 111/111 passed.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Haste|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueueFoundationTests"`: 260/260 passed.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TurnStart|FullyQualifiedName~EndTurn|FullyQualifiedName~MoveUnit|FullyQualifiedName~Haste|FullyQualifiedName~BoardTaskQueueFoundationTests"`: 325/325 passed.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~EnemyBattlefieldUnitTargetScopeGuardTests|FullyQualifiedName~TurnStartReadiesObjectsTests|FullyQualifiedName~LegionRearguardHasteReadyEntryTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~Haste|FullyQualifiedName~MoveUnit|FullyQualifiedName~TargetScope|FullyQualifiedName~Cleave"`: 294/294 passed.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName!~Postgres"`: 8243/8243 passed after the current rule fixes.
- `dotnet test Riftbound.slnx --no-restore`: rule tests passed before the local PostgreSQL smoke segment, then 5 PostgreSQL smoke tests failed locally with `Npgsql` stream EOF/timeout connection errors. This is an environment/persistence smoke blocker, not a local 2P rule regression.
- `npm run build` in `src/Riftbound.DevUi`: passed. `npm ci` reported 2 existing high-severity audit findings; dependency versions were not changed.
- Backend health smoke: `ASPNETCORE_URLS=http://127.0.0.1:5095 ConnectionStrings__Riftbound=` served `/health` with Noop persistence. The default PostgreSQL-backed startup failed locally because PostgreSQL connection initialization timed out.
- Frontend dev server smoke: Vite served `http://127.0.0.1:5178/` and `http://127.0.0.1:5179/` for separate local client origins.
- Chrome plugin smoke: passed for create/join, deck submit, ready, mulligan, draw/call-rune start flow, rune payment, card play, priority pass, turn advance, standard move availability, and post-fix AnyUnit target filtering. Ordinary versions only were used for the same-name cards in this smoke.

## Current Priority

1. Keep `L2P-RG-001` locked with backend conformance coverage.
2. Current local 2P smoke P0/P1 rule blockers found in this pass are fixed and covered by backend tests plus Chrome evidence.
3. Project remains NOT READY. Full official card matrix, broader optional-cost exactness, DB-backed persistence smoke, formal E2E, and final readiness remain open.
