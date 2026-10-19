# 完整CI六小时取消与独立作业验收

固定6d9aee51的[完整CI39224398741](https://github.com/DDDHL0704/riftbound/actions/runs/39224398741)已实际cancelled。官方检查注解明确为“The job has exceeded the maximum execution time of 6h0m0s”；真实SQL步骤被取消，后续原生网络步骤skipped。scope和两平台构建成功不替代完整CI。[原注解](evidence/parallel-full-ci-2026-10-19/cancelled-full-official-annotations-v1.json)、[实际终态与规则核对](evidence/parallel-full-ci-2026-10-19/cancelled-full-rule-parity-verification-v1.json)。

原规则五分片TRX实际13068例/7561方法全过，143原完整对局方法和11副牌组的全部110有向配对/seed424242精确匹配，110实际终态哈希保留。原完整规则源、种子、胜利和回放断言未改；这只是该取消轮次的规则步骤证据，不提升后续797f或新候选。SQL只有部分原始输出，未认定整体完成，网络未执行。5.6MB原始产物ZIP的GitHub摘要和全部条目CRC均验证，全部原TRX与部分SQL原件留存；旧桌面包仅记录远端元数据，没有下载或替换当前可用应用。

原单个rules作业把完整规则和完整SQL串接在同一个六小时预算中。现在rule_suite与postgres都在scope后首波启动，各用独立运行器和六小时预算；两者均保留原PostgreSQL16.13服务及连接，避免Conformance的可选SQL smoke变为无连接直接返回。SQL默认调用保留原41助手与顺序，新增的第42助手也完整执行；原生网络仍在SQL之后执行，独立作业显式构建API，再正常启动服务并保留API/网络原始日志。

既有必需检查名称rules作为汇总门禁，依赖scope、rule_suite和postgres，完整范围使用always执行，包括依赖取消时；任一依赖失败、取消、跳过或缺失都会失败。文档精确白名单仍按既有流程跳过完整任务，未知或失败的范围判定继续走full。此处理考虑到[GitHub条件检查文档](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-jobs-with-conditions)说明的跳过检查表现，以及[needs结果上下文](https://docs.github.com/en/actions/reference/workflows-and-actions/contexts#needs-context)。汇总子作业状态不代替实际TRX、配对、SQL与产物核对。

scope、desktop、全局权限和环境的解析结构保持一致；桌面两个检查名称、原打包脚本及版本固定动作不改。conformance-results保留ci-rules相对路径及精确提交标记；新增postgres-results保留ci-postgres相对路径、原数据和提交标记，full-verification-gate保留汇总收据。没有重复上传同一组大证据，未扩充权限；各并发组仍含精确SHA，只取消同SHA重复，不取消其他固定提交。

65项Python检查通过，含125种依赖状态组合、错误/缺失提交与依赖、失败不能产生成功收据、保留真实SQL环境、完整默认命令及不可条件跳过的门禁。YAML已实际解析，结构保留核对通过。没有规则、原生或官方运行数据变更；先前C#唯一check12975/7427不冒称执行新CI结构，也不跑第二次check。实际新固定full仍PENDING，[实现与验证清单](evidence/parallel-full-ci-2026-10-19/pipeline-change.json)。issue49保留开放，新的CI终态、可见原生产品和Windows实机仍独立验收。Goal active / NOT READY。

提交前治理检查实际拦下observed-source-native-ui-2026-10-18中的旧工作流当前实现哈希。现将该一次CI实现引用变化单独登记，完整旧上下文与旧YAML留存；只更新该上下文的当前工作流引用/明确未提交范围，保留PENDING、原语义、原验证和所有其他54上下文字典。[明确版本增量](evidence/parallel-full-ci-2026-10-19/pipeline-change.json)。帕卡来源批次的54上下文原样保留收据仍是该批写入时的真实快照，不冒称后续CI实现没有变化。
