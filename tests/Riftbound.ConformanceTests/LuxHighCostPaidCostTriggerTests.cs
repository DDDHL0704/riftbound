using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LuxHighCostPaidCostTriggerTests
{
    private const string LuxUnitObjectId = "P1-LUX";
    private const string LuxUnitCardNo = "OGS·006/024";
    private const string LuxLegendObjectId = "P1-LEGEND-LUX";
    private const string LuxLegendCardNo = "OGS·021/024";
    private const string HiddenDrawObjectId = "P1-DRAW-HIDDEN";
    private const string HiddenDrawCardNo = "SFD·106/221";
    private const string HighPrintedSpellObjectId = "P1-SPELL-EVOLUTION-DAY";
    private const string HighPrintedSpellCardNo = "OGN·114/298";
    private const string LowerPrintedSpellObjectId = "P1-SPELL-CRESCENT-STRIKE";
    private const string LowerPrintedSpellCardNo = "UNL-072/219";
    private const string LowerPrintedSpellEffectKind = "CRESCENT_STRIKE_DAMAGE_TARGET_4_OTHER_ENEMY_BATTLEFIELD_1";
    private const string SpellshieldTargetObjectId = "P2-ORNN-SPELLSHIELD-2";
    private const string LuxUnitHighCostEffectKind = "OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3";
    private const string LuxLegendHighCostTrigger = TriggerKinds.LegendHighCostSpellDrawOne;
    private const string RagingDrakeReductionEffectId =
        "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:P1-UNIT-RAGING-DRAKE";

    [Fact]
    public async Task LuxPaidCostHighPrintedSpellReducedBelowThresholdDoesNotTriggerUnitOrLegend()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxPaidCostState(
            HighPrintedSpellObjectId,
            HighPrintedSpellCardNo,
            mana: 1,
            untilEndOfTurnEffects: [RagingDrakeReductionEffectId],
            includeDeck: true);

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-paid-cost-reduced-high-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(HighPrintedSpellObjectId, HighPrintedSpellCardNo, []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var costPaid = AssertSingleCostPaid(result);
        Assert.Equal(6, Assert.IsType<int>(costPaid.Payload["baseMana"]));
        Assert.Equal(1, Assert.IsType<int>(costPaid.Payload["mana"]));
        Assert.Equal(5, Assert.IsType<int>(costPaid.Payload["nextSpellCostReductionMana"]));
        AssertLuxUnitDidNotTrigger(result);
        AssertLuxLegendDidNotTrigger(result);
        AssertLuxPowerUnchanged(result);
        Assert.Equal([HiddenDrawObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LuxPaidCostLowerPrintedSpellRaisedBySpellshieldTaxTriggersUnitAndLegend()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxPaidCostState(
            LowerPrintedSpellObjectId,
            LowerPrintedSpellCardNo,
            mana: 5,
            includeSpellshieldTarget: true,
            includeDeck: true);

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-paid-cost-taxed-low-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(LowerPrintedSpellObjectId, LowerPrintedSpellCardNo, [SpellshieldTargetObjectId]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var costPaid = AssertSingleCostPaid(result);
        Assert.Equal(3, Assert.IsType<int>(costPaid.Payload["baseMana"]));
        Assert.Equal(5, Assert.IsType<int>(costPaid.Payload["mana"]));
        Assert.Equal(2, Assert.IsType<int>(costPaid.Payload["spellshieldTaxMana"]));
        Assert.Equal(
            [SpellshieldTargetObjectId],
            Assert.IsType<string[]>(costPaid.Payload["spellshieldTaxTargetObjectIds"]));

        AssertLuxUnitTriggered(result);
        var lux = result.State.CardObjects[LuxUnitObjectId];
        Assert.Equal(8, lux.Power);
        Assert.Equal(3, lux.UntilEndOfTurnPowerModifier);

        var legendTrigger = Assert.Single(result.Events, IsLuxLegendTriggerEvent);
        Assert.Equal(
            ["legendCardNo", "playedCardManaCost", "playedCardNo", "playerId", "trigger"],
            legendTrigger.Payload.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(LuxLegendCardNo, Assert.IsType<string>(legendTrigger.Payload["legendCardNo"]));
        Assert.Equal(3, Assert.IsType<int>(legendTrigger.Payload["playedCardManaCost"]));

        var drawEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(["count", "playerId"], drawEvent.Payload.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(1, Assert.IsType<int>(drawEvent.Payload["count"]));
        Assert.Equal([HiddenDrawObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);

        var opponentSnapshot = JsonSerializer.Serialize(result.Snapshots["P2"]);
        Assert.DoesNotContain(HiddenDrawCardNo, opponentSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenDrawObjectId, opponentSnapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void LuxPaidCostLowerPrintedSpellPromptExposesOnlySpellshieldEnemyBattlefieldTarget()
    {
        var state = BuildLuxPaidCostState(
            LowerPrintedSpellObjectId,
            LowerPrintedSpellCardNo,
            mana: 5,
            includeSpellshieldTarget: true,
            includeDeck: true);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Equal([LowerPrintedSpellObjectId], (playCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());

        var candidateTargetIds = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(playCandidate.Targets)
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Equal([SpellshieldTargetObjectId], candidateTargetIds);

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, LowerPrintedSpellObjectId, StringComparison.Ordinal));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var metadataTargetIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(targetChoicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Equal([SpellshieldTargetObjectId], metadataTargetIds);

        foreach (var illegalTargetId in new[]
        {
            LuxUnitObjectId,
            LuxLegendObjectId,
            HiddenDrawObjectId,
            HighPrintedSpellObjectId
        })
        {
            Assert.DoesNotContain(illegalTargetId, candidateTargetIds);
            Assert.DoesNotContain(illegalTargetId, metadataTargetIds);
        }
    }

    [Fact]
    public async Task LuxPaidCostPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildLuxPaidCostState(
            LowerPrintedSpellObjectId,
            LowerPrintedSpellCardNo,
            mana: 5,
            includeSpellshieldTarget: true,
            includeDeck: true);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            LowerPrintedSpellObjectId,
            LowerPrintedSpellCardNo,
            [SpellshieldTargetObjectId]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, LowerPrintedSpellObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-lux-paid-cost-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-lux-paid-cost-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        var costPaid = AssertSingleCostPaid(accepted);
        Assert.Equal(3, Assert.IsType<int>(costPaid.Payload["baseMana"]));
        Assert.Equal(5, Assert.IsType<int>(costPaid.Payload["mana"]));
        Assert.Equal(2, Assert.IsType<int>(costPaid.Payload["spellshieldTaxMana"]));
        AssertLuxUnitTriggered(accepted);
        AssertLuxLegendTriggered(accepted);
        var drawEvent = Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(1, Assert.IsType<int>(drawEvent.Payload["count"]));
        var stackAdded = Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Equal(LowerPrintedSpellEffectKind, Assert.IsType<string>(stackAdded.Payload["effectKind"]));
        var acceptedStackItem = AssertLuxPaidCostStackPriorityState(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedStackHash = MatchStateHasher.HashValue(accepted.State.StackItems);
        var acceptedZonesHash = MatchStateHasher.HashValue(accepted.State.PlayerZones);
        var acceptedRunePoolsHash = MatchStateHasher.HashValue(accepted.State.RunePools);
        var acceptedObjectLocationsHash = MatchStateHasher.HashValue(accepted.State.ObjectLocations);
        var acceptedLuxHash = MatchStateHasher.HashValue(accepted.State.CardObjects[LuxUnitObjectId]);
        var acceptedDrawObjectHash = MatchStateHasher.HashValue(accepted.State.CardObjects[HiddenDrawObjectId]);
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
        Assert.Equal(acceptedZonesHash, MatchStateHasher.HashValue(replay.State.PlayerZones));
        Assert.Equal(acceptedRunePoolsHash, MatchStateHasher.HashValue(replay.State.RunePools));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(replay.State.ObjectLocations));
        Assert.Equal(acceptedLuxHash, MatchStateHasher.HashValue(replay.State.CardObjects[LuxUnitObjectId]));
        Assert.Equal(acceptedDrawObjectHash, MatchStateHasher.HashValue(replay.State.CardObjects[HiddenDrawObjectId]));
        AssertLuxPaidCostStackPriorityState(replay, acceptedStackItem);
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
        Assert.Equal(acceptedZonesHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones));
        Assert.Equal(acceptedRunePoolsHash, MatchStateHasher.HashValue(duplicateReplay.State.RunePools));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(duplicateReplay.State.ObjectLocations));
        Assert.Equal(acceptedLuxHash, MatchStateHasher.HashValue(duplicateReplay.State.CardObjects[LuxUnitObjectId]));
        Assert.Equal(acceptedDrawObjectHash, MatchStateHasher.HashValue(duplicateReplay.State.CardObjects[HiddenDrawObjectId]));
        AssertLuxPaidCostStackPriorityState(duplicateReplay, acceptedStackItem);
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
        Assert.Equal(acceptedZonesHash, MatchStateHasher.HashValue(conflict.State.PlayerZones));
        Assert.Equal(acceptedRunePoolsHash, MatchStateHasher.HashValue(conflict.State.RunePools));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(conflict.State.ObjectLocations));
        Assert.Equal(acceptedLuxHash, MatchStateHasher.HashValue(conflict.State.CardObjects[LuxUnitObjectId]));
        Assert.Equal(acceptedDrawObjectHash, MatchStateHasher.HashValue(conflict.State.CardObjects[HiddenDrawObjectId]));
        AssertLuxPaidCostStackPriorityState(conflict, acceptedStackItem);
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
    public async Task LuxPaidCostRejectedSpellshieldTaxPathLeavesStateUnmutated()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxPaidCostState(
            LowerPrintedSpellObjectId,
            LowerPrintedSpellCardNo,
            mana: 4,
            includeSpellshieldTarget: true,
            includeDeck: true);

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-paid-cost-tax-rejected", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(LowerPrintedSpellObjectId, LowerPrintedSpellCardNo, [SpellshieldTargetObjectId]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Equal([LowerPrintedSpellObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Equal([HiddenDrawObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(4, result.State.RunePools["P1"].Mana);
        Assert.Empty(result.State.StackItems);
        AssertLuxPowerUnchanged(result);
        AssertLuxUnitDidNotTrigger(result);
        AssertLuxLegendDidNotTrigger(result);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    private static MatchState BuildLuxPaidCostState(
        string spellObjectId,
        string spellCardNo,
        int mana,
        IReadOnlyList<string>? untilEndOfTurnEffects = null,
        bool includeSpellshieldTarget = false,
        bool includeDeck = false)
    {
        var p1MainDeck = includeDeck ? new[] { HiddenDrawObjectId } : [];
        var p2Battlefields = includeSpellshieldTarget ? new[] { SpellshieldTargetObjectId } : [];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [LuxUnitObjectId] = new(
                LuxUnitObjectId,
                power: 5,
                tags: [CardObjectTags.UnitCard],
                manaCost: 6,
                cardNo: LuxUnitCardNo,
                ownerId: "P1",
                controllerId: "P1"),
            [LuxLegendObjectId] = new(
                LuxLegendObjectId,
                cardNo: LuxLegendCardNo,
                ownerId: "P1",
                controllerId: "P1"),
            [spellObjectId] = new(
                spellObjectId,
                cardNo: spellCardNo,
                ownerId: "P1",
                controllerId: "P1")
        };
        if (includeDeck)
        {
            cardObjects[HiddenDrawObjectId] = new(
                HiddenDrawObjectId,
                cardNo: HiddenDrawCardNo,
                ownerId: "P1",
                controllerId: "P1");
        }

        if (includeSpellshieldTarget)
        {
            cardObjects[SpellshieldTargetObjectId] = new(
                SpellshieldTargetObjectId,
                power: 6,
                tags: [CardObjectTags.UnitCard, "法盾2"],
                manaCost: 6,
                cardNo: "SFD·085/221",
                ownerId: "P2",
                controllerId: "P2");
        }

        return new MatchState(
            "lux-paid-cost-room",
            81,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
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
                    MainDeck = p1MainDeck,
                    Hand = [spellObjectId],
                    Base = [LuxUnitObjectId],
                    LegendZone = [LuxLegendObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            seed: 404,
            untilEndOfTurnEffects: untilEndOfTurnEffects ?? [],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
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

    private static GameEvent AssertSingleCostPaid(ResolutionResult result)
    {
        return Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
    }

    private static GameEvent AssertLuxLegendTriggered(ResolutionResult result)
    {
        var legendTrigger = Assert.Single(result.Events, IsLuxLegendTriggerEvent);
        Assert.Equal(
            ["legendCardNo", "playedCardManaCost", "playedCardNo", "playerId", "trigger"],
            legendTrigger.Payload.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(LuxLegendCardNo, Assert.IsType<string>(legendTrigger.Payload["legendCardNo"]));
        Assert.Equal(LowerPrintedSpellCardNo, Assert.IsType<string>(legendTrigger.Payload["playedCardNo"]));
        Assert.Equal(3, Assert.IsType<int>(legendTrigger.Payload["playedCardManaCost"]));
        Assert.Equal("P1", Assert.IsType<string>(legendTrigger.Payload["playerId"]));
        Assert.Equal(LuxLegendHighCostTrigger, Assert.IsType<string>(legendTrigger.Payload["trigger"]));
        return legendTrigger;
    }

    private static StackItemState AssertLuxPaidCostStackPriorityState(
        ResolutionResult result,
        StackItemState? expectedStackItem = null)
    {
        Assert.Equal(82, result.State.Tick);
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
        Assert.Equal([HiddenDrawObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal([LuxUnitObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal([LuxLegendObjectId], result.State.PlayerZones["P1"].LegendZone);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([SpellshieldTargetObjectId], result.State.PlayerZones["P2"].Battlefields);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal("STACK", result.State.ObjectLocations[LowerPrintedSpellObjectId].Zone);
        Assert.Equal("P1", result.State.ObjectLocations[LowerPrintedSpellObjectId].PlayerId);
        Assert.Equal("HAND", result.State.ObjectLocations[HiddenDrawObjectId].Zone);
        Assert.Equal("P1", result.State.ObjectLocations[HiddenDrawObjectId].PlayerId);
        Assert.Equal(PromptTypes.StackPriority, result.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PlayCard, result.Prompts["P1"].Actions);

        var lux = result.State.CardObjects[LuxUnitObjectId];
        Assert.Equal(8, lux.Power);
        Assert.Equal(3, lux.UntilEndOfTurnPowerModifier);
        Assert.Equal(0, result.State.CardObjects[SpellshieldTargetObjectId].Damage);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(LowerPrintedSpellObjectId, stackItem.SourceObjectId);
        Assert.Equal(LowerPrintedSpellCardNo, stackItem.CardNo);
        Assert.Equal([SpellshieldTargetObjectId], stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal(LowerPrintedSpellEffectKind, stackItem.EffectKind);
        Assert.Equal(4, stackItem.DamageAmount);
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

    private static void AssertLuxUnitTriggered(ResolutionResult result)
    {
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("effectKind", out var effectKind) ? effectKind as string : null, LuxUnitHighCostEffectKind, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("sourceObjectId", out var sourceObjectId) ? sourceObjectId as string : null, LuxUnitObjectId, StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("sourceObjectId", out var sourceObjectId) ? sourceObjectId as string : null, LuxUnitObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("targetObjectId", out var targetObjectId) ? targetObjectId as string : null, LuxUnitObjectId, StringComparison.Ordinal)
            && Equals(gameEvent.Payload["appliedPowerDelta"], 3));
    }

    private static void AssertLuxUnitDidNotTrigger(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Payload.TryGetValue("effectKind", out var effectKind) ? effectKind as string : null, LuxUnitHighCostEffectKind, StringComparison.Ordinal)
            || string.Equals(gameEvent.Payload.TryGetValue("trigger", out var trigger) ? trigger as string : null, LuxUnitHighCostEffectKind, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("sourceObjectId", out var sourceObjectId) ? sourceObjectId as string : null, LuxUnitObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("targetObjectId", out var targetObjectId) ? targetObjectId as string : null, LuxUnitObjectId, StringComparison.Ordinal));
    }

    private static void AssertLuxLegendDidNotTrigger(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, IsLuxLegendTriggerEvent);
    }

    private static bool IsLuxLegendTriggerEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(
                gameEvent.Payload.TryGetValue("trigger", out var trigger) ? trigger as string : null,
                LuxLegendHighCostTrigger,
                StringComparison.Ordinal);
    }

    private static void AssertLuxPowerUnchanged(ResolutionResult result)
    {
        var lux = result.State.CardObjects[LuxUnitObjectId];
        Assert.Equal(5, lux.Power);
        Assert.Equal(0, lux.UntilEndOfTurnPowerModifier);
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
