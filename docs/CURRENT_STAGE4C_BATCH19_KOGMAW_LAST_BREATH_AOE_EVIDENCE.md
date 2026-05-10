# Stage 4C-19 Kogmaw Last-Breath AoE Evidence

日期：2026-05-10

结论：**post-freeze evidence overlay only；NOT READY；不授予 full-official。**

## 1. Identity Check

本文件只记录阶段 4C-19 的卡牌覆盖矩阵 / FAQ evidence overlay。E 不修改服务端、前端、A checkpoint、server audit 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

Kogmaw 在冻结矩阵中的身份：

| 项 | 值 |
|---|---|
| functional unit | `FU-af8b05c294` |
| cardNo | `OGN·190/298` |
| card | Kogmaw / 《克格莫》 |
| oracle/effectId | `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT` |
| 4B freezeStatus | `NEEDS_FAQ_REVIEW` |
| 4B statusFlags | `IMPLEMENTED_UNTESTED`, `NEEDS_ENGINE_SUPPORT`, `NEEDS_FAQ_REVIEW` |
| FAQ/rules refs | `JFAQ-251023 p7` |

Overlay key：`stage4CBatch19KogmawLastBreathAoeDamage`。

Per-FU overlay：`functionalUnits[].stage4C19`。

Overlay status：`KOGMAW_LAST_BREATH_AOE_DAMAGE_REPRESENTATIVE_ROUTE_NOT_FULL_OFFICIAL`。

## 2. Representative Route

4C-19 records this visible-source representative route:

`visible Kogmaw last-breath source -> TRIGGER_QUEUED -> ORDER_TRIGGERS for multi-trigger or single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED -> AOE_DAMAGE_RESOLVED -> DAMAGE_CLEANUP_RUN`

This is a representative route only. It records that the Kogmaw trigger can move through trigger queue / stack / priority and then resolve AoE damage with damage cleanup. It does not adjudicate every FAQ case, damage-prevention/replacement interaction, target/set edge case, or simultaneous-death cleanup branch.

## 3. Matrix Impact

| Metric | Count |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C19` verified FUs | 1 |
| `stage4C19` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 14 |
| cumulative state-based cleanup trigger enqueue verified FUs | 13 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

4B `freezeStatus` / `statusFlags` remain unchanged for `FU-af8b05c294`; `fullOfficial=false`.

## 4. Explicit Non-Coverage

Do not mark these as covered by 4C-19:

- Karthus / `FU-ee1dfb3ed3`
- Undercover Agent / `FU-6a52b04cb2`

Still missing:

- Kogmaw `JFAQ-251023 p7` FAQ adjudication.
- Complete trigger engine beyond the representative Kogmaw slice.
- Full AoE damage target/set/damage-prevention/replacement matrix.
- Post-damage cleanup edge cases and simultaneous deaths.
- Karthus / Undercover Agent coverage.
- 1009 snapshot-entry / 811 functional-unit full-official coverage.
- Formal 18-step E2E.

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**
