using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OrnnFriendlyEquipmentStaticPowerTests
{
    private const string OrnnObjectId = "P1-UNIT-ORNN-STATIC";
    private const string OrnnCardNo = "SFD·085/221";
    private const string OrnnAltCardNo = "SFD·085a/221";
    private const string FriendlyBaseEquipmentObjectId = "P1-EQUIPMENT-BASE";
    private const string SecondFriendlyBaseEquipmentObjectId = "P1-EQUIPMENT-BASE-2";
    private const string FriendlyPlayedEquipmentObjectId = "P1-EQUIPMENT-PLAYED";
    private const string HandEquipmentObjectId = "P1-EQUIPMENT-HAND";
    private const string FaceDownEquipmentObjectId = "P1-EQUIPMENT-FACE-DOWN";
    private const string DirtyControllerEquipmentObjectId = "P1-EQUIPMENT-DIRTY-CONTROLLER";
    private const string EnemyEquipmentObjectId = "P2-EQUIPMENT-ENEMY";
    private const string FriendlyUnitObjectId = "P1-UNIT-FRIENDLY";
    private const string FirstRuneObjectId = "P1-RUNE-1";
    private const string SecondRuneObjectId = "P1-RUNE-2";
    private const string VengeanceObjectId = "P1-SPELL-VENGEANCE";
    private const string LegionQuartermasterObjectId = "P1-UNIT-LEGION-QUARTERMASTER";

    [Theory]
    [InlineData(OrnnCardNo)]
    [InlineData(OrnnAltCardNo)]
    public async Task OrnnCountsOnlyFriendlyPublicFieldEquipmentWhenPlayed(string cardNo)
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnState(cardNo, includeFriendlyFieldEquipment: true);

        var played = await PlayOrnnAsync(engine, state, cardNo);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Contains(OrnnObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(FriendlyBaseEquipmentObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(SecondFriendlyBaseEquipmentObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(EnemyEquipmentObjectId, resolved.State.PlayerZones["P2"].Base);

        var ornn = resolved.State.CardObjects[OrnnObjectId];
        Assert.Equal(6, ornn.Power);
        Assert.Equal([CardObjectTags.UnitCard, "法盾2", CardEquipmentKeywordNames.Tempered], ornn.Tags);
        var staticAura = Assert.Single(
            resolved.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(2, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(6, staticAura.EffectivePower);
        Assert.Equal(
            [FriendlyBaseEquipmentObjectId, SecondFriendlyBaseEquipmentObjectId],
            staticAura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal(
            [FriendlyBaseEquipmentObjectId, SecondFriendlyBaseEquipmentObjectId],
            staticAura.ParticipantDependencyObjectIds);
        Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_PUBLIC_FIELD_EQUIPMENT_COUNT", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_AUTHORITATIVE_FIELD_STATE", staticAura.Lifecycle);

        var unitPlayed = Assert.Single(resolved.Events, IsOrnnUnitPlayedEvent);
        Assert.Equal(6, Assert.IsType<int>(unitPlayed.Payload["power"]));
        Assert.Equal(2, Assert.IsType<int>(unitPlayed.Payload["friendlyEquipmentPowerBonus"]));
    }

    [Fact]
    public async Task OrnnPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildOrnnState(OrnnCardNo, includeFriendlyFieldEquipment: true);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(OrnnObjectId, OrnnCardNo, []);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, OrnnObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-ornn-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-ornn-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertOrnnStackPriorityState(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedStackHash = MatchStateHasher.HashValue(accepted.State.StackItems);
        var acceptedHandHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Hand);
        var acceptedBaseHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Base);
        var acceptedBattlefieldHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Battlefields);
        var acceptedObjectLocationsHash = MatchStateHasher.HashValue(accepted.State.ObjectLocations);
        var acceptedOrnnHash = MatchStateHasher.HashValue(accepted.State.CardObjects[OrnnObjectId]);
        var acceptedFriendlyEquipmentHash = MatchStateHasher.HashValue(new[]
        {
            accepted.State.CardObjects[FriendlyBaseEquipmentObjectId],
            accepted.State.CardObjects[SecondFriendlyBaseEquipmentObjectId]
        });
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
        Assert.Equal(acceptedHandHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P1"].Hand));
        Assert.Equal(acceptedBaseHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P1"].Base));
        Assert.Equal(acceptedBattlefieldHash, MatchStateHasher.HashValue(replay.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(replay.State.ObjectLocations));
        Assert.Equal(acceptedOrnnHash, MatchStateHasher.HashValue(replay.State.CardObjects[OrnnObjectId]));
        Assert.Equal(
            acceptedFriendlyEquipmentHash,
            MatchStateHasher.HashValue(new[]
            {
                replay.State.CardObjects[FriendlyBaseEquipmentObjectId],
                replay.State.CardObjects[SecondFriendlyBaseEquipmentObjectId]
            }));
        AssertOrnnStackPriorityState(replay, acceptedStackItem);
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
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(duplicateReplay.State.StackItems));
        Assert.Equal(acceptedHandHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P1"].Hand));
        Assert.Equal(acceptedBaseHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P1"].Base));
        Assert.Equal(acceptedBattlefieldHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(duplicateReplay.State.ObjectLocations));
        Assert.Equal(acceptedOrnnHash, MatchStateHasher.HashValue(duplicateReplay.State.CardObjects[OrnnObjectId]));
        Assert.Equal(
            acceptedFriendlyEquipmentHash,
            MatchStateHasher.HashValue(new[]
            {
                duplicateReplay.State.CardObjects[FriendlyBaseEquipmentObjectId],
                duplicateReplay.State.CardObjects[SecondFriendlyBaseEquipmentObjectId]
            }));
        AssertOrnnStackPriorityState(duplicateReplay, acceptedStackItem);
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
        Assert.Equal(acceptedHandHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P1"].Hand));
        Assert.Equal(acceptedBaseHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P1"].Base));
        Assert.Equal(acceptedBattlefieldHash, MatchStateHasher.HashValue(conflict.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(conflict.State.ObjectLocations));
        Assert.Equal(acceptedOrnnHash, MatchStateHasher.HashValue(conflict.State.CardObjects[OrnnObjectId]));
        Assert.Equal(
            acceptedFriendlyEquipmentHash,
            MatchStateHasher.HashValue(new[]
            {
                conflict.State.CardObjects[FriendlyBaseEquipmentObjectId],
                conflict.State.CardObjects[SecondFriendlyBaseEquipmentObjectId]
            }));
        AssertOrnnStackPriorityState(conflict, acceptedStackItem);
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
    public async Task OrnnKeepsBasePowerWhenNoFriendlyPublicFieldEquipmentExists()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnState(OrnnCardNo, includeFriendlyFieldEquipment: false);

        var played = await PlayOrnnAsync(engine, state, OrnnCardNo);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal(4, resolved.State.CardObjects[OrnnObjectId].Power);
        var staticAura = Assert.Single(
            resolved.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Empty(staticAura.ParticipantObjectIds ?? []);
        Assert.Equal(0, staticAura.PowerDelta);
        var unitPlayed = Assert.Single(resolved.Events, IsOrnnUnitPlayedEvent);
        Assert.Equal(4, Assert.IsType<int>(unitPlayed.Payload["power"]));
        Assert.False(unitPlayed.Payload.ContainsKey("friendlyEquipmentPowerBonus"));
    }

    [Fact]
    public async Task OrnnFriendlyEquipmentStaticPowerDoesNotProjectFromStandbySource()
    {
        var state = BuildOrnnFieldState(
            ornnPower: 4,
            p1Base: [OrnnObjectId, FriendlyBaseEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 4, isStandby: true),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1")
            });

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));

        var advanced = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-standby-source-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(advanced.Accepted, advanced.ErrorMessage);
        Assert.Equal(4, advanced.State.CardObjects[OrnnObjectId].Power);
        Assert.DoesNotContain(
            advanced.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OrnnFriendlyEquipmentStaticAuraMetadataMatchesAuthoritativeStateAcrossPlayerViews()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnState(OrnnCardNo, includeFriendlyFieldEquipment: true);
        var expectedResiduals = new[]
        {
            "timestamp ordering",
            "dependency ordering",
            "source ordering",
            "keyword gain/loss layering",
            "multiple equipment/static aura interactions",
            "minimum-power layering",
            "full official LayerEngine coverage"
        };

        var played = await PlayOrnnAsync(engine, state, OrnnCardNo);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        var staticAura = Assert.Single(
            resolved.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal($"STATIC_AURA:FRIENDLY_EQUIPMENT_POWER:{OrnnObjectId}", staticAura.EffectId);
        Assert.Equal("OBJECT", staticAura.Scope);
        Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", staticAura.Duration);
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(OrnnObjectId, staticAura.SourceObjectId);
        Assert.Equal(2, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(6, staticAura.EffectivePower);
        Assert.Equal("FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER", staticAura.EffectKind);
        Assert.Equal(OrnnCardNo, staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute", staticAura.SourcePath);
        Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_PUBLIC_FIELD_EQUIPMENT_COUNT", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_AUTHORITATIVE_FIELD_STATE", staticAura.Lifecycle);
        Assert.True(staticAura.IsLayerEngineFoundationOnly);
        Assert.Equal(expectedResiduals, staticAura.DeferredLayerEngineResiduals);
        Assert.Equal([FriendlyBaseEquipmentObjectId, SecondFriendlyBaseEquipmentObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal(
            [FriendlyBaseEquipmentObjectId, SecondFriendlyBaseEquipmentObjectId],
            staticAura.ParticipantDependencyObjectIds);
        Assert.Equal(1, staticAura.Sequence);
        Assert.Equal(5, staticAura.SourceOrder.GetValueOrDefault());
        Assert.Null(staticAura.RequestedPowerDelta);
        Assert.Null(staticAura.AppliedPowerDelta);
        Assert.Null(staticAura.MinimumPower);
        Assert.Null(staticAura.ResultingPower);
        Assert.Null(staticAura.AppliedOrder);

        var p1View = AssertSnapshotStaticAuraMetadata(resolved.Snapshots["P1"], staticAura, expectedResiduals);
        var p2View = AssertSnapshotStaticAuraMetadata(resolved.Snapshots["P2"], staticAura, expectedResiduals);
        Assert.Equal(StaticAuraMetadataSignature(p1View), StaticAuraMetadataSignature(p2View));
    }

    [Fact]
    public async Task OrnnStaticAuraOmitsParticipantMetadataWhenFriendlyEquipmentLeavesPublicFieldAcrossPlayerViews()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 5,
            p1Base: [OrnnObjectId, FirstRuneObjectId],
            p1Graveyard: [FriendlyBaseEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 5),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [FirstRuneObjectId] = Rune(FirstRuneObjectId)
            });

        var tapped = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-aura-no-participants", "P1", CommandTypes.TapRune),
            new TapRuneCommand(FirstRuneObjectId),
            CancellationToken.None);

        Assert.True(tapped.Accepted, tapped.ErrorMessage);
        Assert.Equal(4, tapped.State.CardObjects[OrnnObjectId].Power);
        var staticAura = Assert.Single(
            tapped.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(0, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(4, staticAura.EffectivePower);
        Assert.Empty(staticAura.ParticipantObjectIds ?? []);
        Assert.Equal([OrnnObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Empty(staticAura.ParticipantDependencyObjectIds ?? []);

        var p1View = AssertSnapshotStaticAura(
            tapped.Snapshots["P1"],
            OrnnObjectId,
            OrnnObjectId,
            [],
            powerDelta: 0,
            basePower: 4,
            effectivePower: 4);
        var p2View = AssertSnapshotStaticAura(
            tapped.Snapshots["P2"],
            OrnnObjectId,
            OrnnObjectId,
            [],
            powerDelta: 0,
            basePower: 4,
            effectivePower: 4);
        Assert.Equal(
            p1View.Keys.OrderBy(key => key, StringComparer.Ordinal),
            p2View.Keys.OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public async Task OrnnStaticAuraKeepsOnlyRemainingFriendlyEquipmentParticipantMetadataAcrossPlayerViews()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 6,
            p1Base: [OrnnObjectId, FriendlyBaseEquipmentObjectId, FirstRuneObjectId],
            p1Graveyard: [SecondFriendlyBaseEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 6),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [SecondFriendlyBaseEquipmentObjectId] = Equipment(SecondFriendlyBaseEquipmentObjectId, "P1", "P1"),
                [FirstRuneObjectId] = Rune(FirstRuneObjectId)
            });
        var expectedResiduals = new[]
        {
            "timestamp ordering",
            "dependency ordering",
            "source ordering",
            "keyword gain/loss layering",
            "multiple equipment/static aura interactions",
            "minimum-power layering",
            "full official LayerEngine coverage"
        };

        var tapped = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-aura-one-remaining-participant", "P1", CommandTypes.TapRune),
            new TapRuneCommand(FirstRuneObjectId),
            CancellationToken.None);

        Assert.True(tapped.Accepted, tapped.ErrorMessage);
        Assert.Equal(5, tapped.State.CardObjects[OrnnObjectId].Power);
        var staticAura = Assert.Single(
            tapped.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(5, staticAura.EffectivePower);
        Assert.Equal([FriendlyBaseEquipmentObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal([FriendlyBaseEquipmentObjectId], staticAura.ParticipantDependencyObjectIds);
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, staticAura.ParticipantObjectIds ?? []);
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, staticAura.ParticipantDependencyObjectIds ?? []);

        var p1View = AssertSnapshotStaticAuraMetadata(tapped.Snapshots["P1"], staticAura, expectedResiduals);
        var p2View = AssertSnapshotStaticAuraMetadata(tapped.Snapshots["P2"], staticAura, expectedResiduals);
        Assert.Equal(StaticAuraMetadataSignature(p1View), StaticAuraMetadataSignature(p2View));
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, StringList(p1View, "participantObjectIds"));
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, StringList(p1View, "participantDependencyObjectIds"));
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, StringList(p2View, "participantObjectIds"));
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, StringList(p2View, "participantDependencyObjectIds"));
    }

    [Fact]
    public async Task OrnnStaticAuraExclusionMetadataOmitsNonFriendlyPublicEquipmentAcrossPlayerViews()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 7,
            p1Hand: [HandEquipmentObjectId],
            p1Base:
            [
                OrnnObjectId,
                FaceDownEquipmentObjectId,
                DirtyControllerEquipmentObjectId,
                FriendlyUnitObjectId,
                FirstRuneObjectId
            ],
            p2Base: [EnemyEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 7),
                [HandEquipmentObjectId] = Equipment(HandEquipmentObjectId, "P1", "P1"),
                [FaceDownEquipmentObjectId] = Equipment(FaceDownEquipmentObjectId, "P1", "P1", isFaceDown: true),
                [DirtyControllerEquipmentObjectId] = Equipment(DirtyControllerEquipmentObjectId, "P1", "P2"),
                [FriendlyUnitObjectId] = Unit(FriendlyUnitObjectId, "SFD·125/221", "P1", "P1", power: 3),
                [EnemyEquipmentObjectId] = Equipment(EnemyEquipmentObjectId, "P2", "P2"),
                [FirstRuneObjectId] = Rune(FirstRuneObjectId)
            });
        var excludedObjectIds = new[]
        {
            HandEquipmentObjectId,
            FaceDownEquipmentObjectId,
            DirtyControllerEquipmentObjectId,
            FriendlyUnitObjectId,
            EnemyEquipmentObjectId
        };

        var tapped = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-aura-exclusion-metadata", "P1", CommandTypes.TapRune),
            new TapRuneCommand(FirstRuneObjectId),
            CancellationToken.None);

        Assert.True(tapped.Accepted, tapped.ErrorMessage);
        Assert.Equal(4, tapped.State.CardObjects[OrnnObjectId].Power);
        var staticAura = Assert.Single(
            tapped.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(0, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(4, staticAura.EffectivePower);
        Assert.Empty(staticAura.ParticipantObjectIds ?? []);
        Assert.Equal([OrnnObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Empty(staticAura.ParticipantDependencyObjectIds ?? []);
        AssertStaticAuraDoesNotReferenceObjectIds(staticAura, excludedObjectIds);

        var p1View = AssertSnapshotStaticAura(
            tapped.Snapshots["P1"],
            OrnnObjectId,
            OrnnObjectId,
            [],
            powerDelta: 0,
            basePower: 4,
            effectivePower: 4);
        var p2View = AssertSnapshotStaticAura(
            tapped.Snapshots["P2"],
            OrnnObjectId,
            OrnnObjectId,
            [],
            powerDelta: 0,
            basePower: 4,
            effectivePower: 4);
        AssertSnapshotStaticAuraDoesNotReferenceObjectIds(p1View, excludedObjectIds);
        AssertSnapshotStaticAuraDoesNotReferenceObjectIds(p2View, excludedObjectIds);
    }

    [Fact]
    public async Task OrnnRecomputesUpWhenFriendlyPublicEquipmentResolvesAfterOrnnIsInField()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 4,
            p1Hand: [FriendlyPlayedEquipmentObjectId],
            p1Base: [OrnnObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 4),
                [FriendlyPlayedEquipmentObjectId] = Equipment(
                    FriendlyPlayedEquipmentObjectId,
                    "P1",
                    "P1",
                    cardNo: "SFD·046/221")
            });

        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-dynamic-play-equipment", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(FriendlyPlayedEquipmentObjectId, "SFD·046/221", []),
            CancellationToken.None);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(FriendlyPlayedEquipmentObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Equal(5, resolved.State.CardObjects[OrnnObjectId].Power);
        AssertSnapshotPower(resolved.Snapshots["P1"], OrnnObjectId, basePower: 5, effectivePower: 5);
        AssertSnapshotStaticAura(
            resolved.Snapshots["P1"],
            OrnnObjectId,
            OrnnObjectId,
            [FriendlyPlayedEquipmentObjectId],
            powerDelta: 1,
            basePower: 4,
            effectivePower: 5);
    }

    [Fact]
    public async Task OrnnDynamicEquipmentResolveRefreshesStaticAuraMetadataAcrossPlayerViews()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 4,
            p1Hand: [FriendlyPlayedEquipmentObjectId],
            p1Base: [OrnnObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 4),
                [FriendlyPlayedEquipmentObjectId] = Equipment(
                    FriendlyPlayedEquipmentObjectId,
                    "P1",
                    "P1",
                    cardNo: "SFD·046/221")
            });
        var expectedResiduals = new[]
        {
            "timestamp ordering",
            "dependency ordering",
            "source ordering",
            "keyword gain/loss layering",
            "multiple equipment/static aura interactions",
            "minimum-power layering",
            "full official LayerEngine coverage"
        };

        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-dynamic-metadata-play-equipment", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(FriendlyPlayedEquipmentObjectId, "SFD·046/221", []),
            CancellationToken.None);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(FriendlyPlayedEquipmentObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Equal(5, resolved.State.CardObjects[OrnnObjectId].Power);
        AssertSnapshotPower(resolved.Snapshots["P1"], OrnnObjectId, basePower: 5, effectivePower: 5);
        AssertSnapshotPower(resolved.Snapshots["P2"], OrnnObjectId, basePower: 5, effectivePower: 5);

        var staticAura = Assert.Single(
            resolved.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal($"STATIC_AURA:FRIENDLY_EQUIPMENT_POWER:{OrnnObjectId}", staticAura.EffectId);
        Assert.Equal("OBJECT", staticAura.Scope);
        Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", staticAura.Duration);
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(OrnnObjectId, staticAura.SourceObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(5, staticAura.EffectivePower);
        Assert.Equal("FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER", staticAura.EffectKind);
        Assert.Equal(OrnnCardNo, staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute", staticAura.SourcePath);
        Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_PUBLIC_FIELD_EQUIPMENT_COUNT", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_AUTHORITATIVE_FIELD_STATE", staticAura.Lifecycle);
        Assert.True(staticAura.IsLayerEngineFoundationOnly);
        Assert.Equal(expectedResiduals, staticAura.DeferredLayerEngineResiduals);
        Assert.Equal([FriendlyPlayedEquipmentObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal([FriendlyPlayedEquipmentObjectId], staticAura.ParticipantDependencyObjectIds);
        Assert.True(staticAura.Sequence > 0);
        Assert.True(staticAura.SourceOrder.GetValueOrDefault() > 0);
        Assert.Null(staticAura.RequestedPowerDelta);
        Assert.Null(staticAura.AppliedPowerDelta);
        Assert.Null(staticAura.MinimumPower);
        Assert.Null(staticAura.ResultingPower);
        Assert.Null(staticAura.AppliedOrder);

        var p1View = AssertSnapshotStaticAuraMetadata(resolved.Snapshots["P1"], staticAura, expectedResiduals);
        var p2View = AssertSnapshotStaticAuraMetadata(resolved.Snapshots["P2"], staticAura, expectedResiduals);
        Assert.Equal(StaticAuraMetadataSignature(p1View), StaticAuraMetadataSignature(p2View));
        Assert.Equal([FriendlyPlayedEquipmentObjectId], StringList(p1View, "participantObjectIds"));
        Assert.Equal([FriendlyPlayedEquipmentObjectId], StringList(p2View, "participantObjectIds"));
        Assert.Equal([OrnnObjectId], StringList(p1View, "sourceDependencyObjectIds"));
        Assert.Equal([OrnnObjectId], StringList(p2View, "sourceDependencyObjectIds"));
        Assert.Equal([OrnnObjectId], StringList(p1View, "targetDependencyObjectIds"));
        Assert.Equal([OrnnObjectId], StringList(p2View, "targetDependencyObjectIds"));
        Assert.Equal([FriendlyPlayedEquipmentObjectId], StringList(p1View, "participantDependencyObjectIds"));
        Assert.Equal([FriendlyPlayedEquipmentObjectId], StringList(p2View, "participantDependencyObjectIds"));
        Assert.Equal(expectedResiduals, StringList(p1View, "deferredLayerEngineResiduals"));
        Assert.Equal(expectedResiduals, StringList(p2View, "deferredLayerEngineResiduals"));
    }

    [Fact]
    public async Task OrnnDynamicEnemyEquipmentResolveDoesNotChangeStaticAuraParticipantMetadataAcrossPlayerViews()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 5,
            p1Base: [OrnnObjectId, FriendlyBaseEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 5),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [EnemyEquipmentObjectId] = Equipment(
                    EnemyEquipmentObjectId,
                    "P2",
                    "P2",
                    cardNo: "SFD·046/221")
            });
        state = state with
        {
            TurnPlayerId = "P2",
            ActivePlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(state.RunePools, StringComparer.Ordinal)
            {
                ["P2"] = new(6, 0)
            },
            PlayerZones = state.PlayerZones.ToDictionary(
                entry => entry.Key,
                entry => string.Equals(entry.Key, "P2", StringComparison.Ordinal)
                    ? entry.Value with
                    {
                        Hand = [EnemyEquipmentObjectId]
                    }
                    : entry.Value,
                StringComparer.Ordinal),
            ObjectLocations = state.ObjectLocations
                .Append(new KeyValuePair<string, ObjectLocationState>(
                    EnemyEquipmentObjectId,
                    new ObjectLocationState("P2", "HAND")))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
        };
        var expectedResiduals = new[]
        {
            "timestamp ordering",
            "dependency ordering",
            "source ordering",
            "keyword gain/loss layering",
            "multiple equipment/static aura interactions",
            "minimum-power layering",
            "full official LayerEngine coverage"
        };

        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-dynamic-enemy-equipment-play", "P2", CommandTypes.PlayCard),
            new PlayCardCommand(EnemyEquipmentObjectId, "SFD·046/221", []),
            CancellationToken.None);
        var resolved = await ResolveTopStackAfterP2PlayAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(EnemyEquipmentObjectId, resolved.State.PlayerZones["P2"].Base);
        Assert.Contains(FriendlyBaseEquipmentObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Equal(5, resolved.State.CardObjects[OrnnObjectId].Power);
        AssertSnapshotPower(resolved.Snapshots["P1"], OrnnObjectId, basePower: 5, effectivePower: 5);
        AssertSnapshotPower(resolved.Snapshots["P2"], OrnnObjectId, basePower: 5, effectivePower: 5);

        var staticAura = Assert.Single(
            resolved.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(5, staticAura.EffectivePower);
        Assert.Equal([FriendlyBaseEquipmentObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal([FriendlyBaseEquipmentObjectId], staticAura.ParticipantDependencyObjectIds);
        AssertStaticAuraDoesNotReferenceObjectIds(staticAura, [EnemyEquipmentObjectId]);

        var p1View = AssertSnapshotStaticAuraMetadata(resolved.Snapshots["P1"], staticAura, expectedResiduals);
        var p2View = AssertSnapshotStaticAuraMetadata(resolved.Snapshots["P2"], staticAura, expectedResiduals);
        Assert.Equal(StaticAuraMetadataSignature(p1View), StaticAuraMetadataSignature(p2View));
        Assert.Equal([FriendlyBaseEquipmentObjectId], StringList(p1View, "participantObjectIds"));
        Assert.Equal([FriendlyBaseEquipmentObjectId], StringList(p2View, "participantObjectIds"));
        Assert.Equal([FriendlyBaseEquipmentObjectId], StringList(p1View, "participantDependencyObjectIds"));
        Assert.Equal([FriendlyBaseEquipmentObjectId], StringList(p2View, "participantDependencyObjectIds"));
        AssertSnapshotStaticAuraDoesNotReferenceObjectIds(p1View, [EnemyEquipmentObjectId]);
        AssertSnapshotStaticAuraDoesNotReferenceObjectIds(p2View, [EnemyEquipmentObjectId]);
    }

    [Fact]
    public async Task OrnnDynamicEquipmentRemovalRefreshesStaticAuraMetadataAcrossPlayerViews()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 6,
            p1Hand: [LegionQuartermasterObjectId],
            p1Base: [OrnnObjectId, FriendlyBaseEquipmentObjectId, SecondFriendlyBaseEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 6),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [SecondFriendlyBaseEquipmentObjectId] = Equipment(SecondFriendlyBaseEquipmentObjectId, "P1", "P1"),
                [LegionQuartermasterObjectId] = Unit(LegionQuartermasterObjectId, "SFD·044/221", "P1", "P1")
            });
        var expectedResiduals = new[]
        {
            "timestamp ordering",
            "dependency ordering",
            "source ordering",
            "keyword gain/loss layering",
            "multiple equipment/static aura interactions",
            "minimum-power layering",
            "full official LayerEngine coverage"
        };

        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-dynamic-metadata-remove-equipment", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                LegionQuartermasterObjectId,
                "SFD·044/221",
                [],
                OptionalCosts: [$"RETURN_FRIENDLY_EQUIPMENT:{SecondFriendlyBaseEquipmentObjectId}"]),
            CancellationToken.None);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Contains(FriendlyBaseEquipmentObjectId, played.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, played.State.PlayerZones["P1"].Base);
        Assert.Contains(SecondFriendlyBaseEquipmentObjectId, played.State.PlayerZones["P1"].Hand);
        Assert.Equal(5, played.State.CardObjects[OrnnObjectId].Power);
        AssertSnapshotPower(played.Snapshots["P1"], OrnnObjectId, basePower: 5, effectivePower: 5);
        AssertSnapshotPower(played.Snapshots["P2"], OrnnObjectId, basePower: 5, effectivePower: 5);

        var staticAura = Assert.Single(
            played.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(5, staticAura.EffectivePower);
        Assert.Equal([FriendlyBaseEquipmentObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal([FriendlyBaseEquipmentObjectId], staticAura.ParticipantDependencyObjectIds);
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, staticAura.ParticipantObjectIds ?? []);
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, staticAura.ParticipantDependencyObjectIds ?? []);

        var p1View = AssertSnapshotStaticAuraMetadata(played.Snapshots["P1"], staticAura, expectedResiduals);
        var p2View = AssertSnapshotStaticAuraMetadata(played.Snapshots["P2"], staticAura, expectedResiduals);
        Assert.Equal(StaticAuraMetadataSignature(p1View), StaticAuraMetadataSignature(p2View));
        Assert.Equal([FriendlyBaseEquipmentObjectId], StringList(p1View, "participantObjectIds"));
        Assert.Equal([FriendlyBaseEquipmentObjectId], StringList(p2View, "participantObjectIds"));
        Assert.Equal([FriendlyBaseEquipmentObjectId], StringList(p1View, "participantDependencyObjectIds"));
        Assert.Equal([FriendlyBaseEquipmentObjectId], StringList(p2View, "participantDependencyObjectIds"));
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, StringList(p1View, "participantObjectIds"));
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, StringList(p1View, "participantDependencyObjectIds"));
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, StringList(p2View, "participantObjectIds"));
        Assert.DoesNotContain(SecondFriendlyBaseEquipmentObjectId, StringList(p2View, "participantDependencyObjectIds"));
    }

    [Fact]
    public async Task OrnnDynamicLastEquipmentRemovalOmitsStaticAuraParticipantMetadataAcrossPlayerViews()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 5,
            p1Hand: [LegionQuartermasterObjectId],
            p1Base: [OrnnObjectId, FriendlyBaseEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 5),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [LegionQuartermasterObjectId] = Unit(LegionQuartermasterObjectId, "SFD·044/221", "P1", "P1")
            });

        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-dynamic-metadata-remove-last-equipment", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                LegionQuartermasterObjectId,
                "SFD·044/221",
                [],
                OptionalCosts: [$"RETURN_FRIENDLY_EQUIPMENT:{FriendlyBaseEquipmentObjectId}"]),
            CancellationToken.None);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.DoesNotContain(FriendlyBaseEquipmentObjectId, played.State.PlayerZones["P1"].Base);
        Assert.Contains(FriendlyBaseEquipmentObjectId, played.State.PlayerZones["P1"].Hand);
        Assert.Equal(4, played.State.CardObjects[OrnnObjectId].Power);
        AssertSnapshotPower(played.Snapshots["P1"], OrnnObjectId, basePower: 4, effectivePower: 4);
        AssertSnapshotPower(played.Snapshots["P2"], OrnnObjectId, basePower: 4, effectivePower: 4);

        var staticAura = Assert.Single(
            played.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(0, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(4, staticAura.EffectivePower);
        Assert.Empty(staticAura.ParticipantObjectIds ?? []);
        Assert.Equal([OrnnObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Empty(staticAura.ParticipantDependencyObjectIds ?? []);
        Assert.DoesNotContain(FriendlyBaseEquipmentObjectId, staticAura.ParticipantObjectIds ?? []);
        Assert.DoesNotContain(FriendlyBaseEquipmentObjectId, staticAura.ParticipantDependencyObjectIds ?? []);

        var p1View = AssertSnapshotStaticAura(
            played.Snapshots["P1"],
            OrnnObjectId,
            OrnnObjectId,
            [],
            powerDelta: 0,
            basePower: 4,
            effectivePower: 4);
        var p2View = AssertSnapshotStaticAura(
            played.Snapshots["P2"],
            OrnnObjectId,
            OrnnObjectId,
            [],
            powerDelta: 0,
            basePower: 4,
            effectivePower: 4);
        Assert.Equal(
            p1View.Keys.OrderBy(key => key, StringComparer.Ordinal),
            p2View.Keys.OrderBy(key => key, StringComparer.Ordinal));
        AssertSnapshotStaticAuraDoesNotReferenceObjectIds(p1View, [FriendlyBaseEquipmentObjectId]);
        AssertSnapshotStaticAuraDoesNotReferenceObjectIds(p2View, [FriendlyBaseEquipmentObjectId]);
    }

    [Fact]
    public async Task OrnnRecomputesDownFromStableBaseAndDoesNotDriftAcrossRepeatedAcceptedCommands()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 6,
            p1Base: [OrnnObjectId, FriendlyBaseEquipmentObjectId, SecondFriendlyBaseEquipmentObjectId, FirstRuneObjectId, SecondRuneObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 6),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [SecondFriendlyBaseEquipmentObjectId] = Equipment(SecondFriendlyBaseEquipmentObjectId, "P1", "P1"),
                [FirstRuneObjectId] = Rune(FirstRuneObjectId),
                [SecondRuneObjectId] = Rune(SecondRuneObjectId)
            });
        state = state with
        {
            PlayerZones = state.PlayerZones.ToDictionary(
                entry => entry.Key,
                entry => string.Equals(entry.Key, "P1", StringComparison.Ordinal)
                    ? entry.Value with
                    {
                        Base = [OrnnObjectId, FriendlyBaseEquipmentObjectId, FirstRuneObjectId, SecondRuneObjectId],
                        Graveyard = [SecondFriendlyBaseEquipmentObjectId]
                    }
                    : entry.Value,
                StringComparer.Ordinal),
            ObjectLocations = state.ObjectLocations
                .Where(entry => !string.Equals(entry.Key, SecondFriendlyBaseEquipmentObjectId, StringComparison.Ordinal))
                .Append(new KeyValuePair<string, ObjectLocationState>(
                    SecondFriendlyBaseEquipmentObjectId,
                    new ObjectLocationState("P1", "GRAVEYARD")))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
        };

        var firstTap = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-dynamic-tap-rune-1", "P1", CommandTypes.TapRune),
            new TapRuneCommand(FirstRuneObjectId),
            CancellationToken.None);
        var secondTap = await engine.ResolveAsync(
            firstTap.State,
            new PlayerIntent("intent-ornn-dynamic-tap-rune-2", "P1", CommandTypes.TapRune),
            new TapRuneCommand(SecondRuneObjectId),
            CancellationToken.None);

        Assert.True(firstTap.Accepted, firstTap.ErrorMessage);
        Assert.True(secondTap.Accepted, secondTap.ErrorMessage);
        Assert.Equal(5, firstTap.State.CardObjects[OrnnObjectId].Power);
        Assert.Equal(5, secondTap.State.CardObjects[OrnnObjectId].Power);
        AssertSnapshotPower(secondTap.Snapshots["P1"], OrnnObjectId, basePower: 5, effectivePower: 5);
        AssertSnapshotStaticAura(
            secondTap.Snapshots["P1"],
            OrnnObjectId,
            OrnnObjectId,
            [FriendlyBaseEquipmentObjectId],
            powerDelta: 1,
            basePower: 4,
            effectivePower: 5);
    }

    [Fact]
    public async Task OrnnStaticAuraExclusionMetadataDoesNotLeakIgnoredEquipmentAcrossPlayerViews()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 8,
            p1Hand: [HandEquipmentObjectId],
            p1Base:
            [
                OrnnObjectId,
                FriendlyBaseEquipmentObjectId,
                FaceDownEquipmentObjectId,
                DirtyControllerEquipmentObjectId,
                FriendlyUnitObjectId,
                FirstRuneObjectId
            ],
            p2Base: [EnemyEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 8),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [HandEquipmentObjectId] = Equipment(HandEquipmentObjectId, "P1", "P1"),
                [FaceDownEquipmentObjectId] = Equipment(FaceDownEquipmentObjectId, "P1", "P1", isFaceDown: true),
                [DirtyControllerEquipmentObjectId] = Equipment(DirtyControllerEquipmentObjectId, "P1", "P2"),
                [FriendlyUnitObjectId] = Unit(FriendlyUnitObjectId, "SFD·125/221", "P1", "P1", power: 3),
                [EnemyEquipmentObjectId] = Equipment(EnemyEquipmentObjectId, "P2", "P2"),
                [FirstRuneObjectId] = Rune(FirstRuneObjectId)
            });
        var ignoredObjectIds = new[]
        {
            HandEquipmentObjectId,
            FaceDownEquipmentObjectId,
            DirtyControllerEquipmentObjectId,
            FriendlyUnitObjectId,
            EnemyEquipmentObjectId,
            FirstRuneObjectId
        };
        var expectedResiduals = new[]
        {
            "timestamp ordering",
            "dependency ordering",
            "source ordering",
            "keyword gain/loss layering",
            "multiple equipment/static aura interactions",
            "minimum-power layering",
            "full official LayerEngine coverage"
        };

        var tapped = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-aura-ignored-equipment-metadata", "P1", CommandTypes.TapRune),
            new TapRuneCommand(FirstRuneObjectId),
            CancellationToken.None);

        Assert.True(tapped.Accepted, tapped.ErrorMessage);
        Assert.Equal(5, tapped.State.CardObjects[OrnnObjectId].Power);
        var staticAura = Assert.Single(
            tapped.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(5, staticAura.EffectivePower);
        Assert.Equal([FriendlyBaseEquipmentObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal([FriendlyBaseEquipmentObjectId], staticAura.ParticipantDependencyObjectIds);

        var p1View = AssertSnapshotStaticAuraMetadata(tapped.Snapshots["P1"], staticAura, expectedResiduals);
        var p2View = AssertSnapshotStaticAuraMetadata(tapped.Snapshots["P2"], staticAura, expectedResiduals);
        Assert.Equal(StaticAuraMetadataSignature(p1View), StaticAuraMetadataSignature(p2View));
        foreach (var ignoredObjectId in ignoredObjectIds)
        {
            Assert.DoesNotContain(ignoredObjectId, staticAura.ParticipantObjectIds ?? []);
            Assert.DoesNotContain(ignoredObjectId, staticAura.ParticipantDependencyObjectIds ?? []);
            Assert.DoesNotContain(ignoredObjectId, StringList(p1View, "participantObjectIds"));
            Assert.DoesNotContain(ignoredObjectId, StringList(p1View, "participantDependencyObjectIds"));
            Assert.DoesNotContain(ignoredObjectId, StringList(p2View, "participantObjectIds"));
            Assert.DoesNotContain(ignoredObjectId, StringList(p2View, "participantDependencyObjectIds"));
        }
    }

    [Fact]
    public async Task OrnnRecomputeExcludesEnemyHandFaceDownDirtyControllerAndNonEquipmentObjects()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 7,
            p1Hand: [HandEquipmentObjectId],
            p1Base:
            [
                OrnnObjectId,
                FriendlyBaseEquipmentObjectId,
                FaceDownEquipmentObjectId,
                DirtyControllerEquipmentObjectId,
                FriendlyUnitObjectId,
                FirstRuneObjectId
            ],
            p2Base: [EnemyEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 7),
                [HandEquipmentObjectId] = Equipment(HandEquipmentObjectId, "P1", "P1"),
                [FriendlyBaseEquipmentObjectId] = Unit(FriendlyBaseEquipmentObjectId, "SFD·022/221", "P1", "P1"),
                [FaceDownEquipmentObjectId] = Equipment(FaceDownEquipmentObjectId, "P1", "P1", isFaceDown: true),
                [DirtyControllerEquipmentObjectId] = Equipment(DirtyControllerEquipmentObjectId, "P1", "P2"),
                [FriendlyUnitObjectId] = Unit(FriendlyUnitObjectId, "SFD·125/221", "P1", "P1", power: 3),
                [EnemyEquipmentObjectId] = Equipment(EnemyEquipmentObjectId, "P2", "P2"),
                [FirstRuneObjectId] = Rune(FirstRuneObjectId)
            });

        var tapped = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-dynamic-exclusions", "P1", CommandTypes.TapRune),
            new TapRuneCommand(FirstRuneObjectId),
            CancellationToken.None);

        Assert.True(tapped.Accepted, tapped.ErrorMessage);
        Assert.Equal(4, tapped.State.CardObjects[OrnnObjectId].Power);
        AssertSnapshotPower(tapped.Snapshots["P1"], OrnnObjectId, basePower: 4, effectivePower: 4);
        AssertSnapshotStaticAura(
            tapped.Snapshots["P1"],
            OrnnObjectId,
            OrnnObjectId,
            [],
            powerDelta: 0,
            basePower: 4,
            effectivePower: 4);
    }

    [Fact]
    public void OrnnStaticAuraMetadataDisappearsWhenSourceLeavesFieldAcrossPlayerViews()
    {
        var state = BuildOrnnFieldState(
            ornnPower: 5,
            p1Base: [FriendlyBaseEquipmentObjectId],
            p1Graveyard: [OrnnObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 5),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1")
            });

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));

        var snapshots = ResolutionResult.BuildSnapshots(state);
        AssertSnapshotDoesNotExposeOrnnStaticAuraMetadata(
            snapshots["P1"],
            OrnnObjectId,
            FriendlyBaseEquipmentObjectId);
        AssertSnapshotDoesNotExposeOrnnStaticAuraMetadata(
            snapshots["P2"],
            OrnnObjectId,
            FriendlyBaseEquipmentObjectId);
    }

    [Fact]
    public async Task OrnnStaticAuraMetadataDisappearsAfterAcceptedSourceLeavesFieldCommandAcrossPlayerViews()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 5,
            p1Hand: [VengeanceObjectId],
            p1Base: [OrnnObjectId, FriendlyBaseEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 5),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [VengeanceObjectId] = new CardObjectState(
                    VengeanceObjectId,
                    cardNo: "OGN·229/298",
                    manaCost: 4,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var initialAura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal([FriendlyBaseEquipmentObjectId], initialAura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], initialAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], initialAura.TargetDependencyObjectIds);
        Assert.Equal([FriendlyBaseEquipmentObjectId], initialAura.ParticipantDependencyObjectIds);

        var vengeancePlayed = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-source-leaves-vengeance-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(VengeanceObjectId, "OGN·229/298", [OrnnObjectId]),
            CancellationToken.None);
        var vengeanceResolved = await ResolveTopStackAsync(engine, vengeancePlayed.State);

        Assert.True(vengeancePlayed.Accepted, vengeancePlayed.ErrorMessage);
        Assert.True(vengeanceResolved.Accepted, vengeanceResolved.ErrorMessage);
        Assert.Contains(OrnnObjectId, vengeanceResolved.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(FriendlyBaseEquipmentObjectId, vengeanceResolved.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(OrnnObjectId, vengeanceResolved.State.CardObjects.Keys);
        Assert.DoesNotContain(
            vengeanceResolved.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));

        AssertSnapshotDoesNotExposeOrnnStaticAuraMetadata(
            vengeanceResolved.Snapshots["P1"],
            OrnnObjectId,
            FriendlyBaseEquipmentObjectId);
        AssertSnapshotDoesNotExposeOrnnStaticAuraMetadata(
            vengeanceResolved.Snapshots["P2"],
            OrnnObjectId,
            FriendlyBaseEquipmentObjectId);
    }

    private static async Task<ResolutionResult> PlayOrnnAsync(
        CoreRuleEngine engine,
        MatchState state,
        string cardNo)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(OrnnObjectId, cardNo, []),
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
        Assert.Equal(
            ["cmdType", "cardObjectId", "cardNo", "targetObjectIds", "optionalCosts", "promptId", "snapshotTick"],
            rawCommand.EnumerateObject().Select(property => property.Name).ToArray());
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

    private static StackItemState AssertOrnnStackPriorityState(
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
        Assert.Equal([HandEquipmentObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            [
                FriendlyBaseEquipmentObjectId,
                SecondFriendlyBaseEquipmentObjectId,
                FriendlyUnitObjectId,
                FaceDownEquipmentObjectId,
                DirtyControllerEquipmentObjectId
            ],
            result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([EnemyEquipmentObjectId], result.State.PlayerZones["P2"].Base);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal("STACK", result.State.ObjectLocations[OrnnObjectId].Zone);
        Assert.Equal("P1", result.State.ObjectLocations[OrnnObjectId].PlayerId);
        Assert.Equal("HAND", result.State.ObjectLocations[HandEquipmentObjectId].Zone);
        Assert.Equal("BASE", result.State.ObjectLocations[FriendlyBaseEquipmentObjectId].Zone);
        Assert.Equal("BASE", result.State.ObjectLocations[SecondFriendlyBaseEquipmentObjectId].Zone);
        Assert.Equal("BASE", result.State.ObjectLocations[EnemyEquipmentObjectId].Zone);
        Assert.Equal("P2", result.State.ObjectLocations[EnemyEquipmentObjectId].PlayerId);
        Assert.Equal(OrnnCardNo, result.State.CardObjects[OrnnObjectId].CardNo);
        Assert.Equal([CardObjectTags.UnitCard], result.State.CardObjects[OrnnObjectId].Tags);
        Assert.Equal("P1", result.State.CardObjects[OrnnObjectId].OwnerId);
        Assert.Equal("P1", result.State.CardObjects[OrnnObjectId].ControllerId);
        Assert.False(result.State.CardObjects[OrnnObjectId].IsFaceDown);
        Assert.Equal([CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], result.State.CardObjects[FriendlyBaseEquipmentObjectId].Tags);
        Assert.Equal([CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], result.State.CardObjects[SecondFriendlyBaseEquipmentObjectId].Tags);
        Assert.Equal("P1", result.State.CardObjects[FriendlyBaseEquipmentObjectId].ControllerId);
        Assert.Equal("P1", result.State.CardObjects[SecondFriendlyBaseEquipmentObjectId].ControllerId);
        Assert.DoesNotContain(
            result.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(PromptTypes.StackPriority, result.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PlayCard, result.Prompts["P1"].Actions);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(OrnnObjectId, stackItem.SourceObjectId);
        Assert.Equal(OrnnCardNo, stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal("SFD_ORNN_NO_OPTIONAL_ASSEMBLE_SPELLSHIELD2_PLAY_UNIT", stackItem.EffectKind);
        Assert.Equal(0, stackItem.DamageAmount);
        Assert.Equal(1, stackItem.EffectRepeatCount);
        Assert.False(stackItem.PlayedAfterAnotherCardThisTurn);
        Assert.Equal(string.Empty, stackItem.Destination);
        Assert.Equal("NEUTRAL_OPEN", stackItem.TimingContext);
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

    private static async Task<ResolutionResult> ResolveTopStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-ornn-static-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveTopStackAfterP2PlayAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p2Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-ornn-static-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static MatchState BuildOrnnState(
        string cardNo,
        bool includeFriendlyFieldEquipment)
    {
        var p1Base = new List<string>
        {
            FriendlyUnitObjectId,
            FaceDownEquipmentObjectId,
            DirtyControllerEquipmentObjectId
        };
        var p1Battlefields = new List<string>();
        if (includeFriendlyFieldEquipment)
        {
            p1Base.Insert(0, FriendlyBaseEquipmentObjectId);
            p1Base.Insert(1, SecondFriendlyBaseEquipmentObjectId);
        }

        var p1Hand = new[] { OrnnObjectId, HandEquipmentObjectId };
        var p2Base = new[] { EnemyEquipmentObjectId };
        var objectLocations = p1Hand
            .Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "HAND")))
            .Concat(p1Base.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "BASE"))))
            .Concat(p1Battlefields.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "BATTLEFIELD"))))
            .Concat(p2Base.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P2", "BASE"))))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        return new MatchState(
            "ornn-friendly-equipment-static-power",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            }) with
        {
            Status = MatchStatuses.InProgress,
            ReadyPlayerIds = ["P1", "P2"],
            TurnPlayerId = "P1",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = p1Hand,
                    Base = p1Base,
                    Battlefields = p1Battlefields
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, cardNo, "P1", "P1"),
                [FriendlyUnitObjectId] = Unit(FriendlyUnitObjectId, "SFD·125/221", "P1", "P1"),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [SecondFriendlyBaseEquipmentObjectId] = Equipment(SecondFriendlyBaseEquipmentObjectId, "P1", "P1"),
                [HandEquipmentObjectId] = Equipment(HandEquipmentObjectId, "P1", "P1"),
                [FaceDownEquipmentObjectId] = Equipment(FaceDownEquipmentObjectId, "P1", "P1", isFaceDown: true),
                [DirtyControllerEquipmentObjectId] = Equipment(DirtyControllerEquipmentObjectId, "P1", "P2"),
                [EnemyEquipmentObjectId] = Equipment(EnemyEquipmentObjectId, "P2", "P2")
            },
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BuildOrnnFieldState(
        int ornnPower,
        IReadOnlyList<string>? p1Hand = null,
        IReadOnlyList<string>? p1Base = null,
        IReadOnlyList<string>? p1Graveyard = null,
        IReadOnlyList<string>? p2Base = null,
        Dictionary<string, CardObjectState>? cardObjects = null)
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                Hand = p1Hand ?? [],
                Base = p1Base ?? [OrnnObjectId],
                Graveyard = p1Graveyard ?? []
            },
            ["P2"] = PlayerZones.Empty with
            {
                Base = p2Base ?? []
            }
        };
        var objectLocations = playerZones
            .SelectMany(player => new[]
                {
                    ("HAND", player.Value.Hand),
                    ("BASE", player.Value.Base),
                    ("GRAVEYARD", player.Value.Graveyard)
                }
                .SelectMany(zone => zone.Item2.Select(objectId =>
                    new KeyValuePair<string, ObjectLocationState>(
                        objectId,
                        new ObjectLocationState(player.Key, zone.Item1)))))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        return new MatchState(
            "ornn-friendly-equipment-dynamic-static-power",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            }) with
        {
            Status = MatchStatuses.InProgress,
            ReadyPlayerIds = ["P1", "P2"],
            TurnPlayerId = "P1",
            ActivePlayerId = "P1",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = playerZones,
            CardObjects = cardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: ornnPower)
            },
            ObjectLocations = objectLocations
        };
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        string ownerId,
        string controllerId,
        int power = 0,
        bool isStandby = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: isStandby
                ? [CardObjectTags.UnitCard, CardObjectTags.Standby]
                : [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Equipment(
        string objectId,
        string ownerId,
        string controllerId,
        bool isFaceDown = false,
        string cardNo = "SFD·022/221")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Rune(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "UNL-R01",
            tags: [CardObjectTags.RuneCard, "COLOR:red"],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static bool IsOrnnUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, OrnnObjectId, StringComparison.Ordinal);
    }

    private static void AssertSnapshotPower(
        SnapshotDto snapshot,
        string objectId,
        int basePower,
        int effectivePower)
    {
        var p1View = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var objects = Assert.IsType<Dictionary<string, object?>>(p1View["objects"]);
        var objectView = Assert.IsType<Dictionary<string, object?>>(objects[objectId]);

        Assert.Equal(basePower, Assert.IsType<int>(objectView["basePower"]));
        Assert.Equal(effectivePower, Assert.IsType<int>(objectView["effectivePower"]));
        Assert.Equal(effectivePower, Assert.IsType<int>(objectView["power"]));
    }

    private static Dictionary<string, object?> AssertSnapshotStaticAura(
        SnapshotDto snapshot,
        string sourceObjectId,
        string targetObjectId,
        IReadOnlyList<string> participantObjectIds,
        int powerDelta,
        int basePower,
        int effectivePower)
    {
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
        var effect = Assert.Single(
            continuousEffects,
            effect => string.Equals(Assert.IsType<string>(effect["layer"]), ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect["sourceObjectId"] as string, sourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect["targetObjectId"] as string, targetObjectId, StringComparison.Ordinal));

        Assert.Equal(powerDelta, Assert.IsType<int>(effect["powerDelta"]));
        Assert.Equal(basePower, Assert.IsType<int>(effect["basePower"]));
        Assert.Equal(effectivePower, Assert.IsType<int>(effect["effectivePower"]));
        Assert.Equal(
            "FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER",
            Assert.IsType<string>(effect["effectKind"]));
        Assert.Equal(
            "SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_PUBLIC_FIELD_EQUIPMENT_COUNT",
            Assert.IsType<string>(effect["condition"]));
        Assert.Equal(
            "RECOMPUTED_FROM_CURRENT_AUTHORITATIVE_FIELD_STATE",
            Assert.IsType<string>(effect["lifecycle"]));
        Assert.Equal(
            [sourceObjectId],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(effect["sourceDependencyObjectIds"]));
        Assert.Equal(
            [targetObjectId],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(effect["targetDependencyObjectIds"]));
        if (participantObjectIds.Count == 0)
        {
            Assert.False(effect.ContainsKey("participantObjectIds"));
            Assert.False(effect.ContainsKey("participantDependencyObjectIds"));
            return effect;
        }

        Assert.Equal(
            participantObjectIds,
            Assert.IsAssignableFrom<IReadOnlyList<string>>(effect["participantObjectIds"]));
        Assert.Equal(
            participantObjectIds,
            Assert.IsAssignableFrom<IReadOnlyList<string>>(effect["participantDependencyObjectIds"]));
        return effect;
    }

    private static void AssertSnapshotDoesNotExposeOrnnStaticAuraMetadata(
        SnapshotDto snapshot,
        params string[] hiddenObjectIds)
    {
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
        Assert.DoesNotContain(
            continuousEffects,
            effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect["sourceObjectId"] as string, OrnnObjectId, StringComparison.Ordinal));

        var dependencyKeys = new[]
        {
            "sourceDependencyObjectIds",
            "targetDependencyObjectIds",
            "participantObjectIds",
            "participantDependencyObjectIds"
        };
        foreach (var effect in continuousEffects)
        {
            foreach (var hiddenObjectId in hiddenObjectIds)
            {
                Assert.False(
                    string.Equals(effect["sourceObjectId"] as string, hiddenObjectId, StringComparison.Ordinal)
                    || string.Equals(effect["targetObjectId"] as string, hiddenObjectId, StringComparison.Ordinal),
                    $"Snapshot continuous effect must not reference hidden object id {hiddenObjectId} as source or target.");
            }

            foreach (var dependencyKey in dependencyKeys)
            {
                if (!effect.TryGetValue(dependencyKey, out var objectIdsValue))
                {
                    continue;
                }

                var objectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(objectIdsValue);
                foreach (var hiddenObjectId in hiddenObjectIds)
                {
                    Assert.DoesNotContain(hiddenObjectId, objectIds);
                }
            }
        }
    }

    private static void AssertStaticAuraDoesNotReferenceObjectIds(
        ContinuousEffectState staticAura,
        IReadOnlyList<string> objectIds)
    {
        var objectIdLists = new[]
        {
            staticAura.ParticipantObjectIds ?? [],
            staticAura.SourceDependencyObjectIds ?? [],
            staticAura.TargetDependencyObjectIds ?? [],
            staticAura.ParticipantDependencyObjectIds ?? []
        };
        foreach (var objectIdList in objectIdLists)
        {
            foreach (var objectId in objectIds)
            {
                Assert.DoesNotContain(objectId, objectIdList);
            }
        }
    }

    private static void AssertSnapshotStaticAuraDoesNotReferenceObjectIds(
        Dictionary<string, object?> effect,
        IReadOnlyList<string> objectIds)
    {
        var dependencyKeys = new[]
        {
            "sourceDependencyObjectIds",
            "targetDependencyObjectIds",
            "participantObjectIds",
            "participantDependencyObjectIds"
        };
        foreach (var dependencyKey in dependencyKeys)
        {
            if (!effect.TryGetValue(dependencyKey, out var objectIdsValue))
            {
                continue;
            }

            var viewObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(objectIdsValue);
            foreach (var objectId in objectIds)
            {
                Assert.DoesNotContain(objectId, viewObjectIds);
            }
        }
    }

    private static Dictionary<string, object?> AssertSnapshotStaticAuraMetadata(
        SnapshotDto snapshot,
        ContinuousEffectState staticAura,
        IReadOnlyList<string> expectedResiduals)
    {
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
        var effect = Assert.Single(
            continuousEffects,
            effect => string.Equals(effect["effectId"] as string, staticAura.EffectId, StringComparison.Ordinal));

        Assert.Equal(staticAura.EffectId, Assert.IsType<string>(effect["effectId"]));
        Assert.Equal(staticAura.Scope, Assert.IsType<string>(effect["scope"]));
        Assert.Equal(staticAura.Layer, Assert.IsType<string>(effect["layer"]));
        Assert.Equal(staticAura.Duration, Assert.IsType<string>(effect["duration"]));
        Assert.Equal(staticAura.TargetObjectId, Assert.IsType<string>(effect["targetObjectId"]));
        Assert.Equal(staticAura.SourceObjectId, Assert.IsType<string>(effect["sourceObjectId"]));
        Assert.Equal(staticAura.PowerDelta, Assert.IsType<int>(effect["powerDelta"]));
        Assert.Equal(staticAura.BasePower, Assert.IsType<int>(effect["basePower"]));
        Assert.Equal(staticAura.EffectivePower, Assert.IsType<int>(effect["effectivePower"]));
        Assert.Equal(staticAura.Sequence, Assert.IsType<int>(effect["sequence"]));
        Assert.Equal(staticAura.EffectKind, Assert.IsType<string>(effect["effectKind"]));
        Assert.Equal(staticAura.SourceCardNo, Assert.IsType<string>(effect["sourceCardNo"]));
        Assert.Equal(staticAura.SourcePath, Assert.IsType<string>(effect["sourcePath"]));
        Assert.Equal(staticAura.Condition, Assert.IsType<string>(effect["condition"]));
        Assert.Equal(staticAura.Lifecycle, Assert.IsType<string>(effect["lifecycle"]));
        Assert.Equal("FOUNDATION_ONLY", Assert.IsType<string>(effect["layerEngineStatus"]));
        Assert.Equal(staticAura.SourceOrder.GetValueOrDefault(), Assert.IsType<int>(effect["sourceOrder"]));
        Assert.Equal(staticAura.ParticipantObjectIds, StringList(effect, "participantObjectIds"));
        Assert.Equal(staticAura.SourceDependencyObjectIds, StringList(effect, "sourceDependencyObjectIds"));
        Assert.Equal(staticAura.TargetDependencyObjectIds, StringList(effect, "targetDependencyObjectIds"));
        Assert.Equal(staticAura.ParticipantDependencyObjectIds, StringList(effect, "participantDependencyObjectIds"));
        Assert.Equal(expectedResiduals, StringList(effect, "deferredLayerEngineResiduals"));
        Assert.False(effect.ContainsKey("requestedPowerDelta"));
        Assert.False(effect.ContainsKey("appliedPowerDelta"));
        Assert.False(effect.ContainsKey("minimumPower"));
        Assert.False(effect.ContainsKey("resultingPower"));
        Assert.False(effect.ContainsKey("appliedOrder"));

        return effect;
    }

    private static IReadOnlyList<string> StringList(Dictionary<string, object?> view, string key)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(view[key]);
    }

    private static string StaticAuraMetadataSignature(Dictionary<string, object?> view)
    {
        return string.Join(
            "|",
            Assert.IsType<string>(view["effectId"]),
            Assert.IsType<string>(view["scope"]),
            Assert.IsType<string>(view["layer"]),
            Assert.IsType<string>(view["duration"]),
            Assert.IsType<string>(view["targetObjectId"]),
            Assert.IsType<string>(view["sourceObjectId"]),
            Assert.IsType<int>(view["powerDelta"]).ToString(),
            Assert.IsType<int>(view["basePower"]).ToString(),
            Assert.IsType<int>(view["effectivePower"]).ToString(),
            Assert.IsType<int>(view["sequence"]).ToString(),
            Assert.IsType<int>(view["sourceOrder"]).ToString(),
            Assert.IsType<string>(view["effectKind"]),
            Assert.IsType<string>(view["sourceCardNo"]),
            Assert.IsType<string>(view["sourcePath"]),
            Assert.IsType<string>(view["condition"]),
            Assert.IsType<string>(view["lifecycle"]),
            Assert.IsType<string>(view["layerEngineStatus"]),
            string.Join(",", StringList(view, "participantObjectIds")),
            string.Join(",", StringList(view, "sourceDependencyObjectIds")),
            string.Join(",", StringList(view, "targetDependencyObjectIds")),
            string.Join(",", StringList(view, "participantDependencyObjectIds")),
            string.Join(",", StringList(view, "deferredLayerEngineResiduals")));
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
