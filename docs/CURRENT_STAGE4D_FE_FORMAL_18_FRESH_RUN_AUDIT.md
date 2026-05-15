# Stage 4D Frontend Formal 18-Step Fresh-Run Audit

日期：2026-05-16
结论：**FORMAL 18-STEP FRESH-RUN PASSED / PROJECT NOT READY**

本批是 A 主控在 4D-FE frontend build 与 Chrome smoke fresh-run 之后，对当前代码状态执行的 formal 18-step E2E fresh-run。它验证 A_MASTER §11 的连续正式主流程，但不修改 runtime、前端代码、E2E 脚本、card matrix JSON、`fullOfficial` 或 READY 状态。

## 1. Trigger

Fresh formal command:

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run e2e:formal-18 -- --start-api
```

The script started:

- API: `http://127.0.0.1:5088`
- Vite preview: `http://127.0.0.1:5174/`
- P1 Chrome DevTools: `ws://127.0.0.1:9340/devtools/browser/...`
- P2 Chrome DevTools: `ws://127.0.0.1:9341/devtools/browser/...`

Selected room:

- `formal-18-1778886172096-1`
- active player: `P1`
- P1 battlefield: `OGN·276/298`
- P2 battlefield: `OGN·290/298`

## 2. 18-Step Result

The script reported all steps as OK:

1. P1 creates room and joins through the server hub.
2. P2 joins the same room.
3. Both players submit legal official decks with a deterministic scoring battlefield candidate.
4. Both players ready and the server starts official mulligan.
5. Both players confirm mulligan choices.
6. First turn begins with server rune call and draw.
7. Active player taps server-provided runes for mana.
8. Active player plays a unit from the server `ActionPrompt`.
9. Both players pass priority on the stack window.
10. The played unit resolves from stack into base.
11. The unit moves to the opponent battlefield through a legal server destination.
12. Reconnect restores the active player's authoritative state before ending turn.
13. Active player ends turn.
14. Opponent turn begins.
15. Server resolves the first-turn battlefield score in the same continuous game.
16. Opponent browser reconnects and displays the scored state.
17. Opponent surrenders through the server command path.
18. Result page reflects the authoritative winner.

Final line:

- `Formal 18-step E2E passed: formal-18-1778886172096-1`

The run exited with code 0. Chrome emitted known local warnings such as allocator reload and deprecated GCM endpoint messages; they did not fail the run.

## 3. Scope And Non-Closure

This batch did not touch:

- server runtime / rules / protocols
- frontend runtime source
- Chrome smoke or formal E2E scripts
- card matrix JSON
- `fullOfficial` / READY
- `riftbound-dotnet.sln`

This is current-code formal 18-step main-flow evidence only. It does not close P0/P1, full-card matrix, full official PaymentEngine / LayerEngine, strict battlefield / battle lifecycle breadth, or final READY.
