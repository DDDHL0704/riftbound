# N11 · VEN093 / VEN122 强化后自身消费者

从 main `8ac0afba21574c94e7ce8a03ed83704524fee8e7` 独立移植 CN37790均衡渡命人（4/4紫色）及 CN37822烈阳之鹰（3/3黄色鸟类）：均强化2；强化后分别持续+1/游走及+1/法盾2。支付、法盾、游走和持续加成算法均复用主线。
2026-10-24重新取得[官网API](https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb)与完整卡面，记录/图与旧已目视核对原件逐字节相同：093记录 `56cae7a3fbefc81586b36f1a67ef9f275f63bd629836e9c40b4f7641cf618cc9` /图 `732d365806131523cdfbfc98caa1afae5d4143fac8a9649f960ec1d1733a4e92`；122记录 `017e5f1c4dc6534a5862295511d5e61248b87a94684974d674a9d50521bdbb15` /图 `557ee54ed7ae3e1aa2d47bbef8187cd5016991fd7b9f53dee65ae89cc4d1e473`。QA=null、勘误空、发布/生效日UNKNOWN；不合并全球印次身份。
保留原1040条目录、顺序、scopedAdditions及顶层元数据，仅新增2条精确来源，当前1042/功能分组841；原1009逐字段历史审计保持，仅同步当前1042/非历史33计数。
main尚无PR146的强化来源隐私修复，显式复用其最小依赖：BuildStackItemSnapshotView与CapturedBattleSourceIsPrivate(StackItemState)各加入强化上下文；无VEN032目录/测试或旧WIP祖先。二方法在本包与PR146字节相同，若146先合并无需重复实施。
原11方法/34例所有语义断言保留；替换旧私有辅助依赖为本卡真实命令驱动、合法占有战场与中立空战场初态，公开/隐藏信息、真实回手/游走/法盾支付/负战力与原日志断言均保留。旧182/182属于旧固定输入，不复称本主线结果。
固定源码 `f05ad495bc744960608eaa945cb8bcbebd691111` 全项目构建零警告零错误，定向139/139、零跳过＝原消费者34（TRX用例名/重数与旧34完全相同）+原强化38+解析8+法盾11+真实装备技能34+目录9+历史审计1+原旁观者恢复4。32份最终夹具184命令（171接受/13拒绝）、638事件；4种原日志语义篡改拒绝，逐命令原回放/重复幂等/拒绝状态与视图不变及JSON低层恢复均通过。2488选取输入与1417冻结运行文件前后核等，实际模块路径/SHA/MVID/RIF `c1e5e4f730251507ff758ee296881c41511c0acfd902820561d6b9d117a4d9ea` 另核。
本批收据 `main-source-test-receipt-v1.json` SHA256 `2e5597f4d9f8074b51e76de20636779e9af6c7671eba9fc22a5797b4283e28fa`；QA `var/qa/cards-direct-pr-migration-20261024/n11-main-port-v1/`，旧原件 `var/qa/ven-empowered-roam-ward-20261024/` 保留。按L2等本PR实际普通规则CI后合并；未新跑SQL/network/full/矩阵，未声称普通牌组registry/整卡认证，净整卡0/后端NOT READY。
