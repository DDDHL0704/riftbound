# Stage 4D-04S PaymentEngine Lux High-Cost Paid-Cost Audit

Date: 2026-05-21
Conclusion: **IMPLEMENTED AND A-VALIDATED / WRITELOCK CLOSED / PROJECT NOT READY**

This file records the A-side acceptance for 4D-04S. B-Implementation / Faraday `019e483b-a316-7312-a641-a7b8f3921814` completed the Lux high-cost spell paid-cost representative slice, and A reviewed the diff and reran focused, adjacent, full backend and patch hygiene validation.

This slice keeps the server as the sole rules authority. It does not modify frontend behavior, the card coverage matrix, official catalog data, protocol core fields, Chrome/browser scripts, formal 18-step E2E scripts, `fullOfficial`, READY state or `riftbound-dotnet.sln`.

## 1. Scope

Runtime / test write scope used:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/LuxHighCostPaidCostTriggerTests.cs`

Not touched:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- official card catalog / fixed snapshot data
- frontend runtime / DevUi stores
- Chrome / browser scripts
- formal 18-step E2E scripts
- protocol core fields
- `fullOfficial` / READY status
- `riftbound-dotnet.sln`

## 2. Accepted Behavior

4D-04S is accepted because A verified all of the following:

1. Lux unit high-cost spell trigger now checks the resolved paid mana from the server payment plan instead of printed card cost.
2. Lux Intro Deck legend high-cost draw trigger now uses the same resolved paid mana threshold.
3. A printed high-cost spell reduced below 5 paid mana does not trigger Lux unit power or Lux legend draw.
4. A lower printed-cost spell raised to 5 paid mana by Spellshield tax triggers Lux unit power and Lux legend draw.
5. A Spellshield-tax path with insufficient payment is rejected and leaves state unmutated.
6. Draw events remain hidden-info safe: opponent snapshots do not expose the hidden drawn card object id or card number.

## 3. Hidden Information Review

The new tests assert that the opponent snapshot for the Lux legend draw path does not contain the hidden draw object id or hidden drawn card number. The public `CARD_DRAWN` event only exposes `playerId` and `count`.

This slice does not change hidden-info filtering internals and does not expose private deck order, private hand identities, face-down standby identities, hidden random results or server hidden metadata.

## 4. Protocol / Snapshot Contract

No protocol core field was changed. The runtime change is internal server-authoritative trigger qualification based on the already resolved `PaymentPlan.TotalManaCost`.

Frontend remains display-only and must continue to submit only server prompt / command candidates. It must not compute Lux trigger legality, cost legality, Spellshield tax legality, draw legality or hidden-info visibility locally.

## 5. A-Side Validation

Validation details are recorded in `docs/CURRENT_STAGE4D_04S_PAYMENTENGINE_LUX_HIGH_COST_PAID_COST_EVIDENCE.md`:

- 04S focused Lux paid-cost tests: 3/3 passed.
- Lux / HighCost / RealTriggerQueue focused-adjacent filter: 81/81 passed.
- PaymentEngine / trigger / Lux adjacent filter: 174/174 passed.
- Backend full test: 5234/5234 passed.
- `git diff --check`: passed.

## 6. Residual Risk

Still open:

- full PaymentEngine official breadth;
- complete high-cost trigger breadth beyond these Lux representatives;
- broader trigger queue / APNAP ordering coverage;
- card matrix fullOfficial closure;
- 03MR DOC_MATRIX synchronized matrix + audit-baseline batch;
- FAQ evidence breadth;
- frontend final validation;
- Chrome smoke;
- formal 18-step E2E;
- completion audit and READY.

## 7. Verdict

4D-04S is accepted and its runtime / focused-test write lock is closed. Project remains **NOT READY**.
