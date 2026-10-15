# 原生桌面跨局清理与固定候选补证

2026-10-16，Goal active / NOT READY。本批是客户端控件生命周期修复；当前官方审计目标仍为 CN16，实际规则运行仍为 CN14/DOC04，不把旧候选重新命名为 CN16。

## 已核实的固定证据

`15a79bfb1abfdf969ddbb959b878789247629ccc` 的完整 CI38781161086 实际成功。下载的 conformance-results11997779830 与 verification-scope11995422560 已核对 GitHub SHA-256。TRX 为12,040用例/7,341方法、零失败/跳过，全部旧用例多重集合、143个原完整对局方法、110个双向配对和 seed424242 保留；67条实际 PostgreSQL 场景及 macOS/Windows 打包任务通过，full2704.20秒。[精确核对](evidence/native-lobby-lifecycle-2026-10-16/full-ci-15a79bfb.json)中的466个游戏源输入与831相同。

两个构建的实际 SQL ruleInputs 中 authority/catalog 相同，执行程序集 MVID 派生的 implementation 身份不同，110个最终状态哈希全部变化。记录各次真实哈希，不要求跨二进制相等，也不声称去除身份后所有终态相同；每一配对自身的胜利和 action/event/state 回放断言均执行通过。没有更改身份算法或迁移旧局来消除差异。

同831固定原生候选完成真实双客户端18回合正常8:7结束。独立只读加载真实 SQL，原始 tick0/SEATING/空卡牌开局的117条命令、525条事件完整重放，最终状态一致；未投降、未使用开发种子或改写已接受历史。[产品收据](evidence/paired-83179358-2026-10-16/product-receipt.json)保存自动重连、五张私有洞察窗口冷重连、保密观察、回收/排序/抽牌、时空之门给予月蚀回响、两轮独立目标和洞察的准确范围；结束后12份 API DLL及每个客户端372份 managed DLL重新核对。薇恩在该局未抽到，实际可见支付仍 OPEN。

## 跨局缺陷与限定修复

双方在结算界面点击返回大厅，再普通进入新房间 `vayne-visible-20261016` 后，服务器已保存新房间 tick0/event0，但两端停在等待服务器更新。实际错误栈均是 `MatchTableRenderer.SetRuneActions` 访问已释放的 `Godot.Button`。[失败收据](evidence/native-lobby-lifecycle-2026-10-16/new-room-failure.json)及 [Issue38](https://github.com/DDDHL0704/riftbound/issues/38)保留该观察。新局仅准备了合法40/12/3构筑，没有提交卡组或接受游戏动作；原8:7历史不变。

`ClearMatchResult` 经 `RenderSections([])` 释放旧符文按钮，但 renderer 仍保留按钮引用、合法符文集合及 pending 状态。`MatchTableRenderer.Clear` 现在同时清空这些缓存。服务端规则、身份校验、数据库和已接受历史均未改动，没有增加兼容或迁移分支。

独立 `RuneTableLifecycleProof` 真实实例化原生 MatchScreen，等待 QueueFree 完成后，在旧实现复现同一 ObjectDisposedException；修复后两条路径通过：清空后先收到提示，再进入新桌面；清空后直接进入新桌面。使用同一玩家及重复符文对象 ID，确认新控件必须等待自己的提示，四个实际回收/批量信号正确发出，pending 仍禁用。[实际原生收据](evidence/native-lobby-lifecycle-2026-10-16/native.json)保留红绿日志。桌面 CI 两个 OS 都加入该实际控件回归；它不替代 Windows 物理实机。

[291用例/247方法 focus](evidence/native-lobby-lifecycle-2026-10-16/focus.json)覆盖可选法力提示、房间入口及提示责任边界。424份原 Conformance C#源码逐字节保留。本批唯一 [check](evidence/native-lobby-lifecycle-2026-10-16/check-result.json) 为11,891用例/7,200方法、349.01秒、零失败/跳过，与上一 check 用例多重集合及方法完全相同；2,881冻结输入无漂移。[55项 Python](evidence/native-lobby-lifecycle-2026-10-16/python-tools.json)及[治理109项限定审查/16,481剩余义务](evidence/native-lobby-lifecycle-2026-10-16/governance-index.json)通过一致性核对，不提升语义认证。新固定提交 full 和可见跨局复验分别执行，未完成前门禁继续 OPEN。

## 仍开放的旧原生测试义务

首次尝试沿用综合 `BattleTableInteractionProof`，在尚未到达新增生命周期断言前，旧合成 Player fixture 给对手显式可见手牌却断言全部匿名，导致失败。一次未提交的 fixture 调查越过该点后，在合成的多技能提示缺少 candidateJson 时触发空 JSON 解析失败。实际栈位于 `PromptInteractionController.CurrentAction`；已另行核对7864字节的 battle-declaration JSON有效，不能把它误报为空文件。两次结果均保留；所有探索改动已撤回，原测试源码及断言不变。独立记录后续 fixture/真实协议复核义务，没有把旧综合验收报告为通过。

本批不关闭大厅布局裁切/遮挡、原生自定义卡组编辑、薇恩及其他混合支付、CN16规则输入采用、全部官方语义或 Windows 实机义务。第二次洞察的瞬时牌名观察已保留，但刷新原生读取与实际 SQL 提示一致，未确立可复现的错误原因，也没有因此修改规则。
