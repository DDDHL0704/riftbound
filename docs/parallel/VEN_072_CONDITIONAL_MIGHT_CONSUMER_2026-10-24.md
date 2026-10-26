# N12 · VEN072 深沉咆哮

从 main `8ac0afba21574c94e7ce8a03ed83704524fee8e7` 独立移植精确CN37766/VEN·072/166 SC，2法力、橙色、迅捷；任意目标单位本回合+2，目标当前已强化则改+4。复用现有出牌/选择/费用/临时加成账本与到期流程。
2026-10-24重取[官网API](https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb)与完整卡图，记录SHA `ff3bae50e852aaca39993eafbb28217852087ce8cb9d883a412d77c93d62ca76`、图SHA `b961d91ce81d7624453310a1251d98e65590218e70ff53c851722d4fcf98146b` 与原核读相同；QA=null、勘误空、发布日期/生效日UNKNOWN，不合并全球身份。
共享差量仅CardBehaviorDefinition末尾可空PowerModifierAmountIfTargetEmpowered=null，以及ResolvePowerModifierAmount固定数值分支新增4行；删除这4行后Core与main逐字一致。原动态分支、caller、选取/代际/支付/时机/事件不变。
原32例使用VEN093/122真实消费者；显式最小依赖仅从PR148带入这两条已核对目录/注册，不带旧WIP祖先、其测试或共享隐私改动。本包共3条目录新增，原1040逐项/顺序/scopedAdditions保留，当前1043/功能分组842，原1009逐字段审计保持。
原14方法/32例属性、全部语义断言及辅助数据不变；私有夹具/记录驱动独立化，包含真实强化/反制/回手/法盾/对决/复制/负数/到期及原日志协调篡改拒绝。旧333/333分开归属。
固定源码 `1756204e39d1a52c27611759e29ccd42f285a23a` 全项目构建零警告零错误，定向272/272、零跳过；原32个TRX名称与重数等于旧32，原14方法属性/数据/语义断言逐字保留。相同冻结运行文件另跑原11牌组跨双格式输入FQN，1/1，未跑110对局矩阵。31最终夹具205命令（192接受/13拒绝）、592事件；3中途前缀分列，2种原日志/账本协调篡改拒绝。逐命令原回放/低层帧恢复/重复幂等/拒绝不变性，以及导出场景、复制和回手边界的JSON恢复通过；两次到期Save场景保留逐命令帧恢复和原日志回放。2488选取输入与1417运行文件前后核等，加载路径/SHA/MVID及RIF `6900a2cdafcd92dd50e0826556ad8cab6dd63832a433e7556f6ae7b2b08463f3` 另核。
独立反例 `6d3c3c12aeee3075bc36b1f812be1e1d449afd08` 仅将Core还原为字节一致main：相同目录/定义/原测试两例，未强化1过，已强化1败（应4/实际2）；3份真实输出在断言前保留，非候选回归失败。其源码/bin/RIF `6a3064aae4ea7657d1917e05a925b8c999966622e39fd81590cb4977a48c1443` 与候选分列，未推送或进入PR祖先。
本批收据 `main-source-test-receipt-v1.json` SHA256 `bf2d6253ad941bcc76736cce04af8cae77d4102fb31744066a204d3fd450533b`；QA `var/qa/cards-direct-pr-migration-20261024/n12-main-port-v1/`，旧原件 `var/qa/ven-072-conditional-might-20261024/` 保留。按L2等此PR实际普通规则CI后合并；未新跑SQL/network/full/矩阵，普通牌组registry/整卡认证未闭合，净整卡0/后端NOT READY。
