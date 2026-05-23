# Stage 4D-05P Surrender Replay Evidence

Date: 2026-05-22

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_05P_SURRENDER_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_05P_SURRENDER_REPLAY_EVIDENCE.md`

## Validation

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~CoreRuleEngineRejectsAcceptedSurrenderReplayWithoutMutation"
```

Result: passed `1/1`.

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Surrender|FullyQualifiedName~MatchFinished|FullyQualifiedName~MatchStateHasher"
```

Result: passed `6/6`.

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed `5351/5351`.

```bash
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05J_HAND_CHOICE_REPLAY_AUDIT.md docs/CURRENT_STAGE4D_05J_HAND_CHOICE_REPLAY_EVIDENCE.md docs/CURRENT_STAGE4D_05K_PASS_FOCUS_REPLAY_AUDIT.md docs/CURRENT_STAGE4D_05K_PASS_FOCUS_REPLAY_EVIDENCE.md docs/CURRENT_STAGE4D_05L_PASS_PRIORITY_REPLAY_AUDIT.md docs/CURRENT_STAGE4D_05L_PASS_PRIORITY_REPLAY_EVIDENCE.md docs/CURRENT_STAGE4D_05M_DECLARE_BATTLE_REPLAY_AUDIT.md docs/CURRENT_STAGE4D_05M_DECLARE_BATTLE_REPLAY_EVIDENCE.md docs/CURRENT_STAGE4D_05N_MOVE_UNIT_REPLAY_AUDIT.md docs/CURRENT_STAGE4D_05N_MOVE_UNIT_REPLAY_EVIDENCE.md docs/CURRENT_STAGE4D_05O_END_TURN_REPLAY_AUDIT.md docs/CURRENT_STAGE4D_05O_END_TURN_REPLAY_EVIDENCE.md docs/CURRENT_STAGE4D_05P_SURRENDER_REPLAY_AUDIT.md docs/CURRENT_STAGE4D_05P_SURRENDER_REPLAY_EVIDENCE.md
```

Result: passed. No whitespace warnings, conflict markers or trailing whitespace in the new 05J/05K/05L/05M/05N/05O/05P evidence docs.

## Runtime And Protocol

Runtime changed: no.

Protocol shape changed: no.

Hidden-info leakage found: no.
