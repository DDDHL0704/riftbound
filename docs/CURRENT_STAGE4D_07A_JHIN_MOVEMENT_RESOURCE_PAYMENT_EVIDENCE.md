# Stage 4D-07A Jhin Movement Resource Payment Evidence

Date: 2026-05-22

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/JhinMovementResourceSkillTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_07A_JHIN_MOVEMENT_RESOURCE_PAYMENT_AUDIT.md`
- `docs/CURRENT_STAGE4D_07A_JHIN_MOVEMENT_RESOURCE_PAYMENT_EVIDENCE.md`

## Validation

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore --filter "FullyQualifiedName~JhinMovementResourceSkillGainsManaAndPaymentOnlyPowerWithoutStackResponse|FullyQualifiedName~JhinGeneratedManaAndPowerCanPayLaterLegalRuneCostThenClear"
```

Result: passed `2/2`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore --filter "FullyQualifiedName~JhinMovementResourceSkillTests|FullyQualifiedName~Jhin|FullyQualifiedName~MoveUnit|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PayCost|FullyQualifiedName~PAY_COST|FullyQualifiedName~PaymentEngine|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `1375/1375`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore
```

Result: passed `5404/5404`.

```bash
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05*.md docs/CURRENT_STAGE4D_06*.md docs/CURRENT_STAGE4D_07*.md
```

Result: passed. No whitespace warnings, conflict markers, or trailing whitespace in the new 05J-07A evidence docs.

## Runtime And Protocol

Runtime changed: no. Existing Jhin movement-resource handling already emits correlated trigger / activation / mana / power events, creates a payment-only temporary power resource and lets pending `PAY_COST` quote, spend, clear and record that resource through the PaymentEngine.

Protocol shape changed: no.

Hidden-info leakage found: no.
