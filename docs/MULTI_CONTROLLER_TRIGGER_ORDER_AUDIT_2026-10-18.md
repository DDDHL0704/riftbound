# 双控制者的限定同时技能顺序

CN / zh-CN，2026-10-18取得与观察，适用日期冻结为2026-10-16。本批仅审查并验证所列中性、无目标技能的本方排序权限及控制者交接行为；双方目标选择、可选支付、实际入链时点、同一命令内更细获得时序、战斗角色以及整卡语义均未闭合。全量1740精确印次、中国区1504、英文确认1408/声明1416缺口8、36文档/14293正文块、四项原来源时序增量及国际凯隐身份门禁保留。

## 来源和原错误

[来源manifest](evidence/multi-controller-trigger-order-2026-10-18/source-manifest.json)记录现行官方规则列表14行及15项相关卡牌精确记录，包含官方API、请求、取得时间、原始压缩内容及哈希、地区/语言和各自身份。核心PDF发表于10月10日、10月16日生效，流式取得40278538字节，SHA-256为`d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`，未复制PDF；383.3.d及383.3.d.1要求各控制者选择其自己的同时技能顺序，按相应APNAP顺序办理。动态网页未含PDF链接不解释为来源移除。15项卡文与运行快照字节相同；不从取得日期推定未公开的发布/生效日期。

[实际固定f199原红例](evidence/multi-controller-trigger-order-2026-10-18/actual-fixed-f199-ordinary-red.json)在真实Release `5d3a7038…37cc55`下，从合法空白局按原70命令/231事件及62命令/207事件前缀完整回放，双方各有两个维克托技能。先手能改对手顺序，并在自己一次确认后直接清空队列、跳过对方选择。原行动和事件均可回放不代表语义正确。[原Debug红例及模块](evidence/multi-controller-trigger-order-2026-10-18/original-red-retention.json)另为实际`81c2a72c…91fa6d`；两个执行身份独立保留，不能混用。

## 有限共享修复

沿用共享队列、控制者块、APNAP及原逆序入链流程。服务端只允许当前控制者调整自己的同时技能，其他控制者顺序必须逐项保持。首方确认记录本方顺序并保留队列，次方接到自己的提示；末方完成后按既有流程入链。队列序列化及标准化保留确认标记。独立恢复校验另外检查标记所属、多技能资格、完整控制者块、合法APNAP前缀和获得时钟；原始日志审计继续独立阻止协调伪造。没有整体重写、通用DSL扩张或旧开发局迁移。

原生适配器解析服务端可排序ID，控件只启用本方相邻技能的上下按钮；强制发出对手按钮事件和绕过按钮的直接适配器提交也不能改变对手顺序。本批未改变页面布局。[八条实际生产控件命令](evidence/multi-controller-trigger-order-2026-10-18/native-controls.json)由真实Godot生产弹层发出并由引擎消费；此headless证据不提升为可见双端或Windows实机通过。显式中盘的原生提示夹具含三个特性，仅用作按钮/命令验证，不能当作合法空白局证据。

## 执行证据及原测试保留

[最终focus](evidence/multi-controller-trigger-order-2026-10-18/focus.json)分两次实际执行：共享相邻2445例/1839方法，以及合法空白开局与原生命令相邻384例/259方法。各自程序集身份保留，不相加冒充一个测试程序集。新增19例/5方法覆盖双方首动、每方独立正反顺序、错玩家和更改他方顺序拒绝、持久化标记伪造、原生按钮/适配器及冷恢复。

[八种普通合法空白组合](evidence/multi-controller-trigger-order-2026-10-18/ordinary-eight-combinations.json)合计608条接受命令/1936事件，32处冷恢复及全原行动/事件回放通过。两副合法40张、双特性QA牌组通过两赛制校验，以普通提交、准备、调度、资源、单位打出、移动与伤害到达排序，不注入状态、手牌或RNG。新QA种子0及牌组顺序单独登记；原11副牌组和424242不变。

[真实SQL](evidence/multi-controller-trigger-order-2026-10-18/real-sql.json)覆盖两个普通空白房间，合计152命令/484事件、20冷恢复、6条原下一命令及12次拒绝。每局独立测试部分标记、错误首方、全部提前确认、协调清空标记、协调替换已确认顺序及错误实现身份；四次协调篡改能通过结构校验，仍被原始日志审计拒绝。拒绝不写库、不留缓存，七表精确还原；20个先前房间及原生数据库原帧七表不变。复用原专用数据库并正常停止，不另造数据库或源码树。

