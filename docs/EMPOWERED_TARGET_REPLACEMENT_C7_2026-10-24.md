# C7：已强化目标的选定动作替换（限定 L2 交付）

- 功能身份：VEN·086 / VEN·181 两个精确中文印次；目录前置为 a37587cd（④ d93d2b1f 的便携五文件增量），通用重复眩晕/旧目标前置为 05b39314、fc1a3699；运行时源码 92129377。本文件所属提交补 SQL/Native 叶类，不含①公共入口。
- 来源：现行采用中文核心 [2026-10-10 PDF](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf)，SHA256 d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11；[VEN 官方 FAQ](https://cdn.playloltcg.com/lol/2026/08/2026-08-13/612b1cd53f9f41baaf3c8734c1881a3b.pdf) 第12–14页，SHA256 59bd828207564540aa3c9885f9dfc39764034f633eb6fb84d49440c9edef4742。对应核心370.2、372、374、423.1.a.1、477.3.b及359.3.e.14.a/b。
- 行为：实际选中当前代际单位、且动作能够发生时，眩晕/负战力/回手统一改为目标技能给予本回合 +3；已眩晕、归零为 -0、记住对象而未选定和全体效果不虚构替换。互换先锁定双方原差值；独立规则实例为本次事件的替换选项，不能叠成单次 +6。
- 次序与凭据：受影响单位控制者选择替换次序；原指令 state.Tick+1 为稳定逻辑 AppliedTick，后续答案不改它。私有凭据保留原 stack/controller/source/card 与规则实例/复制/代际绑定；公有效果 ID 仅含 AppliedTick:Occurrence:TargetId:TargetGeneration:AppliedOrder，不嵌入私有来源。共享 CompletionTick/完整 request hash、answers、origin 守卫保留基线。
- 独立指示：OGN104 被替换的合法回手仍执行原拥有者召符文，鬼影湾需真实回手事件；真实控制权变化下 owner 与 controller 分别验证。公开 Might frame 复制对象和每条 ledger，仅移除新私有凭据；主状态保留完整凭据与严格校验。
- 恢复安全：缺失/错误凭据或公有 ID 由 current-shape 拒绝；结构自洽的原 stack/source/controller/time、额外 +3、公开 frame 凭据/数字伪造由不改原指令的出生重放拒绝。+3 和 ledger 顺序数值超界返回专用能力错误，拒绝不变/幂等及同拒绝 session 投降后再冷恢复保留。
- 本机已执行：115例机制/④消费者组合；随后源场景与相邻域132例；最终受影响域与边界159例；12组叶业务配方共136条真实命令、JSON冷恢复与显式初态重放；已编译隐私守卫对原泄露4份与仅同名 sourcePath 2份真实JSON共6控通过。各次输入独立记录，不把这些数目相加作认证率。
- 有限场景：BuildEmpoweredTargetReplacementScenario，仅 empowered-target-action-replacement / empowered-target-action-overflow。前者手牌未强化、必须实际 play/Empower；后者明确 int.MaxValue-1/已强化表示边界。双方70法力/六特性各10及固定符文牌堆均为 QA 预置；严格初态恢复已验，不是自然开局或自然到达数值证明。
- 公共接入由①完成：DEV dispatcher 两 id；EmpoweredTargetReplacementSqlAcceptance.RunAsync / PrepareNetworkAsync / VerifyNetworkAsync(before-restart,after-restart) / VerifyNetworkTamperAsync；EmpoweredTargetReplacementNetworkAcceptance.RunAsync(url,output,recover)。SQL默认12独立房间，Native准备4房间（双印次回响/私有来源，各两身份），客户端 choice 从当前服务端 payment metadata 组装。
- SQL/网络边界：两个叶项目零警告/错误编译。C7真实PG、真实ROOM_SEAT→DEV_SEED出生、API双进程重启、四类SQL持久态篡改及七表原件恢复尚未执行，留固定候选 Actions；内存配方不代替这些门禁。Source dispatcher/Program/manifest/CI未由②改动。
- 原件：本工作树 ignored var/qa/empowered-target-replacement-c7-20261024/；final-id-mechanism-focus-v1、scenario-and-adjacent-v1、leaf-memory-v1、privacy-guard-real-controls-v1 及原 red 目录。两个 Might 原泄露、连续选择原 transcript 红、合法 OGN051→OGN104 私有ID红均保留；旧66生产基线日志不改。ORB/SFD135 闭环测试准备错误及字符串隐私误报另列，不算机制语义红。
- 范围仍开放：Blast Cone 当前 NO_MOVE 占位，未造假栈/marker证明；全来源/整卡/后端验收均未完成，净整卡认证增量0。④五个消费者源仅作为已批准导入输入，最终归④去重；未混入②源码提交。当前付款前置沿已对齐 S4/S5；S7新费用改动未并入本包。
