# 当前项目状态 · 2026-10-28

## 日报快照 · 2026-10-28 09:35（Asia/Shanghai）

本快照记录当时可核实的事实；main 为 `e9530c14d03cb34849bc5dd8ca1c53533679f9df`。数字会随 PR 和 CI 变化，快照不是对后续 main 或完整后端验收的证明。

| 指标 | 结果与范围 |
| --- | --- |
| 待合入 PR | GitHub 全仓库当时 open **7** 个，其中创建满48小时 **1** 个（#158），draft 3 个。#142/#153/#157/#161/#163 在分别记录已吸收原因后关闭，GitHub 状态为 closed、非 merged。 |
| main L2 耗时 | 上海时间10月27日创建且截至快照已完成的 main push L2 共5次，中位 **149分25秒**，最长 **172分25秒**；按 createdAt 至 updatedAt 计，包含排队，且样本包含优化前运行。 |
| 夜间验收 | [定时运行 40591520309](https://github.com/DDDHL0704/riftbound/actions/runs/40591520309) 固定 `c25e778`，总体 **失败**。受限资源归费用线，整局和 Draven 归战斗线，Boon 归卡牌线，集成线跟踪；修复后的完整 L3 尚未运行。 |
| VEN 已接入张数 | **待核实**。适用卡池、接入和整卡认证的口径尚未统一，不能用来源存在或局部测试数代替。 |
| 已核对规则条款数 | **待核实**。须先固定项目清单中的统计单位及可追溯关系，来源引用数不等于语义核对完成数。 |

近期四次已完成的六分片 main L2 约44分39秒至97分37秒，中位61分34秒，只代表该样本。日报每天先更新本地记录，随正常集成 PR 同步到本文件，不单独触发一轮 L2；新样本须记录时间窗口、数量和原始运行链接。

本次耗时样本为 [40433982118](https://github.com/DDDHL0704/riftbound/actions/runs/40433982118)、[40449789952](https://github.com/DDDHL0704/riftbound/actions/runs/40449789952)、[40458851676](https://github.com/DDDHL0704/riftbound/actions/runs/40458851676)、[40500054422](https://github.com/DDDHL0704/riftbound/actions/runs/40500054422) 和 [40529284886](https://github.com/DDDHL0704/riftbound/actions/runs/40529284886)。

## 历史快照 · 2026-10-27

**后端规则优先；日常合入走 L2，完整后端验收仍 OPEN。**

原有限清积压已按 [PR165](https://github.com/DDDHL0704/riftbound/pull/165) 完成：12个原完整head及公开head均为main祖先，12个PR实际merged，开放曾核验22→10；本轮新增功能PR冻结条件已达成。①保持无Goal、无监控，按实际交付/冲突/CI终态接续有限批次。

- **实际main：** [PR164](https://github.com/DDDHL0704/riftbound/pull/164)已普通合入`3839e13547b47f15983bbe5e82fc2f75c9ee2fb6`，parents为原main645a与完整de286，tree与其L2实测synthetic1496e466完全相同。来源是[完整L2 40379433690](https://github.com/DDDHL0704/riftbound/actions/runs/40379433690)：定向7,310方法/13,735例、规则8,262/15,795，零fail/skip；4个ZIP对平台digest及原始plan/discovery/TRX已独立核验。[main自身L2 40433982118](https://github.com/DDDHL0704/riftbound/actions/runs/40433982118)另计，尚不复用PR终态。strict up-to-date、Actions15368、管理员约束和人工批准0保持。
- **支付接受接线：** S3/P7真实SQL、双玩家重连与进程重启[harness40379433088](https://github.com/DDDHL0704/riftbound/actions/runs/40379433088)自然成功，6份ZIP已核平台digest并归档。16phase实际记录同一1496输入、4个实际API正常exit0、通用重启原SQL历史保持；Engine为运行捕获Info/SHA/MVID，未声称归档了该DLL字节。原62c/180e及更早harness各归自己的输入；这些有限接受不等于完整L3。
- **错误码投影修复：** [Issue191](https://github.com/DDDHL0704/riftbound/issues/191)保留原62c定向134红及ac0同134反例。de286仅让12处测试日志投影保留实际ErrorCode，TypedCommand、RuleInputs、原方法/Theory/断言及生产恢复校验不改。首轮364P/24个VEN105观察器输出路径配置失败原件保留，同DLL补验VEN10576/76另计。旧183规则168红均已在de实际L2逐项通过，仍不改标旧183输入。
- **PR生命周期：** #142/#153/#157/#158/#161/#163完整head均已成为main祖先，但原PR仍指向旧开发分支且API实际OPEN；#142改到main被GitHub因无新commits拒绝，未手工关单，不能把这6记为实际merged。#147/#150虽在GitHub已merged到138开发分支，其完整head尚非main祖先；#138后继仍按自己的最新base/head/synthetic L2串行发布。
- **下一有限候选：** [PR183](https://github.com/DDDHL0704/riftbound/pull/183)组合完整VEN110/独立弃牌与复制死亡、完整VEN130当前控制者强化光环及实际main3839的费用/日志投影依赖。ac0原519方法/1,022例通过及其另22方法4P/134F分别保留；本次补验证和最新L2另归新组合，不能复用旧合并输入。原1083目录记录逐项保留，1084/865仅metadata、认证增量0；VEN110地区来源冲突与适用性仍OPEN。S7及VEN057/复制移动不纳入此固定候选。
- **L3与完成边界：** 每天02:00上海时间只固定一次main完整SHA，保留完整规则、69项SQL/四片、17项selected-field、真实SignalR/重启与原日志回放；原18个精确L3方法及两条完整对局冒烟保持。普通消费者变化不额外手动触发重型harness，L2不依赖L3。来源适用性、全部规则语义、整卡与固定输入完整后端验收仍OPEN；客户端、可见双端、安装包及Windows实机留后续阶段，旧暂停Goal不记完成。
- **职责：** ①CI/验收工具/治理及实际同方法交集；②战斗/触发/胜负与恢复；③费用/资源/合法性；④卡牌消费者/注册。普通helper不额外审批，只发完整交付、真实冲突或阻塞，不发ACK。后续移动包的四处实际隐私方法交集已由②集中普通组合，保留放逐与移动观察者两侧保护；实际checkout操作前核对Git。

[工作流](RULE_TEST_WORKFLOW.md) · [目标与分工](BACKEND_RULES_GOAL_2026-10-23.md#四线与共享接点) · [治理](PROJECT_GOVERNANCE.md) · [来源历史入口](audit/current-manifest.json)。历史固定运行/原始失败/源/bin及数据库保留；旧B1/source queue仅作迁移清单。