[实际执行输入](evidence/multi-controller-trigger-order-2026-10-18/actual-compiled-inputs.json)由PE MVID独立读取并复算：当前Debug实现`41711827d112a646ec4a56b3979a155d146ffeb368441fde25847b78ab5f1a7e`、组合身份`119ca62449b526ba297cd37e485443c658c62dcf4425ad7686349ce35da0bc16`；Conformance、独立SQL读取器及冻结四个核心模块/三份数据逐字节一致。固定f199 Release仍是`5d3a…`，不以源码相同替代真实执行身份。

[原五场及全部110配对](evidence/multi-controller-trigger-order-2026-10-18/original-five-and110.json)全部通过，实际完成集合与110期望ID精确相同，原11副牌组、424242、两赛制完整有序数组及原胜利/事件/回放断言保留；原实际Test/API DLL/PDB和执行输入另存。唯一一次[check](evidence/multi-controller-trigger-order-2026-10-18/one-check.json)12474例/7340方法、705.42秒全部通过，零失败/跳过。原12455例多重集合及7335方法完整保留，净增19/5；12618份输入在运行及即时核验期间完全不变，没有第二次check。[55项工具测试](evidence/multi-controller-trigger-order-2026-10-18/tooling-tests.json)通过。

[保留收据](evidence/multi-controller-trigger-order-2026-10-18/test-retention.json)：原456份Conformance中455份逐字节相同；唯一旧代币Theory仅插入次方确认的3行，原所有行、两个Theory行及最终断言仍在。1277原夹具及八份FullGame源码/143方法字节相同，原SQL32份中31份字节相同，Program仅追加新入口和消费者，全部31项旧默认顺序保留。首次修复漏复制队列确认标记造成的7失败、旧代币少确认的2失败，以及新开局帮助方法使用命令别名错误造成的8失败均保留原TRX、实际源码和身份；分别修复后由最终focus及check覆盖，不重写失败历史。

## 固定CI、来源边界和产品门禁

[固定f199完整CI39095829877](https://github.com/DDDHL0704/riftbound/actions/runs/39095829877)实际success且已[独立核验](evidence/multi-controller-trigger-order-2026-10-18/previous-fixed-f199-full.json)：12604例/7476方法、143原完整方法/110配对、93真实SQL路径/90收据、网络15跨身份拒绝/零泄露及双平台实际字节/启动日志。[更早固定91完整CI39088568831](https://github.com/DDDHL0704/riftbound/actions/runs/39088568831)也实际success并独立核验12580例/7470方法、91SQL路径/89收据及相同独立门禁。二者都只覆盖各自固定提交，不覆盖本批417输入；本批新固定完整CI仍待提交运行。

此前取得的[五个共享牌堆消费者来源](evidence/multi-controller-trigger-order-2026-10-18/five-shared-consumer-source-observation.json)及[影响分析](evidence/multi-controller-trigger-order-2026-10-18/five-shared-consumer-source-impact.json)一并发布，明确记录存储路径迁移及原观察manifest哈希。五项现行记录与已取得CN16来源相同；卡牌戏法当前API id38015与运行目录旧id31413是已登记目录采用边界，不伪造为新时序变化，不静默改目录，不合并身份，也不认证整卡。

[产品边界](evidence/multi-controller-trigger-order-2026-10-18/acceptance-boundaries.json)明确：目标选择、可选费用、实际入链时点、更细命令内时序和战斗角色另OPEN；当前可见双客户端、真实网络重连、正常得分结束及完整原生回放和Windows实机仍OPEN。Mac工具再次实际报告锁定，已有解锁请求保留；headless和数据库通过不替代可见验收。

[空间控制](evidence/multi-controller-trigger-order-2026-10-18/storage-control.json)：只留最新f199 A/B和macOS/Windows各一规范zip，旧91应用/包已清退，实测可用空间增加760102912字节（并发文件系统变化可能影响差值）。24份已完成读取器的完全相同依赖/数据使用APFS共享，逻辑18961738字节但未观察到正释放量，不把逻辑大小宣称为物理释放。当前可用约46GiB；本批不新增完整源码checkout，所有来源、原测试/日志/回放/数据库及必要实际模块保留。

统一义务保留原126审查字典、16604原ID和全部来源缺口，新增六个限定分支及两个独立OPEN门禁后为127审查/42上下文/16612义务。语义、测试、当前固定CI及产品验收分别登记，全部42个当前上下文显式等待新固定完整CI。限定无目标排序通过不提升整卡认证。Goal active / NOT READY。
