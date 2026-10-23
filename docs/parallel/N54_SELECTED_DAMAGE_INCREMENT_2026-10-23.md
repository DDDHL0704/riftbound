# N54 选择战场与局部群伤增量（L2）

| 功能身份/精确印次 | 来源与范围 | 实现/测试 | 状态与证据 |
|---|---|---|---|
| OGN250/268、UNL072；OGN123/127/133/296、OGS001/018，各精确分母见既有来源行 | CN/zh-CN，核心10-16生效；10-23完整API行；[选择战场来源](../evidence/selected-battlefield-aoe-parallel-2026-10-23/source-and-red-test-manifest.json)、[局部加伤](../evidence/void-gate-untargeted-damage-parallel-2026-10-23/source-and-red-test-manifest.json)及[泰伯斯](../evidence/tibbers-untargeted-damage-parallel-2026-10-23/source-and-red-test-manifest.json)保留原哈希 | 三法术显式战场选择/校验与槽位匹配，虚空之门按实际受伤对象所在战场加伤，安妮全局加成一次；SelectedBattlefieldAreaDamageTests、SelectedBattlefieldDamageRecoveryTests、VoidGateUntargetedDamageTests、TibbersUntargetedDamageTests | 分支实现/定向及真实SQL网络通过；整卡认证增量0，最新固定full待验。源码随本提交正常入库 |

- 新增95例/10方法；并行canonical138例含3原完整对局、主线旧支付/API校准451例、恢复17例及相邻回归已执行。旧群伤/局部加伤预期经来源校准，原方法/Theory行及9原完整对局源码保留；原失败和理由在`var/qa/untargeted-battlefield-damage-n54-20261023/legacy-selected-consumer-final-calibration-v4.json`。
- 唯一冻结check目录`/Users/dinghaolin/MyProjects/.worktrees/riftbound-backend-n54-check-20261023`，3434输入；提交时仍运行，已观察LocalDestructionRecall旧期待1而实际2的失败。最终源码对应47例focus通过；不改冻结输入、不重跑第二check。终态写同QA目录`sole-check-frozen-owner-terminal-v1.json`，未取得前不称check通过。
- SQL68：17新局118原指令/前缀冷恢复、224事件、3协同篡改拒绝；独立原七表/完整前缀复核通过。11真实SignalR局77原指令、148事件，原API同运行物重启、令牌轮换、重复不变与374条持久化后私有发布观察通过；独立77前缀/44网络意图复核。实际旧网络smoke15拒绝、断线重连和零泄露通过。
- 原始SQL、网络、TRX、失败和执行身份留`var/qa/untargeted-battlefield-damage-n54-20261023/`；SQL/网络证据绑定各自实际运行物，不冒称冻结check同一二进制。8份并行来源/测试归档及manifest在新政策前已暂存，本在途批次保留；未再新生成主线巨型归档/全库台账副本。
- #103保持开放，原1740印次/14293条款/17582历史义务不重算、不删除；机制分支通过不认证全部适用来源或后端阶段。
