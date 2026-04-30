# Current P2 Status

更新时间：2026-04-30

这是新窗口优先读取的短交接文件。它只记录当前开发状态和下一步，完整规则证据仍以 `docs/rules-evidence-index.md`、`docs/p2-rules-preflight.md` 和各 fixture 的 `rulesEvidence` 为准。

## Snapshot

- 当前 P2 功能基线：已覆盖 `OGN·071/298 次元门狂欢` 在当前 2P preflight 中对手选择“卡牌”或“符文”的两个 mode 分支；最新提交以 `git log -1 --oneline` 为准
- 上一个 P2 功能基线：`OGN·008/298 罪恶快感` 弃置友方手牌后按该牌法力费用对战场单位造成伤害
- 最近全量验证：`dotnet test Riftbound.slnx --no-restore` 通过 `329/329`
- 最小 card behavior registry：`156/811 = 19.2%`
- P2 preflight 清单：已完成到 `189`，下一项是 `190. 逐批迁移更多低复杂度官方卡牌`
- 当前工作区预期：只剩未跟踪的 `riftbound-dotnet.sln`，不要提交它，除非用户明确要求

## Current Focus

继续 P2 core rules preflight，逐批迁移低复杂度官方卡牌/模式。优先选择：

- 费用清晰、单目标或少量目标
- 伤害、摧毁、回手、抽牌、本回合效果
- 简单标签、装备对象、战力修正
- 能复用现有 `CardBehaviorRegistry`、`CoreRuleEngine` 和 fixture runner 能力的路径

暂不进入：

- 最终产品 UI
- 一次性全卡牌迁移
- 完整战斗/得分大系统
- 复杂 AI
- 移动端适配
- 提交规则 PDF/FAQ

## Latest Completed

- `SFD·077/221 火箭轰击`：新增 `DESTROY_EQUIPMENT` 模式，覆盖装备目标摧毁和单位目标拒绝。
- `SFD·135/221 紧急召回`：装备返回拥有者手牌，补 `EQUIPMENT_RETURNED_TO_HAND` 事件和单位目标拒绝。
- `OGN·022/298 热电光束`：摧毁所有场上装备，非装备单位不受影响。
- `SFD·162/221 血钱`：摧毁战场上不高于 2 战力单位后，按敌/友方分支打出 1/2 枚休眠“金币”装备指示物。
- `SFD·147/221 坠渊之流`：让所有当前场上单位和装备返回所属者手牌，并移除公开对象状态。
- `OGN·179/298 折戟再战`：当前 2P preflight 中按目标顺序记录双方各自选择一件自己的装备，双方让过后分别摧毁。
- `OGN·224/298 废物利用`：补齐选择一件装备时先摧毁该装备、再抽 1 张牌的分支；不选择装备分支仍覆盖。
- `SFD·005/221 印爆术`：摧毁装备后让目标控制者抽 2 张牌。
- `UNL-070/219 化为灰烬`：装备获得 `瞬息` 标签。
- `SFD·070/221 痛苦之酬`：战场单位 3 点伤害后打出休眠“金币”装备指示物。
- `OGN·069/298 背水一战`：友方单位本回合按当前战力翻倍并获得 `瞬息`。
- `OGN·180/298 逝水如镜`：战场单位或装备获得 `瞬息`。
- `OGN·094/298 精灵召唤` / `UNL-069/219 精灵迸发`：生成的 3 战力“精灵”单位指示物记录 `瞬息` 标签。
- `UNL-200/219 镜花水月`：在当前对象模型中打出活跃“映像”到基地，复制目标当前战力和标签并获得 `瞬息`。
- `OGN·008/298 罪恶快感`：弃置一张友方手牌后，按被弃牌 `manaCost` 对战场单位造成非致命伤害。
- `OGN·071/298 次元门狂欢`：当前 2P preflight 用 mode 记录对手选择“卡牌”或“符文”，分别覆盖双方各抽 1 或双方各召出 1 枚休眠符文。
- `UNL-165/219 暗影的召唤`：友方单位获得 `瞬息` 后抽 2 张牌。
- `OGN·264/298 游击战`：最多两张己方废牌堆待命牌返回手牌。

## Required Per Card

每个新增能力仍必须补齐：

- registry/card behavior 参数
- fixture，且包含 `rulesEvidence`
- conformance 测试
- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md` 的 fixture/进度项

长摘要文档只做短状态维护；不要再把完整已覆盖卡牌列表重复粘到 `START_HERE.md`、`phase-1.md`、`development-audit-status.md` 或 `master-development-plan.md`。

## New Window Read Order

默认只读：

1. `docs/CURRENT_P2_STATUS.md`
2. `README.md`
3. `docs/p2-rules-preflight.md` 的最近进度和相关 fixture 段落
4. `docs/rules-evidence-index.md` 中目标卡牌对应行
5. `docs/conformance-fixture-format.md` 中 fixture schema 规则

按需再读：

- `docs/START_HERE.md`：项目边界和总体目标
- `docs/master-development-plan.md`：阶段计划
- `docs/phase-1.md`：P1/P2 过渡状态
- `docs/protocol-semantics.md`：命令和事件语义
- `docs/rules-card-baseline.md`：卡牌基线和官方目录说明
- `docs/rules-authority-and-audit.md`：规则权威和审计原则
- `docs/development-audit-status.md`：模块级审计状态
