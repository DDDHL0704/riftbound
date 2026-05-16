# Stage 4D-03CO PaymentEngine Jhin Movement Resource Skill Audit

Audit date: 2026-05-16
Conclusion: **FOCUSED JHIN RESOURCE-SKILL SLICE ACCEPTED / PROJECT NOT READY**

## 1. Scope

4D-03CO follows the 4D-03CB Jhin handoff and the 4D-03CA non-legend resource-skill lane gate. It implements only the `UNL-022/219` Jhin movement-triggered resource-skill representative:

- server-captured movement trigger;
- server-filtered `ACTIVATE_ABILITY` prompt after that trigger exists;
- command-side trigger/source/context/resource-use revalidation;
- generated 1 mana plus 1 payment-only power;
- immediate resolution without an ordinary stack response item.

This batch does not close alternate / reprint parity such as `UNL-022a/219`, Honeyfruit, Blue Sentinel, Lux, the 9 legend bridge candidates, frontend runtime, Chrome smoke, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Runtime Contract

`P4ActivatedAbilityCatalog` now exposes `JHIN_MOVE_TRIGGER_GAIN_1_MANA_1_POWER` as a resource skill for `UNL-022/219`.

`CoreRuleEngine` captures server-owned movement triggers for successful Jhin movement paths, including ordinary movement and precise Roam battlefield-to-battlefield movement. The `ACTIVATE_ABILITY` command must include exactly one service-issued optional cost:

```txt
JHIN_MOVE_TRIGGER:<triggerId>
```

Resolution revalidates:

- active player and open-main timing;
- empty ordinary stack and no pending blocker windows;
- source identity, controller, public unit state and current destination;
- movement trigger identity and context;
- no target shape;
- generated-resource use.

Accepted resolution removes the trigger, emits `TRIGGER_RESOLVED`, `ABILITY_ACTIVATED`, `MANA_GAINED` and `POWER_GAINED`, and does not create an ordinary stack item. The lifecycle is intentionally hybrid:

- generated mana enters `RunePool.Mana`;
- generated power enters `TemporaryPaymentResourceState`;
- the temporary power restriction is `PAY_RUNE_COSTS_ONLY_JHIN_MOVE_TEMPORARY_LEDGER_4D_03CO`;
- unused generated mana / power and unused movement triggers are cleared or expired at turn end.

`MatchSession` now exposes the prompt only after a matching service-captured Jhin movement trigger exists, and carries Jhin-specific source requirement metadata including generated mana, generated power, movement trigger choice, stack policy, reaction policy and resource restriction.

## 3. Verifier Coverage

`tests/Riftbound.ConformanceTests/JhinMovementResourceSkillTests.cs` covers 14 focused cases:

| Coverage slice | Assertion |
|---|---|
| Catalog shape | Ability id, source card, generated mana / power and restriction are catalog-bound. |
| Prompt gating | Jhin is hidden before movement and exposed only after a server-captured movement trigger. |
| Stale prompt filtering | Jhin prompt disappears when the captured movement destination no longer matches authoritative location. |
| Successful resolution | The command gains 1 mana, creates 1 payment-only temporary power, clears the trigger and opens no ordinary stack item. |
| Precise Roam movement | `BATTLEFIELD:P1-BATTLEFIELD-A` to `BATTLEFIELD:P1-BATTLEFIELD-B` with `ROAM` queues and resolves the same Jhin trigger path. |
| Later legal payment | Generated mana plus temporary power can pay a later legal rune cost and clear cleanly. |
| Turn cleanup | Unused generated resources and unused movement triggers expire at end turn. |
| No-mutation rejection | Wrong window, missing trigger, stale source, stale context, wrong resource use and handwritten trigger attempts reject without mutation. |

## 4. Coverage Manifest Changes

`PaymentEngineCoverageAuditTests.cs` now records Jhin as an implemented representative instead of a deferred non-legend lane:

- `ResourceSkillCoverageManifest` tracks 20 current catalog `IsResourceSkill=true` representative ability ids.
- `ResourceSkillOfficialBreadthManifest` marks `UNL-022/219` as implemented representative.
- `DeferredNonLegendResourceSkillRuntimeLaneManifest` keeps only three remaining non-legend lanes: Honeyfruit, Blue Sentinel and Lux.
- `ResourceSkillAllWindowMatrixManifest` expands to 42 representative rows because Jhin is now a catalog-bound family; this is representative matrix evidence only, not full official closure.

## 5. Validation

Commands run:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~JhinMovementResourceSkillTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JhinMovementResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Results:

- Focused Jhin movement resource skill tests: passed 14/14.
- Adjacent PaymentEngine / resource skill / legend / prompt / hub regression: passed 705/705.
- Backend full: passed 4620/4620.
- `git diff --check`: passed after final doc sync.

## 6. Non-Closure

4D-03CO is accepted as a focused Jhin representative slice only. It does not close P0-005, P1, full-card matrix, frontend final validation, Chrome smoke, formal 18-step E2E or READY.

The current implementation is deliberately scoped to `UNL-022/219`; alternate / reprint Jhin parity and any official review requiring broader movement trigger semantics remain future work. Honeyfruit, Blue Sentinel and Lux remain deferred non-legend resource-skill lanes.
