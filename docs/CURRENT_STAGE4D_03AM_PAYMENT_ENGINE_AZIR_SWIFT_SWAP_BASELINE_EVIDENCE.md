# Stage 4D-03AM PaymentEngine Azir Swift Swap Baseline Evidence

日期：2026-05-15
结论：**BASELINE ESTABLISHED / PROJECT NOT READY**

本文件记录 4D-03AM 实现前基线。当前只建立 handoff 和测试基线，不实现 Azir swift swap，不修改 runtime、前端、测试代码或 card matrix。

## 1. Evidence Inspected

- `data/official/card-catalog.zh-CN.json`
  - `SFD·050/221` 与 `SFD·050a/221` 均为阿兹尔，官方文本为 green payment + swift + choose controlled unit + swap positions + optional armament reattach + once per turn。
- `docs/rules-evidence-index.md`
  - `p2-preflight-play-sfd-050-azir-swap-skill-static` 与 `p2-preflight-play-sfd-050a-azir-swap-skill-static` 只证明普通 hand `PLAY_CARD` 入场。
  - 文档明确绿色支付迅捷位置交换技能、原位置记忆和武装贴附路径暂缓。
- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
  - static parse found 26 ability id constants.
  - Existing executable representative ability ids include Vi, Xerath, Renata draw / score, Crimson Rose, Fluft Poro, Shadow, Malzahar, Dragon Soul Sage, Sigils, resource conversion equipment and Gold tokens.
  - No Azir `SFD·050/221` / `SFD·050a/221` swift swap ability id is present.
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
  - P0-005 still lists target-bearing colored-cost activated abilities, single / multi-faction costs, Haste special branches, Spellshield full-window tax, Echo costs and trigger payment family as pending.
- `docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md`
  - Existing Spellshield tax verifier covers Xerath / Crimson Rose / Shadow only.
- `docs/CURRENT_STAGE4D_03AL_PAYMENT_ENGINE_RESOURCE_SKILL_COVERAGE_AUDIT.md`
  - Existing resource skill verifier covers current `IsResourceSkill=true` abilities only.

## 2. Baseline Commands

Activated ability / prompt / movement / payment baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: passed 361/361.

Current PaymentEngine / activated ability / tax / prompt adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Renata|FullyQualifiedName~CrimsonRose|FullyQualifiedName~Shadow|FullyQualifiedName~Xerath|FullyQualifiedName~Fluft|FullyQualifiedName~Spellshield|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed 449/449.

Diff hygiene before docs patch:

```sh
git status --short --branch
git diff --check
```

Result: branch `main`; only untracked `riftbound-dotnet.sln`; `git diff --check` passed.

## 3. Baseline Interpretation

- Existing Azir evidence is ordinary play / static preflight evidence only.
- Existing activated ability tests remain green, so 4D-03AM can measure new Azir behavior against a stable adjacent baseline.
- Existing PaymentEngineCoverageAuditTests remain green, so 4D-03AM should not weaken 03AH / 03AK / 03AL verifier discipline.
- This baseline does not prove Azir swift swap is implemented.
- This baseline does not close target-bearing colored-cost activated ability breadth, full `[A]` / `[C]` resource skill family, P0-005, P1, frontend final validation, full-card matrix or READY.

## 4. Residual Risk For Implementation

- Swift timing must reuse existing server timing / prompt model; do not let frontend infer it.
- Green typed payment must be command-side enforced, including wrong-trait and temporary-resource rejection.
- Position swap must update authoritative state and not merely emit cosmetic events.
- Optional armament reattach branch is official text; if not implemented in 4D-03AM, it must remain explicit residual risk.
- Once-per-turn memory must not leak or persist incorrectly across turn transitions.

## 5. Verdict

4D-03AM handoff may proceed. The repo remains **NOT READY** and the active goal remains incomplete.
