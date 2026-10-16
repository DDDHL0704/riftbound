# aa2完整CI超时与可见双客户端限定验收

CN / zh-CN，适用日2026-10-16，固定提交`aa2f095df2b46689bb75d4155e3411c1cfd86d8b`。[原来源manifest](evidence/granted-token-haste-2026-10-16/source-manifest.json)及1740来源/1033运行身份、全量条款和地区/日期缺口全部保留，本轮没有新增整卡语义认证。

[CI38879419423](https://github.com/DDDHL0704/riftbound/actions/runs/38879419423)的整体终态为cancelled。GitHub明确注记超过2小时作业上限；完整规则组件实际12169用例/7383方法通过，5208.84秒；75项真实SQL完成，原两项厄塔汗与新增两项指示物共4项未完成，网络步骤未执行。[取消及逐项核对记录](evidence/granted-token-haste-2026-10-16/ci-aa2-timeout.json)核对全部旧用例/方法、433份旧Conformance源码、原143完整对局/110配对/11牌组/424242、471游戏输入及四产物大小/哈希。110份终态原始哈希变化分别保留，不声明跨构建归一等价。完整规则组件通过不能代替完整CI。

[issue49](https://github.com/DDDHL0704/riftbound/issues/49)限定将同一rules作业上限120改180分钟，全部命令、真实数据库、网络、检查名称和产物步骤保留。此修复不改变游戏输入；此前aa2唯一check12020/7242、当前规则组件与55项Python工具测试作为精确未变输入证据保留，不重复执行同一未变规则的本地check。新配置仍默认完整CI，必须核对其新固定提交终态，不能继承aa2取消结果。[GitHub作业时限定义](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idtimeout-minutes)。

[同一aa2可见双客户端记录](evidence/paired-token-haste-2026-10-16/acceptance.json)：来自精确CI管理代码/PCK的373文件在双方一致，本地QA启动器仅路由服务器/会话/窗口参数；本地服务端471游戏源文件匹配固定提交，36发布文件至终局无漂移，实际程序集身份单独记录，不声称与CI后端二进制相同。普通认证API仅用于两次合法提交牌组，其余46条尝试由原生CUA控件产生，其中45接受、1拒绝。第12回合正常B8:A0；真实PostgreSQL48命令/209事件从tick0、SEATING、零卡牌的原始开局完整行动/事件/状态回放通过，无DEV、投降、旧局迁移或历史改写。截图证据为本chat实际CUA输出，不虚构已存PNG。

手牌指示物明确DECLINE时符能为0，不算可支付时放弃分支。待命护驾由正常资源操作预先准备1法力/1黄色符能，原生选择PAY:TRAIT:yellow，费用事件127绑定指示物与父来源，出生事件128明确活跃，命令38同回合移回基地。未认证护驾进场后黄色可选支付/反身式。11.124218秒中断后双方无需点击重连即可继续原生提交；58.5秒中断超过重试窗口后双方手动重连另记。B实际菜单退出、CUA清单确认未运行、冷启动后手动大厅重连恢复六张私有手牌，SQL状态原哈希不变，再继续至终局。

原生仍有[issue46回收选项缺失](https://github.com/DDDHL0704/riftbound/issues/46)、[issue47待命必选项误标](https://github.com/DDDHL0704/riftbound/issues/47)、[issue48急速标题错误](https://github.com/DDDHL0704/riftbound/issues/48)。第28条待命拒绝实际因漏选optionalCosts，服务器正确拒绝；补选STANDBY_A后第29条成功，不是黄色符能不可用。新提示用例7例实际全红（4费用候选、3待命绑定），首次仅新增测试可空性编译错误及随后真实红例均保留，尚未纳入此次CI时限修复提交。画面需缩放恢复才能取得新帧的现象保留，渲染与捕获根因未确定。原生自定义牌组、其他消费者/费用修正、Windows实机和全部来源/语义义务继续OPEN；Goal active / NOT READY。
