# Stage 4D-07Z Cleanup Redaction Snapshot Prompt Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / cleanup redaction snapshot-prompt audit slice. Project remains **NOT READY**.

## Scope

This slice covers state-based cleanup snapshot and prompt redaction for two cleanup surfaces: a hidden illegal standby task viewed by the opponent, and a public unattached equipment recall task viewed by its controller.

The accepted coverage proves hidden illegal standby cleanup keeps the authoritative state queue on the raw cleanup task while the opponent snapshot redacts the hidden standby object id from the active task id and task item, exposes hidden object kind, exposes no `objectId`, hides standby ids from the opponent battlefield lane, preserves hidden standby count and pending task kind, and keeps the opponent WAIT / SURRENDER prompt free of raw cleanup kind, cleanup id and hidden object id leakage. It also proves unattached equipment cleanup keeps exact public queue / task identity in the controller snapshot while its WAIT / SURRENDER prompt exposes only the localized equipment-cleanup reason without raw task kind, raw cleanup reason, cleanup id or equipment object id leakage.

No runtime behavior change was required because the existing snapshot and prompt redaction paths already separate authoritative task identity from public-facing hidden-info and prompt copy.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `IllegalStandbyAndUnattachedEquipmentTasksRedactHiddenAndRawPromptDetails`.
- Added assertions for authoritative cleanup queue identity, opponent battlefield lane redaction, opponent pending queue redaction, public equipment queue identity and prompt raw-detail redaction.

## Non-Closure

This narrows cleanup snapshot / prompt redaction audit parity only. It does not close full cleanup / replacement-duration breadth, full hidden-info random-zone breadth, full reconnect breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
