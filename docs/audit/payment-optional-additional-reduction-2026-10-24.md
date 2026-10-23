# 可选额外费用显式减免：限定交付（2026-10-24）
范围：费用机制、实例绑定、报价、原声明效果与恢复；净整卡认证增量 **0**，Goal 继续。
| 功能身份 | 精确印次映射 | 来源版本/条款及哈希 | 行为/测试定位 | 风险 | 源码提交 | 实际结果/未覆盖 | 原件路径 | 净认证 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 可选额外费用减免 | SFD·149/221、149a/221；复制实例 SFD·059/221；消费者沿原 Theory | CN 核心2026-10-10（10-23回读）204.2/204.3.a/356.4.c/f.1，SHA d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11；SFD FAQ SHA d0b1e139a35e5bced809bf5656789b019692e07880d0265e99f9bd11d8179f01 | OptionalCostReductions、PlayManaCost/Preview、MatchSession；OptionalAdditionalCostReductionTests | L2 | 规则1e08030b；固定依赖8d671c08、6742c1f6 | 下述限定验证；整套门禁交① Actions | var/qa/payment-optional-reduction-20261023/ | 0 |
协议：原 OptionalCosts 明示每个技能实例/费用的 MANA 或符能选择，不选即原价；组件内归零不溢出，保留原支付声明；复制来源精确绑定 ObjectId/Generation/InstanceId/Origin revision，复用现有 Instances，不叠加旧宿主文本。
排除：基础费、法盾、装配/启动及指示中的费用；“弹幕时间”的旧提前支付时机留 S5，本包拒绝将它当作可减的额外费用。来源：[核心](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf)、[SFD FAQ](https://cdn.playloltcg.com/lol/2026/04/2026-04-15/铸魂淬炼系列_官方FAQ_260114.pdf)。
固定规则本地：56新增＋106相邻原例全部通过/零跳过；Engine SHA256 2567710c2b0e6afb54116f46ba63ec695a467e14af73dd48ff6edac80193a500、MVID bfba2e18-f4f8-4030-9891-910bdf2868c1、信息版本+1e08030b；不称整模块通过。
真实存储/网络：5显式新房间/29冷恢复；另2普通开局42/54指令，双认证实际API6286→9373重启，45/57日志命令、291/348事件；报价/实例/隐藏/拒绝/幂等/原日志/七表不变及2房间真实registry伪造结果拒绝通过，原payload恢复。
原件：fixed-1e08030b/{unit56,adjacent-v1,sql-v3,network-v1}；v16误分类正向预期、早期夹具/编译失败、验收脚本错误的“仍在结算链”假设均保留；SQL元组标签缺失另由sql-case-binding.json据原initial绑定，未改原收据。原测试来源保留，FullGame源码与4aabf78b逐字节相同。
CI源码入口：`dotnet build tests/Riftbound.OptionalCostReductionAcceptance -m:1`；同候选API/独立数据库，用`dotnet run --no-build --project tests/Riftbound.OptionalCostReductionAcceptance -- <sql|prepare|play|recover|verify|tamper> <out> [before-restart|after-restart]`；`--help`列配置，不依赖本地ignored驱动或长期数据库。
需① Actions：顺序sql→prepare→启动API→play→verify before-restart→实际停止/重启API→recover→verify after-restart→停止API→tamper；再承担当前合并输入的模块/check/full/原受影响完整对局及其余完整矩阵。源码入口已编译/help验证；本地原件不提升为当前合并候选全部通过或客户端兼容认证。
OPEN：S1/S2/S5、Lux受限法力、未来据守暂存费用及其余当前官方后端语义；既有消费者时机不在本包认证。仅本轮专用API/PG已正常停止，数据保留；新分工不等待上批full才继续下一独立包。
