# 拉文布鲁姆学院放入手牌与可选战场目标入口

CN / zh-CN，2026-10-18取得与观察，适用范围冻结为2026-10-16。结论只覆盖SFD·215/221、API33314的防守展示法术放入手牌，以及本次真实数据库发现的声明战斗可选目标格式入口；整卡、其他消费者、当前固定完整CI和产品验收分别保持OPEN。锁屏期间后台工作持续，规则测试节奏和固定输入要求不变。

## 来源与缺陷

[来源manifest](evidence/ravenbloom-put-hand-2026-10-18/source-manifest.json)保留16页中文官方响应的原始及压缩哈希、请求、取得时间和每条精确身份。当前1504条与原1504条完整字典相同，零新增、缺失或字段变化；没有改变1740总印次分母、36文档/14293块、四来源增量、凯隐身份门禁或未知发布日期。中文资讯138条最新目录记录亦与原快照完全相同；两条系列售前文章正文仍在web工具与直接HTTP均获取失败，保留来源缺口，不从目录日期或preview标志推定卡牌生效。

中文API及实际查看的官方卡面均写“放入你的手牌”；国际sfd-215-221卡面同样使用put into hand，只作独立地区比较。当前核心414.1、414.2.b、414.3结合明确动作指令核对，核心SHA-256 d6fd7140…892d11、10-10发布/10-16生效未变，没有再复制40MB PDF。SFD215当前官方QA为null，不能虚构成新增FAQ。UNL074冰封宝石与OGN183卡牌骗术的精确当前源记录另存；后者标准禁用，仅用于CoreRules既有控制例。

原实现移动到手牌后发出CARD_DRAWN并捕获抽牌触发来源，导致抽牌计数0/1/2错误增加到1/2/3，前一次真实抽牌后误排入冰封宝石。[7例红例](evidence/ravenbloom-put-hand-2026-10-18/put-hand-red.json)实际3失败、4控制通过；[原固定21两条普通空白开局](evidence/ravenbloom-put-hand-2026-10-18/ordinary-fixed21-red.json)也复现法术分支的错误第二抽。原红源、程序集、原始帧和日志保留。

## 实现与入口修复

只把既有SFD215分支事件改为CARD_ADDED_TO_HAND，删除该事件的drawTriggerSources；原物理移动、控制者、展示/回收、费用和目标流程保留。没有改写旧开发局、重命名历史测试或内部触发标识，也没有整体重写或通用DSL扩张。

[第一次真实SQL失败](evidence/ravenbloom-put-hand-2026-10-18/original-sql-v1-failure.json)保留：新普通开局接受原DECLARE_BATTLE原始字段battlefieldTargetObjectIds:null，随后严格冷恢复报must be an array。原42审计房间及4原生房间七表不变；新部分房间和原日志没有迁移或修补。此前限定语义通过没有被冒充数据库通过。

Protocol让该可选字段的默认null按省略编码；MatchSession复用原可选费用的严格字符串数组扫描，在写入任何接受或拒绝日志前拒绝显式null、错误类型、空白元素、重复键和空白键。原intent冲突优先级保留，坏请求不缓存，合法请求能重用该intent。MatchRecovery和GameCommandJsonMapper字节不变，恢复校验没有放宽。[24例入口红例](evidence/ravenbloom-put-hand-2026-10-18/target-ingress-red.json)实际19失败/5控制通过，修复后全通过。

仅新加入的两条31命令逻辑配方省略最后声明的null字段；[原新配方gzip及差异](evidence/ravenbloom-put-hand-2026-10-18/new-fixture-canonical-targets.json)保留，全部1279原夹具逐字节不变。旧生产者原始帧、失败SQL房间与接受历史不改写。

## 当前实际验证

固定`6c9632f1`实际发布Release的实现身份为83a9bfca…24658、组合4fa44e71…993ce，与先前Debug和固定21分别记录。Mac在每步开始、结束均由只读系统状态确认锁定时，[四条普通开局真实SQL](evidence/ravenbloom-put-hand-2026-10-18/fixed6c-published-sql.json)128命令/500事件、36冷恢复/16原下一命令/16篡改及60畸形入口拒绝通过；原47审计和4原生房间七表未变。[生产原生网关网络验收](evidence/ravenbloom-put-hand-2026-10-18/fixed6c-locked-native-network.json)完成真实套接字丢失、认证恢复、新快照、坏凭证拒绝及15越权拒绝/零私有消息泄露，原51审计和4原生局不变。

