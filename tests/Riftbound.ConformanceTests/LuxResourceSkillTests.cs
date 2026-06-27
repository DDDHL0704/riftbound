using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LuxResourceSkillTests
{
    private const string LuxObjectId = "P1-LUX";
    private const string SecondLuxObjectId = "P1-LUX-TWO";
    private const string SpellObjectId = "P1-SPELL-BULLET-TIME";
    private const string EnemyObjectId = "P2-LUX-TEST-UNIT";
    private const string BulletTimeCardNo = "OGN·268/298";
    private const string EvolutionDayCardNo = "OGN·114/298";
    private const string ArenaCouncilorCardNo = "UNL-001/219";

    private static string LuxResourceAction => LuxResourceActionFor(LuxObjectId);
    private static string SecondLuxResourceAction => LuxResourceActionFor(SecondLuxObjectId);

    [Fact]
    public void CatalogExposesLuxSpellOnlyResourceSkill()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.LuxResourceAbilityId,
            out var ability));

        Assert.Equal(P4ActivatedAbilityCatalog.LuxCardNo, ability.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxResourceAbilityEffectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.PaymentOnlyResource);
        Assert.True(ability.ReactionSpeed);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, ability.GeneratedMana);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxSpellOnlyResourceRestriction, ability.ResourceRestriction);
    }

    [Fact]
    public void LuxSpellOnlySourceIdentityUsesAbilitySourceCardGroup()
    {
        var repositoryRoot = RepositoryRoot();
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));
        var matchSessionSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "MatchSession.cs"));

        Assert.DoesNotContain(
            "sourceState.CardNo, P4ActivatedAbilityCatalog.LuxCardNo",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "cardObject.CardNo, P4ActivatedAbilityCatalog.LuxCardNo",
            matchSessionSource,
            StringComparison.Ordinal);
        Assert.Contains("P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LuxSpellOnlyResourcePromptMakesShortManaSpellPlayable()
    {
        var state = BuildPlayState(mana: 0);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, LuxResourceAction, StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"]);
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, LuxResourceAction, StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, paymentResourcePowerByChoice[LuxResourceAction]["mana"]);
        Assert.Equal(true, paymentResourcePowerByChoice[LuxResourceAction]["paymentOnly"]);
        Assert.Equal(true, paymentResourcePowerByChoice[LuxResourceAction]["spellOnly"]);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxSpellOnlyResourceRestriction, paymentResourcePowerByChoice[LuxResourceAction]["resourceRestriction"]);
        Assert.Equal(0, sourceRequirement["availableMana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, sourceRequirement["availableManaWithPaymentResources"]);
    }

    [Fact]
    public void LuxSpellOnlyResourcePromptDoesNotMakeUnitPlayable()
    {
        var state = BuildPlayState(cardNo: ArenaCouncilorCardNo, mana: 3);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.False(playCandidate.Enabled);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
    }

    [Fact]
    public async Task LuxSpellOnlyResourcePaysSpellManaExhaustsSourceAndCleansLeftover()
    {
        var state = BuildPlayState(mana: 0);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-lux-spell-only-resource", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                SpellObjectId,
                BulletTimeCardNo,
                [],
                OptionalCosts: [LuxResourceAction]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects[LuxObjectId].IsExhausted);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(SpellObjectId, stackItem.SourceObjectId);
        Assert.Equal(BulletTimeCardNo, stackItem.CardNo);
        Assert.Equal(
            [
                "CARD_PLAYED",
                "ABILITY_ACTIVATED",
                "UNIT_EXHAUSTED",
                "MANA_GAINED",
                "TEMPORARY_PAYMENT_RESOURCE_SPENT",
                "TEMPORARY_PAYMENT_RESOURCE_CLEARED",
                "COST_PAID",
                "STACK_ITEM_ADDED"
            ],
            result.Events.Select(gameEvent => gameEvent.Kind));

        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.LuxResourceAbilityId, activatedEvent.Payload["abilityId"]);
        Assert.Equal("no-ordinary-stack-item", activatedEvent.Payload["stackPolicy"]);
        Assert.Equal(true, activatedEvent.Payload["generatedResourceCannotBeTargetedAsResponse"]);
        var manaEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, manaEvent.Payload["mana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, manaEvent.Payload["manaAfter"]);
        var spendEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(1, spendEvent.Payload["consumedMana"]);
        Assert.Equal(1, spendEvent.Payload["remainingMana"]);
        var cleanupEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        Assert.Equal(1, cleanupEvent.Payload["remainingManaBeforeCleanup"]);

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([LuxResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([LuxResourceAction], Assert.IsType<string[]>(costEvent.Payload["luxSpellOnlyResourceActions"]));
        Assert.Equal([LuxObjectId], Assert.IsType<string[]>(costEvent.Payload["luxSpellOnlyResourceSourceObjectIds"]));
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, costEvent.Payload["luxSpellOnlyGeneratedMana"]);
        Assert.Equal(1, costEvent.Payload["luxSpellOnlyConsumedMana"]);
        Assert.Equal(1, costEvent.Payload["luxSpellOnlyRemainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
    }

    [Fact]
    public async Task LuxSpellOnlyResourceUsesTwoReadyLuxSourcesForLargeSpellShortfallAndCleansEachInlineResource()
    {
        var state = BuildPlayState(cardNo: EvolutionDayCardNo, mana: 2, includeSecondLux: true);
        var luxResourceActions = new[] { LuxResourceAction, SecondLuxResourceAction };
        var luxSourceObjectIds = new[] { LuxObjectId, SecondLuxObjectId };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-lux-two-spell-only-resources", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                SpellObjectId,
                EvolutionDayCardNo,
                [],
                OptionalCosts: luxResourceActions),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects[LuxObjectId].IsExhausted);
        Assert.True(result.State.CardObjects[SecondLuxObjectId].IsExhausted);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(SpellObjectId, stackItem.SourceObjectId);
        Assert.Equal(EvolutionDayCardNo, stackItem.CardNo);
        Assert.Equal(
            [
                "CARD_PLAYED",
                "ABILITY_ACTIVATED",
                "UNIT_EXHAUSTED",
                "MANA_GAINED",
                "ABILITY_ACTIVATED",
                "UNIT_EXHAUSTED",
                "MANA_GAINED",
                "TEMPORARY_PAYMENT_RESOURCE_SPENT",
                "TEMPORARY_PAYMENT_RESOURCE_CLEARED",
                "TEMPORARY_PAYMENT_RESOURCE_SPENT",
                "TEMPORARY_PAYMENT_RESOURCE_CLEARED",
                "COST_PAID",
                "STACK_ITEM_ADDED"
            ],
            result.Events.Select(gameEvent => gameEvent.Kind));

        var activatedEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(luxSourceObjectIds, activatedEvents.Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["sourceObjectId"])).ToArray());
        Assert.All(activatedEvents, gameEvent =>
        {
            Assert.Equal(P4ActivatedAbilityCatalog.LuxResourceAbilityId, gameEvent.Payload["abilityId"]);
            Assert.Equal("no-ordinary-stack-item", gameEvent.Payload["stackPolicy"]);
            Assert.Equal(true, gameEvent.Payload["generatedResourceCannotBeTargetedAsResponse"]);
        });

        var manaEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(luxSourceObjectIds, manaEvents.Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["sourceObjectId"])).ToArray());
        Assert.All(manaEvents, gameEvent =>
        {
            Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, gameEvent.Payload["mana"]);
            Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, gameEvent.Payload["generatedMana"]);
            Assert.Equal(true, gameEvent.Payload["paymentOnly"]);
            Assert.Equal(true, gameEvent.Payload["spellOnly"]);
            Assert.Equal(P4ActivatedAbilityCatalog.LuxSpellOnlyResourceRestriction, gameEvent.Payload["resourceRestriction"]);
        });
        Assert.Equal([4, 6], manaEvents.Select(gameEvent => Assert.IsType<int>(gameEvent.Payload["manaAfter"])).ToArray());

        var spendEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(luxSourceObjectIds, spendEvents.Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["sourceObjectId"])).ToArray());
        Assert.All(spendEvents, gameEvent =>
        {
            Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, gameEvent.Payload["consumedMana"]);
            Assert.Equal(0, gameEvent.Payload["remainingMana"]);
            Assert.Equal(true, gameEvent.Payload["paymentOnly"]);
            Assert.Equal(true, gameEvent.Payload["spellOnly"]);
            Assert.Equal(P4ActivatedAbilityCatalog.LuxSpellOnlyResourceRestriction, gameEvent.Payload["resourceRestriction"]);
        });

        var cleanupEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(luxSourceObjectIds, cleanupEvents.Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["sourceObjectId"])).ToArray());
        Assert.All(cleanupEvents, gameEvent =>
        {
            Assert.Equal(0, gameEvent.Payload["remainingManaBeforeCleanup"]);
            Assert.Equal(true, gameEvent.Payload["paymentOnly"]);
            Assert.Equal(true, gameEvent.Payload["spellOnly"]);
        });

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(6, costEvent.Payload["baseMana"]);
        Assert.Equal(6, costEvent.Payload["mana"]);
        Assert.Equal(luxResourceActions, Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal(luxResourceActions, Assert.IsType<string[]>(costEvent.Payload["luxSpellOnlyResourceActions"]));
        Assert.Equal(luxSourceObjectIds, Assert.IsType<string[]>(costEvent.Payload["luxSpellOnlyResourceSourceObjectIds"]));
        Assert.Equal(2 * P4ActivatedAbilityCatalog.LuxGeneratedMana, costEvent.Payload["luxSpellOnlyGeneratedMana"]);
        Assert.Equal(2 * P4ActivatedAbilityCatalog.LuxGeneratedMana, costEvent.Payload["luxSpellOnlyConsumedMana"]);
        Assert.Equal(0, costEvent.Payload["luxSpellOnlyRemainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);

        var nonSpellState = BuildPlayState(cardNo: ArenaCouncilorCardNo, mana: 2, includeSecondLux: true);
        var nonSpellInitialHash = MatchStateHasher.Hash(nonSpellState);
        var nonSpellResult = await new CoreRuleEngine().ResolveAsync(
            nonSpellState,
            new PlayerIntent("intent-lux-two-spell-only-resources-non-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                SpellObjectId,
                ArenaCouncilorCardNo,
                [],
                OptionalCosts: luxResourceActions),
            CancellationToken.None);

        Assert.False(nonSpellResult.Accepted);
        Assert.Empty(nonSpellResult.Events);
        Assert.Equal(nonSpellInitialHash, MatchStateHasher.Hash(nonSpellResult.State));
    }

    [Fact]
    public async Task LuxSpellOnlyResourcePlayCardStalePromptReplayUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildPlayState(mana: 0);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            SpellObjectId,
            BulletTimeCardNo,
            [],
            OptionalCosts: [LuxResourceAction]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, SpellObjectId, StringComparison.Ordinal));

        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-lux-resource-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-lux-resource-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        var acceptedStackItem = AssertLuxSpellOnlyResourceAcceptedState(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var p1PromptAfterAccepted = MatchStateHasher.HashValue(session.PromptFor("P1"));
        var p2PromptAfterAccepted = MatchStateHasher.HashValue(session.PromptFor("P2"));
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PlayCard, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(acceptedJournalEntry.RawCommand.Value));
        AssertPromptScopedPlayCardRawCommand(acceptedJournalEntry.RawCommand.Value, prompt, [LuxResourceAction]);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        AssertLuxSpellOnlyResourceAcceptedState(replay, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PlayCard, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(rejectedJournalEntry.RawCommand.Value));
        AssertPromptScopedPlayCardRawCommand(rejectedJournalEntry.RawCommand.Value, prompt, [LuxResourceAction]);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));
        var journalHashAfterReplay = MatchStateHasher.HashValue(journal.Entries);

        var duplicateReplay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateReplay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateReplay.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateReplay.ErrorMessage);
        Assert.Empty(duplicateReplay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(replay.State.Tick, duplicateReplay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        AssertLuxSpellOnlyResourceAcceptedState(duplicateReplay, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));

        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            changedStaleRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        AssertLuxSpellOnlyResourceAcceptedState(conflict, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("non-spell")]
    [InlineData("exhausted-source")]
    [InlineData("wrong-source-card")]
    [InlineData("unnecessary-resource")]
    [InlineData("duplicate-resource")]
    public async Task LuxSpellOnlyResourceRejectsInvalidCommandsWithoutMutation(string caseName)
    {
        var state = caseName switch
        {
            "non-spell" => BuildPlayState(cardNo: ArenaCouncilorCardNo, mana: 0),
            "exhausted-source" => BuildPlayState(luxOverride: LuxCard() with { IsExhausted = true }),
            "wrong-source-card" => BuildPlayState(luxOverride: LuxCard() with { CardNo = "OGS·015/024" }),
            "unnecessary-resource" => BuildPlayState(mana: 1),
            _ => BuildPlayState()
        };
        var command = caseName == "non-spell"
            ? new PlayCardCommand(
                SpellObjectId,
                ArenaCouncilorCardNo,
                [],
                OptionalCosts: [LuxResourceAction])
            : new PlayCardCommand(
                SpellObjectId,
                BulletTimeCardNo,
                [],
                OptionalCosts: caseName == "duplicate-resource" ? [LuxResourceAction, LuxResourceAction] : [LuxResourceAction]);
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-lux-invalid-{caseName}", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState BuildPlayState(
        string cardNo = BulletTimeCardNo,
        int mana = 0,
        CardObjectState? luxOverride = null,
        bool includeSecondLux = false,
        CardObjectState? secondLuxOverride = null)
    {
        var sourceCard = cardNo switch
        {
            ArenaCouncilorCardNo => ArenaCouncilorCard(),
            EvolutionDayCardNo => EvolutionDayCard(),
            _ => BulletTimeCard()
        };
        var lux = luxOverride ?? LuxCard();
        var p1Base = includeSecondLux
            ? new[] { LuxObjectId, SecondLuxObjectId }
            : [LuxObjectId];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [SpellObjectId] = sourceCard,
            [LuxObjectId] = lux,
            [EnemyObjectId] = EnemyUnit()
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [SpellObjectId] = new("P1", "HAND"),
            [LuxObjectId] = new("P1", "BASE"),
            [EnemyObjectId] = new("P2", "BATTLEFIELD", "P2-MAIN")
        };
        if (includeSecondLux)
        {
            cardObjects[SecondLuxObjectId] = secondLuxOverride ?? LuxCard(SecondLuxObjectId);
            objectLocations[SecondLuxObjectId] = new("P1", "BASE");
        }

        return new MatchState(
            roomId: "lux-resource-skill-test",
            tick: 40,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "Alice",
                ["P2"] = "Bob"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new RunePool(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [SpellObjectId],
                    Base = p1Base
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [EnemyObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static JsonElement PromptScopedPlayCardRawCommand(
        PlayCardCommand command,
        ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            cardObjectId = command.SourceObjectId,
            cardNo = command.CardNo,
            targetObjectIds = command.TargetObjectIds,
            optionalCosts = command.OptionalCosts ?? Array.Empty<string>(),
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
    }

    private static JsonElement PromptScopedPlayCardRawCommandWithClientNote(
        PlayCardCommand command,
        ActionPromptDto prompt,
        string clientNote)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            cardObjectId = command.SourceObjectId,
            cardNo = command.CardNo,
            targetObjectIds = command.TargetObjectIds,
            optionalCosts = command.OptionalCosts ?? Array.Empty<string>(),
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote
        });
    }

    private static void AssertPromptScopedPlayCardRawCommand(
        JsonElement rawCommand,
        ActionPromptDto prompt,
        IReadOnlyList<string> optionalCosts)
    {
        Assert.Equal(CommandTypes.PlayCard, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(SpellObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(BulletTimeCardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Equal(
            optionalCosts,
            rawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => choice.GetString()!)
                .ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertLuxSpellOnlyResourceAcceptedState(
        ResolutionResult result,
        StackItemState? expectedStackItem = null)
    {
        Assert.True(result.State.CardObjects[LuxObjectId].IsExhausted);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(SpellObjectId, stackItem.SourceObjectId);
        Assert.Equal(BulletTimeCardNo, stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        if (expectedStackItem is not null)
        {
            Assert.Equal(expectedStackItem.StackItemId, stackItem.StackItemId);
            Assert.Equal(expectedStackItem.ControllerId, stackItem.ControllerId);
            Assert.Equal(expectedStackItem.SourceObjectId, stackItem.SourceObjectId);
            Assert.Equal(expectedStackItem.EffectKind, stackItem.EffectKind);
            Assert.Equal(expectedStackItem.CardNo, stackItem.CardNo);
            Assert.Equal(expectedStackItem.TargetObjectIds, stackItem.TargetObjectIds);
            Assert.Equal(expectedStackItem.DamageAmount, stackItem.DamageAmount);
            Assert.Equal(expectedStackItem.EffectRepeatCount, stackItem.EffectRepeatCount);
            Assert.Equal(expectedStackItem.OptionalCosts, stackItem.OptionalCosts);
            Assert.Equal(expectedStackItem.PlayedAfterAnotherCardThisTurn, stackItem.PlayedAfterAnotherCardThisTurn);
            Assert.Equal(expectedStackItem.Destination, stackItem.Destination);
            Assert.Equal(expectedStackItem.TimingContext, stackItem.TimingContext);
        }

        if (result.Accepted)
        {
            Assert.Equal(
                [
                    "CARD_PLAYED",
                    "ABILITY_ACTIVATED",
                    "UNIT_EXHAUSTED",
                    "MANA_GAINED",
                    "TEMPORARY_PAYMENT_RESOURCE_SPENT",
                    "TEMPORARY_PAYMENT_RESOURCE_CLEARED",
                    "COST_PAID",
                    "STACK_ITEM_ADDED"
                ],
                result.Events.Select(gameEvent => gameEvent.Kind));
            Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
            Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
            var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
            Assert.Equal([LuxResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
            Assert.Equal([LuxResourceAction], Assert.IsType<string[]>(costEvent.Payload["luxSpellOnlyResourceActions"]));
        }

        return stackItem;
    }

    private static CardObjectState BulletTimeCard()
    {
        return new CardObjectState(
            SpellObjectId,
            cardNo: BulletTimeCardNo,
            tags: [CardObjectTags.SpellCard],
            manaCost: 1,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState ArenaCouncilorCard()
    {
        return new CardObjectState(
            SpellObjectId,
            cardNo: ArenaCouncilorCardNo,
            power: 3,
            tags: [CardObjectTags.UnitCard],
            manaCost: 5,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState EvolutionDayCard()
    {
        return new CardObjectState(
            SpellObjectId,
            cardNo: EvolutionDayCardNo,
            tags: [CardObjectTags.SpellCard],
            manaCost: 6,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState LuxCard(string objectId = LuxObjectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: P4ActivatedAbilityCatalog.LuxCardNo,
            power: 2,
            tags: [CardObjectTags.UnitCard],
            manaCost: 3,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static string LuxResourceActionFor(string sourceObjectId)
    {
        return $"{P4ActivatedAbilityCatalog.LuxSpellOnlyResourceActionPrefix}{sourceObjectId}";
    }

    private static CardObjectState EnemyUnit()
    {
        return new CardObjectState(
            EnemyObjectId,
            cardNo: "SFD·125/221",
            power: 5,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "riftbound-dotnet.sln"))
                || File.Exists(Path.Combine(current.FullName, "Riftbound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test output directory.");
    }
}
