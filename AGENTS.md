# Riftbound Project Agent Guide

## Rule Authority

- Treat the official PDF set, `data/official`, Riot official pages, and `playloltcg` card text as the only rule authorities.
- Community sites, GitHub projects, Stitch output, Tabletop Simulator mats, and old Java behavior are references only. They can inspire UI, workflow, tooling, or regression fixtures, but they cannot override official rules.
- Before changing gameplay behavior, read the matching entry in `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`. Add or update evidence when a new rule surface is implemented.
- Generic mechanics must be implemented in the shared rule engine. Do not patch a single card when the behavior is a keyword, timing window, payment source, battlefield lifecycle, trigger family, or zone/information rule.

## Project Map

- `src/Riftbound.Engine`: authoritative gameplay state, prompts, legality, command handling, rule domains, and snapshots.
- `src/Riftbound.Contracts`: protocol shared by API, clients, tests, and Dev UI. Keep this typed and version-aware.
- `src/Riftbound.Api`: hubs, HTTP endpoints, scenario seeding, static Dev UI hosting, and development CORS.
- `src/Riftbound.DevUi`: React battlefield client. It renders server snapshots and prompts; it must not decide rules locally.
- `tests/Riftbound.*`: conformance, engine, API, and regression gates.
- `docs`: rule authority, evidence, handoff, audits, and implementation contracts.
- `Mods`: Tabletop Simulator and community playmat references. Use for spatial/layout inference only.

## Engine Work

- Model each gameplay feature as protocol -> engine primitive -> event/snapshot -> conformance test.
- Preserve hidden information boundaries in snapshots. The UI should never receive opponent hand order, deck order, unrevealed rune identity, or face-down standby identity.
- Prompts must expose only legal choices and safe summaries. The frontend submits intents with prompt/tick identity; the server remains the judge.
- When using old fixtures, keep the distinction between `legacyOracle`, official-rule expected behavior, and current engine evidence.

## Frontend Work

- The battle desktop follows a TTS-style table layout, but the rules decide what each zone means.
- Zone coordinates live in `src/Riftbound.DevUi/src/components/match/tabletopLayoutData.json` and are checked by `npm --prefix src/Riftbound.DevUi run check:tabletop-layout`.
- The rune deck size is fixed at 12. The UI may display remaining count, drawn rune slots, and revealed rune cards supplied by the server, but it must not infer hidden rune identities.
- Battlefield control, score state, legal actions, pending tasks, and prompts must be rendered from server snapshots/events. Do not derive them from card placement alone.
- If a design reference conflicts with these constraints, keep the rule contract and adapt the visual design.

## Validation

- Frontend quick gate: `npm --prefix src/Riftbound.DevUi run build`.
- Frontend visual/accessibility gate: `npm --prefix src/Riftbound.DevUi run qa:appshots`.
- Browser smoke: `npm --prefix src/Riftbound.DevUi run smoke:chrome`.
- Backend rule changes should include focused tests first, then adjacent/full tests when the blast radius touches shared rule domains.
- Run `git diff --check` before handoff. Do not call the project ready unless the relevant documented gates have actually passed.
