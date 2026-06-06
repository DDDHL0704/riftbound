using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SwitcherooGuardTests
{
    [Fact]
    public async Task SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSwitcherooState();

        var played = await PlaySwitcherooAsync(
            engine,
            state,
            "P1-SPELL-SWITCHEROO",
            ["P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT"]);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Equal([], played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["mana"]) == 2);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SWITCHEROO", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-switcheroo-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-switcheroo-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-SWITCHEROO"], p2Pass.State.PlayerZones["P1"].Graveyard);

        var firstTarget = p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"];
        var secondTarget = p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"];
        Assert.Equal(5, firstTarget.Power);
        Assert.Equal(3, firstTarget.UntilEndOfTurnPowerModifier);
        Assert.Equal(2, firstTarget.Power - firstTarget.UntilEndOfTurnPowerModifier);
        var firstModifier = Assert.Single(firstTarget.UntilEndOfTurnPowerModifiers);
        Assert.Equal(1, firstModifier.AppliedOrder);
        Assert.Equal(2, secondTarget.Power);
        Assert.Equal(-3, secondTarget.UntilEndOfTurnPowerModifier);
        Assert.Equal(5, secondTarget.Power - secondTarget.UntilEndOfTurnPowerModifier);
        var secondModifier = Assert.Single(secondTarget.UntilEndOfTurnPowerModifiers);
        Assert.Equal(1, secondModifier.AppliedOrder);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
        var powerEffects = p2Pass.State.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, powerEffects.Length);
        Assert.Contains(
            powerEffects,
            effect => string.Equals(effect.TargetObjectId, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-SPELL-SWITCHEROO", StringComparison.Ordinal)
                && string.Equals(effect.SourceCardNo, "SFD·145/221", StringComparison.Ordinal)
                && string.Equals(effect.EffectKind, "SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS", StringComparison.Ordinal)
                && string.Equals(effect.SourcePath, "CoreRuleEngine.ApplyPowerModifier", StringComparison.Ordinal)
                && effect.IsLayerEngineFoundationOnly
                && effect.PowerDelta == 3
                && effect.AppliedOrder == 1
                && effect.BasePower == 2
                && effect.EffectivePower == 5);
        Assert.All(
            powerEffects,
            effect => Assert.Contains("full official LayerEngine coverage", effect.DeferredLayerEngineResiduals ?? []));
        var snapshotPowerEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
                p2Pass.Snapshots["P1"].Timing["continuousEffects"])
            .Where(effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal))
            .ToArray();
        var firstTargetEffectView = Assert.Single(
            snapshotPowerEffects,
            effect => string.Equals(effect["targetObjectId"] as string, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal));
        Assert.Equal("P1-SPELL-SWITCHEROO", firstTargetEffectView["sourceObjectId"]);
        Assert.Equal("SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS", firstTargetEffectView["effectKind"]);
        Assert.Equal("CoreRuleEngine.ApplyPowerModifier", firstTargetEffectView["sourcePath"]);
        Assert.Equal(1, Assert.IsType<int>(firstTargetEffectView["appliedOrder"]));
        Assert.Equal("FOUNDATION_ONLY", firstTargetEffectView["layerEngineStatus"]);
        Assert.Contains(
            "timestamp ordering",
            Assert.IsAssignableFrom<IReadOnlyList<string>>(firstTargetEffectView["deferredLayerEngineResiduals"]));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SWITCHEROO", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["powerDelta"]) == 3
            && Assert.IsType<int>(gameEvent.Payload["resultingPower"]) == 5);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["powerDelta"]) == -3
            && Assert.IsType<int>(gameEvent.Payload["resultingPower"]) == 2);
    }

    [Fact]
    public async Task SwitcherooPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildSwitcherooState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            "P1-SPELL-SWITCHEROO",
            "SFD·145/221",
            ["P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT"]);

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
        const string acceptedClientIntentId = "intent-switcheroo-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-switcheroo-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertSwitcherooStackPriorityState(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedStackHash = MatchStateHasher.HashValue(accepted.State.StackItems);
        var acceptedP1HandHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Hand);
        var acceptedP1GraveyardHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Graveyard);
        var acceptedP1BattlefieldsHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Battlefields);
        var acceptedP2BattlefieldsHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P2"].Battlefields);
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
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(replay.State.StackItems));
        Assert.Equal(acceptedP1HandHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P1"].Hand));
        Assert.Equal(acceptedP1GraveyardHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P1"].Graveyard));
        Assert.Equal(acceptedP1BattlefieldsHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(acceptedP2BattlefieldsHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P2"].Battlefields));
        AssertSwitcherooStackPriorityState(replay, acceptedStackItem);
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
        Assert.Single(journal.Entries, entry => !entry.Accepted);
        var journalHashAfterReplay = MatchStateHasher.HashValue(journal.Entries);
        var replayResultHash = MatchStateHasher.HashValue(replay);

        var duplicateReplay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateReplay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateReplay.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateReplay.ErrorMessage);
        Assert.Equal(replayResultHash, MatchStateHasher.HashValue(duplicateReplay));
        Assert.Empty(duplicateReplay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(replay.State.Tick, duplicateReplay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(duplicateReplay.State.StackItems));
        Assert.Equal(acceptedP1HandHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P1"].Hand));
        Assert.Equal(acceptedP1GraveyardHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P1"].Graveyard));
        Assert.Equal(acceptedP1BattlefieldsHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(acceptedP2BattlefieldsHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P2"].Battlefields));
        AssertSwitcherooStackPriorityState(duplicateReplay, acceptedStackItem);
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
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(conflict.State.StackItems));
        Assert.Equal(acceptedP1HandHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P1"].Hand));
        Assert.Equal(acceptedP1GraveyardHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P1"].Graveyard));
        Assert.Equal(acceptedP1BattlefieldsHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(acceptedP2BattlefieldsHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P2"].Battlefields));
        AssertSwitcherooStackPriorityState(conflict, acceptedStackItem);
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

    [Fact]
    public void SwitcherooMainActionPlayCardPromptOnlyExposesLegalBattlefieldUnitTargetsAndSelections()
    {
        var state = BuildSwitcherooState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidates = (prompt.Candidates ?? [])
            .Where(candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal))
            .ToArray();
        var playCandidate = Assert.Single(playCandidates);
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-SPELL-SWITCHEROO", StringComparison.Ordinal));

        string[] legalUnitTargetIds =
        [
            "P1-BATTLEFIELD-UNIT",
            "P2-BATTLEFIELD-UNIT"
        ];
        string[] filteredTargetIds =
        [
            "P2-BATTLEFIELD-EQUIPMENT",
            "P2-BATTLEFIELD-SPELL",
            "P2-BATTLEFIELD-RUNE",
            "P2-FACE-DOWN-STANDBY",
            "P1-BASE-UNIT",
            "UNKNOWN-TARGET"
        ];
        var targetIds = (playCandidate.Targets ?? []).Select(target => target.Id).ToArray();

        Assert.Equal(legalUnitTargetIds, targetIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        foreach (var filteredTargetId in filteredTargetIds)
        {
            Assert.DoesNotContain(filteredTargetId, targetIds);
        }

        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-SPELL-SWITCHEROO", StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Equal(["0", "1"], choicesByIndex.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        var firstTargetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();
        var secondTargetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["1"])
            .Select(choice => choice.Id)
            .ToArray();

        foreach (var choiceIds in new[] { firstTargetChoiceIds, secondTargetChoiceIds })
        {
            Assert.Equal(legalUnitTargetIds, choiceIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
            foreach (var filteredTargetId in filteredTargetIds)
            {
                Assert.DoesNotContain(filteredTargetId, choiceIds);
            }
        }

        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        Assert.False(Assert.IsType<bool>(sourceRequirement["allowsRepeatedTargets"]));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(
            sourceRequirement["legalTargetSelections"]));
    }

    [Theory]
    [InlineData("P1-SPELL-SWITCHEROO", "P1-BATTLEFIELD-UNIT", "P1-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "P1-BASE-UNIT", "P2-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "UNKNOWN-TARGET", "P2-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "P2-BATTLEFIELD-EQUIPMENT", "P1-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "P2-BATTLEFIELD-SPELL", "P1-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "P2-BATTLEFIELD-RUNE", "P1-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-SPELL-SWITCHEROO", "P2-FACE-DOWN-STANDBY", "P1-BATTLEFIELD-UNIT", 2, ErrorCodes.InvalidTarget)]
    [InlineData("P1-BASE-SWITCHEROO", "P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT", 2, ErrorCodes.CardNotInHand)]
    [InlineData("P2-SPELL-SWITCHEROO", "P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT", 2, ErrorCodes.CardNotInHand)]
    [InlineData("P1-SPELL-SWITCHEROO", "P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT", 1, ErrorCodes.InsufficientCost)]
    public async Task SwitcherooRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string firstTargetObjectId,
        string secondTargetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildSwitcherooState(mana);

        var result = await PlaySwitcherooAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            [firstTargetObjectId, secondTargetObjectId]);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SWITCHEROO"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SWITCHEROO", "P1-BASE-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-UNIT"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-SPELL-SWITCHEROO"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(
            [
                "P2-BATTLEFIELD-UNIT",
                "P2-BATTLEFIELD-EQUIPMENT",
                "P2-BATTLEFIELD-SPELL",
                "P2-BATTLEFIELD-RUNE",
                "P2-FACE-DOWN-STANDBY"
            ],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        AssertPowerStateUnchanged(result.State);
        Assert.Null(result.State.CardObjects["P2-FACE-DOWN-STANDBY"].CardNo);
        Assert.True(result.State.CardObjects["P2-FACE-DOWN-STANDBY"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P2-FACE-DOWN-STANDBY"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SwitcherooResolutionSkipsPowerMutationWhenTargetLeavesBattlefield()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSwitcherooState();

        var played = await PlaySwitcherooAsync(
            engine,
            state,
            "P1-SPELL-SWITCHEROO",
            ["P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT"]);

        Assert.True(played.Accepted, played.ErrorMessage);

        var dirtyState = played.State with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = played.State.PlayerZones["P1"],
                ["P2"] = played.State.PlayerZones["P2"] with
                {
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE",
                        "P2-FACE-DOWN-STANDBY"
                    ],
                    Base = ["P2-BATTLEFIELD-UNIT"]
                }
            }
        };

        var p1Pass = await engine.ResolveAsync(
            dirtyState,
            new PlayerIntent("intent-switcheroo-dirty-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-switcheroo-dirty-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-SWITCHEROO"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"].UntilEndOfTurnPowerModifier);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlaySwitcherooAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-switcheroo-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "SFD·145/221",
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

    private static StackItemState AssertSwitcherooStackPriorityState(
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
        Assert.Equal(["P1-BASE-SWITCHEROO", "P1-BASE-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-UNIT"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-SWITCHEROO"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(
            [
                "P2-BATTLEFIELD-UNIT",
                "P2-BATTLEFIELD-EQUIPMENT",
                "P2-BATTLEFIELD-SPELL",
                "P2-BATTLEFIELD-RUNE",
                "P2-FACE-DOWN-STANDBY"
            ],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Null(result.State.PendingPayment);
        AssertPowerStateUnchanged(result.State);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-SPELL-SWITCHEROO", stackItem.SourceObjectId);
        Assert.Equal("SFD·145/221", stackItem.CardNo);
        Assert.Equal(["P1-BATTLEFIELD-UNIT", "P2-BATTLEFIELD-UNIT"], stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal("SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS", stackItem.EffectKind);
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

    private static MatchState BuildSwitcherooState(int mana = 2)
    {
        return new MatchState(
            roomId: "switcheroo-guard-test",
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
                    Hand = ["P1-SPELL-SWITCHEROO"],
                    Base = ["P1-BASE-SWITCHEROO", "P1-BASE-UNIT"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-SWITCHEROO"],
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-UNIT",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE",
                        "P2-FACE-DOWN-STANDBY"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SWITCHEROO"] = Switcheroo("P1-SPELL-SWITCHEROO"),
                ["P1-BASE-SWITCHEROO"] = Switcheroo("P1-BASE-SWITCHEROO"),
                ["P2-SPELL-SWITCHEROO"] = Switcheroo(
                    "P2-SPELL-SWITCHEROO",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-UNIT"] = Unit("P1-BATTLEFIELD-UNIT", "P1", 2, damage: 1),
                ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT", "P1", 4),
                ["P2-BATTLEFIELD-UNIT"] = Unit("P2-BATTLEFIELD-UNIT", "P2", 5, isExhausted: true),
                ["P2-BATTLEFIELD-EQUIPMENT"] = new(
                    "P2-BATTLEFIELD-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    power: 1,
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-SPELL"] = new(
                    "P2-BATTLEFIELD-SPELL",
                    cardNo: "OGN·169/298",
                    power: 1,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-RUNE"] = new(
                    "P2-BATTLEFIELD-RUNE",
                    cardNo: "RUNES·001",
                    power: 1,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-FACE-DOWN-STANDBY"] = new(
                    "P2-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }

    private static CardObjectState Switcheroo(
        string objectId,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·145/221",
            manaCost: 2,
            tags: [CardObjectTags.SpellCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        int damage = 0,
        bool isExhausted = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            damage: damage,
            power: power,
            isExhausted: isExhausted,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static void AssertPowerStateUnchanged(MatchState state)
    {
        Assert.Equal(2, state.CardObjects["P1-BATTLEFIELD-UNIT"].Power);
        Assert.Equal(0, state.CardObjects["P1-BATTLEFIELD-UNIT"].UntilEndOfTurnPowerModifier);
        Assert.Equal(1, state.CardObjects["P1-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(5, state.CardObjects["P2-BATTLEFIELD-UNIT"].Power);
        Assert.Equal(0, state.CardObjects["P2-BATTLEFIELD-UNIT"].UntilEndOfTurnPowerModifier);
        Assert.Equal(0, state.CardObjects["P2-BATTLEFIELD-UNIT"].Damage);
        Assert.True(state.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Equal(4, state.CardObjects["P1-BASE-UNIT"].Power);
        Assert.Equal(0, state.CardObjects["P1-BASE-UNIT"].UntilEndOfTurnPowerModifier);
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
