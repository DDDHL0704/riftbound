# C9 · 贴附宿主自身摧毁替换
基线 `dd6d43ac94165d0489bad4a680f1b264bcdaa87c`；L2，共享机制/目录输入；净整卡认证增量 0，远端实际门禁待验。
范围：当前 CN `SFD·051/221` 守护天使下框；[官方卡面](https://cdn.playloltcg.com/lol/card/20251217/cbbc137ed7644a529456560f55cce23e.png) SHA256 `fde3a9e1ec0824a952d9ab0f073c214a3a3ee62d60d20223f9939b809327c30f`。
依据：[2026-10-10 核心规则](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf) `136.2/370.2/372–374/718.5–719.5`，SHA256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`；铸魂裁判 FAQ 第9页顶部两问另核；旧索拉卡回答不覆盖现行373.2。
共享实现：按实际宿主/装备代际、贴附版本、公开场上共同位置列出必选来源；复用嵌套摧毁与整组召回，替换效果来源/控制者为宿主；新增一条既有事件的 replacementEffectId 值，无 DTO 或事件种类扩展。
独立拓扑修复：夺取并召回沿用整组召回 helper；保留各装备 owner/controller/状态/代际/贴附，协调 ObjectLocations，无新增移动事件。
定向证据：`final-mechanism-and-adjacent-v1` **80/80（41方法）**；其后仅新增两条 FAQ/复制文本消费者 **2/2**；相同生产输入，均排除完整对局套件，不能合称一次82例门禁。
覆盖：真实打出/装配、分离/返回/重打/重新贴附、致命伤害/无Last Breath、两件来源择一/下一独立事件、外来控制/组唯一位置、牌面复制、三个索拉卡印次373.2与复制组不中断、JSON冷恢复/幂等/原日志及伪造。
叶子内存证明：实际编译 SQL Steps/语义及 Native 边界 **8流程/124命令**，含待决三类结构拒绝和一类同构代际原日志拒绝后合法继续；冻结 net10 Engine/Contracts，未执行真实 SQL、原ROOM_SEAT出生审计、net8网络传输或API重启。
SQL叶提供5独立房间；网络叶提供完成单件、待决两件、控制整组三房间，消费实际Joined轮换令牌，严格错误白名单、七表重连差异边界与篡改后原行恢复；四个私有有限 QA 工厂入口无任意状态导入。
边界：718.6缺已核可达的当前CN装备变单位消费者，适用性/自然变身与脱离仍UNKNOWN；仅显式表示及原日志拒绝证据，不称整卡通过，不按RAD前缀排除来源。
原件：本工作树 ignored `var/qa/attached-self-destruction-replacement-c9-20261024/`，完整索引 `complete-delivery-receipt.json`；两项真实原红分别保留。诊断v2专用test DLL未留存的限制不补造，原v1及控制红实际source/bin可取回。
①待办：统一接公共 DEV dispatcher/SQL Program与manifest/Native Program/source links，固定候选实际L2/目录full及受影响完整局、真实PG/双玩家API重启/原日志安全门禁；不混入既有A3，不改原测试矩阵或接受历史。