[两个认证网关客户端后台验收](evidence/ravenbloom-put-hand-2026-10-18/fixed6c-locked-paired-restart.json)在普通随机原开局4命令/10事件的真实调度窗口保存双方私有视图；正常关闭旧API并以新PID启动，0.411秒为API停止至健康的实测间隔，并非客户端完成恢复时长。双方各自动恢复一次、各收到11次实际快照，原私有视图/提示完全一致；随后两次原调度与一次结束回合正常接受，7命令/27事件原始完整日志回放通过，私有非零原种子和空白初态不变。此前两次验收工具的路径/端口探测失败及其4命令/10事件原局原样保留并再次原回放通过；未修改生产模块或旧历史。原7原生及51审计房间七表未变，本轮API/客户端与专用库均已正常停止。[独立核验](evidence/ravenbloom-put-hand-2026-10-18/fixed6c-locked-independent-verification.json)另逐项核对4条前缀/10事件与最终日志、SQL和网关双方原视图、隐藏手牌、实际运行文件及检查点哈希。此范围仅为无桌面的生产网关/数据库/恢复/原下一命令；没有完成可见控件、当前正常得分整局或Windows实机。后台入口可核验复用已完成收据，锁屏不再成为这些工作的依赖。

[最终focus](evidence/ravenbloom-put-hand-2026-10-18/focus.json)307例/72方法通过，包含唯一check的精确失败身份及全部21个原战斗任务例；原286例/65方法多重集合完整保留，含原185可选费用精确用例多重集合、新24入口例、三个原抽牌数的法术/非法术控制、真正抽牌相邻和原Ravenbloom完整对局。两赛制各法术/单位普通空白seed0分支合计128命令/500事件、20冷恢复/16原下一命令、8直接规则拒绝；协调伪造抽牌事件和计数通过结构仍被原行动日志审计拒绝。普通测试不是历史中盘注入，也没有声称盲随机原生牌序。

[最终真实SQL](evidence/ravenbloom-put-hand-2026-10-18/real-sql.json)四个普通空白且匹配赛制的分支128命令/500事件、36冷恢复/16原下一命令和16篡改拒绝通过；60次畸形目标请求不改七表，随后合法声明重用原intent。每条完整原行动/事件精确回放，双方私有快照和提示一致。原43审计房间(包含上次新失败房间)及全部4原生房间七表不变，原专用数据库复用并smart stop。匹配赛制的恢复不能认证错赛制配置身份。

实际Debug实现8e6ee446728f9f7f9bcaddd00defe3a4391b7120d609ed888557ffea1fb47ba2、组合身份15660e85b71f7ddb4672bee1d6339f18b654d9f54bcb6f56612ca7dc0720e493由PE MVID和规范JSON独立复算；Conformance、SQL、冻结四规则模块/PDB和三数据实际相同，与原语义a2c及固定21 Release56b分别记录。

[原5场及110配对](evidence/ravenbloom-put-hand-2026-10-18/original-five-and110.json)完成115场，11原牌组、424242及两赛制逐牌golden保留；12773个执行输入和即时核验无漂移。[唯一check](evidence/ravenbloom-put-hand-2026-10-18/one-check.json)实际执行12545例/7365方法、850.36秒，12544通过、1失败、零跳过；原12510例/7354方法完整保留，新增35例/11方法。失败来自旧BattlefieldContestBattleTaskGuardTests两个手工匿名请求写出null，并断言该字段为null；只将这两个编码改为规范空数组及对应空数组期待，全部旧提示/意图冲突/私有视图/日志/状态断言和方法不变。随后307例定向全部通过，无第二次check。原失败和测试程序集保留；最终测试程序集另存，四生产模块、SQL及原115输入不变，最终固定full仍必需。55工具测试通过。

[源码保留](evidence/ravenbloom-put-hand-2026-10-18/test-retention.json)核对462原Conformance中458份字节相同，三份只更正错误的抽牌事件期待并追加无抽牌断言，第四份仅修正上述三行手工目标数组编码/形状期待；所有原方法和Theory行保留。八份FullGame的143公共方法正文、其他私有辅助、110配对/种子和胜利/回放断言精确不变，唯一共享Ravenbloom事件期待辅助按官方原文更正。35原SQL源中34份相同，Program仅增加新selector及末尾消费者，原34项默认调用的字节和顺序保留。

