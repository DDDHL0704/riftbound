# N17 · CN VEN013 暗影刺客与完整 VEN012 相邻前置

完整干净前置为 PR159 `47209c78954a55080556dceaa65722d4e0c619ea` 与 PR156 `0ef35c1d3892fcbd244a9034d9b666a30a48833f`，合并基底 `8f4c68759c7caf14f9e49b67f0e7be17eca5a551` 已包含 main907、入口、原预组补位、VEN008/093/122、VEN010同名判断及VEN103返手的完整源与测试。两边共同1043条记录和来源元数据逐项相同；只对各自新增记录做精确并集，没有旧累计WIP祖先。GP086/181仍只是已知同名的来源/基础注册前置，其C7效果未认证。

CN37698 / VEN·013/166 SC，暗影刺客，5法力/0符能/5战力。唯一印刷指示是自己废牌堆存在同名牌时活跃进场，不消耗该牌。当前CN官方API于2026-10-24T08:17:18Z刷新，规范记录SHA256 `0843eac06a158f4949b579b3eecee13b6e0f89e1927f5c264c04a5c2cbcb99d4`，图像SHA256 `59003d53d6812ac8729aa8863d7ecb4de3001aa48f962248cf5fe4f69b235dfc`，与原f52bab3c源完全相同，完整卡面已读。

旧150例邻域中2例来自VEN012。为保留这些原身份/断言，本包同时迁入该完整消费者：CN37697 / VEN·012/166 SC 表里杀缭乱，普通3法力/1红色符能，活跃目标与本回合强攻3分别生效，流转3红色从墓地打出后放逐。官方API于2026-10-24T08:23:12Z刷新，规范记录SHA256 `a0c46c50d144154b521b7f5bfb69114d69e0641d1489c1d5b9d7a19029925fed`，图像SHA256 `d4e299cf3159646df6660309b193ec671022630c5c09fa8dfbcfd3ac777ef9c9`，与旧源相同，完整卡面已读。其全部8测试方法/45例和2个数据提供器保留，复用主干已有流转、活跃、独立临时强攻及禁止活跃机制，无额外生产分支或临时牌。

两张卡该API的QA/勘误字段为空，发布/生效日期与跨地区对应关系UNKNOWN。新增两条精确来源后1050条/849功能元数据单元，父包1048条及顺序/来源增量、重复组115/316/201和Ahri全部1009历史字段检查保持。这些数字不构成适用分母或整卡认证。

N17原13方法/38例全部断言与Theory行保留；只将旧未迁移fixture/driver限定到既有159测试辅助方法。159的At/UnitEntryPosition/Driver仅由private改为internal，函数体和该父包全部断言不变。VEN012原重放调用增加终态JSON恢复与实录，原直接规则拒绝断言原样保留并另存实录；不把直接Core拒绝称为持久化或传输入口证明。

共享接点修改前读取②③实际SourceUnitEnterReadyRequirementsSatisfied、StaticAbilityParser.Parse和StaticAbilitySpec记录，内容与本基底完全一致。新条件仅为末尾nullable静态规格、精确中文句解析、既有进场条件读取当时控制者的真实墓地与完整已知名称；不改变UnitEntry算法、事件/MatchState/权限/选择/费用。未动③ValidPermanentPlayTargetStack。

当前先冻结父Core/Parser的消费者前沿：仅保留新增nullable规格字段使原元数据断言可编译，6个原进场场景与完整45例VEN012将用独立输入实测。首轮实际51例为43通过/8失败：013有同名的4例未活跃是预期前沿，012另4例在新增JSON往返哈希失败。其原玩法断言均已通过；手工with追加法盾tag绕过构造器排序，而JSON构造器按Ordinal规范化，故仅规范两处初始tag集合顺序，全部原语义断言/数据行保留。更正夹具后的父Core/Parser前沿尚待执行。源码、旧原件、后续独立证据和TRX位于ignored `var/qa/cards-direct-pr-migration-20261024/n17-main-port-v1/`；来源位于相邻`n17-current-source-v1/`。

本固定源未包含PR157的新ErrorCode恢复契约；后续重基到该契约时，自有journal→RecoveredCommand投影须传递实际entry.ErrorCode，不推断/回填旧历史。日常合入以最新PR head/base/merge输入的L2 required全绿及机械核验为准，夜间L3独立。本机不新跑full/check/SQL/网络/110场矩阵。净整卡认证增量0、backend NOT READY，1740印次/14293条款和适用功能分母UNKNOWN保持。
