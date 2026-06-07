using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReflectionsSwapGuardTests
{
    private const string ReflectionsObjectId = "P1-SPELL-REFLECTIONS";
    private const string ReflectionsCardNo = "UNL-083/219";
    private const string ReflectionsFirstTargetObjectId = "P1-BASE-EPHEMERAL";
    private const string ReflectionsSecondTargetObjectId = "P1-BATTLEFIELD-UNIT";
    private const string ReflectionsEffectKind = "REFLECTIONS_SWAP_FRIENDLY_UNITS_DRAW_1";

    [Fact]
    public async Task ReflectionsSwapsFriendlyBaseAndBattlefieldUnitsAndDrawsOne()
    {
        var engine = new CoreRuleEngine();
        var state = BuildReflectionsState();

        var played = await PlayReflectionsAsync(
            engine,
            state,
            ["P1-BASE-EPHEMERAL", "P1-BATTLEFIELD-UNIT"]);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "REFLECTIONS_SWAP_FRIENDLY_UNITS_DRAW_1", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-reflections-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-reflections-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains("P1-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains("P1-BASE-EPHEMERAL", p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-REFLECTIONS-DRAW-001"], p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SPELL-REFLECTIONS"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_LOCATIONS_SWAPPED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["firstTargetObjectId"] as string, "P1-BASE-EPHEMERAL", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["secondTargetObjectId"] as string, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["count"]) == 1);
    }

    [Fact]
    public async Task ReflectionsPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildReflectionsState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            ReflectionsObjectId,
            ReflectionsCardNo,
            [ReflectionsFirstTargetObjectId, ReflectionsSecondTargetObjectId]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, ReflectionsObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var reorderedStaleRawCommand = ReorderedPromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-reflections-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-reflections-stale-prompt-replay";

        Assert.NotEqual(staleRawCommand.GetRawText(), reorderedStaleRawCommand.GetRawText());
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(reorderedStaleRawCommand));
        AssertPromptScopedPlayCardRawCommand(reorderedStaleRawCommand, prompt, assertPropertyOrder: false);
        Assert.False(reorderedStaleRawCommand.TryGetProperty("clientNote", out _));

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertReflectionsStackPriorityState(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedPlayerZonesHash = MatchStateHasher.HashValue(accepted.State.PlayerZones);
        var acceptedObjectLocationsHash = MatchStateHasher.HashValue(accepted.State.ObjectLocations);
        var acceptedStackHash = MatchStateHasher.HashValue(accepted.State.StackItems);
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
        Assert.Equal(acceptedPlayerZonesHash, MatchStateHasher.HashValue(replay.State.PlayerZones));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(replay.State.ObjectLocations));
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(replay.State.StackItems));
        AssertReflectionsStackPriorityState(replay, acceptedStackItem);
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
        Assert.Single(journal.Entries, entry => !entry.Accepted);
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
        Assert.Equal(acceptedPlayerZonesHash, MatchStateHasher.HashValue(reorderedReplay.State.PlayerZones));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(reorderedReplay.State.ObjectLocations));
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(reorderedReplay.State.StackItems));
        AssertReflectionsStackPriorityState(reorderedReplay, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Single(journal.Entries, entry => !entry.Accepted);
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
        Assert.Equal(acceptedPlayerZonesHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(duplicateReplay.State.ObjectLocations));
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(duplicateReplay.State.StackItems));
        AssertReflectionsStackPriorityState(duplicateReplay, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Single(journal.Entries, entry => !entry.Accepted);
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
        Assert.Equal(acceptedPlayerZonesHash, MatchStateHasher.HashValue(conflict.State.PlayerZones));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(conflict.State.ObjectLocations));
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(conflict.State.StackItems));
        AssertReflectionsStackPriorityState(conflict, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Single(journal.Entries, entry => !entry.Accepted);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-BASE-UNIT", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-BASE-EPHEMERAL", "P1-BASE-UNIT")]
    public async Task ReflectionsRejectsMissingEphemeralOrSamePositionPairsWithoutMutation(
        string firstTargetObjectId,
        string secondTargetObjectId)
    {
        var state = BuildReflectionsState();

        var result = await PlayReflectionsAsync(
            new CoreRuleEngine(),
            state,
            [firstTargetObjectId, secondTargetObjectId]);

        AssertRejectedWithoutMutation(result);
    }

    [Theory]
    [InlineData("P1-BASE-EQUIPMENT", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-BASE-SPELL", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-BASE-RUNE", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-FACE-DOWN-STANDBY", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-STALE-UNIT", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P2-ENEMY-UNIT", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-DIRTY-P2-CONTROLLED-BASE-UNIT", "P1-BATTLEFIELD-UNIT")]
    public async Task ReflectionsRejectsInvalidTargetsWithoutMutation(
        string firstTargetObjectId,
        string secondTargetObjectId)
    {
        var state = BuildReflectionsState();

        var result = await PlayReflectionsAsync(
            new CoreRuleEngine(),
            state,
            [firstTargetObjectId, secondTargetObjectId]);

        AssertRejectedWithoutMutation(result);
    }

    [Fact]
    public void ReflectionsPromptLegalSelectionsRequireEphemeralAndDifferentPositions()
    {
        var state = BuildReflectionsState();

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-SPELL-REFLECTIONS", StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Equal(["0", "1"], choicesByIndex.Keys.OrderBy(index => index, StringComparer.Ordinal).ToArray());
        var firstTargetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();
        var secondTargetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["1"])
            .Select(choice => choice.Id)
            .ToArray();
        var legalTargetSelections = Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(
                sourceRequirement["legalTargetSelections"])
            .ToArray();

        if (sourceRequirement.TryGetValue("allowsRepeatedTargets", out var allowsRepeatedTargets))
        {
            Assert.False(Assert.IsType<bool>(allowsRepeatedTargets));
        }

        Assert.Equal(
            firstTargetChoiceIds.OrderBy(choiceId => choiceId, StringComparer.Ordinal).ToArray(),
            secondTargetChoiceIds.OrderBy(choiceId => choiceId, StringComparer.Ordinal).ToArray());
        Assert.Contains("P1-BASE-EPHEMERAL", firstTargetChoiceIds);
        Assert.Contains("P1-BATTLEFIELD-UNIT", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-BASE-EQUIPMENT", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-BASE-SPELL", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-BASE-RUNE", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-FACE-DOWN-STANDBY", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-DIRTY-P2-CONTROLLED-BASE-UNIT", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-ENEMY-UNIT", firstTargetChoiceIds);
        Assert.Contains("P1-BASE-EPHEMERAL", secondTargetChoiceIds);
        Assert.Contains("P1-BATTLEFIELD-UNIT", secondTargetChoiceIds);
        Assert.DoesNotContain("P1-BASE-EQUIPMENT", secondTargetChoiceIds);
        Assert.DoesNotContain("P1-BASE-SPELL", secondTargetChoiceIds);
        Assert.DoesNotContain("P1-BASE-RUNE", secondTargetChoiceIds);
        Assert.DoesNotContain("P1-FACE-DOWN-STANDBY", secondTargetChoiceIds);
        Assert.DoesNotContain("P1-DIRTY-P2-CONTROLLED-BASE-UNIT", secondTargetChoiceIds);
        Assert.DoesNotContain("P2-ENEMY-UNIT", secondTargetChoiceIds);

        Assert.Contains(legalTargetSelections, selection =>
            selection.SequenceEqual(["P1-BASE-EPHEMERAL", "P1-BATTLEFIELD-UNIT"]));
        Assert.Contains(legalTargetSelections, selection =>
            selection.SequenceEqual(["P1-BASE-UNIT", "P1-BATTLEFIELD-EPHEMERAL"]));
        Assert.DoesNotContain(legalTargetSelections, selection =>
            selection.SequenceEqual(["P1-BASE-UNIT", "P1-BATTLEFIELD-UNIT"]));
        Assert.DoesNotContain(legalTargetSelections, selection =>
            selection.SequenceEqual(["P1-BASE-EPHEMERAL", "P1-BASE-UNIT"]));
        Assert.DoesNotContain(legalTargetSelections, selection =>
            selection.Contains("P1-BASE-EQUIPMENT", StringComparer.Ordinal)
            || selection.Contains("P1-BASE-SPELL", StringComparer.Ordinal)
            || selection.Contains("P1-BASE-RUNE", StringComparer.Ordinal)
            || selection.Contains("P1-FACE-DOWN-STANDBY", StringComparer.Ordinal));
    }

    private static async Task<ResolutionResult> PlayReflectionsAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-reflections-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-REFLECTIONS",
                "UNL-083/219",
                targetObjectIds),
            CancellationToken.None);
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
        bool assertPropertyOrder = true)
    {
        if (assertPropertyOrder)
        {
            Assert.Equal(
                ["cmdType", "cardObjectId", "cardNo", "targetObjectIds", "optionalCosts", "promptId", "snapshotTick"],
                rawCommand.EnumerateObject().Select(property => property.Name).ToArray());
        }

        Assert.Equal(CommandTypes.PlayCard, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(ReflectionsObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(ReflectionsCardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Equal(
            [ReflectionsFirstTargetObjectId, ReflectionsSecondTargetObjectId],
            rawCommand.GetProperty("targetObjectIds")
                .EnumerateArray()
                .Select(target => target.GetString()!)
                .ToArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertReflectionsStackPriorityState(
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
        Assert.Equal(["P1-REFLECTIONS-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(
            [
                "P1-BASE-EPHEMERAL",
                "P1-BASE-UNIT",
                "P1-BASE-EQUIPMENT",
                "P1-BASE-SPELL",
                "P1-BASE-RUNE",
                "P1-FACE-DOWN-STANDBY",
                "P1-DIRTY-P2-CONTROLLED-BASE-UNIT"
            ],
            result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P1-BATTLEFIELD-UNIT",
                "P1-BATTLEFIELD-EPHEMERAL"
            ],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-ENEMY-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal("P1", result.State.ObjectLocations[ReflectionsObjectId].PlayerId);
        Assert.Equal("STACK", result.State.ObjectLocations[ReflectionsObjectId].Zone);
        Assert.Null(result.State.ObjectLocations[ReflectionsObjectId].BattlefieldObjectId);
        Assert.Equal(PromptTypes.StackPriority, result.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PlayCard, result.Prompts["P1"].Actions);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(ReflectionsObjectId, stackItem.SourceObjectId);
        Assert.Equal(ReflectionsCardNo, stackItem.CardNo);
        Assert.Equal([ReflectionsFirstTargetObjectId, ReflectionsSecondTargetObjectId], stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal(ReflectionsEffectKind, stackItem.EffectKind);
        Assert.Equal(0, stackItem.DamageAmount);
        Assert.Equal(1, stackItem.EffectRepeatCount);
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

    private static void AssertRejectedWithoutMutation(ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-REFLECTIONS"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-REFLECTIONS-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(
            [
                "P1-BASE-EPHEMERAL",
                "P1-BASE-UNIT",
                "P1-BASE-EQUIPMENT",
                "P1-BASE-SPELL",
                "P1-BASE-RUNE",
                "P1-FACE-DOWN-STANDBY",
                "P1-DIRTY-P2-CONTROLLED-BASE-UNIT"
            ],
            result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P1-BATTLEFIELD-UNIT",
                "P1-BATTLEFIELD-EPHEMERAL"
            ],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-ENEMY-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].IsFaceDown);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_LOCATIONS_SWAPPED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    private static MatchState BuildReflectionsState()
    {
        return new MatchState(
            roomId: "reflections-swap-guard-test",
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
                ["P1"] = new(2, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-REFLECTIONS-DRAW-001"],
                    Hand = ["P1-SPELL-REFLECTIONS"],
                    Base =
                    [
                        "P1-BASE-EPHEMERAL",
                        "P1-BASE-UNIT",
                        "P1-BASE-EQUIPMENT",
                        "P1-BASE-SPELL",
                        "P1-BASE-RUNE",
                        "P1-FACE-DOWN-STANDBY",
                        "P1-DIRTY-P2-CONTROLLED-BASE-UNIT"
                    ],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-UNIT",
                        "P1-BATTLEFIELD-EPHEMERAL"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ENEMY-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-REFLECTIONS"] = Reflections(),
                ["P1-REFLECTIONS-DRAW-001"] = Unit("P1-REFLECTIONS-DRAW-001"),
                ["P1-BASE-EPHEMERAL"] = Unit(
                    "P1-BASE-EPHEMERAL",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral]),
                ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT"),
                ["P1-BATTLEFIELD-UNIT"] = Unit("P1-BATTLEFIELD-UNIT"),
                ["P1-BATTLEFIELD-EPHEMERAL"] = Unit(
                    "P1-BATTLEFIELD-EPHEMERAL",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral]),
                ["P1-BASE-EQUIPMENT"] = NonUnit("P1-BASE-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard),
                ["P1-BASE-SPELL"] = NonUnit("P1-BASE-SPELL", "OGN·169/298", CardObjectTags.SpellCard),
                ["P1-BASE-RUNE"] = NonUnit("P1-BASE-RUNE", "RUNES·001", CardObjectTags.RuneCard),
                ["P1-FACE-DOWN-STANDBY"] = Unit(
                    "P1-FACE-DOWN-STANDBY",
                    cardNo: null,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P1-STALE-UNIT"] = Unit("P1-STALE-UNIT"),
                ["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"] = Unit(
                    "P1-DIRTY-P2-CONTROLLED-BASE-UNIT",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-ENEMY-UNIT"] = Unit(
                    "P2-ENEMY-UNIT",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }

    private static CardObjectState Reflections()
    {
        return new CardObjectState(
            "P1-SPELL-REFLECTIONS",
            cardNo: "UNL-083/219",
            manaCost: 2,
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Unit(
        string objectId,
        string? cardNo = "SFD·125/221",
        int power = 2,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            isFaceDown: isFaceDown,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState NonUnit(
        string objectId,
        string cardNo,
        string tag)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 1,
            tags: [tag],
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
