# 2026-10-04 统一出牌流程实施记录

本记录对应 [下一批迭代计划](NEXT_NATIVE_PLAY_ITERATION_2026-10-04.md)。费用预览与共享法力计算已落地并通过全量回归；常驻牌确认及效果再次打出仍待后续阶段实施，不能以本次费用验收替代。

## 共享费用与只读预览

- `CoreRuleEngine.PlayManaCost.cs` 按中国区核心规则 356.2 至 356.4 先计额外费用和增费，部分减费作用于指定部分，总额减费再作用于合计；每个减费下限仅约束自身。现有费用族采用先高下限、后无下限的确定顺序，得到最低合法支付额；玩家显式选择其他减费顺序尚未提供。
- 提示的最低法力与实际出牌计划共用计算，删除 `MatchSession` 中相同费用族的重复实现。提示是假定可选减费被选择的下界，预览按实际选择计算。回响、急速和增益选项的可支付门槛同样对含额外费用的合计计算。
- `PreviewPlayCard` 使用当前认证身份、房间、prompt、tick 和选择；不存在的房间不会被创建。请求不扣费、不改变随机状态、不写行动日志，也不广播快照或事件。缺口按法力、符能、经验分别返回。
- 预览与提交使用同一个 `PlayCardPlan` / `PaymentPlan`；预览不提供支付授权凭证。提交重新计算当前状态。源牌、目标、位置、可选费用及资源动作继续由服务端验证。
- 原生 `PlayCardOverlay` 通过 SignalR 请求权威预览。变更选择后清除旧费用、禁用确认并重新核对；请求 ID、prompt 或 tick 不匹配的响应不会覆盖当前结果。费用明细滚动，选择与固定按钮继续沿用原生主题。

## 规则证据与验证

规则来源仍为已锁定的 [中国区核心规则 PDF](https://cdn.playloltcg.com/lol/2026/07/2026-07-23/6f47b6ebe57341a5bb5f4bed5548d051.pdf) 和仓库中国区官方卡牌目录。本轮在线官方读取失败，未确认官网是否另有更新。PDF 校验值和来源清单见 `data/official/upstream/2026-10-04/rules-manifest.json`。

| 验证 | 结果与证据 |
|---|---|
| 356.4.f，总额减费覆盖额外费用 | 修复前狂暴龙怪减费后的焚烧仍收取 1 法力法盾费用，回归用例失败；修复后合计为 0。`cost-order-red.trx` / `PlayCostPreviewTests.TotalReductionAlsoPaysSpellshieldAdditionalMana` |
| 356.4.e，各减费下限独立 | 官方“踊跃的学徒＋7 战力单位＋霹天雳地”示例修复前为 1 法力，修复后为 0。`cost-floor-red.trx` / `EachReducerFloorAppliesOnlyToThatReducer_Cn356Example` |
| 预览、支付、资源与相邻费用族 | 85/85，零失败、零跳过，`cost-order-verified.trx` |
| 全量 | 9339/9339，零失败、零跳过，5 分 6 秒，`unified-cost-full-verified.trx`。首次全量仅一处拒绝文案回归，已恢复具体“战场效果禁止”说明并复验 |
| 原生构建 | .NET / Godot 构建通过，零警告、零错误；五项现有原生门禁通过 |
| 旧响应 | Godot headless `PlayCardOverlayProof --verify-preview` 输出 `PLAY_COST_PREVIEW_FRESHNESS_PASS`；覆盖旧选择、旧 prompt、旧 tick、关闭后重开与不可支付结果 |
| macOS 实连 | 源码目录外启动导出应用，连接本机 15101；希维尔普通费用 4 法力＋1 紫色符能；急速后 5＋2，不补资源时确认禁用；选择回收紫色符文后允许提交，`COST_PAID` 实际扣 5＋2，余额 0＋0 |

测试结果位于 `tests/Riftbound.ConformanceTests/TestResults/`；机器可读证据、包校验值和截图索引见 `docs/evidence/unified-cost-acceptance.json`。

## 范围边界

本次不宣告完整费用规则、全卡牌或完整客户端验收完成。无视基础费用/仅法力/一切费用的效果上下文将在再次打出阶段接入；专用旧伏击路径还未统一到本次预览，保留明确拒绝而非报出可支付的错误费用。常驻牌确认时机、独立入场触发及三个再次打出效果仍按计划继续。

Windows 本轮只有可导出的包，未在 Windows 实机检查。GitHub 仍缺已记录的 workflow 权限，本轮不修改认证权限，未宣告远端 CI 通过。
