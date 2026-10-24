# N12 · VEN072 深沉咆哮

从 main `8ac0afba21574c94e7ce8a03ed83704524fee8e7` 独立移植精确CN37766/VEN·072/166 SC，2法力、橙色、迅捷；任意目标单位本回合+2，目标当前已强化则改+4。复用现有出牌/选择/费用/临时加成账本与到期流程。
2026-10-24重取官网API与完整卡图，记录SHA `ff3bae50e852aaca39993eafbb28217852087ce8cb9d883a412d77c93d62ca76`、图SHA `b961d91ce81d7624453310a1251d98e65590218e70ff53c851722d4fcf98146b` 与原核读相同；QA=null、勘误空、发布日期/生效日UNKNOWN，不合并全球身份。
共享差量仅CardBehaviorDefinition末尾可空PowerModifierAmountIfTargetEmpowered=null，以及ResolvePowerModifierAmount固定数值分支新增4行；删除这4行后Core与main逐字一致。原动态分支、caller、选取/代际/支付/时机/事件不变。当前②树该方法与main相同、tracked clean，无已观察到的同方法并发修改。
原32例使用VEN093/122真实消费者；显式最小依赖仅从PR148带入这两条已核对目录/注册，不带旧WIP祖先、其测试或共享隐私改动。本包共3条目录新增，原1040逐项/顺序/scopedAdditions保留，当前1043/功能分组842，原1009逐字段审计保持。
原14方法/32例属性、全部语义断言及辅助数据不变；私有夹具/记录驱动独立化，包含真实强化/反制/回手/法盾/对决/复制/负数/到期及原日志协调篡改拒绝。旧333/333分开归属。
本主线构建、定向及实际L2 CI待验；QA `var/qa/cards-direct-pr-migration-20261024/n12-main-port-v1/`，旧原件 `var/qa/ven-072-conditional-might-20261024/` 保留。普通牌组registry/整卡适用来源和完整认证未闭合，净整卡0/后端NOT READY。
