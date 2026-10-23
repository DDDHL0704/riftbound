# A3 S3 结构自洽符能篡改验收窄修

- 问题：[Issue106](https://github.com/DDDHL0704/riftbound/issues/106)。原候选`9dbf36b36f4e786ee584bc57f3e0df248d25ca05`，[Actions40022865383](https://github.com/DDDHL0704/riftbound/actions/runs/40022865383)、job120653821064/artifact12530629945，首S3 tamper SIGABRT，后七组未执行，原cases[]为空。原ZIP SHA256 `6c6072d8c88cae0acedc14a98ca303698bf466d199bcaa0bdb63782d2ebbcc8f`，原件与FAIL保持。
- 确认原因：旧叶只改最新权威state_snapshots的red1→2，双方玩家snapshot保持red1/blue1。A3等价源码的单房PG实际返回RECOVERY_INCONSISTENT、`match recovery validation failed`，双方快照各报total/typed power不一致，共四条；未到action-log层，不能称篡改被放过。
- 修正仅在S3验收叶：捕获该room最新权威行及每玩家最新snapshot/prompt，按同一伪造状态重建全部五个payload；commands/events及其他行列不改。必须同时满足shape一致、fresh registry的RECOVERY_INCONSISTENT包含完整action-log audit及replayed final state hash拒绝；不放宽成任意错误。失败/无拒绝/意外异常/还原失败均先落完整诊断。
- finally按捕获的room+row id恢复原payload，独立RepeatableRead只读七表采样要求全值相等；失败的registry不得缓存伪造session，同一registry随后须恢复原局，仍无写入。原网络入口仍调用原after-restart校验，不合成Joined/token或重连收据；新SQL函数仅供该失败case的最低单房复现。
- 实际A3本机输入：从9db归档的278个依赖源码/数据文件字节逐项保持，无S7 Engine；1052目录/语义`3bf8afc44964c6706be63503055fedb383004119d4d632c7803ca2bf9b8cd2f0`。原单行攻击与修后一致化攻击使用同一已加载Engine SHA `c1a585f67f2ed63dd1a938df04f1293904fb7075ad3c6f08434a220baac0567f`、MVID `6ba31872-3cb1-4b1c-8b47-861e12561f8d`，RuleInputIdentity `cef16856349a665692d930a9a6d6f8f39fd8aad9a26a42da99059b34534df3bf`。这是macOS等价源码构建，不能冒称原Linux Engine `e55288fa…`/identity `778915eb…` 的CI结果。
- 单房PG修后：shapeConsistent=true，明确action-log audit失败且最终状态哈希不符；五payload/七表原值还原，原10commands/32events完整回放再过。两条永久独立控制在同一A3源码输入 **2/2、0skip**：单改权威被视图拒绝；同步投影通过形状检查后仍必须被原日志拒绝、无缓存/无写入，恢复原frame后正常恢复。
- 本线656+仅测试叶WIP另跑新增控制及原CardPlayHistory守卫 **12/12、0skip**，只归1041本线输入，不用作A3绿灯。没有改生产Engine、恢复算法、原fixture、原方法/Theory行或FullGame九文件，也不含656 S7功能差异；修复提交可独立cherry-pick。
- 原件：本线ignored `var/qa/payment-a3-surplus-tamper-20261024/`，`original-one-row-diagnosis/`保存完整四条原拒绝、正常frame与还原证明，`fixed-single-room-v1/`保存实际修后函数诊断，`a3-controls-v1/results.trx`、`a3-diagnosis-and-fix-proof-v1.json`保留输入。初次SQL_ASCII环境的22P05与补依赖编译日志另存，不作A3语义红；原setup数据库保留，后续UTF8单房成立。
- 只启动并已关闭本轮独立PG16.13（port54388）；原5432服务未动，没有API/网络重启、整套SQL或矩阵执行。外层网络tamper完整后置认证校验及后续七组仍待①固定候选Actions，旧A3 FAIL不改，S7下一批身份不变，全后端Goal仍ACTIVE。
