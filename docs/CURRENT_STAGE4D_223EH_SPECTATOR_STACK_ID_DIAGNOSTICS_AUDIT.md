# Stage 4D-223EH Spectator Stack Id Diagnostics Audit

Date: 2026-06-19

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `72bb1352` (`test: detail spectator stack id diagnostics`)

## Scope

This shard narrows spectator recovery diagnostics only. `MatchRecoveryValidator` now appends stable expected/actual detail to:

- spectator replay frame snapshot stack item id aggregate mismatches;
- spectator replay frame timing battlefield task stack item id aggregate mismatches.

The existing diagnostic prefixes are preserved. The new list-of-lists formatter is diagnostic-only and is used so battlefield task stack item ids render as readable nested lists, for example `expected [[], []] but got [[stack-drift], []]`.

Valid stack construction, battlefield task construction, stack references, snapshot construction, spectator redaction, timing/task execution and gameplay behavior are unchanged.

## Rule Source

Checked `AGENTS.md`, the five root PDFs and extracted text under `/tmp/riftbound_rules_pdf_text/` before selecting this shard. Relevant latest core-rule anchors for this area remain 103.2.e, 103.3, 120-130, 144.4, 146.1 and 383.4. This shard changes diagnostics only, not rules behavior.

## Tests

- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplaySnapshotStackItemIdsMismatch|FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskPayloadMismatch"` -> `2/2`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1981/1981`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Recovery|FullyQualifiedName~SpectatorReplay|FullyQualifiedName~Snapshot|FullyQualifiedName~Timing|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~TriggerQueue|FullyQualifiedName~OrderTriggers|FullyQualifiedName~Trigger|FullyQualifiedName~Stack|FullyQualifiedName~Battle"` -> `3574/3574`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test Riftbound.slnx --no-restore` -> `8319/8319`
- `git diff --check`
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" src tests docs --glob '!src/Riftbound.DevUi/node_modules/**' --glob '!**/bin/**' --glob '!**/obj/**'`
- Residual stack-id diagnostic assertion scan found no selected old-format assertion without expected/actual detail.

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` remained clean and no-ahead at the post-code-commit check; `main...codex/ui-followup-20260616` divergence was `355 0`.

The historical `rule-audit-remaining-20260615` worktree path remains absent, but local branch `codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `428 0`.

Project remains **NOT READY**. This does not close broader command/recovery/random determinism, continuous-effect breadth, remaining triggerQueue keyed/detail breadth, recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness.
