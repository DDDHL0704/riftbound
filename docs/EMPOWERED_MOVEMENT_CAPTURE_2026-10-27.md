# 已强化移动抽牌：逐文本实例捕获

2026-10-27，②战斗/触发/胜负线；[#190](https://github.com/DDDHL0704/riftbound/issues/190) 保持 OPEN。

## 规则输入和有限范围

当前精确 CN 37748 / VEN·057 的新鲜 API 与亲见整牌面一致：打出为 3 mana + 蓝能量 1，4 战力；强化仅支付 3 mana；已强化时自身移动产生独立抽 1 技能。canonical raw SHA256 `e4d8724224e2f6f5172ea7cbea1f8952b3e645dddd3e1e743051689da5e8cc2b`，整图 SHA256 `a857fee283ad2b1f58d369733d7da87254a64675b035c84eae29866f28a0bf59`。来源、请求、原响应、整图和回读证明在 ignored `var/qa/empowered-movement-20261027/source/`。完整适用性、发布日期/生效日期与官方来源全集仍 UNKNOWN；不把单张精确数据当作全量认证。

保留完整 `fc98919064de75fae058f667772a2d6939d5dcfb`、后继 `6be25a09679ad556199ffbd2a04cd92c49402b51` 以及完整 `acbbce7759f6fe42c7fd56d99e6b6a1f13325ebb` 的普通祖先，复用已交付复制移动生产者。①明确指派的四处实际冲突已取并集：CapturedBattleSourceIsPrivate 的 Stack/Trigger 两重载、OrderTriggerSourceObjectIdForViewer、BuildStackItemSnapshotView 同时保留 MovementObserver、EmpoweredBanish 及原 Death/Jinx/其余观察者保护。原冲突和逐段解法在 `composition-v1/`；没有选一侧覆盖另一侧。

## 生产行为

- TriggerParser 只在该移动抽牌子句前的 `{{已强化>}}` 上设置可空的 `RequiresSourceEmpowered`，不按整卡 Contains 门控其他子句，不按 CardNo 绕路。未门控旧条目的新字段不序列化。
- 每次实际移动、每个当前 RuleTextInstance 都独立读取宿主当前 IsEmpowered 与 Controller；满足条件才捕获技能，保存 `SourceWasEmpowered=true` 的事件出生事实。一次物理移动仅增加一次 movement count。
- 后续执行使用捕获条件和精确文本实例，不再查询来源当前是否已强化。实际返手重置状态/代次、卸除文本或恢复物理牌面不取消已经产生的独立技能。
- 恢复拒绝门控技能缺失/false 条件事实、未门控技能混入条件事实以及伪造复制绑定；完全协调的历史出生伪造仍须被原始接受日志回放拒绝，不能只依赖当前状态否认历史条件。

## 原件、验证与非通过分类

完整旧 23 行消费者在完整组合 `086adf62b979cd6eb241a9a0661283f0db1b124e` 上实际 15P/8F/0skip；8 个真正缺失出生原件均保留。生产 `10858d2442862caf7a5ead8e1ed67329301e33fe` 实际 23/23 通过，原 TRX SHA256 `a2cb65ab8ed9c40971542ded04a7459cb036264d0a3cdf8933dbfd17329c3362`，不重标为后续头。

额外共享消费者覆盖 0/1/2 个真实 Song 文本、同回合两次实际移动、实际 VEN137 复制牌面使用宿主而非原单位状态、赚得后卸除/返手、P1 与观众私区来源保护、结构一致但没有真实出生的攻击，以及原动作/事件回放和 JSON 冷状态相等。没有将直接改 IsEmpowered=false 描述为已执行的解除强化。

新增驱动/夹具失败独立保留：Task 返回签名编译错误；原生能力没有必填 abilityInstanceId；引用不存在的 BF 而真实场地是 F；4 战力来源不满足 OGN169 的不高于 3 门槛，正向返手改用实际 OGN104 并保留其召出符文效果；正向排序误用旧 TriggerIds 槽，④仅显式更正为 OrderedTriggerIds。这些不计生产规则缺陷，不删原方法/Theory/断言。

共享邻接固定输入 `4aeb69919078e1357624f30e9b615f4adf3b9485` 实际 278/278、88 方法、0 fail/skip；TRX `633dc614d0963624dad6ee3dc0a5c629ffbcc2100a4703d3f172b803aa305a9a`。实际四模块 Info/SHA/MVID/DLL 独立保留。该次旧 P6 选择串没有匹配，未计作该项执行；最终消费者核验使用准确 P6 方法全名，结果见最终清单。不同固定输入不相加、不重标。

最终消费者/P6固定输入 `660d849bb002bd94545020f90f0a587406090bff` 实际 **31/31、0 fail、0 skip**（完整原消费者 16 方法/30 行 + 准确 P6 方法 1），TRX `d2d134d63df5fcd4f1998465b0390ed1b33936cd1bf3cb1a3c2255e962e92a11`。实际四模块 Info/SHA/MVID/DLL 以该输入另存。690 个 tracked Conformance C# 均编译，实际 Compile 698 项；五草稿排除且 hash 不变。该输入与共享 278 的生产/官方数据逐字节相同；只新增完整消费者排序字段修正/文档，两个结果分别报告。

最终可取回机器清单见 `var/qa/empowered-movement-20261027/delivery-v9/delivery.json`。当前输入 1085 原始条目/866 功能组属于目录元数据；此前原件及五个外来草稿保持字节不变，全部其余 tracked C# 编译。

## 尚未闭合

本包尚未证明 VEN057 指定的同代次实际解除强化、其所有 token/胜负/全卡组合；VEN035 的真实解除/反向状态消费者另行处理。完整适用性、最新合并输入 L2、SQL/SignalR/真实进程重启/完整矩阵 L3、整卡和全后端认证保持 OPEN，净整卡认证增量 0。新增实现不追加至已在运行的固定候选。已收到 nightly 固定 main645a 的 8 个原 FullGame 及 2 个战斗 SQL 失败原件，后续独立最小归因；不能套用别处的 rune-destination 夹具结论，也不将局部本包通过称为这些历史失败闭合。
