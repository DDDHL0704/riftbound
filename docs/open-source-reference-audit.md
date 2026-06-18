# Open Source Reference Audit

更新时间：2026-06-18

本文件记录本轮从开源 Riftbound / TCG 项目中吸收的高价值做法。所有条目只作为架构、交互、工具链和布局参考；规则、卡牌效果、合法性与隐藏信息边界仍以本仓库官方 PDF、`data/official`、Riot 官方页面和 `playloltcg` 图鉴为准。

## 高价值参考

| 项目 | 可吸收内容 | 本项目落点 | 权威边界 |
|---|---|---|---|
| [PolluxyShi/fwzc_lite](https://github.com/PolluxyShi/fwzc_lite) | Web 1v1、房间、卡组导入、构筑器、预组和规则 PDF 入口 | 用于检查页面流是否覆盖大厅、房间、卡组、对战、规则复核 | 不作为规则裁决源 |
| [TheCardGoat/tcg-engines](https://github.com/TheCardGoat/tcg-engines) | 多 TCG 引擎边界、协议适配、模拟器、测试和 agent 工作方式 | 采用“协议/引擎/模拟/客户端分层”和“禁止 loose any 绕过协议字段”的工程约束 | 不移植其规则语义 |
| [Frooodle/TCG-Game-Mats](https://github.com/Frooodle/TCG-Game-Mats) | Riftbound 桌垫和区域覆盖层思路 | 桌面区域改为可验证坐标契约，支持 TTS 风格还原 | 区域含义以官方规则为准 |
| [Piltover-Archive/RiftboundDeckCodes](https://github.com/Piltover-Archive/RiftboundDeckCodes) | Riftbound 卡组码编码/解码 | 后续卡组导入/导出候选参考 | 卡组合法性仍由服务端按官方规则判定 |
| [Alan-Cha/silhouette-card-maker](https://github.com/Alan-Cha/silhouette-card-maker) | 多导出格式、TTS / Pixelborn / Piltover Archive 适配 | 后续素材、导入格式、桌面工具互操作参考 | 不采用其卡牌效果解释 |
| [apitcg/riftbound-tcg-data](https://github.com/apitcg/riftbound-tcg-data) | 社区卡牌数据快照 | 可用于对比数据缺口和素材字段 | 不覆盖 `data/official` |
| [CommunityDragon/Phizz](https://github.com/CommunityDragon/Phizz) | Riot API 客户端和内容同步模式 | 后续官方内容缓存、速率限制、数据同步参考 | 仅使用官方接口返回内容 |
| [bcollazo/deckgym-core](https://github.com/bcollazo/deckgym-core) | 可模拟状态、动作导出、卡牌实现状态工具 | 卡牌实现矩阵和 engine smoke 思路 | 规则模型不迁移 |
| [boardgame.io](https://github.com/boardgameio/boardgame.io) | 回合制 state/phases/log/time-travel/multiplayer 模式 | 作为 match log、调试重放和快照审计参考 | 不替换现有 C# 规则引擎 |
| [Card-Forge/forge](https://github.com/Card-Forge/forge) | 大型卡牌规则引擎拆分经验 | 复杂关键词、持续效果、层、替代效果的分层参考 | 不借用 MTG 裁决 |
| [sindreslungaard/duel-masters](https://github.com/sindreslungaard/duel-masters) | 浏览器模拟器、规则引擎包拆分、卡池实现覆盖 | 前后端边界与规则测试组织参考 | 不迁移 Duel Masters 规则 |
| [keeshii/ryuu-play](https://github.com/keeshii/ryuu-play) | TypeScript TCG monorepo、server/play/common/simple bot | 对局客户端/服务器包边界和 bot smoke 参考 | 不迁移 Pokemon 规则 |
| [BAA-Studios/MOOgiwara](https://github.com/BAA-Studios/MOOgiwara) | 浏览器 TCG 桌面交互、实时对战视觉 | TTS 风格桌面密度和拖拽交互参考 | 不作为规则源 |
| [db0/godot-card-game-framework](https://github.com/db0/godot-card-game-framework) | 拖拽、牌堆、hover preview、目标箭头、token 交互 | 后续增强卡牌预览、目标线、交互反馈参考 | 不引入 Godot 技术栈 |

## 已转化为项目改造

- 新增根目录 `AGENTS.md`，把规则权威、引擎通用化、前端只读快照、验证命令写成项目级约束。
- 新增 `docs/tabletop-layout-contract.md`，把 TTS 风格桌面抽象为规则区域契约，而不是只靠 CSS 手感。
- 将 `tabletopLayout` 抽成 JSON 数据源，UI 和检查脚本共享一份布局。
- 新增 `check:tabletop-layout`，自动检查双人区域、双战场、12 张符文和坐标边界。

## 后续可继续吸收

- 卡组码导入/导出：参考 Piltover Archive，但入口必须接服务端合法性校验。
- 卡牌实现矩阵：参考 deckgym 的“卡牌实现状态”方式，把官方卡号映射到 engine primitive、测试和证据文档。
- 重放与调试：参考 boardgame.io 的 log/time-travel 思路，保留每个 intent、prompt、snapshot tick 和事件链。
- 桌面交互：参考 Godot framework / MOOgiwara 的 hover、drag、target arrow，但动作提交仍走服务端 legalActions。
