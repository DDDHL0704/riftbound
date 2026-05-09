# Stage 3B Battlefield / Standby / Control / Conquer Evidence Overlay

更新时间：2026-05-09

结论：本文件只服务阶段 3B：Battlefield / Standby / Control / Conquer lifecycle + Central cleanup queue 最小官方化切片。它不是核心规则引擎实现计划，不标记 READY，不进入 1009 张卡 full-official 覆盖，也不实现完整 battle / damage assignment。

FAQ 来源策略：沿用阶段 2 从五份 PDF/FAQ 抽取的候选页码，不使用 `cardQaList`。3B 只把候选卡牌功能单元与规则域绑定，最终适用性仍需后续人工 adjudication 和测试证明。

## 1. 3B 范围

3B 可覆盖：

- 战场对象、战场控制状态、战场进入争夺/无人控制后的生命周期证据。
- 待命区隐私、失去控制后的待命清理候选。
- 控制权改变、控制权冻结/释放、所属者区域回归的候选证据。
- 征服/据守得分 hook 和触发候选的矩阵标注。
- Central cleanup queue 与移动、进离场、控制权改变、得分、状态性清理的依赖标注。

3B 不覆盖：

- 1009 张卡 full-official 实现。
- 核心规则引擎或前端改动。
- 完整 battle lifecycle、完整 spell duel runtime。
- `ASSIGN_COMBAT_DAMAGE` 完整伤害分配。
- `ORDER_TRIGGERS` runtime。
- 所有战场卡的 full-official 文本实现。
- 装备 / 装配 / LayerEngine 扩张。

## 2. 规则 / FAQ 依据

| 依据 | 3B 用途 |
|---|---|
| `CORE-260330 p4-p8 rules 107.2-107.3` | 战场是公开位置；每处战场有独立待命子区域。 |
| `CORE-260330 p22-p26 rules 187-189` | 所属者、控制者、战场控制与战场技能控制者。 |
| `CORE-260330 p28-p33 rules 315.2.b.2, 319-323` | 据守得分和清理任务队列。 |
| `CORE-260330 p35-p36 rules 344-348` | 争夺无人控制战场后的非战斗法术对决。 |
| `CORE-260330 p77-p78 rules 461-464` | 战斗清理、控制确立、征服/据守相关结果。 |
| `JFAQ-251023 p5-p7 q4.1-5.4` | 战斗/法术对决期间控制权冻结，清理时机与失控待命移除。 |
| `SOUL-OFAQ-260114 p21` | 《恶意收购》类控制权改变 / 非战斗法术对决候选。 |
| `SOUL-JFAQ-260114 p4-p5` | 战斗胜负、征服/据守和得分触发候选。 |
| `SOUL-OFAQ-260114 p19-p20` | 0/负战力与有效伤害清理语义，作为 cleanup queue 边界。 |

## 3. 3B Functional Units

| FU | 代表卡 | 3B 用法 | priority | FAQ | 边界 |
|---|---|---|---|---|---|
| `FU-05ce012700` | `SFD·218/221` 沉没神庙 | 战场卡 / 控制状态 / 征服据守证据核心 | P0 | `SOUL-JFAQ-260114 p8`, `SOUL-OFAQ-260114 p15` | 非 PLAY_CARD 战场域代表，不声明所有战场 full-official。 |
| `FU-00ee09c2cc` | `SFD·202/221` 恶意收购 | 控制权改变、控制冻结 FAQ 候选 | P0 | `SOUL-JFAQ-260114 p22`, `SOUL-OFAQ-260114 p21` | 只做控制权生命周期证据，不扩张到完整 battle / hidden info。 |
| `FU-813144e7d4` | `OGN·168/298` 战或逃 | 战场单位回基地、移动后 cleanup 证据 | P0 | `JFAQ-251023 p4`, `SOUL-JFAQ-260114 p12/p16` | 不关闭完整 spell duel、targeting 或 payment 覆盖。 |
| `FU-8dae5c40be` | `OGN·121/298` 提莫 | 待命区、正面朝下隐私、失控待命清理候选 | P1 | 无 | 不执行完整提莫战斗文本或伤害分配。 |
| `FU-e3dcc3b30f` | `OGN·199/298` 控潮者 | 待命 swap / 移动 / 隐私候选 | P1 | `SOUL-JFAQ-260114 p6` | 不用它概括全部 standby 规则。 |
| `FU-7f4a387b92` | `OGN·056/298` 自适应机器人 | 征服 trigger / boon-after-conquer 候选 | P1 | 无 | 只标注 conquer hook，boon/layer 不在 3B 完成。 |
| `FU-c027639a3c` | `OGN·035/298` 薇恩 | 征服 recall / control-zone movement 候选 | P1 | 无 | 不拉入完整 assault 或 combat damage。 |
| `FU-90673ef9fd` | `OGN·285/298` 劫掠船巷 | 战场 FAQ / control freeze / cleanup 候选 | P1 | `JFAQ-251023 p5/p6` | 战场 FAQ 候选，不是该战场卡 full-official。 |
| `FU-6c99fc0e2e` | `OGN·277/298` 后巷酒吧 | cleanup / control battlefield-domain 支撑 | P1 | `JFAQ-251023 p5/p6` | 不扩张到全部战场替换或触发能力。 |
| `FU-d18ac7cbec` | `UNL-209/219` 暮色玫瑰实验室 | cleanup / control / visibility 支撑 | P1 | 无 | 只做支持候选，不完成该战场卡文本。 |
| `FU-95b4531e4e` | `SFD·125/221` 大力仙灵 | 战场占位 / 移动目标 body fixture | P1 | 无 | 只作为对象 fixture，不执行自身卡面文本。 |

