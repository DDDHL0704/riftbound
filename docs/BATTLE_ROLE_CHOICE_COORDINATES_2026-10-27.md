# RuleChoice 完成时的战斗角色事件坐标

2026-10-27；②战斗/触发/胜负线，问题 [#188](https://github.com/DDDHL0704/riftbound/issues/188)、[#189](https://github.com/DDDHL0704/riftbound/issues/189) 保持 OPEN。

## 已复现的事实

固定输入为完整 `b3ed08568f6aab4aa638b1b9e1d38da50af9f64f`，父输入 `8e5192cc02e382624c53e227a15ae90cdf84a345`；复用③完整真实消费者，不复制旧 proposal 补丁。原始 MatchSession 接受 MOVE_UNIT → 获取资源 → 确认 PAY，命令完成 tick 为 1/2/3。

- 符文分支的真实角色事件是 tick 3/order 7，私有 scope 与三个首次角色记录却保留 tick 1/order 7。
- 金币分支的真实角色事件是 tick 3/order 5，私有记录是 tick 1/order 11。六条资源事件已在前一命令提交，最终命令剔除这些事件后，角色记录没有重定位。
- `MatchRecovery.BattleLimited` 对真实已接受日志的严格核对正确拒绝原结果。修复不改变该验证器，不补造、移动或改写已接受历史。

生产变更仅涉及 `ResolveAsync` 的 RuleChoice 最终返回、`FinalizeRuleChoiceBattleRoleCoordinates` 以及战斗限时技能的效果序列保存。等待中的 request/preview 继续使用原父命令的可重现输入；所有选择完整消费后，以最终保留事件的对象身份定位新角色事件，并写入最终 tick/order。此前已提交的 scope/role 字符串原样保留。触发器 ID 保持原生成身份；scope 的事件坐标不由 ID 文本反推。

额外真实战场技能消费者发现独立恢复问题：坚守关键词和私有 grant 被追加为 `[COMBAT_KEYWORD, BATTLE_LIMITED_GRANT]`，而 CardObjectState 的现有 JSON 构造器恢复为按序排列的 `[BATTLE_LIMITED_GRANT, COMBAT_KEYWORD]`。唯一差异就是这个数组顺序。现在技能生产端在保存 grant 时遵守构造器的 ordinal 顺序，保留严格的热状态/JSON 冷状态/重复意图哈希一致性检查。

## 验证口径

新增消费者覆盖实际符文/金币选择、最终角色出生、已有战斗身份在后续资源选择中保持不变、全部私有坐标及 P1/P2/观众投影协调一致的伪造仍被原日志拒绝，以及支付后选择战场坚守目标、真实双 PASS、grant 结算后的 JSON 冷恢复和重复意图。

有限局面从显式第 3 回合 fixture 开始。原消费者误要求它通过空房间出生审计，原始失败保留；现在明确要求该审计拒绝，同时从原有限初态执行完整命令/事件回放并比较最终状态。另一个独立消费者从真实 ROOM_SEAT/ROOM_SEAT/DEV_SEED_SCENARIO 开始，每次接受后 JSON 冷恢复并进入严格 registry；这证明原 QA 出生，不能称为普通合法牌组开局。

未改动全部原 FullGame 测试、现有严格恢复验证器或③拥有的 PayImposedMovementCost 方法。原五个 Gangplank 草稿逐字节保护并仅在本地定向编译中排除；其余全部 tracked Conformance C# 仍编译。新增测试自身的非法普通法术时机、误选大数数量分支和诊断目录缺失均保留原失败，修正夹具/驱动，不计生产规则修复。

固定代码/测试输入 `780677bbef84f8c57bd9f06ca32710971858e828` 实际完成 **171/171、0 fail、0 skip**，共 68 个实际方法；TRX SHA256 `556e0f0ace53c5ec17abbda5784ba603f2d1c77d314f32d96720a34c85a34a2f`。实际四模块 Info 均为该完整 SHA，必要 DLL 按运行时 SHA/MVID 留存。696 个 tracked Conformance C# 全编译，实际 Compile 708 项（含项目链接源、生成源和运行身份注入），五个外来草稿未编译且 hash 不变。此处只报告该固定输入 L1，不提升为 L2/L3。

## 可取回证据与交付边界

完整原件在本 worktree 的 ignored `var/qa/battle-role-coordinates-20261027/`：原始 `actual-journal-red-v1`、坐标修复后暴露错误出生预期的 `actual-journal-fixed-v2`、各原始边界失败、`grant-cold-diagnostic-v7` 的原状态/恢复状态唯一差异、最终固定输入、原 TRX/日志、编译清单、实际加载四模块 Info/SHA/MVID 及必要 DLL。最终机器清单和准确结果见 `delivery-v9/delivery.json`，不能仅凭哈希代替原件。

本包不解决③单独拥有的施加移动费用符文资源即时提交缺口，不默认适用于 PR183 其他输入，也不追加到在途固定候选。整支输入仍需该依赖修复与准确合并输入 L2。SQL、双玩家 SignalR、实际进程重启和完整矩阵留既定固定 SHA Actions；未运行的边界保持 OPEN。当前卡牌适用性/来源全集认证、整卡及全后端完成均未增加，净整卡认证增量 0。
