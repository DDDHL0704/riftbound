# S7 施加移动费用、资源窗口与有界选量

问题：[Issue104](https://github.com/DDDHL0704/riftbound/issues/104)。本批为费用机制 L2；不是整卡认证或后端验收完成。目录保持本线1041条，不复用①1050条输入的结果。

## 官方范围

- 中文大陆核心规则，正文标明最后更新2026-10-10：[原PDF](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf)，SHA256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`。本批使用144.3、204.3.a、204.4.b/b.1/c、412.1/4、430.2/3、718.5.f；原件沿用已核S6收据，未修改文件。
- [官方卡牌API](https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb)，2026-10-24北京时间取得精确印次：搜魔人巡管UNL-163/219（SHA256 `15db1639ba070549bcd81f1787626169a89fffe71271670e2fdc696c40371661`）；顽皮触手UNL-054/219（`bec20ffd4b673ceafc138baf2e0e62db1c3e28095c9154a2fdf5898f65f27a20`）；斯弗尔尚歌SFD·059/221（`7284c7fc45b9095af57b7e5a62d619481cf3f10ccf9ad53e229119b52e8396cc`）。分别支持每名额外同时移动单位的费用、同控制者总战力≤8的移动组、武装独立复制宿主印刷技能。
- 同次API：远古簇碑SFD·117/221（`f7ae0159b19a45d5646f5f1e8d91b7ab15c669d9a9591abf678331798b63982b`）；海克斯异常体SFD·083/221（`fe8b6b1baf1b6b9a621f96fae8185fc2dcc6e1b46494664e194380c13d1a8a96`）。任意数量含0；受限法力不能输入簇碑，异常体输出还受普通+受限法力总容量约束。API精确记录没有附加QA/勘误，未提供发布/生效日期；不推造日期或扩大到其他来源公告。
- 可取回原件：本线ignored `var/qa/payment-imposed-movement-cost-20261024/source/*-current-api.json`、`*-source-receipt.json`、`core-reuse-receipt.json`；规则文本 `var/qa/payment-s3-20261023/source/core.txt`，PDF原收据 `var/qa/payment-restricted-mana-20261024/source/core-source-receipt.json`。最初“古老石碑”搜索为空的原件保留，不作为来源。

## 实现与共享边界

- `PayPlan.Other`及已有RuleChoiceContinuation/PAY_COST窗口实现施加费用。标准移动的已足额直接支付保持原路径；缺资源时开放合法资源来源；需同次横置的移动单位不能兼作横置资源来源。效果移动由实际执行者付款，独立于单位控制者及最后PASS提交者；足额也允许不付。
- 先获取资源再DECLINE是接受的行动：保留资源、横置/牺牲等已付费用，只跳过该移动组，不退原法术费用。最终付款前重算当前组、物体代际、控制/位置/许可及费用；标准组保持整体合法性。单个、顺序独立、返回基地不合并征税；移动依赖的后续效果仍依赖实际移动，独立指令继续。
- 费用先于位置变动和移动观察。活RunePools只沿既有移动调用传递，包括S5已经付款的局部池；不覆盖伤害/死亡/观察witness。巡管有效文字按已存在的物理实例、绑定代际与双方控制关系计数，不按CardNo去重，不改Bound或复制原语。
- 两种转换、普通激活与S5任意符能菜单复用有界二分工具：小菜单保留精确数字，较大菜单进入最多31次无资源提交的区间选择，0合法；最终数量仅验证/提交一次。选项绑定付款人、来源代际、能力/用途及父子区间；不新增DTO、普通stack、优先权或RuleChoice时钟覆盖。
- 候选符文使用实际池与共享容量的联合可达性，不构造不可达钱包。C1时保留所有单独合法来源，任选其一；同时选两个原子拒绝。实际余额与按色余额不变，虚拟总量是可达总量；仅0<C<候选总量时省略虚拟按色字段并给`paymentResourcePowerCapacity`。非约束形状保留旧字段；满池不展示无效回收来源。
- 严格恢复从真实authoritative state重建资源投影，校验C、总量、全部来源/每来源信息及字段存在条件；不全局允许字段缺失。`MatchRecovery.cs`共享差异仅在VisibleRuleChoiceState之前保存paymentPromptState并交原③资源hook第四参；已有显示余额继续用visible state。新逻辑在私有partial，不改N9/C7恢复比较。

## 保留的原失败与校准

- 79da基线完整10例7PASS/3FAIL：缺标准移动资源窗口、效果无力却实际移动、真实装配复制巡管只算一次；原Engine SHA `ad42dd41c686d4693fb2ed213b19ee386fb9006f2e4edbed990c13c823522b4f`，MVID `21d6f311-12e1-4ff7-af52-8a3316607793`，不是最终运行身份。
- v19原124例118PASS/6FAIL：保留真实符文的MAX符能普通提示发生Overflow；v25为17例8PASS/9FAIL：容量投影九类伪造未拒绝；校正反射fixture后的v30为29例27PASS/2FAIL：原ready/steal预览遗漏印刷+附加费用的联合需求。上述红不覆盖或改名。
- v15的847/848包含本次引入的EnergyChannel错误分类，已经按固定+1来源修复。v17旧“0不可选”断言按204.3.a校准，原方法/其他步骤保留。v27新守卫的默认JSON命名不兼容已修正，正常Web JSON和对象属性重排另有正控制。中间战场/位置/反射/事件计数fixture错误及日志均留在原目录，未当作生产红；详见ignored交付收据。
- 原FullGame九文件、143方法151例、11牌组110有向配对与seed424242保持；本机未启动完整矩阵或check/full。

## 本地验证与远端入口

- 最终受影响回归：635方法/1626例全部PASS，0skip（a15+本批WIP；源码哈希与实际运行身份分列）。原件 `var/qa/payment-imposed-movement-cost-20261024/final-affected-v33/`；仅本线精确源码/目录输入，不是全模块或整卡通过。
- 上述结果包含本批8类163例，原848例名称多重集全部保留；S7独立入口/API/Postgres三项目编译、help/identity均通过。API、独立入口与conformance四共享DLL字节相同，WIP Engine SHA `462aa462ff57f55bd3962a553a5d83302ec9c23722e167ab55b0ca382e628ba2`、MVID `f87b24fe-ebac-430c-b167-679c8fafe36d`、RuleInputIdentity `8b054ec45b720c3cea45942048ae105b13fe319d9f3860874e51cef10a476f04`；这不是后续固定SHA的二进制身份。原件 `final-build-v35/`、`final-affected-runtime-v35.json`，提交后的实际identity另记，不回写旧记录。
- 包含12,960种物理符文子集的独立钱包授权对照；容量0/1/2、同色/异色、当前wildcard、MAX数值、费用消费者、九类恢复伪造与正常JSON控制。另有标准/效果、复制绑定边界、资源后放弃、隐藏信息、冷恢复、原日志回放及有界选量0/1/63/64/65/MAX。
- 独立项目 `tests/Riftbound.ImposedMovementAcceptance`：`identity|sql|prepare|play|recover|finish|verify <before-restart|after-restart|final>|tamper`，`ConnectionStrings__Riftbound`自身限定loopback的`riftbound_acceptance`；网络限定loopback HTTP。只四个有限QA标签，不接受用户任意卡/余额/导入状态。
- 每例真实空房→两次EnsurePlayer/ROOM_SEAT→DEV固定场景，普通66或效果70法力+1符能；原始出牌支付后均66/0。获取Lux受限2、选择簇碑大数菜单，第一二分答案后重启：原池66/0、preview66/受限2/符能0，数量尚未提交。恢复必须真实双Joined/新token、旧意图幂等、恢复只读；finish继续选65后PAY/DECLINE，普通法力1、符能64/65，原符文仍在基地。效果组再真实打出Bullet Time，经大菜单选择0，保留符能和目标、受限法力剩1。
- SQL/tamper的实际收尾函数由新增四例无数据库测试直接消费，保留完整原出生与冷恢复/回放；独立原出生测试继续做自己的全部语义断言。待决payer/cost/preview/transcript/range/kind及完成后结构自洽符能伪造走fresh registry、原出生回放拒绝，逐次精确还原五payload行/七表摘要。
- 使用已交a15严格重连助手；公共Program/manifest/CI由①接线。真实PG、双身份SignalR、API进程重启、SQL篡改执行与本批check/完整受影响局仍待固定候选Actions。本机仅定向/受影响测试及编译/help/identity，不把这些升级成远端验收结论；净整卡认证增量0，原Goal继续ACTIVE。
