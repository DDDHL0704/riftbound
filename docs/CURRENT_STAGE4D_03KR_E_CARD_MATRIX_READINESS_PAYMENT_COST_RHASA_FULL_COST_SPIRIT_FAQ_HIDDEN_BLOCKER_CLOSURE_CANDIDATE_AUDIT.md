# 4D-03KR-E Audit - Rhasa Full-Cost Spirit FAQ Hidden

本审计记录 4D-03KR-E 对 `FU-d4b4d9af72` / `OGN·195/298`《裂魂者喇煞》 / `RHASA_FULL_COST_SPIRIT_PLAY_UNIT` 的 row-level blocker closure candidate。结论限定为：已有空废牌堆全额 10 费手牌打出、0 目标入栈、双方让过、源牌进入控制者基地、成为 6 战力 `灵体` 单位对象的代表路径，足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 automated evidence disposition、FAQ adjudication、按废牌堆张数减少费用分支、hidden-info / random-zone breadth 和 full PaymentEngine 仍 open。

Evidence chain:

- Official catalog row: `OGN·195/298` / `RHASA_FULL_COST_SPIRIT_PLAY_UNIT`.
- Fixture: `p2-preflight-play-rhasa-full-cost-spirit-unit.fixture.json`.
- Runner assertion: `ConformanceFixtureRunnerTests`.
- Rule evidence index: `rules-evidence-index.md`.
- P2 preflight evidence: `p2-rules-preflight.md`.
- Runtime binding: `CardBehaviorRegistry.cs` and `CoreRuleEngine.cs`.

Selected row transition:

- `freezeStatus` remains `NEEDS_FAQ_REVIEW`.
- `statusFlags` changes from `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT + NEEDS_FAQ_REVIEW` to `IMPLEMENTED_UNTESTED + NEEDS_FAQ_REVIEW`.
- `fullOfficialBlockers` changes from `NEEDS_ENGINE_SUPPORT + NEEDS_FAQ_REVIEW + NEEDS_AUTOMATED_TEST_EVIDENCE` to `NEEDS_FAQ_REVIEW + NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false` remains unchanged.
- `ready=false` remains unchanged.

Write scope:

- Matrix JSON only.
- Current checkpoint / audit docs only.
- `PaymentEngineCoverageAuditTests.cs` guard only.
- No runtime change.
- No frontend change.
- No protocol field change.
- No official catalog change.
- No Chrome / browser script change.

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 574/574; Rhasa focused 3021/3021; adjacent prompt/payment/FAQ/hidden 374/374; backend full 5145/5145; git diff --check passed.