3B holdback：

| FU | 代表卡 | 原因 |
|---|---|---|
| `FU-5bcc4063c2` | `SFD·143/221` 希维尔 | 急速 payment、持续/清理和 LayerEngine 风险，除非 A 单独打开。 |
| `FU-4e2e19359f` | `UNL-179/219` 峡谷先锋 | Last-breath + cleanup 压力候选，留到 3B 后。 |
| `FU-0681eefc4e` | `UNL-140/219` 强制征召 | 第二控制权/召回压力候选，留到最小控制切片稳定后。 |

## 4. 测试卡组姿态

3B 测试卡组不是 full-official decklist，只是规则域 fixture pool：

- 战场域：`SFD·218/221`, `OGN·285/298`, `OGN·277/298`, `UNL-209/219`。
- 占位/移动：`SFD·125/221`, `OGN·168/298`。
- 控制权改变：`SFD·202/221`。
- 待命/隐私：`OGN·121/298`, `OGN·199/298`。
- 征服/据守 hook：`OGN·056/298`, `OGN·035/298`。

这些卡只允许证明 3B lifecycle 证据边界。任何测试通过都不得自动升级为对应卡牌完整官方文本完成。

## 5. 后续 Battle / Damage 压测清单

以下 functional units 适合在 3B 之后压测 battle lifecycle / damage assignment，但不在 3B 实现：

| FU | 代表卡 | 压测原因 |
|---|---|---|
| `FU-104211dbbc` | `SFD·148/221` 德莱文 | Top20 #3；battle / cleanup / FAQ / payment 复合风险。 |
| `FU-964b214448` | `SFD·020/221` 德莱文 | Top20 #4；基础战斗单位与 FAQ 复合风险。 |
| `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | combat damage 后移动，控制/移动与 damage assignment 交叉。 |
| `FU-422b450261` | `SFD·170/221` 雷克塞 | attack reveal、隐藏信息、战斗状态交叉。 |
| `FU-1945f6918c` | `SFD·029/221` 雷克塞 | overwhelm / battle / hidden-info 压力候选。 |
| `FU-9f7cb73dc4` | `UNL-150/219` 薇古丝 | spellshield / stun / battle 状态交叉。 |
| `FU-ee886701e4` | `OGN·220/298` 强手裂颅 | 双方战场单位 stun 与 cleanup/battle 交叉。 |
| `FU-0b6332bbf0` | `SFD·145/221` 换换乐 | 战场单位战力交换与 damage/cleanup 边界。 |
| `FU-a9dc3495e1` | `OGN·256/298` 妖异狐火 | 多单位摧毁、战力阈值、cleanup。 |
| `FU-64a7f67581` | `OGN·262/298` 天顶之刃 | 敌方战场单位目标、stun、cleanup。 |
| `FU-4329e00e20` | `UNL-198/219` 月之降临 | 战场单位负战力、cleanup、防回归。 |
| `FU-b646702ec0` | `OGN·268/298` 弹幕时间 | PAY_COST 已在 3A；完整 damage assignment 仍留后。 |

## 6. 仍阻断 Full-official

- 1009 snapshot entries / 811 functional units 的逐项 FAQ adjudication 尚未完成。
- Central cleanup queue 仍需由 B/D 证明所有命令、结算、触发、移动、进离场、伤害/战力变化都能统一 enqueue。
- Battle / spell duel lifecycle、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 仍是 full-official 阻断。
- LayerEngine、替代/持续效果、隐藏信息/随机区域、完整 PaymentEngine 仍未清零。

是否允许批量覆盖：**不允许。**
