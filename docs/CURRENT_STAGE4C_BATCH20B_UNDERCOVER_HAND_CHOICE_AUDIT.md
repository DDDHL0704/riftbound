# 阶段 4C-20B Undercover Agent Triggered Hand-Choice 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对 Undercover Agent / 卧底特工 `OGN·178/298` / `FU-6a52b04cb2` 触发结算中 hidden hand-choice prompt 微切片的文档、规则证据与 P0/P1 审计口径。本批只关闭 Undercover Agent 服务端 `HAND_CHOICE` / `CHOOSE_HAND_CARDS` 微切片，不代表 full-official，不标记 READY / READY-CANDIDATE。

## 范围

- 只覆盖 Undercover Agent 绝念在触发结算时打开服务端权威手牌选择 prompt 的代表路径。
- 服务端负责生成 `HAND_CHOICE` / `CHOOSE_HAND_CARDS` prompt、验证提交者与候选手牌、执行弃牌并继续抽两张。
- 手牌不足两张时按 `CORE-260330` p62 / rule `422.4` 处理：弃尽可弃数量，仍抽两张。
- `handChoices` / 手札候选只对选择玩家可见；非选择玩家只看到脱敏等待信息。
- wrong player、stale prompt、invalid choice、malformed / illegal payload 均应拒绝且 no mutation。
- C 已完成前端专用接线；前端只展示服务端 `HAND_CHOICE` prompt candidates，并提交 `CHOOSE_HAND_CARDS`，不得本地裁决规则。
- 本批不实现 Karthus 额外绝念，不实现非 Undercover 的通用 discard / hand-choice engine，不覆盖其它 hand-choice FUs。
- 本批不进入 FAQ regression、1009 / 811 full-official 或正式 18-step E2E。

## 规则证据入口

- `CATALOG` `OGN·178/298`：Undercover Agent / 卧底特工官方卡牌文本与 FU `FU-6a52b04cb2`。
- `CORE-260330` p4-p8 rules 107-129：隐藏信息、对象与 viewer redaction 边界。
- `CORE-260330` p31-p35 rules 318-340：任务 / 结算链、priority 与触发结算入口。
- `CORE-260330` p52-p55 rules 383.3.d-383.3.e：触发技能与多触发排序入口。
- `CORE-260330` p62 / rule `422.4`：效果包含弃牌时弃尽可弃数量；Undercover Agent 例子明确 2 / 1 / 0 手牌均抽两张。

## 已验证证据

- 服务端实现：B 已实现 Undercover Agent 触发结算中的 `HAND_CHOICE` / `CHOOSE_HAND_CARDS` 微切片。
- 服务端验证：A focused backend test `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~UndercoverAgentTriggerTests"` 通过 6/6。
- 已关闭服务端子项：triggered hand-choice prompt、viewer-specific `handChoices` redaction、wrong player / stale / invalid / no-mutation validation、1 / 0 hand shortfall。
- C frontend sync 已完成：前端类型、ActionPanel、状态面板、事件日志和 smoke / preflight 均已接入 `HAND_CHOICE` / `CHOOSE_HAND_CARDS`；前端仍只提交服务端候选，不结算弃牌或抽牌。
- A full validation：backend full 3398/3398、frontend build、Chrome smoke、stage3 preflight、`git diff --check`、matrix JSON 和 4C-20B overlay assertions 均通过。

## 仍保留 P0/P1

- P0：Karthus 额外绝念 optional / multiplicity / multi-Karthus / visibility 裁决仍未实现。
- P0：非 Undercover 的通用 discard / hand-choice engine 仍未关闭。
- P1：其它 hand-choice FUs、public/private discard event redaction 全矩阵、replay / spectator hand-choice redaction 仍需扩展。
- P0：完整 trigger engine、完整 effect resolution、trigger batch、完整 APNAP 组合仍未关闭。
- P0：FAQ regression、1009 entries / 811 functional units full-official 覆盖。
- P0：正式 18-step E2E 与 completion audit。

4C-20B 当前判断：**Undercover Agent triggered hand-choice server prompt 微切片已验证，但不能关闭 READY 阻断**。
