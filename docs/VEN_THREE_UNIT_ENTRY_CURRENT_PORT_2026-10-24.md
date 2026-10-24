# VEN026／048／118 · 当前基础单位入场消费者

2026-10-24。显式完整前置为 [PR170](https://github.com/DDDHL0704/riftbound/pull/170) 的 `67cccc9dce0cf7c985bf10845823201a12c0e7fd`；从main bb8b7a17的独立三卡源合入该修复。相对此前置，生产仅目录3条精确来源及CardBehaviorRegistry的7行定义，无额外Engine/Contracts/Driver修改。

## 当前来源

| 精确CN印次 | 印刷数据与效果 | canonical记录SHA256 |
|---|---|---|
| VEN·026/166 SC | 37714／战地乐团：4法力、3战力；入场给予任意单位本回合+3。 | `fda3087ffa638d450aad1418262d47cd9f4e2472cf92021d92ab2fc27473b119` |
| VEN·048/166 SC | 37739／云端亚龙：6法力、5战力、龙；入场抽1。 | `bd3379abbae849ac7496c6eae475b4e64e40d8415b25197aa87829dcd0c5e503` |
| VEN·118/166 SC | 37818／龙角勇士：6法力、6战力；壁垒。 | `ca5f7310070a52deda70b08aade025518c209735ae6b947786ed0ec80bac707d` |

2026-10-24T09:21:08.063517Z从官方API `https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb` 的VEN完整255项响应提取，当前3张完整卡图已查看；费用、战力、正文、属性、颜色及图片地址与旧源相同。QA为null、errata为空。记录排序键紧凑UTF-8 JSON保留中文；图片及各自SHA见source-v1/receipt.json。发布、生效、适用性和跨区映射仍UNKNOWN，不从CDN日期推断。

## 原断言与新实录

旧99edd的Basic、Projection、严格历史来源扫描、恶意字段守卫及历史算术五文件逐字保留。原Basic/Projection58例的完整名称/数量多重集合全部在内；全局隐藏扫描只增加调用该独立历史来源守卫一行。它只豁免已与精确印刷条款、唯一应用记录、真实resolved/finalized打出及代际相符的原公开来源字段，其余私有引用继续检查。后来强化改变当前基础战力与原临时记录分开，原三个历史算术及原日志伪造断言保留；没有导入新生产算术。

固定验证源 `eca56ffe0c14f8967bdc20897b9c25eb9ed6fb6f`；后续ready只增加本文。构建-m:1、测试单线程，0警告/错误。

| 范围 | 实际结果 |
|---|---|
| 原消费者、隐私/算术守卫、PR170相邻、新双方恢复实录 | 87/87 |
| 相邻入场、目标、战力、光环、目录/历史 | 308/308 |
| 合计 | 395/395，零跳过 |
| 原11套牌组两格式输入守卫 | 1/1；实际完整对局0 |
| 仅去掉3注册的私有反向源 | 33项：12隐私控制P、21预期消费者F |

新增12条实录覆盖每张牌×双方×BASE/友方战场，明确中盘HAND初态及公开战场锚点，均通过真实typed MatchSession命令。32接受命令、92事件；进场先付款后排技能，乐团选目标后+3、亚龙抽牌、勇士安装壁垒。每步冷恢复/重复与原动作事件回放，12个完整JSON帧及24个独立终态/入场事件伪造拒绝。未将其计作规范SEATING或完整对局。原58例继续覆盖任意合法目标、法盾、时机、源/目标离场代际、触发排序、回合到期、烧尽与终局及壁垒伤害分配。

反向 `dfef195acb3ad1e0b977c536269cae8c290a536d` 与验证源仅差CardBehaviorRegistry三定义，目录和完整隐私前置不变；非交付祖先。前置12隐私控制仍过，新增三卡注册/入场21例失败。最初缺前置的51P/7F原件保留，含既有OGN087，未以删断言或新宽松驱动换绿。

原1040条记录、顺序和来源元数据不变，新增后1043印次/842功能组，重复115/316/201不变；Ahri原1009条历史循环及原目录统计断言保留。既有Driver相对前置无差异，六个原未跟踪文件哈希稳定。

实际RIF `063ef51646b393157a54913bd2dff463821c733369fdad895ec12d06fc9ab9a5`；Engine SHA256 `1eb755cea108d972afd62b600c225da514433a4d27be8b6f4e35341fb3ccdb8b` / MVID `ab0a9d32-c38f-4eee-b886-7fe05bdc2fb3`。2495个编译输入和1418个运行文件前后SHA稳定；新实录与父隐私流程的四模块path/SHA/MVID/RIF一致。

## 可取回原件

本机 `/Users/dinghaolin/MyProjects/.worktrees/riftbound-cards-20261023` 的 ignored `var/qa/cards-direct-pr-migration-20261024/unit-entry-main-port-v1/`：

- `source-runtime-proof-v1.json` SHA256 `ac5397fbc1c210a2c7ee372557d9333780135bade02936335ce4bfbb8835a6df`；`verify-final-v1.py`已机械核对。
- `source-v1/`当前API记录、完整PNG和读图回执；源扫描原完整255项在同级next-family-after-ven159-v1。
- `compile-inputs-v3.json`、`runtime-manifest-v3.json`、`focus-v3/unit-entry.trx`、`adjacent-v3/adjacent-v3.trx`、`original-preconstructed-input-v3/`。
- `focus-v3/entry-evidence/`12个完整实录、`arithmetic-evidence/`3原日志、`privacy-evidence/`2完整父流程；`registration-counter-v1/`私有对照全源/bin/TRX。
- `focus-v1/`保留51P/7F，`focus-v2/`75项及原牌组输入结果单独绑定原输入，不另计最终覆盖。

①已协调ValidateAuthoritativeState与PR136的实际插入点：资源视图校验→空Stack保护→既有栈/战斗视图校验。此源前置尚无PR136调用；后续组合须保留两者，PR146的Empower条件已在前置保留。

本机为L1。父PR及当前PR最新head/base/merge-result的quality、targeted、rule_suite实际L2 required绿后才能按依赖合入；未重跑本机full/SQL/网络/110配对。全1740印次/14293条款范围保留，适用/功能分母UNKNOWN，净整卡认证0，backend NOT READY。
