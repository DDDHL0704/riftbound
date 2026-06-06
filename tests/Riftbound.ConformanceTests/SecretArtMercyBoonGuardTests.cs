using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SecretArtMercyBoonGuardTests
{
    [Fact]
    public async Task SecretArtMercyGrantsBoonToFriendlyPublicUnitWithoutFriendlySpellshieldTax()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSecretArtMercyState();

        var played = await PlaySecretArtMercyAsync(engine, state, "P1-FRIENDLY-SPELLSHIELD-UNIT");

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);

        var costEvent = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("P1", costEvent.Payload["playerId"]);
        Assert.Equal(3, Assert.IsType<int>(costEvent.Payload["mana"]));
        Assert.Equal(3, Assert.IsType<int>(costEvent.Payload["baseMana"]));
        Assert.Equal(0, Assert.IsType<int>(costEvent.Payload["spellshieldTaxMana"]));
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["spellshieldTaxTargetObjectIds"]));

        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SECRET-ART-MERCY", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SECRET_ART_MERCY_GRANT_BOON_NO_GLOBAL_BONUS", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-secret-art-mercy-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-secret-art-mercy-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(3, p2Pass.State.Tick);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-SECRET-ART-MERCY"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-FRIENDLY-SPELLSHIELD-UNIT", p2Pass.State.PlayerZones["P1"].Battlefields);

        var target = p2Pass.State.CardObjects["P1-FRIENDLY-SPELLSHIELD-UNIT"];
        Assert.Equal(3, target.Power);
        Assert.Equal(0, target.UntilEndOfTurnPowerModifier);
        Assert.Contains(CardObjectTags.Boon, target.Tags);
        Assert.Contains(CardObjectTags.UnitCard, target.Tags);
        Assert.Contains("法盾", target.Tags);
        Assert.Equal(3, target.Tags.Count);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SECRET-ART-MERCY", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SECRET_ART_MERCY_GRANT_BOON_NO_GLOBAL_BONUS", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "OBJECT_TAG_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SECRET-ART-MERCY", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FRIENDLY-SPELLSHIELD-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["tag"] as string, CardObjectTags.Boon, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-SECRET-ART-MERCY", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FRIENDLY-SPELLSHIELD-UNIT", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["powerDelta"]) == 1
            && Assert.IsType<int>(gameEvent.Payload["resultingPower"]) == 3);
    }

    [Fact]
    public async Task SecretArtMercyPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildSecretArtMercyState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            "P1-SPELL-SECRET-ART-MERCY",
            "OGN·053/298",
            ["P1-FRIENDLY-UNIT"]);

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
        const string acceptedClientIntentId = "intent-secret-art-mercy-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-secret-art-mercy-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertSecretArtMercyStackPriorityState(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedStackHash = MatchStateHasher.HashValue(accepted.State.StackItems);
        var acceptedP1HandHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Hand);
        var acceptedP1BattlefieldsHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Battlefields);
        var acceptedP1GraveyardHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Graveyard);
        var acceptedP2HandHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P2"].Hand);
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
        Assert.Equal(acceptedP1BattlefieldsHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(acceptedP1GraveyardHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P1"].Graveyard));
        Assert.Equal(acceptedP2HandHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P2"].Hand));
        Assert.Equal(acceptedP2BattlefieldsHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P2"].Battlefields));
        AssertSecretArtMercyStackPriorityState(replay, acceptedStackItem);
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
        var replayResultHash = MatchStateHasher.HashValue(replay);
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
        Assert.Equal(replayResultHash, MatchStateHasher.HashValue(duplicateReplay));
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(replay.State.Tick, duplicateReplay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(duplicateReplay.State.StackItems));
        Assert.Equal(acceptedP1HandHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P1"].Hand));
        Assert.Equal(acceptedP1BattlefieldsHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(acceptedP1GraveyardHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P1"].Graveyard));
        Assert.Equal(acceptedP2HandHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P2"].Hand));
        Assert.Equal(acceptedP2BattlefieldsHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P2"].Battlefields));
        AssertSecretArtMercyStackPriorityState(duplicateReplay, acceptedStackItem);
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
        Assert.Equal(acceptedP1BattlefieldsHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(acceptedP1GraveyardHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P1"].Graveyard));
        Assert.Equal(acceptedP2HandHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P2"].Hand));
        Assert.Equal(acceptedP2BattlefieldsHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P2"].Battlefields));
        AssertSecretArtMercyStackPriorityState(conflict, acceptedStackItem);
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
    [InlineData("P2-ENEMY-UNIT", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-FRIENDLY-EQUIPMENT", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-FRIENDLY-SPELL", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-FRIENDLY-RUNE", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-STALE-UNIT", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-FACE-DOWN-STANDBY", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-FRIENDLY-UNIT", 2, ErrorCodes.InsufficientCost)]
    public async Task SecretArtMercyRejectsInvalidTargetsWithoutMutation(
        string targetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildSecretArtMercyState(mana);

        var result = await PlaySecretArtMercyAsync(new CoreRuleEngine(), state, targetObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        AssertNoMutation(result, mana);
    }

    [Theory]
    [InlineData("P1-BASE-SECRET-ART-MERCY", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P2-SPELL-SECRET-ART-MERCY", 3, ErrorCodes.CardNotInHand)]
    public async Task SecretArtMercyRejectsInvalidSourcesWithoutMutation(
        string sourceObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildSecretArtMercyState(mana);

        var result = await PlaySecretArtMercyAsync(new CoreRuleEngine(), state, "P1-FRIENDLY-UNIT", sourceObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        AssertNoMutation(result, mana);
    }

    [Fact]
    public async Task SecretArtMercyAlreadyBoonedTargetDoesNotDuplicateBoonOrPower()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSecretArtMercyState(alreadyBoonedTarget: true);

        var played = await PlaySecretArtMercyAsync(engine, state, "P1-FRIENDLY-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-secret-art-mercy-already-boon-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-secret-art-mercy-already-boon-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var target = p2Pass.State.CardObjects["P1-FRIENDLY-UNIT"];
        Assert.Equal(3, target.Power);
        Assert.Contains(CardObjectTags.Boon, target.Tags);
        Assert.Contains(CardObjectTags.UnitCard, target.Tags);
        Assert.Equal(2, target.Tags.Count);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "OBJECT_TAG_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FRIENDLY-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-FRIENDLY-UNIT", StringComparison.Ordinal));
    }

    [Fact]
    public void SecretArtMercyPromptOffersLegacyCustomTagFriendlyUnitButNotNonUnits()
    {
        var state = WithLegacyCustomTagFriendlyUnit(BuildSecretArtMercyState());

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
            requirement => string.Equals(
                requirement["sourceObjectId"] as string,
                "P1-SPELL-SECRET-ART-MERCY",
                StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var targetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Contains("P1-FRIENDLY-UNIT", targetChoiceIds);
        Assert.Contains("P1-FRIENDLY-CUSTOM-UNIT", targetChoiceIds);
        Assert.DoesNotContain("P1-FRIENDLY-EQUIPMENT", targetChoiceIds);
        Assert.DoesNotContain("P1-FRIENDLY-SPELL", targetChoiceIds);
        Assert.DoesNotContain("P1-FRIENDLY-RUNE", targetChoiceIds);
        Assert.DoesNotContain("P1-FACE-DOWN-STANDBY", targetChoiceIds);
    }

    private static async Task<ResolutionResult> PlaySecretArtMercyAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId,
        string sourceObjectId = "P1-SPELL-SECRET-ART-MERCY")
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-secret-art-mercy-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "OGN·053/298",
                [targetObjectId]),
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

    private static StackItemState AssertSecretArtMercyStackPriorityState(
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
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SECRET-ART-MERCY"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P1-FRIENDLY-UNIT",
                "P1-FRIENDLY-SPELLSHIELD-UNIT",
                "P1-FRIENDLY-EQUIPMENT",
                "P1-FRIENDLY-SPELL",
                "P1-FRIENDLY-RUNE",
                "P1-FACE-DOWN-STANDBY"
            ],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-SECRET-ART-MERCY"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-ENEMY-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal("STACK", result.State.ObjectLocations["P1-SPELL-SECRET-ART-MERCY"].Zone);
        Assert.Equal(2, result.State.CardObjects["P1-FRIENDLY-UNIT"].Power);
        Assert.DoesNotContain(CardObjectTags.Boon, result.State.CardObjects["P1-FRIENDLY-UNIT"].Tags);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-SPELL-SECRET-ART-MERCY", stackItem.SourceObjectId);
        Assert.Equal("OGN·053/298", stackItem.CardNo);
        Assert.Equal(["P1-FRIENDLY-UNIT"], stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal("SECRET_ART_MERCY_GRANT_BOON_NO_GLOBAL_BONUS", stackItem.EffectKind);
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

    private static void AssertNoMutation(ResolutionResult result, int mana)
    {
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SECRET-ART-MERCY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SECRET-ART-MERCY"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P1-FRIENDLY-UNIT",
                "P1-FRIENDLY-SPELLSHIELD-UNIT",
                "P1-FRIENDLY-EQUIPMENT",
                "P1-FRIENDLY-SPELL",
                "P1-FRIENDLY-RUNE",
                "P1-FACE-DOWN-STANDBY"
            ],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-SPELL-SECRET-ART-MERCY"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-ENEMY-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);

        Assert.Equal(2, result.State.CardObjects["P1-FRIENDLY-UNIT"].Power);
        Assert.DoesNotContain(CardObjectTags.Boon, result.State.CardObjects["P1-FRIENDLY-UNIT"].Tags);
        Assert.Equal(2, result.State.CardObjects["P1-FRIENDLY-SPELLSHIELD-UNIT"].Power);
        Assert.Equal([CardObjectTags.UnitCard, "法盾"], result.State.CardObjects["P1-FRIENDLY-SPELLSHIELD-UNIT"].Tags);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "OBJECT_TAG_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal));
    }

    private static MatchState BuildSecretArtMercyState(
        int mana = 3,
        bool alreadyBoonedTarget = false)
    {
        return new MatchState(
            roomId: "secret-art-mercy-boon-guard-test",
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
                    Hand = ["P1-SPELL-SECRET-ART-MERCY"],
                    Base = ["P1-BASE-SECRET-ART-MERCY"],
                    Battlefields =
                    [
                        "P1-FRIENDLY-UNIT",
                        "P1-FRIENDLY-SPELLSHIELD-UNIT",
                        "P1-FRIENDLY-EQUIPMENT",
                        "P1-FRIENDLY-SPELL",
                        "P1-FRIENDLY-RUNE",
                        "P1-FACE-DOWN-STANDBY"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-SECRET-ART-MERCY"],
                    Battlefields = ["P2-ENEMY-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SECRET-ART-MERCY"] = SecretArtMercy("P1-SPELL-SECRET-ART-MERCY"),
                ["P1-BASE-SECRET-ART-MERCY"] = SecretArtMercy("P1-BASE-SECRET-ART-MERCY"),
                ["P2-SPELL-SECRET-ART-MERCY"] = SecretArtMercy(
                    "P2-SPELL-SECRET-ART-MERCY",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-FRIENDLY-UNIT"] = Unit(
                    "P1-FRIENDLY-UNIT",
                    power: alreadyBoonedTarget ? 3 : 2,
                    tags: alreadyBoonedTarget ? [CardObjectTags.Boon, CardObjectTags.UnitCard] : [CardObjectTags.UnitCard]),
                ["P1-FRIENDLY-SPELLSHIELD-UNIT"] = Unit(
                    "P1-FRIENDLY-SPELLSHIELD-UNIT",
                    tags: [CardObjectTags.UnitCard, "法盾"]),
                ["P1-FRIENDLY-EQUIPMENT"] = NonUnit("P1-FRIENDLY-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard),
                ["P1-FRIENDLY-SPELL"] = NonUnit("P1-FRIENDLY-SPELL", "OGN·169/298", CardObjectTags.SpellCard),
                ["P1-FRIENDLY-RUNE"] = NonUnit("P1-FRIENDLY-RUNE", "RUNES·001", CardObjectTags.RuneCard),
                ["P1-STALE-UNIT"] = Unit("P1-STALE-UNIT"),
                ["P1-FACE-DOWN-STANDBY"] = Unit(
                    "P1-FACE-DOWN-STANDBY",
                    cardNo: null,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P2-ENEMY-UNIT"] = Unit(
                    "P2-ENEMY-UNIT",
                ownerId: "P2",
                controllerId: "P2")
            });
    }

    private static MatchState WithLegacyCustomTagFriendlyUnit(MatchState state)
    {
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            ["P1"] = state.PlayerZones["P1"] with
            {
                Battlefields =
                [
                    .. state.PlayerZones["P1"].Battlefields,
                    "P1-FRIENDLY-CUSTOM-UNIT"
                ]
            }
        };
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            ["P1-FRIENDLY-CUSTOM-UNIT"] = Unit(
                "P1-FRIENDLY-CUSTOM-UNIT",
                tags: ["黄沙士兵"])
        };

        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static CardObjectState SecretArtMercy(
        string objectId,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·053/298",
            manaCost: 3,
            tags: [CardObjectTags.SpellCard],
            ownerId: ownerId,
            controllerId: controllerId);
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
