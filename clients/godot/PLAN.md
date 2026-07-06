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
  record manual confirmations, must prove both player identities and the shared
  room in logs and reports, and now write an evidence-directory
  `OPERATOR_GUIDE.md` before the Godot windows launch so operators can recover
  the room, handles, evidence path, package path, and checklist if terminal
  scrollback is lost.
- P5 evidence now also requires machine-readable hidden-information boundary
  logs from both Godot clients: zero opponent hand faces and zero hidden card
  identity leaks. The package README, generated handoff, and visual review files
  repeat that checked conclusion, but the human screenshot confirmation remains
  required.
- P5 evidence now records the inksteel screenshot style guard in the machine
  report and repeats it in the package README, handoff, and visual review files.
  P5 evidence now also records the battle-layout screenshot geometry guard, which
  checks that the black/ivory wire table keeps the selected reference layout,
  is not actually clipped, and still has the right result rail. The final
  package verifier re-runs both guards on the packaged screenshots, while still
  requiring human screenshot confirmation.
- `MEMORY.md` records the current implementation shape and open risks for future
  continuation after context compaction.

## Active Work

1. Keep the Godot client build and evidence scripts green after every scoped
   change.
2. Run visible clean-main simulated preflights after pushed changes to catch
   obvious window, result-panel, screenshot, inksteel style, or evidence
   regressions. The clean-main simulated wrapper runs the inksteel screenshot
   style guard and battle-layout screenshot geometry guard by default after the
   automated result screenshots are written.
3. Prepare for the final two-human P5 run:
   - run `clients/godot/tools/run-clean-main-human-playtest-stack.sh --precheck`
     before both operators start, so final evidence gate and local
     Godot/.NET executable/output-path/custom-worktree/stale-local-API mistakes
     are caught without opening Godot windows;
   - run `clients/godot/tools/run-clean-main-human-playtest-stack.sh`;
   - keep `${RIFTBOUND_SCREENSHOT_DIR}/OPERATOR_GUIDE.md` available during the
     run for the room, player handles, evidence/package paths, and final P5
     operator checklist;
   - have two human operators play from preconstructed decks to the server
     result panel;
   - verify both final screenshots show the result panel and hidden opponent
     information only as card backs/counts;
   - keep the `Inksteel style: passed` and `Battle layout: passed` report lines
     from the evidence checker as visual drift guards before handoff;
   - answer manual prompts truthfully;
   - keep the verified evidence tarball, including `OPERATOR_GUIDE.md`,
     `P5_HANDOFF.md`, and `VISUAL_REVIEW.md`.

## Stop Conditions

- Final P5 is complete only when two real humans using the Godot client complete
  a match to the server result panel and the final evidence package passes
  `verify-human-playtest-package.sh`.
- Automated simulated evidence must remain explicitly labeled as non-final.
- If a preconstructed deck hits an engine/rules gap, stop and report instead of
  patching rules in the client.
