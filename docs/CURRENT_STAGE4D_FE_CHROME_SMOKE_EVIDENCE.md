# Stage 4D Frontend Chrome Smoke Evidence

日期：2026-05-16
结论：**VALIDATED / PROJECT NOT READY**

## Command

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
```

## Result

- Exit code: 0.
- API started on `http://127.0.0.1:5088`.
- Vite preview started on `http://127.0.0.1:5173/`.
- Chrome DevTools endpoint opened on `127.0.0.1:9338`.
- `Chrome smoke OK`: `/`.
- `Chrome smoke OK`: `/lobby`.
- `Chrome smoke OK`: `/decks`.
- `Chrome smoke OK`: `/cards`.
- `Chrome smoke OK`: `/rooms/stage3-smoke`.
- `Chrome smoke OK`: `/matches/stage3-smoke`.
- `Chrome smoke OK`: `/matches/stage3-smoke/result`.
- Final script result: `Chrome smoke passed.`

## Worktree

Before this docs update, `git status --short --branch` showed only expected untracked `riftbound-dotnet.sln`.

## Non-Closure

This evidence does not close P0/P1, formal 18-step, full-card matrix, full official PaymentEngine / LayerEngine, or READY. It only proves the current code state passes the DevUi Chrome smoke route gate after the 4D-FE event-label build-gate fix.
