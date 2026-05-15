# Stage 4D-03AQ PaymentEngine HASTE_READY Coverage Verifier Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本 handoff 是 A 主控给下一枚 P0-005 keyword payment coverage verifier 切片的写锁和验收规格。它不实现 runtime，不修改前端，不修改 card matrix。目标是在 4D-03AP Rek'Sai red exactness guard 之后，用 catalog / registry bound verifier 防止已实现 HASTE_READY official cards 回退成无 trait、任意符能或缺 fixture evidence。

## 1. Target

新增 test-only verifier，绑定当前已实现的 HASTE_READY official cards：

- every `CardBehaviorRegistry.GetAll()` entry whose profile has implemented HASTE_READY must carry `HasteReadyManaCost=1`, `HasteReadyPowerCost=1` and non-empty official `HasteReadyPowerTrait`.
- the verifier must bind the implemented HASTE_READY registry/profile set to existing `p4-play-*-haste-ready.fixture.json` evidence.
- the verifier must be explicit that this is representative coverage, not full official Haste / PaymentEngine closure.

## 2. Input Facts

- `CardCatalogBaselineTests.P4HasteReadyProfilesCarryOfficialColoredPowerTrait` already records official colored traits for the implemented HasteReady profiles.
- `ConformanceFixtureRunnerTests` contains many individual P4 HASTE_READY fixture checks, but no single catalog-bound PaymentEngine coverage verifier ties the implemented HasteReady registry set to fixture presence / closure status.
- `tests/Riftbound.ConformanceTests/Fixtures` currently contains 34 `p4-play-*-haste-ready.fixture.json` files.
- `PaymentEngineCoverageAuditTests` already holds catalog-bound manifests for action windows, Spellshield tax and resource skill coverage; 4D-03AQ should follow that style.
- 4D-03AP added Rek'Sai-specific prompt / command / cost-audit red exactness evidence, but P0-005 still lists keyword payment branches as representative breadth.

## 3. Suggested B Write Scope

Default write scope:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`

Optional only if needed:

- No runtime file should be needed.
- Do not modify fixture JSON unless the verifier reveals a true missing / stale fixture and the smallest safe fix is fixture metadata only.

Forbidden in this slice:

- `src/**` runtime changes
- frontend runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad Haste keyword rewrite
- strong / Overwhelm, battle damage overflow, non-hand haste granting, LayerEngine
- full backend behavior changes
- `riftbound-dotnet.sln`

## 4. Acceptance

Minimum acceptance:

1. `PaymentEngineCoverageAuditTests` gains a HASTE_READY coverage manifest or equivalent verifier.
2. The verifier enumerates the current implemented HASTE_READY registry/profile set and fails if a card with implemented HASTE_READY lacks official typed trait metadata.
3. The verifier fails if an implemented HASTE_READY card lacks a corresponding P4 HASTE_READY fixture evidence anchor.
4. The verifier asserts representative closure language includes `NOT READY` and `P0-005 remains open`.
5. The verifier must not claim full official Haste, full PaymentEngine, full card matrix or READY.
6. Existing focused / adjacent tests remain green.

## 5. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_BASELINE_EVIDENCE.md`.

Post-implementation:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~HasteOptional|FullyQualifiedName~HasteReady"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~HasteOptional|FullyQualifiedName~HasteReady|FullyQualifiedName~PlayCard|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Non-Goals

- Do not add new Haste runtime behavior.
- Do not implement battle keyword modifiers or non-hand haste granting.
- Do not update card matrix full-official status.
- Do not close P0-005, P1, frontend final validation or READY.

## 7. Handoff Verdict

4D-03AQ is ready as a test-only coverage verifier slice. It should add a small catalog-bound HASTE_READY guard to `PaymentEngineCoverageAuditTests` and keep project status **NOT READY**.
