# 查看后出牌完成时恢复原时序

CN / zh-CN，2026-10-18 取得和观察，适用日期冻结为2026-10-16。只审查已完成查看效果后的魔腾许可收尾及共享时序恢复，不提升整卡或其他消费者认证。

## 实际原生红例

[固定48可见双端](evidence/observed-play-window-2026-10-18/fixed48-visible-native-red.json)在真实原库完成第二轮查看、自动重连、原下一选择及一张魔腾的0法力/1任意符能出牌。全部首次查看魔腾先放逐；第二轮提示对B隐藏。实际服务停机至新API健康13.669秒，两端原进程自动恢复，19命令/92事件、原种子和双方私密视图精确不变；原下一选择20/97及正向出牌21/101均按原状态和事件核对。

随后两次原生放弃均接受，结算链和所有许可已空，但权威状态仍为`MAIN / NEUTRAL_CLOSED`；原生结束回合被拒绝。原23接受、1拒绝、103事件及完整回放保留，正常得分终局仍未完成。没有改种子、手牌、RNG结果、旧状态或接受历史，也没有投降。QA唯一API动作是正常认证的A牌组提交，其合法顺序事先根据只读原私有种子规划；之后准备、调度和全部实战操作均来自原生控件，不声称盲随机牌序。原两局七表及H原16/56帧精确不变，双端/API/专用库正常退出。

## 官方来源与实现

[来源manifest](evidence/observed-play-window-2026-10-18/source-manifest.json)保留三张精确印次当前原文、QA、取得时间、压缩原字节和哈希。14条规则目录未变，核心40278538字节再次流式取得，SHA-256`d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`，2026-10-10发布、10-16生效，没有重复PDF。340.2要求空结算链回到开环；340.3/340.4保留后续确认与旧链优先权；340.2.a/346.1区分触发链的焦点保留。精确摘录和身份另外保留。

[禁卡表复核](evidence/observed-play-window-2026-10-18/banlist-source-reconfirmation.json)字节仍为`e0326381…d5ef7`，区分7-17发布、最新增量7-24生效及SFD122本身3-31起标准禁用。普通SFD122仅属CoreRules，UNL032合法通过两赛制。不改写旧未知日期、四项来源增量或凯隐身份缺口。

生产仅在`FinishObservedDeckPlay`最后没有剩余许可和挂起原出牌时，调用既有`RestoreAfterTriggerConfirmation`。原旧链、原序列、法术对决及结束阶段继续各自流程；没有DSL扩张、牌名绕过或旧开发局迁移。独立安全检查和原行动日志审计保留。

## 验证与保留

[原红例](evidence/observed-play-window-2026-10-18/semantic-red.json)7条普通开局中5失败、2全部打出控制通过，失败均为期望开环而实际闭环。实际预修复Debug实现`4f0f63c3…d3ebfc`由保留模块MVID独立复算，另与实际原生Release`87c65e34…49c43d`区分。修复首轮18例通过；扩大后318通过、两条新战斗夹具错误预期失败。该夹具停在初始战斗法术对决，当前规则要求保留进攻方焦点；仅修正这两条新断言，保留失败源码、实际程序集和TRX，没有为它们再改生产。

[最终共享相邻](evidence/observed-play-window-2026-10-18/focus.json)320例/156方法通过，新增20例/8方法。[七条普通空白分支](evidence/observed-play-window-2026-10-18/ordinary-openings.json)212命令/803事件、35冷恢复/28原下一命令，覆盖全放弃、一张打出后放弃、全部打出和第二轮可选放弃；之后真实结束回合进入P2开放主阶段。其他控制保留旧法术、光明未来原出牌队列、戴安娜及初始战斗的原焦点、原结束阶段。初始战斗夹具不认证后续普通闭环战斗响应。

[真实PostgreSQL](evidence/observed-play-window-2026-10-18/real-sql.json)三个原空白分支90命令/340事件、42冷恢复、15原下一命令和24篡改拒绝。协调伪造最终闭环及双方视图能通过结构，仍被原日志审计拒绝；错版输入另拒绝。拒绝不入缓存、不写库，恢复原七表。30旧审计房间及三局原生七表精确不变，原专用库smart stop，没有新库或源码checkout。默认SQL入口只追加新消费者，原33项顺序保留。

[实际编译身份](evidence/observed-play-window-2026-10-18/actual-compiled-inputs.json)为Debug`52504b50…3a401`，四实际模块/PDB和三运行数据在Conformance、SQL与[原115对局](evidence/observed-play-window-2026-10-18/original-five-and110.json)相同。五条原完整对局、全部110双向配对、11牌组/424242及两赛制逐牌golden通过，12702执行输入和即时核验无漂移。

[保留清单](evidence/observed-play-window-2026-10-18/test-retention.json)核对原460份Conformance、1279夹具、8份FullGame源码/143方法逐字节相同，原方法、Theory、种子及胜利/事件/行动回放断言保留。[唯一check](evidence/observed-play-window-2026-10-18/one-check.json)12510例/7354方法全过、769.15秒，原12490/7346多重集合完整保留，新增20/8；本批没有第二次check。55项工具检查通过。

## 固定CI、产品与空间

[前固定46完整CI39103607902](evidence/observed-play-window-2026-10-18/fixed46-full-ci.json)实际success并独立核验12623例/7481方法、143完整方法、全部110配对、95 SQL路径/91收据、网络15跨身份拒绝/零泄露和双平台实际字节/启动日志。两条SQL分支实际80/254和72/230，首轮核验器错把总数均分；保留核验器失败并据原产物和既有本地SQL更正，不改CI输出。旧安装包依清退前实际字节及原日志核对，没有重新下载或声称旧包仍保有。该固定提交不覆盖本批新修复。

[两项原生界面观察](evidence/observed-play-window-2026-10-18/native-ui-source-trace.json)分别登记OPEN：放逐许可误入“摧毁替换”默认文案；预览回调只补主牌堆来源，未覆盖已获许可的STACK来源而清空为CARD。来源分支已按实际固定48字节核对，没有在本批改页面或推定CDN故障；后续修复仍须保留隐藏信息保护和固定可见验收。

[边界](evidence/observed-play-window-2026-10-18/acceptance-boundaries.json)保留新固定full、当前候选可见双端/网络/正常得分及完整原始回放、两项界面修复、独立Windows实机、其他消费者和全部整卡语义。所有原来源、义务和128份限定审查保留，各项语义、测试、固定CI和产品状态分开。Goal active / NOT READY。

[空间记录](evidence/observed-play-window-2026-10-18/storage-control.json)只保留最新Echo A/B及两平台各一规范包，无整份源码复制；142份历史治理输出经透明压缩减少1797349376字节分配，路径/逻辑字节/哈希/模式/修改时间逐项不变。测试、来源、原库、原始帧和必要实际模块保留。

[治理与义务保留](evidence/observed-play-window-2026-10-18/governance-and-obligation-retention.json)实际核对全部16619原ID和128原审查字典不变；新增7分支/3门禁后129审查、44上下文、16629义务。所有44上下文当前固定CI仍PENDING，语义全量认证为false；check后只有治理与有来源的限定证据更新，所有非文档执行输入仍逐字节相同。
