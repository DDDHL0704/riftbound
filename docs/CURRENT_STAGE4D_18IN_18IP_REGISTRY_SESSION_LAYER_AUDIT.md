# Stage 4D 18IN-18IP Registry/Session/Layer Audit

Date: 2026-06-05

Status: accepted on A_MAIN as a bundled runtime/server closure checkpoint. Project remains **NOT READY**.

## Scope

- 18IN: `InMemoryMatchSessionRegistry` now re-runs `MatchRecoveryValidator.Validate(...)` on store-returned recovery frames before restore, even when the store supplied an empty `ValidationErrors` list. Validator failures now reject restore with `RECOVERY_INCONSISTENT` and a `match recovery validation failed` diagnostic before the action-log replay audit path.
- 18IO: official opening `SubmitDeckAsync` and `ReadyAsync` duplicate same-player `clientIntentId` coverage now proves exact raw-command replay stays stable while changed raw command payloads with the same command type reject with `CLIENT_INTENT_CONFLICT` and no public snapshot or journal mutation. A_MAIN replaced the worker's private-field state reflection with public `SnapshotFor` signatures.
- 18IP: LayerEngine battlefield static-aura coverage now locks authoritative scalar metadata and P1/P2 snapshot parity for battlefield source order, participant dependencies, power delta, base power, effective power, LayerEngine status and deferred residual metadata.

## Files

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
- `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`

## Worker Commits

- 18IN `0e1f1393` (`codex/stage4d-18in-registry-revalidate`)
- 18IO `d22cbee4` (`codex/stage4d-18io-session-official-idempotency`)
- 18IP `a33b8c82` (`codex/stage4d-18ip-layer-battlefield-static-aura`)

## Validation

- Focused new tests: `4/4`
  - `RegistryRevalidatesStoreReturnedRecoveryFrameBeforeRestore`
  - `OfficialSubmitDeckDuplicateClientIntentReplaysExactRawButRejectsChangedRawPayload`
  - `OfficialReadyDuplicateClientIntentReplaysExactRawButRejectsChangedRawPayload`
  - `LayerEngineBattlefieldStaticAuraPowerScalarsMatchAuthoritativeStateAcrossPlayerViews`
- Adjacent combined server filter: `1870/1870`
  - `MatchRecoveryTests`
  - `OfficialOpeningTests`
  - `LayerEngineTimestampDependencyTests`
  - `PostgresMatchRecoveryStoreSmokeTests`
- Backend full: `Riftbound.slnx` `7230/7230`
- Mechanical checks before docs sync:
  - `git diff --check`
  - `git diff --cached --check`
  - anchored conflict-marker scan over `src`, `tests`, `docs`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Residual Risk

- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was available.
- The next executable server candidates include Postgres command journal duplicate-intent persistence, Postgres player seat uniqueness, GameHub raw-intent protocol coverage, random recycle order determinism, and Ornn static-aura metadata coverage.
- Broader command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, P0/P1 final closure and final READY status remain open.
