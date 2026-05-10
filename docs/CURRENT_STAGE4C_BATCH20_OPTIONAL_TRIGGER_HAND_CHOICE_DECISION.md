# Stage 4C-20A Optional Trigger / Triggered Hand-Choice Decision Brief

Date: 2026-05-10

Status: **DECISION REQUIRED / NOT READY**

This document records A-side triage for the next Stage 4C slice after the Kogmaw representative baseline. It is not an implementation report and does not mark any functional unit full-official.

## Scope

Candidate cards:

- Karthus / 卡尔萨斯 `OGN·236/298`, `FU-ee1dfb3ed3`, effect kind `OGN_KARTHUS_LAST_BREATH_STATIC_PLAY_UNIT`.
- Undercover Agent / 卧底特工 `OGN·178/298`, `FU-6a52b04cb2`, effect kind `UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT`.

Observed official text from frozen snapshot:

- Karthus: your Last Breath effects can trigger one additional time.
- Undercover Agent: Last Breath, discard two cards from hand, then draw two cards.

## Extracted Rule / FAQ Evidence

Local PDF extraction on 2026-05-10 added two concrete evidence points:

- `BREAK-JFAQ-260416 p3`: when Karthus / 卡尔萨斯 and Gragas Bartender / 戈拉斯克调酒师 are destroyed at the same time, the FAQ answer says Gragas Bartender's Last Breath triggers twice.
- `CORE-260330 p62`, rule `422.4`: when discard is part of an effect, the player discards the maximum possible count; the Undercover Agent example states that a player with two or more cards discards two, with one card discards one, with no hand cards discards none, and draws two regardless of how many cards were discarded.

Impact:

- Karthus simultaneous-destruction behavior is partially evidenced for at least the Karthus + other Last Breath source case.
- Undercover Agent hand-size shortfall is no longer an open rules question.
- Neither evidence removes the need for server-authoritative prompt handling, viewer redaction, stale protection, and no-mutation validation.

## Server Authority Constraints

- The server must decide legality, visibility, prompt timing, trigger ordering, hidden information, and state mutation.
- The frontend may only render authoritative snapshot / prompt data and submit prompt candidates.
- The frontend must not infer optional trigger legality, choose hidden hand cards locally without a server prompt, or decide whether a Karthus repeat is legal.

## Karthus Decision Points

Karthus is global static modifier behavior, not a self-contained Last Breath resolver.

Open questions:

- Does "can trigger one additional time" require an optional yes/no decision per Last Breath trigger?
- Is the extra occurrence modeled by copying the trigger before `ORDER_TRIGGERS`, copying the effect during resolution, or by a replacement/static modifier attached to trigger generation?
- If multiple Karthus objects are controlled, do they stack?
- If Karthus dies simultaneously with another Last Breath source, the Karthus + Gragas Bartender FAQ case says the other Last Breath source triggers twice; broader simultaneous-death and self-modification boundaries still need engine-level modeling.
- Do hidden, face-down, standby, or non-field Karthus objects modify Last Breath triggers?
- Is the optional choice made before `ORDER_TRIGGERS`, during `ORDER_TRIGGERS`, or when the trigger resolves?

Recommended ruling before implementation:

- Do not default Karthus to "yes" in official paths without an explicit server prompt or explicit user-approved representative-only downgrade.
- Treat Karthus as requiring an optional trigger-repeat decision before the extra trigger is inserted into `TriggerQueue`.
- Keep multi-Karthus stacking, simultaneous Karthus death, hidden original visibility, and full trigger batch multiplicity as P0/P1 until separately adjudicated.

## Undercover Agent Decision Points

Undercover Agent is a self-contained Last Breath resolver, but it needs hidden hand choice at trigger resolution time.

Open questions:

- Should the choice window open only at `TRIGGER_RESOLVED` time, after all previous stack/priority changes?
- What public event payload should be visible to the opponent before and after discard?
- Are discarded card identities public once they move to graveyard, or must viewer redaction differ by zone / replay frame?

Resolved by `CORE-260330 p62`, rule `422.4`:

- Discard as many as possible up to two cards.
- Draw two even if fewer than two cards were discarded.

Recommended ruling before implementation:

- Do not auto-discard the first two hand cards.
- Add a server-authoritative triggered hand-choice prompt before mutating hand.
- Only the choosing player sees candidate hand object ids / card identities.
- The opponent sees a waiting prompt and public summary only.
- Wrong player, stale prompt, unknown object, duplicate object, not-in-hand object, and malformed payload must reject with no mutation.
- If the choosing player has fewer than two cards in hand, the prompt must require the available count, not exactly two; zero-card hands should not open a useless choice prompt and must still draw two through the server-authoritative resolver.

## Proposed Prompt Skeletons

These names are proposals only; do not freeze protocol fields until user confirmation.

`OPTIONAL_TRIGGER_DECISION`:

- `promptId`
- `snapshotTick`
- `triggerPrototypeId`
- `choosingPlayerId`
- `sourceObjectId`
- `sourceCardNo`
- `effectKind`
- `decisionKind`: `REPEAT_LAST_BREATH`
- `choices`: `ACCEPT`, `DECLINE`
- viewer redaction for non-choosing players

`CHOOSE_HAND_CARDS_FOR_TRIGGER`:

- `promptId`
- `snapshotTick`
- `triggerId`
- `choosingPlayerId`
- `sourceObjectId`
- `sourceCardNo`
- `effectKind`
- `requiredCount`: `2`
- `candidateObjectIds`
- per-viewer candidate card details only for the choosing player
- public summary for non-choosing players

## Recommended 4C-20A Gate

Before implementing Karthus or Undercover Agent, close a protocol/rules micro-slice:

- Document the optional trigger-repeat timing and hand-choice timing.
- Add prompt / command skeletons only if user approves the field shape.
- Add no-mutation validation tests for wrong player, stale prompt, unknown choice, duplicate choice, hidden information redaction, and malformed payload.
- Do not mark Karthus or Undercover Agent full-official.
- Do not alter the 1009 / 811 full-official counts.

## Current A Recommendation

Proceed with 4C-20A as a decision / protocol micro-slice before any Karthus or Undercover effect implementation.

If the user explicitly authorizes a representative-only downgrade, Karthus can be implemented with default "repeat yes" for a narrow visible single-Karthus / single-Last-Breath source path, but that must remain P1 and must not be described as official optional behavior.

## Still Open P0/P1

- Karthus optional repeat semantics.
- Karthus multiple-object stacking.
- Karthus broader simultaneous-death, self-modification, multi-object, and visibility boundaries.
- Undercover Agent hand-choice prompt.
- Undercover Agent public/private discard event redaction.
- Full trigger engine and trigger batch model.
- Hidden / face-down original trigger modeling.
- FAQ regression.
- 1009 entries / 811 functional units full-official coverage.
- Formal 18-step E2E and completion audit.
