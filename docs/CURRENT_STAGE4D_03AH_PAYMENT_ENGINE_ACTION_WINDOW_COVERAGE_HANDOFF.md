# Stage 4D-03AH PaymentEngine Action-Window Coverage Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文件把 4D-03AF remaining-scope audit 中的 action-window coverage verifier 缺口重新排入 4D-03AH。4D-03AG 已先收口 `PLAY_CARD` typed prompt parity；现在需要一个窄的 conformance audit surface，把当前 PaymentEngine 相关 action windows 明确分类为 `representative-covered`、`policy-non-resource` 或 `remaining-gap`。

## Goal

Add a server-side verifier / audit test that enumerates current PaymentEngine action windows and prevents future slices from claiming P0-005 closure without explicit classification evidence.

Required action windows:

- `PLAY_CARD`
- pending `PAY_COST`
- `TRIGGER_PAYMENT`
- `ASSEMBLE_EQUIPMENT`
- `ACTIVATE_ABILITY`
- `LEGEND_ACT`
- battlefield held / score payment windows
- `HIDE_CARD`
- `MOVE_UNIT`

## Expected Classification Policy

- Use `representative-covered` for windows with focused prompt / command / audit / rollback evidence.
- Use `policy-non-resource` for windows that are intentionally non-resource movement or permission windows today, especially `MOVE_UNIT`.
- Use `remaining-gap` only when the gap is explicit, named, and tied to future work; do not hide it behind a green test.
- The verifier may pass with documented representative coverage and policy decisions, but it must not make a READY or full official closure claim.

## Suggested Write Scope

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_EVIDENCE.md`
- Current checkpoint docs only after implementation validation

Do not modify runtime code unless the verifier exposes a concrete prompt / command mismatch.

## Expected Test Shape

The test should assert:

- all required windows above are present exactly once;
- each entry has classification, evidence summary, representative test/doc anchors, and explicit closure status;
- no entry is silently unclassified;
- `MOVE_UNIT` is explicitly classified as `policy-non-resource` with movement permission evidence, not as a resource-payment window;
- `PLAY_CARD` includes 4D-03AG typed resource prompt parity evidence;
- the manifest/test text states P0-005 remains open unless final full official gates are satisfied.

## No-Go

- Do not update `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- Do not start LayerEngine or frontend work.
- Do not broaden card implementations.
- Do not close P0-005 / P1 / READY from this verifier alone.
- Do not run web data refreshes or change the fixed 2026-04-27 card snapshot scope.

## Acceptance

A acceptance requires focused verifier tests, relevant adjacent PaymentEngine / movement prompt tests, backend full test, `git diff --check`, and audit/evidence docs that keep the project **NOT READY**.
