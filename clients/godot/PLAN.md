# Riftbound Godot Playable v1 Plan

Godogen checkpoint for continuing the existing Playable v1 work. This is not a
replacement for the top-level goal file; it is the repo-local Godot client plan.

## Guardrails

- Work in `clients/godot/` unless the user explicitly authorizes another area.
- Keep the server authoritative. The Godot client renders snapshots/prompts and
  submits server-owned commands only.
- Preserve hidden-information boundaries: opponent hands, hidden standby, and
  unrevealed cards render as backs/counts only.
- Do not mark the project complete until the final two-human P5 package exists
  and verifies.

## Current Status

- P1 same-machine multi-window play is unlocked through isolated session
  arguments and explicit room/handle/player-key overrides.
- P2/P3 have a visible automated regression path that reaches the server result
  panel with the inksteel table/card presentation. This is useful evidence, but
  it is auto-smoke and cannot satisfy final P5.
- P4 local deployment validation has passed from clean pushed `origin/main`:
  Docker image build, Production memory-mode `docker run`, `/health`,
  `/metrics`, Dev UI root, and Docker `HEALTHCHECK` all verified. No public
  cloud instance has been created because target platform and credentials are
  still user-owned.
- P5 evidence collection has been hardened: final runs must come from clean
  pushed `origin/main`, must not use auto-smoke or auto-quit arguments, must
  record manual confirmations, and must now prove both player identities and the
  shared room in logs and reports.
- `MEMORY.md` records the current implementation shape and open risks for future
  continuation after context compaction.

## Active Work

1. Keep the Godot client build and evidence scripts green after every scoped
   change.
2. Run visible clean-main simulated preflights after pushed changes to catch
   obvious window, result-panel, screenshot, or evidence regressions.
3. Prepare for the final two-human P5 run:
   - run `clients/godot/tools/run-clean-main-human-playtest-stack.sh --precheck`
     before both operators start, so final evidence gate mistakes are caught
     without opening Godot windows;
   - run `clients/godot/tools/run-clean-main-human-playtest-stack.sh`;
   - have two human operators play from preconstructed decks to the server
     result panel;
   - verify both final screenshots show the result panel and hidden opponent
     information only as card backs/counts;
   - answer manual prompts truthfully;
   - keep the verified evidence tarball, including `P5_HANDOFF.md`.

## Stop Conditions

- Final P5 is complete only when two real humans using the Godot client complete
  a match to the server result panel and the final evidence package passes
  `verify-human-playtest-package.sh`.
- Automated simulated evidence must remain explicitly labeled as non-final.
- If a preconstructed deck hits an engine/rules gap, stop and report instead of
  patching rules in the client.
