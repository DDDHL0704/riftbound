# Stage 4C-73 Plunder Alley Battlefield Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-90673ef9fd`
- 代表卡：劫掠船巷 / Plunder Alley `OGN·285/298` / cardId `31530`
- 代表 effect：`BATTLEFIELD_RULE_DOMAIN`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 development seed 中防守战场后选择一名防守单位移回基地的 battlefield-domain representative route。
- 本批不是普通 `PLAY_CARD` 路线，不声明完整战场卡文本、完整 `BATTLEFIELD_RULE_DOMAIN` 或 full-official 覆盖。

## 证据事实

- `BehaviorSpecCatalog` 已把 `OGN·285/298` 纳入 implemented battlefield rule card。
- `CoreRuleEngine` 已识别 `OGN·285/298` 为 `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE` battlefield trigger source。
- `MatchSession` development seed `battlefield-defend-move-to-base` 使用 `P2-BATTLEFIELD-PLUNDER-ALLEY` / `OGN·285/298`，并在 prompt 中暴露战场 destination 与防守单位 target choice。
- 既有 core tests 覆盖合法触发与脏 attacker-controlled battlefield source rejection。
- 既有 Hub test 覆盖候选暴露、非法多目标中文拒绝与合法提交后的 snapshot 区域变化。

## 验证

- focused Plunder Alley regression：3/3 passed。
- battlefield defend / held / declare / control adjacent regression：137/137 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明完整 `BATTLEFIELD_RULE_DOMAIN`、完整 `JFAQ-251023 p5-p6` 战场生命周期 / 清理裁定、完整 battle / spell-duel / assign-combat-damage lifecycle、全部 control freeze/release 与 zone movement permutations、full FEPR target / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
