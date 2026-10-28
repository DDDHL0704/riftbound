# 指示中的变量符能费用：限定交付（2026-10-24）
范围：③费用时点、数量、资源、原生提示和既有续体；L2，净整卡认证增量 **0**，后端 Goal 继续。
| 功能身份 | 精确印次 | 来源/定位 | 源码 | 实际结果/未覆盖 |
| --- | --- | --- | --- | --- |
| 弹幕时间结算付款 | OGN·268/298 | 当前 CN 核心 204.3.a、740.4.a.1；下述来源哈希 | 规则 007a72da；InstructionPowerPayment、SelectedBattlefield 支付接点、原施放分支与提示；原 SQL 17 场接点 | 270 定向通过，2 个新增 QA 前缀修正后通过；SQL/实际 API/重启及整套门禁交① Actions |
来源：[核心规则](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf)，2026-10-10 发布/10-16 适用，10-23 回读 SHA256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`；本包不宣称全来源或新公告审计闭合。
行为：施放只付印刷费用并拒绝提前 SPEND_POWER；每次实际执行独立选择并支付 0..当次可用符能；可先横置/回收符文或使用已有反应资源技能；不提前伤害，被无效化或位置失效不收费，保留原伤害/死亡实现及零伤害加值规则。
续体：复用 RuleChoiceContinuation 的真实请求哈希、逐步资源选择、原命令重执行与冷恢复；资源决策在预览可见，原资源/费用事件随最终指令提交一次；不新增 StackItem DTO、恢复豁免或旧日志迁移，不让可选额外费用减免作用于本费用。
固定本地：规则程序集 SHA256 `eb614827936c1e1796465399d37a67a4e229ad4bd577e8c9add562d40a113d16`，MVID `201a1848-925d-4275-8c6c-abe6935626c9`，信息版本 +007a72da；专用入口实际加载记录与 Conformance 文件字节相同。
测试口径：29 新例＋161 原受影响例＋82 相邻例共有 272 个唯一身份取得 PASS；固定轮为 **270/272、2 FAIL**，两例保留真实席位/建场前缀并校准既有 DEV_SEED_SCENARIO:<id> 名称后 **2/2**。这不是单次 272 绿跑，也不代表整模块/check/full/完整对局通过；最早 3 红及全部中间失败原件保留。
原测试：方法/Theory 行、原数值/场所隔离/隐私/回放断言保留；旧施放费用改由实际结算 PAY_COST 执行。原 FullGame 文件与 4aabf78b 逐字节相同；原件及身份对照见 `var/qa/payment-instruction-power-20261024/{original-source,affected-originals-v1,fixed-007a72da-v2,qa-prefix-correction-v3,fixed-validation-summary.json}`。
远端源码入口：`dotnet build tests/Riftbound.InstructionPaymentAcceptance -m:1`；同候选、独立数据库配置 ConnectionStrings__Riftbound，专用 API 配 RIFTBOUND_SERVER_URL，`dotnet run --no-build --project tests/Riftbound.InstructionPaymentAcceptance -- <selector> <out> [stage]`；已编译/help/实际 identity 验证，本机未执行本包 SQL/API。
Actions 顺序：sql（原 17 场）→prepare（2 个独立账号/房间、原 ROOM_SEAT→DEV_SEED 场景，明确 QA 非普通开局）→启动 API→play→verify before-restart→实际停启 API→recover→verify after-restart（七表/双端视图/原资源重复不变）→finish→verify final→停 API→tamper；再由①运行模块/check/full/原受影响完整局与安全门禁。其余 S1/S2/S5、Lux 受限法力和全来源/整卡认证保持 OPEN。
