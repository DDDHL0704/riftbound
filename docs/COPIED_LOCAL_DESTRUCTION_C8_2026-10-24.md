# C8：按真实规则实例分组的局部摧毁替换（限定 L2）

- 功能身份：索拉卡 SFD·173/221、SFD·239/221、SFD·239*/221，实际 Song SFD·059/221 与眼镜 VEN·137 消费者；基线 bb438210。不认证索拉卡其他条款或整卡。
- 来源：采用中文核心 [2026-10-10 PDF](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf)，SHA256 d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11；370.2–370.4、373.2、479.1.d、719.1/719.3。逐个替换实例处理同时事件；已开始的整组不中断，尚未开始的实例按当时位置/控制者/战力判定。
- 原机制红：三精确印次各一份真实 Song 被遗漏，另三份无复制对照通过；committed-frontier-v3 保留。早期 12 个测试准备错误、后续编译错误、7 个入场权限/批量符文准备错误、1 个显式中局误用空房出生审计的错误分别保留，不能算新增机制缺陷。
- 实现只改 DestructionReplacement 的两个方法及私有瞬时 record：事件前捕获真实 AttachedRuleTextRules.Instances，按来源代际与完整规则绑定复核；每个规则实例只开始一次整组。原 PRINTED 选项 ID 保留，复制选项追加实际 ATTACHED 实例 ID；没有新 DTO、持久凭据、事件或全局 CompletionTick 改动。
- 语义边界：单/双 Song、选不同复制体、真实卸除/装备回手/重装、单位回手再打出的代际、真实整单位复制/卡面恢复、真实夺取控制及拥有者不同于单位控制者的 Song；待决 JSON/冷恢复、非法演员/已用印刷组、选项/label/answers/origin 伪造均有断言。结构自洽的真实替代分支仍须被不改原命令的显式初态重放拒绝。
- 有限场景：获批 59 行 BuildCopiedLocalDestructionScenario，三个 copied-local-destruction-soraka-{173,239,239-star} id。全部单位/装备/法术从真实手牌开始；canonical factory、70 法力/六特性各10、21枚真实符文、424242/turn5/private markers 为明确 QA 预置，不是自然开局。公共 dispatcher/Program/manifest 由①接入。
- 本机实际：70 方法、151 个定向/相邻案例通过，未纳入完整对局；该运行的源/bin 在加入新私有 QA factory 前冻结。随后新 factory 的严格初态/非法 id 4/4通过；实际已编译两叶的8组配方共167条命令，逐步 JSON 冷恢复/幂等/显式原初态重放、公开支付 metadata、两种事件载荷及自洽待决分歧均通过。两个叶及探针零警告/错误编译，已加载 DLL SHA/MVID/RuleInputs 独立记录，不绑定旧日志到新输入。
- 叶配方：SQL 六独立房（三印次×单/双 Song），每步待决恢复及原出生审计；Native 两独立双身份房（单复制已完成、双复制最后决定仍待决），实际重启后保留视图/提示/原成功意图幂等。支付选择仅从当前服务端 metadata 生成。
- 真实 PG/API/ROOM_SEAT→DEV_SEED 出生重放仍待固定候选 Actions。复用已修复的实际 Joined/token 轮换、允许列逐行/数字原文比较及重连后独立严格七表基线；本机内存命令配方不能替代这些门禁。
- SQL 待决篡改叶：缺选项、复制 revision、答案 hash 在 current-shape 拒绝；自洽替代实例在原出生审计拒绝。仅临时五投影行，恢复后七表逐行摘要必须一致，再追加合法原待决答案并校验原命令/事件前缀不变；没有改写已接受历史。
- 原件：本工作树 ignored var/qa/copied-destruction-replacement-c8-20261024/；committed-frontier-v3、final-mechanism-and-adjacent-v1、final-mechanism-runtime-v1、finite-source-shapes-v1、leaf-memory-v1 与各原失败目录。净整卡认证增量0；全适用后端与原完整局143方法/151例、110配对/11牌组/424242门禁仍开放。
