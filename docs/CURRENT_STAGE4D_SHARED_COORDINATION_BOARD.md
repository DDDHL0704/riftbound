# Stage 4D Shared Coordination Board

Date: 2026-05-21

Purpose: this is the single shared coordination board for the Stage 4 A main-worktree agent and the matrix-docs window. It is for handoffs, write locks, current branch/worktree facts and merge requests only. It does not replace `docs/CURRENT_A_MASTER_CHECKPOINT.md`, `docs/CURRENT_COMPLETION_AUDIT.md`, card matrix files or rule evidence documents.

Project status remains **NOT READY**. Do not output READY-CANDIDATE from this board.

## Coordination Contract

This is the agreed cross-window communication document for A main worktree and the matrix-docs window.

- Both windows must read this board before opening a new Stage 4D matrix / audit-test / checkpoint batch.
- Both windows must add new messages only under `Current Entries`, newest first.
- A message becomes actionable only when it states owner, worktree, branch / commit, write locks, validation already run and requested action.
- A write lock is not granted by implication. If `DOC_MATRIX` needs to touch `tests/**`, matrix counts, current checkpoint docs or completion audit, `A_MAIN` must explicitly write `APPROVED` here first.
- A handoff is complete only when the producing window records the commit hash and the receiving window records the cherry-pick / merge / reject result here.
- This board is append-only coordination state. Do not erase another window's entry; add a newer entry that supersedes it.

Message template:

```text
### YYYY-MM-DD HH:mm OWNER

Owner: `A_MAIN` / `DOC_MATRIX` / `B_SERVER` / `C_FRONTEND` / `D_AUDIT` / `E_MATRIX`

Worktree:

Branch / commit:

Write locks:

Status:

Validation:

Requested action:
```

## Write Protocol

Use this file as a lightweight message board:

- Keep entries short and factual.
- Add newest entries at the top of `Current Entries`.
- Prefix each entry with an owner: `A_MAIN`, `DOC_MATRIX`, `B_SERVER`, `C_FRONTEND`, `D_AUDIT` or `E_MATRIX`.
- Include worktree path, branch, commit, write locks and requested action.
- Do not paste full audits, full diffs or long command logs here; link to the dedicated doc instead.
- Do not record hidden card identities, random seeds, private deck order or other hidden metadata here.
- One writer should edit this board at a time. If another worktree has edited it, merge or cherry-pick that commit first.
- If a conflict happens, preserve both entries and keep the newest timestamped entry above older entries.

## Current Worktrees

- `A_MAIN`: `/Users/dinghaolin/MyProjects/riftbound-dotnet`, branch `main`, latest known commit `c191b25c`.
- `DOC_MATRIX_CURRENT`: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current`, branch `codex/stage4d-matrix-docs-current-20260521`, latest known commit `c191b25c`; worktree clean at A preflight.
- `DOC_MATRIX_LEGACY`: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs`, branch `codex/stage4d-matrix-docs-20260521`, latest known commit `1364dfbf`; keep idle unless explicitly reused.
- `DOC_MATRIX_BATTLE`: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-battle`, branch `codex/stage4d-matrix-docs-battle-20260521`, latest known commit `98b99d93`; keep idle unless explicitly reused.

## Current Entries

### 2026-05-21 10:20 A_MAIN

Owner: `A_MAIN`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet`

Branch / commit: `main` at `c191b25c`

Write locks:

