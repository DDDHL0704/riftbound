# 查看牌放逐文案与授权来源预览

CN / zh-CN，2026-10-18观察与取得，规则适用范围冻结为2026-10-16。本批只修复已实测的放逐文案和出牌来源预览，保留现有布局、卡面组件、服务端费用与权限、提示及tick绑定；不是整卡或全部消费者认证。

## 修复前证据与范围

[固定8c真实双端](evidence/observed-source-native-ui-2026-10-18/fixed8c-visible-native.json)完成46接受命令/282事件，第17回合正常A8:B0。实际服务停机至新进程健康11.073秒，原双端自动重连；待第二次查看的原20命令/103事件、原种子和双方私有视图精确一致，原下一选择21/108、正向魔腾22/112均与原状态和事件重算匹配。0法力/1通用符能，基地休眠；两次放弃后24/114回到MAIN/NEUTRAL_OPEN，原下一结束回合25/125实际接受并进入B主阶段。完整原始回放、双方终局、返回大厅及正常退出通过，原三局七表和H原16/56帧保留。

这46条中45条来自原生控件，唯一API动作是正常认证的合法A牌组提交，顺序按只读原私有种子规划；不声称盲随机牌序、全原生组牌或自定义卡组大厅预览通过。实际Release d4输入、[发布SQL](evidence/observed-source-native-ui-2026-10-18/fixed8c-release-sql.json)、[错版拒绝](evidence/observed-source-native-ui-2026-10-18/fixed8c-old48-rejection.json)和[原平台字节](evidence/observed-source-native-ui-2026-10-18/fixed8c-native-bytes.json)分别保留。新服务使用真实原重连凭证拒绝旧48输入，零私有/Joined消息，旧三局七表和原24/103文件哈希不变。没有迁移或改写旧开发局。

该局可见“放逐”仍被称为“摧毁替换”，预览仍为CARD；真实旧图在本聊天的CUA记录中，没有声称另存图片或图像哈希。新的独立控件窗口因Mac实际锁定无法取得可见截图；解锁请求待答，窗口已作为独立测试进程停止，不声称原生正常退出或视觉验收。

## 来源与实现

[当前来源](evidence/observed-source-native-ui-2026-10-18/source-manifest.json)三精确记录SFD122/API33212、UNL032/API34560、OGN194/API31426的原文和QA与运行快照相同。14条规则元数据及核心40278538字节SHA-256 d6fd7140…892d11未变；核心2026-10-10发布、10-16生效。禁卡表1807879字节SHA-256 e0326381…d5ef7再次流式核验不变，7-17发布、最新列明增量7-24，SFD122自3-31标准禁用。SFD普通投影只属CoreRules；UNL两赛制原合法牌组保留。没有重复PDF或改写未知日期及既有来源增量。

服务端在既有DECK_OBSERVATION_BANISH种类下提供“选择是否放逐”和对应步骤；客户端阶段显示“查看牌库 · 放逐选择”，说明原效果完成后再按提示支付符能出牌。普通摧毁、代币急速、移动等既有种类保留自己的文案。

Main预览补查沿用当前提示的sourceRequirements，增加STACK来源；公开可见牌仍先走牌桌索引，补查只使用服务端明确授权的对象与精确卡号。没有解析对象ID猜牌、扩张对手隐藏信息、改费用或出牌权限，也没有新布局、字体或资产。frontend-design和Product Design已用于有现有目标的维护核对；可见截图和完整产品验收仍OPEN。

## 验证与保留

[修复前控件结果](evidence/observed-source-native-ui-2026-10-18/native-red.json)在未改三处生产源时，五个观察选择共15项文案断言失败，两种实际Main来源预览各失败；原实际选项和提示绑定检查通过。首轮检查器误把KEEP当DECLINE，并从Confirmed命令正文读取其实位于面板属性的promptId，分别分类为检查器错误，保留首轮源码、程序集和日志；纠正后再确认上述产品红例，没有把检查器异常算成产品缺陷。

[原生控件](evidence/observed-source-native-ui-2026-10-18/native-controls.json)7例全部通过：五个普通投影的原生行动按钮/阶段/选项状态，两个实际Main预览回调和确认按钮。精确魔腾卡号与来源呈现，未知对象及对手提示不能授权。两条普通空白seed0投影合计53命令/195事件，完整原行动与事件回放一致；这属于headless生产控件信号检查，不能代替可见双客户端。[定向83例/45方法](evidence/observed-source-native-ui-2026-10-18/focus.json)通过。

[真实SQL](evidence/observed-source-native-ui-2026-10-18/real-sql.json)三条普通分支90命令/340事件、42冷恢复、15原下一命令、24篡改拒绝通过。旧36个审计房间与全部四个原生房间七表逐字节不变，专用库正常smart stop，无新库。当前Debug bad34120…34a71及合并身份47228e54…b213由实际PE MVID与规范JSON独立复算，Conformance/SQL/冻结四规则模块/PDB及三数据相同，不能与前固定Release d4互换。

[原5场和全部110配对](evidence/observed-source-native-ui-2026-10-18/original-five-and110.json)通过，11原牌组/424242、两赛制逐牌golden、原胜利和事件/行动回放断言保留，12737执行输入及即时核验无漂移。[唯一check](evidence/observed-source-native-ui-2026-10-18/one-check.json)12510例/7354方法、716.55秒全过，零失败/跳过，与前批用例多重集合完全相等，无第二次check；7控件例另计。全部462原Conformance、1279夹具、8份完整对局和35份Postgres源码字节不变。12实际执行程序集/PDB与原115一致，八份核心复用已有冻结引用，只APFS克隆另外四份，未复制源码树。55项工具检查通过。

## 完整CI与未闭合门禁

[固定48完整CI39109499298](evidence/observed-source-native-ui-2026-10-18/fixed48-ci-timeout.json)实际cancelled，GitHub明确标注超出3小时。独立核验其规则套件12639例/7487方法、全部143完整方法及110配对通过，完整保留前12623/7481并增加16/6；SQL只有已完成的95路径/91收据，新增空查看分支未完整收尾，网络恢复步骤未执行。旧取消状态保留，不能写成full成功。旧平台包未重下载，以清退前真实CRC及原启动日志核对。

本批[时限修复](evidence/observed-source-native-ui-2026-10-18/ci-timeout-repair.json)仅将规则作业180分钟调整为300分钟并说明实际原因；原检查名称、全部方法/配对、默认SQL及网络步骤保留。修改在原115即时核验后进行，唯一check和工具测试覆盖最终工作流源。固定8c的完整CI39116825669仍须实际终态，新提交按300分钟上限执行独立full。

[验收边界](evidence/observed-source-native-ui-2026-10-18/acceptance-boundaries.json)保持当前固定full、当前可见控件/双端/原库网络/正常得分与回放、Windows实机及全部全量语义OPEN。只留最新Window A/B和两平台各一规范包，磁盘约56GiB可用，原库、全部测试/来源/原始帧和必要实际模块保留。1740/CN1504/EN1408缺口8、36文档14293块、四时间增量、凯隐身份和全部原义务保留。Goal active / NOT READY。

[统一义务保留核对](evidence/observed-source-native-ui-2026-10-18/governance-and-obligation-retention.json)：129原审查字典、16629原义务ID和76原门禁完整保留；当前130审查、45上下文、16631义务。治理0错误/49警告，semanticCoverageCertified仍为false。唯一check后只有治理证据文档改变，全部非文档执行输入逐字节不变。
