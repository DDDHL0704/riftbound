# Stage 4D-03DP PaymentEngine Full Resource-Skill Row Interaction Matrix Verifier Handoff

日期：2026-05-16
结论：**A-SIDE HANDOFF / PROJECT NOT READY**

## Scope

4D-03DP 承接 4D-03DO。03DO 已把下一枚 concrete B-side official breadth scope 选为：

`B_PAYMENT_ENGINE_OFFICIAL_BREADTH_FULL_RESOURCE_SKILL_ROW_INTERACTION_MATRIX_VERIFIER`

本批只把该 scope 转成 B worker handoff / acceptance contract。它不修改 runtime、frontend、Chrome / browser scripts、formal 18-step scripts、card matrix JSON、`fullOfficial` / READY，也不触碰 `riftbound-dotnet.sln`。

## B Write Scope

Future B 只能围绕以下 test/docs verifier contract 写入：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- future 03DP verifier evidence docs / audit docs
- 必要的 A-side checkpoint / dispatch / completion docs，由 A 验收后同步

默认不打开 runtime 写锁。若 B 在 verifier 中发现必须补 runtime、frontend、matrix 或 Chrome / formal 脚本，必须回到 A 重新开 fresh dispatch。

## Acceptance Contract

B 必须证明 current 32 official `RESOURCE_SKILLS` family rows 与 03CV 的 6 个 row-interaction dimensions 组成的 192 个 interaction surfaces 不能再只停留在 representative proxy evidence。

最小验收项：

1. 每个 current official `RESOURCE_SKILLS` row 绑定 exact source-card group、ability id、03CX source-card parity、03CY runtime/card-row evidence、03DG family verifier 和 03DN current-lane closure。
2. 每个 03CV interaction dimension 都有 executable verifier evidence：prompt quote、Command revalidation、audit parity、generated-resource lifetime、rollback no-mutation、official matrix trace。
3. 每个 row 继续绑定 card-row `fullOfficial=false` blocker evidence，且不能修改 card matrix JSON。
4. 03DN / 03DM / 03DL 只能作为 input closures only；不得被升级为 broader official breadth、P0-005、full official PaymentEngine matrix 或 READY closure。
5. B 交付后 A 必须复核 diff、focused tests、必要 adjacent tests、docs anchors 和 non-closure wording。

## No-Go

- 不关闭 P0-005、P1、broader PaymentEngine official breadth、full official PaymentEngine matrix、full-card matrix、card matrix readiness、frontend final reruns、`fullOfficial` 或 READY。
- 不把 03CV 192-row matrix、03CY runtime/card-row evidence、03DG verifier 或 03DN closure 当作 full official `[A]` / `[C]` row-interaction closure。
- 不修改 runtime、frontend、Chrome / browser scripts、formal 18-step scripts、card matrix JSON 或 `riftbound-dotnet.sln`。

## Suggested B Validation

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

```sh
git diff --check
```
