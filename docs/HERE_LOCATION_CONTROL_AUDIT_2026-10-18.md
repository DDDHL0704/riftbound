# 鲛人滋事者“此处”与技能控制者的限定审查

CN / zh-CN，限定来源观察日期2026-10-18，继承2026-10-16生效核心及既有冻结全量清单。来源1740精确印次、全部既有规则/QA/新闻义务及来源缺口继续保留；本批不作整卡认证。

## 官方来源和有限实现

[来源 manifest](evidence/here-location-control-2026-10-18/source-manifest.json)保留实际取得时间、请求、URL、原始及压缩哈希、语言地区和精确API身份。重新取得的UNL003、UNL184、SFD202、OGS011及核心PDF字节未变；新合法开局补充烈娜塔传奇/英雄和OGN210的精确取得记录。核心发布2026-10-10、生效2026-10-16，SHA256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`，仅流式校验并保留一份规范PDF。

359.3.f.1/2中“此处”随同一代来源单位的当前位置更新，可以是战场或同一玩家的基地；双方各自的基地不是同一位置。359.3.f.4中友敌关系以技能控制者判断，不能因来源常驻牌后来被夺取而改用该牌的新控制者。143、124及区域来源优先级仍按既有适用记录。

UNL003打出技能使用有限的`EnemyUnitAtSourceLocation`目标范围。先确认来源代数及真实场上身份；战场沿用共享位置规则，基地同时核对玩家身份和双方真实成员关系。敌方目标仍相对于栈中技能控制者。四个既有防守触发的战场范围消费者未变，其余位置/控制/重入及消费者继续OPEN。独立印刷来源/代数恢复防护保留，无旧开发局迁移或历史重写。

## 已执行证据

- [原语义红例](evidence/here-location-control-2026-10-18/original-causal-red.json)：固定c69旧实现的四条真实控制/移动轨迹中，双方单位一起到新控制者基地时实际伤害0、预期2；原始命令/事件/状态回放仍完全匹配，证明可重现的语义错误。
- [修复后的因果轨迹](evidence/here-location-control-2026-10-18/fixed-causal-rows.json)：控制变化但同战场2，只有来源回基地0，只有目标回基地0，双方同基地2；另有双方不同基地0。均明确是tick0中盘夹具及后续普通命令，不作空白开局、原生或SQL证明。
- [focus](evidence/here-location-control-2026-10-18/focus.json)：三个实际通过运行合并71个独立用例/26方法，新增6例/3方法，零跳过。新合法雷恩加尔/烈娜塔牌组从原空白seed0执行83命令/330事件；恶意收购后，常驻牌控制者改变，技能控制者保留，仍向原敌方单位造成2伤害；两处待处理冷恢复、两条原下一命令及完整原始回放匹配。
- [目标战力实证](evidence/here-location-control-2026-10-18/legal-opening-target-power.json)：目标基础战力2，实际已选的独立雷恩加尔打出指令给予本回合战力+1，因此受到2伤害后存活。该记录不提升独立传奇技能/排序或整卡认证。探索过的迅捷控制牌在未满足时机的窗口被拒绝，未作为接受的开局路线。
- [真实SQL专项](evidence/here-location-control-2026-10-18/scoped-real-sql.json)与[源/运行/原库核验](evidence/here-location-control-2026-10-18/scoped-real-sql-verification.json)：83/330、两次冷恢复、两条原下一命令、两种独立篡改拒绝通过。缺失来源绑定在结构层拒绝；协调修改技能控制者并重建双端视图可过结构层，仍被生产恢复的原始日志审计拒绝。拒绝无写入，原七表精确还原，所有先前房间行摘要不变。仅执行新专项；原默认SQL完整入口顺序保留并追加一项，其新完整执行待固定CI。
- [原对局与顺序保留](evidence/here-location-control-2026-10-18/original-games-and-ordered-deck-retention.json)：受影响的五个原完整对局方法及110配对通过，原11副牌组在两种赛制下的全部实际逐牌/顺序数组与原golden完全相等，seed424242及胜利/回放断言保留。[源码与测试保留](evidence/here-location-control-2026-10-18/source-and-test-retention.json)独立核对448原Conformance源码和1274原fixture逐字节相同。
- [实际构建身份](evidence/here-location-control-2026-10-18/actual-compiled-input-identity.json)：479个生产输入与冻结原对局完全相同；原对局实际程序集和当前Conformance/SQL实际程序集分别提取PE元数据并记录哈希/MVID。冻结构建身份`449dc700…`，当前SQL实际身份`ffcc7374…`，保持各自原始回放身份。源码相同不提升为二进制身份相同。

[本批唯一一次check](evidence/here-location-control-2026-10-18/one-check.json)12379例/7313方法、522.19秒通过，零失败/跳过；原可比12373例全部保留，新增6例/3方法，3208输入无漂移。固定新提交完整CI及当前Here原生验收待收尾。基地同处正向目前仍限定于明确的中盘轨迹，未宣称其合法空白开局SQL或可见原生分支已完成。

## 已完成的上一固定候选与空间管理

[c69完整CI39051636631](https://github.com/DDDHL0704/riftbound/actions/runs/39051636631)实际success，并已从原始产物[独立核验](evidence/here-location-control-2026-10-18/fixedc69-full-ci-independent-original.json)：12522例/7451方法、143原对局、110配对、90真实SQL路径/88规范收据、15跨身份网络拒绝/零私密泄露及双平台包。它保留为本批的上一完整基线。

[固定c69可见双端验收](evidence/here-location-control-2026-10-18/fixedc69-native-baseline.json)完成81条接受命令，其中80条原生、1条认证API自定义牌组提交；狩猎律动待拥有者出牌处实际离线159.84373秒，双方原生重连、原下一出牌、正常A8:B4及完整原空白开局回放通过。双方胜负、返回大厅可见，原生/API/专用数据库已正常退出。原生自定义牌组提交/预览仍OPEN；该候选不覆盖后来Here修改。

按用户空间要求，仅保留最新可用c69双客户端及Mac/Windows各一份规范产物。完成的九份重复冻结源码树已清退，[独立重建核验](evidence/here-location-control-2026-10-18/storage-retirement-reconstruction.json)验证119592文件引用、11639可达Git blob和477唯一保留内容对象；必要程序集、全部源码/测试/来源/日志/TRX及原始数据库保留，实测释放3207868416字节。已结束的原对局冻结输入通过Git与内容对象重建，无新的整份项目副本。

统一剩余义务继续区分语义、测试、固定CI和产品验收。光明未来未覆盖的顺序组合、烛光圣殿OGN·291/298（旧记录称“烛光圣所”）、未闭合消费者及来源缺口继续OPEN，Windows实机独立OPEN。Goal active / NOT READY。
