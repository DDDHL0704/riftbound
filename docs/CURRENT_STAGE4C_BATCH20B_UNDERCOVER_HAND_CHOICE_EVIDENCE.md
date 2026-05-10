# Stage 4C-20B Undercover Hand Choice Evidence

日期：2026-05-10

结论：**post-freeze evidence overlay only；NOT READY；不授予 full-official。**

## 1. Identity Check

本文件只记录阶段 4C-20B 的卡牌覆盖矩阵 / evidence overlay。E 不修改服务端、前端、审计文档、A checkpoint 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

Undercover Agent 在冻结矩阵中的身份：

| 项 | 值 |
|---|---|
| functional unit | `FU-6a52b04cb2` |
| cardNo | `OGN·178/298` |
| card | Undercover Agent / 《卧底特工》 |
| oracle/effectId | `UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT` |
| 4B freezeStatus | `NEEDS_ENGINE_SUPPORT` |
| 4B statusFlags | `IMPLEMENTED_UNTESTED`, `NEEDS_ENGINE_SUPPORT` |
| rules evidence | `CORE-260330 p62`, rule `422.4` |
| automated tests | `UndercoverAgentTriggerTests` |
| A validation | focused 6/6, backend full 3398/3398, frontend build, Chrome smoke, Stage 3 preflight, diff / JSON / matrix assertions passed |

Overlay key：`stage4CBatch20BUndercoverHandChoice`。

Per-FU overlay：`functionalUnits[].stage4C20B`。

Overlay status：`UNDERCOVER_HAND_CHOICE_PROMPT_REPRESENTATIVE_SHARED_IMPLEMENTATION_NOT_FULL_OFFICIAL`。

## 2. Representative Prompt Slice

4C-20B records this representative route:

`visible face-up field source -> Last Breath trigger -> Stack -> HAND_CHOICE prompt if 2+ hand -> CHOOSE_HAND_CARDS validation -> discard chosen / max possible -> draw two`

Shortfall boundary:

- 1 hand card: discard maximum possible by `CORE-260330 p62` / rule `422.4`, then draw two.
- 0 hand cards: discard maximum possible by `CORE-260330 p62` / rule `422.4`, then draw two.

Visibility boundary:

- hidden source: no trigger, no leak.
- face-down source: no trigger, no leak.
- standby source: no trigger, no leak.

## 3. Matrix Impact

| Metric | Count |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C20B` verified FUs | 1 |
| `stage4C20B` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 15 |
| cumulative state-based cleanup trigger enqueue verified FUs | 13 |
| cumulative hand-choice prompt verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

4B `freezeStatus` / `statusFlags` remain unchanged for `FU-6a52b04cb2`; `fullOfficial=false`.

## 4. Explicit Non-Coverage

Do not mark these as covered by 4C-20B:

- Karthus / `FU-ee1dfb3ed3`
- General discard hand-choice engine full-official behavior.
- Other hidden hand-choice FUs.

Still missing:

- Karthus optional trigger repeat.
- General discard hand-choice engine full-official coverage.
- Other hidden hand-choice FU review.
- Complete trigger engine beyond this representative prompt slice.
- Hidden / face-down / standby visibility regression beyond this tested guard.
- 1009 snapshot-entry / 811 functional-unit full-official coverage.
- Formal 18-step E2E.

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**
