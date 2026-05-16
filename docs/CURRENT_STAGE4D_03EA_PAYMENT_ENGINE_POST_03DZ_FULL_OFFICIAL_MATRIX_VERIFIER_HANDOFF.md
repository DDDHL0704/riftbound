# 4D-03EA PaymentEngine Post-03DZ Full Official Matrix Verifier Handoff

日期：2026-05-16
结论：**A-SIDE HANDOFF / PROJECT NOT READY**

## Scope

4D-03EA 承接 4D-03DZ。03DZ 已把 03DS residual owner lock `full-official-payment-engine-matrix` 派发到：

```txt
B_PAYMENT_ENGINE_FULL_OFFICIAL_MATRIX_POST_03DY_B_RESIDUAL_OWNER_LOCK_VERIFIER
```

本批只把该 scope 转成 B worker handoff / acceptance contract。它不修改 runtime、frontend、Chrome / browser scripts、formal 18-step scripts、card matrix JSON、`fullOfficial` / READY，也不触碰 `riftbound-dotnet.sln`。

## B Write Scope

Future B 只能围绕以下 test/docs verifier contract 写入：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- future 03EA verifier evidence docs / audit docs
- 必要的 A-side checkpoint / dispatch / completion docs，由 A 验收后同步

默认不打开 runtime 写锁。若 B 在 verifier 中发现必须补 runtime、frontend、matrix 或 Chrome / formal 脚本，必须回到 A 重新开 fresh dispatch。

## Acceptance Contract

B 必须证明 full official PaymentEngine matrix 不能再只停留在 dispatch / representative / aggregate proxy evidence。

最小验收项：

1. 每个 `OfficialPaymentEngineMatrixResidualManifest` axis 绑定 exact owner、remaining gap、required prompt / command / audit / rollback evidence。
2. 每个 `OfficialPaymentEngineMatrixSeedRowManifest` representative / missing / policy row 绑定 row status、axis、action window、payment source、prompt quote、legal command shape、Command revalidation、authoritative audit parity、rollback/no-mutation 和 nonclosure blocker。
3. 每个 `PaymentEngineOfficialMatrixDownstreamAggregateManifest` row 绑定 missing official row、downstream family manifest、all-window matrix manifest、covered action windows、excluded action windows 与 remaining official gap。
4. B 必须把 rollback failure、cross-window generation / consumption、card matrix alignment、keyword、resource-skill、target-tax matrices 作为 input evidence，而不是 closure。
5. 每个 row 继续绑定 source-card trace 与 card-row `fullOfficial=false` blocker evidence，且不能修改 card matrix JSON。
6. 4D-03DZ dispatch 与 4D-03DY-B quote parity evidence 只能作为 input evidence only；不得被升级为 full official PaymentEngine matrix、`E_CARD_MATRIX_READINESS`、card matrix 或 READY closure。
7. B 交付后 A 必须复核 diff、focused tests、必要 adjacent tests、docs anchors 和 non-closure wording。

## No-Go

- 不关闭 P0-005、P0-004 adjacency audit-sensitive、P1、broader official breadth、full official resource-skill row interactions、keyword payment branches、remaining payment windows、complete replacement / optional / alternative / tax quote-command-audit parity closure、full official PaymentEngine matrix closure、`E_CARD_MATRIX_READINESS`、card matrix 或 READY。
- 不把 03DZ、03DY-B、official matrix residual / seed / aggregate manifests、all-window downstream matrices 或 historical focused tests 当作 final readiness。
- 不修改 runtime、frontend、Chrome / browser scripts、formal 18-step scripts、card matrix JSON 或 `riftbound-dotnet.sln`。

## Suggested B Validation

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter PaymentEngineCoverageAuditTests
```

```sh
git diff --check
```
