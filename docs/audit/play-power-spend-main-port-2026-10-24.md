# 出牌的实际符能来源分配 · 2026-10-24

- Refs issue118；父PR136/76049630，代码c860e4953a28287b33fb472b1578b473e0ad08ee。四个原生产文件和一个新内部规则类，新增测试；全部既有测试逐文件不变。
- 固定CN核心PDF `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11` 的135.2.e.5.a/b、356、445；九个精确官方API见scoped-authority-readback-v4.json。魔腾沿用已绑定勘误；伊泽瑞尔正文/errata措辞差异与父包API相同，保留原SFD FAQ范围，不按非空errata盲推字段先后。未提供的发布/生效日期保持UNKNOWN，不称全站来源审计。
- OptionalCosts最多一个`POWER_SPEND:<key>:<positiveInt32>,...`；key为A及blue/green/orange/purple/red/yellow，Ordinal、去零、无重复、ASCII规范整数，总数不超过int.MaxValue。A指实际彩虹余额；零费用只用省略选择。sourceRequirements新增powerSpendSelection语法元数据。
- 校验真实资源动作后的可用池、固定总费、各特性缺口和彩虹覆盖；PRINTED_POWER仍限制所需费用特性，各基础费回响独立分配，减费后判断可行性。逐次目标预检仍检查目标/模式/语法，整笔钱包仅在合并全部目标税后的外层计划授权，未取消权威检查。
- 原费用计划保留；实际扣款复用派生debit plan，保留法术限定法力、XP、资源动作和审计。预览显示所选实际余额，COST_PAID继续记录应付费用；无选择时保持原扣法，无新DTO/Frame/clock或支付暂停。保留S5结算付款与behavior.DamageAmount路径，不带回旧OptionalPowerCost伤害分支。
- 原父源六例3P3F，三个默认对照通过、三个合法替代向量不可表达；原方法/六行断言保留。早期夹具、编译和WIP失败均在原件索引分层保留，不算成本批语义红或补丁绿。
- 最终同一不可变DLL分两次5+596，集合互不重叠，共601/601、0skip：新增72、相邻原测试529。包含全部五种混合两点向量、七分量真实组合费、独立回响/减费、资源先验、非法输入、最大池、预览/幂等；独立资源指派枚举24384项/1903合法，限0/1七桶和通用费0..2，不称全整数穷尽。
- 普通出生由空SEATING/tick0/seed0及原25条配方开始，实际牌组双格式通过，真实回收获得green/purple后选择支付purple保留green。29命令全接受/102事件，普通validator、严格出生/全原日志replay、trusted Restore、三次registry正向恢复均通过；同总额换色并协调双方视图/spectator时，原commands/events/initial不变，实际registry拒绝后原房间继续。
- 现有OptionalCosts线级首尾空白仍INVALID_PAYLOAD且不写journal，新selector预览也拒绝；保留旧内部其他费用trim行为。温重试可重返原响应事件但无新写入/扣款，冷重试无新事件；既有冷拒绝码还原为UNSUPPORTED_COMMAND仍单独OPEN。
- 原件根`var/qa/payment-prs-20261024/play-power-spend-118/`，fixed-source-runtime-proof-v9.json SHA `cd6f2766a12222da5faed98cf64ffbfa38a8a3ff38a64f1b7cb4b3512122c707`。实际加载SHA/MVID/RIF均保留；native Info为7604+WIP，3042个编译输入精确等于c860源码，无身份或历史改写。
- 实际新L2规则CI、main集成及其他费用窗口仍待；未新增SQL/API/网络/进程重启/完整对局矩阵，L3归根夜间固定main。净整卡认证增量0，完整Goal保持active。
