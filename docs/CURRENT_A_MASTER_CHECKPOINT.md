# A 主控 Checkpoint

更新日期：2026-05-14
当前结论：**NOT READY**

本文是 A 主控架构 agent 的恢复入口。任何窗口中断或 Codex 关闭后，先读本文，再读 `README.md`、`docs/START_HERE.md`、`docs/符文战场_前端Web开发需求文档_给Codex.md`、`docs/符文战场_服务端核心规则自查文档.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_FRONTEND_REBUILD_PLAN.md`、`docs/CURRENT_COMPLETION_AUDIT.md`，然后用 `git status --short --branch` 和 `git log --oneline -8` 对齐仓库事实。

2026-05-14 最新补充：4D-02I Battle response payment-resource context test-only slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_02I_BATTLE_RESPONSE_PAYMENT_RESOURCE_CONTEXT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02I_BATTLE_RESPONSE_PAYMENT_RESOURCE_CONTEXT_EVIDENCE.md`；focused 431/431、adjacent 608/608、backend full 4199/4199、`git diff --check` 通过。natural `DECLARE_BATTLE` 携带 held-score `RECYCLE_RUNE:*` optional cost 且存在合法 Shadow battle response 时，现在有 dedicated guard 证明 response pass 后保留 payment-resource context，进入 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`，并输出 `RUNE_RECYCLED` / `POWER_GAINED` / `COST_PAID` / `SCORE_GAINED` 审计；内部 carrier 继续不进入公开 snapshot / prompt。本切片不改 runtime、不做 full combat rewrite、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02J Battle response temporary payment-resource context test-only slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_02J_BATTLE_RESPONSE_TEMP_PAYMENT_RESOURCE_CONTEXT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02J_BATTLE_RESPONSE_TEMP_PAYMENT_RESOURCE_CONTEXT_EVIDENCE.md`；focused 432/432、adjacent 608/608、backend full 4200/4200、`git diff --check` 通过。natural `DECLARE_BATTLE` 携带 held-score `TEMP_PAYMENT_RESOURCE:*` optional cost 且存在合法 Shadow battle response 时，现在有 dedicated guard 证明 response pass 后保留 temporary payment-resource context，进入 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`，并输出 `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `TEMPORARY_PAYMENT_RESOURCE_CLEARED` / `COST_PAID` / `SCORE_GAINED` 审计；内部 carrier 继续不进入公开 snapshot / prompt。本切片不改 runtime、不做 full combat rewrite、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02K Battle response activation Brush context handoff / baseline 已建立。交接规格见 `docs/CURRENT_STAGE4D_02K_BATTLE_RESPONSE_ACTIVATION_BRUSH_CONTEXT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_02K_BATTLE_RESPONSE_ACTIVATION_BRUSH_CONTEXT_BASELINE_EVIDENCE.md`；focused baseline 432/432、adjacent baseline 608/608、backend full 4200/4200、`git diff --check` 通过。下一 P0-004 窄切片锁定 `BRUSH_USE_REPLACED_BATTLEFIELD:*` declaration context 穿过真实 Shadow `ACTIVATE_ABILITY` / stack resolution / return-to-response 后，最终 pass 仍进入 replacement-aware held-score path。本 handoff 不改 runtime、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02K Battle response activation Brush context test-only slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_02K_BATTLE_RESPONSE_ACTIVATION_BRUSH_CONTEXT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02K_BATTLE_RESPONSE_ACTIVATION_BRUSH_CONTEXT_EVIDENCE.md`；focused 433/433、adjacent 608/608、backend full 4201/4201、`git diff --check` 通过。`BRUSH_USE_REPLACED_BATTLEFIELD:*` declaration context 现在有 dedicated guard 证明可穿过真实 Shadow activation、stack pass-pass resolution、return-to-response priority 与 final response pass-pass，并最终进入 replacement-aware held-score payment / score / battle close path；internal carrier 在 activation 后和 stack resolution 后继续不进入公开 snapshot / prompt。本切片不改 runtime、不做 full combat rewrite、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02J Battle response temporary payment-resource context handoff / baseline 已建立。交接规格见 `docs/CURRENT_STAGE4D_02J_BATTLE_RESPONSE_TEMP_PAYMENT_RESOURCE_CONTEXT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_02J_BATTLE_RESPONSE_TEMP_PAYMENT_RESOURCE_CONTEXT_BASELINE_EVIDENCE.md`；focused baseline 431/431、adjacent baseline 608/608、backend full 4199/4199、`git diff --check` 通过。下一 P0-004 窄切片锁定 natural `DECLARE_BATTLE` 携带 held-score `TEMP_PAYMENT_RESOURCE:*` optional cost 且存在合法 battle response 时，response pass 后保留 temporary payment-resource context 并进入 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` payment / audit path。本 handoff 不改 runtime、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02H Battle response Brush replacement context test-only slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_02H_BATTLE_RESPONSE_BRUSH_REPLACEMENT_CONTEXT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02H_BATTLE_RESPONSE_BRUSH_REPLACEMENT_CONTEXT_EVIDENCE.md`；focused 430/430、adjacent 608/608、backend full 4198/4198、`git diff --check` 通过。natural `DECLARE_BATTLE` 携带 `BRUSH_USE_REPLACED_BATTLEFIELD:*` 且存在合法 Shadow battle response 时，现在有 dedicated guard 证明 response pass 后保留 Brush replacement context，触发 `BATTLEFIELD_REPLACEMENT_APPLIED` 并用 original held-score battlefield identity 结算支付得分；内部 carrier 继续不进入公开 snapshot / prompt。本切片不改 runtime、不做 full combat rewrite、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02I Battle response payment-resource context handoff / baseline 已建立。交接规格见 `docs/CURRENT_STAGE4D_02I_BATTLE_RESPONSE_PAYMENT_RESOURCE_CONTEXT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_02I_BATTLE_RESPONSE_PAYMENT_RESOURCE_CONTEXT_BASELINE_EVIDENCE.md`；focused baseline 430/430、adjacent baseline 608/608、backend full 4198/4198、`git diff --check` 通过。下一 P0-004 窄切片锁定 natural `DECLARE_BATTLE` 携带 held-score `RECYCLE_RUNE:*` / `TEMP_PAYMENT_RESOURCE:*` optional cost 且存在合法 battle response 时，response pass 后保留 payment-resource context 并进入 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` payment / audit path。本 handoff 不改 runtime、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02H Battle response Brush replacement context handoff / baseline 已建立。交接规格见 `docs/CURRENT_STAGE4D_02H_BATTLE_RESPONSE_BRUSH_REPLACEMENT_CONTEXT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_02H_BATTLE_RESPONSE_BRUSH_REPLACEMENT_CONTEXT_BASELINE_EVIDENCE.md`；focused baseline 429/429、adjacent baseline 608/608、backend full 4197/4197、`git diff --check` 通过。下一 P0-004 窄切片锁定 natural `DECLARE_BATTLE` 携带 `BRUSH_USE_REPLACED_BATTLEFIELD:*` optional cost 且存在合法 battle response 时，response pass 后保留 Brush replacement context 并进入 held-score replacement 分支。本 handoff 不改 runtime、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02G Battle response declaration-context focused slice 已实现并验证，审计与证据见 `docs/CURRENT_STAGE4D_02G_BATTLE_RESPONSE_DECLARATION_CONTEXT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02G_BATTLE_RESPONSE_DECLARATION_CONTEXT_EVIDENCE.md`；focused 429/429、adjacent 608/608、backend full 4197/4197、`git diff --check` 通过。natural `DECLARE_BATTLE` 携带 Icevale Archer battlefield target context 且存在合法 Shadow battle response 时，现在先开启 `BATTLE_RESPONSE_PRIORITY_OPENED`，双方 pass 后服务端复用原 declaration context 打开 trigger payment，并过滤内部 carrier 以避免 player / spectator snapshot 或 prompt 泄漏。本切片不做 full combat rewrite、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02G Battle response declaration-context handoff / baseline 已建立。交接规格见 `docs/CURRENT_STAGE4D_02G_BATTLE_RESPONSE_DECLARATION_CONTEXT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_02G_BATTLE_RESPONSE_DECLARATION_CONTEXT_BASELINE_EVIDENCE.md`；focused baseline 428/428、adjacent baseline 608/608、backend full 4196/4196、`git diff --check` 通过。下一 P0-004 窄切片锁定带 battlefield target / brush replacement / payment-resource context 的 natural `DECLARE_BATTLE` 在存在合法 battle response 时仍必须先开启 `BATTLE_RESPONSE_PRIORITY_OPENED`，并在双方 pass 后保留原 declaration context 继续结算。本 handoff 不改 runtime、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02E Battle task advancement focused slice 已实现并验证，审计与证据见 `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_EVIDENCE.md`；focused 427/427、adjacent 608/608、backend full 4195/4195、`git diff --check` 通过。`ASSIGN_COMBAT_DAMAGE` 关闭当前 natural battle 后现在会在无 cleanup / stack / active battle / active spell duel 阻塞时推进下一个 contested battlefield `START_SPELL_DUEL`，避免多争夺战场停在 `BATTLEFIELD_TASKS` / `WAIT`。本切片不做 full combat rewrite、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02F Battle assignment no-result focused slice 已实现并验证，审计与证据见 `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_EVIDENCE.md`；focused 428/428、adjacent 608/608、backend full 4196/4196、`git diff --check` 通过。natural `ASSIGN_COMBAT_DAMAGE` 分支现在有双方全灭 `BATTLE_NO_RESULT` / `BattleResolutionState.NO_RESULT` / matching `START_BATTLE` cleanup guard，并补上参战者已被 cleanup 移除时仍 emit `BATTLE_CLOSED` 的审计事件。本切片不做 full combat rewrite、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02F Battle assignment no-result handoff / baseline 已建立。交接规格见 `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_BASELINE_EVIDENCE.md`；focused baseline 427/427、adjacent baseline 608/608、backend full 4195/4195、`git diff --check` 通过。下一 P0-004 窄切片锁定 natural `ASSIGN_COMBAT_DAMAGE` 分支中双方全部被摧毁时的 `BATTLE_NO_RESULT` / `BattleResolutionState.NO_RESULT` / task cleanup guard。本 handoff 不改 runtime、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02D Battle response -> assignment integration guard 已验收，审计与证据见 `docs/CURRENT_STAGE4D_02D_BATTLE_RESPONSE_ASSIGNMENT_INTEGRATION_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02D_BATTLE_RESPONSE_ASSIGNMENT_INTEGRATION_EVIDENCE.md`；focused 426/426、adjacent 607/607、backend full 4194/4194 通过。本 test-only 切片证明同一 natural active `START_BATTLE` 中，合法 Shadow battle-response 会先于 assignment window 打开，双方 pass 后再进入 `ASSIGN_COMBAT_DAMAGE` prompt，并在 legal assignment 后清理 matching `START_BATTLE` task。本切片不改 runtime、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02E Battle task advancement handoff / baseline 已建立。交接规格见 `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_BASELINE_EVIDENCE.md`；focused baseline 426/426、adjacent baseline 607/607、backend full 4194/4194 通过。下一 P0-004 窄切片锁定 `ASSIGN_COMBAT_DAMAGE` 关闭当前 natural battle 后推进下一个 contested battlefield `START_SPELL_DUEL`，避免多争夺战场停在 `BATTLEFIELD_TASKS` / `WAIT`。本 handoff 不改 runtime、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02C Natural battle damage assignment focused slice 已实现并验证，审计与证据见 `docs/CURRENT_STAGE4D_02C_NATURAL_BATTLE_DAMAGE_ASSIGNMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02C_NATURAL_BATTLE_DAMAGE_ASSIGNMENT_EVIDENCE.md`；focused 425/425、adjacent 607/607、backend full 4193/4193 通过。自然 active `START_BATTLE` / minimal `DECLARE_BATTLE + COMBAT_ASSIGNMENT` 现在可在多防守且含 `Bulwark` / `Back Row` assignment-ordering 时开启 `ASSIGN_COMBAT_DAMAGE` prompt，合法 assignment 后复用 simultaneous damage / cleanup / battle close / battlefield control runtime，并确认 matching `START_BATTLE` 不残留；一对一 immediate battle 和 4D-02B Shadow battle-response path 保持稳定。本切片不做完整 combat rewrite、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02C Natural battle damage assignment handoff / baseline 已建立。剩余范围审计见 `docs/CURRENT_STAGE4D_02C_BATTLE_LIFECYCLE_REMAINING_SCOPE_AUDIT.md`，交接规格见 `docs/CURRENT_STAGE4D_02C_NATURAL_BATTLE_DAMAGE_ASSIGNMENT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_02C_NATURAL_BATTLE_DAMAGE_ASSIGNMENT_BASELINE_EVIDENCE.md`；focused baseline 319/319、adjacent baseline 598/598、backend full 4188/4188 通过。下一 P0-004 切片锁定 natural active `START_BATTLE` -> `DECLARE_BATTLE` -> optional battle-response pass -> `ASSIGN_COMBAT_DAMAGE` prompt/runtime -> cleanup/control 的最小官方化链路；本 handoff 不改 runtime、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-02B Battle-response priority lifecycle binding focused slice 已实现并验证，审计与证据见 `docs/CURRENT_STAGE4D_02B_BATTLE_RESPONSE_PRIORITY_LIFECYCLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02B_BATTLE_RESPONSE_PRIORITY_LIFECYCLE_EVIDENCE.md`；focused 420/420、adjacent 607/607、backend full 4188/4188 通过。自然 `DECLARE_BATTLE` / contested `START_BATTLE` task 现在可在存在合法 Shadow swift response 时开启 battle-response priority prompt，Shadow 可从该窗口 activation / stack pass-pass / stun，并在双方 pass 后继续 battle close；该 deferred path 限定于最小 `COMBAT_ASSIGNMENT` 命令形态，无合法 battle-response action 或含额外 battle target / payment-resource / brush replacement 选择的普通战斗仍即时结算。本切片不做完整 combat rewrite、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-02B handoff / baseline 已建立：下一 P0 收口切片锁定 battle-response priority lifecycle binding，交接规格见 `docs/CURRENT_STAGE4D_02B_BATTLE_RESPONSE_PRIORITY_LIFECYCLE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_02B_BATTLE_RESPONSE_PRIORITY_LIFECYCLE_BASELINE_EVIDENCE.md`。目标是证明 Shadow swift / reaction prompt 可从真实 `DECLARE_BATTLE` / contested battlefield task queue / battle lifecycle 自然产生，而不是继续依赖构造状态。本切片不做完整 combat rewrite、不启动 LayerEngine、不扩 PaymentEngine、不改前端、不关闭 P0-004 / P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-03AH PaymentEngine action-window coverage verifier 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_EVIDENCE.md`；focused 717/717、backend full 4182/4182、`git diff --check` 通过。本切片新增 server-side conformance audit manifest，枚举 `PLAY_CARD` / `PAY_COST` / `TRIGGER_PAYMENT` / `ASSEMBLE_EQUIPMENT` / `ACTIVATE_ABILITY` / `LEGEND_ACT` / battlefield held score payment / `HIDE_CARD` / `MOVE_UNIT`，并分类为 `representative-covered` 或 `policy-non-resource`；`MOVE_UNIT` 明确不是 resource-payment window。项目仍 **NOT READY**，P0-005 / P1 / READY 未关闭。

2026-05-14 4D-03AH handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 action-window coverage verifier，交接规格见 `docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_BASELINE_EVIDENCE.md`。目标是用 server-side conformance audit test 枚举 `PLAY_CARD` / `PAY_COST` / `TRIGGER_PAYMENT` / `ASSEMBLE_EQUIPMENT` / `ACTIVATE_ABILITY` / `LEGEND_ACT` / battlefield held payment / `HIDE_CARD` / `MOVE_UNIT`，并分类为 representative / policy-non-resource / remaining-gap。本切片不改 runtime、不更新 card matrix、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-03AG `PLAY_CARD` typed resource prompt parity focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_EVIDENCE.md`；focused 454/454、backend full 4177/4177、`git diff --check` 通过。`PLAY_CARD` typed optional power costs 现在只按合法 quoted payment resource choices 计算 prompt metadata，wrong-trait recycle / temporary resource 不再抬高 aggregate；matching typed temporary resource 可被 quote 并 command commit。本切片不扩 action-window verifier、不新增卡、不改前端、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-03AG handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `PLAY_CARD` typed optional power costs 的 inline payment-resource prompt parity。交接规格见 `docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_BASELINE_EVIDENCE.md`。本切片只修 server authoritative prompt choices / metadata 与 command typed payability 的一致性，不扩 action-window verifier、不新增卡、不改前端、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-03AF A 侧 PaymentEngine remaining-scope audit 已建立：审计与基线见 `docs/CURRENT_STAGE4D_03AF_PAYMENT_ENGINE_REMAINING_SCOPE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AF_PAYMENT_ENGINE_REMAINING_SCOPE_BASELINE_EVIDENCE.md`；focused PaymentEngine / movement / activated ability / keyword adjacent baseline 587/587 通过。结论：4D-03AE 后 PaymentEngine 已有强 representative breadth，但 full official P0-005 仍缺一个 action-window coverage verifier / checklist，尤其需明确 `MOVE_UNIT` 作为 movement-permission optional-cost window 的 coverage policy；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-03AE pending temporary resource prompt aggregate parity focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_EVIDENCE.md`；focused 170/170、backend full 4173/4173、`git diff --check` 通过。pending `PAY_COST` / `TRIGGER_PAYMENT` 的 `availablePowerWithPaymentResources` 现在只按合法 `paymentResourceChoices` 口径计入 recycle / temporary resources，避免合法 typed temporary resource 双算与 wrong-trait temporary resource 虚增 aggregate。本切片不扩展命令行为、不新增 resource-skill 卡、不修改前端、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-03AE handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 pending `PAY_COST` / `TRIGGER_PAYMENT` 对 `TEMP_PAYMENT_RESOURCE:*` aggregate prompt metadata 的 parity 修正。交接规格见 `docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_BASELINE_EVIDENCE.md`；focused baseline 167/167、`git diff --check` 通过。本切片只修 `availablePowerWithPaymentResources` legal-choice 口径，不新增 resource-skill 卡、不修改前端、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-03AD trigger temporary payment resource focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AD_PAYMENT_ENGINE_TRIGGER_TEMP_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AD_PAYMENT_ENGINE_TRIGGER_TEMP_RESOURCE_EVIDENCE.md`；focused 149/149、backend full 4170/4170、`git diff --check` 通过。SFD Fiora `TRIGGER_PAYMENT` 现在支持 typed-yellow `TEMP_PAYMENT_RESOURCE:*` prompt quote / command commit / audit。本切片不扩展所有 trigger payment、不让 temporary resources 支付 mana-only trigger payments、不进入 LayerEngine、不修改前端、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-03AD handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 SFD Fiora `TRIGGER_PAYMENT` 对 `TEMP_PAYMENT_RESOURCE:*` 的 typed-yellow prompt / command / audit parity。交接规格见 `docs/CURRENT_STAGE4D_03AD_PAYMENT_ENGINE_TRIGGER_TEMP_RESOURCE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AD_PAYMENT_ENGINE_TRIGGER_TEMP_RESOURCE_BASELINE_EVIDENCE.md`；focused baseline 137/137、`git diff --check` 通过。本切片不扩展所有 trigger payment、不让 temporary resources 支付 mana-only trigger payments、不进入 LayerEngine、不修改前端、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-03AC battlefield held temporary payment resource focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_EVIDENCE.md`；focused 221/221、backend full 4158/4158、`git diff --check` 通过。`BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 现在支持 `TEMP_PAYMENT_RESOURCE:*` prompt quote / command commit / audit，并保留 mixed `RECYCLE_RUNE:*` + temporary resource 支付代表行为。本切片不新增 resource-skill 卡、不进入 LayerEngine、不修改前端、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-03AC handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 对 `TEMP_PAYMENT_RESOURCE:*` 的 prompt quote / command commit / audit parity。交接规格见 `docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_BASELINE_EVIDENCE.md`；exploratory baseline 73/73、focused baseline 208/208、`git diff --check` 通过。本切片不新增 resource-skill 卡、不进入 LayerEngine、不修改前端、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-03AB Brush battlefield replacement focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_EVIDENCE.md`；focused 141/141、adjacent 511/511、Brush focused 8/8、backend full 4144/4144、`git diff --check` 通过。`UNL·T03` 草丛现在通过 `BRUSH_USE_REPLACED_BATTLEFIELD:<original>` 支持 score-time effective battlefield replacement representative，token deferred catalog 已清空。本切片不修改前端、不修改 coverage matrix、不关闭 P0/P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-03AA Image copy-token focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_EVIDENCE.md`；focused 16/16、adjacent 253/253、backend full 4136/4136、`git diff --check` 通过。`UNL·T06` 映像 copy representative 现在从 deferred catalog 退役，Mirror Image / LeBlanc Image 在当前对象模型中复制 `CardNo` / `Power` / tags 并写入 copied-card audit metadata，Brush battlefield replacement 继续 deferred。本切片不修改前端、不修改 coverage matrix、不关闭 P0/P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-03AB handoff / baseline 已建立：下一服务端 token battlefield replacement 切片锁定 `UNL·T03` 草丛 `当你在此处得分时，你可以选择使用被此牌替代的战场来替代此牌`，目标是在有 score-time replacement runtime representative 后退役最后一个 token deferred surface。交接规格见 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_BASELINE_EVIDENCE.md`；focused baseline 141/141、`git diff --check` 通过。本切片不修改前端、不修改 coverage matrix、不关闭 P0/P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-03AA handoff / baseline 已建立：下一服务端 token copy 切片锁定 `UNL·T06` 映像 `当我被打出时，变为某张卡牌的复制体`，目标是只退役 `TOKEN_DEFERRED_IMAGE_COPY_SOURCE_REQUIRED`，让 Mirror Image / LeBlanc Image 在当前对象模型中一致复制 `CardNo` / `Power` / tags 并审计 `copiedCardNo`，同时保留 Brush battlefield replacement deferred。交接规格见 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_BASELINE_EVIDENCE.md`；focused baseline 11/11、`git diff --check` 通过。本切片不修改前端、不修改 coverage matrix、不关闭 P0/P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-03Z Baron Nest token battlefield static focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_EVIDENCE.md`；focused 77/77、adjacent 345/345、backend full 4131/4131、`git diff --check` 通过。`UNL·T01` 男爵巢穴现在作为 destination-specific no-ROAM movement representative 接入服务端 `MOVE_UNIT` / prompt / catalog audit，事件写入 `movementPermission="BARON_NEST_MOVE_STATIC"` 且不冒充 `游走`；Image copy-token 与 Brush battlefield replacement 继续 deferred。本切片不修改前端、不修改 coverage matrix、不关闭 P0/P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-03Z handoff / baseline 已建立：下一服务端 token battlefield static 切片锁定 `UNL·T01` 男爵巢穴 `单位可从任意位置移动到此处`，目标是只退役 `TOKEN_DEFERRED_BARON_NEST_MOVE_STATIC`，让受控正面非战斗单位可从另一处精确战场 no-ROAM 移动到受控 Baron Nest，同时保留 Image copy-token 与 Brush battlefield replacement deferred。交接规格见 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_BASELINE_EVIDENCE.md`；focused baseline 66/66、`git diff --check` 通过。本切片不修改前端、不修改 coverage matrix、不关闭 P0/P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-03Y battlefield deferred catalog focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_EVIDENCE.md`；focused 15/15、adjacent 641/641、backend full 4120/4120、`git diff --check` 通过。`P6BattlefieldEffectCatalog.GetDeferredSurfaces()` 已为空，Poro Forge / Dream Tree / Blood Altar / Blackflame Altar 四个旧 deferred representative 改由 `GetImplementedBattlefieldRuleSurfaces()` 审计 `BATTLEFIELD_RULE_DOMAIN` implemented 状态，同时继续锁住 activated-grant `ACTIVATE_ABILITY` 中文拒绝与 no-mutation。本切片不修改 runtime、不关闭 P0/P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-03Y handoff / baseline 已建立：下一服务端 catalog hygiene 切片锁定 `P6BattlefieldEffectCatalog` 四个已由 `BATTLEFIELD_RULE_DOMAIN` 实现的旧 deferred battlefield representatives（Poro Forge / Dream Tree / Blood Altar / Blackflame Altar），目标是退役过期 deferred 语义，同时保持 `ACTIVATE_ABILITY` blocked、battlefield positive 和 `BATTLEFIELD_RULE_DOMAIN` BehaviorSpec 证据。交接规格见 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_BASELINE_EVIDENCE.md`；focused baseline 15/15、adjacent baseline 641/641、`git diff --check` 通过。本切片不修改 runtime 规则、不关闭 P0/P1 / READY；项目仍 **NOT READY**。

2026-05-14 最新补充：4D-03X legend action deferred catalog focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_EVIDENCE.md`；focused 59/59、adjacent 285/285、backend full 4120/4120、`git diff --check` 通过。`P6LegendAbilityCatalog.GetDeferredSurfaces()` 已为空，Yasuo / Lee Sin / Diana / Poppy / Viktor 五个旧 deferred representative 改由 `GetImplementedLegendActionSurfaces()` 审计 `LEGEND_ACT` / `LEGEND_ACTION_DOMAIN` implemented 状态，同时继续锁住手写 `ACTIVATE_ABILITY` 中文拒绝与 no-mutation。本切片不修改 runtime、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

2026-05-14 4D-03X handoff / baseline 已建立：下一服务端 catalog hygiene 切片锁定 `P6LegendAbilityCatalog` 里五个已由 `LEGEND_ACT` 实现的旧 deferred representative（Yasuo / Lee Sin / Diana / Poppy / Viktor），目标是退役过期 deferred 语义，同时保持 `ACTIVATE_ABILITY` blocked、`LEGEND_ACT` positive 和 `LEGEND_ACTION_DOMAIN` BehaviorSpec 证据。交接规格见 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_BASELINE_EVIDENCE.md`；focused baseline 54/54、adjacent baseline 279/279 通过。本切片不修改 runtime 规则、不关闭 P0-005 / P1 / READY；项目仍 **NOT READY**。

最新 Stage 4D 状态：4D-03 PaymentEngine focused foundation 已通过；4D-03B non-play payment focused slice 已通过；4D-03C play optional / extra payment focused slice 已通过；4D-03D `ACTIVATE_ABILITY` payment resource focused slice 已通过；4D-03E `HIDE_CARD` payment focused slice 已通过；4D-03F pending `PAY_COST` resource focused slice 已通过；4D-03G battlefield held score resource focused slice 已通过；4D-03H trigger payment resource focused slice 已通过；4D-03I Malzahar resource skill focused slice 已通过；4D-03J Malzahar lifecycle focused slice 已通过；4D-03K temporary resource inline focused slice 已通过；4D-03L reaction resource skill focused slice 已通过；4D-03M colored activated draw focused slice 已通过；4D-03N Renata colored activated score focused slice 已通过；4D-03O Crimson Rose ready-unit focused slice 已通过；4D-03P Fluft Poro Warhawk token focused slice 已验收；4D-03Q Shadow swift stun focused slice 已验收；4D-03R Rage Sigil typed resource focused slice 已验收；4D-03S SFD Sigil typed resource family focused slice 已验收；4D-03T OGN Sigil typed resource family focused slice 已验收；项目仍 **NOT READY**。formal 18-step 已通过，见第 46 节和 `docs/CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md`；本文历史章节中“最终 / formal 18 步 E2E 未关闭”之类旧句均被该证据 supersede。4D-03M 审计与证据见 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_EVIDENCE.md`；focused 164/164、adjacent 335/335、backend full 3893/3893、`git diff --check` 通过。4D-03N 审计与证据见 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_EVIDENCE.md`；focused 185/185、adjacent 369/369、backend full 3914/3914、`git diff --check` 通过。4D-03O 审计与证据见 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_EVIDENCE.md`；focused 169/169、adjacent 396/396、backend full 3940/3940、`git diff --check` 通过。4D-03P 审计与证据见 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_EVIDENCE.md`；focused 189/189、adjacent 685/685、backend full 3962/3962、`git diff --check` 通过。4D-03Q 审计与证据见 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_EVIDENCE.md`；focused 239/239、adjacent 779/779、backend full 4003/4003、`git diff --check` 通过。4D-03T 审计与证据见 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_EVIDENCE.md`；focused 238/238、adjacent 486/486、backend full 4068/4068、`git diff --check` 通过。4D-03Q 交接与实现前基线见 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_BASELINE_EVIDENCE.md`，已被 focused slice 验收 supersede，仍保留为回归护栏。4D-03P 交接与实现前基线见 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_BASELINE_EVIDENCE.md`，已被 focused slice 验收 supersede，仍保留为回归护栏。4D-03O 交接与实现前基线见 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_BASELINE_EVIDENCE.md`，已被 focused slice 验收 supersede，仍保留为回归护栏。4D-03N 交接与实现前基线见 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_BASELINE_EVIDENCE.md`，已被 focused slice 验收 supersede，仍保留为回归护栏。当前仍 **NOT READY**，阻断集中在 P0-005 full PaymentEngine breadth、P1 LayerEngine / 关键词 / 全卡 full-official 证据与最终 audit；P0-002 / P0-003 / P0-004 已进一步收窄但未 full-official 关闭。Stage 4D 收口执行顺序见 `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`。

2026-05-14 最新补充：4D-03W Renata Gold bonus focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_EVIDENCE.md`；focused 320/320、adjacent 965/965、backend full 4120/4120、`git diff --check` 通过。该切片只证明带 `RENATA_GOLD_EXTRA_1_MANA` marker 的 Gold token resource activation +1 mana representative，不关闭 equipment-token full rules、完整 `[A]` / `[C]` resource skill family、P0-005 或 READY；项目仍 **NOT READY**。

2026-05-14 4D-03W handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `RENATA_GOLD_EXTRA_1_MANA` marker 对 Gold token resource skill 的 +1 mana bonus。交接规格见 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_BASELINE_EVIDENCE.md`；focused baseline 313/313、adjacent baseline 958/958 通过。该基线已被 focused slice 验收 supersede，仍保留为回归护栏。

2026-05-14 补充：4D-03V Gold token resource skill focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_EVIDENCE.md`；focused 288/288、adjacent 782/782、backend full 4113/4113、`git diff --check` 通过。该切片只证明 `UNL·T05` 与 `SFD·T03` 金币 token reaction resource ability representative，当时不实现 Renata Gold extra mana bonus；该 bonus 已由 4D-03W supersede。

2026-05-14 4D-03V handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `UNL·T05` 与 `SFD·T03` 金币 equipment token 的 reaction-speed destroy + exhaust + gain generic temporary payment-only rune resource ability。交接规格见 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_BASELINE_EVIDENCE.md`；focused baseline 264/264、adjacent baseline 758/758 通过。该基线已被 focused slice 验收 supersede，仍保留为回归护栏。

2026-05-14 最新补充：4D-03U resource conversion equipment focused slice 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_EVIDENCE.md`；focused 230/230、adjacent 485/485、backend full 4089/4089、`git diff --check` 通过。项目仍 **NOT READY**。

2026-05-14 4D-03R handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `SFD·222/221` 暴怒之印 `{{横置}}：{{反应}}—{{获得}}{{红色}}，用以支付符能费用` typed payment-only resource skill representative。交接规格见 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_BASELINE_EVIDENCE.md`；focused baseline 173/173、adjacent baseline 421/421 通过。该基线已被下方 focused slice 验收 supersede，仍保留为回归护栏；项目仍 **NOT READY**。

2026-05-14 4D-03R focused slice 已验收：`SFD·222/221` 暴怒之印 typed red payment-only resource skill 已接入服务端 prompt / command / temporary payment ledger / audit representative。审计与证据见 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_EVIDENCE.md`；focused 191/191、adjacent 439/439、backend full 4021/4021、`git diff --check` 通过。该切片只收窄 P0-005，不关闭 full PaymentEngine、完整 Sigil family、完整 `[A]` / `[C]` resource skill family 或 READY；项目仍 **NOT READY**。

2026-05-14 4D-03S handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定剩余五张 SFD Sigil typed payment-only resource skills：`SFD·226/221` 专注之印 green、`SFD·229/221` 洞察之印 blue、`SFD·231/221` 力量之印 orange、`SFD·234/221` 不和之印 purple、`SFD·238/221` 团结之印 yellow。交接规格见 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_BASELINE_EVIDENCE.md`；focused baseline 191/191、adjacent baseline 439/439 通过。本切片明确不实现 OGN Sigil resource skills，不关闭完整 Sigil family、完整 `[A]` / `[C]` resource skill family、P0-005 或 READY；项目仍 **NOT READY**。

2026-05-14 4D-03S focused slice 已验收：剩余五张 SFD Sigil typed payment-only resource skills 已接入服务端 prompt / command / temporary payment ledger / payment / audit representative。审计与证据见 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_EVIDENCE.md`；focused 213/213、adjacent 461/461、backend full 4043/4043、`git diff --check` 通过。该切片只收窄 P0-005，不实现 OGN Sigil，不关闭 full PaymentEngine、完整 Sigil family、完整 `[A]` / `[C]` resource skill family 或 READY；项目仍 **NOT READY**。

2026-05-14 4D-03T handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 OGN 六张 Sigil typed payment-only resource skills。交接规格见 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_BASELINE_EVIDENCE.md`；focused baseline 213/213、adjacent baseline 461/461 通过。该切片只补 OGN Sigil family，不关闭完整 `[A]` / `[C]` resource skill family、P0-005 或 READY；项目仍 **NOT READY**。

2026-05-14 4D-03T focused slice 已验收：OGN 六张 Sigil typed payment-only resource skills 已接入服务端 prompt / command / temporary payment ledger / payment / audit representative。审计与证据见 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_EVIDENCE.md`；focused 238/238、adjacent 486/486、backend full 4068/4068、`git diff --check` 通过。该切片只收口 SFD + OGN Sigil family representative，不关闭完整 `[A]` / `[C]` resource skill family、P0-005 或 READY；项目仍 **NOT READY**。

2026-05-14 4D-03U handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `OGN·098/298` 能量通道、`SFD·117/221` 远古簇碑、`SFD·083/221` 海克斯异常体三张 resource conversion equipment skills。交接规格见 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_BASELINE_EVIDENCE.md`；focused baseline 209/209、adjacent baseline 464/464 通过。该切片只处理三张 conversion equipment，不关闭完整 `[A]` / `[C]` resource skill family、P0-005 或 READY；项目仍 **NOT READY**。

2026-05-14 4D-03U focused slice 已验收：`OGN·098/298` 能量通道、`SFD·117/221` 远古簇碑、`SFD·083/221` 海克斯异常体三张 resource conversion equipment skills 已接入服务端 prompt / command / rune pool / temporary ledger / audit representative。审计与证据见 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_EVIDENCE.md`；focused 230/230、adjacent 485/485、backend full 4089/4089、`git diff --check` 通过。该切片只证明三张 conversion equipment representative，不关闭完整 `[A]` / `[C]` resource skill family、P0-005 或 READY；项目仍 **NOT READY**。

2026-05-13 4D-03G 已验收：battlefield held score `RECYCLE_RUNE:*` payment resource action focused slice 通过 A 侧复核，focused 22/22、adjacent 224/224、backend full 3809/3809、`git diff --check` 通过。本切片不关闭 P0-005。

2026-05-13 4D-03H handoff / baseline retained：窄切片锁定 `SFD·180/221` / `SFD·180a/221` 菲奥娜“友方单位变为强力后可支付黄色使其活跃”的 trigger payment resource action。A 已确认现有 `ResolveTriggerPayCost` 仍为 mana-only 特化，普通 pending `PAY_COST` 已有 resource-action foundation；实现前 focused baseline 55/55、adjacent baseline 233/233 通过。该基线已被下方 4D-03H focused slice 验收 supersede，仍保留为回归护栏。

2026-05-13 4D-03H focused slice 已验收：SFD Fiora trigger payment resource action 已接入 `TRIGGER_PAYMENT` / `PAY_COST`，支持真实 power transition 开窗、黄色支付、必要 `RECYCLE_RUNE:*` payment resource action、decline 与 stale no-mutation。A 已验证 focused 69/69、adjacent 242/242、backend full 3818/3818、`git diff --check` 通过；审计与证据见 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_EVIDENCE.md`。本切片仍只收窄 P0-005，不关闭 full official PaymentEngine，项目仍 **NOT READY**。

2026-05-14 4D-03I focused slice 已验收：`OGN·113/298` 玛尔扎哈 `[A A]` resource skill 已接入 open-main representative path。服务端 `ACTIVATE_ABILITY` prompt 暴露 Malzahar source 与合法友方单位/装备成本对象；命令侧横置来源、摧毁成本对象到 owner graveyard、获得 2 点带 payment-only / restriction metadata 的通用符能，并保持立即结算、无普通 stack item。审计与证据见 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_EVIDENCE.md`；focused 105/105、adjacent 317/317、backend full 3840/3840、`git diff --check` 通过。该切片只收窄 P0-005 的 resource skill representative breadth，不关闭完整 `[A]` / `[C]`、spell-duel / swift timing、reaction prohibition 或 full PaymentEngine，项目仍 **NOT READY**。

2026-05-14 4D-03J handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 Malzahar resource skill lifecycle，目标是把 4D-03I 留下的 spell-duel / swift timing、reaction prohibition 与 payment-only lifecycle 残余转给 B 服务端实现。交接规格见 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_BASELINE_EVIDENCE.md`；focused baseline 109/109、adjacent baseline 336/336 通过。该基线已被下方 4D-03J focused slice 验收 supersede，仍保留为回归护栏。

2026-05-14 4D-03J focused slice 已验收：Malzahar resource skill lifecycle representative 已实现。服务端 spell-duel focus prompt 在 `SPELL_DUEL_OPEN` 且 `FocusPlayerId` 为当前玩家时公开 Malzahar `ACTIVATE_ABILITY`；命令侧接受 open-main 与 spell-duel focus 两类 timing，成功时横置来源、摧毁友方单位/装备成本对象、创建 temporary payment-only resource ledger、保持立即结算且不创建普通 stack item。pending `PAY_COST` 可用 `TEMP_PAYMENT_RESOURCE:*` 消费该 ledger 支付通用符能费用，mana-only / typed shortfall / unnecessary use rejected，支付关闭或回合资源清理时清除 ledger。审计与证据见 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_EVIDENCE.md`；focused 116/116、adjacent 340/340、backend full 3847/3847、`git diff --check` 通过。该切片只收窄 P0-005 resource skill lifecycle representative，不关闭完整 `[A]` / `[C]`、inline payment-window temporary resource consumption、reaction/counter full target-filter model 或 full PaymentEngine，项目仍 **NOT READY**。

2026-05-14 4D-03K handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 Malzahar temporary payment-only resource 的 inline payment consumption。当前 4D-03J ledger 已可在 pending `PAY_COST` 中通过 `TEMP_PAYMENT_RESOURCE:*` 支付通用符能，但 `PLAY_CARD`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT` inline prompt / command 仍只识别 `RECYCLE_RUNE:*` payment resource action。交接规格见 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_BASELINE_EVIDENCE.md`；focused baseline 331/331、adjacent baseline 526/526 通过。该基线已被下方 4D-03K focused slice 验收 supersede，仍保留为实现前回归护栏；项目仍 **NOT READY**。

2026-05-14 4D-03K focused slice 已验收：Malzahar temporary payment-only resource 已扩展到 `PLAY_CARD` / `ACTIVATE_ABILITY` / `ASSEMBLE_EQUIPMENT` representative inline payment windows。服务端 prompt 在可补 generic rune power shortfall 时公开 `TEMP_PAYMENT_RESOURCE:*`，命令侧统一解析 `RECYCLE_RUNE:*` 与 temporary resource action，commit 在同一 transaction 内应用、扣费、清理 ledger，并在 `COST_PAID` 中分离 `optionalCosts` 与 `paymentResourceActions`。审计与证据见 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_EVIDENCE.md`；focused 344/344、adjacent 539/539、backend full 3860/3860、`git diff --check` 通过。该切片只收窄 P0-005 temporary resource inline representative，不关闭完整 PaymentEngine、完整 `[A]` / `[C]` resource skill family、reaction/counter full target-filter model 或 full READY audit，项目仍 **NOT READY**。

2026-05-14 4D-03L handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `UNL-093/219` 龙魂贤者 `{{反应>}} {{横置}}：{{获得}}{{1}}` reaction-speed resource skill representative。交接规格见 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_BASELINE_EVIDENCE.md`；focused baseline 126/126、adjacent baseline 374/374 通过。该基线已被下方 4D-03L focused slice 验收 supersede，仍保留为实现前回归护栏；项目仍 **NOT READY**。

2026-05-14 4D-03L focused slice 已验收：`UNL-093/219` 龙魂贤者 reaction-speed resource skill 已接入服务端 prompt / command / audit representative。服务端只在 stack-priority reaction window 向当前 priority player 暴露 `ACTIVATE_ABILITY`，命令成功时横置 source、获得 1 点 normal rune-pool mana、不创建普通 stack item，并保留 no-mutation guards。审计与证据见 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_EVIDENCE.md`；focused 140/140、adjacent 388/388、backend full 3874/3874、`git diff --check` 通过。该切片只收窄 P0-005 reaction resource skill representative，不关闭完整 activated ability / resource skill family、full reaction/counter target model 或 full READY audit，项目仍 **NOT READY**。

2026-05-14 4D-03M handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `SFD·088/221` / `SFD·088a/221` 烈娜塔·戈拉斯克 `支付{{1}}和{{蓝色}}：抽一张牌` colored-cost ordinary activated draw representative。交接规格见 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_BASELINE_EVIDENCE.md`；focused baseline 144/144、adjacent baseline 316/316 通过。该基线只建立 B 侧实现交接与回归护栏，不代表功能完成，不关闭 P0-005；项目仍 **NOT READY**。

2026-05-14 4D-03M focused slice 已验收：`SFD·088/221` / `SFD·088a/221` 烈娜塔·戈拉斯克 colored activated draw 已接入服务端 prompt / command / audit representative。服务端 open-main prompt 暴露 `powerCostByTrait.blue=1`、无目标、非横置、普通 stack item before draw 与 typed blue payment resource metadata；命令侧使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` 支付 1 mana + 1 blue typed power，支持必要 blue rune recycle，拒绝 temporary resource / wrong trait / duplicate / invalid / unnecessary recycle、wrong timing/source/payload 并保持 no-mutation；stack pass-pass 后抽 1 张且 source 不横置、不移动、不加分。审计与证据见 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_EVIDENCE.md`；focused 164/164、adjacent 335/335、backend full 3893/3893、`git diff --check` 通过。该切片只收窄 P0-005 colored activated ability representative，不关闭 Renata score、target-bearing abilities、完整 resource skill family 或 full PaymentEngine；项目仍 **NOT READY**。

2026-05-14 4D-03N handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `SFD·088/221` / `SFD·088a/221` 烈娜塔·戈拉斯克 `支付{{4}}和{{蓝色}}{{蓝色}}{{蓝色}}{{蓝色}}，{{横置}}：获得1分` colored activated score representative。交接规格见 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_BASELINE_EVIDENCE.md`；focused baseline 163/163、adjacent baseline 335/335 通过。该基线只建立 B 侧实现交接与回归护栏，不代表功能完成，不关闭 P0-005；项目仍 **NOT READY**。

2026-05-14 4D-03N focused slice 已验收：`SFD·088/221` / `SFD·088a/221` 烈娜塔·戈拉斯克 colored activated score 已接入服务端 prompt / command / stack resolution / audit representative。服务端 open-main prompt 暴露 `powerCostByTrait.blue=4`、无目标、battlefield ready source、`exhaustsSource=true`、普通 stack item before score 与 typed blue payment resource metadata；命令侧使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` 支付 4 mana + 4 blue typed power，支持必要 blue rune recycle，拒绝 temporary resource / wrong trait / duplicate / invalid / unnecessary recycle、wrong timing/source/payload 并保持 no-mutation；stack pass-pass 后获得 1 分并沿用既有 winning-score 语义，source 保持 battlefield / exhausted。审计与证据见 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_EVIDENCE.md`；focused 185/185、adjacent 369/369、backend full 3914/3914、`git diff --check` 通过。该切片只收窄 P0-005 colored activated ability representative，不关闭 target-bearing abilities、完整 resource skill family 或 full PaymentEngine；项目仍 **NOT READY**。

2026-05-14 4D-03O handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `UNL-109/219` 猩红玫瑰 `消耗3经验，{{横置}}：让一名单位变为活跃状态` target-bearing equipment activated ready-unit representative。交接规格见 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_BASELINE_EVIDENCE.md`；focused baseline 143/143、adjacent baseline 370/370 通过。该基线只建立 B 侧实现交接和回归护栏，不代表功能完成，不关闭 P0-005；项目仍 **NOT READY**。

2026-05-14 4D-03O focused slice 已验收：`UNL-109/219` 猩红玫瑰 target-bearing equipment activated ready-unit representative 已接入服务端 prompt / command / stack resolution / audit。服务端 open-main prompt 暴露 controlled face-up ready base equipment source、`experienceCost=3`、target choices、`exhaustsSource=true`、enemy Spellshield target tax 与 ordinary stack item before ready metadata；命令侧使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` 支付 3 experience 和必要 target-tax mana，拒绝 invalid source/target、wrong timing、unsupported optional costs、`RECYCLE_RUNE:*`、`TEMP_PAYMENT_RESOURCE:*`、experience/tax 不足并保持 no-mutation；stack pass-pass 后目标单位变为活跃，source 保持 controller base / exhausted。审计与证据见 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_EVIDENCE.md`；focused 169/169、adjacent 396/396、backend full 3940/3940、`git diff --check` 通过。该切片只收窄 P0-005 target-bearing equipment activated representative，不关闭 Crimson Rose 第一行触发、Fluft、完整 resource skill family 或 full PaymentEngine；项目仍 **NOT READY**。

2026-05-14 4D-03P focused slice 已验收：`UNL-160/219` 绵绵魄罗 battlefield-only no-target activated Warhawk token representative 已接入服务端 prompt / command / stack resolution / audit。服务端 open-main prompt 暴露 controlled face-up ready battlefield source、`requiresBattlefieldSource=true`、`exhaustsSource=true`、zero-cost payment metadata、no-target/no-resource choices 与 Warhawk token metadata；命令侧使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` 记录零成本 payment audit，横置 source，拒绝 wrong timing、spell-duel、invalid payload/source、`RECYCLE_RUNE:*`、`TEMP_PAYMENT_RESOURCE:*` 并保持 no-mutation；stack pass-pass 后创建两名 `UNL·T02` 1 power Spellshield Warhawk token 到 controller base。审计与证据见 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_EVIDENCE.md`；focused 189/189、adjacent 685/685、backend full 3962/3962、`git diff --check` 通过。该切片只收窄 P0-005 battlefield-only token activated representative，不关闭完整 resource skill family、token-play breadth 或 full PaymentEngine；项目仍 **NOT READY**。

2026-05-14 4D-03Q handoff / baseline 已建立：下一 PaymentEngine breadth 切片锁定 `UNL-194/219` 黑影 `{{迅捷>}} 支付{{1}}和{{A}}，{{横置}}：{{眩晕}}一名进攻此处的敌方单位` representative。交接规格见 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_BASELINE_EVIDENCE.md`；focused baseline 198/198、adjacent baseline 738/738 通过。该基线只建立 B 侧实现交接与回归护栏，不代表功能完成，不关闭 P0-004 或 P0-005；项目仍 **NOT READY**。

2026-05-14 4D-03Q focused slice 已验收：`UNL-194/219` 黑影 swift stun representative 已接入服务端 prompt / command / stack resolution / audit。服务端只在 focused battle-response priority representative window 暴露 controlled face-up ready battlefield source、同一战场正在进攻的敌方单位目标、1 mana + 1 generic power cost、Spellshield target tax、source exhaust 与 ordinary stack-before-stun metadata；命令侧使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` 支付费用并横置 source，拒绝 wrong timing、wrong priority、invalid source/target、insufficient cost、invalid/unnecessary payment resources 并保持 no-mutation；stack pass-pass 后重新验证 target，合法则应用 `STUNNED`，stale target no-effect。审计与证据见 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_EVIDENCE.md`；focused 239/239、adjacent 779/779、backend full 4003/4003、`git diff --check` 通过。该切片只收窄 P0-005 target-bearing swift activated representative，不关闭 P0-004 battle lifecycle、完整 swift/reaction family、完整 resource skill family 或 full PaymentEngine；项目仍 **NOT READY**。

## 0. A 主控职责边界

A 是主控规划 / 架构 / 验收 agent，不是默认功能实现 agent。A 的职责是：

- 管理 B/C/D/E 等子 agent 的任务分发、写入范围、验收标准和合并顺序。
- 维护本 checkpoint、当前审计文档、任务拆分、阻断清单和恢复入口。
- 审查子 agent 产出的 diff、测试结果和文档结论，决定是否进入下一轮。
- 遇到前端或服务端实现任务时，优先拆给对应子 agent；A 只做必要的只读审计、命令验证和小型文档维护。
- A 不得默认亲自继续实现功能代码；只有用户明确授权“A 可以亲自改这块”时，才允许修改功能实现。
- A 不得把项目标记为 READY 或完成 goal，除非按 active goal 完成审计证明所有服务端 P0/P1、前端 E2E、1009 张卡效果和文档验收均已收口。

如果 active goal 文本没有显式写出“A 主控职责”，以后恢复时仍以本节为准。

### 0.1 常驻子 agent 复用策略

A 不应为每个小问题反复创建全新子 agent。当前阶段采用“常驻分工池”：

- B：服务端规则 / 协议 / 测试实现与审查。
- C：前端契约 / Web UI / Chrome smoke。
- D：文档、规则证据、P0/P1 审计。
- E：卡牌效果覆盖、官方文本与 FAQ 证据矩阵。

复用原则：

- 同一阶段优先用 `send_input` 继续给已有子 agent 派任务，保留其已读上下文。
- 不主动关闭仍有后续价值的子 agent；只在阶段收口、职责变更或上下文明显污染时关闭。
- 如果窗口中断，先查看本 checkpoint 中记录的 agent id；可恢复时继续向同一 agent 发任务。
- 若底层环境无法恢复某个 agent，则用本 checkpoint、两份当前阶段文档和该 agent 最近总结作为最小 warm start，不再让它无目的重读全项目。

当前常驻审查池：

- B-Review / Maxwell：`019e1068-5757-7bd1-8129-d401c60e0b7f`
- B-Implementation / Raman：`019e2257-8d40-7630-9201-28df44dd689a`（2026-05-14 新建用于 4D-03J 后续 PaymentEngine focused slices；4D-03N 初稿已由 A 验收，当前不持有服务端写入锁）
- C-Review / Copernicus：`019e0bbc-df6f-7151-baf5-f79ff466c5a9`
- D-Review / Nash：`019e1068-6042-7dc3-a45c-655838d02b92`
- E-Review / Poincare：`019e1068-6975-7242-9143-1c50d7ce23fa`

旧池记录：Bohr `019e0bbc-d5d3-75a2-bde2-13e99da8ed76`、Pasteur `019e0bbc-ece9-7fe1-a2ea-8e2afee1f5a2`、Dewey `019e0bdf-e12c-71c3-a394-cbfa3b7942a1` 在 4C-22 派单时已无法通信，工具返回 `agent not found`；不要继续依赖这些旧 id。

长期子 agent 规则：

- B/C/D/E 是长期可复用工作池，阶段内不要因为一次超时就 `close_agent`。
- 超时处理顺序：先 `send_input` 状态询问；再等待；必要时由 A 收回对应写入锁并标记该 agent 暂停，不关闭线程。
- 只有用户明确要求清理、阶段收口且确认不再复用、或 agent 上下文明显污染/不可通信时，才允许关闭或重建。
- 如果必须重建常驻池，必须立即更新本节 agent id，并说明旧 id 失效原因。

2026-05-12 复核：A 已向当前 B/C/D/E 长期代理同步“驻场复用、未授权不写入、不因单次超时清理”的规则；Maxwell、Copernicus、Nash、Poincare 均已确认继续保留上下文。用户已明确“在当前 goal 完成前不需要再申请授权”，4C-59 已完成 Zenith Blade enemy battlefield stun target guard 代表切片，A 负责复核、验证、文档和 checkpoint 收口。

## 0.1.0 阶段 4D-01 Board Task Queue Foundation Checkpoint

状态：**foundation accepted；项目仍 NOT READY。**

本批事实：

- 实现证据提交：`6a3ee038 test: add stage 4D board task queue foundation coverage`
- 新增测试文件：`tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`
- 审计入口：`docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_AUDIT.md`
- 证据入口：`docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_EVIDENCE.md`

覆盖点：

- base-to-battlefield empty / occupied contest queue。
- battlefield-to-base contest removal。
- cleanup-first blocking、ordinary command no-mutation、cleanup repeat until stable。
- illegal standby / unattached equipment redaction。
- `PASS_FOCUS` spell duel close -> matching `START_BATTLE` promotion。
- precise battlefield roam mixed-case object id preservation and destination-only contest tasks。
- reconnect snapshot / prompt pending task phase, active task and opponent hidden-info redaction。

已跑验证：

- Focused：`FullyQualifiedName~BoardTaskQueueFoundation|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~ReconnectWithPendingTaskQueue` 通过 31/31。
- Adjacent：`FullyQualifiedName~BattlefieldContest|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~MoveUnit|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle|FullyQualifiedName~Reconnect` 通过 149/149。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3780/3780。
- `git diff --check` 与新增文件 whitespace check 均无输出。

口径：

- 4D-01 handoff checklist 已满足，下一步可进入 4D-02 spell duel and battle state machine。
- 本批不修改引擎源码、不修改前端、不修改卡牌矩阵。
- P0-002 / P0-003 已收窄为后续 full-official lifecycle 残余，不因本批宣称 READY。
- P0-004、P0-005、P1 LayerEngine / keywords / full-card evidence 仍未关闭。

4C-23 agent 事件：

- A 复用 B/D/E 长期代理做 Lux / Aphelios 候选审查。
- B 超时未落盘时，A 已按长期子 agent 规则先询问状态、等待、再收回本批服务端写入锁；未关闭 B。
- D/E 输出只读草案；A 执行小范围测试修正与 docs/matrix 收口。

## 0.1.0.1 阶段 4D-02 Spell Duel / Battle Checkpoint

状态：**focused slice accepted；项目仍 NOT READY。**

本批事实：

- Handoff 入口：`docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_HANDOFF.md`
- Baseline 入口：`docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_BASELINE_EVIDENCE.md`
- 审计入口：`docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_AUDIT.md`
- 证据入口：`docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_EVIDENCE.md`
- 新增测试文件：`tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`
- Focused new tests：6/6 通过。
- Focused handoff regression：35/35 通过。
- Adjacent regression：127/127 通过。
- Backend full：3786/3786 通过。

当前代码面：

- `MatchSession` 新增 active spell-duel battlefield 推导，多争夺战场时只把 deterministic 第一个未完成战场标为 active，并只给该 task 挂 focus / stack metadata。
- `CoreRuleEngine.ResolvePassFocus` 在 spell duel close 后 cleanup；若 matching `START_BATTLE` 消失，则继续推进下一 pending battlefield task。
- 新增 focused tests 覆盖多争夺战场、wrong-focus no-mutation、spell-duel stack task binding、cleanup 后推进下一 task、`SPELL_DUEL_TASKS` / `BATTLE_TASKS` reconnect metadata 与 hidden redaction。
- P0-004 仍未 full-official 关闭：完整 battle response window、initial stack、swift/reaction 链矩阵、no-result / replacement / prevention / cleanup 全组合仍待实现。

下一步：

- 下一实现切片进入 4D-03 Payment Engine Unification。
- A 不亲自实现功能代码，只做任务派发、复核、验证和文档验收。

## 0.1.0.2 阶段 4D-03 PaymentEngine Checkpoint

状态：**focused foundation accepted；4D-03B focused slice accepted；4D-03C focused slice accepted；4D-03D focused slice accepted；4D-03E focused slice accepted；4D-03F focused slice accepted；4D-03G focused slice accepted；项目仍 NOT READY。**

本批事实：

- Handoff 入口：`docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_HANDOFF.md`
- Baseline 入口：`docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_BASELINE_EVIDENCE.md`
- 审计入口：`docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_AUDIT.md`
- 证据入口：`docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_EVIDENCE.md`
- 新增测试文件：`tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- Focused regression：56/56 通过。
- Adjacent regression：245/245 通过。
- Backend full：3791/3791 通过。

当前代码面：

- `PaymentCostRules` 新增 `PaymentPlan`、`AuthorizePayment`、`TryCommitPayment` 与 plan-driven `COST_PAID` payload helper。
- `ResolvePlayCard`、代表性 `TRIGGER_PAYMENT` / `PAY_COST` 和 `ASSEMBLE_EQUIPMENT` 已接入 shared payment plan / commit foundation。
- `RECYCLE_RUNE:*` payment resource action 的 post-resource insufficient typed-cost rollback 已由 hash / zone / runePool / stack no-mutation 测试覆盖。
- 4D-03 只接受 foundation，不关闭 P0-005 full official；`ACTIVATE_ABILITY`、`LEGEND_ACT`、battlefield held score、全 `[A]` / `[C]`、Haste / Echo / Spellshield、replacement / optional / extra cost 与 full prompt quote parity 仍待后续切片。

4D-03B facts：

- Handoff 入口：`docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_HANDOFF.md`
- Baseline 入口：`docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_BASELINE_EVIDENCE.md`
- 审计入口：`docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md`
- 证据入口：`docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_EVIDENCE.md`
- 修改实现：`src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- Focused regression：18/18 通过。
- Adjacent regression：318/318 通过。
- Backend full：3791/3791 通过。
- `git diff --check` 无输出。

4D-03B 当前代码面：

- Vi `ACTIVATE_ABILITY`、Xerath `ACTIVATE_ABILITY` + Spellshield tax、`LEGEND_ACT` mana / experience payment、battlefield held pay-4-power score 均接入 shared `PaymentPlan` / `TryCommitPayment`。
- `COST_PAID` payload 保留兼容键，同时新增 `paymentId`、`paymentWindow`、source / ability / reason、total cost、remaining pool / experience metadata。
- 本批不修改前端、卡牌矩阵、`MatchSession.cs`、`PaymentCostRules.cs` 或未跟踪的 `riftbound-dotnet.sln`。
- 4D-03B 仍不关闭 P0-005 full official；完整 `[A]` / `[C]`、Haste / Echo / Spellshield 全窗口、替代 / 额外 / 可选费用、费用目标选择与全路径 prompt quote parity 仍待后续切片。

4D-03C facts：

- Handoff 入口：`docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_HANDOFF.md`
- Baseline 入口：`docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_BASELINE_EVIDENCE.md`
- 审计入口：`docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md`
- 证据入口：`docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_EVIDENCE.md`
- 修改实现：`src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：`tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- Focused regression：31/31 通过。
- Adjacent regression：363/363 通过。
- Backend full：3791/3791 通过。
- `git diff --check` 无输出。

4D-03C 当前代码面：

- `TryBuildPlayCardPlan` 的 representative affordability preflight 通过 `PaymentPlan` / `AuthorizePayment` 统一检查 mana / generic power / typed power / experience。
- `ResolvePlayCard` 的 final commit 仍在 `RECYCLE_RUNE:*` payment resource action 落入临时资源池后调用 `TryCommitPayment`，保留既有 rollback 语义。
- `COST_PAID` payload 保留兼容键，同时新增/强化 base/total mana、generic/typed/total power、experience、optional costs、payment resource actions、Spellshield tax、cost reductions 和 remaining pool / experience metadata。
- 本批不修改前端、卡牌矩阵、`PaymentCostRules.cs`、`MatchSession.cs` 或未跟踪的 `riftbound-dotnet.sln`。
- 4D-03C 仍不关闭 P0-005 full official；完整 `[A]` / `[C]`、全 Haste / Echo / Spellshield 窗口、替代 / 额外 / 可选费用全矩阵与全路径 prompt quote parity 仍待后续切片。

4D-03D facts：

- Handoff 入口：`docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_HANDOFF.md`
- Baseline 入口：`docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_BASELINE_EVIDENCE.md`
- 审计入口：`docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md`
- 证据入口：`docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_EVIDENCE.md`
- 修改实现：`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`
- 更新测试：`tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- Focused regression：84/84 通过。
- Adjacent regression：257/257 通过。
- Backend full：3796/3796 通过。
- `git diff --check` 无输出。

4D-03D 当前代码面：

- Vi / Xerath `ACTIVATE_ABILITY` prompt 在 power 不足但基地有可回收基础符文时暴露 `RECYCLE_RUNE:*` payment resource choice 与 contribution metadata。
- Vi / Xerath command path 可在同一 `ACTIVATE_ABILITY` optional costs 中回收符文补足 power，并用同一 `paymentId` 记录 `RUNE_RECYCLED` / `POWER_GAINED` / `COST_PAID`。
- Xerath Spellshield tax mana 不足、无效符文、不必要资源动作均 rejected 且 no-mutation。
- 本批不修改前端、卡牌矩阵、`PaymentCostRules.cs` 或未跟踪的 `riftbound-dotnet.sln`。
- 4D-03D 仍不关闭 P0-005 full official；完整 `[A]` / `[C]`、`PAY_COST` trigger payment resource action、`LEGEND_ACT` / battlefield held score resource action、替代 / 额外 / 可选费用全矩阵与全路径 prompt quote parity 仍待后续切片。

4D-03E facts：

- Handoff 入口：`docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_HANDOFF.md`
- Baseline 入口：`docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_BASELINE_EVIDENCE.md`
- 审计入口：`docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_AUDIT.md`
- 证据入口：`docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_EVIDENCE.md`
- 修改实现：`src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：`tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- Focused regression：88/88 通过。
- Adjacent regression：290/290 通过。
- Backend full：3800/3800 通过。
- `git diff --check` 无输出。

4D-03E 当前代码面：

- `ResolveHideCard` 的标准 `STANDBY_A`、Teemo `STANDBY_TEEMO_MANA` 与 Guerrilla Warfare `STANDBY_FREE` 均通过 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` 支付。
- `COST_PAID` payload 保留 `mana`、`power`、`optionalCosts`、`standbyHideCostWaived`、`teemoStandbyHideReplacement`，并补齐 `paymentWindow = HIDE_CARD`、`sourceObjectId`、`reason = STANDBY_HIDE`、base / total cost 与 remaining pool metadata。
- Bandle Tree 额外待命目的地、Ember Monk 待命触发、hidden-info payload、object location reconciliation、错误 no-mutation 与现有 command shape 保持兼容。
- 本批不修改前端、卡牌矩阵、`MatchSession.cs`、`PaymentCostRules.cs` 或未跟踪的 `riftbound-dotnet.sln`，不关闭 P0-005 full official。

4D-03F facts：

- Handoff 入口：`docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_HANDOFF.md`
- Baseline 入口：`docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_BASELINE_EVIDENCE.md`
- 审计入口：`docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md`
- 证据入口：`docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_EVIDENCE.md`
- 修改实现：`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`
- 更新测试：`tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- Focused regression：55/55 通过。
- Adjacent regression：233/233 通过。
- Backend full：3804/3804 通过。
- `git diff --check` 无输出。

4D-03F 当前代码面：

- `PendingPaymentState` 记录 `PaymentResourceActionIds`，snapshot / prompt metadata 暴露 pending `PAY_COST` 的 resource choices、per-choice power contribution 和 available-power metadata。
- 普通 pending `PAY_COST` command path 可以先事务化应用合法 `RECYCLE_RUNE:*`，再用 shared `PaymentPlan` / `TryCommitPayment` 支付 typed power。
- `COST_PAID` payload 保留兼容键，并补齐 `paymentResourceActions`、`legalPaymentChoiceIds`、`recycledRuneObjectIds` 与 remaining pool metadata。
- 现有 mana-only `TRIGGER_PAYMENT` 代表路径保持兼容；本批不把 battlefield conquer / Vayne / Icevale / Jax trigger payment 改造成 power-cost trigger。
- 本批不修改前端、卡牌矩阵、`PaymentCostRules.cs` 或未跟踪的 `riftbound-dotnet.sln`，不关闭 P0-005 full official。

4D-03G facts：

- Handoff 入口：`docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_HANDOFF.md`
- Baseline 入口：`docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_BASELINE_EVIDENCE.md`
- 审计入口：`docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md`
- 证据入口：`docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_EVIDENCE.md`
- Focused baseline：21/21 通过。
- Adjacent baseline：219/219 通过。
- 修改实现：`src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- Focused regression：22/22 通过。
- Adjacent regression：224/224 通过。
- Backend full：3809/3809 通过。
- `git diff --check` 无输出。

4D-03G 当前代码面：

- `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 可在当前 power 不足但基地有可回收基础符文时，随 `DECLARE_BATTLE` 的 `COMBAT_ASSIGNMENT` 一起提交合法 `RECYCLE_RUNE:<objectId>` payment resource action。
- Core 侧会在同一 `paymentId` / `BATTLEFIELD_HELD` window 下先应用合法回收，生成 `RUNE_RECYCLED` / `POWER_GAINED`，再通过 shared `PaymentPlan` / `TryCommitPayment` 支付 4 power。
- `COST_PAID` payload 保留兼容键，并补齐 `paymentResourceActions`、`recycledRuneObjectIds` 与 remaining pool metadata。
- 不必要回收、错玩家符文、缺 `cardNo` 符文和重复资源动作 rejected 且 no-mutation。
- 本批不修改前端、卡牌矩阵、`MatchSession.cs`、`PaymentCostRules.cs` 或未跟踪的 `riftbound-dotnet.sln`，不关闭 P0-005 full official。

下一步：

- 4D-03J focused slice 已验收；后续继续 P0-005 breadth，优先处理完整 `[A]` / `[C]` family、`LEGEND_ACT` resource action、inline payment-window temporary resource consumption 与更多 payment window quote parity。
- A 继续只做验收、测试复跑、审计和文档收口；不默认亲自改功能代码。

## 0.1.1 阶段 4C-23 Lux Checkpoint

状态：**已完成代表切片收口；项目仍 NOT READY。**

阶段 4C-23 名称：Lux high-cost spell temporary power representative baseline。

本批事实：

- 目标 FU：`FU-f18a49e06d`
- 代表卡：`OGS·006/024` Lux / 拉克丝，snapshot entry id `31585`
- 规则文本：controller 打出 cost >= 5 spell 时，Lux 本回合战力 +3。
- runtime effect：`OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`
- 代表路径：`CARD_PLAYED` cost >= 5 spell -> visible face-up Lux source -> `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` compatibility events -> `POWER_MODIFIED_UNTIL_END_OF_TURN` +3。
- guard：low-cost spell、opponent spell、face-down Lux、standby Lux、source not on field 均 no trigger / no mutation。
- A 修正测试构造：`LuxOpponentHighCostSpellDoesNotTrigger` 使用 P2 手牌中的 `P2-SPELL-EVOLUTION-DAY`。

修改文件：

- `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`
- `docs/CURRENT_STAGE4C_BATCH23_LUX_HIGH_COST_SPELL_POWER_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH23_LUX_HIGH_COST_SPELL_POWER_EVIDENCE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

已跑验证：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Lux|FullyQualifiedName~HighCostSpell|FullyQualifiedName~Ravenbloom|FullyQualifiedName~RealTriggerQueue"` 通过 67/67。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3413/3413。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过。
- `git diff --check` 通过。

矩阵影响：

- `stage4CBatch23LuxHighCostSpellTriggerPower` 顶层 overlay 已新增。
- `functionalUnits[].stage4C23` 只标 `FU-f18a49e06d`。
- stage4C23 verified FUs = 1；verified snapshot entries = 1。
- cumulative real-trigger enqueue verified FUs = 16; cumulative spell-played immediate trigger-event verified FUs = 1。
- fullOfficialUpgrades = 0；fullOfficialStillUncoveredFunctionalUnits = 811。
- 4B freezeStatus/statusFlags 不改变；`fullOfficial=false`。

仍缺：

- 完整 trigger engine、complete APNAP / trigger batch、optional trigger handling。
- 完整 PaymentEngine、paid-cost override / 增减费 / 额外费用 / 替代费用矩阵。
- 完整 LayerEngine、temporary modifier timestamp / dependency / cleanup duration。
- FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

下一候选建议：

- Aphelios / `FU-67c6b0186e` 仍是 high-payoff 3-entry weapon-attachment trigger candidate，但必须另开 dedicated design batch。
- Icevale Archer / `FU-c170628e3a` 和 Vayne / `FU-c027639a3c` 保留为 triggered-cost / conquer pressure candidates。

## 0.1.2 阶段 4C-24 Vayne Checkpoint

状态：**已完成极窄代表切片收口；项目仍 NOT READY。**

阶段 4C-24 名称：Vayne conquer recall representative baseline。

本批事实：

- 目标 FU：`FU-c027639a3c`
- 代表卡：`OGN·035/298` Vayne / 薇恩
- 规则文本：每当 Vayne 征服一处战场时，可以选择支付 1 来让 Vayne 返回所属的手牌。
- 代表路径：visible face-up Vayne 征服战场 -> existing `TRIGGER_PAYMENT` / `PAY_COST` prompt -> `PAY_COST(SPEND_MANA:1)` -> Vayne returns to owner hand。
- decline 路径：`PAY_COST(DECLINE)` 关闭窗口且不回手、不变更。
- guard：hidden / face-down / standby / opponent-controlled source 均 no trigger / no leak / no mutation。

修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（B）
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`（B）
- `docs/CURRENT_STAGE4C_BATCH24_VAYNE_CONQUER_RECALL_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH24_VAYNE_CONQUER_RECALL_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

已跑验证：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Vayne|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST"` 通过 52/52。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3420/3420。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留既有 SignalR / Rollup PURE 注释 warning。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过，覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- JSON / diff hygiene：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过；`git diff --check` 通过。

本批未记录：

- Stage 3 preflight / final 18-step E2E。

仍缺：

- 完整 Assault3、active-entry、complete conquer/control-zone matrix。
- 完整 battlefield / control / conquer lifecycle、control freeze/release、held/conquer scoring order 与 battle cleanup 全矩阵。
- 完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / multi-window full matrix。
- 完整 trigger engine、complete APNAP / trigger batch、optional trigger handling、完整 effect resolution。
- FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Vayne 代表路径外推完整战斗、征服、支付、回手或隐藏信息矩阵。

## 0.1.3 阶段 4C-25 Icevale Archer Checkpoint

状态：**已完成极窄代表切片、验证与文档收口；项目仍 NOT READY。**

阶段 4C-25 名称：Icevale Archer attack payment representative baseline。

本批事实：

- 目标 FU：`FU-c170628e3a`
- 代表卡：`UNL-065/219` Icevale Archer / 冰谷弓箭手
- 规则文本：当 Icevale Archer 进攻时，可以选择支付 1，以此让此处的一名单位在本回合内 -1。
- 代表路径：active start-battle task 下 visible face-up Icevale 作为攻击者 -> `DeclareBattleCommand.BattlefieldTargetObjectIds` 预选同一 battlefield 的正面单位目标 -> 战斗声明后打开 `TRIGGER_PAYMENT` / `PAY_COST` -> `PAY_COST(SPEND_MANA:1)` -> 目标本回合 power -1。
- decline 路径：`PAY_COST(DECLINE)` 关闭窗口且不修改目标战力。
- guard：invalid target、hidden / face-down / standby / opponent-controlled source 均 no trigger / no leak / no mutation。

修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（A）
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`（A）
- `docs/CURRENT_STAGE4C_BATCH25_ICEVALE_ARCHER_ATTACK_PAYMENT_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH25_ICEVALE_ARCHER_ATTACK_PAYMENT_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

已跑验证：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Icevale|FullyQualifiedName~AttackPayment|FullyQualifiedName~TriggerPayment|FullyQualifiedName~DeclareBattle|FullyQualifiedName~Vayne|FullyQualifiedName~Lux"` 通过 102/102。
- JSON / diff hygiene：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过；`git diff --check` 通过。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3429/3429。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。

本批未记录：

- Stage 3 preflight / final 18-step E2E。

仍缺：

- full attack-trigger family、完整 target selection prompt、支付后恢复战斗时点。
- 完整 spell duel / battle lifecycle、battle response window、damage assignment 与战斗结算全矩阵。
- 完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / multi-window full matrix。
- 完整 trigger engine、complete APNAP / trigger batch、optional trigger handling、attack-trigger family 与完整 effect resolution。
- Spellshield target tax、完整 LayerEngine、temporary modifier timestamp / dependency / cleanup duration matrix。
- FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Icevale 代表路径外推完整进攻触发、目标选择、支付、战斗恢复、法盾加税、LayerEngine 或隐藏信息矩阵。

## 0.1.4 阶段 4C-26 Jax Checkpoint

状态：**已完成极窄代表切片收口；项目仍 NOT READY。**

本批候选审查事实：

- A 继续复用长期 B/C/D/E 子 agent 池；未创建临时替代 agent，未清理长期 agent。
- Aphelios / `FU-67c6b0186e` 已由 B/C/D/E 做候选审查。C/D/E 均认为可作为 representative-only batch，但 B 判定当前实现需要新的 trigger mode-choice / mode-memory 契约，不能安全塞入现有 `TRIGGER_PAYMENT` / `PAY_COST` 字段。
- A 判定 Aphelios 暂不进入 4C-26 implementation batch，降级为后续 dedicated design batch candidate。
- Jax / `FU-73f3be35df` 被选为更低耦合的 4C-26 代表切片。
- Jax snapshot entries：`SFD·119/221`、`SFD·119a/221`。
- Jax 代表路径：visible face-up Jax 获得武装贴附 -> existing `EQUIPMENT_ATTACHED` event -> existing `TRIGGER_PAYMENT` / `PAY_COST` -> 支付 1 抽 1；decline 不抽牌、不变更。
- B/D/E 均给出 Jax GO；C 判定若不新增 prompt shape，则前端无需写入锁。

写入锁与执行状态：

- A 曾授予 B / Maxwell 唯一服务端写入锁，范围仅限 `src/Riftbound.Engine/CoreRuleEngine.cs` 与一个服务端测试文件。
- B 首轮超时后产生 partial diff；A 已按长期子 agent 规则询问状态、等待、收回写入锁、复现失败，再重新授予 B 仅测试修复锁。
- B 完成 Jax 服务端实现与 `TriggerPaymentTests` 覆盖；A 未关闭 B，长期子 agent 池继续保留。
- D 只写审计 / 证据文档；E 只写 coverage / risk / freeze 文档；C 无前端写入锁。

4C-26 修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（B）
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`（B）
- `docs/CURRENT_STAGE4C_BATCH26_JAX_WEAPON_ATTACH_PAYMENT_DRAW_AUDIT.md`（D）
- `docs/CURRENT_STAGE4C_BATCH26_JAX_WEAPON_ATTACH_PAYMENT_DRAW_EVIDENCE.md`（D）
- `docs/CURRENT_SERVER_RULE_AUDIT.md`（D）
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`（D）
- `docs/rules-evidence-index.md`（D）
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`（E）
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`（E）
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`（E）
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`（E）
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`（A）

已跑验证：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~TriggerPayment"` 通过 37/37。
- Small regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~Icevale|FullyQualifiedName~Vayne|FullyQualifiedName~Lux|FullyQualifiedName~SunkenTemple|FullyQualifiedName~BattlefieldConquerGold|FullyQualifiedName~TriggerPayment"` 通过 46/46。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3439/3439。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留既有 SignalR / Rollup `PURE` 注释提示。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过。
- `git diff --check` 通过。

本批关闭的代表子项：

- `FU-73f3be35df` / `SFD·119/221`、`SFD·119a/221` visible face-up Jax weapon attach -> trigger payment -> pay 1 draw 1 代表路径。
- `DECLINE` no draw / no mutation 代表路径。
- non-Jax / non-armament no prompt、hidden / face-down / standby / opponent-controlled source no trigger leak、insufficient payment no draw 代表护栏。

当前建议：

- 继续保持 `fullOfficial=false`，不宣称 READY / READY-CANDIDATE。
- Aphelios / `FU-67c6b0186e` 仍保留为后续 dedicated design batch candidate，需要 trigger mode-choice / mode-memory 契约。
- 下一批仍应逐 FU、逐测试推进；完整 Forge / 百炼 / assemble、完整 equipment attachment、完整 optional trigger family / order triggers、完整 PaymentEngine、draw / replacement / hidden-zone matrix、FAQ regression、1009/811 full official、最终 18-step E2E 仍未完成。

## 0.1.5 阶段 4C-27 Treasure Hunter Checkpoint

状态：**已完成极窄代表切片与文档 / 矩阵收口；项目仍 NOT READY。**

阶段 4C-27 名称：Treasure Hunter move -> dormant Gold representative baseline。

本批候选审查事实：

- A/B/D 选择 Treasure Hunter / 寻宝猎人 `SFD·130/221` / `FU-6144ab0271`，而不是 Karthus / `FU-ee1dfb3ed3`。
- Treasure Hunter 规则文本：每当我移动时，打出一个休眠的“金币”装备指示物。
- 代表路径：visible face-up Treasure Hunter 经 existing authoritative move route 成功移动后，触发 `TREASURE_HUNTER_MOVE_CREATE_GOLD`，结算创建一个休眠 Gold equipment token 到 controller base。
- 已覆盖两条移动代表来源：base -> battlefield move；precise ROAM battlefield A -> battlefield B。
- guard：non-Treasure Hunter、hidden / face-down / standby / opponent-controlled source、failed move、no-op move 均 no trigger / no leak / no token。
- 本批不新增 protocol / frontend shape；前端仍只消费既有 snapshot / event / prompt，不本地裁决移动触发或金币创建。
- Karthus / `FU-ee1dfb3ed3` 仍保持 design-gated：optional extra Last Breath、multiplicity、multi-Karthus stacking、hidden / face-down / standby visibility 与 `ORDER_TRIGGERS` batch model 均未裁决。

4C-27 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH27_TREASURE_HUNTER_MOVE_GOLD_AUDIT.md`（D）
- `docs/CURRENT_STAGE4C_BATCH27_TREASURE_HUNTER_MOVE_GOLD_EVIDENCE.md`（D）
- `docs/CURRENT_SERVER_RULE_AUDIT.md`（D）
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`（D）
- `docs/rules-evidence-index.md`（D）
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`（D write lock）

已跑验证：

- Focused backend：A 记录 Treasure Hunter focused 通过 82/82。
- Small regression：A 记录 Treasure Hunter small regression 通过 121/121。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3447/3447。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留既有 SignalR PURE annotation 警告。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- Matrix / whitespace：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 与 `git diff --check` 均通过。

本批关闭的代表子项：

- `FU-6144ab0271` / `SFD·130/221` visible face-up Treasure Hunter successful move -> dormant Gold equipment token 代表路径。
- base -> battlefield 与 precise ROAM A -> B 两条移动来源的代表路径。
- non-Treasure Hunter、hidden / face-down / standby / opponent-controlled source、failed move、no-op move 的 no trigger / no leak / no token 代表护栏。

仍缺：

- 完整 movement / control-zone / roam lifecycle、全部移动来源、移动替代 / 取消 / 同步触发矩阵。
- 完整 move-trigger family、完整 trigger engine、complete APNAP / trigger batch、optional trigger handling 与完整 effect resolution。
- equipment token 全规则、token ownership / controller / zone matrix。
- hidden / face-down 原始触发建模、viewer-specific metadata 全路径、replay redaction 与显露窗口。
- Karthus extra Last Breath design gate、FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Treasure Hunter 代表路径外推完整移动、触发、金币、装备、隐藏信息、Karthus 或 full-official。

## 0.1.6 阶段 4C-28 Battle or Flight Checkpoint

状态：**已完成极窄代表切片文档收口；项目仍 NOT READY。**

阶段 4C-28 名称：Battle or Flight move-to-owner-base target guard representative baseline。

本批候选审查事实：

- A/B/D 选择 Battle or Flight / 战或逃 `OGN·168/298` / `FU-813144e7d4`，而不是 Hostile Takeover、Berserk Impulse 或 Edge of Night。
- Battle or Flight 规则文本：将一名单位从战场上移动到其所属的基地。
- 代表路径：P1 打出 Battle or Flight，选择正面战场单位目标，双方 priority pass 后结算，目标移动到 owner base，并保留 damage / power / object identity。
- guard：battlefield equipment、base unit、stale object、face-down standby object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no unit movement。
- 本批不新增 protocol / frontend shape；前端仍只消费既有 play-card / stack / movement event，不本地裁决目标合法性或移动结算。

4C-28 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH28_BATTLE_OR_FLIGHT_MOVE_TO_BASE_AUDIT.md`（D）
- `docs/CURRENT_STAGE4C_BATCH28_BATTLE_OR_FLIGHT_MOVE_TO_BASE_EVIDENCE.md`（D）
- `docs/CURRENT_SERVER_RULE_AUDIT.md`（D）
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`（D）
- `docs/rules-evidence-index.md`（D）
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`（D write lock）

已跑验证：

- Focused backend：A 记录 Battle or Flight focused 通过 61/61。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3452/3452。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留既有 SignalR PURE annotation 警告。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- Matrix / whitespace：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 与 `git diff --check` 均通过。

本批关闭的代表子项：

- `FU-813144e7d4` / `OGN·168/298` visible spell resolution move battlefield unit -> owner base 代表路径。
- 正面战场单位目标移动后保留 damage / power / object identity 的代表路径。
- battlefield equipment、base unit、stale object、face-down standby object 的 invalid target no-mutation 代表护栏。

仍缺：

- 完整 spell duel / battle lifecycle、swift / reaction timing、face-down standby play 与 priority window 全矩阵。
- 完整 movement / control-zone / roam lifecycle、owner/controller split、attached equipment、movement replacement / prevention / cleanup 交织。
- 完整 targeting prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Battle or Flight 代表路径外推完整 swift、standby reaction、targeting、movement、PaymentEngine、FEPR 或 full-official。

## 0.1.7 阶段 4C-29 Gust Checkpoint

状态：**已完成极窄代表切片文档收口；项目仍 NOT READY。**

阶段 4C-29 名称：Gust return-to-hand target guard representative baseline。

本批候选审查事实：

- A 采纳 D 的低耦合建议选择 Gust / 罡风 `OGN·169/298` / `FU-48662b7661`；B 的 4C-29 写入锁因超时无 diff 已收回但长期代理保留，C/E 的替代建议未作为本批实现目标。
- Gust 规则文本：让战场上一名不高于 3 战力的单位返回其所属的手牌。
- 代表路径：P1 打出 Gust，选择正面公共战场单位且 power <= 3 的目标，双方 priority pass 后结算，目标返回 owner hand。
- guard：power > 3、base unit、stale object、face-down standby object、battlefield equipment 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no return-to-hand mutation。
- 本批不新增 protocol / frontend shape；前端仍只消费既有 play-card / stack / return-to-hand event，不本地裁决目标合法性或回手结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍按 deferred / design-gated 候选管理，不由本批关闭。

4C-29 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH29_GUST_RETURN_TO_HAND_GUARD_AUDIT.md`（D）
- `docs/CURRENT_STAGE4C_BATCH29_GUST_RETURN_TO_HAND_GUARD_EVIDENCE.md`（D）
- `docs/CURRENT_SERVER_RULE_AUDIT.md`（D）
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`（D）
- `docs/rules-evidence-index.md`（D）
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`（D write lock）

已跑验证：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gust|FullyQualifiedName~ReturnToHand|FullyQualifiedName~Return|FullyQualifiedName~Hand"` 通过 112/112。
- Small combined regression：GustReturnToHandTests + BattleOrFlight + existing Gust rejection 通过 13/13。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3458/3458。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留既有 SignalR / Rollup PURE annotation warning。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：E 已运行通过。
- 以上不替代最终正式 18-step E2E。

本批关闭的代表子项：

- `FU-48662b7661` / `OGN·169/298` visible spell resolution return public battlefield unit with power <= 3 to owner hand 代表路径。
- public battlefield target power ceiling `<= 3` 的服务端权威 guard。
- power > 3、base unit、stale object、face-down standby object、battlefield equipment invalid-target no-mutation 代表护栏。

仍缺：

- 完整 swift / reaction timing、spell duel / battle lifecycle、priority window 与 FEPR 全矩阵。
- 完整 return-to-hand / movement / control-zone lifecycle、owner/controller split、attached equipment、replacement / prevention / cleanup 交织。
- 完整 targeting prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Gust 代表路径外推完整 swift、reaction timing、targeting、return-to-hand、movement、PaymentEngine、FEPR、named deferred candidates 或 full-official。

## 0.1.8 阶段 4C-30 Hunt the Weak Checkpoint

状态：**已完成极窄代表切片文档收口；项目仍 NOT READY。**

阶段 4C-30 名称：Hunt the Weak destroy-target guard representative baseline。

本批候选审查事实：

- A 选择 Hunt the Weak / 狩魂 `UNL-159/219` / `FU-282b6e3149` 作为 4C-30 narrow destroy-target guard slice。
- Hunt the Weak 规则文本：摧毁战场上一名不高于 3 战力的单位。
- 代表路径：P1 打出 Hunt the Weak，选择正面公共战场单位且 power <= 3 的目标，双方 priority pass 后结算，目标被摧毁并进入 owner graveyard。
- guard：power > 3、base unit、stale object、face-down standby object、battlefield equipment 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no destroy mutation。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 继续由 viewer-specific snapshot / redaction 保护。
- 本批不新增 protocol / frontend shape；前端仍只消费既有 play-card / stack / destroy event，不本地裁决目标合法性或摧毁结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍按 deferred / design-gated 候选管理，不由本批关闭。

4C-30 B 服务端修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（B）
- `tests/Riftbound.ConformanceTests/HuntTheWeakDestroyGuardTests.cs`（B）

4C-30 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH30_HUNT_THE_WEAK_DESTROY_GUARD_AUDIT.md`（D）
- `docs/CURRENT_STAGE4C_BATCH30_HUNT_THE_WEAK_DESTROY_GUARD_EVIDENCE.md`（D）
- `docs/CURRENT_SERVER_RULE_AUDIT.md`（D）
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`（D）
- `docs/rules-evidence-index.md`（D）
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`（D write lock）

4C-30 E 覆盖矩阵修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`（E）
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`（E）
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`（E）
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`（E）

已跑验证：

- Focused backend：A 记录 Hunt the Weak focused 通过 34/34。
- Adjacent regression：A 记录 adjacent 通过 19/19。
- Backend full：A 记录 `dotnet test Riftbound.slnx --no-restore` 通过 3464/3464。
- Frontend build：A 记录 `npm run build` 通过。
- Chrome smoke：A 记录 `npm run smoke:chrome -- --start-api` 通过。
- 以上 smoke / focused / full test 不得替代最终正式 18-step E2E。

本批关闭的代表子项：

- `FU-282b6e3149` / `UNL-159/219` visible spell resolution destroy public battlefield unit with power <= 3 to owner graveyard 代表路径。
- public battlefield target power ceiling `<= 3` 的服务端权威 guard。
- power > 3、base unit、stale object、face-down standby object、battlefield equipment invalid-target no-mutation 代表护栏。
- face-down standby invalid target 不暴露真实身份 / hidden info 的代表性安全口径。

仍缺：

- 完整 swift / reaction timing、spell duel / battle lifecycle、priority window 与 FEPR 全矩阵。
- 完整 target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- Hunt the Weak 相关 replacement / prevention / cleanup / full targeting matrix 保持 P1/P2 后续项；本批不新增这些方向的 P0。
- 完整 destroy / cleanup / Last Breath trigger interactions、state-based cleanup 与 simultaneous destruction full-official matrix。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Hunt the Weak 代表路径外推完整 swift、reaction timing、targeting、destroy / cleanup、Last Breath trigger、PaymentEngine、FEPR、named deferred candidates 或 full-official。

## 0.1.9 阶段 4C-31 Reprimand Checkpoint

状态：**已完成极窄代表切片文档收口；项目仍 NOT READY。**

阶段 4C-31 名称：Reprimand return-to-hand guard representative baseline。

本批候选审查事实：

- A 选择 Reprimand / 责退 `OGN·172/298` / `FU-d0383ed260` / `REPRIMAND_RETURN_BATTLEFIELD_UNIT_TO_HAND` 作为 4C-31 narrow return-to-hand guard slice。
- Reprimand 规则文本：让一名战场上的单位返回其所属的手牌。
- 代表路径：P1 打出 Reprimand，选择正面公共战场单位目标，双方 priority pass 后结算，目标返回 owner hand。
- guard：base unit、stale object、face-down standby object、battlefield equipment、battlefield spell object、battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no return-to-hand mutation。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 继续由 viewer-specific snapshot / redaction 保护。
- 本批不新增 protocol / frontend shape；前端仍只消费既有 play-card / stack / return-to-hand event，不本地裁决目标合法性或回手结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍按 deferred / design-gated 候选管理，不由本批关闭。

4C-31 B 服务端修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（B）
- `tests/Riftbound.ConformanceTests/ReprimandReturnToHandGuardTests.cs`（B）

4C-31 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH31_REPRIMAND_RETURN_TO_HAND_GUARD_AUDIT.md`（D）
- `docs/CURRENT_STAGE4C_BATCH31_REPRIMAND_RETURN_TO_HAND_GUARD_EVIDENCE.md`（D）
- `docs/CURRENT_SERVER_RULE_AUDIT.md`（D）
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`（D）
- `docs/rules-evidence-index.md`（D）
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`（D write lock）

已跑验证：

- Focused backend：A 记录 focused 通过 58/58。
- Adjacent guard：A 记录 adjacent guard 通过 24/24。
- Backend full：A 记录 backend full 通过 3471/3471。
- Frontend build：A 记录 frontend build passed。
- Chrome smoke：A 记录 Chrome smoke passed。
- 以上 focused / adjacent / full / build / smoke 不得替代最终正式 18-step E2E。

本批关闭的代表子项：

- `FU-d0383ed260` / `OGN·172/298` visible spell resolution return public battlefield unit to owner hand 代表路径。
- public battlefield unit target 的服务端权威 guard。
- base unit、stale object、face-down standby object、battlefield equipment、battlefield spell object、battlefield rune object invalid-target no-mutation 代表护栏。
- face-down standby invalid target 不暴露真实身份 / hidden info 的代表性安全口径。

仍缺：

- Reprimand / swift 相关 swift / reaction timing、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / control-zone matrix 保持 P1/P2 后续项；本批不新增这些方向的 P0。
- 完整 target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 return-to-hand / movement / zone lifecycle、replacement / prevention / cleanup 交织。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Reprimand 代表路径外推完整 swift、reaction timing、spell-duel breadth、targeting、return-to-hand、movement、PaymentEngine、FEPR、named deferred candidates 或 full-official。

## 0.1.10 阶段 4C-32 Ride the Wind Checkpoint

状态：**已完成极窄代表切片文档收口；项目仍 NOT READY。**

阶段 4C-32 名称：Ride the Wind move friendly battlefield unit to owner base ready guard representative baseline。

本批候选审查事实：

- A 选择 Ride the Wind / 驭风而行 `OGN·173/298` / cardId `31403` / `FU-6f84196631` / `RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY` 作为 4C-32 narrow movement + ready target guard slice。
- Ride the Wind 规则文本：迅捷法术，移动一名友方单位，然后让其变为活跃状态。
- 代表路径：P1 打出 Ride the Wind，选择合法 friendly public battlefield unit target，双方 priority pass 后结算，目标 ready 并移动到 owner base。
- guard：enemy battlefield unit、friendly base unit、stale unit、face-down standby object、friendly battlefield equipment、friendly battlefield spell object、friendly battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no ready / no move / no leak。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 继续由 viewer-specific snapshot / redaction 保护。
- 本批不新增 protocol / frontend shape；前端仍只消费既有 play-card / stack / move / ready event，不本地裁决目标合法性或 movement / ready 结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍按 deferred / design-gated 候选管理，不由本批关闭。

4C-32 B 服务端修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（B）
- `tests/Riftbound.ConformanceTests/RideTheWindMoveGuardTests.cs`（B）

4C-32 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH32_RIDE_THE_WIND_MOVE_GUARD_AUDIT.md`（D）
- `docs/CURRENT_STAGE4C_BATCH32_RIDE_THE_WIND_MOVE_GUARD_EVIDENCE.md`（D）
- `docs/CURRENT_SERVER_RULE_AUDIT.md`（D）
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`（D）
- `docs/rules-evidence-index.md`（D）
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`（D write lock）

4C-32 E 覆盖矩阵 / risk / freeze 修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`（E）
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`（E）
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`（E）
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`（E / A 收尾）

已跑验证：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RideTheWind|FullyQualifiedName~Ride|FullyQualifiedName~MoveGuard"` 通过 11/11。
- Adjacent guard regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RideTheWindMoveGuardTests|FullyQualifiedName~BattleOrFlightMoveToBaseTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~GustReturnToHandTests|FullyQualifiedName~HuntTheWeakDestroyGuardTests"` 通过 32/32。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3479/3479。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- 以上 focused / full / build / smoke 不得替代最终正式 18-step E2E。

本批关闭的代表子项：

- `FU-6f84196631` / `OGN·173/298` visible spell resolution ready + move friendly public battlefield unit to owner base 代表路径。
- friendly public battlefield unit target 的服务端权威 guard。
- enemy battlefield unit、friendly base unit、stale unit、face-down standby object、friendly battlefield equipment、friendly battlefield spell object、friendly battlefield rune object invalid-target no-mutation 代表护栏。
- face-down standby invalid target 不暴露真实身份 / hidden info 的代表性安全口径。

仍缺：

- Ride the Wind / swift 相关 swift / reaction timing、spell-duel breadth、完整 movement / roam / precise battlefield / control-zone matrix、owner/controller split、attached-equipment replacement 保持 P1/P2 后续项；本批不新增这些方向的 P0。
- 完整 target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 ready / move / zone lifecycle、replacement / prevention / cleanup 交织。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- full FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Ride the Wind 代表路径外推完整 swift、reaction timing、spell-duel breadth、targeting、movement、roam、precise battlefield、ready、PaymentEngine、FEPR、named deferred candidates 或 full-official。

## 0.1.11 阶段 4C-33 Charm Checkpoint

状态：**已完成极窄代表切片文档收口；项目仍 NOT READY。**

阶段 4C-33 名称：Charm move enemy battlefield unit to owner base target guard representative baseline。

本批候选审查事实：

- A 选择 Charm / 魅惑妖术 `OGN·043/298` / cardId `31255` / `FU-1586b6cdd9` / `CHARM_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE` 作为 4C-33 narrow enemy movement + target guard slice。
- Charm 规则文本：移动一名敌方单位。
- 代表路径：P1 打出 Charm，选择合法 enemy public battlefield unit target，双方 priority pass 后结算，目标移动到 owner base，并保留 damage / power / exhausted / object identity。
- guard：friendly battlefield unit、enemy base unit、stale unit、face-down standby object、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no move / no leak。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 继续由 viewer-specific snapshot / redaction 保护。
- 本批不新增 protocol / frontend shape；前端仍只消费既有 play-card / stack / move event，不本地裁决目标合法性或 movement 结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍按 deferred / design-gated 候选管理，不由本批关闭。

4C-33 B 服务端修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（B）
- `tests/Riftbound.ConformanceTests/CharmMoveToBaseGuardTests.cs`（B）

4C-33 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH33_CHARM_MOVE_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH33_CHARM_MOVE_GUARD_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

4C-33 E 覆盖矩阵 / risk / freeze 修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

已跑验证：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Charm|FullyQualifiedName~MoveToBase|FullyQualifiedName~MoveGuard"` 通过 35/35。
- Adjacent guard regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CharmMoveToBaseGuardTests|FullyQualifiedName~BattleOrFlightMoveToBaseTests|FullyQualifiedName~RideTheWindMoveGuardTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~GustReturnToHandTests|FullyQualifiedName~HuntTheWeakDestroyGuardTests"` 通过 40/40。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3487/3487。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- 以上 focused / full 不得替代最终正式 18-step E2E。

本批关闭的代表子项：

- `FU-1586b6cdd9` / `OGN·043/298` visible spell resolution move enemy public battlefield unit to owner base 代表路径。
- enemy public battlefield unit target 的服务端权威 guard。
- friendly battlefield unit、enemy base unit、stale unit、face-down standby object、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object invalid-target no-mutation 代表护栏。
- face-down standby invalid target 不暴露真实身份 / hidden info 的代表性安全口径。

仍缺：

- Charm 完整目的地选择、full movement / roam / precise battlefield / control-zone matrix、owner/controller split、attached-equipment replacement 保持 P1/P2 后续项；本批不新增这些方向的 P0。
- 完整 target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 movement / zone lifecycle、replacement / prevention / cleanup 交织。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- full FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Charm 代表路径外推完整目的地选择、targeting、movement、roam、precise battlefield、PaymentEngine、FEPR、named deferred candidates 或 full-official。

## 0.1.12 阶段 4C-34 Isolate Checkpoint

状态：**已完成极窄代表切片文档收口；项目仍 NOT READY。**

阶段 4C-34 名称：Isolate move enemy battlefield unit to owner base no-draw target guard representative baseline。

本批候选审查事实：

- A 选择 Isolate / 隔绝 `UNL-124/219` / cardId `34667` / `FU-175d573ae4` / `ISOLATE_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE_NO_DRAW` 作为 4C-34 narrow enemy movement + no-draw target guard slice。
- Isolate 规则文本：将一名敌方单位从战场移动到其基地；然后若该战场上有落单的敌方单位则抽一张牌。
- 代表路径：P1 打出 Isolate，选择合法 enemy public battlefield unit target，双方 priority pass 后结算，目标移动到 owner base，并保留 damage / power / exhausted / object identity。
- 本 fixture 锁定 no-draw 分支，结算事件不包含 `CARD_DRAWN`；落单敌方单位抽牌分支仍未官方化。
- guard：friendly battlefield unit、enemy base unit、stale unit、face-down standby object、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no move / no draw / no leak。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 继续由 viewer-specific snapshot / redaction 保护。
- 本批不新增 protocol / frontend shape；前端仍只消费既有 play-card / stack / move event，不本地裁决目标合法性、movement 结算或 draw 分支。
- Vengeance 保留为 4C-35 低耦合候选；Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍按 deferred / design-gated 候选管理，不由本批关闭。

4C-34 B 服务端修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（B）
- `tests/Riftbound.ConformanceTests/IsolateMoveToBaseGuardTests.cs`（B）

4C-34 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH34_ISOLATE_MOVE_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH34_ISOLATE_MOVE_GUARD_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

4C-34 E 覆盖矩阵 / risk / freeze 修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

已跑验证：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Isolate|FullyQualifiedName~MoveToBase|FullyQualifiedName~MoveGuard"` 通过 46/46。
- Adjacent guard regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~IsolateMoveToBaseGuardTests|FullyQualifiedName~CharmMoveToBaseGuardTests|FullyQualifiedName~BattleOrFlightMoveToBaseTests|FullyQualifiedName~RideTheWindMoveGuardTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~GustReturnToHandTests|FullyQualifiedName~HuntTheWeakDestroyGuardTests"` 通过 48/48。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3495/3495。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过。
- 以上 focused / full / smoke 不得替代最终正式 18-step E2E。

本批关闭的代表子项：

- `FU-175d573ae4` / `UNL-124/219` visible spell resolution move enemy public battlefield unit to owner base no-draw 代表路径。
- enemy public battlefield unit target 的服务端权威 guard。
- friendly battlefield unit、enemy base unit、stale unit、face-down standby object、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object invalid-target no-mutation / no-draw 代表护栏。
- face-down standby invalid target 不暴露真实身份 / hidden info 的代表性安全口径。

仍缺：

- Isolate 落单敌方单位抽牌分支、完整目的地/孤立判定、多位置 battlefield model 保持 P1/P2 后续项；本批不新增这些方向的 P0。
- 完整 target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 movement / roam / precise battlefield / control-zone matrix、owner/controller split、attached-equipment replacement。
- 完整 movement / zone lifecycle、replacement / prevention / cleanup 交织。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Vengeance destroy target route、Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- full FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Isolate 代表路径外推完整落单抽牌、目的地选择、targeting、movement、roam、precise battlefield、PaymentEngine、FEPR、named deferred candidates 或 full-official。

## 0.1.13 阶段 4C-35 Vengeance Checkpoint

状态：**已完成极窄代表切片文档收口；项目仍 NOT READY。**

阶段 4C-35 名称：Vengeance destroy target guard representative baseline。

本批候选审查事实：

- A 选择 Vengeance / 复仇 `OGN·229/298` / cardId `31467` / `FU-07104fa58a` / `VENGEANCE_DESTROY_UNIT` 作为 4C-35 narrow destroy-target guard slice。
- Vengeance 规则文本：摧毁一名单位。
- 代表路径：P1 打出 Vengeance，选择合法 public unit target，双方 priority pass 后结算，目标进入 owner graveyard，并从 base / battlefield 与 public object state 移除。
- 合法目标覆盖：friendly / enemy public unit targets in base / battlefield 均可被摧毁到 owner graveyard；Vengeance 不按 controller 阵营限制目标。
- guard：stale unit、face-down standby object、battlefield / base equipment、battlefield spell object、battlefield rune object、hand / private unit 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no destroy / no leak。
- hidden-info stance：face-down standby target 与 private hand unit target 被拒绝且不暴露真实身份；opponent hidden info 继续由 viewer-specific snapshot / redaction 保护。
- 本批不新增 protocol / frontend shape；前端仍只消费既有 play-card / stack / destroy event，不本地裁决目标合法性或 destroy / cleanup 结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍按 deferred / design-gated 候选管理，不由本批关闭。

4C-35 B 服务端修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（B）
- `tests/Riftbound.ConformanceTests/VengeanceDestroyGuardTests.cs`（B）

4C-35 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH35_VENGEANCE_DESTROY_TARGET_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH35_VENGEANCE_DESTROY_TARGET_GUARD_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

已跑验证：

- Focused backend：通过 107/107。
- Adjacent guard regression：通过 23/23。
- `git diff --check` 通过。
- Backend full：通过 3506/3506。
- Frontend build：通过。
- Chrome smoke：通过。
- 以上 focused / adjacent / diff-check 不得替代最终正式 18-step E2E。

本批关闭的代表子项：

- `FU-07104fa58a` / `OGN·229/298` visible spell resolution destroy public unit target representative baseline。
- friendly / enemy public unit targets in base / battlefield 的服务端权威 destroy-target guard。
- stale unit、face-down standby object、battlefield / base equipment、battlefield spell object、battlefield rune object、hand / private unit invalid-target no-mutation 代表护栏。
- face-down standby / private-zone invalid target 不暴露真实身份 / hidden info 的代表性安全口径。

仍缺：

- Vengeance full-official destroy / cleanup / replacement / prevention / Last Breath interaction 保持 P1/P2 后续项；本批不新增这些方向的 P0。
- 完整 target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 cleanup queue、replacement / prevention duration、attached-equipment detach / replacement breadth、destroyed-this-turn memory 全矩阵。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- full FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Vengeance 代表路径外推完整 destroy、cleanup、replacement、Last Breath、targeting、PaymentEngine、FEPR、named deferred candidates 或 full-official。

## 0.1.14 阶段 4C-36 Hostile Takeover Checkpoint

状态：**4C-37 representative baseline 已验证并入账；项目仍 NOT READY。**

阶段 4C-36 名称：Hostile Takeover control-ready target guard representative baseline。

本批候选审查事实：

- A/B 选择 Hostile Takeover / 恶意收购 `SFD·202/221` / cardId `33301` / `FU-00ee09c2cc` / `HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT` 作为 4C-36 narrow control-ready target guard slice。
- Hostile Takeover 规则文本锚点：获得战场上一名敌方单位的控制权并让其变为活跃状态；回合结束时失去该单位控制权并召回。
- 代表路径：P1 打出 Hostile Takeover，选择 enemy public battlefield unit，双方 priority pass 后结算，P1 获得该单位控制权并 ready；对象 owner 仍为 P2，controller 变为 P1，仍留在 battlefield，并安排 `RETURN_CONTROL_TO_OWNER_AT_TURN_END:P2`。
- 代表证据可引用既有 P5 end-turn return / recall fixture：覆盖临时控制状态在回合结束时归还并召回 owner base；该 fixture 只作为代表证据，不升级 full official。
- guard：friendly battlefield unit、enemy base unit、stale object、face-down standby object、battlefield equipment、battlefield spell object、battlefield rune object、hand / private unit 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no control / no ready / no leak。
- hidden-info stance：face-down standby 与 private-zone target 被拒绝且不暴露真实身份；opponent hidden info 继续由 viewer-specific snapshot / redaction 保护。
- 本批不新增 protocol / frontend shape；前端仍只消费既有 play-card / stack / control / ready / end-turn event，不本地裁决目标合法性、控制权或召回结算。
- Berserk Impulse、Edge of Night、Karthus、Aphelios 仍按 deferred / design-gated 候选管理，不由本批关闭。

4C-36 B 服务端修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（B）
- `tests/Riftbound.ConformanceTests/HostileTakeoverGuardTests.cs`（B）

4C-36 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH36_HOSTILE_TAKEOVER_CONTROL_READY_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH36_HOSTILE_TAKEOVER_CONTROL_READY_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

验证占位：

- Focused backend：通过 265/265。
- Adjacent guard regression：通过 157/157。
- Backend full：通过 3515/3515。
- Frontend build：通过。
- Chrome smoke：通过。
- 上述 focused / adjacent 不得替代最终正式 18-step E2E。

本批关闭的代表子项：

- `FU-00ee09c2cc` / `SFD·202/221` visible spell resolution gain control + ready enemy public battlefield unit representative baseline。
- enemy public battlefield unit target 的服务端权威 control-ready target guard。
- owner/controller split representative check：owner remains P2，controller becomes P1，object remains battlefield。
- `RETURN_CONTROL_TO_OWNER_AT_TURN_END:P2` scheduling representative check。
- friendly battlefield unit、enemy base unit、stale object、face-down standby object、battlefield equipment / spell / rune object、hand / private unit invalid-target no-mutation 代表护栏。
- face-down standby / private-zone invalid target 不暴露真实身份 / hidden info 的代表性安全口径。

仍缺：

- Hostile Takeover full-official standby / reaction timing、battle-start / conquer branch、完整 battlefield / control-zone lifecycle、owner/controller matrix、end-turn cleanup task model 保持 P1/P2 后续项；本批不新增这些方向的 P0。
- 完整 target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 movement / recall / control replacement / cleanup 交织。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- full FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Hostile Takeover 代表路径外推完整待命、反应时机、开战/征服、control lifecycle、end-turn cleanup、targeting、PaymentEngine、FEPR、named deferred candidates 或 full-official。

## 0.1.32 阶段 4C-54 Void Burrower Legend Domain Checkpoint

状态：**4C-54 docs checkpoint recorded；项目仍 NOT READY。**

阶段 4C-54 名称：Void Burrower legend-domain representative automated evidence overlay。

- A 裁决本批不做 direct runtime implementation，只记录 Void Burrower / 虚空遁地兽 `SFD·187/221` / cardId `33285`、`SFD·243/221` / cardId `33354` / `FU-6e7d0dba2c` / `LEGEND_ACTION_DOMAIN` representative automated evidence overlay。
- 已记录 automated evidence：active Void Burrower legend 在征服战场后可自动休眠，展示主牌堆顶部两张牌，存在单位时自动打出一张并回收其余；无可打出单位时回收两张；inactive legend guard 不触发。
- 验证记录：focused 32/32 passed；backend full 3650/3650 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Void Burrower `FU-6e7d0dba2c` representative automated evidence gap。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不做 / 不宣称 direct runtime implementation；不关闭 full-official NO-GO；不关闭 LegendActivePredicate、LegendOptionalTrigger、RevealChoice、shared oracle mapping、hidden / reveal redaction matrix、optional trigger prompt / decline、free-play official semantics、recycle remainder official semantics、unit destination / zone ownership details、`ORDER_TRIGGERS` / battle lifecycle full matrix、FAQ adjudication、1009/811 或 formal E2E。

## 0.1.31 阶段 4C-53 Sett Legend Domain Checkpoint

状态：**4C-53 docs checkpoint recorded；项目仍 NOT READY。**

阶段 4C-53 名称：Sett legend-domain representative automated evidence overlay。

- A 裁决本批不做 direct runtime implementation，只记录 Sett / 腕豪 `OGN·269/298` / cardId `31512` / `FU-6308c2db01` / `LEGEND_ACTION_DOMAIN` representative automated evidence overlay。
- 已记录 automated evidence：Sett 代表路径可自动替代带增益友方单位摧毁、支付 1 mana、消耗增益、以休眠状态召回到基地，并在征服战场时 ready 代表性 Sett legend。
- 验证记录：focused 54/54 passed；backend full 3647/3647 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Sett `FU-6308c2db01` representative automated evidence gap。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不做 / 不宣称 direct runtime implementation；不关闭 full-official NO-GO；不关闭 LegendActivePredicate、LegendOptionalTrigger、ReplacementPayment、boon consume official semantics、dormant recall cleanup、conquest ready lifecycle full matrix、shared oracle mapping、`PAY_COST` prompt / decline、cleanup queue interactions、FAQ adjudication、1009/811 或 formal E2E。

## 0.1.30 阶段 4C-52 Rek'Sai Haste / Overwhelm Checkpoint

状态：**4C-52 docs checkpoint recorded；项目仍 NOT READY。**

阶段 4C-52 名称：Rek'Sai no-optional haste / overwhelm keyword play-unit guard representative baseline。

- 本批只记录 Rek'Sai / 雷克塞 `SFD·029/221` / cardId `33104`、`SFD·029a/221` / cardId `33105` / `FU-1945f6918c` ordinary hand no-optional play-unit + keyword tag guard baseline。
- 已验证范围只限 ordinary hand no-optional `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 3，tags `CARD_TYPE:UNIT` + `强攻` + `急速`，two printings covered。
- 已覆盖 invalid target、wrong zone-source、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：focused 305/305 passed；backend full 3641/3641 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Rek'Sai `SFD·029` / `SFD·029a` ordinary hand no-optional play-unit + keyword tag guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不实现 / 不宣称 full-official haste / overwhelm runtime；不关闭 `HASTE_READY` paid branch full matrix、red resource exactness、Overwhelm / 强攻 battle modifier、`ASSIGN_COMBAT_DAMAGE` overflow behavior、non-hand friendly unit gains haste、LayerEngine、hidden-info、FAQ refs、1009/811 或 formal 18-step E2E。

## 0.1.29 阶段 4C-51 Rek'Sai Attack Reveal Checkpoint

状态：**4C-51 docs checkpoint recorded；项目仍 NOT READY。**

阶段 4C-51 名称：Rek'Sai attack reveal / movement text play-unit guard representative baseline。

- 本批只记录 Rek'Sai / 雷克塞 `SFD·170/221` / cardId `33264`、`SFD·170a/221` / cardId `33265` / `FU-422b450261` ordinary hand play-unit guard baseline。
- 已验证范围只限 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 5，tag `CARD_TYPE:UNIT`，two printings covered。
- 已覆盖 invalid target、wrong zone-source、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：focused 25/25 passed；backend full 3633/3633 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Rek'Sai `SFD·170` / `SFD·170a` ordinary hand play-unit guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不实现 / 不宣称 attack reveal runtime 或 movement runtime；不关闭 attack reveal runtime、top-2 reveal、free play、recycle remainder、unit destination to current battlefield / "here"、hidden-info redaction / reveal matrix、`ORDER_TRIGGERS`、battle lifecycle full matrix、movement / control-zone、FAQ refs、1009/811 或 formal 18-step E2E。

## 0.1.28 阶段 4C-50 Draven Keyword Unit Checkpoint

状态：**4C-50 docs checkpoint recorded；项目仍 NOT READY。**

阶段 4C-50 名称：Draven keyword-unit combat text play-unit guard representative baseline。

- 本批只记录 Draven / 德莱文 `SFD·148/221` / cardId `33240`、`SFD·148a/221` / cardId `33241` / `FU-104211dbbc` ordinary hand play-unit + `法盾` tag guard baseline。
- 已验证范围只限 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 6，tags `CARD_TYPE:UNIT` + `法盾`，two printings covered。
- 已覆盖 invalid target、wrong zone-source、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：focused 17/17 passed；backend full 3625/3625 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Draven `SFD·148` / `SFD·148a` ordinary hand play-unit + `法盾` tag guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不实现 / 不宣称 battle / scoring runtime；不关闭 battle win scoring、destroyed-in-battle opponent scoring、Spellshield target tax、battle cleanup / score once-per-turn matrix、PaymentEngine、FAQ refs、1009/811 或 formal 18-step E2E。

## 0.1.27 阶段 4C-49 Ezreal Checkpoint

状态：**4C-49 docs checkpoint recorded；项目仍 NOT READY。**

阶段 4C-49 名称：Ezreal combat-damage text play-unit guard representative baseline。

- 本批只记录 Ezreal / 伊泽瑞尔 `SFD·082/221` / cardId `33162`、`SFD·082a/221` / cardId `33163`、`SFD·082b/221·P` / cardId `33164` / `FU-2dca1ad450` play-unit guard baseline。
- 已验证范围只限 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 3，tag `CARD_TYPE:UNIT`，three printings covered。
- 已覆盖 invalid target、wrong zone-source、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：focused 21/21 passed；backend full 3617/3617 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Ezreal ordinary hand play-unit + guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不实现 / 不宣称 combat-damage / move runtime；不关闭 attack / defense trigger、“此处” enemy unit target selection、damage equal to Ezreal power、cannot combat damage static、blue swift move to base、swift / reaction timing、blue payment / `PAY_COST`、movement / control-zone matrix、damage prevention / replacement / cleanup、Layer / effective power、FAQ refs、1009/811 或 final 18-step E2E。

## 0.1.26 阶段 4C-48 Vex Checkpoint

状态：**4C-48 docs checkpoint recorded；项目仍 NOT READY。**

阶段 4C-48 名称：Vex spellshield opponent-unit stun static guard representative baseline。

- 本批按 A 收窄边界只记录 test-only Vex / 薇古丝 `UNL-150/219` / cardId `34697` / `FU-9f7cb73dc4` / `VEX_SPELLSHIELD_OPPONENT_UNIT_STUN_STATIC` guard baseline。
- 已验证范围只限 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 4，tags `CARD_TYPE:UNIT` + `法盾` + `约德尔人`。
- 已覆盖 invalid source / target / timing no mutation / no leak guard。
- 验证记录：focused 35/35 passed；backend full 3607/3607 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Vex ordinary hand spellshield-tag play-unit + guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不实现 / 不宣称 opponent-unit stun runtime；不关闭 opponent unit-play listener、battlefield-only condition、`STUNNED` application、cannot-move-this-turn duration、movement guard / cleanup、Spellshield full target tax、FAQ adjudication、1009/811 或 final 18-step E2E。

## 0.1.25 阶段 4C-47 Draven Checkpoint

状态：**4C-47 checkpoint ready；项目仍 NOT READY。**

阶段 4C-47 名称：Draven battle body / play-unit guard representative slice。

- B 完成 Draven / 德莱文 `SFD·020/221` / cardId `33092`、`SFD·020a/221` / cardId `33093` / `FU-964b214448` guard slice，并新增 `tests/Riftbound.ConformanceTests/DravenVanillaGuardTests.cs`。
- Core / frontend / protocol 未改。
- 已覆盖 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 4，tag `CARD_TYPE:UNIT`。
- 已覆盖 invalid target、wrong zone、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：A/B focused 命令 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Draven|FullyQualifiedName~SFD020|FullyQualifiedName~VanillaPlayUnit|FullyQualifiedName~PlayUnit"` 通过 14/14；后端 full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 3601/3601 passed；前端 build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed；Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed。
- 本批只关闭 SFD·020 / SFD·020a ordinary hand play-unit body + guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不关闭 battle win dormant Gold、attack / defense optional red payment、+2 until EOT、full PaymentEngine、Layer / duration cleanup、FAQ refs、1009/811 或 final 18-step E2E。

## 0.1.24 阶段 4C-46 Legend Domain / Shared Oracle Design Gate

状态：**4C-46 design gate recorded；项目仍 NOT READY。**

阶段 4C-46 名称：Legend-domain / shared-oracle design gate for Void Burrower and Sett。

- B/C/D/E 只读门禁一致判断 Void Burrower / 虚空遁地兽 `SFD·187/221` / `FU-6e7d0dba2c` 与 Sett / 腕豪 `OGN·269/298` / `FU-6308c2db01` **NO-GO for direct 4C-46 runtime implementation**。
- 本批只记录设计门禁，不新增 runtime implementation；A 已在 checkpoint 前跑完整验证，确认设计文档 / 矩阵 overlay 未破坏基线。
- 验证记录：后端 full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 3594/3594 passed；前端 build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed；Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed。
- 常驻子 agent：继续复用 B-Review / Maxwell、C-Review / Copernicus、D-Review / Nash、E-Review / Poincare 当前 ID；窗口压缩或句柄待恢复时先 resume 同一 ID，不因一次失联重建。
- Void Burrower 当前服务端代表路径可自动处理征服触发、reveals top two / plays one / recycles remainder / dormant legend；但官方 optional、hidden / reveal choice、shared oracle mapping 未官方化。
- Sett 当前服务端代表路径可自动替代摧毁 / 支付 / 召回 / 征服 ready；但官方 optional replacement、payment、boon、dormant recall cleanup 未官方化。
- P0/P1 保持：LegendActivePredicate、LegendOptionalTrigger、RevealChoice、ReplacementPayment、shared oracle reprint mapping、hidden redaction、`PAY_COST` / cleanup queue interactions、FAQ `SOUL-JFAQ-260114 p14` / `SOUL-OFAQ-260114 p4`、1009/811 与 final 18-step E2E。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不关闭完整 legend trigger/action、hidden reveal/choice/recycle、replacement/payment、boon、dormant recall cleanup 或 shared oracle official coverage。

## 0.1.23 阶段 4C-45 Switcheroo Checkpoint

状态：**4C-45 checkpoint ready；项目仍 NOT READY。**

阶段 4C-45 名称：Switcheroo ultra-narrow representative battlefield power-swap guard overlay。

- B 完成 Switcheroo / 换换乐 `SFD·145/221` / cardId `33237` / `FU-0b6332bbf0` / `SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS` guard slice；新增 `tests/Riftbound.ConformanceTests/SwitcherooGuardTests.cs`，并最小修改 `src/Riftbound.Engine/CoreRuleEngine.cs`。
- 已覆盖 ordinary hand `PLAY_CARD` with two public battlefield unit targets -> stack / pass-pass -> this-turn power swap representative route。
- 已覆盖 non-public battlefield unit target guard：equipment / spell / rune / face-down standby / left-play target 不得入栈或不得在结算时产生 power mutation。
- 验证记录：A focused 命令 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Switcheroo|FullyQualifiedName~PowerSwap|FullyQualifiedName~Power"` 通过 284/284；后端 full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 3594/3594 passed；前端 build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed；Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed。
- 本批只关闭 Switcheroo representative battlefield power-swap target guard overlay。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不关闭 true LayerEngine、later modifier ordering、duration cleanup / EOT expiry、same-battlefield precision beyond current representative model、damage / battle math、full FAQ `SOUL-JFAQ-260114 p14`、1009/811 或 final 18-step E2E。

## 0.1.22 阶段 4C-44 Akshan Checkpoint

状态：**4C-44 checkpoint ready；项目仍 NOT READY。**

阶段 4C-44 名称：Akshan ultra-narrow representative play-unit guard baseline。

- B 完成 Akshan / 阿克尚 `SFD·109/221` / cardId `33194` / `FU-7419ee7d9d` / `AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT` guard slice，并新增 `tests/Riftbound.ConformanceTests/AkshanGuardTests.cs`。
- 已覆盖 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 4，tags `CARD_TYPE:UNIT` + `哨兵` + `百炼`；不选择 optional assemble，不支付 orange-orange extra cost。
- 已覆盖 explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：focused 命令 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Akshan|FullyQualifiedName~PlayUnit|FullyQualifiedName~KeywordUnit|FullyQualifiedName~Assemble"` 已由 B 和 A 均通过，A 结果 189/189；后端 full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 3582/3582 passed；前端 build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed；Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed。
- 本批只关闭 Akshan 普通 hand no-optional / no-extra play-unit guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不关闭 optional assemble、orange-orange extra play、enemy equipment move / control、weapon attach、control-until-leaves cleanup、LayerEngine / continuous effects、FAQ full behavior、1009/811 或 final 18-step E2E。

## 0.1.21 阶段 4C-43 Sfur Song Checkpoint

状态：**4C-43 checkpoint ready；项目仍 NOT READY。**

阶段 4C-43 名称：Sfur Song play-equipment target guard representative baseline。

- B 完成 Sfur Song / 斯弗尔尚歌 `SFD·059/221` / cardId `33139` / `FU-9a623b3185` / `SFUR_SONG_PLAY_EQUIPMENT` guard slice；Core unchanged，Core 无改动。
- 已覆盖 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment。
- 已覆盖 explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：A rerun focused 通过 268/268；A backend full 3576/3576 通过；A frontend build 通过；A Chrome smoke 通过；`jq empty` / matrix targeted assert / `git diff --check` 通过；D 未运行 full tests。
- 本批只关闭 Sfur Song 普通 hand play-equipment target guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不关闭复制宿主技能文字、持续文本 / layer、完整 assemble / equipment attach lifecycle、装备控制权 / 区域移动、FAQ full behavior、1009/811 或 final 18-step E2E。

## 0.1.20 阶段 4C-42 Time Gate Checkpoint

状态：**4C-42 checkpoint ready；项目仍 NOT READY。**

阶段 4C-42 名称：Time Gate play-equipment target guard representative baseline。

- B 完成 Time Gate / 预时之门 `SFD·078/221` / cardId `33158` / `FU-081d97eb3e` / `TIME_GATE_PLAY_EQUIPMENT` guard slice；Core gap none，Core 无改动。
- 已覆盖 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment。
- 已覆盖 explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：A/B focused 通过 292/292；A backend full 3570/3570 通过；A frontend build 通过；A Chrome smoke 通过；`jq empty` / matrix targeted assert / `git diff --check` 通过；D 未运行重测试。
- 本批只关闭 Time Gate 普通 hand play-equipment target guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不关闭 activated / tap ability、payment `[A]`、next spell gains Echo、optional echo payment / repeat、duration cleanup、equipment exhaust / readiness lifecycle、FAQ timing、1009/811 或 final 18-step E2E。

## 0.1.19 阶段 4C-41 Giant Arm Kato Checkpoint

状态：**4C-41 checkpoint ready；项目仍 NOT READY。**

阶段 4C-41 名称：Giant Arm Kato play-unit keyword-tag target guard representative baseline。

- B 完成 Giant Arm Kato / 巨腕加藤 `SFD·112/221` / cardId `33198` / `FU-464ec8c275` / `GIANT_ARM_KATO_PLAY_KEYWORD_UNIT` guard slice；Core gap none，Core 无改动。
- 已覆盖 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 3，tags `CARD_TYPE:UNIT` + `法盾`。
- 已覆盖 explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：A/B focused 通过 99/99；A backend full 3564/3564 通过；A frontend build 通过；A Chrome smoke 通过；`jq empty` / matrix targeted assert / `git diff --check` 通过。
- 本批只关闭 Giant Arm Kato 普通 hand play-unit keyword-tag target guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不关闭 Spellshield target tax、move-to-battlefield trigger、friendly-unit choice / prompt、keyword grant、+power until EOT、LayerEngine / duration cleanup、movement / control matrix、FAQ、1009/811 或 final 18-step E2E。

## 0.1.18 阶段 4C-40 Sea Monster Hook Checkpoint

状态：**4C-40 checkpoint ready；项目仍 NOT READY。**

阶段 4C-40 名称：Sea Monster Hook play-equipment target guard representative baseline。

- B 完成 Sea Monster Hook / 海兽钓钩 `OGN·242/298` / cardId `31482` / `FU-2653af0380` / `SEA_MONSTER_HOOK_PLAY_EQUIPMENT` guard slice；Core gap none，Core 无改动。
- 已覆盖 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment。
- 已覆盖 explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：A/B focused 通过 272/272；A backend full 3558/3558 通过；A frontend build 通过；A Chrome smoke 通过；`jq empty` / matrix targeted assert / `git diff --check` 通过。
- 本批只关闭 Sea Monster Hook 普通 hand play-equipment target guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不关闭 activated ability：pay 1 + yellow + exhaust、destroy friendly unit、top-five look / choice、free play、recycle remainder、hidden / zone / payment / layer / FAQ、1009/811 或 final 18-step E2E。

## 0.1.17 阶段 4C-39 Zhonya's Hourglass Checkpoint

状态：**4C-39 checkpoint ready；项目仍 NOT READY。**

阶段 4C-39 名称：Zhonya's Hourglass play-equipment target guard representative baseline。

- B 完成 Zhonya's Hourglass / 中娅沙漏 `OGN·077/298` / cardId `31291` / `FU-fb79eea7fc` / `ZHONYAS_HOURGLASS_PLAY_EQUIPMENT` guard slice；Core gap none，Core 无改动。
- 已覆盖普通 hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment。
- 已覆盖 explicit target reject no mutation，以及 source not in hand / wrong zone、opponent source、face-down standby source、insufficient mana no mutation / no leak guard。
- 验证记录：A/B focused 通过 268/268；A backend full 3552/3552 通过；A frontend build 通过；A Chrome smoke 通过；`jq empty` / matrix targeted assert / `git diff --check` 通过。
- 本批只关闭 Zhonya 普通 hand play-equipment target guard representative evidence。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不关闭 standby / reaction timing、destroy replacement recall、完整 equipment / layer / FAQ、hidden info、1009/811 或 final 18-step E2E。

## 0.1.16 阶段 4C-38 Edge of Night Checkpoint

状态：**D 最小文档入账；项目仍 NOT READY。**

阶段 4C-38 名称：Edge of Night play-equipment / assemble-purple target guard representative baseline。

- B 完成 Edge of Night / 夜之锋刃 `SFD·139/221` / cardId `33229` / `FU-804412488c` / `EDGE_OF_NIGHT_PLAY_EQUIPMENT` 的 test-only narrow representative guard slice；Core gap none，服务端 Core 无改动。
- 已覆盖普通 `PLAY_CARD` hand route：0 target -> stack / pass-pass -> base equipment；explicit target rejected 且 no payment / no mutation。
- 已覆盖 face-up controlled base Edge of Night `ASSEMBLE_PURPLE` -> friendly public unit target -> pay 1 purple -> `COST_PAID` + `EQUIPMENT_ATTACHED`。
- 已覆盖 invalid assemble no tick / no events / no payment / no stack / no attach / no leak：face-down / hidden source、source in hand、opponent source、already-attached source、unknown source、unknown / opponent / face-down standby / non-unit target、missing / wrong optional cost、insufficient purple。
- 验证记录：A focused filter 通过 98/98；backend full 3546/3546 通过；frontend build 通过；Chrome smoke 通过。
- 本批只关闭 narrow assemble / play guard representative evidence；Edge of Night face-down standby immediate attach remains P0 / design-gated。
- 仍保持 `fullOfficial=false`；不宣称 READY / READY-CANDIDATE；不关闭 full official standby immediate attach、hidden redaction、equipment layer、FAQ、1009/811 或 final 18-step E2E。

## 0.1.15 阶段 4C-37 Berserk Impulse Checkpoint

状态：**D 文档代表切片已入账；项目仍 NOT READY。**

阶段 4C-37 名称：Berserk Impulse opponent top main-deck unit target-guard representative baseline。

本批候选审查事实：

- A/B 选择 Berserk Impulse / 暴怒冲动 `OGN·025/298` / cardId `31231` / `FU-b05eda44ce` / `BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT` 作为 4C-37 narrow opponent top main-deck unit target guard slice。
- Berserk Impulse 规则文本锚点：`迅捷`；每名对手展示其主牌堆顶部一张牌，你从中选择一张，并当作自己的牌打出、无视费用，然后回收其余卡牌。
- 代表路径：P1 从手牌打出 Berserk Impulse 并支付 4，选择 P2 已揭示 / 代表性 public top main-deck unit；双方 priority pass 后，该单位从 P2 main deck 顶部打出到 P1 base。
- 代表事件语义：`UNIT_PLAYED_TO_BASE` 记录 source spell、target object、`ownerPlayerId=P2`、`playedByPlayerId=P1`、`sourceZone=MAIN_DECK`、`destinationZone=BASE`。
- 结算后目标单位 damage reset to 0、until-end-of-turn effects / power modifier 清空、exhausted reset to false。
- target guard：friendly top unit、opponent second main-deck unit、top spell / equipment / rune、face-down top unit、private hand / base / battlefield unit 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no deck movement / no stack item / no unit played / no leak。
- dirty resolution guard：stack target 结算前不再是 opponent top、target 非 unit、face-down top unit、wrong controller / ownership dirty top target 均不移动，不产生 `UNIT_PLAYED_TO_BASE`；源法术正常入墓。
- hidden-info stance：本批只覆盖代表性“已揭示 / 可选 top object”目标 guard 与 face-down / private-zone no-leak；不覆盖完整隐藏区展示、选择 prompt、未选牌回收 redaction 或多对手隐私边界。
- 本批不新增 protocol / frontend shape；前端仍不本地裁决目标合法性、隐藏信息展示或免费打出结算。
- Edge of Night、Karthus、Aphelios 仍按 deferred / design-gated 候选管理，不由本批关闭。

4C-37 B 服务端修改文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`（B）
- `tests/Riftbound.ConformanceTests/BerserkImpulseGuardTests.cs`（B）

4C-37 D 文档修改文件：

- `docs/CURRENT_STAGE4C_BATCH37_BERSERK_IMPULSE_OPPONENT_TOP_UNIT_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH37_BERSERK_IMPULSE_OPPONENT_TOP_UNIT_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

4C-37 E 覆盖矩阵修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

验证记录：

- Focused backend：通过 17/17。
- Matrix / diff：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过；`git diff --check` 通过。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3529/3529。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- 上述 focused / full / build / smoke 不得替代最终正式 18-step E2E。

本批关闭的代表子项：

- `FU-b05eda44ce` / `OGN·025/298` visible / revealed opponent top main-deck unit played to P1 base representative baseline。
- opponent top main-deck unit target 的服务端权威 target guard。
- owner/source/play-by event semantics representative check。
- damage / until-end-of-turn / exhausted reset representative check。
- invalid friendly top、opponent second、top spell / equipment / rune、face-down top unit、private hand / base / battlefield target no-mutation / no-leak 代表护栏。
- dirty resolution top changed / non-unit / face-down / wrong controller no-move 代表护栏。

仍缺：

- Berserk Impulse full-official multi-opponent reveal / choose / recycle、full hidden-zone prompt / redaction、未选牌回收、非单位分支、完整“当作自己的牌打出”owner/controller/payment matrix 保持 P0 / design-gated；最终 READY 前仍不能关闭。
- 完整 spell duel / reaction timing、target prompt、target invalidation、hidden / random-zone policy。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- 完整 LayerEngine、free-play branch interactions、private-zone replay redaction。
- Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- full FAQ regression、1009/811 full-official、正式 18-step E2E、completion audit。

口径：

- `fullOfficial=false`。
- 不宣称 READY / READY-CANDIDATE。
- 不因 Berserk Impulse 代表路径外推完整 hidden-zone reveal / choose / recycle、spell duel / reaction timing、PaymentEngine、LayerEngine、FAQ closure、1009/811 full-official 或正式 18-step E2E。

## 0.2 阶段 0 当前基线

阶段 0 只做主控建档、只读审计与任务拆分，不实现功能代码。已读取：

- `docs/A_MASTER_AGENT_GOAL.md`
- `docs/符文战场_前端Web开发需求文档_给Codex.md`
- `docs/符文战场_服务端核心规则自查文档.md`
- 五份本地规则 / FAQ PDF：`《符文战场》核心规则_260330.pdf`、`裁判FAQ_251023.pdf`、`铸魂淬炼系列_官方FAQ_260114.pdf`、`铸魂淬炼系列_裁判FAQ.pdf`、`《符文战场》破限系列_裁判FAQ_260416.pdf`

### 当前仓库结构

- `src/Riftbound.Api`：ASP.NET Core HTTP API 与 SignalR `GameHub`。
- `src/Riftbound.Contracts`：协议 DTO、命令、snapshot、prompt、事件与错误码。
- `src/Riftbound.Engine`：规则引擎、MatchSession、恢复、日志、卡牌/关键词/战场/传奇能力目录。
- `src/Riftbound.CardCatalog`：官网卡牌快照加载、schema 校验、行为规格与关键词覆盖报告。
- `src/Riftbound.Persistence`：PostgreSQL journal / recovery / player store。
- `src/Riftbound.DevUi`：React + TypeScript + Vite Web UI。
- `tests/Riftbound.ConformanceTests`：规则、Hub、恢复、fixture 与 conformance 测试。
- `data/official`：2026-04-27 官网卡牌快照，当前目标统计口径为 1009 条。
- `docs`：主控、审计、计划、证据和验收文档。

### 当前已修改文件

当前 `git status --short --branch` 显示：

- 新增暂存/未提交：`docs/A_MASTER_AGENT_GOAL.md`
- 已修改：`docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/rules-evidence-index.md`
- 已修改协议/服务端：`src/Riftbound.Contracts/Protocol.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`
- 已修改前端：`src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`、`src/Riftbound.DevUi/src/components/match/MatchTopBar.tsx`、`src/Riftbound.DevUi/src/pages/MatchPage.tsx`、`src/Riftbound.DevUi/src/stores/useMatchController.ts`、`src/Riftbound.DevUi/src/styles/globals.css`、`src/Riftbound.DevUi/src/types/protocol.ts`、`src/Riftbound.DevUi/src/utils/errors.ts`
- 已修改测试：`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`、`tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-moonrise-enemy-battlefield-power-minus-2.fixture.json`、`tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- 未跟踪主控/审计文档：`docs/CURRENT_A_MASTER_CHECKPOINT.md`、`docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`、`docs/CURRENT_PAYMENT_LAYER_DESIGN.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`
- 未跟踪服务端文件：`src/Riftbound.Engine/PaymentCostRules.cs`
- 未跟踪本地文件：`riftbound-dotnet.sln`，不属于交付，默认不提交。

注意：`PaymentCostRules.cs` 已被 `CoreRuleEngine.cs` 引用，任何合并该批服务端改动时必须一并纳入，否则会构建失败。

### 当前服务端接口

HTTP：

- `GET /health`
- `GET /catalog/summary`
- `GET /catalog/p3-status`
- `GET /catalog/behavior-specs?cardNo=...`
- `GET /catalog/keyword-coverage`

SignalR：

- Hub：`/hubs/game`
- Client callable：`JoinRoom(roomId, playerId, reconnectToken?)`、`Reconnect(roomId, playerId, reconnectToken)`、`RequestSnapshot(roomId, playerId)`、`Pass(roomId, playerId, clientIntentId)`、`EndTurn(roomId, playerId, clientIntentId)`、`Ready(roomId, playerId, clientIntentId)`、`SubmitIntent(roomId, playerId, clientIntentId, cmd)`、development-only `SeedScenario(roomId, playerId, scenarioId, clientIntentId)`
- Server events：`Joined`、`Snapshot`、`Prompt`、`Events`、`Error`

核心协议 DTO：

- `SnapshotDto(Tick, TurnNumber, ActivePlayerId, Players, Lanes, Stack, Timing, TurnState)`
- `ActionPromptDto(PlayerId, Actionable, Reason, Actions, PromptId?, SnapshotTick?, Candidates?, View?)`
- `ActionPromptCandidateDto(Action, Label, Enabled, Reason, Sources?, Targets?, Destinations?, Modes?, OptionalCosts?, Metadata?)`
- `PromptViewDto(Type, Title, Message, RelatedBattlefieldId?, RelatedStackItemId?, RelatedBattleId?, RelatedSpellDuelId?, MinSelection?, MaxSelection?, Metadata?)`
- `GameEvent(Kind, Description, Payload)`
- `ErrorDto(Code, Message)`

当前没有名为 `MatchSnapshot`、`LegalAction`、`RoomState`、`GameLogEntry`、`ActionError` 的独立公开 DTO；真实公开名以 `SnapshotDto`、`ActionPromptCandidateDto`、SignalR room/session payload、`GameEvent`、`ErrorDto` 为准。

### 当前前端接口

前端服务 adapter：

- `ApiClient.health() -> /health`
- `ApiClient.behaviorSpecs() -> /catalog/behavior-specs`
- `ApiClient.keywordCoverage() -> /catalog/keyword-coverage`
- `MatchSocket.connect()`
- `MatchSocket.joinRoom(roomId, playerId, reconnectToken?)`
- `MatchSocket.reconnect(roomId, playerId, reconnectToken)`
- `MatchSocket.requestSnapshot(roomId, playerId)`
- `MatchSocket.ready(roomId, playerId, clientIntentId)`
- `MatchSocket.submitIntent(roomId, playerId, clientIntentId, command)`
- `MatchSocket.disconnect()`

前端路由：

- `home`、`cards`、`decks`、`lobby`、`room/:roomId`、`match/:matchId`、`result/:matchId`、`settings`

前端命令类型当前包括：

- `SUBMIT_DECK`、`MULLIGAN`、`PASS_PRIORITY`、`PASS_FOCUS`、`PASS`、`END_TURN`、`SURRENDER`、`PLAY_CARD`、`HIDE_CARD`、`REVEAL_CARD`、`TAP_RUNE`、`RECYCLE_RUNE`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT`、`DECLARE_BATTLE`、`ACTIVATE_ABILITY`、`LEGEND_ACT`
- 命令可附 `promptId/snapshotTick`，但 C-Review 指出下一轮应把戳从“提交时套当前 prompt”调整为“命令生成时捕获对应 prompt”，避免旧草稿贴新戳。

### 当前 P0/P1 阻断

结论：**NOT READY**。

P0：

- 完整 battlefield / standby / control / held / conquer task lifecycle 未官方化。
- central cleanup task queue 未覆盖所有状态变化、替代效果和进出战场路径。
- spell duel / battle lifecycle 仍有大量代表路径，缺完整官方 pending / focus / initial-stack / combat damage assignment / battle cleanup 状态机。
- `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 尚无正式 prompt / command / payload schema。
- 完整正式 18 步 E2E 尚未以同一连续正式牌局收口。
- 当前复杂 prompt 只有安全降级展示，尚未形成正式产品交互。

P1：

- PaymentEngine 仍未统一；主支付路径已有事件包络，但自动触发 / 替代费用等旧 `COST_PAID` 路径仍待迁移。
- LayerEngine / 持续效果 / 替代效果 / 禁止效果仍未达到最终完整模型。
- 1009 张卡效果覆盖矩阵、官方文本、FAQ 证据与自动化测试仍未最终清零。
- replay / recovery / determinism 仍需按全命令、全恢复、全随机边界补最终审计。
- 文档存在若干已修复项与历史批次描述混杂的问题，需要 D/E 后续清理 superseded 标注。

### B/C/D/E 子 agent 分工

- B：服务端规则 / 协议 / 测试。下一轮优先做负力量残留 clamp 审计、payment envelope 自动路径、battle/spellDuel 生命周期 id 预备、central cleanup queue 设计切片。
- C：前端契约 / Web UI / Chrome smoke。下一轮优先做 prompt 戳生成位置审查、未知 prompt 通用降级策略、metadata 安全展示策略、`relatedBattleId/relatedSpellDuelId` 展示与 E2E 断言准备。
- D：文档、规则证据、P0/P1 审计。下一轮优先更新前端契约缺口矩阵、阶段 0 checkpoint、completion audit 与 NOT READY 口径。
- E：卡牌效果覆盖、官方文本与 FAQ 证据矩阵。下一轮优先建立 full-official / representative 区分、1009 张卡覆盖矩阵、规则证据缺口索引。

### 不可并行修改文件列表

同一轮只能由一个 agent 修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs`
- `src/Riftbound.Contracts/Protocol.cs`
- `src/Riftbound.DevUi/src/stores/useMatchController.ts`
- `src/Riftbound.DevUi/src/services/matchSocket.ts`
- `src/Riftbound.DevUi/src/types/protocol.ts`
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/components/match/CardDetailDrawer.tsx`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/rules-evidence-index.md`
- `data/official/card-catalog.zh-CN.json`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/**`

阶段 0 完成后必须等待用户确认，再进入下一阶段。

## 0.3 阶段 1 协议与规则基线汇总

阶段 1 已按 `docs/A_MASTER_AGENT_GOAL.md` 执行，范围限定为协议、`ActionPrompt` / command / payload、snapshot / visibility、服务端 P0 阻断梳理与小范围初步收口；未进入完整前端重建，未进入 1009 张卡全量实现，结论仍为 **NOT READY**。

阶段 1 checkpoint 保护：

- 已创建 commit：`78b6896 checkpoint: complete stage 1 protocol baseline`
- 该 commit 纳入 `docs/A_MASTER_AGENT_GOAL.md`、`docs/CURRENT_A_MASTER_CHECKPOINT.md`、`docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md` 与被 `CoreRuleEngine` 引用的 `src/Riftbound.Engine/PaymentCostRules.cs`。
- `riftbound-dotnet.sln` 是本地不交付文件，未纳入 commit。
- commit 前验证：`git diff --check` 通过；`dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore` 3312/3312 通过；`npm run build` 通过。

### B 服务端协议 / 规则 / 测试

完成项：

- 确认真实协议 DTO 字段，不需要修改 `src/Riftbound.Contracts/Protocol.cs`。
- 为 `promptId + snapshotTick` 服务端语义补测试：匹配戳可通过；stale `promptId` 继续 `PROMPT_EXPIRED`；仅 `snapshotTick` 过期且 `promptId` 匹配时也返回 `PROMPT_EXPIRED`。
- 修复负力量残留 clamp：真实负力量叠加临时正战力修正后，回合结束 / 清除临时修正不再错误归零；snapshot 的 `basePower` 保留负值。

本阶段 B 修改：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`

关闭：

- 负力量“临时正修正到期后被夹成 0”的服务端 P0 残留。
- prompt 戳服务端最小语义测试缺口。

未关闭：

- 完整 CleanupQueue、Battle / SpellDuel lifecycle、PaymentEngine、LayerEngine、复杂 prompt payload、全卡 full-official-rule-pass。

### C 前端契约

完成项：

- `useMatchController.submitCommand` 只补缺失的 `promptId/snapshotTick`，不覆盖命令生成时已经携带的戳。
- `ActionPanel` 与 `CardDetailDrawer` 在命令生成处携带当前 prompt 戳。
- `PromptType` 允许未知服务端类型，避免未来复杂窗口让前端类型层卡死。
- 复杂 / 未知 prompt fallback 扩为安全通用策略；metadata 默认只展示数量 / 类型摘要，只有白名单安全字段展示字符串。
- 未新增 `PAY_COST`、伤害分配或触发排序命令；前端仍只展示、收集并提交服务端已有候选。

本阶段 C 修改：

- `src/Riftbound.DevUi/src/types/protocol.ts`
- `src/Riftbound.DevUi/src/stores/useMatchController.ts`
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/components/cards/CardDetailDrawer.tsx`

未关闭：

- 正式 `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` payload / schema / command。
- prompt 变化后清空各类 composer 草稿的系统化处理。
- 正式支付、伤害分配、触发排序专用 UI。
- typed snapshot views、battle / spell duel lifecycle 展示字段、cleanup queue 解释字段。

### D 文档 / 前端契约缺口

完成项：

- 新增 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`，记录真实 `SnapshotDto`、`ActionPromptDto`、`PromptViewDto`、`ActionPromptCandidateDto`、`GameEvent`、`ErrorDto` 字段，并明确当前没有独立 `MatchSnapshot` / `LegalAction` / `RoomState` / `GameLogEntry` / `ActionError` DTO。
- 更新 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`，区分复杂 prompt 降级展示已做 / 正在做，与正式 payload / command / 专用交互仍缺。
- 更新 `docs/CURRENT_RULE_EVIDENCE_TODO.md`，把已验收的 0 / 负战力、具体战场大小写移到防回归，并补阶段 1 阻断视角。

本阶段 D 修改 / 新增：

- 新增 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`
- 修改 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- 修改 `docs/CURRENT_RULE_EVIDENCE_TODO.md`

关闭：

- 阶段 1 协议字段文档口径缺口。

未关闭：

- 复杂 prompt 正式 payload / command、typed error details、stack / timing / object state 稳定字段、battle / spellDuel 完整 lifecycle 文档与实现缺口。

### E 卡牌覆盖基线

完成项：

- 新增 `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`。
- 基于 `data/official/card-catalog.zh-CN.json` 做只读统计：官方快照 1009 条，1009 个唯一 `id`，1009 个唯一精确 `cardNo`。
- 建立阶段 1 统计口径：card entry、collector no、base collector、effect-oracle / functional unit、representative、full-official。
- 确认 functional unit 口径为 811 个功能单元；113 个重复功能组涉及 311 条，理论可少写 198 个重复实现。
- 确认 `cardQaList` 为 0，FAQ 证据必须从 PDF / FAQ 抽取，不能从卡表字段推断。

本阶段 E 新增：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`

未关闭：

- 1009 张卡 full-official 覆盖矩阵、FAQ 证据抽取、逐卡实现 / 测试。

是否允许进入卡牌效果批量覆盖：**不允许**。阶段 4 前不得启动 1009 张全量实现。

### 阶段 1 修改 / 新增文件

本阶段新增：

- `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`

本阶段修改：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `src/Riftbound.DevUi/src/types/protocol.ts`
- `src/Riftbound.DevUi/src/stores/useMatchController.ts`
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/components/cards/CardDetailDrawer.tsx`
- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

注意：阶段 1 之外的既有未提交改动仍在工作区，包括 `docs/A_MASTER_AGENT_GOAL.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/rules-evidence-index.md`、`src/Riftbound.Contracts/Protocol.cs`、`src/Riftbound.Engine/PaymentCostRules.cs` 等；最终合并时必须统一审查。

### 阶段 1 验收命令

A 主控复验：

- `git diff --check`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3312/3312 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过；包含 `check:event-labels` 与 `check:user-facing-text`。

子 agent 报告的补充验证：

- B 聚焦测试 4/4 通过，相关回归 16/16 通过，完整 conformance 3312/3312 通过。
- C DevUi build 通过，前端锁定文件 `git diff --check` 通过。

### 阶段 1 判断

- 服务端协议是否可以冻结：**阶段 1 协议壳可以冻结**。`SnapshotDto`、`ActionPromptDto`、`PromptViewDto`、`ActionPromptCandidateDto`、`GameEvent`、`ErrorDto` 作为当前基线可用；但复杂窗口仍只是预留，不代表完整官方规则 READY。
- 是否允许 C 进入正式前端 UI 开发：**只允许进入阶段 2 的基础页面 / 数据层 / 外壳开发**；复杂支付、伤害分配、触发排序 UI 必须等待服务端正式 payload / command 落地。
- 是否允许 E 进入卡牌效果批量覆盖：**不允许**。E 只能继续做 FAQ 抽取计划、覆盖矩阵和代表 / full-official 区分。
- 是否标记 READY：**不允许**。

### 阶段 1 后仍存在的 P0/P1

P0：

- 完整 battlefield / standby / control / held / conquer task lifecycle 未官方化。
- central cleanup task queue 未覆盖所有状态变化、替代效果和进出战场路径。
- spell duel / battle lifecycle 缺完整官方 pending / focus / initial-stack / damage assignment / cleanup 状态机。
- `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 尚无正式 prompt / command / payload schema。
- 正式 18 步 E2E 未以同一连续正式牌局收口。
- 正式复杂 prompt 交互仍缺。

P1：

- PaymentEngine 自动触发 / 替代费用路径仍未完全统一。
- LayerEngine / 持续效果 / 替代效果 / 禁止效果未最终收口。
- 1009 张卡 full-official 覆盖矩阵、官方文本、FAQ 证据与自动化测试未完成。
- replay / recovery / determinism 仍需最终审计。

### 下一阶段建议

阶段 2 只建议进入“前端数据层与基础页面”：

- C 可推进正式 UI 外壳、页面结构和 snapshot / prompt 数据流，但不得实现复杂规则窗口裁决。
- B 继续设计 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 的正式服务端 schema 与最小状态机切片。
- D 维护阶段 2 前端契约验收矩阵。
- E 继续 FAQ 抽取计划和覆盖矩阵，不进入批量卡牌实现。

阶段 1 完成后必须等待用户确认，再进入阶段 2。

## 0.4 阶段 2 服务端 P0 契约收口 + 前端基础页面 / 数据层汇总

阶段 2 已按用户确认后的阶段门槛执行，范围限定为服务端 P0 契约骨架、复杂 prompt / command / payload schema、前端基础 UI / 数据层安全外壳、规则证据链与卡牌覆盖矩阵框架；未进入完整前端重建，未进入 1009 张卡全量实现，未启动最终 18 步 E2E，结论仍为 **NOT READY**。

阶段 2 开始前 checkpoint 保护：

- 已创建阶段 1 WIP commit：`78b6896 checkpoint: complete stage 1 protocol baseline`
- 已创建阶段 1 保护记录 commit：`bc0872d docs: record stage 1 checkpoint protection`
- 已创建阶段 2 checkpoint commit：`dfc4bd4 checkpoint: complete stage 2 protocol and frontend baseline`
- `riftbound-dotnet.sln` 是本地不交付文件，阶段 2 仍未纳入。

### B 服务端协议 / P0 契约骨架

完成项：

- 在 `src/Riftbound.Contracts/Protocol.cs` 增加 `CommandTypes` 常量，并为 `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 建立正式 command / payload DTO 壳。
- 新增 `ActionPromptContractDto` 与 `ActionPromptContracts`，明确三类复杂 prompt 的 `promptKind`、candidate action、required payload、legal choices、validation error、visible metadata、hidden metadata 口径。
- 新增 `ErrorCodes.InvalidPayload = "INVALID_PAYLOAD"`，让 malformed payload 有稳定错误码。
- 在 `CoreRuleEngine` 为三类复杂命令加入当前阶段的服务端权威入口与稳定拒绝路径：没有正式运行窗口时拒绝，不让前端自行裁决。
- 补 conformance / hub / fixture shape 测试，覆盖 schema 注册、字段形状、错误语义和无窗口拒绝。

关闭：

- `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 不再是“完全没有正式命令 / payload 名称”的 P0 子阻断。
- malformed 复杂命令 payload 不再缺稳定错误语义。

未关闭：

- 真实 `PAY_COST` pending window、`DECLINE_PAY_COST`、PaymentEngine 统一支付状态机仍缺。
- 真实伤害分配状态机、合法性校验、battle cleanup 仍缺。
- 真实触发排序 pending window、触发队列、同控触发排序规则仍缺。
- battlefield / standby / control / held / conquer lifecycle、central cleanup queue、spell duel / battle lifecycle 仍未完整官方化。

### C 前端基础 UI / 数据层

完成项：

- 新增 `SnapshotDebugPanel`，只展示权威 snapshot / prompt 摘要：tick、turn、active player、phase、stack、task count、prompt stamp、candidate、public zone counts。
- 在 `MatchPage` 接入 debug panel，在 `ActionPanel` 对复杂 / 未知 prompt 保持安全降级：只展示服务端候选摘要与“需要服务端正式交互支持”，不计算规则结果。
- 前端协议类型同步阶段 2 schema：`ActionPromptContractDto`、`ActionPromptContracts`、`CombatDamageAssignmentDto`、三类复杂 `GameCommand` union。
- 错误文案同步 `INVALID_PAYLOAD`。

未做且禁止在本阶段做：

- 未实现或模拟 `PAY_COST` 实际支付结果。
- 未实现或模拟 `ASSIGN_COMBAT_DAMAGE` 合法性 / 分配结果。
- 未实现或模拟 `ORDER_TRIGGERS` 排序结果。
- 未实现 spell duel / battle / conquer / victory 的本地裁决。

### D 规则证据 / 审计文档

完成项：

- 新增 `docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`，记录阶段 2 三类复杂 prompt 契约计划与运行时缺口。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_FRONTEND_CONTRACT_GAPS.md` 与 `docs/rules-evidence-index.md`。
- 为 battlefield / standby / control / held / conquer lifecycle、cleanup queue、spell duel / battle lifecycle、damage assignment、`PAY_COST`、`ORDER_TRIGGERS` 建立规则证据链入口。
- 标记阶段 1/2 已替代的历史口径：0 / 负战力、具体战场 objectId、replay/final hash 旧 wording、复杂 prompt “无入口”旧 wording。

### E 卡牌覆盖矩阵 / FAQ 框架

完成项：

- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`。
- 新增 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 新增 `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`。
- 建立当前只读矩阵统计：1009 snapshot entries、811 functional units、694 direct card behavior functional units、117 non-`PLAY_CARD` representative domain units、179 FAQ candidate units、227 FAQ candidate snapshot entries。
- 输出前 20 个高风险 functional units，供后续阶段优先实现和测试。

仍禁止：

- 不进入 1009 张卡全量效果实现。
- 不修改核心规则引擎。

### 阶段 2 修改 / 新增文件

阶段 2 新增：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`
- `src/Riftbound.DevUi/src/components/match/SnapshotDebugPanel.tsx`

阶段 2 修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/rules-evidence-index.md`
- `src/Riftbound.Contracts/Protocol.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/pages/MatchPage.tsx`
- `src/Riftbound.DevUi/src/styles/globals.css`
- `src/Riftbound.DevUi/src/types/protocol.ts`
- `src/Riftbound.DevUi/src/utils/errors.ts`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

阶段 2 已提交为 `dfc4bd4 checkpoint: complete stage 2 protocol and frontend baseline`；`riftbound-dotnet.sln` 仍为本地不交付未跟踪文件，不应纳入。

### 阶段 2 验收命令

A 主控复验：

- `git diff --check`：通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3318/3318 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过；包含 `check:event-labels` 与 `check:user-facing-text`。
- Chrome smoke：当前仓库未发现既有 smoke 脚本，阶段 2 未运行；C 的下一步应先补一个最小 smoke 脚本，再进入正式 UI smoke。

子 agent 报告的补充验证：

- B 聚焦新增测试 6/6 通过，相关回归 7/7 通过，完整 conformance 3318/3318 通过。
- C DevUi build 通过，前端锁定文件 `git diff --check` 通过。
- D 文档 diff check 通过。
- E JSON skeleton `jq empty` 通过，diff check 通过。

### 阶段 2 判断

- 关闭的 P0/P1：**未关闭完整 P0/P1**；仅关闭三类复杂 prompt “无正式 schema / command 名称”的 P0 子阻断与 malformed payload 稳定错误语义缺口。
- 服务端协议是否可以冻结：**只能冻结阶段 2 契约壳**。`PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 的 command / payload 名称与 prompt contract 字段可作为前端迁移基线；真实 runtime window、合法性、结算顺序和状态机尚不能冻结。
- 是否允许 C 进入正式前端 UI 开发：**只允许继续基础页面、数据层、UI shell、safe fallback 与 smoke 脚本**；不得实现支付、伤害分配、触发排序、battle / spell duel / conquer / victory 的本地规则结果。
- 是否允许 E 进入卡牌效果批量覆盖：**不允许**。E 只能继续矩阵、FAQ 证据、风险排序和代表单元拆分。
- 是否标记 READY：**不允许**。

### 阶段 2 后仍存在的 P0/P1

P0：

- `PAY_COST` 真实 pending window / choice validation / decline / PaymentEngine 状态机仍缺。
- `ASSIGN_COMBAT_DAMAGE` 真实 battle damage assignment window / legality / cleanup 仍缺。
- `ORDER_TRIGGERS` 真实 trigger queue / ordering window / deterministic resolution 仍缺。
- battlefield / standby / control / held / conquer lifecycle 仍未完整官方化。
- central cleanup task queue 仍未覆盖替代效果、状态变化和进出区域路径。
- spell duel / battle lifecycle 仍缺完整 pending / focus / initial-stack / damage assignment / cleanup 状态机。
- 最终 18 步 E2E 与 Chrome smoke 正式链路未启动。

P1：

- PaymentEngine 自动触发 / 替代费用路径仍未完全统一。
- LayerEngine / 持续效果 / 替代效果 / 禁止效果未最终收口。
- 1009 张卡 full-official 覆盖矩阵、官方文本、FAQ 证据与自动化测试未完成。
- replay / recovery / determinism 仍需最终审计。
- `GameCommandJsonMapper` 尚未把三类复杂命令映射成强类型解析路径，当前阶段依赖 raw command 稳定拒绝与契约壳。

### 下一阶段建议

阶段 3 建议仍按小切片推进，禁止同时大改：

- B 优先从 `PAY_COST` 真实 pending window 与 decline/validation 状态机切入；每完成一个窗口就补 conformance / hub 测试。
- C 先补最小 Chrome smoke 脚本，再继续首页 / 大厅 / 房间 / Match UI shell；复杂 prompt 只消费 B 已冻结的字段。
- D 继续把阶段 2 证据链落到具体规则页码 / FAQ 条目，并保持 superseded 口径。
- E 继续高风险 functional unit 的 FAQ 映射与测试设计，不进入全量实现。

阶段 2 完成后必须等待用户确认，再进入阶段 3。

## 0.5 阶段 3A Smoke 基线 / 强类型复杂命令 / PAY_COST 最小 runtime / 对战桌面外壳汇总

阶段 3 已按用户补充要求收窄为 3A / 3B / 3C。当前只执行并收口 **阶段 3A**；不继续推进完整阶段 3，不启动最终 18 步 E2E，不进入 1009 张卡全量实现，结论仍为 **NOT READY**。

当前阶段 3A 基线：

- 阶段 3A checkpoint commit 已创建：`3d70ef0 checkpoint: complete stage 3A smoke mapper and pay cost slice`。
- 阶段 3A 计划入口：`docs/CURRENT_STAGE3A_PLAN.md`。
- `docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md` 只保留为宽阶段 3 总审计图，不作为当前 3A 验收范围。
- `riftbound-dotnet.sln` 仍是本地不交付未跟踪文件，不应纳入后续 checkpoint commit。

阶段 3A commit 后复验：

- `git status --short --branch`：仅剩未跟踪本地文件 `riftbound-dotnet.sln`。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3324/3324 通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过；包含 `check:event-labels` 与 `check:user-facing-text`。Vite 仅输出 SignalR 依赖的 Rollup 注释提示。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过；API health、DevUi preview、Chrome headless / CDP 7 个基础路由均 OK，无脚本捕获 runtime error。
- 当前结论仍是 **NOT READY**。

### B 服务端 3A 切片

完成项：

- `GameCommandJsonMapper` 已把 `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 从 raw payload 映射为对应强类型 command。
- malformed complex payload 稳定走 `INVALID_PAYLOAD` 或等价稳定错误路径；unsupported/raw command 兼容行为未破坏。
- 新增 `PendingPaymentState` 与 `pay-cost-window` development seed，只实现一个最小 `PAY_COST` runtime 窗口。
- 服务端 `PAY_COST` prompt 暴露 `paymentId`、`paymentWindow`、cost、legal payment choices / choice ids 等元数据。
- 合法 `PAY_COST` 可提交并关闭窗口；非法 choice、错误玩家、错误窗口、资源不足、非 `PAY_COST` 命令均拒绝且不改变 authoritative state。
- Hub 层已覆盖 stale `promptId` / stale `snapshotTick` 拒绝，失败命令不广播 snapshot / event。

关闭的 3A P0 子项：

- 3A-P0-002：三类复杂命令强类型映射。
- 3A-P0-003：`PAY_COST` 最小 runtime 切片。

仍未关闭：

- 完整 PaymentEngine、decline / optional / replacement / trigger cost 全路径。
- `ASSIGN_COMBAT_DAMAGE` 真实 runtime。
- `ORDER_TRIGGERS` 真实 runtime。
- 完整 battle / spell duel / cleanup / battlefield lifecycle。

### C 前端 3A 外壳 / Smoke

完成项：

- 新增 `npm run smoke:chrome`，脚本可启动 API、启动 DevUi preview、启动 Google Chrome headless / CDP 并打开 3A 基础路由。
- Smoke 覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- 对战桌面外壳增加房间 / match status / battlefield / hand / ActionPanel / log / snapshot debug 的安全展示。
- 前端 `PAY_COST` 只在服务端 candidate metadata 明确提供 `paymentId`、`paymentWindow`、`paymentChoiceIds` 时提交对应服务端候选，不计算支付结果。
- `ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS`、`SPELL_DUEL_ACTION` 和未知复杂 prompt 继续安全降级，不在前端计算伤害、触发排序、战场控制、胜负。
- A 验证时为新增后端事件 `PAYMENT_WINDOW_CLOSED` 补了一个 EventLog 中文标签，属于 build 阻断的小型展示修复。

关闭的 3A P0 子项：

- 3A-P0-001：Chrome smoke 基线已存在并通过。
- 3A-P0-004：前端对战桌面外壳保持服务端权威与 safe fallback。

仍未关闭：

- 真实房间创建 / 加入 / 准备 / 开始 / 连续对战 18 步 E2E。
- 正式支付选择 UI、伤害分配 UI、触发排序 UI。
- 战场控制、争夺、胜负、battle / spell duel 的完整产品交互。

### D 3A 审计 / 证据

完成项：

- 新增 `docs/CURRENT_STAGE3A_PLAN.md`，明确 3A 范围、P0/P1、证据位和验收红线。
- 新增 `docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md` 作为宽阶段 3 总图，但当前不按该文档展开实现。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md`。

### E 3A 卡牌 / FAQ 证据

完成项：

- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`。
- 新增 `docs/CURRENT_CARD_EFFECT_STAGE3A_SMOKE_PAY_COST_EVIDENCE.md`。
- 只建立 3A smoke / PAY_COST 最小切片相关 functional units 与 FAQ 证据入口，不进入 1009 张卡全量效果实现。

### 阶段 3A 修改 / 新增文件

阶段 3A 新增：

- `docs/CURRENT_CARD_EFFECT_STAGE3A_SMOKE_PAY_COST_EVIDENCE.md`
- `docs/CURRENT_STAGE3A_PLAN.md`
- `docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md`
- `src/Riftbound.DevUi/scripts/chrome-smoke.mjs`
- `src/Riftbound.DevUi/src/components/match/MatchStatusPanel.tsx`

阶段 3A 修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/rules-evidence-index.md`
- `src/Riftbound.Contracts/GameCommandJsonMapper.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `src/Riftbound.DevUi/package.json`
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/components/match/BattlefieldArea.tsx`
- `src/Riftbound.DevUi/src/components/match/EventLog.tsx`
- `src/Riftbound.DevUi/src/components/match/PlayerBoard.tsx`
- `src/Riftbound.DevUi/src/pages/MatchPage.tsx`
- `src/Riftbound.DevUi/src/pages/RoomPage.tsx`
- `src/Riftbound.DevUi/src/styles/globals.css`
- `src/Riftbound.DevUi/src/types/protocol.ts`

### 阶段 3A 验收命令

A 主控复验：

- `git diff --check`：通过。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3324/3324 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过；包含 `check:event-labels` 与 `check:user-facing-text`。Vite 仅输出 SignalR 依赖的 Rollup 注释提示。
- `source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过；API health、DevUi preview、Chrome headless / CDP 7 个基础路由均 OK，无脚本捕获的 runtime error。

### 阶段 3A 判断

- 阶段 3A 收口标准：**已满足**。
- 是否标记 READY：**不允许**。
- 是否进入阶段 3B / 3C：**不允许自动进入**，必须等待用户确认。
- 是否允许 C 开始正式复杂交互：仅 `PAY_COST` 最小窗口可基于服务端 candidate 做安全提交；`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS`、battle / spell duel / battlefield control 仍只能 safe fallback。
- 是否允许 E 进入 1009 全量：**不允许**。

下一阶段建议：

- 用户已确认进入阶段 3B；阶段 3A 后续由下方 0.6 记录接续。
- 仍不得标记 READY，不得启动最终 18 步 E2E，不得进入 1009 张卡全量效果实现。

## 0.6 阶段 3B Battlefield / Standby / Control / Conquer lifecycle + Central cleanup queue 汇总

阶段 3B 已按用户确认范围执行：只收口 battlefield lifecycle、standby lifecycle、control / contested / uncontrolled、conquer / hold scoring 最小流程、central cleanup queue 最小稳定循环，以及前端 battlefield UI 与 authoritative snapshot 联调。未启动最终 18 步 E2E，未进入 1009 张卡 full-official 实现，结论仍为 **NOT READY**。

本阶段核心产物：

- 服务端 snapshot 新增/稳定 `lanes.battlefields[].unitsBySide/standbySlots/standbySlotCount/hiddenStandbyCount/scoredThisTurn/scoredThisTurnPlayerIds`，并为 `timing.pendingTaskQueue` 增加最小 metadata。
- 对手视角会隐藏 battlefield 面朝下 standby 的 `objectId`，并从 opponent-visible `zones.battlefields`、`objects`、非法待命 cleanup task id 中脱敏。
- 据守/征服相关得分路径增加 `BATTLEFIELD_SCORE_GAINED_THIS_TURN` marker，同一战场同一回合重复得分返回 `BATTLEFIELD_SCORE_PREVENTED`，且不会重复扣费或改变分数。
- 前端 battlefield UI 只读取服务端 snapshot/event：两处战场、控制者/无人控制/争夺中、待命区、双方单位、本回合得分状态、中央清理和战场日志；未在前端计算控制权、争夺、得分、胜负、伤害分配或触发排序。
- 文档和卡牌矩阵已补 3B 证据边界：`docs/CURRENT_STAGE3B_PLAN.md`、server/frontend gaps、rule evidence todo、rules evidence index、card coverage baseline/matrix/risk 与 3B battlefield lifecycle evidence。

阶段 3B 修改 / 新增文件：

- 服务端 / 测试：`src/Riftbound.Engine/MatchSession.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`、`tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`。
- 前端 / smoke：`src/Riftbound.DevUi/scripts/chrome-smoke.mjs`、`src/Riftbound.DevUi/src/components/match/BattlefieldArea.tsx`、`src/Riftbound.DevUi/src/components/match/MatchStatusPanel.tsx`、`src/Riftbound.DevUi/src/components/match/SnapshotDebugPanel.tsx`、`src/Riftbound.DevUi/src/pages/MatchPage.tsx`、`src/Riftbound.DevUi/src/styles/globals.css`、`src/Riftbound.DevUi/src/types/protocol.ts`。
- 文档 / 证据：`docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_A_MASTER_CHECKPOINT.md`。
- 新增文档：`docs/CURRENT_STAGE3B_PLAN.md`、`docs/CURRENT_CARD_EFFECT_STAGE3B_BATTLEFIELD_LIFECYCLE_EVIDENCE.md`。
- `riftbound-dotnet.sln` 仍是本地未跟踪文件，不应纳入 checkpoint commit。

阶段 3B 验收命令：

- `git diff --check`：通过。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3325/3325 通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过；包含 `check:event-labels` 与 `check:user-facing-text`，仅有 SignalR 依赖的 Rollup PURE 注释 warning。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过；API health、DevUi preview、Chrome headless / CDP 路由 smoke 通过，`/matches/stage3-smoke` 覆盖中央清理、中央战场、待命区、服务端行动提示和权威快照摘要，并断言 debug 页面不出现 `mainDeck/runeDeck/handHidden/stackItemId/reconnectToken` raw 字段。

阶段 3B checkpoint 保护：

- 已创建 commit：`a74beac78ec3f555dd478b61b012d875d81dfa5c checkpoint: complete stage 3B battlefield cleanup baseline`。
- commit 后 `git status --short --branch`：仅剩未跟踪本地文件 `riftbound-dotnet.sln`，未纳入 checkpoint。
- commit 后后端验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3325/3325 passed。
- commit 后前端 build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仍只有 SignalR/Rollup PURE 注释 warning。
- commit 后 Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过，`/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result` 均 OK。
- 当前结论仍是 **NOT READY**。
- 下一阶段建议为阶段 3C；开始前仍需用户明确确认。

阶段 3B 判断：

- 阶段 3B 收口标准：**已满足当前最小切片**。
- 是否标记 READY：**不允许**。
- 是否启动最终 18 步 E2E：**不允许**。
- 是否进入 1009 张卡全量：**不允许**。
- 是否允许前端进入后续 battlefield / cleanup UI 联调：可以继续基于服务端 snapshot/prompt 做只读展示和 prompt candidate 提交；仍不得本地裁决控制权、争夺、清理、得分或胜负。

仍存在 P0/P1：

- P0：cleanup queue 全触发面统一 enqueue 与 repeat-until-stable 最终审计未完成。
- P0：control freeze/release 在完整 battle / spell duel 期间的代表 fixture 仍未关闭。
- P0：失控待命延迟清理的全部时机和所有 standby 卡族仍未关闭。
- P0：held/conquer scoring order、得分替代、付费触发拒付、同时触发排序和全战场卡仍未关闭。
- P0：完整 battle lifecycle、完整 damage assignment runtime、完整 `ORDER_TRIGGERS` runtime、完整 PaymentEngine / LayerEngine、最终 18 步 E2E 仍未关闭。
- P1：前端 battlefield / cleanup 字段仍偏 DevUi，正式 DTO 和解释字段未冻结。

下一阶段建议：

- 阶段 3B checkpoint commit 已创建并通过 post-commit 后端测试、前端 build 与 Chrome smoke；继续排除 `riftbound-dotnet.sln`。
- 等待用户确认后，阶段 3C 建议优先切 control freeze/release、battle task lifecycle 与 damage assignment 的最小服务端窗口，同时补双窗口 reload/reconnect 隐藏信息 smoke；仍不进入最终 18 步 E2E 或 1009 全量。

## 0.7 阶段 3C Spell Duel / Battle Lifecycle / ASSIGN_COMBAT_DAMAGE 汇总

阶段 3C 已按用户确认范围执行：只收口 spell duel / battle lifecycle 的最小官方化切片、`ASSIGN_COMBAT_DAMAGE` 服务端权威 runtime prompt / validation / simultaneous commit、battle cleanup 代表链，以及前端 spell duel / battle / damage assignment UI 安全接线。未启动最终 18 步 E2E，未进入 1009 张卡 full-official 实现，结论仍为 **NOT READY**。

本阶段核心产物：

- 服务端 `ASSIGN_COMBAT_DAMAGE` 不再停在 shell：最小 runtime 会验证 battleId / battlefieldId、wrong player、stale prompt、unknown target、damage total mismatch、非法超分配、lethal-first violation、malformed payload，并保证失败命令不改变权威状态。
- 服务端 prompt / snapshot 表达 `battleId`、`battlefieldId`、`assigningPlayerId`、`damagePool`、`legalTargets`、`existingDamage`、`lethalDamageThreshold`、`requiredAssignments`；合法提交后同时造成战斗伤害，致命单位进入 cleanup，battle close 后重新计算 battlefield control / contested。
- 服务端测试覆盖 spell duel pass close -> damage assignment -> legal assignment -> simultaneous damage -> battle cleanup -> battlefield control update 的连续路径，同时覆盖 illegal assignment、stale prompt 和隐藏待命不泄漏。
- 前端只读取服务端 snapshot/prompt candidate：展示 spell duel / battle 状态、focus / priority、attacker / defender、battlefieldId、damage assignment prompt 与服务端合法目标；只提交服务端支持的 `ASSIGN_COMBAT_DAMAGE` command，不本地裁决伤害、战斗结果、战场控制或胜负。
- 文档和卡牌矩阵已补 3C 证据边界：`docs/CURRENT_STAGE3C_SPELL_DUEL_BATTLE_DAMAGE_EVIDENCE.md`、`docs/CURRENT_CARD_EFFECT_STAGE3C_BATTLE_DAMAGE_EVIDENCE.md`、server/frontend gaps、rule evidence todo、card coverage baseline/matrix/risk。

阶段 3C 修改 / 新增文件：

- 服务端 / 协议 / 测试：`src/Riftbound.Contracts/Protocol.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`。
- 前端 / smoke：`src/Riftbound.DevUi/scripts/chrome-smoke.mjs`、`src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`、`src/Riftbound.DevUi/src/components/match/EventLog.tsx`、`src/Riftbound.DevUi/src/components/match/MatchStatusPanel.tsx`、`src/Riftbound.DevUi/src/styles/globals.css`。
- 文档 / 证据：`docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_A_MASTER_CHECKPOINT.md`。
- 新增文档：`docs/CURRENT_STAGE3C_SPELL_DUEL_BATTLE_DAMAGE_EVIDENCE.md`、`docs/CURRENT_CARD_EFFECT_STAGE3C_BATTLE_DAMAGE_EVIDENCE.md`。
- `riftbound-dotnet.sln` 仍是本地未跟踪文件，不应纳入 checkpoint commit。

阶段 3C 验收命令：

- `git diff --check`：通过。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3329/3329 通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过；包含 `check:event-labels` 与 `check:user-facing-text`，仅有 SignalR 依赖的 Rollup PURE 注释 warning。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过；API health、DevUi preview、Chrome headless / CDP 路由 smoke 通过，`/matches/stage3-smoke` 覆盖法术对决、战斗、伤害分配、中央清理、战场、待命区、服务端行动提示和权威快照摘要，并断言 debug 页面不出现 `mainDeck/runeDeck/handHidden/stackItemId/reconnectToken/battleState/damageLedger/participantControllerIds` raw 字段。

阶段 3C checkpoint 保护：

- 已创建 commit：`2c10a1b3f433cb28a4bc806753e02236da831a15` (`checkpoint: complete stage 3C battle damage baseline`)。
- commit 后 `git status --short --branch`：仅剩未跟踪本地文件 `riftbound-dotnet.sln`，未纳入 checkpoint。
- commit 后后端验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3329/3329 passed。
- commit 后前端 build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仍只有 SignalR/Rollup PURE 注释 warning。
- commit 后 Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过，`/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result` 均 OK。
- 当前结论仍是 **NOT READY**。
- 下一阶段建议为阶段 3D / 第三阶段收口；开始前仍需用户明确确认。

阶段 3C 判断：

- 阶段 3C 收口标准：**已满足当前最小切片**。
- 是否标记 READY：**不允许**。
- 是否启动最终 18 步 E2E：**不允许**。
- 是否进入 1009 张卡全量：**不允许**。
- 是否允许前端进入后续 battle / damage UI 联调：可以继续基于服务端 snapshot/prompt 做只读展示和 prompt candidate 提交；仍不得本地裁决 battle、damage assignment、battlefield control 或 victory。

仍存在 P0/P1：

- P0：完整 spell duel lifecycle 未完成，尤其 `SPELL_DUEL_ACTION`、全反应链、触发排序和全部 close -> next task 全路径。
- P0：完整 battle lifecycle 未完成，尤其完整 battle task、战斗响应窗口、初始栈、所有多攻防组合和 control freeze/release。
- P0：完整 `ASSIGN_COMBAT_DAMAGE` full-rule runtime 未完成，3C 只关闭最小 prompt / validation / simultaneous commit；壁垒、后排、同优先级、负战力、不可分配和替代/预防矩阵仍缺。
- P0：battle cleanup 全路径未完成，替代/预防、LayerEngine、control freeze/release 与 cleanup queue 全触发面仍缺。
- P0：`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、完整 PaymentEngine / LayerEngine、最终 18 步 E2E、1009 张卡 full-official 矩阵仍未关闭；3D 后已关闭的只是最小 runtime / UI / evidence 子项。
- P1：`SpellDuelView` / `BattleView` / `BattleResolutionView` / `DamageAssignmentPromptView` 正式 DTO 尚未冻结，当前仍偏 DevUi / dictionary view。

下一阶段建议：

- 阶段 3C checkpoint 已保护，继续排除 `riftbound-dotnet.sln`。
- 等待用户确认后进入阶段 3D / 第三阶段收口；该项后续已由 3D 更新为：`ORDER_TRIGGERS` 最小 runtime / UI / evidence 已关闭，battle initial stack / 完整 trigger ordering、control freeze/release、双窗口 reload/reconnect 隐藏信息 smoke 仍留后续；仍不进入最终 18 步 E2E 或 1009 全量，除非用户明确开启对应阶段。

## 0.8 阶段 3D 第三阶段收口审计

阶段 3D 按用户确认范围执行：D 只做文档 / 规则证据 / 第三阶段收口审计；B/C/E 已并行完成 `ORDER_TRIGGERS` 最小 runtime / UI / evidence。阶段 3D 不启动最终验收版 18 步 E2E，不进入 1009 张卡 full-official 实现，结论仍为 **NOT READY**。

阶段 3D checkpoint 保护：

- 已创建 commit：`698c4ae7545b60c383e974e796eb8e2b06835a64 checkpoint: complete stage 3 core flow baseline`
- commit 后工作树状态：仅剩未跟踪本地文件 `riftbound-dotnet.sln`，该文件不属于交付，未纳入 commit。
- commit 后后端测试：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3333/3333。
- commit 后前端 build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留 SignalR / Rollup `PURE` 注释提示。
- commit 后 Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过，覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- 第三阶段结论：**DONE**。
- 项目整体结论：仍为 **NOT READY**。
- 下一阶段建议：阶段 4 大任务，即全量卡牌效果覆盖 + FAQ 回归 + 正式 18 步 E2E + 最终审计。

本阶段核心产物：

- 新增 `docs/CURRENT_STAGE3_COMPLETION_AUDIT.md`，汇总 3A / 3B / 3C / 3D 的关闭子项、仍缺 P0/P1、阶段 4 入口和最终验收边界。
- `ORDER_TRIGGERS` / 多触发排序最小 runtime 已由 B 完成：prompt metadata 包含 `orderingPlayerId/orderedTriggerIds/triggerIds/triggers/triggerChoices/legalOrderingConstraints/triggeredByEventKind`；command 支持 `orderedTriggerIds` 并兼容 `triggerIds`；合法排序清空 `TriggerQueue`、按顺序加入 `StackItems`、设置 priority player，并发出 `TRIGGERS_ORDERED` / `TRIGGERS_MOVED_TO_STACK`。
- C 已完成 `ORDER_TRIGGERS` UI：上移 / 下移排序，提交 `orderedTriggerIds`，不本地结算触发；新增 `stage3-preflight.mjs`，C 侧 build / smoke / preflight 通过。
- E 已补 stage3D 矩阵 overlay 和 `ORDER_TRIGGERS` 证据文档。
- B 验证记录：`ConformanceFixtureShapeTests` 109/109 通过；full `dotnet test Riftbound.slnx --no-restore` 3333/3333 通过；`git diff --check` 通过。
- A 主控最终验证：`git diff --check` 通过；`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 3333/3333 通过；`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api` 通过。
- 已把 priority/focus、spell duel close、battle lifecycle、damage assignment、battle cleanup、battlefield control update、conquer/hold scoring、standby visibility、cleanup queue 的第三阶段证据状态整理为“已有关联证据 / 仍缺口 / 下一阶段”。
- 明确第三阶段 preflight / smoke 不是最终验收版 18 步 E2E；不得宣称 1009 张卡已经覆盖。

第三阶段收口判断：

- 3A 已关闭：Chrome route smoke 基线、三类复杂命令 typed mapper、`PAY_COST` 最小 runtime、前端外壳不裁决规则。
- 3B 已关闭：battlefield / standby snapshot 只读字段、非法待命 cleanup 代表路径、control / held / conquer 代表结果、central cleanup queue 最小 task view。
- 3C 已关闭：spell duel focus/pass/close 代表链、battle view / battle resolution 最小 task、`ASSIGN_COMBAT_DAMAGE` 最小 runtime prompt / submit / reject / simultaneous commit、battle damage -> cleanup -> control update 代表链。
- 3D 已关闭：`ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项、第三阶段收口审计口径、阶段 4 / 最终验收边界文档风险。
- 上述关闭均为阶段性最小切片，不等于 READY。

仍存在 P0/P1：

- P0：完整 trigger engine、完整 effect resolution、APNAP / 跨控制者复杂排序、battle initial stack 全规则、trigger cost / decline / payment 未完成。
- P0：完整 priority/focus / `SPELL_DUEL_ACTION`、spell duel 全反应链和触发排序交织未完成。
- P0：完整 battle lifecycle、battle response window、control freeze/release 和 next task 全路径未完成。
- P0：`ASSIGN_COMBAT_DAMAGE` 全规则矩阵仍缺壁垒、后排、同优先级、负战力、不可分配和替代/预防。
- P0：battle cleanup 全路径、cleanup queue 全触发面、standby 全时机、conquer/hold scoring order 未完成。
- P0：完整 PaymentEngine / `DECLINE_PAY_COST`、LayerEngine、最终 18 步 E2E、1009 张卡 full-official 覆盖仍未完成。
- P1：正式 DTO、隐藏信息三层断言、产品 UI polish、replay/recovery/determinism 全边界仍需后续审计。

是否允许进入阶段 4：

- A 主控判断：**第三阶段 DONE，可以准备进入阶段 4**。
- 不允许把阶段 4 入口误读为 READY、最终验收版 18 步 E2E 或 1009 全量阶段。

阶段 3D checkpoint commit 建议文件列表：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_CARD_EFFECT_STAGE3D_ORDER_TRIGGERS_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_STAGE3_COMPLETION_AUDIT.md`
- `src/Riftbound.Contracts/GameCommandJsonMapper.cs`
- `src/Riftbound.Contracts/Protocol.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `src/Riftbound.DevUi/scripts/chrome-smoke.mjs`
- `src/Riftbound.DevUi/scripts/stage3-preflight.mjs`
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/components/match/EventLog.tsx`
- `src/Riftbound.DevUi/src/components/match/MatchStatusPanel.tsx`
- `src/Riftbound.DevUi/src/styles/globals.css`
- `src/Riftbound.DevUi/src/types/protocol.ts`
- `src/Riftbound.DevUi/src/utils/formatters.ts`
- 明确排除：`riftbound-dotnet.sln`。

## 0.9 阶段 4 / 4A 第三阶段基线复核

阶段 4 主控任务已固化到 `docs/CURRENT_STAGE4_MASTER_PLAN.md`。阶段 4 范围是全量卡牌效果覆盖 + FAQ 回归 + 正式 18 步 E2E + 最终审计；当前仍 **NOT READY**，不得自动 `update_goal complete`，不得自动标记最终 READY。

4A 复核范围：

- 复核阶段 3D checkpoint commit。
- 复核后端 full test、前端 build、Chrome smoke。
- 复核 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 三类复杂 prompt 仍工作。
- 复核 battlefield / standby / control / conquer / cleanup / battle / damage 基线仍工作。
- 复核隐藏信息不泄漏。
- 更新本 checkpoint。

4A 复核结果：

- 3D checkpoint commit：`698c4ae7545b60c383e974e796eb8e2b06835a64 checkpoint: complete stage 3 core flow baseline` 已存在于 `main`。
- 4A 前工作树：仅有 `docs/CURRENT_A_MASTER_CHECKPOINT.md` 的 3D post-commit 记录和未跟踪本地 `riftbound-dotnet.sln`；后续新增本阶段计划文档 `docs/CURRENT_STAGE4_MASTER_PLAN.md`。`riftbound-dotnet.sln` 继续不提交。
- 后端 full test：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3333/3333。
- 前端 build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留 SignalR / Rollup `PURE` 注释提示。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过，覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- Stage 3 preflight 复核：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api` 通过；日志在脚本成功后出现 API 关停期间的 SignalR / Npgsql cancellation 记录，不作为阻断。
- 复杂 prompt 证据：`tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs` 仍覆盖 `PayCostPromptExposesPendingPaymentWindow`、`AssignCombatDamagePromptExposesRuntimeMetadataAndHidesOpponentStandby`、`OrderTriggersPromptExposesRuntimeMetadata`、`TRIGGERS_ORDERED` / `TRIGGERS_MOVED_TO_STACK`，并由 full test 覆盖。
- 核心生命周期证据：同一测试文件仍覆盖 standby visibility、illegal standby cleanup、unattached equipment cleanup、pending cleanup task、battle / spell duel / damage assignment 代表链。
- 隐藏信息证据：服务端测试仍覆盖 face-down standby redaction、`handHidden` 计数、random seed / cursor redaction；Chrome smoke 和 stage3 preflight 继续断言 debug 页面不出现 `mainDeck`、`runeDeck`、`handHidden`、`damageLedger`、`triggerQueue` 等 raw hidden 字段。
- 前端权威性复核：`ActionPanel` 仍只根据服务端 `prompt.view` / `candidate.action` 渲染复杂 prompt，并提交服务端声明的 command；未发现前端自行裁决支付、伤害分配、触发排序、战场控制、战斗结果、法术对决结果、得分或胜负。

4A 结论：**通过**。没有新增 P0/P1；允许进入 4B 卡牌覆盖矩阵冻结。项目整体仍 **NOT READY**。

## 0.10 阶段 4B 卡牌覆盖矩阵冻结

阶段 4B 按阶段 4 主控任务执行：只读取 `data/official/card-catalog.zh-CN.json` 中 2026-04-27 固定官网快照，不实时抓取官网，不实现卡牌效果，不进入 4C 批量代码实现。E 负责覆盖矩阵写入；A 负责只读复核、结构校验和 checkpoint。

4B 产物：

- 新增 `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 更新 `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`。

4B 冻结口径：

- 官方快照：`data/official/card-catalog.zh-CN.json`。
- `fetchedAt = 2026-04-27`。
- snapshot entries = 1009。
- unique `cardId` = 1009。
- unique exact collector id / `cardNo` = 1009。
- functional units = 811。
- unique oracle/effectIds = 807。
- token / rune / battlefield / promo / `*` 变体 / lowercase suffix 或异画全部计入 1009 entries：token 13、rune 48、battlefield 59、promo `·P` 4、`*` 变体 36、lowercase suffix / alternate-art 100。

4B status 计数：

| status | functional units | snapshot entries |
|---|---:|---:|
| `IMPLEMENTED_TESTED` | 50 | 77 |
| `IMPLEMENTED_UNTESTED` | 30 | 30 |
| `SHARED_ORACLE_IMPLEMENTATION` | 102 | 273 |
| `NEEDS_ENGINE_SUPPORT` | 501 | 501 |
| `NEEDS_FAQ_REVIEW` | 128 | 128 |
| `BLOCKED` | 0 | 0 |

4B A 复核命令：

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：通过。
- `jq -e` 核验 `sourceCatalog.snapshotEntries == 1009`、`sourceCatalog.functionalUnits == 811`、`snapshotEntries.length == 1009`、`functionalUnits.length == 811`、unique `cardId/cardNo` 均为 1009、unique `functionalUnitId` 为 811、`stage4BCardCoverageFreeze` 存在、六类 status definitions 存在、`BLOCKED` 计数为 0：通过。
- `jq -e` 核验 status counts 合计为 811 / 1009、full-official uncovered list 为 811、`ready == false`、`no4CImplementation == true`：通过。
- `jq -e` 核验所有 `snapshotEntries[].stage4B` 和 `functionalUnits[].stage4B` 必备字段存在：通过。
- `git diff --check -- docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json docs/CURRENT_CARD_EFFECT_RISK_TOP20.md docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`：通过。

4B 风险 / 批量顺序：

- full-official coverage 仍为 0/811；4B 只冻结矩阵，不授予 READY 或 full-official。
- `NEEDS_ENGINE_SUPPORT` status flag 仍影响 762 FUs。
- `NEEDS_FAQ_REVIEW` status flag 仍影响 179 FUs。
- Top risk 仍从中娅沙漏、海兽钓钩、德莱文、伊泽瑞尔、薇古丝、雷克塞、沉没神庙、战或逃等高风险 functional units 开始。
- 4C 建议顺序：先清 P0/P1 engine support blockers，再做 FAQ adjudication + ruling-backed tests，再做 reusable oracle/effectId clusters，再补 implemented-but-untested direct FUs，最后审核 representative tested FUs 是否可升级。

4B 结论：**通过**。矩阵可以解释 1009 / 811 差异，没有新增 P0/P1；允许进入 4C 高风险 functional units 批量实现与测试。项目整体仍 **NOT READY**。

## 0.11 阶段 4C-1 ORDER_TRIGGERS APNAP / battle initial stack 第一批

阶段 4C-1 按 functional unit / engine blocker 推进，不按卡号盲目实现。本批只处理高风险 trigger ordering 基础能力：`ORDER_TRIGGERS` 的保守 APNAP controller-block runtime、battle initial stack 代表路径、hidden trigger source metadata redaction，以及覆盖矩阵 / 规则证据 overlay。未进入 1009 张卡 full-official 实现，未启动正式 18 步 E2E，项目整体仍 **NOT READY**。

4C-1 服务端改动：

- `ORDER_TRIGGERS` ordering player 从“队列第一项控制者”调整为 APNAP controller-block 排序玩家。
- prompt metadata 中 `triggerIds` 保留 raw queue order，`orderedTriggerIds` 改为服务端可直接提交的 `STACK_RESOLUTION_ORDER_TOP_FIRST` 默认合法顺序。
- validation 要求 submitted order 匹配保守 APNAP legal resolution controller blocks；允许同控制者 block 内重排，拒绝跨控制者 block 非法重排。
- 合法排序后按确定顺序进入 stack，事件记录 `orderingPolicy`、`controllerBlockOrder`、`legalResolutionControllerBlockOrder`。
- `BuildCorePrompts` 在 battle/start-battle task 之前优先公开 `ORDER_TRIGGERS` window。
- snapshot / prompt trigger views 对对手 hidden face-down standby source 做 viewer-level redaction：`sourceObjectId = HIDDEN`、`effectKind = HIDDEN`、`sourceVisibility = HIDDEN`。
- 新增 battle initial stack 代表测试，覆盖进入 `ORDER_TRIGGERS` 后再进入 `STACK_PRIORITY`。

4C-1 覆盖矩阵 / 文档改动：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 新增 `stage4CBatch1TriggerOrdering` 顶层 overlay。
- 67 个 `ORDER_TRIGGERS` dependency functional units 增加 `stage4C1` overlay。
- Top20 中 6 个 FU 的 `ORDER_TRIGGERS` / battle initial stack blocker 被部分降低，但 full-official upgrades 仍为 0。
- 新增 `docs/CURRENT_STAGE4C_BATCH1_TRIGGER_ORDERING_EVIDENCE.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH1_TRIGGER_ORDERING_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`。

4C-1 A 复核命令：

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：通过。
- `git diff --check`：通过。
- 结构抽检：snapshot entries = 1009、functional units = 811、`stage4C1` tagged = 67、full-official upgrades = 0、`stage4CBatch1TriggerOrdering` 存在。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过，3337/3337。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过；仅保留 SignalR / Rollup `PURE` 注释提示。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过，覆盖首页、lobby、decks、cards、room、match、result smoke 页面。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api`：通过，player-a / player-b 双上下文均 OK；尾部 499/143 与 allocator 记录来自脚本关停，不作为阻断。

4C-1 关闭的 P0 子项：

- `ORDER_TRIGGERS` 保守 APNAP controller-block runtime 子集。
- `orderedTriggerIds` 作为服务端可提交默认合法顺序的 prompt candidate 语义。
- 跨控制者非法重排拒绝且失败命令不改变 authoritative state。
- battle initial stack 代表触发排序窗口。
- hidden face-down standby trigger source redaction。

4C-1 仍保留 P0/P1：

- 完整 trigger engine、真实卡牌全触发生成、完整 effect resolution 尚未完成。
- trigger payment / decline / payment failure 尚未完成。
- 完整 APNAP 多玩家独立排序、battle initial stack full official matrix、FAQ adjudication 仍待 4C/4D。
- 1009 entries / 811 functional units full-official 覆盖仍未完成。
- 正式 18 步 E2E、completion audit、P0/P1 清零证明仍未完成。

4C-1 结论：**通过**。没有新增 P0/P1，没有前端本地规则裁决路径，没有发现 hidden information 泄漏；允许继续阶段 4C 下一批。下一批建议优先做完整 trigger engine + real card-trigger enqueue，然后处理 trigger payment / decline / payment failure。项目整体仍 **NOT READY**。

## 0.12 阶段 4C-2 Real Card-Trigger Enqueue 第一小批

阶段 4C-2 继续按 functional unit / engine blocker 小批推进。本批只把 `Watchful Sentinel` / `OGN·096/298` / `FU-67568b793d` 的多真实遗言触发代表路径接入服务端权威触发队列，不扩展到所有 last-breath / destroyed / attack / conquer 触发族，不进入 1009 full-official 覆盖。项目整体仍 **NOT READY**。

4C-2 服务端改动：

- `CoreRuleEngine` 在真实 `UNIT_DESTROYED` 路径中，当多个 `Watchful Sentinel` 遗言抽牌触发同时产生时，改为生成 `TriggerQueue`。
- 多触发路径进入 `ORDER_TRIGGERS prompt -> StackItems -> pass priority -> TRIGGER_RESOLVED / CARD_DRAWN`。
- prompt metadata 的 APNAP 默认 `orderedTriggerIds` 可直接提交并 accepted。
- 非法跨控制者排序 rejected，且失败命令不改变 authoritative state。
- 单个 `Watchful Sentinel` 遗言继续保留旧即时结算兼容；本批不把它宣称为统一单触发策略。
- 新增 `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 覆盖真实卡牌事件入队、排序、非法排序无副作用和栈结算。

4C-2 覆盖矩阵 / 文档改动：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 新增 `stage4CBatch2RealTriggerEnqueue` 顶层 overlay。
- 确认 `OGN·096/298` 对应 `FU-67568b793d`，仅该 FU 添加 `stage4C2` overlay。
- overlay status：`REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。
- 新增 `docs/CURRENT_STAGE4C_BATCH2_REAL_TRIGGER_ENQUEUE_EVIDENCE.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH2_REAL_TRIGGER_ENQUEUE_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`。

4C-2 A 复核命令：

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~OrderTriggers|FullyQualifiedName~WatchfulSentinel"`：通过，11/11。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过，3338/3338。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过；仅保留 SignalR / Rollup `PURE` 注释提示。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api`：通过，player-a / player-b 双上下文均 OK；尾部 499/143、allocator 与连接 abort 记录来自脚本关停，不作为阻断。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：通过。
- 结构抽检：snapshot entries = 1009、functional units = 811、`stage4CBatch2RealTriggerEnqueue` 存在、`stage4C2` verified FU = `FU-67568b793d`、full-official upgrades = 0、full-official uncovered = 811。
- `git diff --check`：通过。

4C-2 关闭的 P0 子项：

- 真实 `UNIT_DESTROYED` 事件生成 `Watchful Sentinel` 多触发 `TriggerQueue` 的代表路径。
- 真实触发经 `ORDER_TRIGGERS` 排序后进入 stack 并由 priority pass 结算。
- 真实触发非法跨控制者排序 no-mutation 拒绝。

4C-2 仍保留 P0/P1：

- 完整 trigger engine 尚未完成。
- `Watchful Sentinel` 之外的 last-breath / friendly-destroyed / on-play registered trigger / attack / defense / conquer 触发族仍未统一入队。
- state-based cleanup trigger enqueue、trigger payment / decline / payment failure、完整 effect resolution、FAQ adjudication 仍待后续批次。
- 1009 entries / 811 functional units full-official 覆盖、正式 18 步 E2E、completion audit 仍未完成。

4C-2 结论：**通过**。没有新增 P0/P1，没有前端本地规则裁决路径，没有发现 hidden information 泄漏；允许继续阶段 4C 下一批。下一批建议扩展 last-breath / destroyed-family real enqueue，但仍按小批、逐 FU、逐测试推进。项目整体仍 **NOT READY**。

## 0.13 阶段 4C-3 Honest Broker Last-Breath Enqueue 小批

阶段 4C-3 继续扩展 last-breath / destroyed-family real enqueue，但仍保持小批范围。本批只把 `Honest Broker` / `SFD·155/221` / `FU-3acf92c924` 的多真实遗言金币触发接入服务端权威触发队列，不宣称完整 trigger engine、不进入 1009 full-official 覆盖。项目整体仍 **NOT READY**。

4C-3 服务端改动：

- `HonestBrokerCardNo` / `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` 扩展到真实多触发路径：`UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EQUIPMENT_TOKEN_CREATED`。
- 多控制者真实 last-breath 触发沿用 4C-1 APNAP 默认 `orderedTriggerIds`，可直接提交并 accepted。
- 非法跨控制者排序 rejected，且失败命令不改变 authoritative state。
- 与 4C-2 合并后，`Watchful Sentinel` 与 `Honest Broker` 两条 last-breath 代表路径已有 real enqueue 证据。
- 单触发 `Watchful Sentinel` / `Honest Broker` 仍保留即时结算兼容；本批不宣称统一单触发策略完成。

4C-3 覆盖矩阵 / 文档改动：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 新增 `stage4CBatch3LastBreathEnqueue` 顶层 overlay。
- 确认 `SFD·155/221` 对应 `FU-3acf92c924`，仅该 FU 添加 `stage4C3` overlay。
- 未回退 4C-2 `FU-67568b793d` overlay；累计 real-trigger enqueue verified FUs = 2。
- 新增 `docs/CURRENT_STAGE4C_BATCH3_LAST_BREATH_ENQUEUE_EVIDENCE.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH3_LAST_BREATH_ENQUEUE_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`。

4C-3 A 复核命令：

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~OrderTriggers|FullyQualifiedName~WatchfulSentinel|FullyQualifiedName~HonestBroker"`：通过，13/13。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过，3339/3339。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过；仅保留 SignalR / Rollup `PURE` 注释提示。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api`：通过，player-a / player-b 双上下文均 OK；尾部 499/143、allocator、Chrome WebApp 和 mojo 记录来自脚本关停 / 本地 Chrome 噪声，不作为阻断。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：通过。
- 结构抽检：snapshot entries = 1009、functional units = 811、`stage4CBatch3LastBreathEnqueue` 存在、`stage4C2` verified FU = `FU-67568b793d`、`stage4C3` verified FU = `FU-3acf92c924`、full-official upgrades = 0。
- `git diff --check`：通过。

4C-3 关闭的 P0 子项：

- `Honest Broker` 遗言金币真实多触发排序 / 入栈 / 结算代表路径。
- Watchful + Honest Broker 两条 last-breath 代表路径具备 real enqueue 证据。
- 多控制者真实 last-breath APNAP 默认排序 accepted；非法跨控制者排序 no-mutation 拒绝。

4C-3 仍保留 P0/P1：

- 完整 trigger engine 尚未完成。
- 其他 destroyed-family、state-based cleanup trigger enqueue、trigger payment / decline / payment failure、完整 effect resolution、FAQ adjudication 仍待后续批次。
- 1009 entries / 811 functional units full-official 覆盖、正式 18 步 E2E、completion audit 仍未完成。
- 正式 trigger DTO / 解释字段 / 单触发兼容策略文档化仍为 P1。

4C-3 结论：**通过**。没有新增 P0/P1，没有前端本地规则裁决路径，没有发现 hidden information 泄漏；允许继续阶段 4C 下一批。下一批建议继续扩展 destroyed-family 或进入 trigger payment / decline，但必须继续逐 FU、逐测试推进。项目整体仍 **NOT READY**。

## 0.14 阶段 4C-4 Treasure Pile Trigger Payment 小批

阶段 4C-4 进入 trigger payment / decline / payment failure 的最小真实卡牌切片。本批只把 `SFD·220/221`《珍宝堆》/ `FU-4694e33f45` 的征服触发支付窗口服务端官方化，不宣称完整 PaymentEngine、不进入 1009 full-official 覆盖。项目整体仍 **NOT READY**。

4C-4 服务端 / 前端改动：

- `CoreRuleEngine` 将 `BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD` 从自动支付并自动创建金币，改为打开 `PendingPaymentState`：`paymentWindow = TRIGGER_PAYMENT`。
- 现有 `PAY_COST` command 复用于触发支付窗口；合法 choice 只有 `SPEND_MANA:1` 和 `DECLINE`。
- `SPEND_MANA:1` 路径会扣 1 点法力、创建休眠“金币”装备指示物、关闭窗口，并写入 `COST_PAID`、`BATTLEFIELD_TRIGGER_RESOLVED`、`EQUIPMENT_TOKEN_CREATED`、`PAYMENT_WINDOW_CLOSED`。
- `DECLINE` 路径会关闭窗口且不扣费、不创建指示物，并写入 `TRIGGER_PAYMENT_DECLINED`、`PAYMENT_WINDOW_CLOSED`。
- wrong player、stale prompt、unknown choice、duplicate choice、pay+decline、malformed payload、insufficient mana 都已覆盖拒绝 / no mutation。
- 前端只补 `PAYMENT_WINDOW_OPENED`、`TRIGGER_PAYMENT_DECLINED` 的中文事件 label，不计算支付合法性或触发结算。

4C-4 覆盖矩阵 / 文档改动：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 新增 `stage4CBatch4TriggerPayment` 顶层 overlay。
- 确认 `SFD·220/221` 对应 `FU-4694e33f45`，仅该 FU 添加 `stage4C4` overlay。
- overlay status：`TRIGGER_PAYMENT_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。
- 新增 `docs/CURRENT_STAGE4C_BATCH4_TRIGGER_PAYMENT_EVIDENCE.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH4_TRIGGER_PAYMENT_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`。

4C-4 A 复核命令：

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TriggerPayment|FullyQualifiedName~BattlefieldConquerGold|FullyQualifiedName~PayCost"`：通过，11/11。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~OrderTriggers|FullyQualifiedName~HonestBroker|FullyQualifiedName~WatchfulSentinel"`：通过，13/13。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过，3344/3344。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过；仅保留 SignalR / Rollup `PURE` 注释提示。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api`：第一次与 smoke 并行抢占 API 端口失败；sequential rerun 通过，player-a / player-b 双上下文均 OK；尾部 499/143 和 allocator 记录来自脚本关停 / 本地 Chrome 噪声，不作为阻断。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：通过。
- 结构抽检：snapshot entries = 1009、functional units = 811、`stage4CBatch4TriggerPayment` 存在、`stage4C4` verified FU = `FU-4694e33f45`、full-official upgrades = 0。
- `git diff --check`：通过。

4C-4 关闭的 P0 子项：

- `SFD·220/221`《珍宝堆》征服触发支付 / 拒付代表路径。
- 触发支付 choice 校验和支付失败 no-mutation 代表路径。
- Hub stale prompt 对 trigger payment 拒绝且不广播突变。

4C-4 仍保留 P0/P1：

- 完整 PaymentEngine 尚未完成。
- `SFD·220/221` 之外的 triggered-cost functional units 未完成。
- 完整 trigger engine、state-based cleanup trigger enqueue、完整 effect resolution、FAQ adjudication 仍待后续批次。
- 1009 entries / 811 functional units full-official 覆盖、正式 18 步 E2E、completion audit 仍未完成。
- `TRIGGER_PAYMENT` 长期 DTO / UX 契约冻结仍为 P1。

4C-4 结论：**通过**。没有新增 P0/P1，没有前端本地规则裁决路径，没有发现 hidden information 泄漏；允许继续阶段 4C 下一批。下一批建议优先 state-based cleanup trigger enqueue 或继续扩展 triggered-cost payment windows，但仍必须逐 FU、逐测试推进。项目整体仍 **NOT READY**。

## 0.15 阶段 4C-5 State-Based Cleanup Trigger Enqueue 小批

阶段 4C-5 进入 state-based cleanup -> trigger enqueue 的最小真实卡牌切片。本批只把可见、非 face-down、非 standby 的 `OGN·096/298`《警觉的哨兵》接入 state-based cleanup `LETHAL_DAMAGE` 触发入队代表路径，不宣称完整 trigger engine、不进入 1009 full-official 覆盖。项目整体仍 **NOT READY**。

4C-5 服务端改动：

- `OGN·029/298`《星落》造成致命伤害后，state-based cleanup `LETHAL_DAMAGE` 可摧毁两名可见 Watchful Sentinel。
- 两名 Watchful Sentinel 的绝念抽牌触发串成 `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- hidden / standby Watchful Sentinel 不入队，避免 trigger metadata 泄漏不可见或待命来源。
- 本批不扩完整 trigger engine，不授予 full-official，不覆盖其他 destroyed / last-breath / friendly-destroyed functional units。

4C-5 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH5_STATE_CLEANUP_TRIGGER_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- 本批 D 未修改服务端、前端、覆盖矩阵 JSON、coverage baseline、risk top20、stage4B freeze 或 `riftbound-dotnet.sln`。

4C-5 A 复核命令：

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests"`：通过，4/4。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过，3346/3346。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过；只有既有 SignalR / Rollup `PURE` 注释警告。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api`：通过。
- `git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`：通过。

4C-5 关闭的 P0 子项：

- state-based cleanup `LETHAL_DAMAGE` -> visible Watchful Sentinel last-breath enqueue representative。
- hidden / standby Watchful Sentinel 不入队的 metadata 泄漏护栏。

4C-5 仍保留 P0/P1：

- 完整 trigger engine。
- 其他 destroyed / last-breath / friendly-destroyed functional units。
- 隐藏 / face-down 原始触发建模。
- 完整 effect resolution、FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18 步 E2E、completion audit 仍未完成。

4C-5 结论：**通过**。没有新增 P0/P1，没有前端本地规则裁决路径，没有把 hidden / standby Watchful 误入队；允许继续阶段 4C 下一批。下一批建议继续扩展其他 destroyed-family 或 hidden / face-down trigger policy，但仍必须逐 FU、逐测试推进。项目整体仍 **NOT READY**。

## 0.16 阶段 4C-6 Honest Broker Cleanup Trigger Enqueue 小批

阶段 4C-6 继续推进 state-based cleanup -> trigger enqueue 的最小真实卡牌切片。本批只把可见、非 face-down、非 standby 的 `SFD·155/221`《诚实掮客》接入 state-based cleanup `LETHAL_DAMAGE` 触发入队代表路径，不改协议或前端，不宣称完整 trigger engine，不进入 1009 full-official 覆盖。项目整体仍 **NOT READY**。

4C-6 服务端改动：

- `OGN·029/298`《星落》造成致命伤害后，state-based cleanup `LETHAL_DAMAGE` 可摧毁两个可见 Honest Broker。
- 两个 Honest Broker 的绝念金币触发串成 `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED`。
- hidden / standby Honest Broker 不入队、不创建 token，避免 trigger metadata 泄漏不可见或待命来源。
- 本批不扩完整 trigger engine，不授予 full-official，不覆盖其他 destroyed / last-breath / friendly-destroyed functional units。

4C-6 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH6_HONEST_CLEANUP_TRIGGER_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- 本批 D 未修改服务端、前端、覆盖矩阵 JSON、coverage baseline、risk top20、stage4B freeze 或 `riftbound-dotnet.sln`。

4C-6 A 复核命令：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter FullyQualifiedName~RealTriggerQueueTests`：通过，6/6。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过，3348/3348。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过；只有既有 SignalR / Rollup `PURE` 注释警告。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api`：通过。
- `git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`：通过。

4C-6 关闭的 P0 子项：

- state-based cleanup `LETHAL_DAMAGE` -> visible Honest Broker last-breath enqueue representative。
- hidden / standby Honest Broker 不入队、不创建 token 的 metadata 泄漏护栏。

4C-6 仍保留 P0/P1：

- 完整 trigger engine。
- 其他 destroyed / last-breath / friendly-destroyed functional units。
- hidden / face-down 原始触发建模。
- 完整 effect resolution、FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18 步 E2E、completion audit 仍未完成。

4C-6 结论：**通过**。没有新增 P0/P1，没有协议或前端改动，没有把 hidden / standby Honest Broker 误入队或误创建 token；允许继续阶段 4C 下一批。下一批建议继续扩展其他 destroyed-family 或 hidden / face-down trigger policy，但仍必须逐 FU、逐测试推进。项目整体仍 **NOT READY**。

## 0.17 阶段 4C-7 Scouting Warhawk Explicit Destroy Trigger Enqueue 小批

阶段 4C-7 继续按 functional unit / engine blocker 小批推进。本批只把 `Scouting Warhawk` / `OGN·216/298` / `FU-0500c77a70` 的 explicit destroy last-breath 召符文代表路径接入服务端权威触发队列；支撑摧毁来源为 `Spirit Fire` / `OGN·256/298`，不是 state cleanup。项目整体仍 **NOT READY**。

4C-7 服务端改动：

- explicit destroy `UNIT_DESTROYED` 可识别可见 Scouting Warhawk 的 `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1`。
- 官方化路径串成：explicit destroy `UNIT_DESTROYED` -> visible Scouting Warhawk trigger -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk 不入队、不显示 prompt metadata、不触发 `RUNES_CALLED`。
- single-trigger compatibility 保留，既有 `P79ScoutingWarhawk` 测试继续通过。
- 本批没有协议或前端字段变化，不进入 1009 full-official，不宣称完整 trigger engine。

4C-7 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH7_SCOUTING_WARHAWK_TRIGGER_AUDIT.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH7_SCOUTING_WARHAWK_TRIGGER_EVIDENCE.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`，记录 `FU-0500c77a70` 的 `stage4C7` overlay。
- 矩阵口径保持 1009 snapshot entries / 811 functional units；`stage4C7` tagged FUs = 1；`fullOfficialUpgrades = 0`。
- 本批没有服务端协议或前端改动，不触碰 `riftbound-dotnet.sln`。

4C-7 A 复核命令：

- focused：通过，9/9。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过，3350/3350。
- frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：passed。
- 矩阵结构断言：1009 / 811 不变，`stage4C7` 仅 `FU-0500c77a70`，Warhawk `fullOfficial=false`，`fullOfficialUpgrades=0`。

4C-7 关闭的 P0 子项：

- Scouting Warhawk explicit destroy real trigger enqueue representative。
- hidden / face-down / standby Warhawk 不入队、不显示 prompt metadata、不触发 `RUNES_CALLED` 的信息泄漏护栏。

4C-7 仍保留 P0/P1：

- 完整 trigger engine。
- state cleanup Warhawk（4C-8 后续已补代表路径；不再作为当前独立 P0 子项）。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- hidden / face-down 原始触发建模。
- FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-7 结论：**通过**。没有新增 P0/P1，没有协议或前端字段变化，没有把 hidden / face-down / standby Warhawk 误入队或误触发 `RUNES_CALLED`；允许继续阶段 4C 下一批。阶段 4C 仍在逐 FU、逐测试批量推进；项目整体仍 **NOT READY**。

## 0.18 阶段 4C-8 Scouting Warhawk Cleanup Trigger Enqueue 小批

阶段 4C-8 继续按 functional unit / engine blocker 小批推进。本批只把 `Scouting Warhawk` / `OGN·216/298` / `FU-0500c77a70` 的 state-based cleanup lethal damage 召符文代表路径接入服务端权威触发队列；支撑来源为 `Starfall` / `OGN·029/298` 造成 lethal damage 后的 state-based cleanup，不是 explicit destroy source 的新增覆盖。项目整体仍 **NOT READY**。

4C-8 服务端改动：

- Starfall lethal damage 后的 state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` 可识别可见 Scouting Warhawk 的 `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1`。
- 官方化路径串成：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible Scouting Warhawk trigger -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk cleanup 路径不入队、不显示 prompt metadata、不触发 `RUNES_CALLED`。
- 4C-7 explicit destroy 路径和 single-trigger compatibility 保留。
- 本批没有协议或前端字段变化，不进入 1009 full-official，不宣称完整 trigger engine。

4C-8 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH8_SCOUTING_WARHAWK_CLEANUP_TRIGGER_AUDIT.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH8_SCOUTING_WARHAWK_CLEANUP_TRIGGER_EVIDENCE.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`，记录 `FU-0500c77a70` 的 `stage4C8` cleanup overlay，同时保留 `stage4C7` explicit destroy overlay。
- 矩阵口径保持 1009 snapshot entries / 811 functional units；`stage4C8` tagged FUs = 1；cumulative state-based cleanup trigger enqueue verified FUs = 3；`fullOfficialUpgrades = 0`。
- 本批没有服务端协议或前端改动，不触碰 `riftbound-dotnet.sln`。

4C-8 A 复核命令：

- focused：通过，11/11。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过，3352/3352。
- frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：passed。
- 矩阵结构断言：1009 / 811 不变，`stage4C7` 与 `stage4C8` 均只标记 `FU-0500c77a70`，Warhawk `fullOfficial=false`，`fullOfficialUpgrades=0`。

4C-8 关闭的 P0 子项：

- Scouting Warhawk state-based cleanup lethal damage real trigger enqueue representative。
- hidden / face-down / standby Warhawk cleanup 路径不入队、不显示 prompt metadata、不触发 `RUNES_CALLED` 的信息泄漏护栏。
- 4C-7 explicit destroy Warhawk 路径继续保持。

4C-8 仍保留 P0/P1：

- 完整 trigger engine。
- Sad / Loyal Poro（4C-9 后续已补 state-based cleanup 条件抽牌代表路径；不再作为当前独立 P0 子项）。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- hidden / face-down 原始触发建模。
- FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-8 结论：**通过**。没有新增 P0/P1，没有协议或前端字段变化，没有把 hidden / face-down / standby Warhawk cleanup 路径误入队或误触发 `RUNES_CALLED`；允许继续阶段 4C 下一批。阶段 4C 仍在逐 FU、逐测试批量推进；项目整体仍 **NOT READY**。

## 0.19 阶段 4C-9 Poro Cleanup Trigger Enqueue 小批

阶段 4C-9 继续按 functional unit / engine blocker 小批推进。本批只把 Sad / Loyal Poro 条件抽牌的 state-based cleanup lethal damage 代表路径接入服务端权威触发队列；支撑来源为 `Starfall` / `OGN·029/298` 造成 lethal damage 后的 state-based cleanup。项目整体仍 **NOT READY**。

4C-9 服务端改动：

- 覆盖 `FU-f8bfd5c6f9` / `SFD·036/221` Sad Poro / `SAD_PORO_LAST_BREATH_DRAW_1`。
- 覆盖 `FU-938b749c23` / `UNL-221/219` Sad Poro / `SAD_PORO_LAST_BREATH_DRAW_1`。
- 覆盖 `FU-0415e3b46d` / `UNL-156/219` Loyal Poro / `LOYAL_PORO_LAST_BREATH_DRAW_1`。
- 官方化路径串成：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Poro condition -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- Sad 条件：base-zone、visible、非 face-down、非 standby，且同位置无其他友方正面非待命单位时触发。
- Loyal 条件：base-zone、visible、非 face-down、非 standby，且同位置有至少一个其他友方正面非待命单位，并且该友方不在本轮 cleanup removal set 中时触发。
- hidden / face-down / standby Poro cleanup 路径不入队、不显示 prompt metadata、不抽牌。
- 同位置其他友方也同时被 cleanup 摧毁的落单判定未官方化；runtime 对 Loyal 采取保守不入队，本批不宣称完整规则。
- 现有 explicit destroy P79 Sad / Loyal immediate compatibility 保留。
- 本批没有协议或前端字段变化，不进入 1009 full-official，不宣称完整 trigger engine。

4C-9 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH9_PORO_CLEANUP_TRIGGER_AUDIT.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH9_PORO_CLEANUP_TRIGGER_EVIDENCE.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`，记录 3 个 Poro FU 的 `stage4C9` cleanup overlay。
- 矩阵口径保持 1009 snapshot entries / 811 functional units；`stage4C9` tagged FUs = 3；cumulative real-trigger enqueue verified FUs = 6；cumulative state-based cleanup trigger enqueue verified FUs = 6；`fullOfficialUpgrades = 0`。
- 本批没有服务端协议或前端改动，不触碰 `riftbound-dotnet.sln`。

4C-9 A 复核命令：

- focused：通过，21/21。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过，3358/3358。
- frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：passed。
- 矩阵结构断言：1009 / 811 不变，`stage4C9` 仅标记 `FU-f8bfd5c6f9`、`FU-938b749c23`、`FU-0415e3b46d`，三者 `fullOfficial=false`，`fullOfficialUpgrades=0`。

4C-9 关闭的 P0 子项：

- Sad Poro / `SFD·036/221` state-based cleanup 条件抽牌 real trigger enqueue representative。
- Sad Poro / `UNL-221/219` state-based cleanup 条件抽牌 real trigger enqueue representative。
- Loyal Poro / `UNL-156/219` state-based cleanup 条件抽牌 real trigger enqueue representative。
- hidden / face-down / standby Poro cleanup 路径不入队、不显示 prompt metadata、不抽牌的信息泄漏护栏。

4C-9 仍保留 P0/P1：

- 完整 trigger engine。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- battlefield objectLocation Poro condition matrix。
- simultaneous cleanup condition timing，尤其同位置其他友方也同时被 cleanup 摧毁时的 Sad / Loyal 判定。
- hidden / face-down 原始触发建模。
- FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-9 结论：**通过**。没有新增 P0/P1，没有协议或前端字段变化，没有把 hidden / face-down / standby Poro cleanup 路径误入队或误抽牌；允许继续阶段 4C 下一批。阶段 4C 仍在逐 FU、逐测试批量推进；项目整体仍 **NOT READY**。

## 0.20 阶段 4C-10 Unsung Hero Cleanup Trigger Enqueue 小批

阶段 4C-10 继续按 functional unit / engine blocker 小批推进。本批只把 Unsung Hero / `SFD·167/221` / `FU-1701d1d89a` 的 state-based cleanup powerful last-breath draw-2 代表路径接入服务端权威触发队列；支撑来源为 `Starfall` / `OGN·029/298` 造成 lethal damage 后的 state-based cleanup。项目整体仍 **NOT READY**。

4C-10 服务端改动：

- 覆盖 `FU-1701d1d89a` / `SFD·167/221` Unsung Hero / `UNSUNG_HERO_LAST_BREATH_DRAW_2_IF_POWERFUL`。
- 官方化路径串成：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Unsung Hero power >= 5 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN` x2。
- power < 5 cleanup 路径不入队、不抽牌。
- hidden / face-down / standby Unsung Hero cleanup 路径不入队、不显示 prompt metadata、不抽牌。
- 现有 explicit destroy P79 Unsung immediate compatibility 保留。
- 严格边界：本批只用 `CardObjectState.Power >= 5` 代表强力；不覆盖 LayerEngine / effective power / temporary modifier；不覆盖 battlefield objectLocation 全矩阵；不迁移 explicit destroy。
- 本批没有协议或前端字段变化，不进入 1009 full-official，不宣称完整 trigger engine。

4C-10 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH10_UNSUNG_HERO_CLEANUP_TRIGGER_AUDIT.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH10_UNSUNG_HERO_CLEANUP_TRIGGER_EVIDENCE.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`。
- 本批不修改前端运行时代码或 `riftbound-dotnet.sln`。

4C-10 A 复核命令：

- focused：通过，21/21。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过，3361/3361。
- frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：passed。
- 4C-10 矩阵结构断言：passed；`stage4C10` tagged FU = 1（`FU-1701d1d89a`），cumulative real trigger enqueue FUs = 7，cumulative state-based cleanup trigger enqueue FUs = 7，`fullOfficialUpgrades = 0`，1009 / 811 冻结计数不变。
- false-completion 窄断言：passed；未发现候选就绪、正式 E2E 已通过或 `fullOfficial: true` 误宣称。

4C-10 关闭的 P0 子项：

- Unsung Hero / `SFD·167/221` state-based cleanup powerful last-breath draw-2 real trigger enqueue representative。
- power < 5 cleanup 路径不入队、不抽牌的阈值护栏。
- hidden / face-down / standby Unsung Hero cleanup 路径不入队、不显示 prompt metadata、不抽牌的信息泄漏护栏。

4C-10 仍保留 P0/P1：

- 完整 trigger engine。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- effective power / LayerEngine、temporary modifier 和完整强力判定。
- battlefield objectLocation matrix。
- hidden / face-down 原始触发建模。
- FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-10 结论：**通过**。没有新增 P0/P1，没有协议或前端字段变化，没有把 power < 5 或 hidden / face-down / standby Unsung Hero cleanup 路径误入队或误抽牌；允许继续阶段 4C 下一批。阶段 4C 仍在逐 FU、逐测试批量推进；项目整体仍 **NOT READY**。

## 0.21 阶段 4C-11 Ghostly Centaur Cleanup Trigger Enqueue 小批

阶段 4C-11 继续按 functional unit / engine blocker 小批推进。本批只把 Ghostly Centaur / `UNL-068/219` / `FU-0f2c4a3ea5` 的 friendly-destroyed power 代表路径接入 state-based cleanup 后的服务端权威触发队列；支撑来源为 `Starfall` / `OGN·029/298` 造成 lethal damage 后的 state-based cleanup。项目整体仍 **NOT READY**。

4C-11 服务端改动：

- 覆盖 `FU-0f2c4a3ea5` / `UNL-068/219` Ghostly Centaur / 幽魂半人马 friendly-destroyed power 最小真实触发队列切片。
- 官方化路径串成：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Ghostly source -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `POWER_MODIFIED_UNTIL_END_OF_TURN` +2。
- hidden / face-down / standby / opponent-controlled source 不入队、不显示 prompt metadata、不加战力。
- source 同时在本轮 cleanup removal set 中时保守不入队；本批不裁定完整同时死亡触发次数。
- 同一 Ghostly source 在同一个 cleanup pass 中最多入队一次，这是 4C-11 保守边界，不代表完整规则。
- 真实 stack destruction Ghostly 旧 P79 immediate compatibility 保留，未迁移到 `TriggerQueue`；这是 4C-11 当时 P1，4C-13 后已迁移关闭。
- 本批不覆盖 Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent，不进入 full-official，不宣称完整 trigger engine。

4C-11 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH11_GHOSTLY_CENTAUR_CLEANUP_TRIGGER_AUDIT.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH11_GHOSTLY_CENTAUR_CLEANUP_TRIGGER_EVIDENCE.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`。
- 本批不修改前端运行时代码或 `riftbound-dotnet.sln`。

4C-11 A 复核命令：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79GhostlyCentaurGainsTemporaryPowerWhenAnotherFriendlyUnitDestroyed"` 通过，23/23。
- `git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`：passed。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3364/3364。
- A frontend build：passed。
- A Chrome smoke：passed。
- A Stage 3 preflight：passed。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：passed。
- 4C-11 矩阵结构断言：passed；`stage4C11` tagged FU = 1（`FU-0f2c4a3ea5`），cumulative real trigger enqueue FUs = 8，cumulative state-based cleanup trigger enqueue FUs = 8，`fullOfficialUpgrades = 0`，1009 / 811 冻结计数不变。
- false-completion 窄断言：passed；未发现候选就绪、正式 E2E 已通过或 `fullOfficial: true` 误宣称。

4C-11 关闭的 P0 子项：

- Ghostly Centaur / `UNL-068/219` friendly-destroyed power state-based cleanup real trigger enqueue representative。
- hidden / face-down / standby / opponent-controlled Ghostly source 不入队、不显示 prompt metadata、不加战力的信息泄漏与控制权护栏。
- cleanup removal set 中 source 同时死亡时保守不入队、同一 cleanup pass 同一 source 最多入队一次的最小边界。

4C-11 仍保留 P0/P1：

- 完整 trigger engine。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent。
- 完整同时死亡触发次数、同一 cleanup pass 多对象时序、source 同时死亡时的官方裁定。
- hidden / face-down 原始触发建模。
- FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。
- 历史 P1（4C-13 后已关闭）：真实 stack destruction Ghostly 旧 P79 immediate compatibility 后续迁移到 `TriggerQueue`。

4C-11 结论：**通过**。没有新增 P0/P1，没有协议或前端字段变化，没有把 hidden / face-down / standby / opponent-controlled Ghostly source 误入队或误加战力；允许继续阶段 4C 下一批。阶段 4C 仍在逐 FU、逐测试批量推进；项目整体仍 **NOT READY**。

## 0.22 阶段 4C-12 Resonant Soul Cleanup Trigger Enqueue 小批

阶段 4C-12 继续按 functional unit / engine blocker 小批推进。本批只把 Resonant Soul / `OGN·118/298` / `FU-c146331876` 的 first-friendly-destroyed draw 代表路径接入 state-based cleanup 后的服务端权威触发队列；支撑来源为 `Starfall` / `OGN·029/298` 造成 lethal damage 后的 state-based cleanup。项目整体仍 **NOT READY**。

4C-12 服务端改动：

- 覆盖 `FU-c146331876` / `OGN·118/298` Resonant Soul / 残响之魂 first-friendly-destroyed draw 最小真实触发队列切片。
- 官方化路径串成：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Resonant Soul source，owner not already in `DestroyedUnitOwnerIdsThisTurn` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `CARD_DRAWN` 1。
- hidden / face-down / standby / opponent-controlled source 不入队、不显示 prompt metadata、不抽牌。
- source 同时在本轮 cleanup removal set 中时保守不入队；本批不裁定完整同时死亡触发次数。
- 每 owner 每 cleanup pass 只按首次 destroyed unit 生成本批 source set；同回合已经记录 destroyed owner 时不入队。
- true stack destruction Resonant 旧 P79 immediate compatibility 保留，未迁移到 `TriggerQueue`；cleanup 事件跳过旧 immediate helper 防重复；这是 4C-12 当时 P1，4C-13 后已迁移关闭。
- 本批不覆盖 Viktor / Ghostly 后续 / Kogmaw / Karthus / Undercover Agent，不进入 full-official，不宣称完整 trigger engine。

4C-12 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH12_RESONANT_SOUL_CLEANUP_TRIGGER_AUDIT.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH12_RESONANT_SOUL_CLEANUP_TRIGGER_EVIDENCE.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`。
- 本批不修改前端运行时代码或 `riftbound-dotnet.sln`。

4C-12 A 复核命令：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79ResonantSoulDrawsOnlyForFirstFriendlyUnitDestroyedEachTurn"` 通过，27/27。
- `git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`：passed。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3368/3368。
- A frontend build：passed。
- A Chrome smoke：passed。
- A Stage 3 preflight：passed。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：passed。
- 4C-12 矩阵结构断言：passed；`stage4C12` tagged FU = 1（`FU-c146331876`），cumulative real trigger enqueue FUs = 9，cumulative state-based cleanup trigger enqueue FUs = 9，`fullOfficialUpgrades = 0`，1009 / 811 冻结计数不变。
- false-completion 窄断言：passed；未发现候选就绪、正式 E2E 已通过或 `fullOfficial: true` 误宣称。

4C-12 关闭的 P0 子项：

- Resonant Soul / `OGN·118/298` first-friendly-destroyed draw state-based cleanup real trigger enqueue representative。
- hidden / face-down / standby / opponent-controlled Resonant Soul source 不入队、不显示 prompt metadata、不抽牌的信息泄漏与控制权护栏。
- cleanup removal set 中 source 同时死亡时保守不入队。
- 每 owner 每 cleanup pass 只按首次 destroyed unit 生成本批 source set；同回合已记录 destroyed owner 时不入队。

4C-12 仍保留 P0/P1：

- 完整 trigger engine。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- Viktor / Ghostly 后续 / Kogmaw / Karthus / Undercover Agent。
- 完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup pass 多对象排序、source 同时死亡时的官方裁定。
- hidden / face-down 原始触发建模。
- FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。
- 历史 P1（4C-13 后已关闭）：true stack destruction Resonant 旧 P79 immediate compatibility 后续迁移到 `TriggerQueue`。

4C-12 结论：**通过**。没有新增 P0/P1，没有协议或前端字段变化，没有把 hidden / face-down / standby / opponent-controlled Resonant Soul source 误入队或误抽牌；cleanup 路径跳过旧 immediate helper，避免重复触发；允许继续阶段 4C 下一批。阶段 4C 仍在逐 FU、逐测试批量推进；项目整体仍 **NOT READY**。

## 0.23 阶段 4C-13 Stack Destroyed Trigger Migration 小批

阶段 4C-13 继续按 functional unit / engine blocker 小批推进。本批不新增 FU；只迁移 / 关闭 4C-11 / 4C-12 留下的 P1：Ghostly Centaur + Resonant Soul true stack destruction 旧 immediate compatibility -> real trigger queue。项目整体仍 **NOT READY**。

4C-13 服务端改动：

- 覆盖 `FU-0f2c4a3ea5` / `UNL-068/219` Ghostly Centaur，以及 `FU-c146331876` / `OGN·118/298` Resonant Soul。
- 官方化路径串成：true stack destruction 非 cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> Ghostly `POWER_MODIFIED_UNTIL_END_OF_TURN` +2 / Resonant `CARD_DRAWN` 1。
- cleanup path 继续通过 `IsStateBasedCleanupDestroyedEvent` 排除旧 stack helper，避免重复入队。
- hidden / face-down / standby / opponent-controlled source 不入队；source 必须留场、正面、非 standby、同 controller。
- Resonant 继续尊重 `DestroyedUnitOwnerIdsThisTurn`。
- 旧 P79 tests 已更新为 queue / stack / priority 语义。
- 本批不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent，不进入 full-official，不宣称完整 trigger engine。

4C-13 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH13_STACK_DESTROYED_TRIGGER_MIGRATION_AUDIT.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH13_STACK_DESTROYED_TRIGGER_MIGRATION_EVIDENCE.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`。
- 本批不修改前端运行时代码或 `riftbound-dotnet.sln`。

4C-13 A 复核命令：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79GhostlyCentaur|FullyQualifiedName~P79ResonantSoul"` 通过，30/30。
- B full backend：passed，3370/3370。
- A backend full：passed，3370/3370。
- `git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`：passed。
- A frontend build：passed。
- A Chrome smoke：passed。
- A Stage 3 preflight：passed。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：passed。
- 4C-13 矩阵结构断言：passed；route-upgraded FUs = 2（`FU-0f2c4a3ea5`、`FU-c146331876`），unique new FU coverage = 0，cumulative real trigger enqueue FUs = 9，cumulative state-based cleanup trigger enqueue FUs = 9，`fullOfficialUpgrades = 0`，1009 / 811 冻结计数不变。
- false-completion 窄断言：passed；未发现候选就绪、正式 E2E 已通过或 `fullOfficial: true` 误宣称。

4C-13 关闭的 P1 / P0 子项：

- P1：Ghostly Centaur true stack destruction 旧 immediate compatibility -> real trigger queue / stack / priority 迁移。
- P1：Resonant Soul true stack destruction 旧 immediate compatibility -> real trigger queue / stack / priority 迁移。
- P0 子项：Ghostly Centaur 与 Resonant Soul 两个既有 FU 现在同时具备 cleanup representative 与 true stack destruction representative 的 real enqueue 证据。
- P0 子项：cleanup path 通过事件来源护栏避免旧 stack helper 重复入队。

4C-13 仍保留 P0/P1：

- 完整 trigger engine。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- Viktor / Kogmaw / Karthus / Undercover Agent。
- 完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup pass 多对象排序、source 同时死亡时的官方裁定。
- hidden / face-down 原始触发建模。
- FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

4C-13 结论：**通过**。没有新增 FU，没有新增 P0/P1，没有协议或前端字段变化；4C-11 / 4C-12 留下的 stack destruction immediate migration P1 已关闭。允许继续阶段 4C 下一批；阶段 4C 仍在逐 FU、逐测试批量推进；项目整体仍 **NOT READY**。

## 0.24 阶段 4C-14 Savage Jawfish Trigger Enqueue Baseline 小批

阶段 4C-14 继续按 functional unit / engine blocker 小批推进。本批新增一个 FU 的 real trigger enqueue baseline：Savage Jawfish / `UNL-129/219` / `FU-bd94334cc5`。项目整体仍 **NOT READY**，不得标记 READY / READY-CANDIDATE。

4C-14 服务端改动：

- 覆盖 `FU-bd94334cc5` / `UNL-129/219` Savage Jawfish / 凶残颚鱼。
- true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED` 均进入 `TriggerQueue`。
- 多触发走 `ORDER_TRIGGERS`；单触发 auto-stack。
- priority 双方 pass 后 `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED` +1。
- Guard：来源必须仍在场、face-up、non-standby、同 controller，不能是被摧毁对象 / cleanup removal set。
- hidden face-down / standby / opponent-controlled source 不 enqueue、不泄漏、不加经验。
- 旧 P79 Savage Jawfish fixture 已更新为 queue / stack / priority semantics。
- 明确边界：同一来源同一 cleanup / stack pass 多个友方被摧毁时，当前最小切片保守 cap 为每 source 每 pass 最多一次；这不是 full official trigger-count matrix，作为 P1 / TODO 保留。
- 本批不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent，不进入 full-official，不宣称完整 trigger engine。

4C-14 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH14_SAVAGE_JAWFISH_TRIGGER_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- D 本批不修改服务端 / 前端代码、E 矩阵 / evidence 文件或 `riftbound-dotnet.sln`。

4C-14 A 复核命令：

- Focused：`RealTriggerQueueTests|P79SavageJawfish` 通过，33/33。
- Backend full：passed，3374/3374。
- Frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。

4C-14 关闭的 P0 子项：

- Savage Jawfish / `UNL-129/219` trigger enqueue baseline。
- true stack `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` 或 single-trigger auto-stack -> `StackItems` -> priority -> `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED` +1。
- Starfall lethal state-based cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` 或 single-trigger auto-stack -> `StackItems` -> priority -> `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED` +1。
- hidden face-down / standby / opponent-controlled source 不入队、不泄漏、不加经验；source 不能是 destroyed object / cleanup removal set。

4C-14 仍保留 P0/P1：

- 完整 trigger engine。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- Viktor / Kogmaw / Karthus / Undercover Agent。
- 完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup / stack pass 多对象排序、source 同时死亡时的官方裁定。
- Savage Jawfish 同一来源同一 pass 多个友方被摧毁时的 full official trigger-count matrix。
- hidden / face-down 原始触发建模。
- FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

4C-14 结论：**通过**。没有协议或前端字段变化；本批新增 1 个 FU 的代表性 real enqueue baseline，但 `fullOfficialUpgrades=0`，不代表 full official，不代表 READY-CANDIDATE。允许继续阶段 4C 下一批；阶段 4C 仍在逐 FU、逐测试批量推进；项目整体仍 **NOT READY**。

## 0.25 阶段 4C-15 Viktor Feasibility Blocker

阶段 4C-15 候选为 Viktor destroyed non-minion token trigger / `FU-b5cb36a5c9`。B 本轮只做只读可行性检查，未修改代码，未新增测试。D 本轮只更新文档。4C-14 Savage Jawfish 已 checkpoint：`2deef64 checkpoint: complete stage 4C savage jawfish trigger batch`。

4C-15 阻断原因：

- 当前 `CardObjectTags` 没有 `Minion` / `随从` / subtype 字段。
- 当前 `CardObjectState` 没有稳定 token family / subtype / `isMinion` 字段。
- 多个“随从”创建路径经 `CreateBaseUnitTokens` 只落成 `CARD_TYPE:UNIT`，不保留 `cardNo` / `tokenName` / `TokenFamilyName`。
- 摧毁时无法可靠区分“随从单位”和普通单位。
- Viktor fixtures 当前也描述 destroyed-listener / non-minion filtering / minion-token path deferred。

4C-15 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH15_VIKTOR_BLOCKER.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- D 本批不修改服务端 / 前端代码、E 矩阵 / evidence 文件或 `riftbound-dotnet.sln`。

4C-15 结论：

- 不建议硬编码 Viktor 的“非随从”判定。
- 4C-15 未实现，未新增测试，不关闭 `FU-b5cb36a5c9`。
- 该项作为 P0/P1 blocker 记录，需要先冻结 token subtype / family 模型或由用户裁定官方解释。
- 后续建议：先做 `CardObjectState` subtype / token-family 模型和随从 token factory 统一写入，再做 Viktor；或者用户确认跳过 Viktor，改做不依赖“非随从”分类的下一个 safe FU。
- A 推荐路径：先做模型前置切片，再回到 Viktor；在用户确认前，不派发 Viktor 功能实现任务。

4C-15 仍保留 P0/P1：

- Viktor `FU-b5cb36a5c9` destroyed non-minion token trigger。
- token subtype / token-family / minion classification 模型。
- 完整 trigger engine。
- 其他 destroyed / last-breath / friendly-destroyed functional units。
- Kogmaw / Karthus / Undercover Agent。
- hidden / face-down 原始触发建模。
- FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-15 结论：**阻断记录完成，功能未关闭**。项目整体仍 **NOT READY**，不得标记 READY / READY-CANDIDATE；不得宣称 1009 / 811 full-official 或正式 E2E。

## 0.26 阶段 4C-15A Minion Token Family 前置模型切片

阶段 4C-15A 已由 B 完成，范围只限 token subtype / family / minion classification 最小前置模型；不实现 Viktor 本体。项目整体仍 **NOT READY**，不得标记 READY / READY-CANDIDATE。

4C-15A 服务端改动事实：

- 新增稳定 tag：`TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily`。
- `P6TokenFactoryCatalog` 的官方三种“随从”token factory（`OGN·271/298`、`OGN·272/298`、`OGN·273/298`）带该 tag。
- `CoreRuleEngine.CreateBaseUnitTokens` 对 `tokenName == "随从"` 自动追加 `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION`。
- Viktor legend 直接创建随从路径同步带 `TOKEN_FAMILY:MINION`。
- Common Cause、Future Forge、Faithful Craftsman、Vanguard Captain、Mechanical Trickster、Viktor legend、battlefield held minion 等可生成带 marker 的随从 token。
- 普通单位不带 marker；Gold / Sprite / Warhawk / Sand Soldier 等非“随从”token factory 不带 marker。
- hidden face-down standby 即使内部带 marker，对手 snapshot 仍不泄漏 tags / cardNo / power。

4C-15A 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH15A_MINION_TOKEN_FAMILY_AUDIT.md`。
- 追加更新 `docs/CURRENT_STAGE4C_BATCH15_VIKTOR_BLOCKER.md`，保留 4C-15 blocker 历史并记录 4C-15A 后续结果。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- D 本批不修改服务端 / 前端代码、E 矩阵 / evidence 文件或 `riftbound-dotnet.sln`。

4C-15A A 复核命令：

- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3375/3375。
- `git diff --check`：passed。

4C-15A 关闭的 P0/P1 子项：

- 4C-15 blocker 中“随从 token 没有稳定 family marker”的前置模型子项已部分关闭。
- `TOKEN_FAMILY:MINION` 可作为后续 Viktor 非随从过滤的服务端权威输入之一。

4C-15A 仍保留 P0/P1：

- Viktor `FU-b5cb36a5c9` destroyed non-minion trigger 本体。
- destroy / cleanup 入队时 destroyed target pre-removal state 判定。
- 完整 trigger engine。
- 其他 destroyed / last-breath / friendly-destroyed functional units。
- Kogmaw / Karthus / Undercover Agent。
- hidden / face-down 原始触发建模。
- FAQ regression。
- 1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-15A 结论：**通过前置模型切片，未关闭 Viktor 本体**。未改协议 record 字段，未改前端，不宣称 full-official，不宣称 READY-CANDIDATE；允许后续在明确 destroyed target pre-removal state 后回到 Viktor。

## 0.27 阶段 4C-15B Viktor Trigger Baseline

前置 commit：`034f1ed checkpoint: complete stage 4C minion token family baseline`。阶段 4C-15B 已由 B 完成 Viktor destroyed non-minion token trigger 最小官方化代表切片；范围不包括 Kogmaw / Karthus / Undercover，不包括完整 trigger engine，不授予 full-official。项目整体仍 **NOT READY**，不得标记 READY / READY-CANDIDATE。

4C-15B 服务端改动事实：

- 修改文件：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`。
- 目标 FU：`FU-b5cb36a5c9` / Viktor destroyed non-minion token trigger，覆盖 `ARC-006/006`、`OGN·246/298`、`OGN·246a/298`。
- visible surviving friendly Viktor source 看到另一名友方非随从单位被摧毁时触发。
- destroyed target 使用 pre-removal `CardObjectState` 判定：unit、same controller / friendly、not source、not `CardObjectTags.MinionTokenFamily`。
- source guard：Viktor still on field、face-up、non-standby、same controller、not cleanup removal set。
- 覆盖 true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED`。
- trigger path：`TriggerQueue` -> single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED`，在 controller base 创建 1-power Zaun minion `OGN·273/298`，并带 `TOKEN_FAMILY:MINION`。
- minion target 不入队、不造 token；hidden / face-down / standby / opponent source 不入队、不泄漏、不造 token；source also dying 不入队。

4C-15B 新增测试：

- `RealViktorDestroyedNonMinionTriggersAutoStackAndCreatesMinionToken`
- `StateBasedCleanupViktorDestroyedNonMinionTriggersAutoStackAndCreatesMinionToken`
- `ViktorDestroyedMinionTargetDoesNotEnqueueTrigger`
- `StateBasedCleanupInvalidViktorSourcesDoNotEnqueueOrLeak`
- `StateBasedCleanupViktorSkipsWhenSourceAlsoDies`

4C-15B 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH15B_VIKTOR_TRIGGER_AUDIT.md`。
- 追加更新 `docs/CURRENT_STAGE4C_BATCH15_VIKTOR_BLOCKER.md`，明确 4C-15A 已关闭模型前置，4C-15B 已实现代表性 baseline，但仍非 full-official。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- D 本批不修改服务端 / 前端代码、E 矩阵 / evidence 文件或 `riftbound-dotnet.sln`。

4C-15B A 复核命令：

- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3380/3380。
- `git diff --check`：B pending / expected passed；A 将在文档后再次复核。

4C-15B 关闭的 P0/P1 子项：

- Viktor `FU-b5cb36a5c9` destroyed non-minion trigger 的代表性 baseline：true stack 与 Starfall lethal state-based cleanup 两条 `UNIT_DESTROYED` 路径均可进入 `TriggerQueue` / single-trigger auto-stack / `StackItems` / priority / token effect。
- 4C-15A 模型前置已被 `TOKEN_FAMILY:MINION` 最小切片关闭；4C-15B 使用 pre-removal state 完成非随从 destroyed target 过滤代表路径。

4C-15B 仍保留 P0/P1：

- P1：same source same stack / cleanup pass multiple non-minion friendly deaths 的 full official trigger-count matrix 仍保守 one source once。
- P0：Kogmaw / Karthus / Undercover Agent 等其他 destroyed-family / friendly-destroyed FUs。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-15B 结论：**通过 Viktor 代表性 trigger baseline，未关闭 full-official**。未改协议 record 字段，未改前端，不宣称 full trigger engine，不宣称 READY-CANDIDATE；允许 4C 继续扩其他 destroyed-family / friendly-destroyed FUs。

## 0.28 阶段 4C-16 Mechanical Trickster Trigger Baseline

阶段 4C-16 选择 safe route：Mechanical Trickster / `OGN·239/298` / effect kind `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`。本批只把旧 immediate token create 迁移为真实 TriggerQueue / Stack / Priority 语义；不进入 Ironclad Vanguard、Kogmaw、Karthus、Undercover，不授予 full-official。项目整体仍 **NOT READY**，不得标记 READY / READY-CANDIDATE。

4C-16 服务端改动事实：

- 修改文件：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`。
- true stack `UNIT_DESTROYED` 后生成 `TRIGGER_QUEUED`。
- 单触发 auto-stack；多触发走 `ORDER_TRIGGERS` -> `StackItems`。
- priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x3。
- face-down / standby Mechanical Trickster 不入队、不泄漏 prompt metadata、不创建 token。
- 旧 `P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed` fixture 已更新为 queue / priority semantics。
- 未改协议、未改前端。

4C-16 新增 / 更新测试：

- `RealMechanicalTricksterLastBreathTriggersOrderAndCreateMinionsThroughStack`
- `RealMechanicalTricksterHiddenSourcesDoNotEnqueueOrCreateMinions`
- `P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed` updated

4C-16 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH16_MECHANICAL_TRICKSTER_TRIGGER_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- D 本批不修改服务端 / 前端代码、E 矩阵 / evidence 文件或 `riftbound-dotnet.sln`。

4C-16 A 复核命令：

- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3382/3382。
- Frontend build / smoke：A 将在 D 文档后继续运行；本 checkpoint 先不提前记为完成。

4C-16 关闭的 P0/P1 子项：

- Mechanical Trickster last-breath create minions 的旧 immediate compatibility 已迁移为代表性 real trigger queue / stack / priority 语义。
- face-down / standby Mechanical Trickster negative guard 已有代表测试，不入队、不泄漏、不造 token。

4C-16 仍保留 P0/P1：

- P1：Ironclad Vanguard 仍是旧 immediate compatibility，未迁移到 real trigger queue。
- P0：Kogmaw / Karthus / Undercover Agent 等 high-risk destroyed-family / friendly-destroyed holdbacks。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-16 结论：**通过 Mechanical Trickster 代表性 trigger migration，未关闭 full-official**。未改协议 record 字段，未改前端，不宣称 full trigger engine，不宣称 READY-CANDIDATE；允许 4C 继续扩其他 destroyed-family / friendly-destroyed FUs。

## 0.29 阶段 4C-17 Ironclad Vanguard Trigger Baseline

阶段 4C-17 已把 Ironclad Vanguard / `SFD·021/221` / `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` 的旧 immediate last-breath create robots 路径迁移到真实 TriggerQueue / Stack / Priority 代表基线。项目整体仍 **NOT READY**，不得标记 READY / READY-CANDIDATE。

4C-17 已验证实现事实：

- true stack `UNIT_DESTROYED` 后生成 `TRIGGER_QUEUED`。
- 单触发 auto-stack；多触发走 `ORDER_TRIGGERS` -> `StackItems`。
- priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x2，创建两名 3 战力 Robot / 机器人 token 到 controller base。
- face-down / standby Ironclad Vanguard 不入队、不泄漏 prompt metadata、不创建 token。
- 旧 `P79IroncladVanguardCreatesTwoRobotsWhenDestroyed` fixture 已更新为 queue / priority semantics。
- 不进入 Kogmaw、Karthus、Undercover，不授予 full-official。
- 矩阵口径修正：冻结矩阵中 Ironclad Vanguard 的正确 FU 是 `FU-6d0971786b`；`IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` 作为 4C-17 overlay `triggerEffectKind` 记录，不创建不存在的 `FU-a76d38727a`。

4C-17 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH17_IRONCLAD_VANGUARD_TRIGGER_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 与本 checkpoint。
- 未修改前端运行时代码；未提交 `riftbound-dotnet.sln`。

4C-17 A 验证：

- B focused filter：通过 42/42。
- B backend full：通过 3384/3384。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3384/3384。
- A frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- A Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- `git diff --check`、`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 与 4C-17 矩阵断言通过。

4C-17 当前 P0/P1：

- 关闭 P1 子项：Ironclad Vanguard true stack destruction 旧 immediate migration 已迁移到 real trigger queue / stack / priority 代表路径。
- 仍留 P1：Ironclad Vanguard state-based cleanup last-breath route 未在本批官方化。
- P0：Kogmaw / Karthus / Undercover Agent 等 high-risk destroyed-family / friendly-destroyed holdbacks。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-17 结论：**通过 Ironclad Vanguard 代表性 trigger migration，未关闭 full-official**。只关闭 true stack 代表路径，不宣称 full trigger engine，不宣称 full official coverage，不宣称正式 18-step E2E。

## 0.30 阶段 4C-18 Mechanical + Ironclad Cleanup Trigger Baseline

阶段 4C-18 已补齐 Mechanical Trickster + Ironclad Vanguard 在 lethal damage state-based cleanup 后的 `UNIT_DESTROYED` -> `TriggerQueue` / `StackItems` / priority / `TRIGGER_RESOLVED` 代表路线。4C-16 已关闭 Mechanical Trickster true stack migration；4C-17 已关闭 Ironclad Vanguard true stack migration；4C-18 关闭这两个 FU 的 cleanup-route representative trigger enqueue baseline。项目整体仍 **NOT READY**。

4C-18 已验证范围：

- Mechanical Trickster / `OGN·239/298` / `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`。
- Ironclad Vanguard / `SFD·021/221` / 冻结矩阵 FU `FU-6d0971786b` / `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`。
- state-based cleanup `LETHAL_DAMAGE` 产生 `UNIT_DESTROYED` 后，visible、face-up、non-standby source 入队；单触发 auto-stack，多触发走 `ORDER_TRIGGERS`；priority pass 后 Mechanical 创建 minion token x3，Ironclad 创建 robot token x2。
- hidden / face-down / standby source 不入队、不泄漏 prompt metadata、不创建 token。
- 代表 lethal damage 来源使用 Starfall cleanup route；不进入 Kogmaw、Karthus、Undercover。

4C-18 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH18_MECHANICAL_IRONCLAD_CLEANUP_TRIGGER_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- D 本批不修改服务端 / 测试 / 前端 / E matrix / coverage 文件或 `riftbound-dotnet.sln`。

4C-18 验证：

- Verified：4C-16 Mechanical Trickster true stack migration；4C-17 Ironclad Vanguard true stack migration。
- B focused filter：通过 47/47。
- B backend full：通过 3388/3388。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3388/3388。
- A frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- A Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- `git diff --check`、`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 与 4C-18 matrix assertions 通过。
- 新增/更新测试：`StateBasedCleanupMechanicalTrickstersTriggerOrderAndCreateMinionsThroughStack`、`StateBasedCleanupIroncladVanguardsTriggerOrderAndCreateRobotsThroughStack`、`StateBasedCleanupHiddenMechanicalTrickstersDoNotEnqueueTriggers`、`StateBasedCleanupHiddenIroncladVanguardsDoNotEnqueueTriggers`。

4C-18 后仍保留 P0/P1：

- P0：Kogmaw / Karthus / Undercover Agent 等 destroyed-family / friendly-destroyed holdbacks。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P1：same source same cleanup pass / same stack pass 多对象触发次数的 full official multiplicity matrix。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-18 结论：**通过 Mechanical Trickster + Ironclad Vanguard cleanup-route representative trigger enqueue baseline，未关闭 full-official**。项目整体仍 **NOT READY**，不得标记 READY / READY-CANDIDATE。

## 0.31 阶段 4C-19 Kogmaw Last-Breath AoE Baseline 审计

阶段 4C-19 已补 Kogmaw / 克格莫 `OGN·190/298` / `FU-af8b05c294` 的 visible face-up field source 绝念 AoE damage 代表切片。A 已验证 focused/backend full/frontend build/Chrome smoke/diff/矩阵断言；本批只关闭 Kogmaw representative baseline，不关闭 full-official。项目整体仍 **NOT READY**。

4C-19 已验证范围：

- Kogmaw visible、face-up、field source 的 last-breath AoE damage representative baseline。
- 路径：`UNIT_DESTROYED` -> `TriggerQueue` -> auto-stack 或 `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED` -> battlefield units take 4 damage -> cleanup queue stabilizes。
- AoE 使用 source pre-removal battlefield location，只伤害该 battlefield 的当前单位；其他 battlefield 单位不受伤害。
- hidden / face-down / standby Kogmaw source 不入队、不泄漏 prompt metadata、不造成 AoE damage。
- Kogmaw 被摧毁但缺少 battlefield location 时安全降级为 no-enqueue / no-damage。
- 不实现 Karthus 额外绝念，不实现 Undercover Agent discard / draw，不进入 full trigger engine 或 full-official。

4C-19 文档改动：

- 新增 `docs/CURRENT_STAGE4C_BATCH19_KOGMAW_LAST_BREATH_AOE_AUDIT.md`。
- 更新 `docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/rules-evidence-index.md` 与本 checkpoint。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`、`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`。
- 新增 `docs/CURRENT_STAGE4C_BATCH19_KOGMAW_LAST_BREATH_AOE_EVIDENCE.md`。
- 服务端修改限于 `src/Riftbound.Engine/CoreRuleEngine.cs` 与 `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`；未修改前端协议/组件。

4C-19 验证结果：

- Focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests&FullyQualifiedName~Kogmaw"` 通过 4/4。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3392/3392。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Hygiene：`git diff --check`、`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 与 4C-19 matrix assertions 通过。
- 新增/更新测试：`RealKogmawLastBreathDealsFourToDestroyedBattlefieldAndCleanupStabilizes`、`StateBasedCleanupKogmawLastBreathDealsFourToDestroyedBattlefield`、`StateBasedCleanupHiddenKogmawsDoNotEnqueueOrDealAoeDamage`、`RealKogmawDestroyedWithoutBattlefieldLocationDoesNotEnqueueOrDealDamage`。
- Matrix：`stage4CBatch19KogmawLastBreathAoeDamage` 只标记 `FU-af8b05c294`，`fullOfficialUpgrades=0`，`fullOfficialStillUncoveredFunctionalUnits=811`；Karthus / Undercover Agent 未标记。

4C-19 后仍保留 P0/P1：

- P0：Karthus / Undercover Agent 等 destroyed-family / friendly-destroyed holdbacks。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P1：same source same pass / simultaneous destruction / AoE damage 后多轮 cleanup 与触发交织的 full official multiplicity matrix。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 entries / 811 functional units full-official 覆盖、正式 18-step E2E、completion audit 仍未完成。

4C-19 结论：**通过 Kogmaw AoE last-breath representative baseline，未关闭 full-official**。项目整体仍 **NOT READY**，不得标记 READY / READY-CANDIDATE。

## 0.32 阶段 4C-20 Optional Trigger / Hidden Choice 分诊

阶段 4C-20 已做 read-only triage，未修改功能代码，未修改覆盖矩阵，未提交 `riftbound-dotnet.sln`。A 暂停直接实现 Karthus / Undercover Agent，原因是两条候选都触发需要裁决的核心规则语义；继续硬写会违反服务端权威和“前端只提交 prompt candidate”的原则。

裁决简报：`docs/CURRENT_STAGE4C_BATCH20_OPTIONAL_TRIGGER_HAND_CHOICE_DECISION.md`。

4C-20A 证据补强（2026-05-10）：

- `《符文战场》破限系列_裁判FAQ_260416.pdf` p3 / `BREAK-JFAQ-260416 p3`：Karthus 与 Gragas Bartender 同时被摧毁时，Gragas Bartender 的 `<绝念>` 触发两次。该证据关闭了“可见 Karthus 与另一个 Last Breath 源同时死亡时是否影响该源”的代表问题，但仍未关闭 optional choice、多 Karthus 叠加、Karthus 自身/多源批量建模、hidden / face-down / standby visibility 边界。
- `《符文战场》核心规则_260330.pdf` p62 / `CORE-260330 p62` / rule `422.4`：Undercover Agent 的弃牌是效果的一部分；手牌不足两张时弃尽可弃数量，且无论实际弃置几张都抽两张。该证据关闭 Undercover Agent hand-size shortfall ruling，但不关闭 hand-choice prompt、viewer redaction、public discard event payload、stale/wrong-player/no-mutation validation。

候选核对：

- Karthus / 卡尔萨斯 `OGN·236/298` / `FU-ee1dfb3ed3` / `OGN_KARTHUS_LAST_BREATH_STATIC_PLAY_UNIT`；冻结矩阵 status flags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；evidence candidate：`BREAK-JFAQ-260416 p3`。
- Undercover Agent / 卧底特工 `OGN·178/298` / `FU-6a52b04cb2` / `UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT`；冻结矩阵 status flags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`；rules evidence：`CORE-260330 p62`。

分诊结论：

- Karthus 不宜直接做“默认 yes”的 representative implementation。`可以额外触发一次` 涉及 optional choice、是否复制 trigger 或复制 effect、多 Karthus 是否叠加、Karthus 同时死亡是否仍生效、hidden / face-down / standby Karthus 是否提供静态修正，以及与 `ORDER_TRIGGERS` / APNAP / trigger batch 的全局耦合。
- Undercover Agent 不宜在没有 prompt 的情况下实现。`弃置两张手牌，然后抽两张牌` 发生在 trigger resolution 时，必须由服务端打开 viewer-specific hand choice prompt；自动弃前两张会替玩家选择并可能泄漏隐藏信息。手牌不足两张时的规则已由 `CORE-260330 p62` 关闭：弃尽可弃数量后仍抽两张。
- D 建议优先做 Undercover 的 triggered discard hand-choice prompt 前置裁决；B 建议若必须二选一，Karthus 可以做代表性 default yes，但需保留 optional decline P1。A 判断：不要默认 yes；先补基础设施 / 规则裁决。

4C-20 推荐下一步：

- 4C-20A：建立最小 optional-trigger decision / triggered hand-choice prompt 设计裁决文档，冻结 command/prompt 字段前先列出 Karthus optional repeat 与 Undercover discard-choice 的共同需求。
- 若用户确认 `可以额外触发一次` 可在代表切片中默认选择 yes，才允许 B 做 Karthus representative baseline，并明确 P1：未实现 optional decline。
- 若用户确认 Undercover 手牌不足规则和弃牌选择 prompt 语义，才允许进入 Undercover implementation。

当前停机原因：

- 需要用户/规则裁决：Karthus optional semantics / trigger-generation model 与 Undercover triggered hidden hand-choice prompt / redaction semantics。
- 项目整体仍 **NOT READY**。
- 不得标记 READY / READY-CANDIDATE，不得启动正式 18-step E2E，不得进入 1009 full-official。

## 0.33 阶段 4C-20B Undercover Agent Triggered Hand-Choice 审计

阶段 4C-20B 已由 B 完成 Undercover Agent / 卧底特工 `OGN·178/298` / `FU-6a52b04cb2` 的服务端 `HAND_CHOICE` / `CHOOSE_HAND_CARDS` 微切片。本批只关闭 Undercover triggered hand-choice server prompt 的代表性服务端子项，不代表 full-official，不得标记 READY / READY-CANDIDATE。项目整体仍 **NOT READY**。

4C-20B 审计入口：

- `docs/CURRENT_STAGE4C_BATCH20B_UNDERCOVER_HAND_CHOICE_AUDIT.md`。

4C-20B 已关闭服务端子项：

- Undercover Agent 绝念触发结算时由服务端打开 triggered hand-choice prompt。
- viewer-specific `handChoices` redaction：只有选择玩家能看到候选手牌；非选择玩家只看到脱敏等待信息。
- wrong player、stale prompt、invalid choice、malformed / illegal payload 拒绝且 no mutation。
- `CORE-260330` p62 / rule `422.4` 的 1 / 0 手牌 shortfall：手牌不足两张时弃尽可弃数量，仍抽两张。

4C-20B 验证结果：

- A focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~UndercoverAgentTriggerTests"` 通过 6/6。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3398/3398。
- A frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- A Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- A stage3 preflight：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api` 通过。
- A mechanical checks：`git diff --check` 通过；`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过；4C-20B matrix assertion 确认只标记 `FU-6a52b04cb2`，Karthus `FU-ee1dfb3ed3` 未标记。
- C frontend sync：前端已接入 `HAND_CHOICE` / `CHOOSE_HAND_CARDS` 类型、ActionPanel、状态面板、事件日志、Chrome smoke 与 stage3 preflight 覆盖；前端只展示服务端候选并提交 `CHOOSE_HAND_CARDS`，不结算弃牌或抽牌。
- D 本轮只更新文档，不修改服务端、前端、coverage matrix JSON 或 `riftbound-dotnet.sln`。

4C-20B 后仍保留 P0/P1：

- P0：Karthus 额外绝念 optional / multiplicity / multi-Karthus / visibility 裁决仍未实现。
- P0：非 Undercover 的通用 discard / hand-choice engine、其它 hand-choice FUs、完整 trigger engine、完整 effect resolution、完整 APNAP / trigger batch 仍未关闭。
- P1：public/private discard event redaction 全矩阵、replay / spectator hand-choice redaction、更多 hand-size / replacement / prevention 组合仍需扩展。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit 仍未完成。

4C-20B 结论：**通过 Undercover Agent triggered hand-choice server prompt 微切片，未关闭 full-official**。允许 4C 继续推进前端接线或下一批规则切片；项目整体仍 **NOT READY**。

## 0.34 阶段 4C-21 Sunken Temple Trigger Payment 审计

阶段 4C-21 已由 B 完成 Sunken Temple / 沉没神庙 `SFD·218/221` / `FU-05ce012700` 的征服强力单位触发支付代表切片。本批只把旧 immediate auto pay + draw 改为服务端权威 `TRIGGER_PAYMENT` / `PAY_COST` 窗口，不代表 full-official，不得标记 READY / READY-CANDIDATE。项目整体仍 **NOT READY**。

4C-21 checkpoint commit：本节随 `checkpoint: complete stage 4C sunken temple trigger payment baseline` 一起提交；具体 hash 以 `git log -1 --oneline` 为准。

4C-21 审计入口：

- `docs/CURRENT_STAGE4C_BATCH21_SUNKEN_TEMPLE_TRIGGER_PAYMENT_AUDIT.md`。
- `docs/CURRENT_STAGE4C_BATCH21_SUNKEN_TEMPLE_TRIGGER_PAYMENT_EVIDENCE.md`。

4C-21 已关闭服务端子项：

- Sunken Temple 征服此处且战场上留存强力单位时打开服务端权威 `TRIGGER_PAYMENT` / `PAY_COST` prompt。
- 旧 immediate auto pay + draw 口径已 superseded；前端 / 测试不得再把 `SFD·218/221` 解释为自动支付、自动抽牌。
- `PAY_COST(SPEND_MANA:1)` 支付成功后抽 1；`PAY_COST(DECLINE)` 拒付关闭窗口且不抽牌。
- focused 覆盖 invalid / stale / insufficient 等 no-mutation 语义；仍不外推为完整 PaymentEngine。

4C-21 证据入口：

- `CATALOG` `SFD·218/221`：沉没神庙文本为“当你征服此处时，如果此战场上留存至少一名强力单位，则你可以选择支付 1 来抽一张牌”。
- `SOUL-OFAQ-260114` p15：沉没神庙 powerful / conquest timing 证据入口。
- `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5：触发式技能费用、拒付与合法性流程入口。

4C-21 验证结果：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~P79BattlefieldConquerPowerfulUnitPaysOneToDraw|FullyQualifiedName~P79BattlefieldConquerPowerfulDrawSeedOffersBattlefieldDestinationAndDraws"` 通过 13/13。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3404/3404。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Stage 3 preflight：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api` 通过，player-a / player-b 双上下文 OK；尾部 499/143、allocator 与一次 `OperationCanceledException` 记录来自脚本关停 / 本地连接取消噪声，不作为阻断。
- B changed code/tests：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`、`tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`、`tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`。
- A 本轮因子 agent 超时接管了小范围服务端实现、测试校准、文档和矩阵收口；未修改前端功能代码，未触碰 `riftbound-dotnet.sln`。
- 正式 18-step E2E 未运行；本批验证不得替代最终验收。

4C-21 后仍保留 P0/P1：

- P0：完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、更多非出牌支付窗口仍未关闭。
- P0：完整 battlefield / conquer lifecycle、战场控制冻结、battle cleanup 与 conquest scoring 全规则矩阵仍未关闭。
- P0：完整 trigger engine、完整 effect resolution、FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 仍未完成。
- P1：Sunken Temple full-official timing matrix，包括 effective power / LayerEngine、temporary modifier、征服后变强力、战场上多单位同时离场等组合仍需补证据和测试。

4C-21 结论：**通过 Sunken Temple triggered payment 代表切片，未关闭 full-official**。阶段 4C 可继续推进下一批规则切片；项目整体仍 **NOT READY**。

## 0.35 阶段 4C-22 Muddy Dredger Warhawk Baseline 审计

阶段 4C-22 已由 A 决定收 Muddy Dredger / 腐泥疏浚工 `UNL-153/219` / `FU-b829fb32b9`，而不是 E 建议的 Aphelios。理由是 B/D 都判断 Muddy 是低耦合服务端 representative slice，且代码、focused backend 与 backend full 已通过。本批只关闭 visible face-up Last Breath -> Warhawk token 的代表性服务端子项，不代表 full-official，不得标记 READY / READY-CANDIDATE。项目整体仍 **NOT READY**。

4C-22 基线：从 checkpoint `5241179` `checkpoint: complete stage 4C sunken temple trigger payment baseline` 之后继续推进；不得回滚或覆盖本文件顶部 A 刚加的长期代理池记录。

4C-22 审计入口：

- `docs/CURRENT_STAGE4C_BATCH22_MUDDY_DREDGER_WARHAWK_AUDIT.md`。
- `docs/CURRENT_STAGE4C_BATCH22_MUDDY_DREDGER_WARHAWK_EVIDENCE.md`。

4C-22 已关闭服务端子项：

- visible face-up Muddy Dredger 被 state-based cleanup 摧毁后产生 `UNIT_DESTROYED` 并进入 `TriggerQueue`。
- 触发经 `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` 结算。
- 结算后 `UNIT_TOKEN_CREATED` Warhawk `UNL·T02` 到 controller base。
- hidden / face-down / standby / invalid source no enqueue / no leak / no token。
- Spellshield 只以 Warhawk token tag / identity 代表；Spellshield target tax 不在本批关闭。

4C-22 证据入口：

- `CATALOG` `UNL-153/219`：Muddy Dredger / 腐泥疏浚工 Last Breath 创建 / 打出 Warhawk 到你的基地。
- `CATALOG` `UNL·T02`：Warhawk / 战鹰 1 power token unit，带 Spellshield。
- `CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p52-p55 rules 383.3.d-383.3.e；p77 rule 460；p92-p105 keyword rules 800+。
- `JFAQ-251023` p2-p4 q2.2-q2.3；`SOUL-OFAQ-260114` p19-p20。

4C-22 验证结果：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MuddyDredger|FullyQualifiedName~RealTriggerQueue"` 通过 52/52。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3407/3407。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Stage 3 preflight 本批未运行；正式 18-step E2E 未运行。
- D 本轮只更新 docs 审计 / 证据 / checkpoint 文档；不修改服务端、前端、coverage matrix JSON 或 `riftbound-dotnet.sln`。
- 正式 18-step E2E 未运行；本批验证不得替代最终验收。

4C-22 后仍保留 P0/P1：

- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling、完整 effect resolution 仍未关闭。
- P0：完整 Last Breath / destroyed / friendly-destroyed family、same-source / same-pass / simultaneous destruction multiplicity matrix 仍未关闭。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口仍未关闭。
- P0：Spellshield target tax / mandatory additional cost / multi-target tax / insufficient payment regression 不由本批关闭。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit 仍未完成。
- P1：Warhawk token “打出”语义、token source / ownership / controller event fields 与 token family taxonomy 仍需后续证据。

4C-22 结论：**通过 Muddy Dredger Warhawk representative baseline，未关闭 full-official**。阶段 4C 可继续推进下一批低耦合规则切片；项目整体仍 **NOT READY**。

## 1. 总目标

以当前仓库五份官方规则 / FAQ PDF 与 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 官网卡牌快照为准，完成本地双人 1v1 标准构筑产品级 Web 游戏基线：

- 服务端是唯一规则权威。
- 前端只展示并提交服务端 `ActionPrompt` 与权威 `snapshot` 支持的合法操作。
- 先本地房间，先双人对战，暂不做账号、人机、观战、回放、云部署。
- 前端目标为正式产品 UI，允许使用官网卡图；需要原型图时可使用 image generation。
- 允许分阶段小步拆出 `CleanupQueue`、`PaymentEngine`、`LayerEngine`、`BattleEngine` 等架构，但禁止无边界大改。
- 最终清除审计文档中的 P0/P1 阻断，后端 full test、Chrome smoke / 正式 18 步 E2E 与审计文档全部收口。
- 最终卡牌层目标是当前快照 1009 张卡按官方文本与 FAQ 完整覆盖。

当前阶段必须始终对齐两份用户指定文档：

- `docs/符文战场_前端Web开发需求文档_给Codex.md`：前端 P0 包括正式产品 UI、通用 prompt 渲染、支付费用、伤害分配、触发排序、结算链/法术对决/战斗状态展示，且前端不得裁决规则。
- `docs/符文战场_服务端核心规则自查文档.md`：服务端审计必须覆盖权威状态、隐藏信息、区域/对象/控制权、费用、清理、战斗、法术对决、伤害分配、得分胜负、单卡/关键词与测试证据。

## 2. 当前仓库基线

当前阶段 2 checkpoint：`dfc4bd4 checkpoint: complete stage 2 protocol and frontend baseline`。其后的保护记录提交只更新本 checkpoint，恢复时以 `git log --oneline -8` 对齐最新 HEAD。

当前工作区预期：

- `main` 分支。
- `riftbound-dotnet.sln` 可为未跟踪本地文件，不属于交付，不应提交。

最新提交已补：

- `MOVE_UNIT` 支持 `BASE -> BATTLEFIELD:<battlefieldObjectId>`。
- 基地单位移动到敌方已占战场可触发战场争夺与法术对决代表路径。
- `PLAY_CARD` prompt 的条件减费与 Core 条件判断对齐，避免未满足条件时暴露实际付不起的来源。

`dda6385` 后已由 A 主控确认、并在第一轮修复的风险：

- `CoreRuleEngine.NormalizeMoveUnitLocation` 曾把整个 destination 转大写，可能把正式战场 objectId 中的小写 `a` 改成 `A`。
- 官方快照存在小写 `a` 战场：`OGN·276a/298`、`OGN·278a/298`、`OGN·293a/298`。
- 第一轮 B 已修复为只规范化 zone，不改变 objectId 大小写，并补小写 `a` 战场移动测试。
- 第一轮 B 已修复 0 / 负力量清理语义：`Power <= 0 && Damage == 0` 不再自动入清理；`Power <= 0 && Damage > 0` 走致命伤害清理；负力量对象真实 `Power` 保留，战斗输出仍按 0。

## 3. 第一轮 Agent 分工

### B 服务端 P0

负责人：子 agent `019e0b77-176c-7dd1-918b-9d173df7e681`

允许修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`

任务：

- 修复具体战场移动 objectId 大小写回归，并补小写 `a` 战场测试。
- 修复 0 / 负力量单位清理规则风险：力量小于等于 0 不能自动死亡；受到至少 1 点伤害后应死亡；负力量战斗输出按 0，但真实力量保留。
- 状态：**已完成，A 主控已跑聚焦测试与后端 full test 验收。**

禁止：

- 不做 `PaymentEngine` / `LayerEngine` / `BattleEngine` 大重构。
- 不改前端和审计文档。

### E 证据审计

负责人：子 agent `019e0b77-20bc-7a51-8d00-b1d4d30d270e`

允许修改：

- `docs/rules-evidence-index.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- 可新增小型证据 TODO 文档

任务：

- 落 0 / 负力量 FAQ 证据。
- 落具体战场 objectId 大小写风险和验收条目。
- 保持 `NOT READY` 结论。
- 状态：**已完成。**

### C 服务端规则设计

负责人：子 agent `019e0b77-3747-7cd0-9824-04477aa9b88c`

允许修改：

- `docs/CURRENT_PAYMENT_LAYER_DESIGN.md`

任务：

- 输出第一阶段 `PaymentEngine` / `LayerEngine` 最小接口设计。
- 列出当前费用、减费、额外费用、持续效果散落点。
- 状态：**已完成，产物为 `docs/CURRENT_PAYMENT_LAYER_DESIGN.md`。**

### D 前端契约

负责人：子 agent `019e0b77-2c13-7290-b14b-4d4939eed5a0`

允许修改：

- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`

任务：

- 列出正式产品 UI 仍需服务端补的字段 / 窗口。
- 拆正式 18 步 E2E 的 Chrome 调试顺序。
- 状态：**已完成，产物为 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`。**

## 4. 第一轮验收记录

B 的代码结果已优先审查，E/C/D 文档结果已纳入工作区。

已跑验证：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZeroPower|FullyQualifiedName~NegativePower|FullyQualifiedName~LowercaseOfficialBattlefield|FullyQualifiedName~MoveUnitCommandMovesBaseUnitToConcreteBattlefield|FullyQualifiedName~ActionPromptOffersConcreteBattlefieldsForBaseUnitMove"`：11/11 通过。
- `git diff --check`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3304/3304 通过。

第一轮不得把项目标记为 READY。即使本轮全过，仍至少保留这些阻断：

- central cleanup queue 未完整官方化。
- spell duel / battle 完整生命周期仍未完成。
- PaymentEngine 未统一。
- LayerEngine 未统一。
- 全官方卡牌证据与执行仍未完成。
- 正式 18 步 E2E 未最终收口。

## 5. 恢复步骤

中断后恢复时：

1. 读本文。
2. 跑 `git status --short --branch`。
3. 看最新 8 条提交：`git log --oneline -8`。
4. 若子 agent 已完成，先审其修改文件和测试结果。
5. 本阶段拆任何前端/服务端任务前，先用 `docs/符文战场_前端Web开发需求文档_给Codex.md` 与 `docs/符文战场_服务端核心规则自查文档.md` 校验目标边界。
6. 若有冲突，优先保护 B 的服务端核心修复，再合并 E/C/D 文档。
7. 每轮结束更新本文的 HEAD、分工状态、测试记录和剩余阻断。

## 6. 第二轮 B2 协议契约状态

目标：最小、兼容补正式 PromptView/PromptType 契约入口。

已完成：

- `Riftbound.Contracts` 新增 `PromptTypes` 常量与 `PromptViewDto`，并在 `ActionPromptDto` 末尾追加可空 `View` 字段。
- `MatchSession.ActionPromptBuilder.Build` 为现有 prompt 构造 view，不改变 `actions/candidates` 语义。
- DevUi 协议类型新增 `PromptType`、`PromptViewDto`、`ActionPromptDto.view`，未改 UI 使用逻辑。
- 补充 `PromptView` 聚焦测试，覆盖 main prompt 的 `MAIN_ACTION` 与 mulligan prompt 的 `MULLIGAN`。
- A 主控已复审实现顺序：`PromptTypeFor` 与现有 `BuildPrompts` 的窗口优先级一致，且 `view` 为追加可空字段，不破坏旧消费者。

已跑验证：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~PromptView|FullyQualifiedName~MatchRecovery"`：66/66 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3306/3306 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `git diff --check`：通过。

仍保留阻断：

- damage assignment/payment/trigger ordering 本轮未实现。
- battle/spell duel 完整生命周期仍未收口；稳定 battle/spellDuel id 在第二波只做了派生视图入口。
- central cleanup queue、PaymentEngine、LayerEngine、正式 18 步 E2E 仍未最终 READY。

## 7. 第二波并行任务

启动时间：2026-05-09

### B2 前端 PromptView 消费

负责人：子 agent `019e0b88-0775-74a1-8b1f-741daa61d93d`

状态：**已完成，A 主控已复审并跑前端 build / full conformance 验收。**

允许修改：

- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/components/match/MatchTopBar.tsx`
- 必要时新增小型前端 helper
- 可在 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md` 追加完成记录

禁止修改服务端契约和规则代码。

结果：

- `ActionPanel` 已优先展示 `prompt.view.title/message/type`；旧 prompt 无 view 时继续展示原 reason。
- `relatedBattlefieldId`、`relatedStackItemId` 只做轻量文本展示，不在前端推导规则。
- `MatchTopBar` 因需要改父级传参链路，本轮未动。

### Galileo Payment 探针

负责人：子 agent `019e0b92-1a62-7ec0-be51-615120328845`

状态：**已完成，只读。**

目标：基于当前代码找出 `PaymentEngine/PAY_COST` 下一步最小安全写入范围、测试过滤器与独占文件。

结论：

- 下一步 Payment 不应整体搬 `TryBuildPlayCardPlan`，应先抽支付原语、支付资源动作、token 解析和 quote 元数据。
- 建议最小新增 `src/Riftbound.Engine/PaymentCostRules.cs`，只在 `CoreRuleEngine.cs` / `MatchSession.cs` 接入调用点；第一刀不改 `Protocol.cs`，不引入 pending `PAY_COST` window。
- 独立 `PAY_COST` 仍缺 `PromptTypes.PayCost`、`PayCostCommand`、`paymentPlanId/paymentId/window`、typed cost/resource/additional cost schema 和 pending payment state。

### Faraday Battle/Prompt 探针

负责人：子 agent `019e0b92-2552-7062-a452-b8fd2336441b`

状态：**已完成，只读。**

目标：基于当前代码找出 battle/spell duel 稳定 id、`ASSIGN_COMBAT_DAMAGE` 与复杂 prompt 下一步最小安全写入范围、测试过滤器与独占文件。

结论：

- 下一步小改应先做稳定 `battleId/spellDuelId` 派生视图，不应直接拆 `ASSIGN_COMBAT_DAMAGE`。
- 原因：`ASSIGN_COMBAT_DAMAGE` 需要新增命令、PromptType、pending battle state，并把 `ResolveDeclareBattle` 从同步结算拆成多阶段状态机，风险大且容易与 Payment/PromptView 冲突。

### A 主控第二波稳定 ID 小改

状态：**已完成。**

改动：

- `SpellDuelState` 派生 `spellDuelId` 与 `battlefieldObjectId`。
- `BattleState` 派生 `battleId`。
- `snapshot.timing.spellDuel`、`snapshot.timing.battle`、`timing.battlefieldTasks` 追加对应 id。
- `PromptView.relatedBattleId` / `relatedSpellDuelId` 在战斗声明、法术对决焦点窗口中填充。
- 未新增 `ASSIGN_COMBAT_DAMAGE`，未改变 `ResolveDeclareBattle` 的同步战斗伤害结算。

已跑验证：

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ActionPromptView"`：4/4 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ActionPromptView|FullyQualifiedName~CoreRuleEngineStartsBattlefieldSpellDuelAfterStackResolutionLeavesContestedBattlefield|FullyQualifiedName~CoreRuleEngineMarksContestSpellDuelCompletedWhenAllPlayersPassFocus|FullyQualifiedName~CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask|FullyQualifiedName~P4DeclareBattleCommand"`：63/63 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3308/3308 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `git diff --check`：通过。

## 8. 当前独占文件原则

- `src/Riftbound.Engine/CoreRuleEngine.cs`：同一时间只允许一个服务端规则 worker 修改。
- `src/Riftbound.Engine/MatchSession.cs`：同一时间只允许一个服务端协议/状态 worker 修改；前端 worker 禁止修改。
- `src/Riftbound.Contracts/Protocol.cs`：契约字段必须由 A 主控确认后单 agent 修改。
- `src/Riftbound.DevUi/src/components/cards/CardDetailDrawer.tsx`：动作 composer 集中且风险高，同一时间只允许一个前端 worker 修改。
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`：第二波由 B2 修改完成；后续继续前端 UI 时由单个前端 worker 独占。
- `src/Riftbound.DevUi/src/components/match/MatchTopBar.tsx`：第二波未改；若接入 `prompt.view` 需连同父级传参由单个前端 worker 独占。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`：巨大测试文件，同一时间只允许一个服务端 worker 修改。
- `docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`：可追加，但主控合并前要避免互相覆盖最新结论。

## 9. 第三波 Payment 最小抽取

状态：**已完成。**

目标：按 Galileo 探针建议，只抽支付原语，不引入 `PAY_COST` 状态机，不搬 `TryBuildPlayCardPlan`。

改动：

- 新增 `src/Riftbound.Engine/PaymentCostRules.cs`。
- 将符文/符能支付纯逻辑集中到 `PaymentCostRules.PayRuneCosts`、`CanPayRuneCosts`、`CanPayPowerCost`、`PayPowerCost`、`NormalizePowerCostByTrait`。
- 将经验支付集中到 `PaymentCostRules.PayExperienceCosts`。
- `CoreRuleEngine` 原私有支付方法保留为薄包装，以减少调用点改动和冲突面。

已跑验证：

- `source scripts/dev-env.sh && dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~P7TypedPowerPayment|FullyQualifiedName~P7PlayCardRecyclesRune|FullyQualifiedName~PaymentResource|FullyQualifiedName~P4ExperienceOptionalCost|FullyQualifiedName~P4HasteOptionalReadyBranch|FullyQualifiedName~CrescentGuardReady|FullyQualifiedName~P4AssembleEquipmentCommand|FullyQualifiedName~P79RagingDrake|FullyQualifiedName~P79BattlefieldStatic|FullyQualifiedName~P79BattlefieldHeldUnitCostIncrease|FullyQualifiedName~P79LegendAct"`：195/195 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3308/3308 通过。
- `git diff --check`：通过。

仍保留阻断：

- 尚未统一支付资源动作副作用。
- 尚未统一 Core/Prompt 减费 quote。
- 尚未新增 `PAY_COST` prompt/schema/command/pending payment state。
- `COST_PAID` payload 已在主要玩家命令窗口补了 `paymentId/paymentWindow/remaining*`，战场触发/替代费用等自动支付旧路径仍待迁移，并需先向深层 helper 传入当前 action tick。

## 10. 第三波前端 PromptView 顶栏接入

状态：**已完成。**

改动：

- `MatchPage` 将当前 `prompt` 传给 `MatchTopBar`。
- `MatchTopBar` 展示服务端 `prompt.view.title/type`，作为顶栏窗口状态。
- 仍然只展示服务端状态，不根据 `PromptView` 在前端裁决行动、战斗或费用。

已跑验证：

- `source ../../scripts/dev-env.sh && npm run build`：通过。

## 11. 第三波 PromptType 预留

状态：**已完成。**

改动：

- `Riftbound.Contracts.PromptTypes` 预留 `SPELL_DUEL_ACTION`、`ASSIGN_COMBAT_DAMAGE`、`PAY_COST`、`ORDER_TRIGGERS`。
- DevUi `PromptType` union 同步这些复杂窗口类型。
- 本轮只稳定命名，不实际发出这些窗口，也不新增提交命令或 pending state。

已跑验证：

- `source scripts/dev-env.sh && dotnet build src/Riftbound.Contracts/Riftbound.Contracts.csproj --no-restore`：通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3308/3308 通过。
- `git diff --check`：通过。

## 12. 第三波 Payment 事件包络

状态：**已完成。**

目标：不引入 `PAY_COST` 状态机，只让主支付路径事件具备后续正式 UI/审计可追踪的稳定字段。

改动：

- `PaymentCostRules` 新增 `BuildPaymentId` 与 `BuildCostPaidPayload`。
- `PLAY_CARD` 主路径、`ASSEMBLE_EQUIPMENT` 主路径、伏击打出、启动技能、传奇行动、待命埋伏、待命翻开反应的 `COST_PAID` 追加 `paymentId/paymentWindow/remainingMana/remainingPower/remainingPowerByTrait/remainingExperience`。
- 通过回收符文支付资源动作产生的 `RUNE_RECYCLED` / `POWER_GAINED` 与对应 `COST_PAID` 使用同一个 `paymentId`。
- 保留既有 `mana/power/powerByTrait/optionalCosts/paymentResourceActions/recycledRuneObjectIds` 等字段，避免破坏现有前端与 fixture。

已跑验证：

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~P79TypedPowerPaymentSeedOffersAmountChoicesAndPlaysThroughHub|FullyQualifiedName~P79AssemblePaymentRecycleSeedOffersResourceAndAttachesThroughHub"`：2/2 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~LegendAct|FullyQualifiedName~HideCard|FullyQualifiedName~RevealCard|FullyQualifiedName~AssemblePaymentRecycle|FullyQualifiedName~TypedPowerPayment"`：193/193 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3308/3308 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `git diff --check`：通过。

仍保留阻断：

- 尚未新增独立 `PAY_COST` prompt/command/pending state。
- 战场触发、替代效果等自动支付 `COST_PAID` 旧路径仍待迁移到同一包络；迁移前应先把 action tick 传入相关 helper，避免生成不可追踪的支付 id。
- Core 与 Prompt 的减费 quote 仍未统一。

## 13. 第三波 prompt 戳过期保护

状态：**已完成。**

目标：给正式 UI 的 `promptId + snapshotTick` 提交提供最小服务端错误语义，不改变旧客户端。

改动：

- `ErrorCodes` 新增 `PROMPT_EXPIRED`。
- DevUi `GameCommand` 支持可选 `promptId/snapshotTick`。
- `useMatchController.submitCommand` 会把当前 `ActionPromptDto.promptId/snapshotTick` 附到提交命令。
- DevUi 错误文案已覆盖 `PROMPT_EXPIRED`。
- `MatchSession.SubmitAsync` 在调用规则引擎前读取 raw command 中的可选 `promptId/snapshotTick`，与当前服务端 prompt 比对；不匹配时返回 rejected `ResolutionResult`，由 Hub 下发 `PROMPT_EXPIRED`。
- 未强制旧客户端必须提交 prompt 戳；未实现复杂 prompt payload schema。

已跑验证：

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~SubmitIntentRejectsStalePromptStamp|FullyQualifiedName~P79TypedPowerPaymentSeedOffersAmountChoicesAndPlaysThroughHub"`：2/2 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3309/3309 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `git diff --check`：通过。

仍保留阻断：

- `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 仍缺正式 prompt payload 与命令。
- 前端仍需要未知复杂 prompt 的通用安全渲染。

## 14. 第三波复杂 PromptView 降级渲染

状态：**已完成。**

目标：在正式复杂 prompt schema 未完成前，让前端遇到服务端未来发出的复杂窗口时有安全、可读、不裁决规则的降级展示。

改动：

- `ActionPanel` 对 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` 展示通用服务端选项面板。
- 面板展示候选来源、目标、位置、模式、费用和安全 metadata 摘要。
- 不生成服务端未提供的命令，不在前端计算费用、排序、伤害分配或法术对决结果。

已跑验证：

- `source ../../scripts/dev-env.sh && npm run build`：通过。

仍保留阻断：

- 复杂 prompt 的正式 payload schema、手动支付、伤害分配提交和触发排序提交仍未实现。

## 15. 阶段 4C-55 Vex Alt A Spellshield / Yordle Guard Checkpoint

状态：**已完成并建议 checkpoint。项目整体仍 NOT READY。**

长期子代理复用：

- B / Maxwell：`019e1068-5757-7bd1-8129-d401c60e0b7f`
- C / Copernicus：`019e0bbc-df6f-7151-baf5-f79ff466c5a9`
- D / Nash：`019e1068-6042-7dc3-a45c-655838d02b92`
- E / Poincare：`019e1068-6975-7242-9143-1c50d7ce23fa`

本批范围：

- 只记录 Vex alt A / 薇古丝 `UNL-150a/219` / cardId `34698` / `FU-4d8ee1696b` / `VEX_ALT_A_SPELLSHIELD_OPPONENT_UNIT_STUN_STATIC` 的 test-only ordinary hand `PLAY_CARD` 0-target + `法盾` / `约德尔人` 标签 guard baseline。
- 新增无效输入 no mutation / no leak 测试：显式目标、错误区域、对手源牌、面朝下待命源牌、费用不足。
- 不实现 / 不宣称 opponent-unit stun / cannot-move runtime、Spellshield full target tax、movement/control effects、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。
- `UNL-150/219` / `FU-9f7cb73dc4` 保持 4C-48 独立覆盖，不由 4C-55 重新标记。

验证结果：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~VexAlt|FullyQualifiedName~Vex|FullyQualifiedName~Spellshield|FullyQualifiedName~PlayUnit"`：59/59 passed。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3656/3656 passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：passed。
- `git diff --check`：passed。

仍保留阻断：

- actual opponent-unit stun / cannot-move static behavior。
- all Vex variants full official behavior。
- movement / control-zone full matrix。
- cleanup / replacement / duration。
- PaymentEngine full officialization。
- FAQ adjudication for Vex refs。
- 1009 snapshot-entry / 811 functional-unit full-official coverage。
- formal 18-step E2E acceptance。

## 16. 阶段 4C-56 Secret Art! Mercy Boon Guard Verified Representative

状态：**已完成代表切片收口并 checkpoint 为 `3631c00`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Secret Art! Mercy / 秘奥义！慈悲度魂落 `OGN·053/298` / cardId `31265` / `FU-3461727400` / `SECRET_ART_MERCY_GRANT_BOON_NO_GLOBAL_BONUS`。
- 只补 test-only dedicated guard：ordinary hand `PLAY_CARD`、支付 3 mana、友方 public field unit 目标获得 `增益` 与 permanent +1、友方法盾目标 no-tax、非法目标 no mutation / no leak。
- 不实现 / 不宣称 global all-boons extra +1 this turn、LayerEngine / duration cleanup、full Spellshield tax matrix、1009/811 full-official 或 formal 18-step E2E。

修复事实：

- B / Maxwell 在授权后修复 `src/Riftbound.Engine/CoreRuleEngine.cs`，新增 / 使用 `IsPlayerControlledFieldUnitObject` 与 `IsVisibleFieldUnitObject`，让 `FriendlyUnit`、`FriendlyUnitThenFriendlyUnit`、`FriendlyThenEnemyUnits` 第一目标、`FriendlyThenEnemyBattlefieldUnits` 第一目标只接受控制方 visible field unit。
- B / Maxwell 同步修复 `src/Riftbound.Engine/MatchSession.cs` 的 prompt candidate 单位判定，允许 legacy custom-tag public field unit（例如 `黄沙士兵`），同时排除 equipment / spell / rune / standby / face-down objects。
- 新增 `tests/Riftbound.ConformanceTests/SecretArtMercyBoonGuardTests.cs`，覆盖成功路径、friendly Spellshield no-tax、invalid target/source/no mutation、insufficient mana、already-booned no duplicate，以及 prompt parity。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SecretArtMercy|FullyQualifiedName~Boon|FullyQualifiedName~Spellshield"`：passed 87/87。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SandSoldiersRise|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~FriendlyUnit"`：passed 133/133。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3668/3668。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C56` 从 `BLOCKED_BACKEND_TARGET_VALIDATION_FAILURE` 回填为 `REPRESENTATIVE_GUARD_VERIFIED_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH56_SECRET_ART_MERCY_BOON_GUARD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH56_SECRET_ART_MERCY_BOON_GUARD_EVIDENCE.md` 记录 narrow representative guard verified 证据。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

剩余边界：

- standby / reaction、quick / spell-duel breadth。
- global all-boons extra +1 this turn。
- LayerEngine / duration cleanup。
- full target matrix 与 full Spellshield tax。
- PaymentEngine、FAQ adjudication、1009/811 full-official。
- formal 18-step E2E acceptance。

Checkpoint 记录：

- 已提交：`3631c00 checkpoint: complete stage 4C secret art mercy guard`。
- 提交前验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过；`git diff --cached --check` 通过。
- 已纳入：`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`、`tests/Riftbound.ConformanceTests/SecretArtMercyBoonGuardTests.cs`、4C-56 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 17. 阶段 4C-57 Reflections Swap Draw Guard Verified Representative

状态：**已完成代表切片收口并 checkpoint 为 `b18be5d`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Reflections / 镜中幻影 `UNL-083/219` / cardId `34618` / `FU-f0eb0fb704` / `REFLECTIONS_SWAP_FRIENDLY_UNITS_DRAW_1`。
- 只补 test-only dedicated guard：ordinary hand `PLAY_CARD`、支付 2 mana、两个友方 public field unit 目标处于不同代表位置、至少一个目标带 `瞬息`、结算时 swap locations 并 draw 1、非法目标 no mutation / no leak。
- 不实现 / 不宣称 exact multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR targeting / stack lifecycle、full movement / control-zone lifecycle、hidden-info / redaction matrix、PaymentEngine full officialization、Ephemeral lifecycle、draw replacement / deck exhaustion、1009/811 full-official 或 formal 18-step E2E。

修复事实：

- B / Maxwell 修复 `src/Riftbound.Engine/CoreRuleEngine.cs`，在 `PLAY_CARD` target validation 中加入 `HasValidSwapTargetLocations`，让 `SwapsTargetLocations` 牌在命令阶段拒绝 same-position pair。
- B / Maxwell 同步修复 `src/Riftbound.Engine/MatchSession.cs`，把 `AnyTargetRequiredTag` 与 `SwapsTargetLocations` 纳入 server target-selection constraint，prompt `legalTargetSelections` 只保留 Ephemeral-qualified different-position pairs。
- 新增 `tests/Riftbound.ConformanceTests/ReflectionsSwapGuardTests.cs`，覆盖成功 swap/draw、no-Ephemeral rejection、same-position rejection、friendly equipment / spell / rune / face-down standby / stale / enemy / dirty controller rejection，以及 prompt parity。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Reflections|FullyQualifiedName~Swap|FullyQualifiedName~FriendlyUnit"`：passed 54/54。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~SandSoldiersRise|FullyQualifiedName~Reflections"`：passed 112/112。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3679/3679。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C57` 回填为 `REFLECTIONS_SWAP_FRIENDLY_UNITS_DRAW_GUARD_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH57_REFLECTIONS_SWAP_DRAW_GUARD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH57_REFLECTIONS_SWAP_DRAW_GUARD_EVIDENCE.md` 记录 narrow representative guard verified 证据。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`b18be5d checkpoint: complete stage 4C reflections swap guard`。
- 提交前验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过；`git diff --check` 通过；`git diff --cached --check` 通过。
- 已纳入：`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`、`tests/Riftbound.ConformanceTests/ReflectionsSwapGuardTests.cs`、4C-57 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 18. 阶段 4C-58 Spirit Fire Total Power Target Guard Verified Representative

状态：**已完成代表切片收口并 checkpoint 为 `2de935b`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Spirit Fire / 妖异狐火 `OGN·256/298` / cardId `31498` / `FU-a9dc3495e1` / `SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4`。
- 只补 ordinary hand `PLAY_CARD`、支付 3 mana、public battlefield unit targets、total target power <= 4、stack / pass-pass 后 destroy to owner graveyard 的 representative target guard。
- 不实现 / 不宣称 same-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR targeting / stack lifecycle、full destroy / cleanup / replacement / prevention / Last Breath interactions、full Spellshield tax matrix、PaymentEngine、LayerEngine / effective power、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

修复事实：

- A 在 B / Maxwell 留下 partial Core diff 后收回 B 写锁并完成窄切片。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 新增 Spirit Fire effect-specific target guard，让默认 `BattlefieldUnit` scope 不能接受 battlefield equipment / spell / rune / hidden / dirty objects。
- `src/Riftbound.Engine/MatchSession.cs` 同步 prompt parity：Spirit Fire `targetChoicesByIndex` 与 `legalTargetSelections` 过滤 public battlefield unit / controller-consistency 目标，server legal selections 继续执行 total-power <= 4。
- 新增 `tests/Riftbound.ConformanceTests/SpiritFireDestroyGuardTests.cs`，覆盖成功 total-power-four destroy、total power > 4 rejection、battlefield equipment / spell / rune / face-down standby / stale / base / hand / dirty controller rejection，以及 prompt parity。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpiritFireDestroyGuardTests|FullyQualifiedName~SpiritFire|FullyQualifiedName~TargetGuard|FullyQualifiedName~Spellshield"`：passed 48/48。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~SpiritFire"`：passed 112/112。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3690/3690。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C58` 回填为 `SPIRIT_FIRE_DESTROY_TOTAL_POWER_GUARD_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH58_SPIRIT_FIRE_TOTAL_POWER_TARGET_GUARD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH58_SPIRIT_FIRE_TOTAL_POWER_TARGET_GUARD_EVIDENCE.md` 记录 narrow representative guard verified 证据。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`2de935b checkpoint: complete stage 4C spirit fire target guard`。
- 提交前验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过；`git diff --check` 通过；`git diff --cached --check` 通过。
- 已纳入：`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`、`tests/Riftbound.ConformanceTests/SpiritFireDestroyGuardTests.cs`、4C-58 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 19. 阶段 4C-59 Zenith Blade Enemy Battlefield Stun Guard Verified Representative

状态：**已完成代表切片收口并 checkpoint 为 `70d9ee0`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Zenith Blade / 天顶之刃 `OGN·262/298` / cardId `31504` / `FU-64a7f67581` / `ZENITH_BLADE_STUN_ENEMY_BATTLEFIELD_UNIT_NO_MOVE`。
- 只补 ordinary hand `PLAY_CARD`、支付 3 mana、enemy public battlefield unit target、stack / pass-pass 后 apply `STUNNED` until end of turn 的 representative target guard。
- 不实现 / 不宣称 optional friendly unit movement、precise multi-battlefield destination selection、standby / reaction、quick / spell-duel timing、full FEPR targeting / stack lifecycle、status duration cleanup / replacement / prevention interactions、full Spellshield tax matrix、PaymentEngine、LayerEngine / effective power and duration ordering、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

修复事实：

- A 基于 B / Maxwell 和 E / Poincare 的只读候选建议裁决选择 E 推荐的低耦合无 FAQ Zenith Blade 路线；B 建议的 Skullcrack 因 FAQ 引用留作后续复核。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 新增 Zenith Blade effect-specific target guard，让共享 `EnemyBattlefieldUnit` scope 不能为本效果接受 battlefield equipment / spell / rune / hidden / dirty objects。
- `MatchSession` 无需代码改动；既有 prompt `EnemyBattlefieldUnit` path 已走 visible enemy field-unit helper，本批新增测试冻结 prompt parity。
- 新增 `tests/Riftbound.ConformanceTests/ZenithBladeStunGuardTests.cs`，覆盖成功 enemy battlefield unit stun、battlefield equipment / spell / rune / face-down standby / stale / base / hand / friendly / dirty controller rejection，以及 prompt targetChoices parity。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZenithBladeStunGuardTests|FullyQualifiedName~ZenithBlade"`：passed 15/15。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZenithBlade|FullyQualifiedName~Stun|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt"`：passed 154/154。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3701/3701。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C59` 回填为 `ZENITH_BLADE_ENEMY_BATTLEFIELD_STUN_GUARD_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH59_ZENITH_BLADE_ENEMY_BATTLEFIELD_STUN_GUARD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH59_ZENITH_BLADE_ENEMY_BATTLEFIELD_STUN_GUARD_EVIDENCE.md` 记录 narrow representative guard verified 证据。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`70d9ee0 checkpoint: complete stage 4C zenith blade target guard`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/ZenithBladeStunGuardTests.cs`、4C-59 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 20. 阶段 4C-60 Firestorm Enemy Battlefield Damage Guard Verified Representative

状态：**已完成代表切片收口并 checkpoint 为 `d695fcd`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Firestorm / 烈火风暴 `OGS·002/024` / cardId `31581` / `FU-fe9dbeea3d` / `FIRESTORM_DAMAGE_ALL_ENEMY_BATTLEFIELD_UNITS_3`。
- 只补 ordinary hand `PLAY_CARD`、支付 6 mana、zero-target stack item、stack / pass-pass 后 enemy public battlefield units 受到 3 点伤害的 representative damage guard。
- 不实现 / 不宣称 damage prevention / replacement / cleanup、lethal-damage triggers、formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR stack lifecycle、PaymentEngine、LayerEngine / effective power、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

修复事实：

- A 基于 matrix fresh risk pass 选择低耦合、无 FAQ、单 FU Firestorm 路线。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 新增 `GetEnemyBattlefieldUnitObjectIds`，并让 enemy battlefield unit damage resolution 走 unit-only / public battlefield / controller-consistency 过滤。
- 新增 `tests/Riftbound.ConformanceTests/FirestormEnemyBattlefieldDamageGuardTests.cs`，覆盖成功 zero-target enemy battlefield unit damage、enemy battlefield equipment / spell / rune / face-down standby / dirty controller / friendly battlefield / enemy base exclusion，以及 explicit target no-mutation rejection。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~FirestormEnemyBattlefieldDamageGuardTests|FullyQualifiedName~Firestorm"`：passed 13/13。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Firestorm|FullyQualifiedName~CrescentStrike|FullyQualifiedName~BulletTime|FullyQualifiedName~DamageAllEnemyBattlefield|FullyQualifiedName~EnemyBattlefield"`：passed 36/36。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3711/3711。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C60` 回填为 `FIRESTORM_ENEMY_BATTLEFIELD_DAMAGE_GUARD_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH60_FIRESTORM_ENEMY_BATTLEFIELD_DAMAGE_GUARD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH60_FIRESTORM_ENEMY_BATTLEFIELD_DAMAGE_GUARD_EVIDENCE.md` 记录 narrow representative guard verified 证据。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`d695fcd checkpoint: complete stage 4C firestorm damage guard`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/FirestormEnemyBattlefieldDamageGuardTests.cs`、4C-60 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 21. 阶段 4C-61 Overcharged Energy Field Unit Guard Verified Representative

状态：**已完成代表切片收口并 checkpoint 为 `627430a`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Overcharged Energy / 过载能量 `OGN·123/298` / cardId `31345` / `FU-b2e0e1d8da` / `OVERCHARGED_ENERGY_EXHAUST_ALL_FRIENDLY_DAMAGE_ALL_BATTLEFIELD_12`。
- 只补 ordinary hand `PLAY_CARD`、支付 7 mana、zero-target stack item、stack / pass-pass 后 friendly public field units 横置、public battlefield units 受到 12 点伤害的 representative field-unit guard。
- 不实现 / 不宣称 damage prevention / replacement / cleanup、lethal-damage triggers、formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR stack lifecycle、PaymentEngine、LayerEngine / effective power、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

修复事实：

- A 基于 matrix fresh risk pass 选择低耦合、无 FAQ、单 FU Overcharged Energy 路线。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 新增 / 复用 public unit enumerators：`GetControlledPublicFieldUnitObjectIds` 用于 friendly-unit exhaust；`GetBattlefieldUnitObjectIds` 用于 all-battlefield-unit damage。
- 新增 `tests/Riftbound.ConformanceTests/OverchargedEnergyGuardTests.cs`，覆盖成功 zero-target friendly public field-unit exhaust、public battlefield-unit damage、equipment / spell / rune / face-down standby / dirty controller / base damage exclusion，以及 explicit target no-mutation rejection。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OverchargedEnergyGuardTests|FullyQualifiedName~OverchargedEnergy|FullyQualifiedName~Overcharged"`：passed 12/12。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Overcharged|FullyQualifiedName~Tibbers|FullyQualifiedName~BladeWhirlwind|FullyQualifiedName~DamageAllBattlefield|FullyQualifiedName~Firestorm|FullyQualifiedName~CrescentStrike|FullyQualifiedName~EnemyBattlefield"`：passed 53/53。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3722/3722。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C61` 回填为 `OVERCHARGED_ENERGY_FIELD_UNIT_GUARD_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH61_OVERCHARGED_ENERGY_FIELD_UNIT_GUARD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH61_OVERCHARGED_ENERGY_FIELD_UNIT_GUARD_EVIDENCE.md` 记录 narrow representative guard verified 证据。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`627430a checkpoint: complete stage 4C overcharged energy field unit guard`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/OverchargedEnergyGuardTests.cs`、4C-61 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 22. 阶段 4C-62 Hunt Ready Friendly Units Guard Verified Representative

状态：**已完成代表切片收口并 checkpoint 为 `d505dcf`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Hunt / 狩猎 `SFD·204/221` / cardId `33303` / `FU-f877e60407` / `HUNT_READY_ALL_FRIENDLY_UNITS`。
- 只补 ordinary hand `PLAY_CARD`、支付 1 mana、zero-target stack item、stack / pass-pass 后 friendly public field units 变为活跃的 representative ready guard。
- 不实现 / 不宣称 formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR stack lifecycle、PaymentEngine、readiness duration / replacement / prevention interactions、LayerEngine / effective power、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

修复事实：

- A 基于 matrix fresh risk pass 选择低耦合、无 FAQ、单 FU Hunt 路线。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 让 `ReadiesAllFriendlyUnits` 复用 `GetControlledPublicFieldUnitObjectIds`，只 ready friendly public field units。
- 新增 `tests/Riftbound.ConformanceTests/HuntReadyGuardTests.cs`，覆盖成功 zero-target friendly public field-unit ready、equipment / spell / rune / face-down standby / dirty controller / enemy unit exclusion，以及 explicit target no-mutation rejection。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HuntReadyGuardTests|FullyQualifiedName~HuntAndReadies|FullyQualifiedName~HuntReady"`：passed 10/10。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Hunt|FullyQualifiedName~ReadyAll|FullyQualifiedName~ReadiesAll|FullyQualifiedName~UNIT_READIED|FullyQualifiedName~Overcharged|FullyQualifiedName~FieldUnit"`：passed 121/121。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3731/3731。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C62` 回填为 `HUNT_READY_FRIENDLY_UNITS_GUARD_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH62_HUNT_READY_FRIENDLY_UNITS_GUARD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH62_HUNT_READY_FRIENDLY_UNITS_GUARD_EVIDENCE.md` 记录 narrow representative guard verified 证据。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`d505dcf checkpoint: complete stage 4C hunt ready guard`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/HuntReadyGuardTests.cs`、4C-62 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 23. 阶段 4C-63 AnyUnit Target Scope Guard Verified Representative

状态：**已完成代表切片收口并 checkpoint 为 `fd6ef1b`。项目整体仍 NOT READY。**

本批范围：

- 目标为 First Mate / 大副 `OGN·132/298` / cardId `31356` / `FU-abf504d74e` / `FIRST_MATE_PLAY_UNIT_READY_ANOTHER_UNIT`。
- 只补 ordinary hand `PLAY_CARD`、支付 3 mana、选择 public field unit target、stack / pass-pass 后目标变为活跃的 representative AnyUnit target-scope guard。
- 不实现 / 不宣称 all AnyUnit card texts / modes、formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR stack lifecycle、PaymentEngine、LayerEngine / effective power、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

修复事实：

- A 基于 matrix fresh risk pass 选择低耦合、无 FAQ、单 FU First Mate 路线。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 让 `CardTargetScopes.AnyUnit` 复用 public field-unit guard，只接受由所在区域玩家控制的公开场上单位；该 guard 排除 face-down、standby、equipment、spell、rune 与 dirty controller objects，并保留 trait-only unit object 兼容。
- 新增 `tests/Riftbound.ConformanceTests/AnyUnitTargetScopeGuardTests.cs`，覆盖成功 target ready、equipment / spell / rune / face-down standby / face-up standby / dirty controller / hand / stale targets no-mutation rejection，以及无 `TargetRequiredTag` 的 AnyUnit non-unit regression。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AnyUnitTargetScopeGuardTests|FullyQualifiedName~CoreRuleEnginePlaysFirstMateReadyAnotherUnit|FullyQualifiedName~CoreRuleEnginePlaysTheCurtainRises|FullyQualifiedName~CoreRuleEnginePlaysMirrorImageCreatesEphemeralCopyInBase"`：passed 15/15。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AnyUnitTargetScopeGuardTests|FullyQualifiedName~FirstMate|FullyQualifiedName~CurtainRises|FullyQualifiedName~Beatdown|FullyQualifiedName~AnyUnit|FullyQualifiedName~TargetScope|FullyQualifiedName~TargetGuard"`：passed 16/16。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3742/3742。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C63` 回填为 `ANY_UNIT_TARGET_SCOPE_GUARD_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH63_ANY_UNIT_TARGET_SCOPE_GUARD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH63_ANY_UNIT_TARGET_SCOPE_GUARD_EVIDENCE.md` 记录 narrow representative guard verified 证据。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`fd6ef1b checkpoint: complete stage 4C any unit target guard`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/AnyUnitTargetScopeGuardTests.cs`、4C-63 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 24. 阶段 4C-64 EnemyBattlefieldUnit Target Scope Guard Verified Representative

状态：**已完成代表切片收口并 checkpoint 为 `d2dfe96`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Megashark Cannon / 怒海大鲨炮 `OGN·092/298` / cardId `31310` / `FU-6d67456a80` / `MEGASHARK_CANNON_PLAY_UNIT_DAMAGE_6_ENEMY_BATTLEFIELD`。
- 只补 ordinary hand `PLAY_CARD`、支付 6 mana、选择 enemy public battlefield unit target、stack / pass-pass 后目标受到 6 点伤害的 representative EnemyBattlefieldUnit direct target-scope guard。
- 不实现 / 不宣称 composite target scopes、all EnemyBattlefieldUnit card texts / alternate modes、formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR stack lifecycle、PaymentEngine、LayerEngine、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

修复事实：

- A 基于 matrix fresh risk pass 选择低耦合、无 FAQ、单 FU Megashark Cannon 路线。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 让 `CardTargetScopes.EnemyBattlefieldUnit` 复用 enemy public battlefield-unit guard，只接受敌方公开战场单位；该 guard 排除 face-down、standby、equipment、spell、rune、dirty controller、friendly battlefield、base、hand 与 stale targets。
- 新增 `tests/Riftbound.ConformanceTests/EnemyBattlefieldUnitTargetScopeGuardTests.cs`，覆盖成功 target damage、equipment / spell / rune / face-down standby / face-up standby / dirty controller / friendly battlefield / enemy base / hand / stale targets no-mutation rejection，以及无 `TargetRequiredTag` 的 EnemyBattlefieldUnit non-unit regression。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~EnemyBattlefieldUnitTargetScopeGuardTests|FullyQualifiedName~CoreRuleEnginePlaysMegasharkCannonDamageEnemyBattlefield|FullyQualifiedName~CoreRuleEngineRejectsMegasharkCannonWhenTargetIsFriendlyUnit|FullyQualifiedName~CoreRuleEnginePlaysCrescentStrikeTargetPlusSplash|FullyQualifiedName~CoreRuleEngineRejectsCrescentStrikeAgainstFriendlyOrBaseUnit|FullyQualifiedName~P4CrescentStrike"`：passed 18/18。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~EnemyBattlefieldUnitTargetScopeGuardTests|FullyQualifiedName~Megashark|FullyQualifiedName~CrescentStrike|FullyQualifiedName~ZenithBlade|FullyQualifiedName~CharmMoveToBaseGuardTests|FullyQualifiedName~EnemyBattlefieldUnit|FullyQualifiedName~HostileTakeoverGuardTests|FullyQualifiedName~IsolateMoveToBaseGuardTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~RideTheWindMoveGuardTests"`：passed 82/82。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C64` 回填为 `ENEMY_BATTLEFIELD_UNIT_TARGET_SCOPE_GUARD_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH64_ENEMY_BATTLEFIELD_UNIT_TARGET_SCOPE_GUARD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH64_ENEMY_BATTLEFIELD_UNIT_TARGET_SCOPE_GUARD_EVIDENCE.md` 记录 narrow representative guard verified 证据。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`d2dfe96 checkpoint: complete stage 4C enemy battlefield target guard`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/EnemyBattlefieldUnitTargetScopeGuardTests.cs`、4C-64 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 25. 阶段 4C-65 Demacia Envoy Experience Static Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `a806e69`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Demacia Envoy / 德玛西亚使节 `UNL-092/219` / cardId `34630` / `FU-d68c203b01` / `DEMACIA_ENVOY_PLAY_UNIT_GAIN_EXPERIENCE_STATIC`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 2 mana、0 目标入栈、stack / pass-pass 后源牌进入基地成为 2 战力单位、控制者获得 1 经验。
- 不实现 / 不宣称 all experience cards / amounts、experience payment / optional cost replacement、level-up LayerEngine、battlefield conquest / hold Hunt experience、activate ability experience gain、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择低耦合、无 FAQ、单 FU Demacia Envoy 路线。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `UNL-092/219` 为 direct card behavior：`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 2`、`GainExperienceOnPlay: 1`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-demacia-envoy-experience-static.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysP2PreflightFixture|FullyQualifiedName~P4FixedExperienceGainOnPlayUpdatesControllerExperience|FullyQualifiedName~resource-experience|FullyQualifiedName~ResourceExperience"`：passed 4/4。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Demacia|FullyQualifiedName~Experience|FullyQualifiedName~GainExperience|FullyQualifiedName~LevelThreshold"`：passed 37/37。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C65` 回填为 `EXPERIENCE_STATIC_PLAY_UNIT_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH65_DEMACIA_ENVOY_EXPERIENCE_STATIC_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH65_DEMACIA_ENVOY_EXPERIENCE_STATIC_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`a806e69 checkpoint: complete stage 4C demacia envoy experience evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-65 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 26. 阶段 4C-66 Tibbers All Battlefield Damage Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `9bc5b81`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Tibbers / 提伯斯 `OGS·018/024` / cardId `31597` / `FU-c168bd394c` / `TIBBERS_PLAY_UNIT_DAMAGE_ALL_BATTLEFIELD_UNITS_3`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 8 mana、0 目标入栈、stack / pass-pass 后源牌进入基地成为 7 战力单位、公开战场单位各受到 3 点伤害。
- 不实现 / 不宣称 all battlefield-wide damage cards / amounts、damage prevention / replacement / cleanup、lethal trigger interactions、formal multi-battlefield precision、PaymentEngine、LayerEngine、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择低耦合、无 FAQ、单 FU Tibbers 路线。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `OGS·018/024` 为 direct card behavior：`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 7`、`DamagesAllBattlefieldUnits: true`、`DamageAmount: 3`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-tibbers-damage-all-battlefield-units.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysTibbersDamageAllBattlefieldUnits|FullyQualifiedName~CoreRuleEngineTibbersResolutionSkipsOpponentControlledBattlefieldObject|FullyQualifiedName~CoreRuleEngineRejectsTibbersWhenTargetsAreProvided"`：passed 3/3。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Tibbers|FullyQualifiedName~BladeWhirlwind|FullyQualifiedName~DamageAllBattlefield|FullyQualifiedName~OverchargedEnergy|FullyQualifiedName~FirestormEnemyBattlefieldDamageGuardTests|FullyQualifiedName~CrescentStrike|FullyQualifiedName~EnemyBattlefield"`：passed 63/63。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C66` 回填为 `TIBBERS_ALL_BATTLEFIELD_DAMAGE_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH66_TIBBERS_ALL_BATTLEFIELD_DAMAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH66_TIBBERS_ALL_BATTLEFIELD_DAMAGE_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`9bc5b81 checkpoint: complete stage 4C tibbers damage evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-66 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 27. 阶段 4C-67 Bubblebot Ready Friendly Mechanical Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `3691e1d`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Bubblebot / 泡泡机 `SFD·062/221` / cardId `33142` / `FU-3f5a9ef0e0` / `BUBBLEBOT_PLAY_UNIT_READY_FRIENDLY_MECHANICAL`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 3 mana、选择另一名友方公开机械单位、stack / pass-pass 后源牌进入基地成为 3 战力单位、目标机械单位变为活跃。
- 不实现 / 不宣称 all friendly Mechanical ready cards / modes、full FriendlyUnit target-scope matrix、formal multi-battlefield precision、PaymentEngine、readiness replacement / prevention、LayerEngine、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择低耦合、无 FAQ、单 FU Bubblebot 路线。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `SFD·062/221` 为 direct card behavior：`TargetScope: FriendlyUnit`、`TargetRequiredTag: CARD_TYPE:UNIT|机械`、`ReadiesTarget: true`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 3`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-bubblebot-ready-friendly-mechanical.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBubblebotReadyFriendlyMechanical|FullyQualifiedName~CoreRuleEngineRejectsBubblebotWhenTargetIsNotMechanical"`：passed 2/2。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Bubblebot|FullyQualifiedName~Mechanical|FullyQualifiedName~FirstMate|FullyQualifiedName~HuntReady|FullyQualifiedName~AnyUnitTargetScopeGuardTests"`：passed 32/32。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C67` 回填为 `BUBBLEBOT_READY_FRIENDLY_MECHANICAL_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH67_BUBBLEBOT_READY_FRIENDLY_MECHANICAL_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH67_BUBBLEBOT_READY_FRIENDLY_MECHANICAL_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`3691e1d checkpoint: complete stage 4C bubblebot ready evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-67 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 28. 阶段 4C-68 Treasure Golem Create Four Gold Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `b7bf537`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Treasure Golem / 宝藏魔像 `SFD·174/221` / cardId `33270` / `FU-7472703e56` / `TREASURE_GOLEM_PLAY_UNIT_CREATE_FOUR_GOLD`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 8 mana、0 目标入栈、stack / pass-pass 后源牌进入基地成为 9 战力单位，并创建四个休眠的 Gold equipment token。
- 不实现 / 不宣称 all Gold-token creation cards / alternate token counts、complete destination selection、Gold equipment spend / activation / extra-mana interactions、PaymentEngine、equipment cleanup / replacement、LayerEngine、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择低耦合、无 FAQ、单 FU Treasure Golem 路线，用于覆盖 Gold equipment token creation representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `SFD·174/221` 为 direct card behavior：`CreatedBaseEquipmentTokenCount: 4`、`CreatedBaseEquipmentTokenName: 金币`、`CreatedBaseEquipmentTokenTags: CARD_TYPE:EQUIPMENT`、`CreatedBaseEquipmentTokenIsExhausted: true`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 9`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-treasure-golem-create-four-gold.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期；`p4-play-treasure-golem-target-rejected.fixture.json` 已记录 unexpected-target no-mutation guard。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysTreasureGolemCreateFourGold|FullyQualifiedName~CoreRuleEngineRejectsTreasureGolemWhenTargetsAreProvided|FullyQualifiedName~P4TreasureGolemTargetRejectedFixture"`：passed 3/3。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TreasureGolem|FullyQualifiedName~Gold|FullyQualifiedName~JungleAmbush|FullyQualifiedName~BloodMoney|FullyQualifiedName~PainfulPayoff|FullyQualifiedName~HonestBroker"`：passed 30/30。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C68` 回填为 `TREASURE_GOLEM_CREATE_FOUR_GOLD_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH68_TREASURE_GOLEM_CREATE_FOUR_GOLD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH68_TREASURE_GOLEM_CREATE_FOUR_GOLD_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`b7bf537 checkpoint: complete stage 4C treasure golem gold evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-68 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 29. 阶段 4C-69 Faithful Craftsman Create Minion Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `913759e`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Faithful Craftsman / 忠实的工坊主 `OGN·211/298` / cardId `31447` / `FU-2e2a00f575` / `FAITHFUL_CRAFTSMAN_PLAY_UNIT_CREATE_MINION`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 3 mana、0 目标入栈、stack / pass-pass 后源牌进入基地成为 2 战力单位，并创建一名未横置 1 战力 Minion unit token。
- 不实现 / 不宣称 all Minion-token creation cards / alternate token counts、complete destination selection、full token subtype/family taxonomy、PaymentEngine、token replacement / prevention / cleanup、LayerEngine、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择低耦合、无 FAQ、单 FU Faithful Craftsman 路线，用于覆盖 Minion unit token creation representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `OGN·211/298` 为 direct card behavior：`CreatedBaseUnitTokenCount: 1`、`CreatedBaseUnitTokenPower: 1`、`CreatedBaseUnitTokenName: 随从`、`CreatedBaseUnitTokenTags: CARD_TYPE:UNIT`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 2`。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 已在 created unit token route 对 `随从` token 追加 `TOKEN_FAMILY:MINION`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-faithful-craftsman-create-minion.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期；`CoreRuleEngineRejectsFaithfulCraftsmanWhenTargetsAreProvided` 已记录 unexpected-target no-mutation guard。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysFaithfulCraftsmanCreateMinion|FullyQualifiedName~CoreRuleEngineRejectsFaithfulCraftsmanWhenTargetsAreProvided"`：passed 2/2。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~FaithfulCraftsman|FullyQualifiedName~MinionTokenFamily|FullyQualifiedName~CommonCause|FullyQualifiedName~FutureForge|FullyQualifiedName~VanguardCaptain|FullyQualifiedName~MechanicalTrickster|FullyQualifiedName~Viktor"`：passed 18/18。此前并行跑 focused 与 regression 时触发一次 `obj/ref` 文件锁；顺序重跑后通过，非测试失败。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C69` 回填为 `FAITHFUL_CRAFTSMAN_CREATE_MINION_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH69_FAITHFUL_CRAFTSMAN_CREATE_MINION_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH69_FAITHFUL_CRAFTSMAN_CREATE_MINION_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`913759e checkpoint: complete stage 4C faithful craftsman minion evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-69 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 30. 阶段 4C-70 Skullcrack Battlefield Stun Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `8dfc4b5`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Skullcrack / 强手裂颅 `OGN·220/298` / cardId `31458` / `FU-ee886701e4` / `SKULLCRACK_STUN_FRIENDLY_AND_ENEMY_BATTLEFIELD_UNITS`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 2 mana、按顺序选择一名友方战场单位和一名敌方战场单位、stack / pass-pass 后对两名目标施加 `STUNNED`。
- 不实现 / 不宣称 same-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR target / stack lifecycle、PaymentEngine、status duration / cleanup / replacement / prevention、LayerEngine、hidden-info / redaction matrix、完整 FAQ 裁定、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、已有 FAQ 引用、单 FU 的 Skullcrack 路线，用于覆盖 both-sides battlefield stun representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `OGN·220/298` 为 direct card behavior：`Cost: 2`、`TargetCount: 2`、`StatusEffectId: STUNNED`、`TargetScope: FriendlyBattlefieldThenEnemyBattlefieldUnits`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-skullcrack-stun-friendly-and-enemy-battlefield-units.fixture.json` 已记录官方卡面、核心规则 / FAQ 证据和完整 pass-pass 结算预期；`CoreRuleEngineRejectsSkullcrackAgainstWrongOrderOrBaseUnits` 已记录 wrong-order / base-unit / same-side target no-mutation guard。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysSkullcrackStunsFriendlyAndEnemyBattlefieldUnits|FullyQualifiedName~CoreRuleEngineRejectsSkullcrackAgainstWrongOrderOrBaseUnits"`：passed 2/2。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Skullcrack|FullyQualifiedName~RunePrison|FullyQualifiedName~Kerplunk|FullyQualifiedName~HeroicCharge|FullyQualifiedName~ZenithBlade|FullyQualifiedName~Stun|FullyQualifiedName~Stunned|FullyQualifiedName~SolariLeader|FullyQualifiedName~Stunning"`：passed 64/64。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C70` 回填为 `SKULLCRACK_FRIENDLY_AND_ENEMY_BATTLEFIELD_STUN_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH70_SKULLCRACK_BATTLEFIELD_STUN_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH70_SKULLCRACK_BATTLEFIELD_STUN_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`8dfc4b5 checkpoint: complete stage 4C skullcrack battlefield stun evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-70 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 31. 阶段 4C-71 Diana Spell Duel Static Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `5c94f4e`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Diana / 黛安娜 `UNL-079/219` / cardId `34612` / `FU-4215291160` / `DIANA_SPELL_DUEL_INSIGHT_STATIC`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 3 mana、0 目标入栈、stack / pass-pass 后源牌进入基地成为 3 战力 `CARD_TYPE:UNIT|巨神峰`。
- 不实现 / 不宣称 actual spell-duel Insight trigger / payment / reveal / draw、`UNL-079a/219` alt A FU、standby / reaction、quick / spell-duel timing、full FEPR lifecycle、PaymentEngine、LayerEngine、hidden-info / top-card redaction matrix、FAQ、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、无 FAQ 的 Diana spell-duel-static route，用于覆盖 keyword/static source-unit ordinary play representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `UNL-079/219` 为 direct card behavior：`Cost: 3`、`TargetCount: 0`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 3`、`SourceUnitTags: 巨神峰`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-diana-spell-duel-static.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期；`CoreRuleEnginePlaysKeywordOnlySourceUnit` 与 `CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided` 已覆盖合法路线和带目标拒绝路线。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysKeywordOnlySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided"`：passed 459/459。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Diana|FullyQualifiedName~SpellDuel|FullyQualifiedName~Insight|FullyQualifiedName~PassFocus|FullyQualifiedName~Swift"`：passed 45/45。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C71` 回填为 `DIANA_SPELL_DUEL_STATIC_PLAY_UNIT_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH71_DIANA_SPELL_DUEL_STATIC_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH71_DIANA_SPELL_DUEL_STATIC_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`5c94f4e checkpoint: complete stage 4C diana spell duel static evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-71 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 32. 阶段 4C-72 Hextech Ray Damage Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `2db719e`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Hextech Ray / 海克斯射线 `OGN·009/298` / cardId `31215` / `FU-441cb9fb7f` / `HEXTECH_RAY_DAMAGE_3`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 1 mana、选择一名战场单位、stack / pass-pass 后造成 3 点伤害并进入废牌堆，同时记录 end-turn damage cleanup 与 Swift spell-duel focus 代表路径。
- 不实现 / 不宣称完整 FAQ 裁定、complete FEPR target / stack lifecycle、全部 timing windows、完整 spell-duel lifecycle、PaymentEngine beyond ordinary pay 1、damage prevention / replacement / lethal cleanup / trigger matrix、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、已有 FAQ 引用、单 FU 的 Hextech Ray 路线，用于覆盖 direct damage stack、cleanup 与 Swift spell-duel focus representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `OGN·009/298` 为 direct card behavior：`Cost: 1`、`TargetCount: 1`、`DamageAmount: 3`、`CanPlayDuringSpellDuel: true`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-hextech-ray-damage-stack.fixture.json`、`p2-preflight-hextech-ray-damage-clears-end-turn.fixture.json` 与 `p6-play-swift-hextech-ray-in-spell-duel-focus.fixture.json` 已记录官方卡面、核心规则 / FAQ 证据和代表性结算预期；`CoreRuleEngineRejectsBattlefieldOnlySpellWhenTargetIsBaseUnit` 已记录基地单位目标拒绝 no-mutation guard。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysHextechRayThroughStack|FullyQualifiedName~CoreRuleEngineClearsHextechRayDamageAtEndTurn|FullyQualifiedName~P6SwiftKeywordAllowsHextechRayInSpellDuelFocusWindow|FullyQualifiedName~CoreRuleEngineRejectsBattlefieldOnlySpellWhenTargetIsBaseUnit"`：passed 4/4。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HextechRay|FullyQualifiedName~Swift|FullyQualifiedName~Damage|FullyQualifiedName~Cleanup|FullyQualifiedName~EndTurn|FullyQualifiedName~BattlefieldOnlySpell"`：passed 202/202。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C72` 回填为 `HEXTECH_RAY_DAMAGE_STACK_SWIFT_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH72_HEXTECH_RAY_DAMAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH72_HEXTECH_RAY_DAMAGE_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`2db719e checkpoint: complete stage 4C hextech ray damage evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-72 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 33. 阶段 4C-73 Plunder Alley Battlefield Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `dbacc2d`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Plunder Alley / 劫掠船巷 `OGN·285/298` / cardId `31530` / `FU-90673ef9fd` / `BATTLEFIELD_RULE_DOMAIN`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 development seed 中防守战场后选择一名防守单位移回基地的 battlefield-domain representative route。
- 不实现 / 不宣称完整 `BATTLEFIELD_RULE_DOMAIN`、完整 `JFAQ-251023 p5-p6` 战场生命周期 / 清理裁定、battle / spell-duel / assign-combat-damage lifecycle、control freeze/release 与 zone movement matrix、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、已有 FAQ 引用、单 FU 的 Plunder Alley 路线，用于覆盖 battlefield-domain defend move-to-base representative evidence。
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` 已把 `OGN·285/298` 纳入 implemented battlefield rule cards；`src/Riftbound.Engine/CoreRuleEngine.cs` 已识别它为 `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE` source。
- `src/Riftbound.Engine/MatchSession.cs` development seed `battlefield-defend-move-to-base` 使用 `P2-BATTLEFIELD-PLUNDER-ALLEY` / `OGN·285/298`；既有 core / Hub tests 覆盖 prompt 候选、非法目标拒绝、合法触发与 dirty source guard。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldDefendMovesChosenSurvivingDefenderToBase|FullyQualifiedName~P79BattlefieldDefendMoveToBaseRejectsAttackerControlledBattlefield|FullyQualifiedName~P79BattlefieldDefendMoveToBaseSeedOffersBattlefieldDestinationAndChoice"`：passed 3/3。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldDefend|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~DeclareBattle|FullyQualifiedName~BattlefieldControl|FullyQualifiedName~Plunder|FullyQualifiedName~BattlefieldDestination"`：passed 137/137。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C73` 回填为 `PLUNDER_ALLEY_BATTLEFIELD_DEFEND_MOVE_TO_BASE_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH73_PLUNDER_ALLEY_BATTLEFIELD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH73_PLUNDER_ALLEY_BATTLEFIELD_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`dbacc2d checkpoint: complete stage 4C plunder alley battlefield evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-73 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 34. 阶段 4C-74 Sivir Haste Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `fc3383f`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Sivir / 希维尔 `SFD·143/221`、`SFD·143a/221` / cardId `33234`、`33235` / `FU-5bcc4063c2` / `SIVIR_PLAY_UNIT_NO_OPTIONAL_HASTE`、`SIVIR_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand no-optional `PLAY_CARD`、支付基础 4 mana、0 目标入栈、stack / pass-pass 后源牌进入基地成为 4 战力 `CARD_TYPE:UNIT|急速`，以及代表 `HASTE_READY` 额外 1 mana + 1 purple power active-entry branch。
- 不实现 / 不宣称 complete PaymentEngine、wild-rune count、+2 power / Roam branch、LayerEngine / continuous effects、cleanup queue / replacement effects、control-zone movement、full FEPR timing / targeting、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、无 FAQ、共享 oracle 的 Sivir Haste route，用于覆盖 Haste optional payment representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `SFD·143/221` 与 `SFD·143a/221` 为 direct card behavior：`Cost: 4`、`TargetCount: 0`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 4`、`SourceUnitTags: 急速`、`HasteReadyManaCost: 1`、`HasteReadyPowerCost: 1`、`HasteReadyPowerTrait: RuneTrait.Purple`。
- `tests/Riftbound.ConformanceTests/Fixtures` 中四个 Sivir fixtures 覆盖 no-optional 与 HASTE_READY route；`ConformanceFixtureRunnerTests` 覆盖 unexpected target rejection 与 wrong-trait power rejection。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost|FullyQualifiedName~CoreRuleEngineRejectsSourceUnitWithoutOptionalAdditionalCostWhenTargetsAreProvided|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForSivir|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForSivirAltA|FullyQualifiedName~P4HasteOptionalReadyBranchRejectsSivirWrongTraitPower"`：passed 78/78。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Sivir|FullyQualifiedName~Haste|FullyQualifiedName~PaymentResource|FullyQualifiedName~RecycleRune|FullyQualifiedName~PayCost|FullyQualifiedName~PowerByTrait"`：passed 103/103。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C74` 回填为 `SIVIR_HASTE_READY_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH74_SIVIR_HASTE_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH74_SIVIR_HASTE_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`fc3383f checkpoint: complete stage 4C sivir haste evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-74 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 35. 阶段 4C-75 Gentleman Duel Damage Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `686d51f`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Gentleman Duel / 绅士决斗 `OGS·008/024` / cardId `31587` / `FU-265c03a141` / `GENTLEMAN_DUEL_POWER_PLUS_3_THEN_MUTUAL_POWER_DAMAGE`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 6 mana、友方单位再敌方单位目标、stack / pass-pass 后友方目标本回合战力 +3，随后两名目标按当前战力互相造成伤害，并摧毁受到致命伤害的敌方目标。
- 不实现 / 不宣称 Swift / spell-duel timing、complete FEPR target matrix、LayerEngine / duration cleanup、replacement / prevention、battle damage assignment lifecycle、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、无 FAQ、单 FU 的 Gentleman Duel route，用于覆盖 mutual current-power damage representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `OGS·008/024` 为 direct card behavior：`Cost: 6`、`TargetCount: 2`、`TargetScope: FriendlyThenEnemyUnits`、`PowerModifierAmount: 3`、`DealsMutualTargetPowerDamage: true`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-gentleman-duel-power-then-mutual-damage.fixture.json` 已记录官方卡面、核心规则证据和代表性结算预期；`ConformanceFixtureRunnerTests` 覆盖 +3 power modifier、两条 damage events、敌方目标致命伤害摧毁和 owner graveyard placement。Duel sibling tests 覆盖共享 target-order guard。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysGentlemanDuelPowerThenMutualDamage|FullyQualifiedName~CoreRuleEnginePlaysDuelMutualPowerDamage|FullyQualifiedName~CoreRuleEngineRejectsDuelWhenTargetsAreReversed|FullyQualifiedName~P4DuelTargetOrderRejectedFixture|FullyQualifiedName~CoreRuleEnginePlaysMarchingOrdersEchoMutualPowerDamage|FullyQualifiedName~CoreRuleEnginePlaysClashOfGiantsMutualPowerDamage"`：passed 6/6。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gentleman|FullyQualifiedName~Duel|FullyQualifiedName~MutualPower|FullyQualifiedName~PowerModified|FullyQualifiedName~Damage|FullyQualifiedName~Cleanup|FullyQualifiedName~ClashOfGiants|FullyQualifiedName~MarchingOrders|FullyQualifiedName~FriendlyThenEnemy"`：passed 203/203。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C75` 回填为 `GENTLEMAN_DUEL_POWER_THEN_MUTUAL_DAMAGE_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH75_GENTLEMAN_DUEL_DAMAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH75_GENTLEMAN_DUEL_DAMAGE_EVIDENCE.md` 记录 narrow representative evidence。
- 顺手修正 `stage4CBatch74SivirHasteEvidence.nextPressureCandidateFunctionalUnits` 中 Syndra / Long Sword follow-on candidate 的 cardNo 文档错误，不改变 4C-74 evidence 结论。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`686d51f checkpoint: complete stage 4C gentleman duel damage evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-75 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 36. 阶段 4C-76 Long Sword Equipment Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `5ef4a45`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Long Sword / 长剑 `SFD·022/221` / cardId `33095` / `FU-5accdd09f9` / `LONG_SWORD_AGILE_PLAY_EQUIPMENT`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 2 mana、0 目标入栈、stack / pass-pass 后源牌进入控制者基地成为 `CARD_TYPE:EQUIPMENT` / `武装` / `灵便` 装备对象，显式目标拒绝、最小 `ASSEMBLE_RED` 贴附与 owner/controller 身份保持代表证据。
- 不实现 / 不宣称 Agile reaction attach、complete equipment lifecycle、LayerEngine equipment modifiers、PaymentEngine beyond represented routes、replacement / prevention / cleanup interactions、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、无 FAQ、单 FU 且范围较窄的 Long Sword route，用于覆盖 equipment play / target rejection / minimal assemble identity representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `SFD·022/221` 为 direct card behavior：`Cost: 2`、`TargetCount: 0`、`PlaysSourceToBaseAsEquipment: true`、`SourceEquipmentTags: 武装|灵便`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-long-sword-agile-equipment.fixture.json`、`p4-play-long-sword-target-rejected.fixture.json`、`p4-assemble-equipment-long-sword-attach.fixture.json` 与 `p5-equipment-state-assemble-long-sword-owner-controller.fixture.json` 已记录普通装备打出、显式目标拒绝、最小贴附和身份保持证据；随动 / 脱离 fixtures 只作为邻接回归。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LongSword|FullyQualifiedName~P4AssembleEquipmentCommandLongSwordAttachFixture|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixture|FullyQualifiedName~P5MoveUnitAttachedEquipmentFollowsHostFixture|FullyQualifiedName~P5EquipmentDetachesWhenHostDestroyedFixture"`：passed 11/11。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LongSword|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~Equipment|FullyQualifiedName~Attached|FullyQualifiedName~Attach|FullyQualifiedName~MoveUnit|FullyQualifiedName~NonUnitSource"`：passed 336/336。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C76` 回填为 `LONG_SWORD_AGILE_EQUIPMENT_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH76_LONG_SWORD_EQUIPMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH76_LONG_SWORD_EQUIPMENT_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`5ef4a45 checkpoint: complete stage 4C long sword equipment evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-76 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 37. 阶段 4C-77 Syndra Spell Duel Echo Static Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `db4eae7`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Syndra / 辛德拉 `UNL-146/219` / cardId `34691` / `FU-bf350b5796` / `SYNDRA_SPELL_DUEL_ECHO_STATIC`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 6 mana、0 目标入栈、stack / pass-pass 后源牌进入控制者基地成为 6 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象，并记录共享 source-unit 带目标拒绝证据。
- 不实现 / 不宣称 actual spell-duel detection、Echo 2 purple grant、granted Echo payment/repeat、complete spell-duel lifecycle、PaymentEngine、LayerEngine、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、无 FAQ、单 FU 的 Syndra route，用于覆盖 spell-duel / Echo static 文本的普通入场代表证据。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `UNL-146/219` 为 direct card behavior：`Cost: 6`、`TargetCount: 0`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 6`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-syndra-spell-duel-echo-static.fixture.json` 已记录官方卡面、核心规则证据和代表性结算预期；共享 `CoreRuleEnginePlaysVanillaSourceUnit` 与 `CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided` 覆盖普通入场和带目标拒绝。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysVanillaSourceUnit|FullyQualifiedName~CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided|FullyQualifiedName~SpellDuel|FullyQualifiedName~Echo"`：passed 361/361。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpellDuel|FullyQualifiedName~Echo|FullyQualifiedName~Payment|FullyQualifiedName~PayCost|FullyQualifiedName~PowerByTrait|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~VanillaSourceUnit"`：passed 553/553。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C77` 回填为 `SYNDRA_SPELL_DUEL_ECHO_STATIC_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH77_SYNDRA_SPELL_DUEL_ECHO_STATIC_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH77_SYNDRA_SPELL_DUEL_ECHO_STATIC_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`db4eae7 checkpoint: complete stage 4C syndra spell duel echo static evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-77 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 38. 阶段 4C-78 Moon Rise Power Minus 2 Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `ca0fd20`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Moon Rise / 月之降临 `UNL-198/219` / cardId `34751` / `FU-4329e00e20` / `MOON_RISE_ENEMY_BATTLEFIELD_MINUS_2_NO_MOVE`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付 3 mana、0 目标入栈、stack / pass-pass 后敌方战场单位本回合内战力 -2，当前单战场区域模型下跳过可选敌方单位移动，并保留负战力边界。
- 不实现 / 不宣称 full multi-battlefield selection、optional enemy movement、complete control-zone movement matrix、complete cleanup replacement / duration-effect matrix、complete battle / spell-duel lifecycle、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、无 FAQ、单 FU 的 Moon Rise route，用于覆盖 enemy battlefield power-modifier / negative-power representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `UNL-198/219` 为 direct card behavior：`Cost: 3`、`TargetCount: 0`、`PowerModifierAmount: -2`、`ModifiesAllEnemyBattlefieldUnits: true`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-moonrise-enemy-battlefield-power-minus-2.fixture.json` 已记录官方卡面、核心规则证据和代表性结算预期；`ConformanceFixtureRunnerTests` 覆盖敌方战场单位 -2、友方战场与双方基地不受影响、负战力战斗输出和 cleanup 边界。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysMoonRiseEnemyBattlefieldPowerMinus2|FullyQualifiedName~P4DeclareBattleNegativePowerAttackerDealsNoCombatDamageAndRetainsTruePower|FullyQualifiedName~P7PostStackCleanupDoesNotDestroyUndamagedNegativePowerFieldUnit|FullyQualifiedName~P7PostStackCleanupDestroysZeroOrNegativePowerUnitWithDamage"`：passed 5/5。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MoonRise|FullyQualifiedName~PowerModifier|FullyQualifiedName~PowerMinus|FullyQualifiedName~NegativePower|FullyQualifiedName~Cleanup|FullyQualifiedName~CombatDamage|FullyQualifiedName~Stack|FullyQualifiedName~Priority"`：passed 196/196。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C78` 回填为 `MOON_RISE_ENEMY_BATTLEFIELD_MINUS_2_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH78_MOON_RISE_POWER_MINUS_2_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH78_MOON_RISE_POWER_MINUS_2_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`ca0fd20 checkpoint: complete stage 4C moon rise power minus 2 evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-78 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 39. 阶段 4C-79 Forced Conscription Control Small Enemy Recall Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `c6e5b2a`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Forced Conscription / 强制征召 `UNL-140/219` / cardId `34683` / `FU-0681eefc4e` / `FORCED_CONSCRIPTION_CONTROL_SMALL_ENEMY_RECALL`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、不支付 5 experience optional cost、支付基础 5 mana、选择敌方战场 3 战力及以下 `CARD_TYPE:UNIT` 单位、stack / pass-pass 后获得控制权、休眠并召回到控制者基地，外加 4 战力目标拒绝和 dirty already-controlled enemy-zone target guard。
- 不实现 / 不宣称 optional 5 experience any-enemy-unit branch、complete owner/controller model、complete control-zone movement matrix、complete cleanup replacement / duration-effect matrix、complete PaymentEngine optional-cost semantics、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、无 FAQ、单 FU 的 Forced Conscription route，用于覆盖 control small enemy recall representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `UNL-140/219` 为 direct card behavior：`Cost: 5`、`TargetCount: 1`、`TargetScope: EnemyBattlefieldUnit`、`MaxTargetPower: 3`、`TargetRequiredTag: CARD_TYPE:UNIT`、`GainsControlOfTargetToBase: true`、`ExhaustsControlledTarget: true`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-forced-conscription-control-small-enemy-recall.fixture.json` 已记录官方卡面、核心规则证据和代表性结算预期；`ConformanceFixtureRunnerTests` 覆盖 control-to-base route、4-power rejection、dirty already-controlled enemy-zone target guard，并通过 Taken For A Ride / Hostile Takeover 相邻控制权回归。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ForcedConscription|FullyQualifiedName~TakenForARide|FullyQualifiedName~HostileTakeover"`：passed 18/18。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Control|FullyQualifiedName~Battlefield|FullyQualifiedName~MoveUnit|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"`：passed 1718/1718。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：rerun passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C79` 回填为 `FORCED_CONSCRIPTION_CONTROL_SMALL_ENEMY_RECALL_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH79_FORCED_CONSCRIPTION_CONTROL_SMALL_ENEMY_RECALL_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH79_FORCED_CONSCRIPTION_CONTROL_SMALL_ENEMY_RECALL_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`c6e5b2a checkpoint: complete stage 4C forced conscription evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-79 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 40. 阶段 4C-80 Bullet Time Power Damage Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `1193517`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Bullet Time / 弹幕时间 `OGN·268/298` / cardId `31511` / `FU-b646702ec0` / `BULLET_TIME_DAMAGE_ENEMY_BATTLEFIELD_UNITS_BY_POWER_SPENT`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付基础 1 mana 与 `SPEND_POWER` 金额、0 目标入栈、stack `damageAmount` 来自支付符能、stack / pass-pass 后对敌方战场单位造成等量伤害，外加符能不足拒绝和 typed power / `RECYCLE_RUNE` 支付资源守卫。
- 不实现 / 不宣称 complete `JFAQ-251023 p6`、complete battle / spell-duel lifecycle、complete `ASSIGN_COMBAT_DAMAGE` timing interactions、complete PaymentEngine、complete FEPR target / stack / timing windows、noncombat damage replacement / layer matrix、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、FAQ-mentioned、单 FU 的 Bullet Time route，用于覆盖 power-spent enemy battlefield damage representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `OGN·268/298` 为 direct card behavior：`Cost: 1`、`TargetCount: 0`、`EffectKind: BULLET_TIME_DAMAGE_ENEMY_BATTLEFIELD_UNITS_BY_POWER_SPENT`、`DamagesAllEnemyBattlefieldUnits: true`、`DamageAmountFromOptionalPowerCost: true`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-bullet-time-power-damage-enemy-battlefield.fixture.json` 已记录官方卡面、核心规则证据和代表性结算预期；`ConformanceFixtureRunnerTests` 覆盖 pay-power damage route、insufficient power rejection，并通过 typed power / payment resource / recycle rune 相邻支付回归。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BulletTime|FullyQualifiedName~TypedPowerPayment|FullyQualifiedName~PaymentResource|FullyQualifiedName~RecycleRune"`：passed 24/24。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Payment|FullyQualifiedName~PayCost|FullyQualifiedName~PowerByTrait|FullyQualifiedName~SpendPower|FullyQualifiedName~RecycleRune|FullyQualifiedName~DamageAllEnemyBattlefield|FullyQualifiedName~EnemyBattlefield|FullyQualifiedName~Firestorm|FullyQualifiedName~CrescentStrike|FullyQualifiedName~Stack|FullyQualifiedName~Priority"`：passed 250/250。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C80` 回填为 `BULLET_TIME_POWER_DAMAGE_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH80_BULLET_TIME_POWER_DAMAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH80_BULLET_TIME_POWER_DAMAGE_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`1193517 checkpoint: complete stage 4C bullet time power damage evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-80 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 41. 阶段 4C-81 Flame Chompers Source Unit Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `a2aae28`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Flame Chompers / 嚼火者手雷 `OGN·006/298` / cardId `31210` / `FU-af2c43c430` / `OGN_FLAME_CHOMPERS_DISCARD_ALT_PLAY_UNIT`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付基础 3 mana、0 目标入栈、stack / pass-pass 后源牌进入控制者基地，成为 3 战力 `CARD_TYPE:UNIT` 单位对象，外加通用 source-unit target rejection 和官方开局 hand / card candidate 可见性。
- 不实现 / 不宣称 discard replacement pay-red-power play path、complete replacement / cleanup queue semantics、complete PaymentEngine、complete FEPR target / stack / timing windows、alternate source-zone play interactions、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、无 FAQ、单 FU 的 Flame Chompers route，用于覆盖 ordinary source-unit-to-base representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `OGN·006/298` 为 direct card behavior：`Cost: 3`、`TargetCount: 0`、`EffectKind: OGN_FLAME_CHOMPERS_DISCARD_ALT_PLAY_UNIT`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 3`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-flame-chompers-discard-static.fixture.json` 已记录官方卡面、核心规则证据和代表性结算预期；`ConformanceFixtureRunnerTests` 覆盖 ordinary source-unit route、target rejection，并通过 official opening / source-unit / discard / cleanup 相邻回归。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysVanillaSourceUnit|FullyQualifiedName~CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided|FullyQualifiedName~OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub"`：passed 306/306。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~VanillaSourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~PlayCard|FullyQualifiedName~Discard|FullyQualifiedName~Cleanup|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"`：passed 1954/1954。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C81` 回填为 `FLAME_CHOMPERS_SOURCE_UNIT_TO_BASE_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH81_FLAME_CHOMPERS_SOURCE_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH81_FLAME_CHOMPERS_SOURCE_UNIT_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`a2aae28 checkpoint: complete stage 4C flame chompers source unit evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-81 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 42. 阶段 4C-82 Duel Mutual Power Damage Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `4050533`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Duel / 决斗 `OGN·128/298` / cardId `31352` / `FU-2779c06158` / `DUEL_MUTUAL_POWER_DAMAGE`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付基础 2 mana、友方单位 then 敌方单位目标顺序、stack / pass-pass mutual current-power damage、敌方目标致命清理和反向目标顺序拒绝。
- 不实现 / 不宣称 complete battle / spell-duel lifecycle、complete `ASSIGN_COMBAT_DAMAGE` timing interactions、complete LayerEngine / continuous effects、complete FEPR target / stack / timing windows、replacement / prevention、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、无 FAQ、单 FU 的 Duel route，用于覆盖 mutual current-power damage representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `OGN·128/298` 为 direct card behavior：`Cost: 2`、`TargetCount: 2`、`TargetScope: FriendlyThenEnemyUnits`、`DealsMutualTargetPowerDamage: true`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-duel-mutual-power-damage.fixture.json` 与 `tests/Riftbound.ConformanceTests/Fixtures/p4-play-duel-target-order-rejected.fixture.json` 已记录官方卡面、核心规则证据和代表性结算 / 拒绝预期；`ConformanceFixtureRunnerTests` 覆盖 play route、reversed target order rejection 和 mutual-power adjacent routes。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~DuelMutualPowerDamage|FullyQualifiedName~RejectsDuelWhenTargetsAreReversed|FullyQualifiedName~DuelTargetOrderRejected"`：passed 3/3。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Duel|FullyQualifiedName~MutualPower|FullyQualifiedName~FriendlyThenEnemy|FullyQualifiedName~ClashOfGiants|FullyQualifiedName~MarchingOrders|FullyQualifiedName~Gentleman|FullyQualifiedName~PowerModified|FullyQualifiedName~Damage|FullyQualifiedName~Cleanup|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Target"`：passed 1410/1410。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C82` 回填为 `DUEL_MUTUAL_POWER_DAMAGE_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH82_DUEL_MUTUAL_POWER_DAMAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH82_DUEL_MUTUAL_POWER_DAMAGE_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`4050533 checkpoint: complete stage 4C duel mutual power damage evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-82 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 43. 阶段 4C-83 Mighty Faerie Source Unit Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `7919c42`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Mighty Faerie / 大力仙灵 `SFD·125/221` / cardId `33215` / `FU-95b4531e4e` / `MIGHTY_FAERIE_MOVE_PAYMENT_PLAY_UNIT`。
- 本批是 evidence-only overlay，不修改功能代码；只记录 ordinary hand `PLAY_CARD`、支付基础 4 mana、0 目标入栈、stack / pass-pass 后源牌进入控制者基地，成为 4 战力、带 `仙灵` 标签的 `CARD_TYPE:UNIT` 单位对象，以及当前普通手牌打出路径携带显式目标时拒绝。
- 不实现 / 不宣称 move-to-battlefield trigger、optional purple power payment、same-battlefield friendly-unit movement、complete control-zone movement matrix、complete PaymentEngine、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、无 FAQ、单 FU 的 Mighty Faerie route，用于覆盖 ordinary source-unit-to-base representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `SFD·125/221` 为 direct card behavior：`Cost: 4`、`TargetCount: 0`、`EffectKind: MIGHTY_FAERIE_MOVE_PAYMENT_PLAY_UNIT`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 4`、`SourceUnitTags: 仙灵`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-mighty-faerie-move-payment-static.fixture.json` 与 `tests/Riftbound.ConformanceTests/Fixtures/p4-play-mighty-faerie-target-rejected.fixture.json` 已记录官方卡面、核心规则证据和代表性结算 / 拒绝预期；`ConformanceFixtureRunnerTests` 覆盖 keyword/source-unit play route、target rejection 和 battlefield / movement / payment adjacent routes。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysKeywordOnlySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided|FullyQualifiedName~P4MightyFaerieTargetRejectedFixture"`：passed 460/460。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MightyFaerie|FullyQualifiedName~Faerie|FullyQualifiedName~KeywordOnlySourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~MoveUnit|FullyQualifiedName~Battlefield|FullyQualifiedName~Payment|FullyQualifiedName~PayCost|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority"`：passed 2117/2117。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C83` 回填为 `MIGHTY_FAERIE_SOURCE_UNIT_TO_BASE_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH83_MIGHTY_FAERIE_SOURCE_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH83_MIGHTY_FAERIE_SOURCE_UNIT_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`7919c42 checkpoint: complete stage 4C mighty faerie source unit evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-83 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 44. 阶段 4C-84 Heimerdinger Source Unit Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `76c2abb`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Heimerdinger / 黑默丁格 `ARC-003/006`、`OGN·111/298` / cardIds `31571`、`31329` / `FU-02075a26e3` / `ARC_HEIMERDINGER_YORDLE_STATIC_PLAY_UNIT`、`OGN_HEIMERDINGER_YORDLE_TAP_STATIC_PLAY_UNIT`。
- 本批是 evidence-only overlay，不修改功能代码；只记录两张共享条目 ordinary hand `PLAY_CARD`、支付基础 3 mana、0 目标入栈、stack / pass-pass 后源牌进入控制者基地，成为 3 战力、带 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象，以及 active-entry / keyword-source-unit 带目标打出拒绝和 ARC 官方开局候选可见性。
- 不实现 / 不宣称 copied tap skills from other friendly legends / units / equipment、complete static ability-copy model、ability ownership / controller matrix、activated / tap ability PaymentEngine integration、`SOUL-JFAQ-260114 p11/p22` regression beyond citation tracking、shared oracle full-official behavior、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择已 IMPLEMENTED_TESTED、shared oracle、FAQ-referenced 的 Heimerdinger FU，用于覆盖 shared ordinary source-unit-to-base representative evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `ARC-003/006` 与 `OGN·111/298` 为 direct card behavior：`Cost: 3`、`TargetCount: 0`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 3`、`SourceUnitTags: 约德尔人`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-arc-heimerdinger-yordle-static.fixture.json` 与 `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-heimerdinger-yordle-static.fixture.json` 已记录官方卡面、核心规则证据和代表性结算预期；`ConformanceFixtureRunnerTests` 覆盖 active-entry / keyword-source-unit play route、target rejection、official opening 和 payment / activated-ability adjacent routes。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysActiveEntrySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsActiveEntrySourceUnitWhenTargetsAreProvided|FullyQualifiedName~CoreRuleEnginePlaysKeywordOnlySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided|FullyQualifiedName~OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub"`：passed 484/484。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Heimerdinger|FullyQualifiedName~Yordle|FullyQualifiedName~ActiveEntrySourceUnit|FullyQualifiedName~KeywordOnlySourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~Tap|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Payment|FullyQualifiedName~PayCost|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~OfficialDeck"`：passed 1847/1847。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C84` 回填为 `HEIMERDINGER_SHARED_SOURCE_UNIT_TO_BASE_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH84_HEIMERDINGER_SOURCE_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH84_HEIMERDINGER_SOURCE_UNIT_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`76c2abb checkpoint: complete stage 4C heimerdinger source unit evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-84 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 45. 阶段 4C-85 Rune Resource Domain Verified Representative Evidence

状态：**已完成代表证据收口并 checkpoint 为 `f33e733`。项目整体仍 NOT READY。**

本批范围：

- 目标为 Rune Resource Domain / 符文资源域 `FU-0ec69ae7e6`、`FU-39041f4562`，代表卡为 `OGN·007/298` 炽烈符文 / cardId `31211` 与 `OGN·042/298` 翠意符文 / cardId `31252`，覆盖这两个 FU 下 OGN / SFD / UNL 的 16 个 red / blue basic rune snapshot entries。
- 本批是 evidence-only overlay，不修改功能代码；只记录官方符文卡映射到 non-play `RUNE_RESOURCE_DOMAIN`、不进入 direct `PLAY_CARD` registry、控制者基地符文通过服务端 `RECYCLE_RUNE` / `paymentResourcePowerByChoice` 作为支付资源贡献 1 点对应 trait 符能。
- 本批同步记录 typed `SPEND_POWER:red:2` 接受 red 支付资源并拒绝 blue 支付资源、generic `SPEND_POWER:2` 可接受 red / blue 任一服务端候选、double-resource requirement、over-recycle no-mutation guard、Hub development seeds 与 Chrome smoke。
- 不实现 / 不宣称 complete rune call / tap / recycle lifecycle、全部颜色/trait taxonomy、complete PaymentEngine、替代/额外费用、reaction payment windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

证据事实：

- A 基于 matrix fresh risk pass 选择剩余两个已 IMPLEMENTED_TESTED、shared oracle、无 stage4C overlay 的 Rune Resource Domain FU，用于覆盖 red / blue basic rune resource-domain payment-resource representative evidence。
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` 的 `OfficialRuleDomainBehaviorCatalog` 已将 `CardCategoryName == "符文"` 的官方卡映射到 `RUNE_RESOURCE_DOMAIN`，并保持符文卡不进入 direct `PLAY_CARD` registry。
- `src/Riftbound.Engine/MatchSession.cs` 的 `RECYCLE_RUNE` prompt metadata 与 `PlayCardPaymentResourcePowerByChoiceForBehavior` 只从控制者基地中可回收且带 `COLOR:*` 的符文生成服务端支付资源候选，并公开 `trait` / `power` metadata。
- `CardCatalogBaselineTests`、`ConformanceFixtureRunnerTests` 与 `GameHubJoinTests` 覆盖 all-rune domain mapping、red/blue trait metadata、typed/generic payment resource legality、wrong-trait rejection、multi-resource requirement、over-recycle no-mutation guard 和 Hub seed authoritative snapshot。

验证记录：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6RuneResourceDomainMapsAllRuneEntriesWithoutMakingRunesPlayableCards|FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount|FullyQualifiedName~P7PlayCardPaymentResourceContributionMetadataSeparatesTraits|FullyQualifiedName~P7PlayCardGenericPaymentResourceCanUseMixedTraitContribution|FullyQualifiedName~P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions|FullyQualifiedName~P79TypedPowerPaymentRecycleSeedOffersPartialAmountAndPlaysThroughHub|FullyQualifiedName~P79TypedPowerPaymentDoubleRecycleSeedRequiresBothResourcesAndPlaysThroughHub|FullyQualifiedName~P79TypedPowerPaymentMixedRecycleSeedExposesTraitsAndAcceptsMatchingResourceThroughHub|FullyQualifiedName~P79TypedPowerPaymentGenericMixedRecycleSeedAcceptsAnyTraitResourceThroughHub"`：passed 10/10。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RuneResourceDomain|FullyQualifiedName~RecycleRune|FullyQualifiedName~TypedPowerPayment|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~PayCost|FullyQualifiedName~PayCostWindow|FullyQualifiedName~Payment|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"`：passed 240/240。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed 3754/3754。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：passed。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：passed。

文档 / 矩阵处理：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C85` 回填为 `RUNE_RESOURCE_DOMAIN_PAYMENT_RESOURCE_REPRESENTATIVE_NOT_FULL_OFFICIAL`。
- `docs/CURRENT_STAGE4C_BATCH85_RUNE_RESOURCE_DOMAIN_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH85_RUNE_RESOURCE_DOMAIN_EVIDENCE.md` 记录 narrow representative evidence。
- `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 保持全局 **NOT READY** 结论。

Checkpoint 记录：

- 已提交：`f33e733 checkpoint: complete stage 4C rune resource domain evidence`。
- 提交前必须验证：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`、`git diff --check`、`git diff --cached --check`。
- 已纳入：4C-85 相关 docs / matrix。
- 已排除：`riftbound-dotnet.sln`，因为它是未跟踪本地 sln 文件且不属于本阶段交付。

## 46. Formal 18-Step E2E Checkpoint

状态：**formal 18-step 连续正式主流程已通过；项目整体仍 NOT READY。**

本批范围：

- 新增 `src/Riftbound.DevUi/scripts/chrome-formal-18-e2e.mjs` 与 `npm run e2e:formal-18`。
- 脚本使用两个独立 headless Chrome profile 模拟 P1 / P2，并通过 SignalR 驱动正式房间创建、提交合法标准构筑卡组、ready、mulligan、首回合资源、出牌、结算链双方让过、单位移动、重连、P2 首回合战场得分、投降和结果页。
- 脚本会重试房间号直到官方开局满足 P1 先手、P1 战场非 `OGN·290/298`、P2 战场为 `OGN·290/298`，保证同一连续正式对局能覆盖 P2 `BATTLEFIELD_FIRST_TURN_GAIN_SCORE`。
- 页面正文断言不暴露 `mainDeck`、`runeDeck`、`handHidden`、`stackItemId`、`reconnectToken` 等 raw debug / hidden-info 文本。

通过证据：

- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node --check scripts/chrome-formal-18-e2e.mjs`：通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过；仅保留既有 SignalR/Rollup PURE 注释 warning。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run e2e:formal-18 -- --start-api`：通过，房间 `formal-18-1778623926434-15`。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过。

文档处理：

- 新增 `docs/CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md`。
- 更新 `docs/CURRENT_COMPLETION_AUDIT.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`、`docs/CURRENT_FRONTEND_REBUILD_PLAN.md` 与本 checkpoint。
- 已提交：`3aed179 checkpoint: add formal 18 step e2e evidence`。

口径：

- 该脚本满足 `docs/A_MASTER_AGENT_GOAL.md` 第 11 节 formal 18-step 主流程。
- 不宣称 full official battle lifecycle、完整 battlefield contest/control task、完整 PaymentEngine、LayerEngine、1009/811 full-official 或 READY。
- `docs/任务补充.md` 的严格战场争夺/战斗完整官方化仍由已有 seeded battle/control smoke 与后续 P0-002 / P0-004 收口承接。

## 47. Active START_BATTLE Guard Checkpoint

状态：**active `START_BATTLE` guard test-only evidence 已通过；项目整体仍 NOT READY。**

本批范围：

- 新增 `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`。
- 只做 test-only overlay，不修改 `CoreRuleEngine.cs`、`MatchSession.cs` 或前端代码。
- 固化争夺战场 spell-duel 完成后的 active `START_BATTLE` 代表路径：P1 prompt 只暴露当前争夺战场的 `DECLARE_BATTLE`；P2 prompt 保持等待；sourceRequirements 只列当前战场上的公开、正面、ready、受控 attacker / defender。
- 覆盖 wrong battlefield、其他战场单位、base、stale、face-down standby、equipment、spell、rune 等非法 `DECLARE_BATTLE` no-mutation guard。
- 覆盖合法 active task `DECLARE_BATTLE` 结算后 `START_BATTLE` task 不再留在 queue，并保留 `BATTLE_DECLARED` / `BATTLE_CLOSED` / `BATTLEFIELD_CONTROL_RESOLVED` 代表事件。

通过证据：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests"`：17/17 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContest|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~PendingTaskQueue"`：94/94 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3771/3771 通过。

口径：

- 本批只关闭 active `START_BATTLE` prompt / command guard 的代表性测试缺口。
- 不宣称完整 battle state machine、完整 spell duel / battle lifecycle、multi-battlefield APNAP、完整 damage assignment、replacement / prevention、PaymentEngine、LayerEngine、1009/811 full-official 或 READY。

## 48. Stage 4C-86 Imperial Shrine Evidence Checkpoint

状态：**Stage 4C-86 Imperial Shrine representative evidence 已通过；项目整体仍 NOT READY。**

本批范围：

- 代表 FU：`FU-ec31812b00`。
- 代表卡：帝王神坛 / Imperial Shrine `SFD·207/221` / cardId `33306`。
- 只做 evidence-only overlay，不修改功能代码或前端代码。
- 将既有服务端权威 `DECLARE_BATTLE` 征服触发路径入账：征服该战场后，有 1 法力且存在确定受控战场单位时，支付 1、返回该单位到拥有者手牌，并在战场创建 2 战力 `SFD·T02` 黄沙士兵。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch86ImperialShrineConquerPayReturnSandSoldierGuard`、`functionalUnits[].stage4C86` 与 `snapshotEntries[].stage4C86`。

通过证据：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerSandSoldier"`：3/3 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldConquer"`：45/45 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3771/3771 通过。

口径：

- 本批只关闭 Imperial Shrine / 帝王神坛代表性 battlefield rule-domain 证据入账缺口。
- 不宣称完整 optional trigger prompt / decline、完整 PaymentEngine、完整 battlefield / spell-duel / battle lifecycle、multi-battlefield APNAP、FAQ p22 完整裁定、hidden-info / redaction、1009/811 full-official 或 READY。

## 49. Stage 4C-87 Shield Wall Evidence Checkpoint

状态：**Stage 4C-87 Shield Wall representative evidence 已通过；项目整体仍 NOT READY。**

本批范围：

- 代表 FU：`FU-a7fbef72ba`。
- 代表卡：禁军之墙 / Shield Wall `SFD·043/221` / cardId `33119`。
- 只做 evidence-only overlay，不修改功能代码或前端代码。
- 将既有服务端权威 `PLAY_CARD` 路径入账：支付 2、动态允许选择 0 到全部己方战场单位、stack pass-pass 后把所选友方战场单位移动到拥有者基地；敌方单位、己方基地单位、重复目标均拒绝且不产生栈/状态副作用。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch87ShieldWallMoveFriendlyBattlefieldUnitsGuard`、`functionalUnits[].stage4C87` 与 `snapshotEntries[].stage4C87`。

通过证据：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ShieldWall"`：2/2 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MoveFriendly|FullyQualifiedName~MoveUnit|FullyQualifiedName~FriendlyBattlefieldUnit"`：63/63 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3771/3771 通过。

口径：

- 本批只关闭 Shield Wall / 禁军之墙代表性 move-friendly-battlefield-units 证据入账缺口。
- 不宣称完整 multi-battlefield movement、standby / quick timing windows、完整 FEPR、完整 PaymentEngine、hidden-info / redaction、1009/811 full-official 或 READY。

## 50. Stage 4C-88 Malzahar Resource Skill Design Gate

状态：**Stage 4C-88 Malzahar design gate 已记录；其“未实现功能”结论已被 4D-03I open-main representative focused slice 部分 supersede；完整 FU / full-official 仍未关闭，项目整体仍 NOT READY。**

本批范围：

- 候选 FU：`FU-0f7cbe26ce`。
- 候选卡：玛尔扎哈 / Malzahar `OGN·113/298` / cardId `31332`。
- 新增 design-gate 文档：`docs/CURRENT_STAGE4C_BATCH88_MALZAHAR_RESOURCE_SKILL_DESIGN_GATE.md`。
- 不修改 `src/`、`tests/`、DevUi 或 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 不新增 representative evidence，不设置 `stage4C88`，不升级 `fullOfficial`。

当前事实：

- 现有 registry / fixture 只覆盖 Malzahar 普通手牌打出：支付 4、0 目标、stack pass-pass 后作为 3 战力单位进入控制者基地。
- 4D-03I 已实现并验收官方 activated resource skill 的 open-main representative：摧毁一个友方单位或装备、横置、获得 2 点 payment-only 通用符能用于支付符能费用；focused 105/105、adjacent 317/317、backend full 3840/3840 通过。
- 仍未关闭完整 `[A]` / `[C]` family、spell-duel / swift timing、reaction prohibition、payment-only lifecycle 与 full-official FU。

派单结论：

- Malzahar 不能仅凭 Stage 4C-88 evidence-only 入账；4D-03I 已完成服务端 open-main representative，后续仍需要 D/E 完整 FAQ / 规则裁定与 C prompt-only UI 接线覆盖完整 official breadth。
- 本 design gate 明确成本目标、时点、资源限制、反应限制、隐藏信息与验收命令边界，供后续实现批次使用。

## 51. Stage 4C-89 Vanilla Unit Evidence Checkpoint

状态：**Stage 4C-89 vanilla unit representative evidence 已记录；项目整体仍 NOT READY。**

本批范围：

- 代表 FU：`FU-d635fc47f4`、`FU-72ce6fb8a4`。
- 代表卡：山脉亚龙 / Mountain Drake `OGN·142/298` / cardId `31366`。
- 代表卡：船坞潜伏者 / Dockside Lurker `OGN·175/298` / cardId `31405`。
- 只做 evidence-only overlay，不修改功能代码、测试代码或前端代码。
- 将既有 vanilla source-unit 服务端权威路径入账：ordinary hand `PLAY_CARD`、支付基础费用、0 目标、stack pass-pass 后源牌进入控制者基地成为官方战力 `CARD_TYPE:UNIT` 单位对象；显式目标输入被拒绝且 no mutation。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch89VanillaUnitEvidence`、`functionalUnits[].stage4C89` 与 `snapshotEntries[].stage4C89`。

通过证据：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysVanillaSourceUnit|FullyQualifiedName~CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided"`：305/305 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~VanillaSourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~PlayCard|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"`：1879/1879 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3771/3771 通过。

口径：

- 本批只关闭 Mountain Drake / Dockside Lurker 代表性无卡面效果单位证据入账缺口。
- 不宣称其他 source-zone play routes、静态文本单位、active-entry / keyword / movement / control-zone rules、完整 PaymentEngine / FEPR、hidden-info / redaction、1009/811 full-official 或 READY。

## 52. Stage 4C-90 Active Entry Unit Evidence Checkpoint

状态：**Stage 4C-90 active-entry unit representative evidence 已记录；项目整体仍 NOT READY。**

本批范围：

- 代表 FU：`FU-c1dc472304`、`FU-1207daea8f`。
- 代表卡：先锋扈从 / Vanguard Squire `OGS·016/024` / cardId `31595`。
- 代表卡：好斗的龙犬 / Aggressive Dragonhound `SFD·006/221` / cardId `33078`。
- 只做 evidence-only overlay，不修改功能代码、测试代码或前端代码。
- 将既有 active-entry source-unit 服务端权威路径入账：ordinary hand `PLAY_CARD`、支付基础费用、0 目标、stack pass-pass 后源牌进入控制者基地成为官方战力/标签且 `IsExhausted=false` 的 `CARD_TYPE:UNIT` 单位对象；显式目标输入被拒绝且 no mutation。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch90ActiveEntryUnitEvidence`、`functionalUnits[].stage4C90` 与 `snapshotEntries[].stage4C90`。

通过证据：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysActiveEntrySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsActiveEntrySourceUnitWhenTargetsAreProvided"`：24/24 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActiveEntry|FullyQualifiedName~ActiveSourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~PlayCard|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"`：1879/1879 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3771/3771 通过。

口径：

- 本批只关闭 Vanguard Squire / Aggressive Dragonhound 代表性 active-entry source-unit 证据入账缺口。
- 不宣称其他 active-entry family、roam / battlefield movement、tap skills、attack triggers、完整 PaymentEngine / FEPR、hidden-info / redaction、1009/811 full-official 或 READY。

## 53. Stage 4C-91 Royal Guard Sand Soldier Evidence Checkpoint

状态：**Stage 4C-91 Royal Guard representative evidence 已记录；项目整体仍 NOT READY。**

本批范围：

- 代表 FU：`FU-29d76f0175`。
- 代表卡：皇家守卫 / Royal Guard `SFD·157/221` / cardId `33251`。
- 只做 evidence-only overlay，不修改功能代码、测试代码或前端代码。
- 将既有 Royal Guard source-unit + Sand Soldier token 服务端权威路径入账：ordinary hand `PLAY_CARD`、支付基础 4 费用、0 目标、stack pass-pass 后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并创建一名 2 战力、带 `CARD_TYPE:UNIT|黄沙士兵` 标签的黄沙士兵单位指示物；显式目标输入被拒绝且 no mutation。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch91RoyalGuardSandSoldierEvidence`、`functionalUnits[].stage4C91` 与 `snapshotEntries[].stage4C91`。

通过证据：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RoyalGuard|FullyQualifiedName~SandSoldier|FullyQualifiedName~CreateSandSoldier"`：10/10 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CreateSandSoldier|FullyQualifiedName~TokenCreated|FullyQualifiedName~UnitToken|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~PlayCard|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"`：1880/1880 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3771/3771 通过。

口径：

- 本批只关闭 Royal Guard / 皇家守卫代表性 source-unit + Sand Soldier token 证据入账缺口。
- 不宣称精确“此处”目的地选择、完整 token factory、control-zone / battlefield 目的地矩阵、replacement / cleanup、完整 PaymentEngine / FEPR、hidden-info / redaction、1009/811 full-official 或 READY。

## 54. Stage 4C-92 Stern Sergeant Experience Evidence Checkpoint

状态：**Stage 4C-92 Stern Sergeant representative evidence 已记录；项目整体仍 NOT READY。**

本批范围：

- 代表 FU：`FU-5f03740098`。
- 代表卡：严厉军士 / Stern Sergeant `UNL-157/219` / cardId `34705`。
- 只做 evidence-only overlay，不修改功能代码、测试代码或前端代码。
- 将既有 Stern Sergeant source-unit + dynamic experience 服务端权威路径入账：ordinary hand `PLAY_CARD`、支付基础 6 费用、0 目标、stack pass-pass 后源牌进入控制者基地成为 6 战力、带 `CARD_TYPE:UNIT|精锐` 标签的单位对象，并按结算后控制者场上友方单位数量获得经验；显式目标输入被拒绝且 no mutation。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch92SternSergeantExperienceEvidence`、`functionalUnits[].stage4C92` 与 `snapshotEntries[].stage4C92`。

通过证据：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4DynamicExperienceGainOnPlayCountsFriendlyFieldUnits|FullyQualifiedName~CoreRuleEnginePlaysKeywordOnlySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided"`：460/460 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SternSergeant|FullyQualifiedName~DynamicExperience|FullyQualifiedName~Experience|FullyQualifiedName~KeywordOnlySourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~PlayCard|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"`：1913/1913 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3771/3771 通过。

口径：

- 本批只关闭 Stern Sergeant / 严厉军士代表性 source-unit + dynamic experience 证据入账缺口。
- 不宣称战斗/移动触发经验、经验消耗技能、完整 experience economy、完整 PaymentEngine / FEPR、LayerEngine、hidden-info / redaction、1009/811 full-official 或 READY。

## 55. Stage 4C-93 Royal Attendant Legend Mode Evidence Checkpoint

状态：**Stage 4C-93 Royal Attendant representative evidence 已记录；项目整体仍 NOT READY。**

本批范围：

- 代表 FU：`FU-92e31978af`。
- 代表卡：皇家随从 / Royal Attendant `SFD·039/221` / cardId `33115`。
- 只做 evidence-only overlay，不修改功能代码、测试代码或前端代码。
- 将既有 Royal Attendant source-unit + legend mode 服务端权威路径入账：ordinary hand `PLAY_CARD`、支付基础 3 费用、选择 `LEGEND` 目标与 `READY_LEGEND` / `EXHAUST_LEGEND` mode、stack pass-pass 后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT`，并按 mode 让目标传奇变为活跃或休眠；无效显式目标输入被拒绝且 no mutation。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch93RoyalAttendantLegendModeEvidence`、`functionalUnits[].stage4C93` 与 `snapshotEntries[].stage4C93`。

通过证据：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RoyalAttendant|FullyQualifiedName~royal-attendant|FullyQualifiedName~READY_LEGEND|FullyQualifiedName~EXHAUST_LEGEND"`：5/5 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RoyalAttendant|FullyQualifiedName~Legend|FullyQualifiedName~PlayCardPrompt|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"`：1894/1894 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3771/3771 通过。

口径：

- 本批只关闭 Royal Attendant / 皇家随从代表性 source-unit + legend mode 证据入账缺口。
- 不宣称完整 legend interaction domain、非手牌或替代来源打出、完整 PaymentEngine / FEPR、LayerEngine、hidden-info / redaction、1009/811 full-official 或 READY。

## 56. Stage 4C-94 Babbling Poro Predict Evidence Checkpoint

状态：**Stage 4C-94 Babbling Poro representative evidence 已记录；项目整体仍 NOT READY。**

本批范围：

- 代表 FU：`FU-677c27eea7`。
- 代表卡：叨叨魄罗 / Babbling Poro `UNL-224/219` / cardId `34777`。
- 只做 evidence-only overlay，不修改功能代码、测试代码或前端代码。
- 将既有 UNL Babbling Poro source-unit + predict recycle 服务端权威路径入账：ordinary hand `PLAY_CARD`、支付基础 2 费用、选择己方主牌堆顶部牌、stack pass-pass 后源牌进入控制者基地成为 2 战力、带 `CARD_TYPE:UNIT|魄罗|预知` 标签的单位对象，并将所选顶部牌回收到主牌堆底部；选择非顶部牌被拒绝且 no mutation。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch94BabblingPoroPredictEvidence`、`functionalUnits[].stage4C94` 与 `snapshotEntries[].stage4C94`。

通过证据：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysPredictSourceUnitRecycleTopCard|FullyQualifiedName~CoreRuleEngineRejectsPredictSourceUnitWhenTargetIsOutsideTopCard"`：12/12 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Predict|FullyQualifiedName~FriendlyMainDeck|FullyQualifiedName~MainDeck|FullyQualifiedName~Recycle|FullyQualifiedName~SourceUnit|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost|FullyQualifiedName~Poro"`：1830/1830 通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3771/3771 通过。

口径：

- 本批只关闭 UNL Babbling Poro / 叨叨魄罗代表性 source-unit + predict recycle 证据入账缺口。
- 不宣称 predict 不回收 / decline 分支、完整主牌堆查看窗口隐私矩阵、非手牌或替代来源打出、完整 PaymentEngine / FEPR、hidden-info / redaction、1009/811 full-official 或 READY。

## 57. Stage 4C-95 Static Effect Design Gate

状态：**Stage 4C-95 design gate 已记录；项目整体仍 NOT READY。**

本批范围：

- 设计门禁文档：`docs/CURRENT_STAGE4C_BATCH95_STATIC_EFFECT_DESIGN_GATE.md`。
- 覆盖剩余 direct-card `IMPLEMENTED_UNTESTED` 候选中不能 evidence-only 入账的四个 FU：熔浆巨龙、娑娜、安妮、温驯的宝石龙。
- 本批不修改功能代码、测试代码、前端代码或 coverage matrix。

判定：

- 现有 fixture 只证明普通 hand `PLAY_CARD` 进入基地成为单位对象。
- fixture 描述与 `rules-evidence-index.md` 均明确写着核心官方文本 deferred：其他友方单位活跃进场、回合结束活跃符文、法术/技能伤害 +1、龙单位打出后活跃符文。
- 这些 FU 仍保持 `IMPLEMENTED_UNTESTED` / `NEEDS_AUTOMATED_TEST_EVIDENCE`，需要后续功能设计、实现和新证据入账。

## 58. Stage 4C-96 Legacy Guard Evidence Alignment

状态：**Stage 4C-96 representative evidence alignment 已记录；项目整体仍 NOT READY。**

本批范围：

- 审计入口：`docs/CURRENT_STAGE4C_BATCH96_LEGACY_GUARD_EVIDENCE_ALIGNMENT_AUDIT.md`。
- 证据入口：`docs/CURRENT_STAGE4C_BATCH96_LEGACY_GUARD_EVIDENCE_ALIGNMENT_EVIDENCE.md`。
- 对 Stage 4C-60 至 Stage 4C-69 已有代表性自动化证据的 10 个 direct-card FU 做矩阵状态对齐：烈火风暴、过载能量、狩猎、大副、怒海大鲨炮、德玛西亚使节、提伯斯、泡泡机、宝藏魔像、忠实的工坊主。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch96LegacyGuardEvidenceAlignment`、`functionalUnits[].stage4C96` 与 `snapshotEntries[].stage4C96`。
- 将上述 10 个 FU 的 `stage4B.freezeStatus` 从 `IMPLEMENTED_UNTESTED` 升级为 `IMPLEMENTED_TESTED`，并将自动化状态改为 `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`；`fullOfficial=false` 保持不变。

通过证据：

- focused legacy guard regression：67/67 通过。
- adjacent legacy guard regression：193/193 通过。
- backend full：3771/3771 通过。

口径：

- 本批不修改功能代码、测试代码或前端代码，只做 matrix / docs evidence alignment。
- 不声明完整 same-battlefield、standby / reaction、quick / spell-duel、FEPR、PaymentEngine、replacement / prevention、cleanup、LayerEngine、hidden-info / redaction、FAQ full adjudication、1009/811 full-official 或 READY。

## 59. Stage 4C-97 Arena / Minion / Annie Evidence Alignment

状态：**Stage 4C-97 representative evidence alignment 已记录；项目整体仍 NOT READY。**

本批范围：

- 审计入口：`docs/CURRENT_STAGE4C_BATCH97_ARENA_MINION_ANNIE_EVIDENCE_ALIGNMENT_AUDIT.md`。
- 证据入口：`docs/CURRENT_STAGE4C_BATCH97_ARENA_MINION_ANNIE_EVIDENCE_ALIGNMENT_EVIDENCE.md`。
- 对竞技场勤务小队、三张官方随从 token factory、黑暗之女共 5 个已有代表性自动化证据的 FU 做矩阵状态对齐。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch97ArenaServiceCrewMinionAnnieEvidenceAlignment`、`functionalUnits[].stage4C97` 与 `snapshotEntries[].stage4C97`。
- `IMPLEMENTED_TESTED` 计数升至 73，`IMPLEMENTED_UNTESTED` 降至 7；`fullOfficial=false` 保持不变。

通过证据：

- focused regression：6/6 通过。
- adjacent regression：87/87 通过。
- backend full：3771/3771 通过。

口径：

- 本批不修改功能代码、测试代码或前端代码，只做 matrix / docs evidence alignment。
- 不声明完整 equipment-trigger ordering、完整 token factory domain、完整 legend action domain、FEPR、PaymentEngine、trigger engine、LayerEngine、hidden-info / redaction、FAQ full adjudication、1009/811 full-official 或 READY。

## 60. Stage 4C-98 Battlefield Residual Evidence Alignment

状态：**Stage 4C-98 representative battlefield evidence alignment 已记录；项目整体仍 NOT READY。**

本批范围：

- 审计入口：`docs/CURRENT_STAGE4C_BATCH98_BATTLEFIELD_RESIDUAL_EVIDENCE_ALIGNMENT_AUDIT.md`。
- 证据入口：`docs/CURRENT_STAGE4C_BATCH98_BATTLEFIELD_RESIDUAL_EVIDENCE_ALIGNMENT_EVIDENCE.md`。
- 对剩余三个已有代表性自动化证据的 battlefield rule-domain FU 做矩阵状态对齐：力量方尖碑、荣耀竞技场、冰霜要塞。
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage4CBatch98BattlefieldResidualEvidenceAlignment`、`functionalUnits[].stage4C98` 与 `snapshotEntries[].stage4C98`。
- `IMPLEMENTED_TESTED` 计数升至 76，`IMPLEMENTED_UNTESTED` 降至 4；`fullOfficial=false` 保持不变。

通过证据：

- focused battlefield residual regression：8/8 通过。
- adjacent battlefield residual regression：87/87 通过。
- backend full：3771/3771 通过。

口径：

- 本批不修改功能代码、测试代码或前端代码，只做 matrix / docs evidence alignment。
- 不声明完整 battlefield rule-domain matrix、battle / spell-duel / assign-combat-damage lifecycle、FEPR、PaymentEngine、trigger engine、LayerEngine、hidden-info / redaction、FAQ full adjudication、1009/811 full-official 或 READY。

## 61. Stage 4D P0/P1 Closure Plan

状态：**Stage 4D P0/P1 closure plan 已记录；项目整体仍 NOT READY。**

本批范围：

- 新增主控计划入口：`docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`。
- 将 Stage 4C representative evidence alignment 后的剩余工作切换为 P0/P1 架构收口，不再默认追加 evidence-only overlay。
- 明确 4D-01 至 4D-06 的执行顺序、写锁、建议 owner、验收门槛和 no-go 边界：
  - 4D-01：Board task queue foundation，收口 P0-002 / P0-003。
  - 4D-02：Spell duel and battle state machine，收口 P0-004。
  - 4D-03：PaymentEngine unification，收口 P0-005。
  - 4D-04：LayerEngine and keyword full-pass track，收口 P1-001 / P1-002 / P1-003。
  - 4D-05：Frontend authority and Chrome gate。
  - 4D-06：Final evidence and completion audit。
- 本批不修改功能代码、测试代码、前端代码或 coverage matrix。

口径：

- 4C-98 后矩阵仍为 `IMPLEMENTED_TESTED=76`、`IMPLEMENTED_UNTESTED=4`、`NEEDS_ENGINE_SUPPORT=501`、`NEEDS_FAQ_REVIEW=128`、`SHARED_ORACLE_IMPLEMENTATION=102`。
- 四个剩余 `IMPLEMENTED_UNTESTED` 已由 4C-95 设计门禁确认，不能仅凭普通 source-unit fixture 入账。
- Formal 18-step E2E 已满足 A_MASTER 主流程门槛，但不能替代 strict battlefield contest / battle lifecycle / PaymentEngine / LayerEngine full official 收口。
- 只有 P0/P1 清零、backend full、frontend build、Chrome smoke、formal E2E、hidden-info long-chain、card matrix 和 final completion audit 全部通过后，才允许 READY / goal complete。

## 62. Stage 4D-01 Board Task Queue Foundation Handoff

状态：**4D-01 board task queue foundation handoff 已记录；项目整体仍 NOT READY。**

本批范围：

- 新增服务端实现交接规格：`docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_HANDOFF.md`。
- 只读审计现有 `PendingTaskQueue`、`RunStateBasedCleanupLoop`、`AdvancePendingBattlefieldTasksAfterStateChange`、`BattlefieldContestBattleTaskGuardTests` 与 pending queue shape tests。
- 明确 4D-01 的 exclusive write lock、允许新增测试文件、blocked parallel writes、required implementation shape、focused / adjacent / backend full 验收命令和 no-go criteria。

口径：

- 本批不修改功能代码、测试代码、前端代码或 coverage matrix。
- 4D-01 只承接 P0-002 / P0-003 的 board task queue foundation；P0-004 battle/spell-duel lifecycle 仍属于 4D-02，P0-005 PaymentEngine 仍属于 4D-03。
- 不把任何 P0/P1 标记 resolved，不声明 READY，不调用 active goal complete。

## 63. Stage 4D-01 Board Task Queue Baseline Evidence

状态：**4D-01 baseline evidence 已记录；项目整体仍 NOT READY。**

本批范围：

- 新增 baseline evidence：`docs/CURRENT_STAGE4D_01_BASELINE_EVIDENCE.md`。
- 在 4D-01 实现前记录当前 HEAD 的 focused / adjacent 测试基线。
- focused baseline：`FullyQualifiedName~BoardTaskQueueFoundation|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~PendingTaskQueue`，22/22 通过。
- adjacent baseline：`FullyQualifiedName~BattlefieldContest|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~MoveUnit|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle`，139/139 通过。

口径：

- 本批不修改功能代码、测试代码、前端代码或 coverage matrix。
- baseline 只证明现有代表性 queue / battlefield / move / cleanup / spell-duel / start-battle 周边测试在实现前为绿色。
- baseline 不关闭 P0-002 / P0-003 / P0-004 / P0-005，不声明 READY。

## 64. Stage 4D-01 Board Task Queue Foundation Evidence

状态：**4D-01 foundation accepted；项目整体仍 NOT READY。**

本批范围：

- 实现证据提交：`6a3ee038 test: add stage 4D board task queue foundation coverage`。
- 新增测试：`tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`。
- 新增审计入口：`docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_AUDIT.md`。
- 新增证据入口：`docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_EVIDENCE.md`。
- 不修改引擎、API、前端或 coverage matrix。

通过证据：

- focused：`FullyQualifiedName~BoardTaskQueueFoundation|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~ReconnectWithPendingTaskQueue`，31/31 通过。
- adjacent：`FullyQualifiedName~BattlefieldContest|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~MoveUnit|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle|FullyQualifiedName~Reconnect`，149/149 通过。
- backend full：3780/3780 通过。
- `git diff --check` 与新增文件 whitespace check 均无输出。

口径：

- 4D-01 handoff checklist 已被自动化测试覆盖。
- P0-002 / P0-003 被进一步收窄，但完整 held/conquer/control lifecycle、replacement/prevention、所有状态变化 unified cleanup queue 仍未 full-official resolved。
- 下一实现切片进入 4D-02 spell duel / battle state machine。
- P0-004、P0-005、P1 与 READY 均未关闭；不得调用 active goal complete。
