# 4D-05D B_SERVER Prompt: Activated Ability Spellshield Tax Replay Guards

Date: 2026-05-21

Owner: `B_SERVER`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet`

## Objective

Add focused server-side coverage for `ACTIVATE_ABILITY` payment-plan windows that include Spellshield target tax and source exhaustion, proving successful activation commands cannot be replayed to mutate state a second time.

This continues P0-005 `B_PAYMENT_ENGINE_OFFICIAL_BREADTH` after 05A/05B/05C. It should target a real residual noted in the audits: target/tax activated ability breadth, remaining payment windows and replay/hash hardening. Do not repeat ordinary pending `PAY_COST` or Fiora trigger-payment invalid-choice coverage that already exists.

This is rollback / replay / revalidation narrowing evidence only. It does not close complete PaymentEngine, card matrix, P0/P1, frontend gates, Chrome smoke, formal E2E or READY.

## Allowed Write Scope

- `tests/Riftbound.ConformanceTests/CrimsonRoseActivatedAbilityTests.cs`
- `tests/Riftbound.ConformanceTests/ShadowActivatedAbilityTests.cs`
- Optional helper extraction inside those same files.
- `src/Riftbound.Engine/CoreRuleEngine.cs` only if a focused test exposes an actual runtime bug.
- Optional 05D audit/evidence docs if needed.

## Locked Scope

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- Frontend files
- Official catalog / source snapshot files
- API/protocol core fields
- Browser / Chrome / formal E2E scripts
- `fullOfficial`
- READY / READY-CANDIDATE status
- `riftbound-dotnet.sln`

## Required Coverage

Add compact tests using existing helpers in `CrimsonRoseActivatedAbilityTests` and/or `ShadowActivatedAbilityTests`.

Cover at least two representative replay surfaces:

- Crimson Rose `ACTIVATE_ABILITY` with enemy Spellshield target tax: accepted once, pays experience plus tax, exhausts source, creates exactly one stack item; replaying the same `ActivateAbilityCommand` against the post-activation state rejects without mutation.
- Shadow `ACTIVATE_ABILITY` with enemy Spellshield target tax: accepted once, pays mana + power + Spellshield tax, exhausts source, creates exactly one stack item; replaying the same `ActivateAbilityCommand` against the post-activation state rejects without mutation.

Assertions should prove:

- rejected replay emits no events
- exact `MatchStateHasher` hash is preserved for replay rejection
- no second `ABILITY_ACTIVATED`, `COST_PAID`, source-exhaust, stack-item or tax event appears
- rune pool / experience / card zones / source exhaustion / stack contents remain exactly as after the first accepted activation
- hidden information is not introduced into events, prompts or error text
- protocol shape does not change

If existing behavior already rejects correctly, keep the slice test-only. Add runtime code only when the focused tests expose a real server bug.

## Validation

Run at least:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~CrimsonRoseActivatedAbilityTests|FullyQualifiedName~ShadowActivatedAbilityTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CrimsonRose|FullyQualifiedName~Shadow|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Spellshield|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt"
git diff --check
```

If runtime code changes, also run full backend:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## Stop Conditions

Stop and report to A_MAIN if:

- Any backend test fails after the intended change.
- The fix would require protocol core field changes.
- Hidden information leakage is observed.
- The implementation requires matrix JSON or `PaymentEngineCoverageAuditTests.cs` edits.
- The behavior conflicts with the service-side authority principle.
