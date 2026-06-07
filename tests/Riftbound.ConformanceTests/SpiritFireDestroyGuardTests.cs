using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SpiritFireDestroyGuardTests
{
    [Fact]
    public async Task SpiritFireDestroysBattlefieldUnitsWithTotalPowerFour()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireState();

        var played = await PlaySpiritFireAsync(
            engine,
            state,
            ["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"]);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-spirit-fire-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-spirit-fire-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-SPIRIT-FIRE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P2-SPIRIT-FIRE-UNIT-001", p2Pass.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("P2-SPIRIT-FIRE-UNIT-002", p2Pass.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("P2-SPIRIT-FIRE-KEEPER-001", p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("P2-SPIRIT-FIRE-UNIT-001", p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("P2-SPIRIT-FIRE-UNIT-002", p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-SPIRIT-FIRE-UNIT-001", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-SPIRIT-FIRE-UNIT-002", StringComparison.Ordinal));
    }

    [Fact]
    public void SpiritFireMatchSessionMainActionPromptOnlyExposesLegalTargetCandidates()
    {
        var state = BuildSpiritFireState();
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
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, "P1-SPELL-SPIRIT-FIRE", StringComparison.Ordinal));
        var targetIds = (playCandidate.Targets ?? []).Select(target => target.Id).ToArray();

        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-SPELL-SPIRIT-FIRE", StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstTargetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();
        var legalTargetSelections = Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(
                sourceRequirement["legalTargetSelections"])
            .ToArray();
        string[] legalTargets =
        [
            "P2-SPIRIT-FIRE-UNIT-001",
            "P2-SPIRIT-FIRE-UNIT-002"
        ];
        string[] filteredTargets =
        [
            "P2-SPIRIT-FIRE-KEEPER-001",
            "P2-BATTLEFIELD-EQUIPMENT",
            "P2-BATTLEFIELD-SPELL",
            "P2-BATTLEFIELD-RUNE",
            "P2-FACE-DOWN-STANDBY",
            "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
            "P2-BASE-UNIT",
            "P1-HAND-UNIT"
        ];

        foreach (var legalTarget in legalTargets)
        {
            Assert.Contains(legalTarget, targetIds);
            Assert.Contains(legalTarget, firstTargetChoiceIds);
        }

        foreach (var filteredTarget in filteredTargets)
        {
            Assert.DoesNotContain(filteredTarget, targetIds);
            Assert.DoesNotContain(filteredTarget, firstTargetChoiceIds);
            Assert.DoesNotContain(legalTargetSelections, selection =>
                selection.Contains(filteredTarget, StringComparer.Ordinal));
        }

        Assert.Contains(legalTargetSelections, selection =>
            selection.Count == legalTargets.Length
            && legalTargets.All(target => selection.Contains(target, StringComparer.Ordinal)));
    }

    [Fact]
    public async Task SpiritFirePlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildSpiritFireState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            "P1-SPELL-SPIRIT-FIRE",
            "OGN·256/298",
            ["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, command.SourceObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-spirit-fire-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-spirit-fire-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertSpiritFireStackPriorityState(accepted);
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
        AssertPromptScopedPlayCardRawCommand(acceptedJournalEntry.RawCommand.Value, command, prompt);
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
        AssertSpiritFireStackPriorityState(replay, acceptedStackItem);
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
        AssertPromptScopedPlayCardRawCommand(rejectedJournalEntry.RawCommand.Value, command, prompt);
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
        AssertSpiritFireStackPriorityState(duplicateReplay, acceptedStackItem);
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
        AssertSpiritFireStackPriorityState(conflict, acceptedStackItem);
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

    [Fact]
    public async Task SpiritFireRejectsTotalPowerAboveFourWithoutMutation()
    {
        var state = BuildSpiritFireState();

        var result = await PlaySpiritFireAsync(
            new CoreRuleEngine(),
            state,
            ["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-KEEPER-001"]);

        AssertRejectedWithoutMutation(state, result);
    }

    [Theory]
    [InlineData("P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P2-BATTLEFIELD-SPELL")]
    [InlineData("P2-BATTLEFIELD-RUNE")]
    [InlineData("P2-FACE-DOWN-STANDBY")]
    [InlineData("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT")]
    [InlineData("P2-STALE-UNIT")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P1-HAND-UNIT")]
    public async Task SpiritFireRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildSpiritFireState();

        var result = await PlaySpiritFireAsync(
            new CoreRuleEngine(),
            state,
            [targetObjectId]);

        AssertRejectedWithoutMutation(state, result);
    }

    [Fact]
    public void SpiritFirePromptChoicesAndLegalSelectionsOnlyExposePublicBattlefieldUnits()
    {
        var state = BuildSpiritFireState();

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
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-SPELL-SPIRIT-FIRE", StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Equal(4, Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        Assert.Contains("0", choicesByIndex.Keys);
        Assert.Equal(["0", "1", "2", "3"], choicesByIndex.Keys.OrderBy(index => index, StringComparer.Ordinal).ToArray());
        var legalTargetSelections = Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(
                sourceRequirement["legalTargetSelections"])
            .ToArray();
        string[] legalTargetIds =
        [
            "P2-SPIRIT-FIRE-UNIT-001",
            "P2-SPIRIT-FIRE-UNIT-002"
        ];
        string[] invalidTargetIds =
        [
            "P2-SPIRIT-FIRE-KEEPER-001",
            "P2-BATTLEFIELD-EQUIPMENT",
            "P2-BATTLEFIELD-SPELL",
            "P2-BATTLEFIELD-RUNE",
            "P2-FACE-DOWN-STANDBY",
            "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
            "P2-BASE-UNIT",
            "P1-HAND-UNIT"
        ];

        foreach (var choicesByIndexEntry in choicesByIndex)
        {
            var targetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndexEntry.Value)
                .Select(choice => choice.Id)
                .ToArray();

            Assert.Equal(legalTargetIds, targetChoiceIds);
            foreach (var invalidTargetId in invalidTargetIds)
            {
                Assert.DoesNotContain(invalidTargetId, targetChoiceIds);
            }
        }

        Assert.Contains(legalTargetSelections, selection =>
            selection.SequenceEqual(["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"]));
        Assert.DoesNotContain(legalTargetSelections, selection =>
            selection.Contains("P2-SPIRIT-FIRE-KEEPER-001", StringComparer.Ordinal));
        Assert.DoesNotContain(legalTargetSelections, selection =>
            selection.Contains("P2-BATTLEFIELD-EQUIPMENT", StringComparer.Ordinal)
            || selection.Contains("P2-BATTLEFIELD-SPELL", StringComparer.Ordinal)
            || selection.Contains("P2-BATTLEFIELD-RUNE", StringComparer.Ordinal)
            || selection.Contains("P2-FACE-DOWN-STANDBY", StringComparer.Ordinal)
            || selection.Contains("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT", StringComparer.Ordinal));
    }

    private static async Task<ResolutionResult> PlaySpiritFireAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-spirit-fire-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-SPIRIT-FIRE",
                "OGN·256/298",
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
        PlayCardCommand command,
        ActionPromptDto prompt)
    {
        Assert.Equal(CommandTypes.PlayCard, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(command.SourceObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(command.CardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Equal(
            command.TargetObjectIds,
            rawCommand.GetProperty("targetObjectIds")
                .EnumerateArray()
                .Select(target => target.GetString()!)
                .ToArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertSpiritFireStackPriorityState(
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
        Assert.Equal(RunePool.Empty, result.State.RunePools["P2"]);
        Assert.Equal(["P1-HAND-UNIT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(["P2-BASE-UNIT"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(
            [
                "P2-SPIRIT-FIRE-UNIT-001",
                "P2-SPIRIT-FIRE-UNIT-002",
                "P2-SPIRIT-FIRE-KEEPER-001",
                "P2-BATTLEFIELD-EQUIPMENT",
                "P2-BATTLEFIELD-SPELL",
                "P2-BATTLEFIELD-RUNE",
                "P2-FACE-DOWN-STANDBY",
                "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"
            ],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Null(result.State.PendingPayment);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-SPELL-SPIRIT-FIRE", stackItem.SourceObjectId);
        Assert.Equal("OGN·256/298", stackItem.CardNo);
        Assert.Equal(["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"], stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal("SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4", stackItem.EffectKind);
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
        }

        return stackItem;
    }

    private static void AssertRejectedWithoutMutation(MatchState initialState, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(initialState), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SPIRIT-FIRE", "P1-HAND-UNIT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
    }

    private static MatchState BuildSpiritFireState()
    {
        return new MatchState(
            roomId: "spirit-fire-target-guard-test",
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
                ["P1"] = new(3, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SPIRIT-FIRE", "P1-HAND-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-SPIRIT-FIRE-UNIT-001",
                        "P2-SPIRIT-FIRE-UNIT-002",
                        "P2-SPIRIT-FIRE-KEEPER-001",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SPIRIT-FIRE"] = SpiritFire(),
                ["P1-HAND-UNIT"] = Unit("P1-HAND-UNIT"),
                ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", ownerId: "P2", controllerId: "P2"),
                ["P2-SPIRIT-FIRE-UNIT-001"] = Unit("P2-SPIRIT-FIRE-UNIT-001", ownerId: "P2", controllerId: "P2"),
                ["P2-SPIRIT-FIRE-UNIT-002"] = Unit("P2-SPIRIT-FIRE-UNIT-002", ownerId: "P2", controllerId: "P2"),
                ["P2-SPIRIT-FIRE-KEEPER-001"] = Unit("P2-SPIRIT-FIRE-KEEPER-001", power: 5, ownerId: "P2", controllerId: "P2"),
                ["P2-BATTLEFIELD-EQUIPMENT"] = NonUnit("P2-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P2"),
                ["P2-BATTLEFIELD-SPELL"] = NonUnit("P2-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P2"),
                ["P2-BATTLEFIELD-RUNE"] = NonUnit("P2-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P2"),
                ["P2-FACE-DOWN-STANDBY"] = Unit(
                    "P2-FACE-DOWN-STANDBY",
                    cardNo: null,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = Unit(
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"),
                ["P2-STALE-UNIT"] = Unit("P2-STALE-UNIT", ownerId: "P2", controllerId: "P2")
            });
    }

    private static CardObjectState SpiritFire()
    {
        return new CardObjectState(
            "P1-SPELL-SPIRIT-FIRE",
            cardNo: "OGN·256/298",
            manaCost: 3,
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
        string tag,
        string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 1,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId);
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
