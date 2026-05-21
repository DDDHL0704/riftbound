# Stage 4D-04T Trigger APNAP Ordering Evidence

Date: 2026-05-21

Owner: `B_SERVER`

Project status: **NOT READY**

## Scope

This slice is test/evidence-only. It adds focused conformance coverage for the narrow trigger queue / `ORDER_TRIGGERS` APNAP ordering breadth prompt:

- APNAP prompt metadata remains server-authored.
- Non-ordering players are rejected without mutation.
- Missing, duplicate and foreign trigger ids are rejected without mutation.
- Within-controller reordering is accepted inside a legal controller block.
- Cross-controller reordering remains rejected by the existing representative test.
- Hidden trigger source details remain redacted from opponent prompts, opponent snapshots and public order events.

No runtime behavior, protocol core fields, official catalog, frontend, matrix JSON, formal E2E scripts, or PaymentEngine code changed in this slice.

## Validation

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrderTriggers|FullyQualifiedName~TriggerQueue|FullyQualifiedName~APNAP|FullyQualifiedName~RealTriggerQueue"
```

Result: passed 70/70, failed 0, skipped 0.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~GameHub|FullyQualifiedName~OrderTriggers|FullyQualifiedName~TriggerPayment"
```

Result: passed 396/396, failed 0, skipped 0.

```sh
git diff --check
```

Result: passed.

## A-Side Acceptance Validation

A_MAIN reviewed the diff and reran validation on the main worktree before committing this slice:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrderTriggers|FullyQualifiedName~TriggerQueue|FullyQualifiedName~APNAP|FullyQualifiedName~RealTriggerQueue"
```

Result: passed 70/70, failed 0, skipped 0.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~GameHub|FullyQualifiedName~OrderTriggers|FullyQualifiedName~TriggerPayment"
```

Result: passed 396/396, failed 0, skipped 0.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 5240/5240, failed 0, skipped 0.

```sh
git diff --check
```

Result: passed.

## Findings

- Runtime bug fixed: none found.
- Hidden-info leak found: none in the added prompt/snapshot/public-event assertions.
- Protocol shape changed: no.
- Remaining open items: A_MAIN still owns integration, acceptance, broader Stage 4 gates, formal E2E / frontend acceptance, card-matrix readiness and any READY decision.

Final conclusion: **NOT READY**.