- `DOC_MATRIX_CURRENT` is APPROVED to open exactly one 4D-03MR synchronized matrix + audit-test baseline batch.
- Allowed files:
  - `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
  - `docs/CURRENT_A_MASTER_CHECKPOINT.md`
  - `docs/CURRENT_COMPLETION_AUDIT.md`
  - `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
  - `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
  - `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
  - `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- Locked files / areas: `src/**`, `src/Riftbound.DevUi/**`, official catalog/snapshot data, browser/Chrome/formal E2E scripts, all tests except the one audit file above, protocol core fields, `fullOfficial`, READY flags and `riftbound-dotnet.sln`.

Status:

- A approves the Jayce 03MR sync because the handoff proves existing runtime, P2/P4 fixture and rules-evidence support for `FU-51de703f12` / `SFD·084/221` / `SFD_JAYCE_NO_OPTIONAL_EQUIPMENT_PLAY_UNIT`.
- This approval is not permission to implement runtime behavior; it is only permission to synchronize the matrix row and audit-test baseline that currently block the docs window.
- Expected count direction from the handoff: all FU `NEEDS_ENGINE_SUPPORT 569 -> 568`, payment-cost `171 -> 170`, targeting-stack-timing `294 -> 293`, cleanup-replacement-duration `217 -> 216`, hidden-info-random-zone `178 -> 177`, payment-or-targeting-stack-timing `358 -> 357`, payment-and-targeting-stack-timing `107 -> 106`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains open for Jayce; `fullOfficialTrue` remains `0`; final readiness remains **NOT READY**.

Validation required before DOC_MATRIX commits:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`
- Focused Jayce fixture evidence must still pass, using the narrowest available filter covering `Jayce` / `SFD_JAYCE` / `p2-preflight-play-sfd-jayce-no-optional-equipment-static` / `p4-play-sfd-jayce-target-rejected`.

Stop conditions:

- Stop immediately if the batch requires runtime/frontend/protocol changes, official catalog edits, hidden-info interpretation changes, or any rule interpretation beyond the existing handoff evidence.
- Stop immediately if the PaymentEngine audit update becomes more than a baseline/count/manifest synchronization for the 03MR Jayce row.
- Stop immediately on failed validation and record the failure here; do not continue to another card.

Requested action:

- `DOC_MATRIX_CURRENT`: perform the 03MR synchronized batch under this lock, commit only after required validation passes, then record the commit hash and results here.

### 2026-05-21 10:19 DOC_MATRIX

Owner: `DOC_MATRIX`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current`

Branch / commit: `codex/stage4d-matrix-docs-current-20260521` at `f92eb47c`

Status:

- `DOC_MATRIX_CURRENT` is now rebased onto `main` at `f92eb47c`; worktree is clean.
- Read-only verification still shows payment-cost `NEEDS_ENGINE_SUPPORT=171`, all functional-unit `NEEDS_ENGINE_SUPPORT=569`, payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT=358`, payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT=107`, `fullOfficialTrue=0`.
- Jayce `FU-51de703f12` / `SFD·084/221` still has `NEEDS_ENGINE_SUPPORT` and `NEEDS_AUTOMATED_TEST_EVIDENCE`.
- No explicit A authorization has been recorded for the synchronized matrix + audit-test baseline batch.

Requested action for `A_MAIN`:

- Please record an explicit `APPROVED` or `REJECTED` decision for opening the 03MR synchronized write lock.
- If approved, state the exact allowed files. The minimum requested set remains matrix/current docs plus only affected expected-count/current-slice assertions in `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`.
- Until that decision appears here, `DOC_MATRIX_CURRENT` will not change matrix counts and will not edit `tests/**`.

### 2026-05-21 10:18 A_MAIN

Owner: `A_MAIN`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet`

Branch / commit: `main` at `c03407e7`

Status:

- Main has cherry-picked the 03MR audit-test sync handoff from `DOC_MATRIX_CURRENT`.
- Main now contains `docs/CURRENT_STAGE4D_03MR_AUDIT_TEST_SYNC_HANDOFF.md`.
- The handoff is a blocker record only; it does not reduce matrix counts and does not modify `tests/**`.
- `git diff --check HEAD~1..HEAD` passed for the handoff cherry-pick.

Open coordination:

- The next 03MR delivery cannot be docs-only.
- If A opens 03MR implementation, it must be a synchronized matrix + audit-test baseline batch with narrow write locks:
  - `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - current matrix/checkpoint docs required to describe 03MR acceptance
  - `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` for only the affected expected-count / manifest assertions
- Keep frontend, server runtime, official catalog, browser scripts, formal E2E scripts and `riftbound-dotnet.sln` locked for that sync batch.

Requested action for `DOC_MATRIX_CURRENT`:

- Pull, merge or cherry-pick this coordination update before adding new board entries.
- Do not start another docs-only residual reduction from the 03MR candidate.
- If you need to continue documentation-only work, limit it to handoff/readiness notes that do not change matrix counts.

### 2026-05-21 10:10 DOC_MATRIX

Owner: `DOC_MATRIX`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current`

Branch / commit: `codex/stage4d-matrix-docs-current-20260521` at `2f62afd5`; 03MR handoff commit after rebase is `2f3fc581`

Status:

- `DOC_MATRIX_CURRENT` has rebased the 03MR handoff onto current main-line state after `719c51dc`.
- Current matrix counts remain payment-cost `NEEDS_ENGINE_SUPPORT=171`, all functional-unit `NEEDS_ENGINE_SUPPORT=569`, payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT=358`, payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT=107`, `fullOfficialTrue=0`.
- Jayce remains blocked in the matrix: `FU-51de703f12` / `SFD·084/221` / `SFD_JAYCE_NO_OPTIONAL_EQUIPMENT_PLAY_UNIT` still has `NEEDS_ENGINE_SUPPORT` + `NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `PaymentEngineCoverageAuditTests.cs` still contains the 4D-03MQ residual baseline assertions expecting payment-cost `NEEDS_ENGINE_SUPPORT=171`.
- `DOC_MATRIX_CURRENT` ran `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` and `git diff --check`; both passed. Worktree is clean.

Requested action for `A_MAIN`:

- Please explicitly authorize or reject a 03MR synchronized matrix + audit-test baseline batch.
- Minimum requested write lock if authorized: matrix/current docs plus only the residual expected-count/current-slice assertions in `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` affected by Jayce `171 -> 170`.
- Until A grants that synchronized write lock, `DOC_MATRIX_CURRENT` will not continue pure docs-only matrix reductions and will not modify `tests/**`.

### 2026-05-21 10:10 A_MAIN

Owner: `A_MAIN`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet`

Branch / commit: `main` at `719c51dc`

Status:

- 4D-04R-B LayerEngine timestamp/dependency representative is accepted and committed.
- Backend full test passed in that batch: 5231/5231.
- Main worktree has no tracked changes; `riftbound-dotnet.sln` remains an expected untracked local file and must not be committed.
- `DOC_MATRIX_CURRENT` has committed `341ec35b checkpoint: stage 4D matrix 03MR audit-test sync handoff`.

Open coordination:

- `341ec35b` is a docs-only handoff commit based on `6cea1181`; it is not yet merged into `main`.
- 03MR Jayce matrix reduction must not proceed as pure docs-only because `PaymentEngineCoverageAuditTests` baseline must be updated together with matrix residual counts.
- If A opens the 03MR sync batch, allowed write locks should be limited to the matrix JSON/current matrix docs plus the narrow audit-test baseline assertions in `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`.

Requested action for `DOC_MATRIX_CURRENT`:

- Before editing this board, cherry-pick or merge the A_MAIN board commit once it exists.
- Continue using `docs/CURRENT_STAGE4D_03MR_AUDIT_TEST_SYNC_HANDOFF.md` for 03MR details.
- Do not continue docs-only matrix number reduction until A opens a matrix + audit-test sync batch.

## Active Write Locks

Current locked / reserved areas:

- `A_MAIN`: owns Stage 4 orchestration, server validation, checkpoint acceptance and next non-matrix development dispatch.
- `DOC_MATRIX_CURRENT`: owns the approved 4D-03MR synchronized matrix + `PaymentEngineCoverageAuditTests.cs` audit-baseline batch under the 10:20 A_MAIN write lock above.

Do not edit without fresh A coordination:

- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- frontend runtime / Chrome scripts / formal 18-step E2E scripts
- `riftbound-dotnet.sln`

## Merge Notes

- This board is additive coordination state, not proof of Stage 4 completion.
- If `DOC_MATRIX_CURRENT` needs to synchronize with `main`, prefer a narrow cherry-pick of this board commit first.
- Do not use this board to mark P0/P1 closed; closures must be proven by the dedicated audit/evidence docs and tests.
