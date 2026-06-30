using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AkshanGuardTests
{
    private const string AkshanObjectId = "P1-UNIT-AKSHAN";
    private const string AkshanCardNo = "SFD·109/221";
    private const string AkshanStealPrefix = "AKSHAN_STEAL_EQUIPMENT:";
    private const string AkshanStealReason = "AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL";
    private const string VengeanceObjectId = "P1-SPELL-VENGEANCE";
    private const string EnemyWeaponObjectId = "P2-EQUIPMENT-WEAPON";
    private const string EnemyNonWeaponObjectId = "P2-EQUIPMENT-NON-WEAPON";
    private const string FriendlyEquipmentObjectId = "P1-EQUIPMENT-FRIENDLY";
    private const string OrangeRuneObjectId = "P1-RUNE-ORANGE";
    private const string PayOrangePower = "orange";

    [Fact]
    public async Task AkshanPlayCardWithNoTargetsUsesStackAndResolvesToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanState();

        var played = await PlayAkshanAsync(engine, state, "P1-UNIT-AKSHAN", []);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-akshan-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-akshan-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), p2Pass.State.RunePools["P1"]);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-AKSHAN", "P1-FACE-DOWN-STANDBY-AKSHAN", "P1-UNIT-AKSHAN"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);

        var unit = p2Pass.State.CardObjects["P1-UNIT-AKSHAN"];
        Assert.Equal("SFD·109/221", unit.CardNo);
        Assert.Equal("P1", unit.OwnerId);
        Assert.Equal("P1", unit.ControllerId);
        Assert.Equal(4, unit.Power);
        Assert.Equal([CardObjectTags.UnitCard, "哨兵", "百炼"], unit.Tags);
        Assert.False(unit.IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "阿克尚", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["power"]) == 4);
    }

    [Fact]
    public void AkshanMainActionPlayCardPromptDoesNotExposeTargetsForNoTargetUnitPlay()
    {
        var state = BuildAkshanPromptStateWithEnemyEquipment();

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate =>
                string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal)
                && candidate.Enabled
                && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, AkshanObjectId, StringComparison.Ordinal)));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, AkshanObjectId, StringComparison.Ordinal));

        var invalidTargetObjectIds = new[]
        {
            "P1-TARGET-UNIT",
            "P1-BASE-AKSHAN",
            "P1-FACE-DOWN-STANDBY-AKSHAN",
            EnemyWeaponObjectId,
            EnemyNonWeaponObjectId
        };
        var targetIds = (playCandidate.Targets ?? []).Select(target => target.Id).ToArray();
        Assert.Empty(targetIds);
        foreach (var invalidTargetObjectId in invalidTargetObjectIds)
        {
            Assert.DoesNotContain(invalidTargetObjectId, targetIds);
        }

        var requirement = AkshanSourceRequirement(state, AkshanObjectId, AkshanCardNo);
        Assert.Equal(AkshanObjectId, requirement["sourceObjectId"]);
        Assert.Equal(AkshanCardNo, requirement["cardNo"]);
        Assert.Equal(0, Assert.IsType<int>(requirement["minTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["maxTargetCount"]));

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            requirement["targetChoicesByIndex"]);
        var metadataTargetIds = targetChoicesByIndex.Values
            .Where(rawTargetChoices => rawTargetChoices is not null)
            .SelectMany(rawTargetChoices => Assert
                .IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(rawTargetChoices)
                .Select(choice => choice.Id))
            .ToArray();
        Assert.Empty(targetChoicesByIndex);
        Assert.Empty(metadataTargetIds);
        foreach (var invalidTargetObjectId in invalidTargetObjectIds)
        {
            Assert.DoesNotContain(invalidTargetObjectId, metadataTargetIds);
        }
    }

    [Fact]
    public async Task AkshanPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildAkshanState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            AkshanObjectId,
            AkshanCardNo,
            []);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, AkshanObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var reorderedStaleRawCommand = ReorderedPromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        Assert.NotEqual(staleRawCommand.GetRawText(), reorderedStaleRawCommand.GetRawText());
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(reorderedStaleRawCommand));
        AssertPromptScopedPlayCardRawCommand(reorderedStaleRawCommand, prompt, assertPropertyOrder: false);
        Assert.False(reorderedStaleRawCommand.TryGetProperty("clientNote", out _));
        const string acceptedClientIntentId = "intent-akshan-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-akshan-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertAkshanStackPriorityState(accepted);
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
        AssertAkshanStackPriorityState(replay, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = Assert.Single(journal.Entries, entry => !entry.Accepted);
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
        AssertAkshanStackPriorityState(reorderedReplay, acceptedStackItem);
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
        AssertAkshanStackPriorityState(duplicateReplay, acceptedStackItem);
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
        AssertAkshanStackPriorityState(conflict, acceptedStackItem);
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
    public void AkshanPromptExposesLegalTemperedAndEnemyEquipmentChoicesWhenOrangeCostPayable()
    {
        var state = BuildAkshanStealState();

        var requirement = AkshanSourceRequirement(state);
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["optionalCostChoices"]);

        Assert.Equal(
            [TemperedAttachCost(FriendlyEquipmentObjectId), StealCost(EnemyNonWeaponObjectId), StealCost(EnemyWeaponObjectId)],
            optionalCostChoices.Select(choice => choice.Id).ToArray());
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            requirement["targetChoicesByIndex"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["minTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["maxTargetCount"]));

        var noOrangeRequirement = AkshanSourceRequirement(BuildAkshanStealState(orangePower: 1));
        var noOrangeChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            noOrangeRequirement["optionalCostChoices"]);
        Assert.Equal(
            [TemperedAttachCost(FriendlyEquipmentObjectId)],
            noOrangeChoices.Select(choice => choice.Id).ToArray());
    }

    [Fact]
    public async Task AkshanOrangeStealWeaponPaysOrangeMovesControlsAndAttaches()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanStealState();
        var optionalCosts = new[] { StealCost(EnemyWeaponObjectId) };

        var played = await PlayAkshanAsync(engine, state, AkshanObjectId, [], optionalCosts);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(0, played.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(PayOrangePower));
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal(optionalCosts, stackItem.OptionalCosts);

        var costEvent = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(2, powerByTrait[PayOrangePower]);

        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(AkshanObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(EnemyWeaponObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(EnemyWeaponObjectId, resolved.State.PlayerZones["P2"].Base);
        var equipment = resolved.State.CardObjects[EnemyWeaponObjectId];
        Assert.Equal("P2", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Equal(AkshanObjectId, equipment.AttachedToObjectId);

        var controlEvent = Assert.Single(resolved.Events, IsAkshanControlChanged);
        Assert.Equal(AkshanObjectId, controlEvent.Payload["sourceObjectId"]);
        Assert.Equal(EnemyWeaponObjectId, controlEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P2", controlEvent.Payload["previousControllerId"]);
        Assert.Equal("P1", controlEvent.Payload["controllerId"]);
        Assert.Equal("P2", controlEvent.Payload["ownerId"]);
        Assert.Equal(AkshanStealReason, controlEvent.Payload["reason"]);
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(controlEvent.Payload["optionalCosts"]));

        var attachedEvent = Assert.Single(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal));
        Assert.Equal(EnemyWeaponObjectId, attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal(AkshanObjectId, attachedEvent.Payload["attachedToObjectId"]);
    }

    [Fact]
    public async Task AkshanCanPayTemperedAttachAndOrangeStealTogether()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanStealState();
        var optionalCosts = new[] { TemperedAttachCost(FriendlyEquipmentObjectId), StealCost(EnemyWeaponObjectId) };

        var played = await PlayAkshanAsync(engine, state, AkshanObjectId, [], optionalCosts);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(0, played.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(PayOrangePower));
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal(optionalCosts, stackItem.OptionalCosts);

        var costEvent = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(2, powerByTrait[PayOrangePower]);

        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal(AkshanObjectId, resolved.State.CardObjects[FriendlyEquipmentObjectId].AttachedToObjectId);
        Assert.Equal(AkshanObjectId, resolved.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
        Assert.Equal("P1", resolved.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Contains(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "TEMPERED_OPTIONAL_ATTACH", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, FriendlyEquipmentObjectId, StringComparison.Ordinal));
        Assert.Contains(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, EnemyWeaponObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AkshanOrangeStealCanRecycleOrangeRuneForSecondOrangePower()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanStealState(orangePower: 1, includeOrangeRune: true);
        var optionalCosts = new[] { StealCost(EnemyWeaponObjectId), RecycleOrangeRuneCost() };

        var requirement = AkshanSourceRequirement(state);
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["optionalCostChoices"]);
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["paymentResourceChoices"]);

        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, StealCost(EnemyWeaponObjectId), StringComparison.Ordinal));
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, RecycleOrangeRuneCost(), StringComparison.Ordinal));

        var played = await PlayAkshanAsync(engine, state, AkshanObjectId, [], optionalCosts);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.DoesNotContain(OrangeRuneObjectId, played.State.PlayerZones["P1"].Base);
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal([StealCost(EnemyWeaponObjectId)], stackItem.OptionalCosts);
        var costEvent = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([StealCost(EnemyWeaponObjectId)], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal([RecycleOrangeRuneCost()], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(2, powerByTrait[PayOrangePower]);

        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal("P1", resolved.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Equal(AkshanObjectId, resolved.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
    }

    [Fact]
    public void AkshanOrangeStealPromptDoesNotQuoteGenericTemporaryResourceInPlayCardWindow()
    {
        var state = BuildAkshanStealState(orangePower: 1, includeOrangeRune: true) with
        {
            TemporaryPaymentResources = [GenericTemporaryPlayCardPaymentResource()]
        };

        var requirement = AkshanSourceRequirement(state);
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                requirement["optionalCostChoices"])
            .ToArray();
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                requirement["paymentResourceChoices"])
            .ToArray();

        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, StealCost(EnemyWeaponObjectId), StringComparison.Ordinal));
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, RecycleOrangeRuneCost(), StringComparison.Ordinal));
        Assert.DoesNotContain(
            paymentResourceChoices,
            choice => choice.Id.StartsWith(PaymentCostRules.TemporaryPaymentResourceActionPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AkshanOrangeStealNonWeaponMovesAndControlsWithoutAttach()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanStealState();

        var played = await PlayAkshanAsync(
            engine,
            state,
            AkshanObjectId,
            [],
            [StealCost(EnemyNonWeaponObjectId)]);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(EnemyNonWeaponObjectId, resolved.State.PlayerZones["P1"].Base);
        var equipment = resolved.State.CardObjects[EnemyNonWeaponObjectId];
        Assert.Equal("P2", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Null(equipment.AttachedToObjectId);
        Assert.Contains(resolved.Events, IsAkshanControlChanged);
        Assert.DoesNotContain(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-EQUIPMENT-FRIENDLY", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-MISSING-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-NON-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-HAND-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-FACE-DOWN-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-STALE-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-P1-CONTROLLED-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("P2-P1-OWNED-EQUIPMENT", 4, 2, 0, ErrorCodes.UnsupportedCardBehavior)]
    [InlineData(EnemyWeaponObjectId, 4, 1, 0, ErrorCodes.InsufficientCost)]
    [InlineData(EnemyWeaponObjectId, 4, 0, 2, ErrorCodes.InsufficientCost)]
    public async Task AkshanOrangeStealRejectsInvalidOrInsufficientChoicesWithoutMutation(
        string equipmentObjectId,
        int mana,
        int orangePower,
        int greenPower,
        string expectedErrorCode)
    {
        var state = BuildAkshanStealState(mana: mana, orangePower: orangePower, greenPower: greenPower);

        var result = await PlayAkshanAsync(
            new CoreRuleEngine(),
            state,
            AkshanObjectId,
            [],
            [StealCost(equipmentObjectId)]);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Contains(AkshanObjectId, result.State.PlayerZones["P1"].Hand);
        Assert.Contains(EnemyWeaponObjectId, result.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(EnemyWeaponObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal("P2", result.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Null(result.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Theory]
    [InlineData("AKSHAN_STEAL_EQUIPMENT:")]
    [InlineData("AKSHAN_STEAL_EQUIPMENT:P2-EQUIPMENT-WEAPON", "AKSHAN_STEAL_EQUIPMENT:P2-EQUIPMENT-NON-WEAPON")]
    [InlineData("AKSHAN_STEAL_EQUIPMENT:P2-EQUIPMENT-WEAPON", "AKSHAN_STEAL_EQUIPMENT:P2-EQUIPMENT-WEAPON")]
    public async Task AkshanOrangeStealRejectsMalformedDuplicateOrConflictingOptionalCosts(params string[] optionalCosts)
    {
        var state = BuildAkshanStealState();

        var result = await PlayAkshanAsync(
            new CoreRuleEngine(),
            state,
            AkshanObjectId,
            [],
            optionalCosts);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Contains(AkshanObjectId, result.State.PlayerZones["P1"].Hand);
        Assert.Equal("P2", result.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Null(result.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task AkshanOrangeStealStaleEquipmentBeforeResolutionNoEffectsEquipmentSide()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanStealState();
        var played = await PlayAkshanAsync(
            engine,
            state,
            AkshanObjectId,
            [],
            [StealCost(EnemyWeaponObjectId)]);
        var staleState = MoveEnemyWeaponToGraveyard(played.State);

        var resolved = await ResolveTopStackAsync(engine, staleState);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(AkshanObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(EnemyWeaponObjectId, resolved.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("P2", resolved.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Null(resolved.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
        Assert.DoesNotContain(resolved.Events, IsAkshanControlChanged);
        Assert.DoesNotContain(resolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AkshanStolenEquipmentDoesNotReturnAtEndTurnWhileAkshanRemains()
    {
        var engine = new CoreRuleEngine();
        var resolved = await PlayAndResolveAkshanStealAsync(engine, BuildAkshanStealState());

        var ended = await engine.ResolveAsync(
            resolved.State,
            new PlayerIntent("intent-akshan-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(ended.Accepted, ended.ErrorMessage);
        Assert.Contains(EnemyWeaponObjectId, ended.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(EnemyWeaponObjectId, ended.State.PlayerZones["P2"].Base);
        Assert.Equal("P1", ended.State.CardObjects[EnemyWeaponObjectId].ControllerId);
        Assert.Equal(AkshanObjectId, ended.State.CardObjects[EnemyWeaponObjectId].AttachedToObjectId);
        Assert.DoesNotContain(ended.Events, IsAkshanControlReturned);
    }

    [Fact]
    public async Task AkshanLeavingFieldReturnsStolenEquipmentToOwnerBase()
    {
        var engine = new CoreRuleEngine();
        var resolved = await PlayAndResolveAkshanStealAsync(
            engine,
            BuildAkshanStealState(mana: 8, includeVengeance: true));

        var vengeancePlayed = await engine.ResolveAsync(
            resolved.State,
            new PlayerIntent("intent-akshan-vengeance-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                VengeanceObjectId,
                "OGN·229/298",
                [AkshanObjectId]),
            CancellationToken.None);
        var vengeanceResolved = await ResolveTopStackAsync(engine, vengeancePlayed.State);

        Assert.True(vengeancePlayed.Accepted, vengeancePlayed.ErrorMessage);
        Assert.True(vengeanceResolved.Accepted, vengeanceResolved.ErrorMessage);
        Assert.Contains(AkshanObjectId, vengeanceResolved.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(AkshanObjectId, vengeanceResolved.State.CardObjects.Keys);
        Assert.Contains(EnemyWeaponObjectId, vengeanceResolved.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(EnemyWeaponObjectId, vengeanceResolved.State.PlayerZones["P1"].Base);
        var equipment = vengeanceResolved.State.CardObjects[EnemyWeaponObjectId];
        Assert.Equal("P2", equipment.OwnerId);
        Assert.Equal("P2", equipment.ControllerId);
        Assert.Null(equipment.AttachedToObjectId);

        var returnEvent = Assert.Single(vengeanceResolved.Events, IsAkshanControlReturned);
        Assert.Equal(AkshanObjectId, returnEvent.Payload["sourceObjectId"]);
        Assert.Equal(EnemyWeaponObjectId, returnEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P1", returnEvent.Payload["previousControllerId"]);
        Assert.Equal("P2", returnEvent.Payload["controllerId"]);
        Assert.Equal(AkshanStealReason, returnEvent.Payload["reason"]);
    }

    [Theory]
    [InlineData("P1-UNIT-AKSHAN", "P1-TARGET-UNIT", 4, ErrorCodes.InvalidTarget)]
    [InlineData("P1-BASE-AKSHAN", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P2-UNIT-AKSHAN", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P1-FACE-DOWN-STANDBY-AKSHAN", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P1-UNIT-AKSHAN", "", 3, ErrorCodes.InsufficientCost)]
    public async Task AkshanPlayCardRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string targetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildAkshanState(mana);
        var targetObjectIds = string.IsNullOrWhiteSpace(targetObjectId) ? Array.Empty<string>() : [targetObjectId];

        var result = await PlayAkshanAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            targetObjectIds);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-AKSHAN"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-AKSHAN", "P1-FACE-DOWN-STANDBY-AKSHAN"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-UNIT-AKSHAN"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.False(result.State.CardObjects["P1-UNIT-AKSHAN"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY-AKSHAN"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-AKSHAN"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY-AKSHAN"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayAkshanAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-akshan-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "SFD·109/221",
                targetObjectIds,
                OptionalCosts: optionalCosts),
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
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
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
        var expectedPropertyNames = new[]
        {
            "cmdType",
            "cardObjectId",
            "cardNo",
            "targetObjectIds",
            "optionalCosts",
            "promptId",
            "snapshotTick"
        };
        var propertyNames = rawCommand.EnumerateObject().Select(property => property.Name).ToArray();
        if (assertPropertyOrder)
        {
            Assert.Equal(expectedPropertyNames, propertyNames);
        }
        else
        {
            Assert.Equal(
                expectedPropertyNames.OrderBy(propertyName => propertyName).ToArray(),
                propertyNames.OrderBy(propertyName => propertyName).ToArray());
        }

        Assert.Equal(CommandTypes.PlayCard, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(AkshanObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(AkshanCardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertAkshanStackPriorityState(
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
            ["P1-TARGET-UNIT", "P1-BASE-AKSHAN", "P1-FACE-DOWN-STANDBY-AKSHAN"],
            result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal("STACK", result.State.ObjectLocations[AkshanObjectId].Zone);

        var unit = result.State.CardObjects[AkshanObjectId];
        Assert.Equal(AkshanCardNo, unit.CardNo);
        Assert.Equal("P1", unit.OwnerId);
        Assert.Equal("P1", unit.ControllerId);
        Assert.Equal(0, unit.Power);
        Assert.NotEqual(4, unit.Power);
        Assert.Equal(4, unit.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard], unit.Tags);
        Assert.DoesNotContain(unit.Tags, tag => string.Equals(tag, "哨兵", StringComparison.Ordinal));
        Assert.DoesNotContain(unit.Tags, tag => string.Equals(tag, "百炼", StringComparison.Ordinal));
        Assert.False(unit.IsExhausted);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(AkshanObjectId, stackItem.SourceObjectId);
        Assert.Equal(AkshanCardNo, stackItem.CardNo);
        Assert.Equal("AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT", stackItem.EffectKind);
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

    private static IReadOnlyDictionary<string, object?> AkshanSourceRequirement(MatchState state)
    {
        return AkshanSourceRequirement(state, AkshanObjectId, AkshanCardNo);
    }

    private static IReadOnlyDictionary<string, object?> AkshanSourceRequirement(
        MatchState state,
        string sourceObjectId,
        string cardNo)
    {
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        return Assert.Single(
            sourceRequirements,
            entry => string.Equals(entry["sourceObjectId"] as string, sourceObjectId, StringComparison.Ordinal)
                && string.Equals(entry["cardNo"] as string, cardNo, StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayAndResolveAkshanStealAsync(
        CoreRuleEngine engine,
        MatchState state,
        string equipmentObjectId = EnemyWeaponObjectId)
    {
        var played = await PlayAkshanAsync(
            engine,
            state,
            AkshanObjectId,
            [],
            [StealCost(equipmentObjectId)]);
        Assert.True(played.Accepted, played.ErrorMessage);
        return await ResolveTopStackAsync(engine, played.State);
    }

    private static async Task<ResolutionResult> ResolveTopStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-akshan-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-akshan-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        return p2Pass;
    }

    private static MatchState MoveEnemyWeaponToGraveyard(MatchState state)
    {
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var p2Zones = playerZones["P2"];
        playerZones["P2"] = p2Zones with
        {
            Base = p2Zones.Base
                .Where(objectId => !string.Equals(objectId, EnemyWeaponObjectId, StringComparison.Ordinal))
                .ToArray(),
            Graveyard = p2Zones.Graveyard.Contains(EnemyWeaponObjectId, StringComparer.Ordinal)
                ? p2Zones.Graveyard
                : p2Zones.Graveyard.Concat([EnemyWeaponObjectId]).ToArray()
        };

        return state with
        {
            PlayerZones = playerZones
        };
    }

    private static MatchState BuildAkshanStealState(
        int mana = 4,
        int orangePower = 2,
        int greenPower = 0,
        bool includeVengeance = false,
        bool includeOrangeRune = false)
    {
        var powerByTrait = new Dictionary<string, int>(StringComparer.Ordinal);
        if (orangePower > 0)
        {
            powerByTrait[RuneTrait.Orange] = orangePower;
        }

        if (greenPower > 0)
        {
            powerByTrait[RuneTrait.Green] = greenPower;
        }

        var p1Hand = includeVengeance
            ? new[] { AkshanObjectId, VengeanceObjectId }
            : [AkshanObjectId];
        var p1Base = includeOrangeRune
            ? new[] { FriendlyEquipmentObjectId, OrangeRuneObjectId }
            : [FriendlyEquipmentObjectId];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [AkshanObjectId] = Akshan(AkshanObjectId),
            [EnemyWeaponObjectId] = Equipment(EnemyWeaponObjectId, "P2", "P2", weapon: true),
            [EnemyNonWeaponObjectId] = Equipment(EnemyNonWeaponObjectId, "P2", "P2", weapon: false),
            [FriendlyEquipmentObjectId] = Equipment(FriendlyEquipmentObjectId, "P1", "P1", weapon: true),
            ["P2-NON-EQUIPMENT"] = new(
                "P2-NON-EQUIPMENT",
                cardNo: "SFD·125/221",
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2"),
            ["P2-HAND-EQUIPMENT"] = Equipment("P2-HAND-EQUIPMENT", "P2", "P2", weapon: true),
            ["P2-FACE-DOWN-EQUIPMENT"] = Equipment("P2-FACE-DOWN-EQUIPMENT", "P2", "P2", weapon: true, isFaceDown: true),
            ["P2-STALE-EQUIPMENT"] = Equipment("P2-STALE-EQUIPMENT", "P2", "P2", weapon: true),
            ["P2-P1-CONTROLLED-EQUIPMENT"] = Equipment("P2-P1-CONTROLLED-EQUIPMENT", "P2", "P1", weapon: true),
            ["P2-P1-OWNED-EQUIPMENT"] = Equipment("P2-P1-OWNED-EQUIPMENT", "P1", "P2", weapon: true)
        };
        if (includeOrangeRune)
        {
            cardObjects[OrangeRuneObjectId] = Rune(OrangeRuneObjectId, RuneTrait.Orange);
        }

        if (includeVengeance)
        {
            cardObjects[VengeanceObjectId] = new CardObjectState(
                VengeanceObjectId,
                cardNo: "OGN·229/298",
                manaCost: 4,
                tags: [CardObjectTags.SpellCard],
                ownerId: "P1",
                controllerId: "P1");
        }

        return new MatchState(
            roomId: "akshan-orange-steal-test",
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
                ["P1"] = new(mana, 0, powerByTrait),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = p1Hand,
                    Base = p1Base
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-EQUIPMENT"],
                    Base =
                    [
                        EnemyWeaponObjectId,
                        EnemyNonWeaponObjectId,
                        "P2-NON-EQUIPMENT",
                        "P2-FACE-DOWN-EQUIPMENT",
                        "P2-P1-CONTROLLED-EQUIPMENT",
                        "P2-P1-OWNED-EQUIPMENT"
                    ],
                    Graveyard = ["P2-STALE-EQUIPMENT"]
                }
            },
            cardObjects: cardObjects,
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static MatchState BuildAkshanState(int mana = 4)
    {
        return new MatchState(
            roomId: "akshan-guard-test",
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
                    Hand = ["P1-UNIT-AKSHAN"],
                    Base =
                    [
                        "P1-TARGET-UNIT",
                        "P1-BASE-AKSHAN",
                        "P1-FACE-DOWN-STANDBY-AKSHAN"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-UNIT-AKSHAN"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-AKSHAN"] = Akshan("P1-UNIT-AKSHAN"),
                ["P1-BASE-AKSHAN"] = Akshan("P1-BASE-AKSHAN"),
                ["P1-FACE-DOWN-STANDBY-AKSHAN"] = Akshan(
                    "P1-FACE-DOWN-STANDBY-AKSHAN",
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P2-UNIT-AKSHAN"] = Akshan(
                    "P2-UNIT-AKSHAN",
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

    private static MatchState BuildAkshanPromptStateWithEnemyEquipment()
    {
        var state = BuildAkshanState();
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var p2Zones = playerZones["P2"];
        playerZones["P2"] = p2Zones with
        {
            Base = [.. p2Zones.Base, EnemyWeaponObjectId, EnemyNonWeaponObjectId]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[EnemyWeaponObjectId] = Equipment(EnemyWeaponObjectId, "P2", "P2", weapon: true);
        cardObjects[EnemyNonWeaponObjectId] = Equipment(EnemyNonWeaponObjectId, "P2", "P2", weapon: false);

        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static CardObjectState Akshan(
        string objectId,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·109/221",
            isFaceDown: isFaceDown,
            manaCost: 4,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Equipment(
        string objectId,
        string ownerId,
        string controllerId,
        bool weapon,
        bool isFaceDown = false)
    {
        var tags = weapon
            ? new[] { CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon }
            : [CardObjectTags.EquipmentCard];
        return new CardObjectState(
            objectId,
            cardNo: weapon ? "SFD·186/221" : "SFD·064/221",
            isFaceDown: isFaceDown,
            tags: tags,
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static bool IsAkshanControlChanged(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "EQUIPMENT_CONTROL_CHANGED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal);
    }

    private static bool IsAkshanControlReturned(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "EQUIPMENT_CONTROL_RETURNED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, AkshanStealReason, StringComparison.Ordinal);
    }

    private static string StealCost(string equipmentObjectId)
    {
        return $"{AkshanStealPrefix}{equipmentObjectId}";
    }

    private static string TemperedAttachCost(string equipmentObjectId)
    {
        return $"TEMPERED_ATTACH:{equipmentObjectId}";
    }

    private static string RecycleOrangeRuneCost()
    {
        return $"RECYCLE_RUNE:{OrangeRuneObjectId}";
    }

    private static TemporaryPaymentResourceState GenericTemporaryPlayCardPaymentResource()
    {
        return new TemporaryPaymentResourceState(
            "MALZAHAR:TEMP-AKSHAN-PLAY-CARD",
            "P1",
            "P1-MALZAHAR",
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
            "PLAY_CARD",
            2,
            2,
            [PaymentCostRules.RuneCostPaymentKind],
            1);
    }

    private static CardObjectState Rune(string objectId, string trait)
    {
        return new CardObjectState(
            objectId,
            cardNo: $"RUNE-{trait}",
            tags: [CardObjectTags.RuneCard, $"COLOR:{trait}"],
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