## 来源族、完整CI与产品边界

[有限族清单](evidence/ravenbloom-put-hand-2026-10-18/family-candidate-obligation-links.json)的70精确候选全部映射到原统一印次义务，51在运行目录、19在外；不据运行目录或preview标志判定不适用，授予/复制/指示物和其他非字面消费者仍需列举。各既有限定审查不扩大。[英文8页实际密度](evidence/ravenbloom-put-hand-2026-10-18/english-gallery-density-observation.json)为200/199/199/198/200/198/198/16、合计1408，API声明1416，缺口8仍OPEN，未编造身份或静默改变全量分母。

[固定8c full39116825669](evidence/ravenbloom-put-hand-2026-10-18/fixed8c-ci-cancelled.json)实际cancelled，GitHub确认3小时上限；规则12659例/7495方法及143原完整方法/110配对通过，SQL只有98路径/92收据，网络未执行。原状态不提升为full成功。[固定21 full39125088373](evidence/ravenbloom-put-hand-2026-10-18/fixed21-full-verified.json)现已取得实际success终态，独立核验12659例/7495方法、143原完整方法/110配对、101 SQL路径/93收据、网络15拒绝/零泄露及既有双平台规范包摘要；全部前46的12623例/7481方法及91 SQL收据/95路径保留，新增36例/14方法及两项各三条SQL分支。只保留4MB规范验证zip及日志zip，不解出重复树、不重新下载相同原生包。早先pending快照原样保留。300分钟时限和所有门禁不减，本批6c固定full另待终态，后续未提交的复制授予修复不由旧固定覆盖。

[固定21实际Release SQL及旧8c真实重连凭证拒绝](evidence/ravenbloom-put-hand-2026-10-18/fixed21-live-old8c-rejection.json)与当前Debug分开；原四局和审计库七表未写，零私有/Joined消息。旧固定8c可见46/282正常8:0仅属旧范围，不能代替当前新规则验收。实际CI导出包不支持用场景路径启动调试控件，进程虽然0退出但ERROR且未执行用例，保留为[验收入口不可用](evidence/ravenbloom-put-hand-2026-10-18/fixed21-packaged-debug-entry-unavailable.json)，没有冒充产品通过。

只保留最新Source A/B和两平台各一规范压缩包，旧包在新包字节/签名/启动核验后清退，源码/全部测试/来源/原始帧/原库和必要旧运行模块保留；本批没有复制整份源码树。[边界](evidence/ravenbloom-put-hand-2026-10-18/acceptance-boundaries.json)保持当前固定full、可见控件/双端/网络/正常得分/完整回放、Windows实机和所有全量语义OPEN。先前锁定记录保留；最新解锁后CUA可操作，观察A未连接大厅并正常退出两个现有客户端，未开始新验收局。按用户新要求，锁屏期间继续后台规则、真实SQL、原生网络/重连、回放和固定CI；可见产品及Windows单独补验，不反复请求解锁。Goal active / NOT READY。

[统一清单与保留核验](evidence/ravenbloom-put-hand-2026-10-18/governance-and-obligation-retention.json)保留全部130原审查字典、76原门禁字典、四来源增量与16631原义务ID；追加6限定分支和3门禁后为131审查、46上下文、16640义务。治理0错误，全部46当前CI状态仍PENDING，全量语义和产品状态不提升。

2026-10-19 固定6c完整终态补核：CI39135994258实际success，原始conformance压缩包与日志ZIP直接核验12694例/7506方法、全部143原完整方法/110配对、105 SQL路径/94收据，保留固定21的12659例/7495方法与93 SQL收据；网络15独立拒绝/零泄露、两个桌面构建成功。桌面产物仅使用GitHub元数据摘要，本地没有重复下载或宣称Windows实机通过。此固定6c证据不覆盖随后未提交的复制预知输入，新的可见产品和Windows门禁仍OPEN。详见[固定6c完整核验](evidence/ravenbloom-put-hand-2026-10-18/fixed6c-full-verified.json)。
