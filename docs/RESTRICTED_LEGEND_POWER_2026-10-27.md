# 受限传奇符能：当前用途切片与未完范围

Issue200 独立分支 `codex/payment-restricted-legend-resources-20261027` 基于 main `21b3e43fb7c3f7bb1c50e9957968890dc41b02ea`，不混入 PR203 的 No128/Hwei 或 No078。当前代码与测试输入为 `77e09ada4977b5d1496f36f8a66d125afd5d1891`；本记录后继只补说明。

精确五个 CN 卡面原件在本树 ignored `var/qa/restricted-legend-resources-20261027/source-v1/`：山隐之焰 SFD·189/244 的符能仅用于打出装备或装备技能，虚空之女 OGN·247/299/299* 仅用于打出法术。日期、全来源适用性仍 UNKNOWN。

此次直接取回[中文核心规则 PDF](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf)，SHA256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11` 与保留来源一致。430.2.a 规定获得资源技能不传递优先行动权或焦点；430.3 允许在启动打出过程前获得资源。仅核实这一精确文件，未宣称官网目录或全部适用性已重新闭合。

费用用途由实际服务端卡牌行为确定。匹配用途的受限彩虹符能用于费用分配、显式支出向量、授权、扣账、费用预览与对应提示；普通单位和用途不符的卡仍不能使用。真实资源命令保留原焦点/优先权，拒绝不变性、原日志、重连、去重与恢复安全检查保持。

各原件不相加、不改绑：

| 固定输入 / 原件 | 实际结果与范围 |
| --- | --- |
| c27 / neutral-resource-baseline-v2 | 原 14 例 9P/5F；五条中立取资源均拒绝，实际初始回合为3 |
| 1fd3ad82 / resource-purpose-framework-v7 | 47 例 42P/5F；用途资源池框架，非整卡通过 |
| 07712664 / resource-grant-and-timing-v8 | 56 例 46P/10F；原错误焦点与通用符能预期保留 |
| 同07712664 / complete-legacy-baseline-v10 | 25 方法、164 例 124P/40F；包括旧待结算牌型门槛与任意用途支付预期 |
| 7fcd2b04 / actual-play-counterexamples-v12 | 空目的地传入null造成编译失败，语义执行为0；后继按协议修为普通空字符串 |
| 8167e6cd / actual-play-counterexamples-v13 | 17 例 5P/12F；12条因预览夹具初始tick=1在正常恢复处停止，未到实际支付 |
| 3c5d0a79 / actual-play-counterexamples-v14 | 修正日志初始tick=0，17 例 13P/4F；四条匹配用途的预览/支付反例，八条用途拒绝及五条时点通过 |
| 77e09ada / actual-purpose-fix-v15 | 46 方法、168/168、0skip；四条原反例逐名转P，含实际法术/装备/单位、显式向量、受限池/法力、预览、原日志、重连、去重和恢复安全的定向检查 |

最后一阶段入口 `var/qa/restricted-legend-resources-20261027/actual-purpose-delivery-v18.json`。它引用原 TRX、discovery、plan 和对应保留构建 DLL；测试计划程序集哈希与保留字节一致，未声称额外采集了运行时模块 MVID。失败、编译失败和 fixture 停止点在 `original-and-new-counterexamples-v16.json` 各归原输入。

尚未闭合：所有装备技能用途、受限桶占用的总容量、完整可选费用交互，以及旧来源错误预期的逐项校准。原方法、Theory行、断言与拒绝守卫未删除；不能把旧164中的失败改记为本轮通过。该独立分支尚无新 PR/L2、SQL、SignalR、真实进程重启、完整胜局或矩阵 L3，通过范围不扩大到整卡或全后端。
