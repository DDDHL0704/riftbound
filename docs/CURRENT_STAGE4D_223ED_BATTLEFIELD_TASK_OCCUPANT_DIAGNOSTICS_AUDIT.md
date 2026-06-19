# Stage 4D-223ED Battlefield Task Occupant Diagnostics Audit

Date: 2026-06-19 19:03 CST

Branch: `main`

Code commit: `75be9902` (`test: detail battlefield task occupant diagnostics`)

Project status: **NOT READY**

## Scope

Stage 4D-223ED is a narrow P1-004 runtime/server recovery diagnostics shard. It only improves battlefield task participant-object occupant membership diagnostic detail in `MatchRecoveryValidator`; it does not change valid battlefield task construction, battlefield state construction, object tags, object locations, spectator redaction, snapshot replay, authoritative replay, timing, task execution or gameplay behavior.

The runtime validator now keeps the existing diagnostic prefixes while appending stable expected/actual detail for participant object ids that do not match battlefield-state occupants.

Affected diagnostics:

- Recovered snapshot timing battlefield task participant object id not in battlefield state occupants.
- Recovered snapshot timing battlefield task participant object id required by battlefield state occupants.
- Spectator replay timing battlefield task participant object id not in authoritative state battlefield state occupants.
- Spectator replay timing battlefield task participant object id required by authoritative state battlefield state occupants.

Representative suffixes:

- `expected [participant-a, participant-b] but got participant-standby`
- `expected contains participant-b but got [participant-a, participant-standby]`

## Rule Anchors

Rule source was checked through `AGENTS.md`, the root PDF text in `/tmp/riftbound_rules_pdf_text/`, and the current Stage 4D shared docs. The relevant anchors remain latest core rules 107.2, 107.3, 109, 120-130, 141.1, 144 and 383.4. No rule behavior changed.

## Validation

Passed:

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- Focused `ParticipantObjectsInconsistentWithBattlefieldStateOccupants` filter: `2/2`
- Focused `MatchRecoveryTests` BattlefieldTask filter: `54/54`
- Changed-class `MatchRecoveryTests`: `1981/1981`
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/Stack/Battle filter: `3574/3574`
- Backend full `dotnet test Riftbound.slnx --no-restore`: `8319/8319`
- `git diff --check`
- Anchored conflict-marker scan over `src`, `tests` and `docs` with `bin`/`obj` excluded: no findings.
- Residual battlefield task occupant assertion search found only the selected assertions with expected/actual detail.

## Coordination

A_MAIN created no subagent and continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

At the 2026-06-19 19:03 CST pre-docs sync:

- Local `main` was ahead of `origin/main` by one code commit: `main...origin/main` = `1 0`.
- External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`; `main...codex/ui-followup-20260616` = `332 0`.
- Historical `codex/rule-audit-remaining-20260615` branch was present locally at `3c8ef8fe`; its old worktree path remains absent; `main...codex/rule-audit-remaining-20260615` = `405 0`.

This shard narrows battlefield task occupant membership diagnostics only. It does not close broader command/recovery/random determinism, continuous-effect breadth, remaining triggerQueue keyed/detail breadth, full LayerEngine breadth, P0/P1, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness.
