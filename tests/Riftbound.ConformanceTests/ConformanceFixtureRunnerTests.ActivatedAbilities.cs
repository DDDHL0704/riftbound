using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task P6BattlefieldEffectCatalogAuditsImplementedBattlefieldRuleRepresentativesAgainstOfficialText()
    {
        var surfaces = P6BattlefieldEffectCatalog.GetImplementedBattlefieldRuleSurfaces();

        Assert.Equal(4, surfaces.Count);
        Assert.Empty(P6BattlefieldEffectCatalog.GetDeferredSurfaces());
        Assert.Contains(surfaces, surface => surface.IsActivatedCommandSurface);
        Assert.Contains(surfaces, surface => !surface.IsActivatedCommandSurface);
        Assert.Contains(surfaces, surface => string.Equals(
            surface.SurfaceKind,
            P6BattlefieldEffectCatalog.TriggerSurfaceKind,
            StringComparison.Ordinal));
        Assert.Contains(surfaces, surface => string.Equals(
            surface.SurfaceKind,
            P6BattlefieldEffectCatalog.ReplacementSurfaceKind,
            StringComparison.Ordinal));
        Assert.Contains(surfaces, surface => string.Equals(
            surface.SurfaceKind,
            P6BattlefieldEffectCatalog.StaticKeywordSurfaceKind,
            StringComparison.Ordinal));
        Assert.All(surfaces, surface =>
        {
            Assert.False(P4ActivatedAbilityCatalog.TryGetByAbilityId(surface.SurfaceId, out _));
            Assert.False(string.IsNullOrWhiteSpace(surface.Reason));
            Assert.Contains("BATTLEFIELD_RULE_DOMAIN", surface.Reason, StringComparison.Ordinal);
        });

        var officialCatalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var functionalUnits = FunctionalUnitBuilder.Build(officialCatalog.Cards);
        var implementedBehaviors = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(
            officialCatalog.Cards,
            CardBehaviorRegistry.GetAll()
                .Select(definition => new ImplementedCardBehavior(
                    definition.CardNo,
                    definition.EffectKind,
                    definition.DisplayName))
                .ToArray());
        var behaviorSpecs = BehaviorSpecCatalogBuilder.Build(
            officialCatalog.Cards,
            functionalUnits,
            implementedBehaviors);

        foreach (var surface in surfaces)
        {
            var officialCard = officialCatalog.Cards.Single(card =>
                string.Equals(card.CardNo, surface.SourceCardNo, StringComparison.Ordinal));
            var parsed = RuleTextParser.Parse(officialCard);

            Assert.Equal("战场", officialCard.CardCategoryName);
            Assert.Contains(surface.OfficialTextAnchor, officialCard.CardEffect, StringComparison.Ordinal);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(surface.SourceCardNo, out _));
            Assert.Contains(behaviorSpecs, spec =>
                string.Equals(spec.CardNo, surface.SourceCardNo, StringComparison.Ordinal)
                && string.Equals(spec.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal)
                && string.Equals(spec.ImplementedEffectKind, OfficialRuleDomainBehaviorCatalog.BattlefieldRuleDomainEffectKind, StringComparison.Ordinal));

            switch (surface.SurfaceKind)
            {
                case P6BattlefieldEffectCatalog.ActivatedGrantSurfaceKind:
                    Assert.NotEmpty(parsed.ActivatedAbilities);
                    break;
                case P6BattlefieldEffectCatalog.TriggerSurfaceKind:
                    Assert.NotEmpty(parsed.Triggers);
                    break;
                case P6BattlefieldEffectCatalog.ReplacementSurfaceKind:
                    Assert.NotEmpty(parsed.Replacements);
                    break;
                case P6BattlefieldEffectCatalog.StaticKeywordSurfaceKind:
                    Assert.NotEmpty(parsed.Keywords);
                    Assert.NotEmpty(parsed.StaticAbilities);
                    break;
                default:
                    Assert.Fail($"Unexpected battlefield surface kind '{surface.SurfaceKind}'.");
                    break;
            }
        }
    }

    public static IEnumerable<object[]> P6ImplementedBattlefieldRuleActivatedSurfaceData()
    {
        return P6BattlefieldEffectCatalog.GetImplementedBattlefieldRuleSurfaces()
            .Where(surface => surface.IsActivatedCommandSurface)
            .Select(surface => new object[] { surface });
    }

    [Theory]
    [MemberData(nameof(P6ImplementedBattlefieldRuleActivatedSurfaceData))]
    public async Task P6ActivateAbilityCommandRejectsImplementedBattlefieldRuleCommandRepresentativesOutsideRegistry(
        P6ImplementedBattlefieldRuleSurface surface)
    {
        var targetObjectIds = Enumerable.Range(1, surface.TargetCount)
            .Select(index => $"P1-BATTLEFIELD-TARGET-{index:000}")
            .ToArray();
        var battlefieldObjectIds = new[] { "P1-BATTLEFIELD-RULE-SOURCE" }
            .Concat(targetObjectIds)
            .ToArray();
        var cardObjects = targetObjectIds
            .Select(targetObjectId => new KeyValuePair<string, CardObjectState>(
                targetObjectId,
                new CardObjectState(
                    targetObjectId,
                    power: 2,
                    tags: [CardObjectTags.UnitCard])))
            .Concat(
            [
                new KeyValuePair<string, CardObjectState>(
                    "P1-BATTLEFIELD-RULE-SOURCE",
                    new CardObjectState(
                        "P1-BATTLEFIELD-RULE-SOURCE",
                        power: 0,
                        cardNo: surface.SourceCardNo,
                        ownerId: "P1",
                        controllerId: "P1"))
            ])
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var state = PunishmentState(mana: 10) with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 3,
                ["P2"] = 0
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = battlefieldObjectIds
                },
                ["P2"] = PlayerZones.Empty
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(10, 10),
                ["P2"] = RunePool.Empty
            },
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-p6-battlefield-rule-activate-ability-reject-{surface.SurfaceId}", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-BATTLEFIELD-RULE-SOURCE",
                surface.SurfaceId,
                targetObjectIds),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前启动技能路径尚未由服务端开放。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(10, 10), result.State.RunePools["P1"]);
        Assert.Equal(3, result.State.PlayerExperience["P1"]);
        Assert.Equal(battlefieldObjectIds, result.State.PlayerZones["P1"].Battlefields);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-RULE-SOURCE"].IsExhausted);
        foreach (var targetObjectId in targetObjectIds)
        {
            Assert.Equal(0, result.State.CardObjects[targetObjectId].Damage);
        }

        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P6TokenFactoryCatalogAuditsDeferredRuleSurfacesAgainstOfficialText()
    {
        var surfaces = P6TokenFactoryCatalog.GetDeferredRuleSurfaces();

        Assert.Empty(surfaces);
        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            "TOKEN_DEFERRED_GOLD_REACTION_DESTROY_EXHAUST_GAIN_A_UNL",
            StringComparison.Ordinal));
        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            "TOKEN_DEFERRED_GOLD_REACTION_DESTROY_EXHAUST_GAIN_A_SFD",
            StringComparison.Ordinal));
        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.ImageCopySurfaceId,
            StringComparison.Ordinal));
        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.BrushReplacementSurfaceId,
            StringComparison.Ordinal));
        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.BaronNestMoveStaticSurfaceId,
            StringComparison.Ordinal));
        Assert.All(surfaces, surface =>
        {
            Assert.False(P4ActivatedAbilityCatalog.TryGetByAbilityId(surface.SurfaceId, out _));
            Assert.False(string.IsNullOrWhiteSpace(surface.Reason));
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(surface.SourceCardNo, out _));
        });

        await Task.CompletedTask;
    }

    [Fact]
    public async Task P6TokenFactoryCatalogAuditsImplementedRuleSurfacesAgainstOfficialText()
    {
        var surfaces = P6TokenFactoryCatalog.GetImplementedRuleSurfaces();

        Assert.Equal(3, surfaces.Count);
        Assert.Contains(surfaces, surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.ImageCopySurfaceId,
            StringComparison.Ordinal));
        Assert.Contains(surfaces, surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.BaronNestMoveStaticSurfaceId,
            StringComparison.Ordinal));
        Assert.Contains(surfaces, surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.BrushReplacementSurfaceId,
            StringComparison.Ordinal));

        var officialCatalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        foreach (var surface in surfaces)
        {
            Assert.False(surface.IsActivatedCommandSurface);
            Assert.False(P4ActivatedAbilityCatalog.TryGetByAbilityId(surface.SurfaceId, out _));
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(surface.SourceCardNo, out var definition));
            var officialCard = officialCatalog.Cards.Single(card =>
                string.Equals(card.CardNo, surface.SourceCardNo, StringComparison.Ordinal));
            var parsed = RuleTextParser.Parse(officialCard);

            Assert.StartsWith("指示物", officialCard.CardCategoryName, StringComparison.Ordinal);
            Assert.Contains(surface.OfficialTextAnchor, officialCard.CardEffect, StringComparison.Ordinal);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(surface.SourceCardNo, out _));
            switch (surface.SurfaceKind)
            {
                case P6TokenFactoryCatalog.CopyTokenSurfaceKind:
                    Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, surface.SourceCardNo);
                    Assert.Equal(1, surface.TargetCount);
                    Assert.True(definition.RequiresCopySource);
                    Assert.Contains(P6TokenFactoryCatalog.CopySourceRequiredTag, definition.Tags);
                    break;
                case P6TokenFactoryCatalog.BattlefieldStaticSurfaceKind:
                    Assert.Equal(P6TokenFactoryCatalog.BaronNestTokenCardNo, surface.SourceCardNo);
                    Assert.Equal(0, surface.TargetCount);
                    Assert.Equal("指示物战场", definition.CategoryName);
                    Assert.False(definition.RequiresCopySource);
                    Assert.NotEmpty(parsed.StaticAbilities);
                    break;
                case P6TokenFactoryCatalog.BattlefieldReplacementSurfaceKind:
                    Assert.Equal(P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo, surface.SourceCardNo);
                    Assert.Equal(0, surface.TargetCount);
                    Assert.Equal("指示物战场", definition.CategoryName);
                    Assert.False(definition.RequiresCopySource);
                    Assert.NotEmpty(parsed.Replacements);
                    break;
                default:
                    Assert.Fail($"Unexpected implemented token surface kind '{surface.SurfaceKind}'.");
                    break;
            }
        }
    }

    [Fact]
    public void P6TokenFactoryCatalogHasNoDeferredActivatedTokenCommandSurfaces()
    {
        Assert.DoesNotContain(
            P6TokenFactoryCatalog.GetDeferredRuleSurfaces(),
            surface => surface.IsActivatedCommandSurface);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandAddsViDoublePowerSkillToStack()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-VI"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPELLSHIELD-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new(
                    "P1-UNIT-VI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: "UNL-030/219"),
                ["P2-SPELLSHIELD-UNIT-001"] = new(
                    "P2-SPELLSHIELD-UNIT-001",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-ability-premodel", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                "PAY_2_RED_DOUBLE_POWER",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], result.State.PlayerZones["P2"].Battlefields);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1", stackItem.ControllerId);
        Assert.Equal("P1-UNIT-VI", stackItem.SourceObjectId);
        Assert.Equal("UNL-030/219", stackItem.CardNo);
        Assert.Equal("VI_PAY_2_RED_DOUBLE_POWER_UNTIL_END_OF_TURN", stackItem.EffectKind);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Equal("BASE", result.State.ObjectLocations["P1-UNIT-VI"].Zone);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations["P2-SPELLSHIELD-UNIT-001"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Equal(["ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(evt => evt.Kind));
    }

    [Fact]
    public async Task ActivateAbilityDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-VI"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPELLSHIELD-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new(
                    "P1-UNIT-VI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: "UNL-030/219"),
                ["P2-SPELLSHIELD-UNIT-001"] = new(
                    "P2-SPELLSHIELD-UNIT-001",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield])
            }
        };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new ActivateAbilityCommand(
            "P1-UNIT-VI",
            "PAY_2_RED_DOUBLE_POWER",
            []);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.ActivateAbility, prompt.EnabledActions());
        var rawCommand = PromptScopedRawCommand(CommandTypes.ActivateAbility, prompt);
        const string clientIntentId = "intent-p1-activate-ability-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref rawCommand),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), accepted.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI"], accepted.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], accepted.State.PlayerZones["P2"].Battlefields);
        var stackItem = Assert.Single(accepted.State.StackItems);
        Assert.Equal("P1", stackItem.ControllerId);
        Assert.Equal("P1-UNIT-VI", stackItem.SourceObjectId);
        Assert.Equal("UNL-030/219", stackItem.CardNo);
        Assert.Equal("VI_PAY_2_RED_DOUBLE_POWER_UNTIL_END_OF_TURN", stackItem.EffectKind);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Equal("BASE", accepted.State.ObjectLocations["P1-UNIT-VI"].Zone);
        Assert.Equal("BATTLEFIELD", accepted.State.ObjectLocations["P2-SPELLSHIELD-UNIT-001"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, accepted.State.TimingState);
        Assert.Equal("P1", accepted.State.PriorityPlayerId);
        Assert.Equal(PromptTypes.StackPriority, accepted.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, accepted.Prompts["P1"].EnabledActions());
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.ActivateAbility, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.ActivateAbility, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref rawCommand),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(["ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], replay.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), replay.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI"], replay.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], replay.State.PlayerZones["P2"].Battlefields);
        var replayStackItem = Assert.Single(replay.State.StackItems);
        Assert.Equal(stackItem.StackItemId, replayStackItem.StackItemId);
        Assert.Equal("P1-UNIT-VI", replayStackItem.SourceObjectId);
        Assert.Equal("UNL-030/219", replayStackItem.CardNo);
        Assert.Equal("VI_PAY_2_RED_DOUBLE_POWER_UNTIL_END_OF_TURN", replayStackItem.EffectKind);
        Assert.Empty(replayStackItem.TargetObjectIds);
        Assert.Equal(TimingStates.NeutralClosed, replay.State.TimingState);
        Assert.Equal("P1", replay.State.PriorityPlayerId);
        Assert.Equal(PromptTypes.StackPriority, replay.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, replay.Prompts["P1"].EnabledActions());
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.ActivateAbility,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref changedRawCommand),
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(new RunePool(0, 0), conflict.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI"], conflict.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], conflict.State.PlayerZones["P2"].Battlefields);
        var conflictStackItem = Assert.Single(conflict.State.StackItems);
        Assert.Equal(stackItem.StackItemId, conflictStackItem.StackItemId);
        Assert.Equal("P1-UNIT-VI", conflictStackItem.SourceObjectId);
        Assert.Equal("UNL-030/219", conflictStackItem.CardNo);
        Assert.Equal("VI_PAY_2_RED_DOUBLE_POWER_UNTIL_END_OF_TURN", conflictStackItem.EffectKind);
        Assert.Empty(conflictStackItem.TargetObjectIds);
        Assert.Equal(TimingStates.NeutralClosed, conflict.State.TimingState);
        Assert.Equal("P1", conflict.State.PriorityPlayerId);
        Assert.Equal(PromptTypes.StackPriority, conflict.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, conflict.Prompts["P1"].EnabledActions());
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P7TypedPowerPaymentActivatesViSkillWithTraitPool()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-VI"]
                },
                ["P2"] = PlayerZones.Empty
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    2,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new(
                    "P1-UNIT-VI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: "UNL-030/219")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-activate-vi-trait-power", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                "PAY_2_RED_DOUBLE_POWER",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.RunePools["P1"].PowerByTrait);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("VI_PAY_2_RED_DOUBLE_POWER_UNTIL_END_OF_TURN", stackItem.EffectKind);
        Assert.Equal(["ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(evt => evt.Kind));
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("ACTIVATE_ABILITY", costPaidEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-UNIT-VI", costPaidEvent.Payload["sourceObjectId"]);
        Assert.Equal("PAY_2_RED_DOUBLE_POWER", costPaidEvent.Payload["abilityId"]);
        Assert.Equal("VI_PAY_2_RED_DOUBLE_POWER_UNTIL_END_OF_TURN", costPaidEvent.Payload["reason"]);
        Assert.Equal(2, costPaidEvent.Payload["totalManaCost"]);
        Assert.Equal(1, costPaidEvent.Payload["genericPower"]);
        Assert.Equal(1, costPaidEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costPaidEvent.Payload["remainingMana"]);
        Assert.Equal(0, costPaidEvent.Payload["remainingPower"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costPaidEvent.Payload["remainingPowerByTrait"]));
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillOutsideOpenMainWindowWithChineseError()
    {
        var state = PunishmentState(mana: 2) with
        {
            TimingState = TimingStates.NeutralClosed,
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-VI"]
                },
                ["P2"] = PlayerZones.Empty
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new(
                    "P1-UNIT-VI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: "UNL-030/219")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-vi-timing-rejected", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                "PAY_2_RED_DOUBLE_POWER",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("启动技能只能在当前玩家的开放主阶段提交。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillWithTargets()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-VI"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPELLSHIELD-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new(
                    "P1-UNIT-VI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: "UNL-030/219"),
                ["P2-SPELLSHIELD-UNIT-001"] = new(
                    "P2-SPELLSHIELD-UNIT-001",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-ability-target-rejected", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                "PAY_2_RED_DOUBLE_POWER",
                ["P2-SPELLSHIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("该技能不接受目标或额外费用。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 1), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillWithOptionalCosts()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-VI"]
                },
                ["P2"] = PlayerZones.Empty
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new(
                    "P1-UNIT-VI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: "UNL-030/219")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-ability-optional-cost-rejected", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                "PAY_2_RED_DOUBLE_POWER",
                [],
                ["UNSUPPORTED_OPTIONAL"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("该技能不接受目标或额外费用。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 1), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(3, result.State.CardObjects["P1-UNIT-VI"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillWhenPowerIsMissing()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-VI"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new(
                    "P1-UNIT-VI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: "UNL-030/219")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-ability-missing-power", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                "PAY_2_RED_DOUBLE_POWER",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal("资源不足，无法启动该技能。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillFromNonViSource()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-PORO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-PORO"] = new(
                    "P1-UNIT-PORO",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: "OGN·013/298")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-ability-non-vi-source", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-PORO",
                "PAY_2_RED_DOUBLE_POWER",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("该来源没有服务端支持的启动技能。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 1), result.State.RunePools["P1"]);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillFromOpponentSource()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-UNIT-VI"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-VI"] = new(
                    "P2-UNIT-VI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: "UNL-030/219")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-vi-opponent-source", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P2-UNIT-VI",
                "PAY_2_RED_DOUBLE_POWER",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("启动技能来源必须是当前玩家控制且可支付费用的场上物体。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 1), result.State.RunePools["P1"]);
        Assert.Equal(3, result.State.CardObjects["P2-UNIT-VI"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViOpponentControlledSourceInPlayerZone()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-VI-OPPONENT-CONTROLLED"]
                },
                ["P2"] = PlayerZones.Empty
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI-OPPONENT-CONTROLLED"] = new(
                    "P1-UNIT-VI-OPPONENT-CONTROLLED",
                    cardNo: "UNL-030/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-vi-opponent-controlled-player-zone", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI-OPPONENT-CONTROLLED",
                "PAY_2_RED_DOUBLE_POWER",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("启动技能只能选择当前玩家控制的来源。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 1), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI-OPPONENT-CONTROLLED"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(3, result.State.CardObjects["P1-UNIT-VI-OPPONENT-CONTROLLED"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillWhenSourceIsNotField()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-VI"]
                },
                ["P2"] = PlayerZones.Empty
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-VI"] = new(
                    "P1-HAND-VI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: "UNL-030/219")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-vi-source-not-field", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-HAND-VI",
                "PAY_2_RED_DOUBLE_POWER",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 1), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-VI"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, result.State.CardObjects["P1-HAND-VI"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandResolvesViDoublePowerSkillOnStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-vi-double-power-skill.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-VI"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-VI"].UntilEndOfTurnPowerModifier);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandUsesCardIdentityAfterViIsPlayed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-then-activate-vi-double-power-skill.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal("UNL-030/219", result.FinalState.CardObjects["P1-UNIT-VI"].CardNo);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-VI"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-VI"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillTargetFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-vi-double-power-skill-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 1), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillOptionalCostFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-vi-double-power-skill-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 1), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-VI"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillPowerMissingFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-vi-double-power-skill-power-missing-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-VI"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-VI"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillNonViSourceFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-vi-double-power-skill-non-vi-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 1), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-PORO"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-PORO"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillOpponentSourceFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-vi-double-power-skill-opponent-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 1), result.FinalState.RunePools["P1"]);
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-VI"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsViDoublePowerSkillSourceNotFieldFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-vi-double-power-skill-source-not-field-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 1), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-VI"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(3, result.FinalState.CardObjects["P1-HAND-VI"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandAddsXerathDamageSkillWithSpellshieldTaxToStack()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPELLSHIELD-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 2),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-SPELLSHIELD-UNIT-001"] = new(
                    "P2-SPELLSHIELD-UNIT-001",
                    power: 5,
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-spellshield-tax", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-SPELLSHIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.True(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1", stackItem.ControllerId);
        Assert.Equal("P1-UNIT-XERATH", stackItem.SourceObjectId);
        Assert.Equal("UNL-026/219", stackItem.CardNo);
        Assert.Equal("XERATH_PAY_RED_EXHAUST_DAMAGE_3", stackItem.EffectKind);
        Assert.Equal(3, stackItem.DamageAmount);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], stackItem.TargetObjectIds);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations["P1-UNIT-XERATH"].Zone);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations["P2-SPELLSHIELD-UNIT-001"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Equal(["ABILITY_ACTIVATED", "COST_PAID", "UNIT_EXHAUSTED", "STACK_ITEM_ADDED"], result.Events.Select(evt => evt.Kind));
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.Equal(0, costPaidEvent.Payload["mana"]);
        Assert.Equal(2, costPaidEvent.Payload["power"]);
        Assert.Equal("ACTIVATE_ABILITY", costPaidEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-UNIT-XERATH", costPaidEvent.Payload["sourceObjectId"]);
        Assert.Equal("PAY_RED_EXHAUST_DAMAGE_3", costPaidEvent.Payload["abilityId"]);
        Assert.Equal("XERATH_PAY_RED_EXHAUST_DAMAGE_3", costPaidEvent.Payload["reason"]);
        Assert.Equal(0, costPaidEvent.Payload["totalManaCost"]);
        Assert.Equal(2, costPaidEvent.Payload["genericPower"]);
        Assert.Equal(2, costPaidEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costPaidEvent.Payload["remainingMana"]);
        Assert.Equal(0, costPaidEvent.Payload["remainingPower"]);
        Assert.Equal(1, costPaidEvent.Payload["spellshieldTaxPower"]);
        Assert.Equal(
            ["P2-SPELLSHIELD-UNIT-001"],
            Assert.IsType<string[]>(costPaidEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task P7TypedPowerPaymentActivatesXerathSkillWithTraitPool()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPELLSHIELD-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 2
                    }),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-SPELLSHIELD-UNIT-001"] = new(
                    "P2-SPELLSHIELD-UNIT-001",
                    power: 5,
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-activate-xerath-trait-power", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-SPELLSHIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.RunePools["P1"].PowerByTrait);
        Assert.True(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("XERATH_PAY_RED_EXHAUST_DAMAGE_3", stackItem.EffectKind);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], stackItem.TargetObjectIds);
        Assert.Equal(["ABILITY_ACTIVATED", "COST_PAID", "UNIT_EXHAUSTED", "STACK_ITEM_ADDED"], result.Events.Select(evt => evt.Kind));
    }

    [Fact]
    public async Task P4ActivateAbilityCommandDoesNotTaxXerathDamageSkillForFriendlySpellshieldTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-UNIT-XERATH",
                        "P1-SPELLSHIELD-UNIT-001"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P1-SPELLSHIELD-UNIT-001"] = new(
                    "P1-SPELLSHIELD-UNIT-001",
                    power: 5,
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-friendly-spellshield-no-tax", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P1-SPELLSHIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.True(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-SPELLSHIELD-UNIT-001", Assert.Single(stackItem.TargetObjectIds));
        Assert.Equal("XERATH_PAY_RED_EXHAUST_DAMAGE_3", stackItem.EffectKind);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.Equal(0, costPaidEvent.Payload["mana"]);
        Assert.Equal(1, costPaidEvent.Payload["power"]);
        Assert.Equal(0, costPaidEvent.Payload["spellshieldTaxPower"]);
        Assert.Empty(Assert.IsType<string[]>(costPaidEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillOutsideOpenMainWindowWithChineseError()
    {
        var state = PunishmentState(mana: 1) with
        {
            TimingState = TimingStates.NeutralClosed,
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "SFD·125/221")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-timing-rejected", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("启动技能只能在当前玩家的开放主阶段提交。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSourceIsExhausted()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-exhausted-source", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("泽拉斯的技能来源必须未横置。", result.ErrorMessage);
        Assert.DoesNotContain("Xerath", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, result.State.RunePools["P1"].TotalPower);
        Assert.True(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsMissing()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-missing-target", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("泽拉斯的技能需要且只能选择 1 个单位目标。", result.ErrorMessage);
        Assert.DoesNotContain("Xerath", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, result.State.RunePools["P1"].TotalPower);
        Assert.False(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillTargetWithoutCardNo()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"] = new(
                    "P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-unknown-target", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("泽拉斯的技能目标必须是场上的单位。", result.ErrorMessage);
        Assert.DoesNotContain("Xerath", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, result.State.RunePools["P1"].TotalPower);
        Assert.False(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal(0, result.State.CardObjects["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTooManyTargetsAreProvided()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P2-UNIT-001",
                        "P2-UNIT-002"
                    ]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-UNIT-002"] = new(
                    "P2-UNIT-002",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-too-many-targets", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                [
                    "P2-UNIT-001",
                    "P2-UNIT-002"
                ]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("泽拉斯的技能需要且只能选择 1 个单位目标。", result.ErrorMessage);
        Assert.DoesNotContain("Xerath", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 1), result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal(0, result.State.CardObjects["P2-UNIT-001"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-UNIT-002"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillWithOptionalCosts()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-optional-cost", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-UNIT-001"],
                ["ECHO"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("泽拉斯的技能不接受额外费用。", result.ErrorMessage);
        Assert.DoesNotContain("Xerath", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 1), result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal(0, result.State.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillFromNonXerathSource()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-VI"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new(
                    "P1-UNIT-VI",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-030/219"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-non-xerath-source", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("该来源没有服务端支持的泽拉斯技能。", result.ErrorMessage);
        Assert.DoesNotContain("Xerath", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 1), result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P1-UNIT-VI"].IsExhausted);
        Assert.Equal(0, result.State.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsNotUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-EQUIPMENT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-EQUIPMENT-001"] = new(
                    "P2-EQUIPMENT-001",
                    tags: [CardObjectTags.EquipmentCard],
                    cardNo: "SFD·022/221")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-non-unit-target", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("泽拉斯的技能目标必须是场上的单位。", result.ErrorMessage);
        Assert.DoesNotContain("Xerath", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 1), result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard], result.State.CardObjects["P2-EQUIPMENT-001"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSourceIsNotBattlefield()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-source-not-battlefield", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("泽拉斯的技能来源必须是当前玩家控制的战场单位。", result.ErrorMessage);
        Assert.DoesNotContain("Xerath", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 1), result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal(0, result.State.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillFromOpponentSource()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-XERATH", "P2-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-XERATH"] = new(
                    "P2-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-opponent-source", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P2-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 1), result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P2-UNIT-XERATH"].IsExhausted);
        Assert.Equal(0, result.State.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathOpponentControlledSourceInPlayerZone()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH-OPPONENT-CONTROLLED"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH-OPPONENT-CONTROLLED"] = new(
                    "P1-UNIT-XERATH-OPPONENT-CONTROLLED",
                    cardNo: "UNL-026/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-opponent-controlled-player-zone", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH-OPPONENT-CONTROLLED",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("启动技能只能选择当前玩家控制的来源。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 1), result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P1-UNIT-XERATH-OPPONENT-CONTROLLED"].IsExhausted);
        Assert.Equal(0, result.State.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSpellshieldTaxPowerIsMissing()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPELLSHIELD-UNIT-001"]
                }
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-026/219"),
                ["P2-SPELLSHIELD-UNIT-001"] = new(
                    "P2-SPELLSHIELD-UNIT-001",
                    power: 5,
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-activate-xerath-spellshield-tax-insufficient", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                "PAY_RED_EXHAUST_DAMAGE_3",
                ["P2-SPELLSHIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal("资源不足，无法启动泽拉斯的技能。", result.ErrorMessage);
        Assert.DoesNotContain("Xerath", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 1), result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandResolvesXerathDamageSkillWithSpellshieldTax()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-spellshield-tax.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.True(result.FinalState.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal(3, result.FinalState.CardObjects["P2-SPELLSHIELD-UNIT-001"].Damage);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillSpellshieldTaxInsufficientFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-spellshield-tax-insufficient-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 1), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-XERATH"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal(0, result.FinalState.CardObjects["P2-SPELLSHIELD-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandResolvesXerathDamageSkillWithoutTaxForFriendlySpellshield()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-friendly-spellshield-no-tax.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.True(result.FinalState.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal(3, result.FinalState.CardObjects["P1-SPELLSHIELD-UNIT-001"].Damage);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillFromExhaustedSourceFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-exhausted-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 1), result.FinalState.RunePools["P1"]);
        Assert.True(result.FinalState.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillMissingTargetFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-missing-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 1), result.FinalState.RunePools["P1"]);
        Assert.False(result.FinalState.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillTooManyTargetsFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-too-many-targets-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 1), result.FinalState.RunePools["P1"]);
        Assert.False(result.FinalState.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-002"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillOptionalCostFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 1), result.FinalState.RunePools["P1"]);
        Assert.False(result.FinalState.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillNonXerathSourceFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-non-xerath-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 1), result.FinalState.RunePools["P1"]);
        Assert.False(result.FinalState.CardObjects["P1-UNIT-VI"].IsExhausted);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillNonUnitTargetFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-non-unit-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 1), result.FinalState.RunePools["P1"]);
        Assert.False(result.FinalState.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P2-EQUIPMENT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillSourceNotBattlefieldFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-source-not-battlefield-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 1), result.FinalState.RunePools["P1"]);
        Assert.False(result.FinalState.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ActivateAbilityCommandRejectsXerathDamageSkillOpponentSourceFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-activate-xerath-damage-skill-opponent-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 1), result.FinalState.RunePools["P1"]);
        Assert.False(result.FinalState.CardObjects["P2-UNIT-XERATH"].IsExhausted);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }
}
