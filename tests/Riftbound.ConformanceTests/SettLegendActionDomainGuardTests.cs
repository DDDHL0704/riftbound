using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SettLegendActionDomainGuardTests
{
    [Fact]
    public async Task SettLegendActivePaysOneAndRecallsBoonUnitInsteadOfDestroyingIt()
    {
        var state = SettDestroyReplacementState("OGN·269/298", "P1-LEGEND-SETT", mana: 1, legendExhausted: false, boonAttacker: true);

        var result = await DeclareSettBattleAsync(state, "intent-sett-legend-replacement-positive");

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects["P1-LEGEND-SETT"].IsExhausted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(["P1-SETT-BOON-ATTACKER"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);

        var recalledUnit = result.State.CardObjects["P1-SETT-BOON-ATTACKER"];
        Assert.True(recalledUnit.IsExhausted);
        Assert.Equal(1, recalledUnit.Power);
        Assert.Equal(0, recalledUnit.Damage);
        Assert.DoesNotContain(CardObjectTags.Boon, recalledUnit.Tags);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BOON_UNIT_DESTROYED_PAY_1_RECALL_EXHAUSTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-SETT-BOON-ATTACKER", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["mana"]) == 1
            && string.Equals(gameEvent.Payload["reason"] as string, "BOON_UNIT_DESTROYED_PAY_1_RECALL_EXHAUSTED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BOON_CONSUMED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_RECALLED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["replacementEffectId"] as string, "SETT_BOON_UNIT_DESTROYED_RECALL_EXHAUSTED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-SETT-BOON-ATTACKER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SettLegendDeclareBattleStalePromptReplayUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = SettDestroyReplacementState("OGN·269/298", "P1-LEGEND-SETT", mana: 1, legendExhausted: false, boonAttacker: true);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new DeclareBattleCommand(
            "BATTLEFIELD:P1-MAIN",
            state.PlayerZones["P1"].Battlefields,
            state.PlayerZones["P2"].Battlefields,
            ["COMBAT_ASSIGNMENT"]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.DeclareBattle, prompt.Actions);
        var staleRawCommand = PromptScopedDeclareBattleRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedDeclareBattleRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-sett-declare-battle-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-sett-declare-battle-stale-prompt-replay";
        var initialStateHash = MatchStateHasher.Hash(state);

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.NotEmpty(accepted.Events);
        Assert.NotEqual(initialStateHash, MatchStateHasher.Hash(accepted.State));
        AssertSettReplacementAcceptedState(accepted.State);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, accepted.Prompts["P1"].Actions);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedPlayerZonesHash = MatchStateHasher.HashValue(accepted.State.PlayerZones);
        var acceptedObjectLocationsHash = MatchStateHasher.HashValue(accepted.State.ObjectLocations);
        var acceptedLegendHash = MatchStateHasher.HashValue(accepted.State.CardObjects["P1-LEGEND-SETT"]);
        var acceptedBoonHash = MatchStateHasher.HashValue(accepted.State.CardObjects["P1-SETT-BOON-ATTACKER"]);
        var p1PromptAfterAccepted = MatchStateHasher.HashValue(session.PromptFor("P1"));
        var p2PromptAfterAccepted = MatchStateHasher.HashValue(session.PromptFor("P2"));
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.DeclareBattle, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(acceptedJournalEntry.RawCommand.Value));
        AssertPromptScopedDeclareBattleRawCommand(acceptedJournalEntry.RawCommand.Value, command, prompt);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        AssertSettRejectedReplayDidNotMutate(
            replay,
            acceptedStateHash,
            acceptedPlayerZonesHash,
            acceptedObjectLocationsHash,
            acceptedLegendHash,
            acceptedBoonHash);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = Assert.Single(journal.Entries, entry => !entry.Accepted);
        Assert.Equal(journal.Entries[1], rejectedJournalEntry);
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.DeclareBattle, rejectedJournalEntry.CommandType);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(MatchStateHasher.HashValue(replay.Prompts), MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(MatchStateHasher.HashValue(replay.Snapshots), MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(rejectedJournalEntry.RawCommand.Value));
        AssertPromptScopedDeclareBattleRawCommand(rejectedJournalEntry.RawCommand.Value, command, prompt);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));
        var journalHashAfterReplay = MatchStateHasher.HashValue(journal.Entries);

        var duplicateReplay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        AssertSettRejectedReplayDidNotMutate(
            duplicateReplay,
            acceptedStateHash,
            acceptedPlayerZonesHash,
            acceptedObjectLocationsHash,
            acceptedLegendHash,
            acceptedBoonHash);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateReplay.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateReplay.ErrorMessage);
        Assert.Equal(replay.State.Tick, duplicateReplay.State.Tick);
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

        AssertSettRejectedReplayDidNotMutate(
            conflict,
            acceptedStateHash,
            acceptedPlayerZonesHash,
            acceptedObjectLocationsHash,
            acceptedLegendHash,
            acceptedBoonHash);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
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
    [InlineData("OGN·269/298", "P1-LEGEND-SETT-NO-MANA", 0, false, true)]
    [InlineData("OGN·269/298", "P1-LEGEND-SETT-EXHAUSTED", 1, true, true)]
    [InlineData("OGN·269/298", "P1-LEGEND-SETT-NON-BOON", 1, false, false)]
    [InlineData("OGN·310*/298", "P1-LEGEND-SETT-ALT", 0, false, true)]
    public async Task SettLegendReplacementSkipsInvalidRepresentativeCases(
        string legendCardNo,
        string legendObjectId,
        int mana,
        bool legendExhausted,
        bool boonAttacker)
    {
        var state = SettDestroyReplacementState(legendCardNo, legendObjectId, mana, legendExhausted, boonAttacker);

        var result = await DeclareSettBattleAsync(state, $"intent-sett-legend-replacement-skip-{legendObjectId}");

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(mana, result.State.RunePools["P1"].Mana);
        Assert.Equal(legendExhausted, result.State.CardObjects[legendObjectId].IsExhausted);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-SETT-BOON-ATTACKER"], result.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BOON_UNIT_DESTROYED_PAY_1_RECALL_EXHAUSTED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BOON_CONSUMED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_RECALLED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["replacementEffectId"] as string, "SETT_BOON_UNIT_DESTROYED_RECALL_EXHAUSTED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-SETT-BOON-ATTACKER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SettLegendExhaustedReprintReadiesOnConquer()
    {
        var state = SettConquerState("OGN·310/298", "P1-LEGEND-SETT-REPRINT", legendExhausted: true);

        var result = await DeclareSettBattleAsync(state, "intent-sett-legend-conquer-ready");

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.False(result.State.CardObjects["P1-LEGEND-SETT-REPRINT"].IsExhausted);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TriggerKinds.LegendConquestReadySelf, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["legendObjectId"] as string, "P1-LEGEND-SETT-REPRINT", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-LEGEND-SETT-REPRINT", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> DeclareSettBattleAsync(MatchState state, string intentId)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent(intentId, "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                state.PlayerZones["P1"].Battlefields,
                state.PlayerZones["P2"].Battlefields,
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
    }

    private static void AssertSettReplacementAcceptedState(MatchState state)
    {
        Assert.Equal(0, state.RunePools["P1"].Mana);
        Assert.Equal(["P1-SETT-BOON-ATTACKER"], state.PlayerZones["P1"].Base);
        Assert.Empty(state.PlayerZones["P1"].Battlefields);
        Assert.Empty(state.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1-LEGEND-SETT"], state.PlayerZones["P1"].LegendZone);
        Assert.Equal(["P2-SETT-DEFENDER"], state.PlayerZones["P2"].Battlefields);

        var legend = state.CardObjects["P1-LEGEND-SETT"];
        Assert.Equal("OGN·269/298", legend.CardNo);
        Assert.True(legend.IsExhausted);

        var recalledUnit = state.CardObjects["P1-SETT-BOON-ATTACKER"];
        Assert.Equal("P1-SETT-BOON-ATTACKER", recalledUnit.ObjectId);
        Assert.Equal("P1", recalledUnit.OwnerId);
        Assert.Equal("P1", recalledUnit.ControllerId);
        Assert.True(recalledUnit.IsExhausted);
        Assert.Equal(1, recalledUnit.Power);
        Assert.Equal(0, recalledUnit.Damage);
        Assert.Contains(CardObjectTags.UnitCard, recalledUnit.Tags);
        Assert.DoesNotContain(CardObjectTags.Boon, recalledUnit.Tags);
    }

    private static void AssertSettRejectedReplayDidNotMutate(
        ResolutionResult result,
        string acceptedStateHash,
        string acceptedPlayerZonesHash,
        string acceptedObjectLocationsHash,
        string acceptedLegendHash,
        string acceptedBoonHash)
    {
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(acceptedPlayerZonesHash, MatchStateHasher.HashValue(result.State.PlayerZones));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(result.State.ObjectLocations));
        Assert.Equal(acceptedLegendHash, MatchStateHasher.HashValue(result.State.CardObjects["P1-LEGEND-SETT"]));
        Assert.Equal(acceptedBoonHash, MatchStateHasher.HashValue(result.State.CardObjects["P1-SETT-BOON-ATTACKER"]));
        AssertSettReplacementAcceptedState(result.State);
    }

    private static JsonElement PromptScopedDeclareBattleRawCommand(
        DeclareBattleCommand command,
        ActionPromptDto prompt)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            cmdType = CommandTypes.DeclareBattle,
            battlefieldId = command.BattlefieldId,
            attackerObjectIds = command.AttackerObjectIds,
            defenderObjectIds = command.DefenderObjectIds,
            optionalCosts = command.OptionalCosts,
            battlefieldTargetObjectIds = command.BattlefieldTargetObjectIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        }));
        return document.RootElement.Clone();
    }

    private static JsonElement PromptScopedDeclareBattleRawCommandWithClientNote(
        DeclareBattleCommand command,
        ActionPromptDto prompt,
        string clientNote)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            cmdType = CommandTypes.DeclareBattle,
            battlefieldId = command.BattlefieldId,
            attackerObjectIds = command.AttackerObjectIds,
            defenderObjectIds = command.DefenderObjectIds,
            optionalCosts = command.OptionalCosts,
            battlefieldTargetObjectIds = command.BattlefieldTargetObjectIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote
        }));
        return document.RootElement.Clone();
    }

    private static void AssertPromptScopedDeclareBattleRawCommand(
        JsonElement rawCommand,
        DeclareBattleCommand command,
        ActionPromptDto prompt)
    {
        Assert.Equal(CommandTypes.DeclareBattle, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(command.BattlefieldId, rawCommand.GetProperty("battlefieldId").GetString());
        Assert.Equal(
            command.AttackerObjectIds,
            rawCommand.GetProperty("attackerObjectIds").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal(
            command.DefenderObjectIds,
            rawCommand.GetProperty("defenderObjectIds").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal(
            command.OptionalCosts,
            rawCommand.GetProperty("optionalCosts").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal(JsonValueKind.Null, rawCommand.GetProperty("battlefieldTargetObjectIds").ValueKind);
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));
    }

    private static MatchState SettDestroyReplacementState(
        string legendCardNo,
        string legendObjectId,
        int mana,
        bool legendExhausted,
        bool boonAttacker)
    {
        return BaseState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-SETT-BOON-ATTACKER"],
                    LegendZone = [legendObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SETT-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SETT-BOON-ATTACKER"] = new(
                    "P1-SETT-BOON-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: boonAttacker ? [CardObjectTags.UnitCard, CardObjectTags.Boon] : [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                [legendObjectId] = new(
                    legendObjectId,
                    cardNo: legendCardNo,
                    isExhausted: legendExhausted,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-SETT-DEFENDER"] = new(
                    "P2-SETT-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState SettConquerState(
        string legendCardNo,
        string legendObjectId,
        bool legendExhausted)
    {
        return BaseState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-SETT-ATTACKER"],
                    LegendZone = [legendObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SETT-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SETT-ATTACKER"] = new(
                    "P1-SETT-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                [legendObjectId] = new(
                    legendObjectId,
                    cardNo: legendCardNo,
                    isExhausted: legendExhausted,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-SETT-DEFENDER"] = new(
                    "P2-SETT-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BaseState(int mana)
    {
        return new MatchState(
            roomId: "sett-legend-action-domain-guard-test",
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
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal));
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
