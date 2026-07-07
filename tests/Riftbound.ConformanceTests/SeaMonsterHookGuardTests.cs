using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SeaMonsterHookGuardTests
{
    private const string SeaMonsterHookObjectId = "P1-EQUIPMENT-SEA-MONSTER-HOOK";
    private const string SeaMonsterHookBaseObjectId = "P1-BASE-SEA-MONSTER-HOOK";
    private const string SeaMonsterHookCardNo = "OGN·242/298";
    private const string SeaMonsterHookEffectKind = "SEA_MONSTER_HOOK_PLAY_EQUIPMENT";

    [Fact]
    public async Task SeaMonsterHookPlayCardWithNoTargetsUsesStackAndResolvesToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSeaMonsterHookState();

        var played = await PlaySeaMonsterHookAsync(engine, state, "P1-EQUIPMENT-SEA-MONSTER-HOOK", []);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-EQUIPMENT-SEA-MONSTER-HOOK", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SEA_MONSTER_HOOK_PLAY_EQUIPMENT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-sea-monster-hook-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-sea-monster-hook-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-SEA-MONSTER-HOOK", "P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK", "P1-EQUIPMENT-SEA-MONSTER-HOOK"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);

        var equipment = p2Pass.State.CardObjects["P1-EQUIPMENT-SEA-MONSTER-HOOK"];
        Assert.Equal("OGN·242/298", equipment.CardNo);
        Assert.Equal("P1", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Equal([CardObjectTags.EquipmentCard], equipment.Tags);
        Assert.False(equipment.IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-EQUIPMENT-SEA-MONSTER-HOOK", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, "P1-EQUIPMENT-SEA-MONSTER-HOOK", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentName"] as string, "海兽钓钩", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal));
    }

    [Fact]
    public void SeaMonsterHookMainActionPlayCardPromptExposesNoTargetChoices()
    {
        var state = BuildSeaMonsterHookState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, SeaMonsterHookObjectId, StringComparison.Ordinal));
        var targetIds = (playCandidate.Targets ?? []).Select(target => target.Id).ToArray();
        var invalidTargetIds = new[]
        {
            "P1-TARGET-UNIT",
            "P1-BASE-SEA-MONSTER-HOOK",
            "P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK",
            "P2-EQUIPMENT-SEA-MONSTER-HOOK"
        };
        Assert.Empty(targetIds);
        foreach (var invalidTargetId in invalidTargetIds)
        {
            Assert.DoesNotContain(invalidTargetId, targetIds);
        }

        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        Assert.True(metadata.TryGetValue("sourceRequirements", out var rawSourceRequirements));
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            rawSourceRequirements);
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, SeaMonsterHookObjectId, StringComparison.Ordinal));
        Assert.True(sourceRequirement.TryGetValue("targetChoicesByIndex", out var rawTargetChoicesByIndex));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            rawTargetChoicesByIndex);
        var metadataTargetIds = targetChoicesByIndex.Values
            .SelectMany(rawTargetChoices => Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(rawTargetChoices)
                .Select(choice => choice.Id))
            .ToArray();

        Assert.Empty(metadataTargetIds);
        foreach (var invalidTargetId in invalidTargetIds)
        {
            Assert.DoesNotContain(invalidTargetId, metadataTargetIds);
        }

        Assert.Empty(targetChoicesByIndex);
    }

    [Fact]
    public async Task SeaMonsterHookPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildSeaMonsterHookState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            SeaMonsterHookObjectId,
            SeaMonsterHookCardNo,
            []);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, SeaMonsterHookObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var reorderedStaleRawCommand = ReorderedPromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        Assert.NotEqual(staleRawCommand.GetRawText(), reorderedStaleRawCommand.GetRawText());
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(reorderedStaleRawCommand));
        AssertPromptScopedPlayCardRawCommand(reorderedStaleRawCommand, prompt);
        Assert.False(reorderedStaleRawCommand.TryGetProperty("clientNote", out _));
        const string acceptedClientIntentId = "intent-sea-monster-hook-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-sea-monster-hook-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertSeaMonsterHookStackPriorityState(accepted);
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
        AssertPromptScopedPlayCardRawCommand(acceptedJournalEntry.RawCommand.Value, prompt);
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
        AssertSeaMonsterHookStackPriorityState(replay, acceptedStackItem);
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
        AssertPromptScopedPlayCardRawCommand(rejectedJournalEntry.RawCommand.Value, prompt);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));
        var journalHashAfterReplay = MatchStateHasher.HashValue(journal.Entries);

        var reorderedReplay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            reorderedStaleRawCommand,
            CancellationToken.None);

        Assert.False(reorderedReplay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, reorderedReplay.ErrorCode);
        Assert.Equal(replay.ErrorMessage, reorderedReplay.ErrorMessage);
        Assert.Empty(reorderedReplay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(reorderedReplay.State));
        Assert.Equal(replay.State.Tick, reorderedReplay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(reorderedReplay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(reorderedReplay.Snapshots));
        AssertSeaMonsterHookStackPriorityState(reorderedReplay, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));

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
        AssertSeaMonsterHookStackPriorityState(duplicateReplay, acceptedStackItem);
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
        AssertSeaMonsterHookStackPriorityState(conflict, acceptedStackItem);
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
    [InlineData("P1-EQUIPMENT-SEA-MONSTER-HOOK", "P1-TARGET-UNIT", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-BASE-SEA-MONSTER-HOOK", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P2-EQUIPMENT-SEA-MONSTER-HOOK", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P1-EQUIPMENT-SEA-MONSTER-HOOK", "", 2, ErrorCodes.InsufficientCost)]
    public async Task SeaMonsterHookPlayCardRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string targetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildSeaMonsterHookState(mana);
        var targetObjectIds = string.IsNullOrWhiteSpace(targetObjectId) ? Array.Empty<string>() : [targetObjectId];

        var result = await PlaySeaMonsterHookAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            targetObjectIds);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SEA-MONSTER-HOOK"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-SEA-MONSTER-HOOK", "P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-EQUIPMENT-SEA-MONSTER-HOOK"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.False(result.State.CardObjects["P1-EQUIPMENT-SEA-MONSTER-HOOK"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public void SeaMonsterHookActivatedAbilityPromptIsBehaviorSpecDriven()
    {
        var ability = SeaMonsterHookActivatedAbility();
        var state = BuildSeaMonsterHookActivatedAbilityState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.ActivateAbility, prompt.Actions);
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Contains(activateCandidate.Sources ?? [], source => string.Equals(source.Id, SeaMonsterHookBaseObjectId, StringComparison.Ordinal));
        var targetIds = (activateCandidate.Targets ?? []).Select(target => target.Id).ToArray();
        Assert.Contains("P1-TARGET-UNIT", targetIds);
        Assert.DoesNotContain(SeaMonsterHookBaseObjectId, targetIds);
        Assert.DoesNotContain("P1-OTHER-EQUIPMENT", targetIds);

        var metadata = Assert.IsType<Dictionary<string, object?>>(activateCandidate.Metadata);
        Assert.True(metadata.TryGetValue("sourceRequirements", out var rawSourceRequirements));
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            rawSourceRequirements);
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, SeaMonsterHookBaseObjectId, StringComparison.Ordinal)
                && string.Equals(requirement["abilityId"] as string, ability.AbilityId, StringComparison.Ordinal));
        Assert.Equal(SeaMonsterHookCardNo, sourceRequirement["cardNo"]);
        Assert.Equal(1, sourceRequirement["manaCost"]);
        Assert.Equal(1, sourceRequirement["minTargetCount"]);
        Assert.Equal(1, sourceRequirement["maxTargetCount"]);
        Assert.True((bool)sourceRequirement["exhaustsSource"]!);
        var powerCostByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(sourceRequirement["powerCostByTrait"]);
        Assert.Equal(1, powerCostByTrait[RuneTrait.Yellow]);
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["targetChoicesByIndex"]);
        var targetChoices = Assert.Single(targetChoicesByIndex);
        Assert.Equal("0", targetChoices.Key);
        Assert.Contains(targetChoices.Value, choice => string.Equals(choice.Id, "P1-TARGET-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(targetChoices.Value, choice => string.Equals(choice.Id, SeaMonsterHookBaseObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(targetChoices.Value, choice => string.Equals(choice.Id, "P1-OTHER-EQUIPMENT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SeaMonsterHookActivatedAbilityDestroysFriendlyUnitPlaysUniqueEligibleTopFiveUnitAndRecyclesRest()
    {
        var engine = new CoreRuleEngine();
        var ability = SeaMonsterHookActivatedAbility();
        var state = BuildSeaMonsterHookActivatedAbilityState();

        var activated = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-sea-monster-hook-activate", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(
                SeaMonsterHookBaseObjectId,
                ability.AbilityId,
                ["P1-TARGET-UNIT"]),
            CancellationToken.None);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal(1, activated.State.Tick);
        Assert.Equal(RunePool.Empty, activated.State.RunePools["P1"]);
        Assert.True(activated.State.CardObjects[SeaMonsterHookBaseObjectId].IsExhausted);
        Assert.Contains("P1-TARGET-UNIT", activated.State.PlayerZones["P1"].Base);
        var stackItem = Assert.Single(activated.State.StackItems);
        Assert.Equal(SeaMonsterHookBaseObjectId, stackItem.SourceObjectId);
        Assert.Equal(ability.EffectKind, stackItem.EffectKind);
        Assert.Equal(["P1-TARGET-UNIT"], stackItem.TargetObjectIds);
        Assert.Contains(activated.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, SeaMonsterHookBaseObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, ability.AbilityId, StringComparison.Ordinal));
        Assert.Contains(activated.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, ability.AbilityId, StringComparison.Ordinal));
        Assert.Contains(activated.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_EXHAUSTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, SeaMonsterHookBaseObjectId, StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-sea-monster-hook-activate-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-sea-monster-hook-activate-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(
            [SeaMonsterHookBaseObjectId, "P1-OTHER-EQUIPMENT", "P1-ELIGIBLE-UNIT"],
            p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-TARGET-UNIT"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.False(p2Pass.State.CardObjects.ContainsKey("P1-TARGET-UNIT"));
        Assert.True(p2Pass.State.CardObjects[SeaMonsterHookBaseObjectId].IsExhausted);
        Assert.False(p2Pass.State.CardObjects["P1-ELIGIBLE-UNIT"].IsExhausted);
        Assert.Equal("P1", p2Pass.State.CardObjects["P1-ELIGIBLE-UNIT"].ControllerId);
        Assert.Equal("P1-DECK-KEEP", p2Pass.State.PlayerZones["P1"].MainDeck[0]);
        Assert.DoesNotContain("P1-ELIGIBLE-UNIT", p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-INELIGIBLE-UNIT", p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-SPELL", p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-EQUIPMENT", p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-RUNE", p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1"], p2Pass.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-TARGET-UNIT", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-ELIGIBLE-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceZone"] as string, "MAIN_DECK", StringComparison.Ordinal));
        var recycleEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
        Assert.Equal(4, recycleEvent.Payload["count"]);
        Assert.False(recycleEvent.Payload.ContainsKey("cardIds"));
    }

    [Fact]
    public async Task SeaMonsterHookActivatedAbilityWithNoEligibleTopFiveUnitsRecyclesAllLookedCardsPrivatelyWithoutChoice()
    {
        var engine = new CoreRuleEngine();
        var ability = SeaMonsterHookActivatedAbility();
        var state = BuildSeaMonsterHookActivatedAbilityNoEligibleState();

        var activated = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-sea-monster-hook-no-eligible-activate", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(
                SeaMonsterHookBaseObjectId,
                ability.AbilityId,
                ["P1-TARGET-UNIT"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-sea-monster-hook-no-eligible-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-sea-monster-hook-no-eligible-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Null(p2Pass.State.PendingCardChoice);
        Assert.Equal(
            [SeaMonsterHookBaseObjectId, "P1-OTHER-EQUIPMENT"],
            p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-TARGET-UNIT"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.False(p2Pass.State.CardObjects.ContainsKey("P1-TARGET-UNIT"));
        Assert.Equal("P1-DECK-KEEP", p2Pass.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Contains("P1-INELIGIBLE-UNIT", p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-SPELL", p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-EQUIPMENT", p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-RUNE", p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-SECOND-TOP-SPELL", p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(p2Pass.Prompts.Values, prompt => prompt.Actions.Contains(CommandTypes.ChooseCards));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_CHOICE_REQUESTED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-TARGET-UNIT", StringComparison.Ordinal));
        var recycleEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
        Assert.Equal(5, recycleEvent.Payload["count"]);
        Assert.Equal("LOOKED_NOT_REVEALED", recycleEvent.Payload["visibility"]);
        Assert.False(recycleEvent.Payload.ContainsKey("cardIds"));
    }

    [Fact]
    public async Task SeaMonsterHookActivatedAbilityWithMultipleEligibleTopFiveUnitsPromptsControllerToChoosePrivately()
    {
        var engine = new CoreRuleEngine();
        var ability = SeaMonsterHookActivatedAbility();
        var state = BuildSeaMonsterHookActivatedAbilityMultiEligibleState();

        var activated = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-sea-monster-hook-multi-activate", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(
                SeaMonsterHookBaseObjectId,
                ability.AbilityId,
                ["P1-TARGET-UNIT"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-sea-monster-hook-multi-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-sea-monster-hook-multi-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-TARGET-UNIT"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.False(p2Pass.State.CardObjects.ContainsKey("P1-TARGET-UNIT"));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-TARGET-UNIT", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_CHOICE_REQUESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, SeaMonsterHookBaseObjectId, StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["requiredCount"]) == 0
            && Assert.IsType<int>(gameEvent.Payload["maxCount"]) == 1
            && Assert.IsType<int>(gameEvent.Payload["legalCount"]) == 2);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal));

        var p1Prompt = p2Pass.Prompts["P1"];
        Assert.True(p1Prompt.Actionable);
        Assert.Equal("CARD_CHOICE", p1Prompt.View?.Type);
        Assert.Contains("CHOOSE_CARDS", p1Prompt.Actions);
        var chooseCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "CHOOSE_CARDS", StringComparison.Ordinal));
        Assert.True(chooseCandidate.Enabled);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(chooseCandidate.Metadata);
        Assert.Equal("SEA_MONSTER_HOOK_TOP_FIVE_PLAY", metadata["choiceWindow"]);
        Assert.Equal("P1", metadata["choosingPlayerId"]);
        Assert.Equal(0, Assert.IsType<int>(metadata["requiredCount"]));
        Assert.Equal(1, Assert.IsType<int>(metadata["maxCount"]));
        var cardChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["cardChoices"]).ToArray();
        Assert.Equal(["P1-ELIGIBLE-UNIT", "P1-SECOND-ELIGIBLE-UNIT"], cardChoices.Select(choice => choice.Id).ToArray());
        Assert.DoesNotContain(cardChoices, choice => string.Equals(choice.Id, "P1-INELIGIBLE-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(cardChoices, choice => string.Equals(choice.Id, "P1-TOP-SPELL", StringComparison.Ordinal));
        Assert.DoesNotContain(cardChoices, choice => string.Equals(choice.Id, "P1-TOP-EQUIPMENT", StringComparison.Ordinal));
        Assert.Equal(
            ["P1-ELIGIBLE-UNIT", "P1-SECOND-ELIGIBLE-UNIT"],
            Assert.IsAssignableFrom<IEnumerable<string>>(metadata["legalObjectIds"]).ToArray());

        var p2Prompt = p2Pass.Prompts["P2"];
        Assert.False(p2Prompt.Actionable);
        Assert.Equal("CARD_CHOICE", p2Prompt.View?.Type);
        Assert.DoesNotContain("CHOOSE_CARDS", p2Prompt.Actions);
        Assert.DoesNotContain(
            p2Prompt.Candidates ?? [],
            candidate => candidate.Metadata is not null
                && (candidate.Metadata.ContainsKey("cardChoices") || candidate.Metadata.ContainsKey("legalObjectIds")));

        var choiceId = Assert.IsType<string>(metadata["choiceId"]);
        var choosePayload = JsonSerializer.SerializeToElement(new
        {
            choiceId,
            choiceWindow = "SEA_MONSTER_HOOK_TOP_FIVE_PLAY",
            chosenObjectIds = new[] { "P1-SECOND-ELIGIBLE-UNIT" }
        });
        var chosen = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-sea-monster-hook-multi-choose", "P1", "CHOOSE_CARDS"),
            new UnsupportedCommand("CHOOSE_CARDS", choosePayload),
            CancellationToken.None);

        Assert.True(chosen.Accepted, chosen.ErrorMessage);
        Assert.Empty(chosen.State.StackItems);
        Assert.Equal(
            [SeaMonsterHookBaseObjectId, "P1-OTHER-EQUIPMENT", "P1-SECOND-ELIGIBLE-UNIT"],
            chosen.State.PlayerZones["P1"].Base);
        Assert.Equal("P1-DECK-KEEP", chosen.State.PlayerZones["P1"].MainDeck[0]);
        Assert.DoesNotContain("P1-SECOND-ELIGIBLE-UNIT", chosen.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-ELIGIBLE-UNIT", chosen.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-SPELL", chosen.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-EQUIPMENT", chosen.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-RUNE", chosen.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(chosen.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_CHOICE_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["choiceId"] as string, choiceId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, SeaMonsterHookBaseObjectId, StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["chosenCount"]) == 1);
        Assert.Contains(chosen.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-SECOND-ELIGIBLE-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceZone"] as string, "MAIN_DECK", StringComparison.Ordinal));
        var recycleEvent = Assert.Single(chosen.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
        Assert.Equal(4, recycleEvent.Payload["count"]);
        Assert.Equal("LOOKED_NOT_REVEALED", recycleEvent.Payload["visibility"]);
        Assert.False(recycleEvent.Payload.ContainsKey("cardIds"));
        Assert.DoesNotContain(chosen.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SeaMonsterHookActivatedAbilityTopFiveChoiceCanDeclineAndRecycleAllLookedCardsPrivately()
    {
        var engine = new CoreRuleEngine();
        var pending = await OpenSeaMonsterHookMultiEligibleChoiceAsync(engine);
        var chooseCandidate = Assert.Single(
            pending.Prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, "CHOOSE_CARDS", StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(chooseCandidate.Metadata);
        var choiceId = Assert.IsType<string>(metadata["choiceId"]);

        var declined = await engine.ResolveAsync(
            pending.State,
            new PlayerIntent("intent-sea-monster-hook-multi-decline", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(
                choiceId,
                "SEA_MONSTER_HOOK_TOP_FIVE_PLAY",
                []),
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Empty(declined.State.StackItems);
        Assert.Equal(
            [SeaMonsterHookBaseObjectId, "P1-OTHER-EQUIPMENT"],
            declined.State.PlayerZones["P1"].Base);
        Assert.Equal("P1-DECK-KEEP", declined.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Contains("P1-ELIGIBLE-UNIT", declined.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-SECOND-ELIGIBLE-UNIT", declined.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-SPELL", declined.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-EQUIPMENT", declined.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-TOP-RUNE", declined.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(declined.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal));
        Assert.Contains(declined.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_CHOICE_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["choiceId"] as string, choiceId, StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["chosenCount"]) == 0);
        var recycleEvent = Assert.Single(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
        Assert.Equal(5, recycleEvent.Payload["count"]);
        Assert.Equal("LOOKED_NOT_REVEALED", recycleEvent.Payload["visibility"]);
        Assert.False(recycleEvent.Payload.ContainsKey("cardIds"));
    }

    private static async Task<ResolutionResult> PlaySeaMonsterHookAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-sea-monster-hook-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "OGN·242/298",
                targetObjectIds),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> OpenSeaMonsterHookMultiEligibleChoiceAsync(CoreRuleEngine engine)
    {
        var ability = SeaMonsterHookActivatedAbility();
        var state = BuildSeaMonsterHookActivatedAbilityMultiEligibleState();
        var activated = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-sea-monster-hook-open-choice-activate", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(
                SeaMonsterHookBaseObjectId,
                ability.AbilityId,
                ["P1-TARGET-UNIT"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-sea-monster-hook-open-choice-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-sea-monster-hook-open-choice-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains("CHOOSE_CARDS", p2Pass.Prompts["P1"].Actions);
        return p2Pass;
    }

    private static JsonElement ReorderedPromptScopedPlayCardRawCommand(
        PlayCardCommand command,
        ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            snapshotTick = prompt.SnapshotTick,
            promptId = prompt.PromptId,
            optionalCosts = command.OptionalCosts ?? Array.Empty<string>(),
            targetObjectIds = command.TargetObjectIds,
            cardNo = command.CardNo,
            cardObjectId = command.SourceObjectId,
            cmdType = CommandTypes.PlayCard
        });
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
        ActionPromptDto prompt)
    {
        Assert.Equal(CommandTypes.PlayCard, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(SeaMonsterHookObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(SeaMonsterHookCardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertSeaMonsterHookStackPriorityState(
        ResolutionResult result,
        StackItemState? expectedStackItem = null)
    {
        Assert.Equal(1, result.State.Tick);
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-TARGET-UNIT", "P1-BASE-SEA-MONSTER-HOOK", "P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK"],
            result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal("STACK", result.State.ObjectLocations[SeaMonsterHookObjectId].Zone);

        var equipment = result.State.CardObjects[SeaMonsterHookObjectId];
        Assert.Equal(SeaMonsterHookCardNo, equipment.CardNo);
        Assert.Equal("P1", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Equal([CardObjectTags.EquipmentCard], equipment.Tags);
        Assert.False(equipment.IsExhausted);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(SeaMonsterHookObjectId, stackItem.SourceObjectId);
        Assert.Equal(SeaMonsterHookCardNo, stackItem.CardNo);
        Assert.Equal(SeaMonsterHookEffectKind, stackItem.EffectKind);
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
            Assert.Equal(expectedStackItem.OptionalCosts, stackItem.OptionalCosts);
            Assert.Equal(expectedStackItem.DamageAmount, stackItem.DamageAmount);
            Assert.Equal(expectedStackItem.EffectRepeatCount, stackItem.EffectRepeatCount);
            Assert.Equal(expectedStackItem.PlayedAfterAnotherCardThisTurn, stackItem.PlayedAfterAnotherCardThisTurn);
            Assert.Equal(expectedStackItem.Destination, stackItem.Destination);
            Assert.Equal(expectedStackItem.TimingContext, stackItem.TimingContext);
        }

        return stackItem;
    }

    private static MatchState BuildSeaMonsterHookState(int mana = 3)
    {
        return new MatchState(
            roomId: "sea-monster-hook-guard-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SEA-MONSTER-HOOK"],
                    Base =
                    [
                        "P1-TARGET-UNIT",
                        "P1-BASE-SEA-MONSTER-HOOK",
                        "P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-EQUIPMENT-SEA-MONSTER-HOOK"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SEA-MONSTER-HOOK"] = SeaMonsterHook("P1-EQUIPMENT-SEA-MONSTER-HOOK"),
                ["P1-BASE-SEA-MONSTER-HOOK"] = SeaMonsterHook("P1-BASE-SEA-MONSTER-HOOK"),
                ["P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK"] = SeaMonsterHook(
                    "P1-FACE-DOWN-STANDBY-SEA-MONSTER-HOOK",
                    isFaceDown: true,
                    tags: [CardObjectTags.EquipmentCard, CardObjectTags.Standby]),
                ["P2-EQUIPMENT-SEA-MONSTER-HOOK"] = SeaMonsterHook(
                    "P2-EQUIPMENT-SEA-MONSTER-HOOK",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-TARGET-UNIT"] = new(
                    "P1-TARGET-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });
    }

    private static CardObjectState SeaMonsterHook(
        string objectId,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "OGN·242/298",
            isFaceDown: isFaceDown,
            manaCost: 3,
            tags: tags ?? [CardObjectTags.EquipmentCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static P4ActivatedAbilityDefinition SeaMonsterHookActivatedAbility()
    {
        return Assert.Single(
            P4ActivatedAbilityCatalog.GetAll(),
            ability => string.Equals(ability.SourceCardNo, SeaMonsterHookCardNo, StringComparison.Ordinal)
                && string.Equals(
                    ability.Kind,
                    ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest,
                    StringComparison.Ordinal));
    }

    private static MatchState BuildSeaMonsterHookActivatedAbilityState()
    {
        return new MatchState(
            roomId: "sea-monster-hook-activated-ability-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Yellow] = 1
                }),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck =
                    [
                        "P1-ELIGIBLE-UNIT",
                        "P1-INELIGIBLE-UNIT",
                        "P1-TOP-SPELL",
                        "P1-TOP-EQUIPMENT",
                        "P1-TOP-RUNE",
                        "P1-DECK-KEEP"
                    ],
                    Base =
                    [
                        SeaMonsterHookBaseObjectId,
                        "P1-TARGET-UNIT",
                        "P1-OTHER-EQUIPMENT"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [SeaMonsterHookBaseObjectId] = SeaMonsterHook(SeaMonsterHookBaseObjectId),
                ["P1-TARGET-UNIT"] = Unit("P1-TARGET-UNIT", "SFD·125/221", power: 3),
                ["P1-OTHER-EQUIPMENT"] = Equipment("P1-OTHER-EQUIPMENT", "OGN·077/298"),
                ["P1-ELIGIBLE-UNIT"] = Unit("P1-ELIGIBLE-UNIT", "SFD·020/221", power: 4),
                ["P1-INELIGIBLE-UNIT"] = Unit("P1-INELIGIBLE-UNIT", "SFD·148/221", power: 5),
                ["P1-TOP-SPELL"] = Spell("P1-TOP-SPELL", "SFD·087/221"),
                ["P1-TOP-EQUIPMENT"] = Equipment("P1-TOP-EQUIPMENT", "OGN·077/298"),
                ["P1-TOP-RUNE"] = Rune("P1-TOP-RUNE", "SFD·238/221"),
                ["P1-DECK-KEEP"] = Unit("P1-DECK-KEEP", "SFD·125/221", power: 2)
            });
    }

    private static MatchState BuildSeaMonsterHookActivatedAbilityMultiEligibleState()
    {
        var state = BuildSeaMonsterHookActivatedAbilityState();
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            MainDeck =
            [
                "P1-ELIGIBLE-UNIT",
                "P1-SECOND-ELIGIBLE-UNIT",
                "P1-TOP-SPELL",
                "P1-TOP-EQUIPMENT",
                "P1-TOP-RUNE",
                "P1-DECK-KEEP"
            ]
        };

        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-SECOND-ELIGIBLE-UNIT"] = Unit("P1-SECOND-ELIGIBLE-UNIT", "SFD·020/221", power: 4);
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static MatchState BuildSeaMonsterHookActivatedAbilityNoEligibleState()
    {
        var state = BuildSeaMonsterHookActivatedAbilityState();
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            MainDeck =
            [
                "P1-INELIGIBLE-UNIT",
                "P1-TOP-SPELL",
                "P1-TOP-EQUIPMENT",
                "P1-TOP-RUNE",
                "P1-SECOND-TOP-SPELL",
                "P1-DECK-KEEP"
            ]
        };

        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-SECOND-TOP-SPELL"] = Spell("P1-SECOND-TOP-SPELL", "SFD·151/221");
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static CardObjectState Unit(string objectId, string cardNo, int power)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Equipment(string objectId, string cardNo)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            tags: [CardObjectTags.EquipmentCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Spell(string objectId, string cardNo)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Rune(string objectId, string cardNo)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            tags: [CardObjectTags.RuneCard],
            ownerId: "P1",
            controllerId: "P1");
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
}
