# Stage 4D-223EE Battlefield Task Participant Location Diagnostics Audit

Date: 2026-06-19 19:21 CST

Branch: `main`

Code commit: `058bb44a` (`test: detail battlefield task participant location diagnostics`)

Project status: **NOT READY**

## Scope

Stage 4D-223EE is a narrow P1-004 runtime/server recovery diagnostics shard. It only improves battlefield task participant object location diagnostic detail in `MatchRecoveryValidator`; it does not change valid battlefield task construction, battlefield state construction, object locations, object tags, spectator redaction, snapshot replay, authoritative replay, timing, task execution or gameplay behavior.

The runtime validator now keeps the existing diagnostic prefixes while appending stable expected/actual detail when a battlefield task participant object is not located at the task battlefield, or when the participant object is absent from the object location index.

Representative suffixes:

- `expected BATTLEFIELD @ battlefield-a but got BATTLEFIELD @ battlefield-b`
- `expected BATTLEFIELD @ battlefield-a but got BASE @ <empty>`
- `expected contains participant-missing-location but got [battlefield-a, participant-a, participant-base, participant-other]`

## Rule Anchors

Rule source was checked through `AGENTS.md`, the root PDF text in `/tmp/riftbound_rules_pdf_text/`, and the current Stage 4D shared docs. The relevant anchors remain latest core rules 120-130 for game objects/cards/privacy, 144.4 for unit battlefield/base movement constraints, 146.1 for unit location, and 383.4 for battlefield/trigger participation context. No rule behavior changed.

## Validation

Passed:

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- Focused `ParticipantObjectsOutsideTaskBattlefield` filter: `2/2`
- Focused `MatchRecoveryTests` BattlefieldTask filter: `54/54`
- Changed-class `MatchRecoveryTests`: `1981/1981`
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/Stack/Battle filter: `3574/3574`
- Backend full `dotnet test Riftbound.slnx --no-restore`: `8319/8319`
- `git diff --check`
- Anchored conflict-marker scan over `src`, `tests` and `docs` with `bin`/`obj` excluded: no findings.
- Residual battlefield task participant-location assertion search found no selected assertion without expected/actual detail.

## Coordination

A_MAIN created no subagent and continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

At the 2026-06-19 19:21 CST pre-docs sync:

- Local `main` was ahead of `origin/main` by one code commit: `main...origin/main` = `1 0`.
- External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` remained no-ahead; `main...codex/ui-followup-20260616` = `337 0`.
- Historical `codex/rule-audit-remaining-20260615` branch remained no-ahead; its old worktree path remains absent; `main...codex/rule-audit-remaining-20260615` = `410 0`.

This shard narrows battlefield task participant location diagnostics only. It does not close broader command/recovery/random determinism, continuous-effect breadth, remaining triggerQueue keyed/detail breadth, full LayerEngine breadth, P0/P1, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness.
