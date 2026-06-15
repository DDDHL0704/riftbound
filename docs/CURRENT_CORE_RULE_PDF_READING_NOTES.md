# Current Core Rule PDF Reading Notes

Date: 2026-06-15

Purpose: record the user-supplied root-level rule PDFs as a standing Stage 4D implementation gate. This document is an index and working summary only; it does not copy the PDF text. When a runtime/server slice touches game rules, prompts, timing, scoring, hidden information, card legality, costs, triggers, battle, standby, equipment, or replay semantics, re-check the relevant PDF section before changing code or tests.

## Source PDFs

Canonical local source directory: `/Users/dinghaolin/IdeaProjects/riftbound`.

- `/Users/dinghaolin/IdeaProjects/riftbound/《符文战场》核心规则_260330.pdf`
- `/Users/dinghaolin/IdeaProjects/riftbound/铸魂淬炼系列_官方FAQ_260114.pdf`
- `/Users/dinghaolin/IdeaProjects/riftbound/裁判FAQ_251023.pdf`
- `/Users/dinghaolin/IdeaProjects/riftbound/《符文战场》破限系列_裁判FAQ_260416.pdf`
- `/Users/dinghaolin/IdeaProjects/riftbound/铸魂淬炼系列_裁判FAQ.pdf`

Working text was extracted on 2026-06-15 with `pdftotext -layout` to `/tmp/riftbound_rules_pdf_text/`:

- `core_rules_260330.txt` (`6540` lines)
- `soulforged_official_faq_260114.txt` (`565` lines)
- `judge_faq_251023.txt` (`516` lines)
- `breaking_limits_judge_faq_260416.txt` (`399` lines)
- `soulforged_judge_faq.txt` (`897` lines)

Recreate the working text with:

```bash
mkdir -p /tmp/riftbound_rules_pdf_text
pdftotext -layout "/Users/dinghaolin/IdeaProjects/riftbound/《符文战场》核心规则_260330.pdf" /tmp/riftbound_rules_pdf_text/core_rules_260330.txt
pdftotext -layout "/Users/dinghaolin/IdeaProjects/riftbound/铸魂淬炼系列_官方FAQ_260114.pdf" /tmp/riftbound_rules_pdf_text/soulforged_official_faq_260114.txt
pdftotext -layout "/Users/dinghaolin/IdeaProjects/riftbound/裁判FAQ_251023.pdf" /tmp/riftbound_rules_pdf_text/judge_faq_251023.txt
pdftotext -layout "/Users/dinghaolin/IdeaProjects/riftbound/《符文战场》破限系列_裁判FAQ_260416.pdf" /tmp/riftbound_rules_pdf_text/breaking_limits_judge_faq_260416.txt
pdftotext -layout "/Users/dinghaolin/IdeaProjects/riftbound/铸魂淬炼系列_裁判FAQ.pdf" /tmp/riftbound_rules_pdf_text/soulforged_judge_faq.txt
```

## Precedence

Use this precedence when implementing or auditing behavior:

1. Latest core rules: `《符文战场》核心规则_260330.pdf`.
2. Official FAQ/errata: `铸魂淬炼系列_官方FAQ_260114.pdf`, where it clarifies or overrides specific behavior.
3. Judge FAQ sources as adjudication and design-answer evidence, especially the 2026-04-16 `破限系列` FAQ because it is based on the 2026-03-30 core rules.
4. Older judge FAQ notes remain useful only where not superseded by newer core rules or official FAQ.

If implementation notes, existing tests, or older docs conflict with the latest core rules plus official FAQ, treat the PDFs as the source of truth and document the reconciliation in the slice audit.

## Standing Gate

- Before a Stage 4D slice changes rules-adjacent server/runtime behavior, read the relevant extracted text or source PDF sections again.
- Record the rule source in the slice audit when the slice depends on a rule interpretation.
- Prefer tests that encode the rule as an observable server contract without exposing hidden/private card identities, random seeds, private deck order, or other hidden metadata.
- Keep protocol-envelope and replay validation grounded in canonical turn timing and visibility rules; do not add replay assertions that require clients or spectators to see private state.
- For pure protocol-envelope tests with no rule-behavior change, still check whether the command surface is rule-sensitive and mention the PDFs when relevant.

## High-Signal Rule Anchors

- Card text overrides rules when they fundamentally conflict; "cannot" overrides "can"; execute as much as possible and ignore impossible instructions.
- Main deck and rune deck order are hidden. Battlefield, base, permanents, runes, common game area objects, and public states are public. Hands and face-down standby cards are private.
- Only one game action is processed at a time. Simultaneous effects and triggers are ordered by the core timing rules, with turn-player order where applicable.
- Priority and focus are distinct. Priority grants discretionary action permission; focus drives spell-duel opportunity order.
- Normal open state allows default play/activate actions for the current priority holder. Spell-duel open state allows Swift/Reaction permissions. Closed state allows Reaction permissions.
- Cleanup is pending after relevant state changes and repeats until no further state change. It does not resolve legal stack items or pass priority/focus during cleanup.
- Stack handling follows the HOT / Finalize / Execute / Pass / Resolve structure. Resource-gain skills and some pending items resolve immediately and do not open ordinary priority/focus passing.
- Spell duels start around contested battlefields, use focus, and close when all players pass focus without adding playable items. Non-battle spell-duel closure can establish battlefield control and conquest.
- Targets and choices are selected before legality confirmation. Illegal actions are cancelled/undone at legality check. At resolution, invalid targets are ignored and the effect resolves as much as possible.
- Costs include base cost, mandatory extra costs such as Spellshield, optional extra costs, increases, and reductions. Replacement effects that replace costs can still count as paid costs.
- Triggered skills are added after triggering events resolve, can be optional, and are ordered by controller and turn-player order. Declining an optional triggered cost removes that trigger from the stack rather than countering it.
- Battle assigns attacker/defender identities, resolves combat damage assignment before simultaneous damage, then battle cleanup determines win/loss/no-result, control/conquest, and identity removal.
- Conquest and hold scoring are one point per battlefield per turn. Cleanup win checks require threshold and lead over opponents.
- Destroy is not movement. Last Breath is recorded before the destroyed permanent moves to discard and does not trigger if replacement prevents the discard movement.
- Burnout moves as many cards as possible, recycles discard into deck as needed, and can chain repeated score gain during the same draw/move instruction.
- Move is immediate, uses no stack, and only units move. Invalid battle destinations can recall instead. Roam expands standard move options.
- Attach/unattach is not movement. Top card movement carries attachments; top leaving the field detaches attachments at the prior location. Illegal unattached equipment is recalled during cleanup.
- Haste is an optional extra play cost and can enter active directly. Swift and Reaction are permission keywords, not function changes. Spellshield tax is paid per target selection according to the official FAQ. Standby is not playing a card and gives face-down cards special later play permissions.

## Slice-Relevant Reminders

- GameHub replay/protocol-envelope tests must preserve player identity normalization, event kind stability, snapshot/prompt fanout, server tick stability, and protocol/schema defaults without asserting private information leakage.
- `OBJECTS_READIED` is rule-backed by wakeup-phase readying and must remain represented in clients/log labels.
- Pay-cost, PlayCard, ActivateAbility, triggered-cost, and Spellshield surfaces should cite the cost/targeting/stack sections when behavior changes.
- MoveUnit, DeclareBattle, AssignCombatDamage, Pass, EndTurn, OrderTriggers, HideCard, RevealCard, Standby, equipment, and scoring slices are directly rule-sensitive and should re-check the corresponding anchors before implementation.

Project status remains **NOT READY**.
