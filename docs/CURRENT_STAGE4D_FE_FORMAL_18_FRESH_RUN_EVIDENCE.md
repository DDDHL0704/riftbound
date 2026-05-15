# Stage 4D Frontend Formal 18-Step Fresh-Run Evidence

日期：2026-05-16
结论：**VALIDATED / PROJECT NOT READY**

## Command

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run e2e:formal-18 -- --start-api
```

## Result

- Exit code: 0.
- API started on `http://127.0.0.1:5088`.
- Vite preview started on `http://127.0.0.1:5174/`.
- P1 Chrome DevTools opened on `127.0.0.1:9340`.
- P2 Chrome DevTools opened on `127.0.0.1:9341`.
- Formal room: `formal-18-1778886172096-1`.
- Active player: `P1`.
- P1 battlefield: `OGN·276/298`.
- P2 battlefield: `OGN·290/298`.
- Steps 1 through 18 all reported `OK`.
- Final script result: `Formal 18-step E2E passed: formal-18-1778886172096-1`.

## Step Summary

1. Room creation / P1 join passed.
2. P2 join passed.
3. Legal official deck submission passed.
4. Ready / official mulligan start passed.
5. Mulligan confirmation passed.
6. First turn rune call and draw passed.
7. Server-provided rune tap passed.
8. Server `ActionPrompt` play unit action passed.
9. Stack priority pass-pass passed.
10. Stack resolution into base passed.
11. Legal server movement destination passed.
12. Active-player authoritative reconnect passed.
13. End turn passed.
14. Opponent turn start passed.
15. Same-game battlefield score resolution passed.
16. Opponent reconnect to scored state passed.
17. Server surrender command path passed.
18. Result page authoritative winner passed.

## Worktree

Before this docs update, `git status --short --branch` showed only expected untracked `riftbound-dotnet.sln`.

## Non-Closure

This evidence does not close P0/P1, full-card matrix, full official PaymentEngine / LayerEngine, strict battlefield / battle lifecycle breadth, or READY. It only proves the current code state passes the formal 18-step main-flow E2E gate after the 4D-FE build and Chrome smoke fresh-runs.
