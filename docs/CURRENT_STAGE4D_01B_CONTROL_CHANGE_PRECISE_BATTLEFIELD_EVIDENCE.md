# Stage 4D-01B Control Change Precise Battlefield Evidence

日期：2026-05-14
结论：**FOCUSED / ADJACENT / FULL GREEN / PROJECT NOT READY**

## Validation

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HostileTakeover|FullyQualifiedName~BattlefieldControlLifecycle|FullyQualifiedName~BoardTaskQueueFoundation|FullyQualifiedName~PendingTaskQueue"
```

Result:

```text
Passed: 29, Failed: 0, Skipped: 0, Total: 29
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HostileTakeover|FullyQualifiedName~MoveUnit|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle|FullyQualifiedName~ObjectLocation"
```

Result:

```text
Passed: 175, Failed: 0, Skipped: 0, Total: 175
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4203, Failed: 0, Skipped: 0, Total: 4203
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Notes

The focused tests prove Hostile Takeover preserves precise `BattlefieldObjectId` across controller change, opens destination-scoped contest tasks when an opposing occupant remains, and does not open contest when no opposing occupant remains.
