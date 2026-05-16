# 4D-03EE-E E_CARD_MATRIX_READINESS Evidence-to-Row Mapping Verifier Audit

日期：2026-05-17
结论：**EVIDENCE-TO-ROW VERIFIER ONLY / MATRIX JSON LOCKED / PROJECT NOT READY**

本文件记录 4D-03ED-E 之后的 E-side card matrix readiness evidence-to-row mapping verifier。它只把 03ED-E preflight 中的 6 条 accepted upstream evidence category 映射为可审计的 card matrix row query、source manifest、blocker reason 与 current `fullOfficial=false` 证据；不修改 runtime、frontend、browser scripts、formal 18-step scripts、card matrix JSON、official card catalog、`fullOfficial`、READY 或 `riftbound-dotnet.sln`。

## 输入

- baseCommit=`2bb5af5a`（`test: 固定 03ed-e card matrix readiness preflight`）。
- input preflight manifest=`Post03EdCardMatrixReadinessMappingPreflightManifest`。
- input verifier evidence manifest=`Post03EcUpstreamOfficialClosureVerifierEvidenceManifest`。
- input dispatch manifest=`Post03EbCardMatrixReadinessDispatchManifest`。
- downstream owner=`E_CARD_MATRIX_READINESS`。

## 本批新增契约

- manifest=`Post03EeCardMatrixReadinessEvidenceToRowMappingVerifierManifest`
- classification=`post-03ed-e-card-matrix-readiness-evidence-to-row-mapping-verifier`
- concrete gate=`E_CARD_MATRIX_READINESS_POST_03ED_E_ACCEPTED_EVIDENCE_TO_ROW_MAPPING_VERIFIER`

本批固定 6 条 accepted upstream evidence category 到 card matrix row query 的映射：

| accepted evidence category | row query | expected functional units |
|---|---:|---:|
| broader-payment-engine-official-breadth | all-functional-units | 811 |
| full-official-resource-skill-row-interactions | payment-cost | 360 |
| keyword-payment-branches | payment-or-targeting-stack-timing | 548 |
| remaining-payment-windows | payment-cost | 360 |
| replacement-optional-alternative-tax-quote-command-audit-parity | payment-and-targeting-stack-timing | 256 |
| full-official-payment-engine-matrix | payment-cost | 360 |

每行必须保留 source manifest trace、source catalog rows、card matrix functional units、blocker reasons 与 current `fullOfficial=false` rows。该 verifier 不是 card matrix JSON authorization，也不是 `fullOfficial` upgrade。

## 锁定范围

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 不得修改。
- `data/official/card-catalog.zh-CN.json` 不得修改。
- Runtime、Contracts、Api、DevUi、Chrome / browser scripts、formal 18-step scripts 不得修改。
- `fullOfficial` / READY 不得升级。
- `riftbound-dotnet.sln` 不得触碰。

## 验收口径

当前 matrix skeleton 仍为：

```txt
snapshotEntries=1009
functionalUnits=811
fullOfficialTrue=0
ready=false
```

本批只允许证明 accepted upstream evidence 已能映射到 matrix row queries；`E_CARD_MATRIX_READINESS`、card matrix、P0/P1 与 READY 仍 open。
